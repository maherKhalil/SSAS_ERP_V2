using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

public sealed class EfUnitOfWork<TDbContext>(
  TDbContext dbContext,
  IDomainEventDispatcher domainEventDispatcher,
  ILogger<EfUnitOfWork<TDbContext>> logger) : IUnitOfWork
  where TDbContext : PersistenceDbContext
{
  // ==================================================================================================
  // ⚠ THE LAST LINE OF "A COMMITTED COMMAND MUST NOT BE REPORTED AS FAILED" (item 175).
  // ==================================================================================================
  //
  // `DomainEventDispatcher` isolates every consumer and does not throw, so in the normal case nothing
  // reaches this catch. It exists for that contract being BROKEN -- by a future dispatcher, or by a
  // failure in dispatch itself rather than in a consumer.
  //
  // ⚠ ITEM 173 PROVED THE ORDERING ALONE IS NOT ENOUGH, AND A PLANT THAT REDDENED NOTHING IS WHAT SHOWED
  // IT. Putting dispatch back inside the `try` changed no test, because the guarantee lived on a contract
  // nothing enforced rather than on the ordering. A dispatcher throwing OUTRIGHT still reached the caller
  // over a durable write until this catch existed.
  //
  // Both dispatch sites are guarded, not just the transactional one: `SaveChangesAsync` outside a
  // transaction has also already written by the time it dispatches.
  private static readonly Action<ILogger, Exception?> LogDispatchFailure = LoggerMessage.Define(
    LogLevel.Error,
    new EventId(1, nameof(LogDispatchFailure)),
    "Domain event dispatch failed after a successful write. The command succeeded and the data is " +
    "durable; consumers may not have run.");

  private IDbContextTransaction? transaction;

  public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var changes = await dbContext.SaveChangesAsync(cancellationToken);
    if (transaction is null)
    {
      await DispatchAfterCommitAsync(cancellationToken);
    }

    return changes;
  }

  public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (transaction is not null)
    {
      throw new InvalidOperationException("Nested transactions are not supported.");
    }

    transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    return new EfTransaction(this, transaction);
  }

  private async Task CommitAsync(IDbContextTransaction currentTransaction, CancellationToken cancellationToken)
  {
    EnsureActiveTransaction(currentTransaction);

    // ==============================================================================================
    // DISPATCH IS OUTSIDE THE TRY, BECAUSE BY THEN THE WRITE IS DURABLE (item 173).
    // ==============================================================================================
    //
    // It used to sit inside, and three failures followed from that. A consumer that threw reached the
    // `catch`, which called `RollbackAsync` on an ALREADY-COMMITTED transaction; that throws, and the
    // provider error propagated INSTEAD of the consumer's, so `throw;` was never reached and the real
    // cause was destroyed. The caller was then told a committed command had failed.
    //
    // `DEC` reasoning, from `ADR-009` and item 166: dispatch happens after commit because an event
    // announcing rolled-back work is worse than no event. **The symmetric statement is that a committed
    // write must not be reported as failed**, which is what this ordering makes true.
    try
    {
      await SaveChangesAsync(cancellationToken);
      await currentTransaction.CommitAsync(cancellationToken);
    }
    catch
    {
      await currentTransaction.RollbackAsync(CancellationToken.None);
      throw;
    }
    finally
    {
      transaction = null;
      await currentTransaction.DisposeAsync();
    }

    // Past this point the commit has succeeded, so nothing here may fail the command.
    await DispatchAfterCommitAsync(cancellationToken);
  }

  private async Task RollbackAsync(IDbContextTransaction currentTransaction, CancellationToken cancellationToken)
  {
    EnsureActiveTransaction(currentTransaction);

    try
    {
      await currentTransaction.RollbackAsync(cancellationToken);
    }
    finally
    {
      transaction = null;
      await currentTransaction.DisposeAsync();
    }
  }

  private async Task DispatchAfterCommitAsync(CancellationToken cancellationToken)
  {
    try
    {
      await DispatchDomainEventsAsync(cancellationToken);
    }
    catch (Exception exception)
    {
      LogDispatchFailure(logger, exception);
    }
  }

  private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
  {
    var aggregates = dbContext.ChangeTracker.Entries()
      .Select(entry => entry.Entity)
      .OfType<IHasDomainEvents>()
      .Where(aggregate => aggregate.DomainEvents.Count > 0)
      .ToArray();
    var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToArray();

    if (domainEvents.Length == 0)
    {
      return;
    }

    await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
    foreach (var aggregate in aggregates)
    {
      aggregate.ClearDomainEvents();
    }
  }

  private void EnsureActiveTransaction(IDbContextTransaction currentTransaction)
  {
    if (!ReferenceEquals(transaction, currentTransaction))
    {
      throw new InvalidOperationException("The transaction is no longer active.");
    }
  }

  private sealed class EfTransaction(EfUnitOfWork<TDbContext> unitOfWork, IDbContextTransaction transaction) : ITransaction
  {
    private bool completed;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
      if (completed)
      {
        throw new InvalidOperationException("The transaction has already completed.");
      }

      // ⚠ MARKED BEFORE THE ATTEMPT, NOT AFTER IT (item 173). `EfUnitOfWork.CommitAsync` disposes this
      // transaction and clears its field in a `finally`, so the transaction is finished with WHETHER OR
      // NOT the commit succeeded. Setting the flag afterwards meant a failed commit left `completed`
      // false, and disposal then attempted a rollback that `EnsureActiveTransaction` refused -- a second
      // exception which, in an `await using` block, REPLACES the one the body was propagating.
      completed = true;
      await unitOfWork.CommitAsync(transaction, cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
      if (completed)
      {
        throw new InvalidOperationException("The transaction has already completed.");
      }

      completed = true;
      await unitOfWork.RollbackAsync(transaction, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
      if (!completed)
      {
        await RollbackAsync();
      }
    }
  }
}

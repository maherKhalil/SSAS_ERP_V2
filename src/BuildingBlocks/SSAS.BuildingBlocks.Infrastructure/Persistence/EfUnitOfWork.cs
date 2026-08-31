using Microsoft.EntityFrameworkCore.Storage;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

public sealed class EfUnitOfWork<TDbContext>(TDbContext dbContext, IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork
  where TDbContext : PersistenceDbContext
{
  private IDbContextTransaction? transaction;

  public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var changes = await dbContext.SaveChangesAsync(cancellationToken);
    if (transaction is null)
    {
      await DispatchDomainEventsAsync(cancellationToken);
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

    // Past this point the commit has succeeded. `DispatchAsync` does not throw -- a consumer failure is
    // logged against its event and consumer type and the command still succeeds.
    await DispatchDomainEventsAsync(cancellationToken);
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

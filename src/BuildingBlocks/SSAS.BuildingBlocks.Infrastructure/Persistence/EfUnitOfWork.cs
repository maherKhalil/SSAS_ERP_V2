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

    try
    {
      await SaveChangesAsync(cancellationToken);
      await currentTransaction.CommitAsync(cancellationToken);
      await DispatchDomainEventsAsync(cancellationToken);
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

      await unitOfWork.CommitAsync(transaction, cancellationToken);
      completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
      if (completed)
      {
        throw new InvalidOperationException("The transaction has already completed.");
      }

      await unitOfWork.RollbackAsync(transaction, cancellationToken);
      completed = true;
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

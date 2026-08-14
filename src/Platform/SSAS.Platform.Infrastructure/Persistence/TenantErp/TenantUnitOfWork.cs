using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Tenant ERP unit of work (ADR-017). Mirrors PlatformUnitOfWork's failure translation exactly — the same
// concurrency, unique-violation and write-failure mapping — so moving Company across the boundary did not
// change how its failures surface to handlers.
//
// The inner EfUnitOfWork is built lazily, because the context itself does not exist until routing has
// been resolved. A routing failure surfaces as a Result here rather than an exception, since a handler
// calling SaveChanges is already prepared for a failed save.
public sealed class TenantUnitOfWork(
  ITenantDbContextProvider contextProvider,
  IDomainEventDispatcher domainEventDispatcher) : ITenantUnitOfWork
{
  private EfUnitOfWork<TenantDbContext>? inner;

  public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    var unitOfWork = await ResolveAsync(cancellationToken);
    if (unitOfWork.IsFailure)
    {
      return Result.Failure<int>(unitOfWork.Error);
    }

    try
    {
      return Result.Success(await unitOfWork.Value.SaveChangesAsync(cancellationToken));
    }
    catch (DbUpdateConcurrencyException)
    {
      return Result.Failure<int>(IdentityAccessErrors.ConcurrencyConflict);
    }
    catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
    {
      return Result.Failure<int>(IdentityAccessErrors.UniqueConstraintViolation);
    }
    catch (DbUpdateException)
    {
      return Result.Failure<int>(IdentityAccessErrors.WriteFailure);
    }
  }

  public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
  {
    var unitOfWork = await ResolveAsync(cancellationToken);
    if (unitOfWork.IsFailure)
    {
      // A transaction cannot be opened against a database we have no trusted route to. Failing loudly is
      // correct: the alternative would be a caller believing it holds a transaction that does not exist.
      throw new TenantStorageUnavailableException(unitOfWork.Error);
    }

    return await unitOfWork.Value.BeginTransactionAsync(cancellationToken);
  }

  private async Task<Result<EfUnitOfWork<TenantDbContext>>> ResolveAsync(CancellationToken cancellationToken)
  {
    if (inner is not null)
    {
      return Result.Success(inner);
    }

    var context = await contextProvider.ResolveAsync(cancellationToken);
    if (context.IsFailure)
    {
      return Result.Failure<EfUnitOfWork<TenantDbContext>>(context.Error);
    }

    // Bound to the SAME scoped context the repositories use, so a save commits exactly the changes they
    // tracked. A second context here would silently discard them.
    inner = new EfUnitOfWork<TenantDbContext>(context.Value, domainEventDispatcher);
    return Result.Success(inner);
  }
}

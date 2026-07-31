using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Infrastructure.Persistence;

public sealed class PlatformUnitOfWork(
  PlatformDbContext dbContext,
  IDomainEventDispatcher domainEventDispatcher) : IPlatformUnitOfWork
{
  private readonly EfUnitOfWork<PlatformDbContext> inner = new(dbContext, domainEventDispatcher);

  public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      return Result.Success(await inner.SaveChangesAsync(cancellationToken));
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

  public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
    inner.BeginTransactionAsync(cancellationToken);
}

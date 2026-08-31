using Microsoft.Extensions.Logging;
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
  IDomainEventDispatcher domainEventDispatcher,
  ILogger<EfUnitOfWork<PlatformDbContext>> logger) : IPlatformUnitOfWork
{
  private readonly EfUnitOfWork<PlatformDbContext> inner = new(dbContext, domainEventDispatcher, logger);

  public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      return Result.Success(await inner.SaveChangesAsync(cancellationToken));
    }
    // ⚠ THE EXCEPTION IS DISCARDED AND THE DISTINCTION IS NOT — THE `when` FILTER BELOW IS WHY (T-256).
    //
    // These three arms look like a chain that throws away everything SQL Server said. They are not: the
    // MIDDLE arm's filter, `when (exception.InnerException is SqlException { Number: 2601 or 2627 })`,
    // does the discrimination BEFORE any body runs. Arm selection is the measurement; by the time control
    // reaches a body, the only fact still needed has already been used.
    //
    // **So a unique violation and a deadlock do NOT arrive identically.** 2601/2627 take the middle arm
    // and become `UniqueConstraintViolation`; a deadlock (1205) falls to the last and becomes
    // `WriteFailure`. That was asserted as a defect and measured to be false (T-249) — the check is what
    // makes this comment safe to write.
    //
    // What IS lost is the index NAME, which only the `SqlException` message carries and `Error(Code,
    // Message)` cannot hold. That costs nothing here: **EF Core logs the failed command at `Error` under
    // `Microsoft.EntityFrameworkCore.Update` with the exception attached** (measured, T-247), correlated
    // to the request by `CorrelationIdMiddleware` and `FromLogContext`. The operator has the index name;
    // the caller never needed it.
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

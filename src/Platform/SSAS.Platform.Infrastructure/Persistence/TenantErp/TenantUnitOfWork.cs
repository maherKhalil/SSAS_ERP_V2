using SSAS.BuildingBlocks.Tenancy.Persistence;
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
    // ---- AN AUTHORIZATION REFUSAL, KEPT SEPARATE FROM AN OUTAGE (FP-006C5).
    //
    // Caught before the storage case below and returned with the authorizer's own Company.*, Branch.* or
    // BranchTransfer.* code, so the caller — and the HTTP mapper above it — can tell "you may not write
    // here" from "the database is unreachable". They are different answers with different status codes and
    // opposite retry advice.
    catch (TenantWriteAuthorizationException exception)
    {
      return Result.Failure<int>(exception.Error);
    }
    catch (TenantStorageUnavailableException exception)
    {
      // A cutover freeze is a CONTROLLED, VISIBLE maintenance outcome, not a write failure (ADR-020). It
      // surfaces with its own TenantStorage.* code so an operator sees "frozen by a cutover" rather than a
      // generic save error, and so a handler can distinguish it from data-integrity failures.
      return Result.Failure<int>(exception.Error);
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

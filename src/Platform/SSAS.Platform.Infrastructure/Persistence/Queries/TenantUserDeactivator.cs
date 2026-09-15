using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// ==================================================================================================
// PLATFORM'S SIDE OF `REQ-SS-0007` (T-091). THE SECOND OF TWO GUARDS ON A TERMINATED EMPLOYEE.
// ==================================================================================================
//
// T-090's guard is at `UserEmployeeResolver` and closes SELF-SERVICE per request against live state. This
// one closes AUTHENTICATION, and **it cannot close an access token already issued** — permissions travel in
// the token's claims, bounded at fifteen minutes by `JwtOptionsValidator`. That window is T-090's to close
// and this cannot substitute for it.
//
// ---- IT READS THE LINK IN THE OPPOSITE DIRECTION FROM `UserEmployeeResolver`.
//
// That file says it is *the one read over `UserEmployeeLink`*, and until T-091 it was. It answers
// **user -> employee**, on `UX_UserEmployeeLink_TenantId_TenantUserId`. This answers **employee -> user**,
// on `UX_UserEmployeeLink_TenantId_EmployeeId` — a different question on a different index, and the second
// unique index exists precisely so both directions are a seek.
//
// **Two readers, one table, and both are Platform's.** The link stays where `ADR-030` put it; no module
// learns its shape.
//
// ---- IT COMMITS ITS OWN WRITE, AND THE CALLER KNOWS THAT.
//
// The Platform database is not the tenant database and `ADR-017` means no single transaction spans them.
// This saves through `IPlatformUnitOfWork` and that save is final the moment it returns. **The ordering
// that makes it safe is the CALLER's** — `TerminateEmployeeCommandHandler` holds an uncommitted tenant
// transaction across this call so that a failure here rolls the termination back, and read that file for
// the trade it accepts.
public sealed class TenantUserDeactivator(
  PlatformDbContext dbContext,
  ITenantUserRepository tenantUsers,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  IDateTimeProvider clock) : ITenantUserDeactivator
{
  public async Task<Result> DeactivateForEmployeeAsync(
    Guid employeeId, CancellationToken cancellationToken = default)
  {
    // ---- NO TRUSTED TENANT IS A REFUSAL HERE, UNLIKE IN `UserEmployeeResolver`.
    //
    // There, absence is an ordinary answer: a caller with no tenant context has no linked employee. Here
    // the caller is mid-write on a tenant's data, so a missing tenant context is not "nobody to deactivate"
    // — it is a request that should never have reached a write path, and answering `Success` would report a
    // guard as satisfied when it never ran.
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty || employeeId == Guid.Empty)
    {
      return Result.Failure(IdentityAccessErrors.InvalidTenantUserTransition);
    }

    var tenantUserId = await dbContext.UserEmployeeLinks.AsNoTracking()
      .Where(link => link.TenantId == tenantId && link.EmployeeId == employeeId)
      .Select(link => (long?)link.TenantUserId)
      .SingleOrDefaultAsync(cancellationToken);

    // ---- NO ACCOUNT IS A SUCCESS.
    //
    // Most employees have no tenant user, and **today every employee does** — nothing in production writes
    // a `UserEmployeeLink` yet (T-092). A termination that failed because the person never had a login
    // would make this guard a bug for every real caller on the day it shipped.
    if (tenantUserId is not { } userId)
    {
      return Result.Success();
    }

    var tenantUser = await tenantUsers.GetByIdAsync(userId, cancellationToken);
    if (tenantUser is null)
    {
      // A link naming a user that does not exist. `FK_UserEmployeeLink_TenantUsers_TenantId_TenantUserId`
      // makes it impossible, so reaching it means the invariant is already broken — **and this is a WRITE
      // path, so failing loudly is right.** Contrast the dangling-EMPLOYEE link, which is genuinely
      // reachable because `ADR-030` Decision 4 forbids that foreign key.
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    // ---- ALREADY DEACTIVATED IS A SUCCESS, WHICH IS WHAT MAKES A RETRY SAFE.
    //
    // `TenantUser.Deactivate` refuses when the status is not `Active`. Treating that as a failure would
    // mean the operator's retry after a partial failure refuses on the half that already completed — and
    // the half-state this task exists to make repairable would become unrepairable by retry.
    if (tenantUser.Status != TenantUserStatus.Active)
    {
      return Result.Success();
    }

    var deactivated = tenantUser.Deactivate(Guid.NewGuid(), clock.UtcNow);
    if (deactivated.IsFailure)
    {
      return deactivated;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsSuccess ? Result.Success() : Result.Failure(saved.Error);
  }
}

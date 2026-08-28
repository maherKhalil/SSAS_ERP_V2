using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Tenancy;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// THE ONE READ OVER `UserEmployeeLink` (ADR-030, T-084). Platform answers; the module asks.
//
// ---- IT SCOPES BY TENANT AND THE TENANT IS NOT THE CALLER'S TO CHOOSE.
//
// The contract takes the tenant USER explicitly, because a caller may legitimately ask about a user other
// than itself. It does NOT take the tenant, which is read from the trusted request context — a tenant
// parameter would let a caller ask about a user in another tenant, which is a cross-tenant read dressed as
// a lookup. The same reasoning `ITenantModuleEntitlement` gives for answering only about the current
// request's tenant.
//
// The composite index `UX_UserEmployeeLink_TenantId_TenantUserId` is a seek on exactly these two columns,
// which is why `data-model.md` specifies no separate covering index.
//
// ---- NO TRUSTED TENANT IS `null`, NOT AN EXCEPTION.
//
// Absence is an ordinary answer here (`ADR-030` Decision 5), and a caller with no tenant context has no
// linked employee by definition. Throwing would turn a normal state into a fault on the path that exists
// for exactly the callers who do not have one.
//
// ==================================================================================================
// AND IT REFUSES A TERMINATED EMPLOYEE (T-090, AC-SS-0012, OD-SS-0004).
// ==================================================================================================
//
// `OD-SS-0004` ruled **history kept, access disabled.** History was kept from the day the link was built;
// access was not disabled, and a terminated employee whose tenant user was still active could read their
// own payslips and attendance. **Nothing asserted otherwise, because nothing was asked.**
//
// ---- WHY THE REFUSAL IS HERE AND NOT IN EACH SELF-SERVICE READ.
//
// A check in each read is the per-handler shape `REQ-SS-0003` rejected for permissions: *the architecture
// guards assert permissions, not scopes; nothing would catch the handler that forgot.* **There are already
// two self-service scope resolvers, leave balances would make three, and the third would have to remember.**
// This is the ONE place both go through, so the third inherits it without knowing it exists.
//
// ---- AND WHY NOT ON THE IDENTITY, WHICH IS WHERE `REQ-SS-0007` POINTS.
//
// Deactivating the tenant user is the right answer to a different question and it is `T-091`. It cannot be
// the answer to THIS one, for a measured reason: **permissions are carried in the access token's claims,
// not resolved live** (`CurrentUser.Permissions`), so deactivation cannot close an already-issued token —
// bounded at fifteen minutes by `JwtOptionsValidator`, but not zero. **This resolver runs per request,
// against live state, so it closes that window at once.**
//
// The two are complements, not alternatives, and the ordering matters: with this in place, a failure of
// the cross-database deactivation degrades to incomplete cleanup rather than exposure.
//
// ---- WHAT THIS DOES **NOT** DO.
//
// It does not close AUTHENTICATION. A terminated employee's tenant user can still sign in and reach every
// administrative surface their grants allow. `REQ-SS-0007`'s literal words — *cannot authenticate* — are
// NOT satisfied here and are `T-091`'s.
//
// **And it does not touch the link.** `REQ-SS-0006` / `AC-SS-0011` / `TS-SS-0009`: the row survives
// termination so retained payslips stay attributable. **What closes is the resolution, not the record** —
// which is why the refusal lives in this method and not in a `Where` clause over `UserEmployeeLinks`.
public sealed class UserEmployeeResolver(
  PlatformDbContext dbContext,
  ICurrentTenant currentTenant,
  IEmploymentStandingDirectory employmentStanding)
  : IUserEmployeeResolver
{
  public async Task<Guid?> ResolveEmployeeIdAsync(
    long tenantUserId, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty || tenantUserId <= 0)
    {
      return null;
    }

    // SingleOrDefault rather than FirstOrDefault: the unique index makes a second row impossible, so a
    // second row would be a corrupted invariant and should fail loudly rather than be silently picked from.
    var employeeIds = await dbContext.UserEmployeeLinks.AsNoTracking()
      .Where(link => link.TenantId == tenantId && link.TenantUserId == tenantUserId)
      .Select(link => (Guid?)link.EmployeeId)
      .SingleOrDefaultAsync(cancellationToken);

    if (employeeIds is not { } employeeId)
    {
      return null;
    }

    // ---- TERMINATED AND UNKNOWN COLLAPSE INTO THE SAME `null`, AND THAT IS THE ESTABLISHED PRECEDENT.
    //
    // The self-service routes answer `404 *.no_linked_employee` for a null, exactly as they already do for
    // a caller who was never linked and for a link naming an employee that no longer exists. **Three
    // conditions, one answer, deliberately:** the caller did nothing wrong, cannot act on the difference,
    // and a distinct code would disclose that a link exists — `BR-PLT-0002` with extra steps.
    //
    // **The cost is named rather than hidden: a terminated employee cannot tell "your access ended" from
    // "you were never linked."** That is an operational message to deliver out of band, not a fact for an
    // API to leak.
    var standing = await employmentStanding.GetStandingAsync(employeeId, cancellationToken);

    // `Current` and nothing else. `Unknown` fails closed with `Ended` — see `EmploymentStanding`, where
    // that direction is chosen once so no caller has to choose it again.
    return standing == EmploymentStanding.Current ? employeeId : null;
  }
}

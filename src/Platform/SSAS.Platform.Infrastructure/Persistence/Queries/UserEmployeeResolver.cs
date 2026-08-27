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
public sealed class UserEmployeeResolver(PlatformDbContext dbContext, ICurrentTenant currentTenant)
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

    return employeeIds;
  }
}

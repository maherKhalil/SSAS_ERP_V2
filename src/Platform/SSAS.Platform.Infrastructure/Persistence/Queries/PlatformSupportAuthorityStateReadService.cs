using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

// Live, persistence-backed usable-authority evaluation (ADR-016 / DEC-TEN-0019). Authority is never
// inferred from configuration or a bare principal row: a principal counts only when it is Active, holds
// at least one active assignment that the code-owned catalog still recognises as PlatformSupport-scoped
// (defense-in-depth filter drops unknown/tenant-scoped/corrupt names), AND its anchoring authentication
// account is currently eligible to authenticate. This is read fresh on every call so concurrent bootstrap
// convergence and disable/revoke lifecycle transitions are always observed.
public sealed class PlatformSupportAuthorityStateReadService(
  PlatformDbContext dbContext,
  IAuthenticationAccountRepository accountRepository,
  IPermissionCatalog permissionCatalog) : IPlatformSupportAuthorityStateReadService
{
  public async Task<bool> HasUsablePlatformAuthorityAsync(CancellationToken cancellationToken = default)
  {
    var activePrincipals = await dbContext.PlatformSupportPrincipals
      .AsNoTracking()
      .Include(principal => principal.PermissionAssignments)
      .Where(principal => principal.Status == PlatformSupportPrincipalStatus.Active)
      .ToListAsync(cancellationToken);

    foreach (var principal in activePrincipals)
    {
      var activeNames = principal.PermissionAssignments
        .Where(assignment => assignment.IsActive)
        .Select(assignment => assignment.PermissionName.Value);

      if (PlatformSupportPermissionFilter.FilterToPlatformSupportScope(activeNames, permissionCatalog).Count == 0)
      {
        continue;
      }

      var account = await accountRepository.GetByIdentityIdAsync(principal.IdentityId, cancellationToken);
      if (account is { IsAuthenticationEligible: true })
      {
        return true;
      }
    }

    return false;
  }
}

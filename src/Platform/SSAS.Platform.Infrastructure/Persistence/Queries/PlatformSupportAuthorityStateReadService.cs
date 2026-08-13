using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

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

  // Administrative authority (DEC-TEN-0026). Same liveness rules as the general predicate — Active principal,
  // active (non-removed) assignment, eligible anchoring account — but narrowed to exactly
  // Platform.Support.Administer. Deliberately NOT expressed in terms of the general predicate: a principal can
  // satisfy that one on Platform.Tenants.View alone while conferring no administrative capability at all.
  public async Task<bool> HasUsablePlatformAdministrativeAuthorityAsync(CancellationToken cancellationToken = default)
  {
    // Catalog currentness gate first: if the code-owned catalog no longer exposes Administer as a
    // PlatformSupport permission, no persisted assignment for that name can confer administrative authority,
    // regardless of how many rows survive in history.
    if (!permissionCatalog.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var definition) ||
      definition.Scope != PermissionScope.PlatformSupport)
    {
      return false;
    }

    // Narrow database-side to Active principals holding an ACTIVE Administer assignment (RemovedUtc IS NULL —
    // IsActive is a computed property and does not translate). This rides the existing filtered unique index on
    // (PlatformSupportPrincipalId, PermissionName) WHERE RemovedUtc IS NULL, so no assignment graph is loaded.
    var administerName = PermissionName.Create(PlatformPermissionNames.AdministerPlatformSupport).Value;
    var candidateIdentityIds = await dbContext.PlatformSupportPrincipals
      .AsNoTracking()
      .Where(principal => principal.Status == PlatformSupportPrincipalStatus.Active &&
        principal.PermissionAssignments.Any(assignment =>
          assignment.RemovedUtc == null && assignment.PermissionName == administerName))
      .Select(principal => principal.IdentityId)
      .ToListAsync(cancellationToken);

    // Anchoring account must still be able to authenticate, exactly as for general authority: an Administer
    // grant held by a principal whose account is disabled confers nothing usable.
    foreach (var identityId in candidateIdentityIds)
    {
      var account = await accountRepository.GetByIdentityIdAsync(identityId, cancellationToken);
      if (account is { IsAuthenticationEligible: true })
      {
        return true;
      }
    }

    return false;
  }
}

using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class PlatformSupportPermissionReadService(PlatformDbContext dbContext, IPermissionCatalog permissionCatalog)
  : IPlatformSupportPermissionReadService
{
  public async Task<IReadOnlyCollection<string>> GetActivePermissionsAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default)
  {
    var names = await dbContext.PlatformPermissionAssignments.AsNoTracking()
      .Where(assignment =>
        assignment.PlatformSupportPrincipalId == platformSupportPrincipalId &&
        assignment.RemovedUtc == null)
      .Select(assignment => assignment.PermissionName)
      .ToListAsync(cancellationToken);

    // Defense in depth (ADR-015 / DEC-TEN-0018): re-validate against the code-owned catalog so a corrupt
    // or force-seeded non-PlatformSupport row can never be read back as platform-support authority.
    return PlatformSupportPermissionFilter.FilterToPlatformSupportScope(
      names.Select(permission => permission.Value),
      permissionCatalog)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(value => value, StringComparer.Ordinal)
      .ToArray();
  }
}

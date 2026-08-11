using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlatformSupportPrincipalRepository(PlatformDbContext dbContext) : IPlatformSupportPrincipalRepository
{
  public Task<PlatformSupportPrincipal?> GetByIdAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals
      .Include(principal => principal.PermissionAssignments)
      .SingleOrDefaultAsync(principal => principal.Id == platformSupportPrincipalId, cancellationToken);

  public Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(
    long identityId,
    CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals
      .Include(principal => principal.PermissionAssignments)
      .SingleOrDefaultAsync(principal => principal.IdentityId == identityId, cancellationToken);

  public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals.AnyAsync(principal => principal.IdentityId == identityId, cancellationToken);

  public async Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default)
  {
    await dbContext.PlatformSupportPrincipals.AddAsync(principal, cancellationToken);
  }
}

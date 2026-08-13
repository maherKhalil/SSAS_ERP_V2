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

  // UPDLOCK/HOLDLOCK holds a lock on the principal row (and key-range for the unique IdentityId) until the
  // enclosing transaction commits, serializing platform-session creation against a concurrent principal Disable
  // (Phase 4B / L1). Mirrors the existing account/session for-update reads; the lock — not the isolation level —
  // provides the invariant, so correctness is independent of READ_COMMITTED_SNAPSHOT.
  public Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(
    long identityId,
    CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals
      .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformSupportPrincipals] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId}")
      .SingleOrDefaultAsync(cancellationToken);

  // Same update lock keyed by the primary key, for the Disable flow (principal locked before its session range).
  // Deliberately does not Include PermissionAssignments: the locked read exists to serialize the lifecycle
  // decision, and widening it would extend locking to rows the flow does not mutate.
  public Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(
    long platformSupportPrincipalId,
    CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals
      .FromSqlInterpolated($"SELECT * FROM [platform].[PlatformSupportPrincipals] WITH (UPDLOCK, HOLDLOCK) WHERE [PlatformSupportPrincipalId] = {platformSupportPrincipalId}")
      .SingleOrDefaultAsync(cancellationToken);

  public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
    dbContext.PlatformSupportPrincipals.AnyAsync(principal => principal.IdentityId == identityId, cancellationToken);

  public async Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default)
  {
    await dbContext.PlatformSupportPrincipals.AddAsync(principal, cancellationToken);
  }
}

using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface IPlatformSupportPrincipalRepository
{
  Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default);

  Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default);

  // Load the principal under a transactionally-effective update lock so platform-session creation can serialize
  // its Active-eligibility decision against a concurrent Disable (Phase 4B / L1, DEC-TEN-0023). Correctness does
  // not depend on the database isolation level (RCSI on or off).
  Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default);

  // Same update lock, keyed by principal id — used by the Disable flow so it takes the principal lock BEFORE any
  // session lock, matching the global account → principal → session order (Phase 4B / L1, DEC-TEN-0023).
  Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default);

  Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default);

  Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default);
}

using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Flat, physical-database-only projection for D9. This is intentionally distinct from backup-fleet reads:
// restore verification has different due evidence (durable successful verification runs) and admission facts.
public interface ITenantDatabaseRestoreVerificationFleetReadRepository
{
  Task<IReadOnlyList<string>> ListEligibleSourceServerKeysAsync(
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>> ListCandidatesAsync(
    string sourceServerKey,
    long afterTenantDatabaseId,
    int take,
    CancellationToken cancellationToken = default);
}

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

  // The durable operation row is the authority for verification freshness. The aggregate timestamp is a
  // projection which may lag a completed D7 operation, so readiness refresh never derives its cadence from
  // that projection alone.
  Task<DateTimeOffset?> FindLatestSuccessfulVerificationCompletedUtcAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);
}

namespace SSAS.Platform.Application.TenantStorage;

// Trusted verification-server observation for D8. An unavailable observation is deliberately data, not an
// exception the reconciler might mistake for "nothing is running".
public interface ITenantDatabaseRestoreVerificationServerObserver
{
  Task<TenantDatabaseRestoreVerificationServerObservation> ObserveAsync(
    TenantDatabaseRestoreVerificationServerObservationRequest request,
    CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseRestoreVerificationServerObservationRequest(
  long VerificationRunId,
  long TenantDatabaseId,
  string RestoreServerKey,
  string SourceServerKey,
  string VerificationDatabaseName);

public sealed record TenantDatabaseRestoreVerificationServerObservation(
  bool ServerStateObserved,
  bool VerificationDatabaseExists,
  bool RestoreIsActiveOnServer,
  string? VerificationDatabaseState = null,
  string? UnavailableReason = null);

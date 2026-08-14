namespace SSAS.Platform.Domain.Enums;

// Can the application currently reach and authenticate to a physical tenant database (ADR-018)?
//
// One of the four orthogonal status dimensions, and deliberately separate from schema compatibility: a
// customer VPN outage and a failed release are indistinguishable under a single combined status, and the
// operator response to each is completely different.
//
// `TlsFailure` and `NetworkBlocked` are recorded in ADR-018 as future refinements. They are not modelled
// here because the current provider cannot distinguish them dependably, and a value that is never written
// truthfully is worse than its absence.
public enum TenantDatabaseConnectivityStatus
{
  // Pre-verification. ADR-018's gating table treats Unknown as DENY — it is not optimism.
  Unknown = 1,
  Healthy = 2,
  Unreachable = 3,
  AuthenticationFailed = 4
}

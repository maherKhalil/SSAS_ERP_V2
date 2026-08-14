using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// Can the application reach and authenticate to a physical tenant database (ADR-018)?
//
// Deliberately SEPARATE from schema health, and separate for a reason that is operational rather than
// aesthetic: connectivity changes far more often than schema does, so the two belong on different cadences,
// and a connectivity failure must never be reported — or recorded — as a schema problem.
//
// The contract is narrow on purpose. This service observes connectivity and records connectivity. It does
// not read migration history, and it writes nothing to the schema dimension: a check that observes nothing
// about schema must leave the previous schema observation exactly as it found it.
public interface ITenantDatabaseConnectivityHealthService
{
  // Probes ONE physical database. Takes the physical database id, not a tenant: a shared database is one
  // probe regardless of how many tenants it hosts.
  Task<Result<TenantDatabaseConnectivityResult>> CheckAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);

  // Probes a bounded page of physical databases and records what it finds.
  Task<Result<TenantDatabaseConnectivitySweepSummary>> SweepAsync(
    int maximumDatabases,
    CancellationToken cancellationToken = default);
}

// `Verified` distinguishes "we probed and concluded" from "there is no supported path to probe this at all"
// — the customer-managed case. An unverifiable database keeps Unknown rather than having a fabricated
// result recorded against it.
public sealed record TenantDatabaseConnectivityResult(
  long TenantDatabaseId,
  TenantDatabaseConnectivityStatus Status,
  bool Verified);

public sealed record TenantDatabaseConnectivitySweepSummary(
  int Discovered,
  int Healthy,
  int Unreachable,
  int AuthenticationFailed,
  int NotVerifiable);

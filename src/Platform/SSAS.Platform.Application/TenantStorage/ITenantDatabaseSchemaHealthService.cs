using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// READ-ONLY schema health for physical tenant databases (ADR-018).
//
// It must never apply a migration. A health check that could change schema would mean every diagnostic
// run is also a deployment, which is precisely the coupling ADR-018 separates: the orchestrator consumes
// this service, never the reverse.
public interface ITenantDatabaseSchemaHealthService
{
  // Checks ONE physical database. Takes the physical database id, not a tenant: a shared database is one
  // check regardless of how many tenants it hosts.
  Task<Result<TenantDatabaseSchemaHealthResult>> CheckAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default);

  // Checks a bounded page of physical databases and persists what it finds.
  Task<Result<TenantDatabaseHealthSweepSummary>> SweepAsync(
    int maximumDatabases,
    CancellationToken cancellationToken = default);
}

// Outcome of inspecting one physical database's tenant migration history against the deployed catalog.
//
// `AppliedMigration` and `TargetMigration` are observations recorded for operators; the STATUS is the
// conclusion, and it is derived from comparing full histories rather than from either endpoint alone.
public sealed record TenantDatabaseSchemaHealthResult(
  long TenantDatabaseId,
  TenantDatabaseConnectivityStatus ConnectivityStatus,
  TenantDatabaseSchemaCompatibilityStatus SchemaCompatibilityStatus,
  string? AppliedMigration,
  string? TargetMigration,
  IReadOnlyCollection<string> PendingMigrations);

public sealed record TenantDatabaseHealthSweepSummary(
  int Discovered,
  int UpToDate,
  int PendingMigrations,
  int AheadOfApplication,
  int HistoryMismatch,
  int Unreachable,
  // Databases the platform never connects to, so nothing about their health was verified. Counted
  // separately so an operator is never shown a customer-managed database as though it had been checked.
  int NotVerifiable);

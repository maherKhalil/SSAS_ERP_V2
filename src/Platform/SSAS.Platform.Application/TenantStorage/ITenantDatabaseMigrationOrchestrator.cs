using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.TenantStorage;

// Fleet migration orchestration for physical tenant databases (ADR-018).
//
// This is the write-oriented counterpart to the schema-health service, and it is what replaces the manual
// per-database command as the normal path for a platform-managed estate. It consumes health rather than
// re-deriving it.
//
// It is invoked explicitly — by deployment tooling or an operator action — never from the request path and
// never blindly at host startup. ADR-018 is explicit that serving instances would race each other, startup
// would scale with estate size, and DDL authority does not belong in the process serving requests.
public interface ITenantDatabaseMigrationOrchestrator
{
  // Migrates ONE physical database under single-writer ownership. Returns the per-database outcome rather
  // than throwing, so a fleet run can record it and continue.
  Task<Result<TenantDatabaseMigrationOutcome>> MigrateAsync(
    long tenantDatabaseId,
    TenantMigrationRunOptions options,
    CancellationToken cancellationToken = default);

  // Discovers eligible physical databases and migrates them, one at a time, each under its own ownership.
  // Mutual exclusion is scoped per database (ADR-018 lock invariant 1) so one slow database cannot
  // serialise the estate.
  Task<Result<TenantMigrationRunSummary>> RunAsync(
    TenantMigrationRunOptions options,
    CancellationToken cancellationToken = default);
}

// `ApprovalGranted` carries the explicit per-run approval that `PlatformAfterApproval` requires. Its
// default of false is the point: ADR-018 treats absence of approval as denial, never default-allow.
public sealed record TenantMigrationRunOptions(
  bool ApprovalGranted = false,
  int MaximumDatabases = 100,
  TimeSpan? OwnershipTimeout = null);

public enum TenantDatabaseMigrationOutcomeKind
{
  AlreadyUpToDate = 1,
  Migrated = 2,
  // Ownership was held elsewhere. A clean skip-and-report, never a forced proceed (lock invariant 5).
  SkippedOwnershipHeld = 3,
  // Pending migrations the platform may not apply: CustomerDba, or PlatformAfterApproval without approval.
  BlockedPendingCustomer = 4,
  Unreachable = 5,
  AheadOfApplication = 6,
  MigrationHistoryMismatch = 7,
  Failed = 8,
  // CustomerManaged hosting has no runtime connectivity path at all (ADR-021), so nothing was attempted.
  NotVerifiable = 9
}

public sealed record TenantDatabaseMigrationOutcome(
  long TenantDatabaseId,
  TenantDatabaseMigrationOutcomeKind Kind,
  string? AppliedMigration,
  // Safe summary only — never credential, connection or endpoint material.
  string? Detail);

// The deployment report ADR-018 requires. Blocked-pending-customer is its own category, deliberately not
// folded into failure: a release is not broken because a customer DBA has not yet acted.
public sealed record TenantMigrationRunSummary(
  int Discovered,
  int AlreadyUpToDate,
  int Migrated,
  int Failed,
  int Skipped,
  int BlockedPendingCustomer,
  int Unreachable,
  int AheadOfApplication,
  int HistoryMismatch,
  int NotVerifiable,
  IReadOnlyCollection<TenantDatabaseMigrationOutcome> Outcomes);

using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// Executes ONE backup operation against ONE physical tenant database (ADR-022 §10).
//
// The contract deliberately expresses no SQL Server syntax. `Full`, `Differential` and `TransactionLog` are
// provider vocabulary carried inside TenantDatabaseBackupOperation, and the command text, destination path,
// compression flags and credential all stay behind this boundary in Infrastructure.
//
// It also carries no destination and no database name. The provider resolves both from trusted sources —
// the physical registry and configuration — so no caller can influence WHERE a complete copy of a database
// is written (ADR-022 §11, compliance rule 23).
public interface ITenantDatabaseBackupProvider
{
  Task<TenantDatabaseBackupProviderResult> ExecuteAsync(
    TenantDatabaseBackupRequest request,
    CancellationToken cancellationToken = default);
}

// WHAT to execute. Every field is either an identifier the provider resolves through trusted state, or a
// policy-derived option — never infrastructure detail supplied from above.
public sealed record TenantDatabaseBackupRequest(
  long TenantDatabaseId,
  TenantDatabaseBackupOperation Operation,
  long BackupRunId,
  TenantDatabaseBackupOptions Options,
  // Set ONLY for scheduler-originated work (TS-Backup Phase C). It carries the last-successful timestamp the
  // scheduling decision was based on; if a platform-managed backup of this operation has completed since
  // then, the decision is stale and this run is redundant.
  //
  // NULL for manual execution, which means "take this backup now" and is deliberately never subject to the
  // schedule. A conditional manual backup would be a surprising thing to hand an operator during an
  // incident.
  DateTimeOffset? SupersededIfCompletedAfterUtc = null);

// Policy-derived execution options. Read from the persisted policy by the executor, never from a caller.
public sealed record TenantDatabaseBackupOptions(
  string DestinationKey,
  TenantDatabaseBackupCompressionMode CompressionMode);

// What the provider observed. Deliberately DISTINCT from the persisted TenantDatabaseBackupRun: the
// provider reports evidence, and the executor decides what that means for domain state. Infrastructure
// never mutates the Platform aggregate through this type.
public sealed record TenantDatabaseBackupProviderResult(
  TenantDatabaseBackupOutcome Outcome,
  string? ProviderBackupSetIdentity = null,
  string? SafeArtifactReference = null,
  long? BackupSizeBytes = null,
  decimal? FirstLsn = null,
  decimal? LastLsn = null,
  decimal? DatabaseBackupLsn = null,
  decimal? CheckpointLsn = null,
  Guid? BackupSetGuid = null,
  DateTimeOffset? StartedUtc = null,
  DateTimeOffset? CompletedUtc = null,
  string? SafeErrorSummary = null);

// The provider's own outcome vocabulary. Maps onto run statuses in the executor rather than being the run
// status itself, so a provider concern never becomes a domain state by accident.
public enum TenantDatabaseBackupOutcome
{
  // Command completed AND post-operation evidence reconciled. Nothing less earns this.
  Succeeded = 1,

  Failed = 2,

  // Another platform worker holds backup ownership of this physical database. NOT a failure.
  SkippedOwnershipHeld = 3,

  // A server-side backup was already in flight against this database (ADR-022 §14). NOT a failure either.
  SkippedInFlightOperation = 4,

  // A precondition made the operation impossible before it was issued — wrong recovery model, missing
  // differential base, unsupported compression requirement, unbackable database state.
  BlockedByPrecondition = 5,

  // Another platform worker already satisfied this scheduling decision while this one waited for ownership
  // (ADR-022 §13). Nothing is running and nothing failed — the work was already done.
  SkippedSupersededByRecentBackup = 6
}

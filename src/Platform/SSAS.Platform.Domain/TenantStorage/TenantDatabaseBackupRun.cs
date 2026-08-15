using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// WHAT ACTUALLY HAPPENED — one record per provider backup operation (ADR-022 §15).
//
// Unlike migration, where a per-run summary suffices, backup history answers questions no other state can:
// when the last successful full, differential and log backup were; whether the current chain is continuous
// and what the latest differential depends on; what failed and how it was classified; which artifacts have
// been verified; and whether retention is about to remove something the chain still needs.
//
// Phase A establishes the model and contracts. NOTHING IN PHASE A EXECUTES A BACKUP: no provider exists, no
// scheduler exists, and no background component creates rows here. Tests create them; production will not
// until Phase B.
public sealed class TenantDatabaseBackupRun : AggregateRoot<long>, IAuditableEntity
{
  public const int DestinationKeyMaximumLength = 128;

  // A SAFE artifact reference — a logical name or provider handle, never a resolved path, UNC, URL or
  // signed link. Sized for a provider handle rather than for a URL, deliberately.
  public const int ArtifactReferenceMaximumLength = 256;

  public const int ProviderBackupIdentityMaximumLength = 256;

  // Same bound and same rule as TenantDatabase.LastMigrationError: a safe operator-facing summary that can
  // never carry a connection string, credential or resolved secret-bearing path.
  public const int ErrorSummaryMaximumLength = 512;

  public const int ActorMaximumLength = 256;

  private TenantDatabaseBackupRun(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    TenantDatabaseBackupRunStatus status,
    string? destinationKey,
    DateTimeOffset startedUtc,
    string actor)
    : base(0)
  {
    TenantDatabaseId = tenantDatabaseId;
    Operation = operation;
    Status = status;
    DestinationKey = destinationKey;
    StartedUtc = startedUtc.ToUniversalTime();
    CreatedUtc = StartedUtc;
    CreatedBy = actor;
    ModifiedUtc = StartedUtc;
    ModifiedBy = actor;
  }

  private TenantDatabaseBackupRun()
    : base(0)
  {
    Operation = TenantDatabaseBackupOperation.SqlServerFull();
  }

  public long TenantDatabaseId { get; private set; }

  // Provider-scoped (ProviderKey, OperationCode), never a universal Full/Differential/Log enum — see
  // TenantDatabaseBackupOperation and ADR-022 §10.
  public TenantDatabaseBackupOperation Operation { get; private set; }

  public TenantDatabaseBackupRunStatus Status { get; private set; }

  public DateTimeOffset StartedUtc { get; private set; }

  public DateTimeOffset? CompletedUtc { get; private set; }

  // The trusted KEY the run was directed at, never the resolved location it wrote to.
  public string? DestinationKey { get; private set; }

  public string? ArtifactReference { get; private set; }

  public string? ProviderBackupIdentity { get; private set; }

  public long? SizeBytes { get; private set; }

  public string? ErrorSummary { get; private set; }

  // ---- Chain metadata (ADR-022 §9). Recorded from the first backup, because reconstructing continuity
  // retrospectively is far harder than capturing it. All nullable: these exist only after a provider
  // operation completes, and Phase A never runs one.
  //
  // SQL Server LSNs are numeric(25,0), which exceeds every integral CLR type and fits comfortably in
  // decimal.
  public decimal? FirstLsn { get; private set; }

  public decimal? LastLsn { get; private set; }

  public decimal? DatabaseBackupLsn { get; private set; }

  // The checkpoint this backup set records (TS-Backup Phase D7).
  //
  // THE ANCHOR DIFFERENTIAL APPLICABILITY IS DECIDED BY: a differential's `DatabaseBackupLsn` equals the
  // checkpoint LSN of the full it was taken against, which is what makes "is this differential restorable
  // onto that baseline?" answerable from platform records alone.
  //
  // NULLABLE, AND HISTORICAL ROWS STAY NULL. On the instances this was established against, a full's
  // checkpoint and first LSN happened to be identical, so backfilling one from the other would have looked
  // right — and would have been inventing evidence about backups nobody captured this value for. A chain
  // whose baseline predates this column is reported as unverifiable at depth rather than guessed at.
  public decimal? CheckpointLsn { get; private set; }

  public Guid? BackupSetGuid { get; private set; }

  public TenantDatabaseBackupVerificationState VerificationState { get; private set; }
    = TenantDatabaseBackupVerificationState.NotVerified;

  public DateTimeOffset? LastVerifiedUtc { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<TenantDatabaseBackupRun> Start(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    string? destinationKey,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (tenantDatabaseId <= 0)
    {
      return Result.Failure<TenantDatabaseBackupRun>(TenantStorageErrors.TenantDatabaseRequired);
    }

    ArgumentNullException.ThrowIfNull(operation);

    var normalizedKey = destinationKey?.Trim();
    if (normalizedKey is { Length: > DestinationKeyMaximumLength })
    {
      return Result.Failure<TenantDatabaseBackupRun>(TenantStorageErrors.BackupDestinationKeyInvalid);
    }

    return Result.Success(new TenantDatabaseBackupRun(
      tenantDatabaseId,
      operation,
      TenantDatabaseBackupRunStatus.Running,
      string.IsNullOrEmpty(normalizedKey) ? null : normalizedKey,
      occurredUtc,
      actor));
  }

  // SUCCESS REQUIRES POST-OPERATION EVIDENCE, NOT COMMAND SUBMISSION (ADR-022 §14, §15). A provider backup
  // identity is therefore mandatory here: it is the reconciled evidence that a backup set actually exists,
  // and without it a run has nothing to be successful about. A worker that lost its session cannot reach
  // this method with evidence it never obtained.
  public Result Succeed(
    string providerBackupIdentity,
    string? artifactReference,
    long? sizeBytes,
    decimal? firstLsn,
    decimal? lastLsn,
    decimal? databaseBackupLsn,
    decimal? checkpointLsn,
    Guid? backupSetGuid,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (string.IsNullOrWhiteSpace(providerBackupIdentity) ||
      providerBackupIdentity.Length > ProviderBackupIdentityMaximumLength)
    {
      return Result.Failure(TenantStorageErrors.BackupProviderEvidenceRequired);
    }

    if (artifactReference is { Length: > ArtifactReferenceMaximumLength })
    {
      return Result.Failure(TenantStorageErrors.BackupArtifactReferenceInvalid);
    }

    if (sizeBytes is < 0)
    {
      return Result.Failure(TenantStorageErrors.BackupRunSizeInvalid);
    }

    Status = TenantDatabaseBackupRunStatus.Succeeded;
    ProviderBackupIdentity = providerBackupIdentity.Trim();
    ArtifactReference = artifactReference?.Trim();
    SizeBytes = sizeBytes;
    FirstLsn = firstLsn;
    LastLsn = lastLsn;
    DatabaseBackupLsn = databaseBackupLsn;
    CheckpointLsn = checkpointLsn;
    BackupSetGuid = backupSetGuid;
    ErrorSummary = null;
    Complete(actor, occurredUtc);
    return Result.Success();
  }

  public Result Fail(string? errorSummary, string actor, DateTimeOffset occurredUtc)
  {
    Status = TenantDatabaseBackupRunStatus.Failed;
    ErrorSummary = Truncate(errorSummary);
    Complete(actor, occurredUtc);
    return Result.Success();
  }

  // A CONTROLLED NON-EXECUTION, never a failure (ADR-022 §14). Nothing failed: another worker holds
  // ownership, or a server-side backup is still in flight. Recording these as failures would degrade
  // recovery readiness on the strength of coordination working exactly as designed.
  public Result Skip(
    TenantDatabaseBackupRunStatus status,
    string? reasonSummary,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (status is not (TenantDatabaseBackupRunStatus.SkippedOwnershipHeld or
      TenantDatabaseBackupRunStatus.SkippedInFlightOperation or
      TenantDatabaseBackupRunStatus.BlockedByPolicy))
    {
      return Result.Failure(TenantStorageErrors.BackupRunSkipStatusInvalid);
    }

    Status = status;
    ErrorSummary = Truncate(reasonSummary);
    Complete(actor, occurredUtc);
    return Result.Success();
  }

  public Result RecordVerification(
    TenantDatabaseBackupVerificationState state,
    string? errorSummary,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (state == TenantDatabaseBackupVerificationState.NotVerified)
    {
      // Verification concluded something. Writing NotVerified back would erase evidence rather than record
      // it — the same rule the health dimensions follow for Unknown.
      return Result.Failure(TenantStorageErrors.BackupVerificationResultRequired);
    }

    VerificationState = state;
    LastVerifiedUtc = occurredUtc.ToUniversalTime();
    if (state == TenantDatabaseBackupVerificationState.VerificationFailed)
    {
      Status = TenantDatabaseBackupRunStatus.VerificationFailed;
      ErrorSummary = Truncate(errorSummary);
    }

    Touch(actor, occurredUtc);
    return Result.Success();
  }

  private void Complete(string actor, DateTimeOffset occurredUtc)
  {
    CompletedUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
  }

  private static string? Truncate(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? null
      : value.Length <= ErrorSummaryMaximumLength ? value : value[..ErrorSummaryMaximumLength];

  private void Touch(string actor, DateTimeOffset occurredUtc)
  {
    ModifiedUtc = occurredUtc.ToUniversalTime();
    ModifiedBy = actor;
  }

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}

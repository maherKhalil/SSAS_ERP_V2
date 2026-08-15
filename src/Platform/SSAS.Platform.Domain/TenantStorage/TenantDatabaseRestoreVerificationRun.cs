using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// ONE actual restore-verification operation (ADR-022 §17, TS-Backup Phase D).
//
// A SEPARATE ENTITY from TenantDatabaseBackupRun, and not for tidiness. Backup execution and restore
// verification have different lifecycles, run on different servers under different identities, and differ
// by orders of magnitude in cost — but the decisive difference is that a verification CREATES A DATABASE
// THAT CAN OUTLIVE THE PROCESS THAT CREATED IT. Nothing in backup history can express which operation
// created a given database, whether that operation is still active, or whether disposal succeeded, and
// ADR-022 §17 makes automated destructive cleanup conditional on exactly those facts.
//
// THIS RECORD IS THE REASON CLEANUP CAN BE SAFE. It is written BEFORE the restore begins, so a process that
// dies mid-restore leaves a row that positively identifies the database it abandoned. A `finally` block
// cannot do that, which §17 states explicitly.
public sealed class TenantDatabaseRestoreVerificationRun : AggregateRoot<long>, IAuditableEntity
{
  // SQL Server catalog names are sysname (128 Unicode characters).
  public const int VerificationDatabaseNameMaximumLength = 128;

  // Sized like ServerKey, and the same kind of value: a trusted configuration LOOKUP KEY, never an address.
  public const int RestoreServerKeyMaximumLength = 128;

  // Same bound and same rule as every other operator-facing summary in this model: safe text that can never
  // carry a connection string, credential or resolved path.
  public const int ErrorSummaryMaximumLength = 512;

  public const int ActorMaximumLength = 256;

  private TenantDatabaseRestoreVerificationRun(
    long tenantDatabaseId,
    long sourceBackupRunId,
    TenantDatabaseRestoreDepth depth,
    string restoreServerKey,
    string actor,
    DateTimeOffset occurredUtc)
    : base(0)
  {
    TenantDatabaseId = tenantDatabaseId;
    SourceBackupRunId = sourceBackupRunId;
    Depth = depth;
    RestoreServerKey = restoreServerKey;
    Status = TenantDatabaseRestoreVerificationStatus.Admitted;
    CleanupState = TenantDatabaseVerificationCleanupState.NotRequired;
    StartedUtc = occurredUtc.ToUniversalTime();
    CreatedUtc = StartedUtc;
    CreatedBy = actor;
    ModifiedUtc = StartedUtc;
    ModifiedBy = actor;
  }

  private TenantDatabaseRestoreVerificationRun()
    : base(0)
  {
    RestoreServerKey = string.Empty;
  }

  // The PHYSICAL database whose chain this verifies. A shared database is verified once, as one physical
  // database with one chain — never per tenant (ADR-022 §19).
  public long TenantDatabaseId { get; private set; }

  // The full baseline this verification exercised. THE LINK PHASE E NEEDS: it is what lets an activation
  // gate require a verification of the CURRENT baseline rather than accepting a verification of a
  // superseded one (ADR-022 §18).
  public long SourceBackupRunId { get; private set; }

  public TenantDatabaseRestoreDepth Depth { get; private set; }

  // Trusted configuration key for the verification target. Never an address, never a connection string.
  public string RestoreServerKey { get; private set; }

  public TenantDatabaseRestoreVerificationStatus Status { get; private set; }

  // A SEPARATE DIMENSION from Status, so a failed drop can never erase a proven restore (ADR-022 §17).
  public TenantDatabaseVerificationCleanupState CleanupState { get; private set; }

  // Null until a verification database is about to be created. Once set, THIS is the value the orphan sweep
  // correlates a physical database against — never a name pattern (ADR-022 §17, compliance rule 24).
  public string? VerificationDatabaseName { get; private set; }

  public DateTimeOffset StartedUtc { get; private set; }

  public DateTimeOffset? CompletedUtc { get; private set; }

  // Safe operator-facing summary only.
  public string? ErrorSummary { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  // Whether this run currently holds a verification database. Used by cleanup eligibility: a database whose
  // run is still active is NOT an orphan, however old it looks (ADR-022 §17).
  public bool IsActive =>
    Status is TenantDatabaseRestoreVerificationStatus.Admitted or
      TenantDatabaseRestoreVerificationStatus.Restoring;

  // ADMISSION. Reaching this at all means this instance won the serialising event for this database's due
  // verification state — the invariant is enforced by the store and the database, not by this method
  // (ADR-022 §17, compliance rule 43).
  public static Result<TenantDatabaseRestoreVerificationRun> Admit(
    long tenantDatabaseId,
    long sourceBackupRunId,
    TenantDatabaseRestoreDepth depth,
    string restoreServerKey,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (tenantDatabaseId <= 0)
    {
      return Result.Failure<TenantDatabaseRestoreVerificationRun>(TenantStorageErrors.TenantDatabaseRequired);
    }

    // A verification with no baseline to restore is not a verification.
    if (sourceBackupRunId <= 0)
    {
      return Result.Failure<TenantDatabaseRestoreVerificationRun>(
        TenantStorageErrors.RestoreVerificationBaselineRequired);
    }

    var normalizedKey = restoreServerKey?.Trim();
    if (string.IsNullOrEmpty(normalizedKey) || normalizedKey.Length > RestoreServerKeyMaximumLength)
    {
      return Result.Failure<TenantDatabaseRestoreVerificationRun>(
        TenantStorageErrors.RestoreVerificationServerKeyInvalid);
    }

    return Result.Success(new TenantDatabaseRestoreVerificationRun(
      tenantDatabaseId, sourceBackupRunId, depth, normalizedKey, actor, occurredUtc));
  }

  // Records the database this run is ABOUT to create, and moves to Restoring.
  //
  // ORDER IS THE WHOLE POINT: the name is durable before the database exists, never after. A process that
  // dies between this write and the restore leaves a row naming a database that may or may not have been
  // created — which the sweep can check — whereas the reverse order can leave a database nothing knows
  // about at all.
  public Result BeginRestore(string verificationDatabaseName, string actor, DateTimeOffset occurredUtc)
  {
    if (Status != TenantDatabaseRestoreVerificationStatus.Admitted)
    {
      return Result.Failure(TenantStorageErrors.RestoreVerificationNotAdmitted);
    }

    var normalized = verificationDatabaseName?.Trim();
    if (string.IsNullOrEmpty(normalized) || normalized.Length > VerificationDatabaseNameMaximumLength)
    {
      return Result.Failure(TenantStorageErrors.RestoreVerificationDatabaseNameInvalid);
    }

    VerificationDatabaseName = normalized;
    Status = TenantDatabaseRestoreVerificationStatus.Restoring;

    // Pending from this moment, because from this moment a database may exist that must be disposed of.
    CleanupState = TenantDatabaseVerificationCleanupState.Pending;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // The chain restored, the database came online, and the required probes succeeded. THE ONLY path that
  // produces recovery evidence.
  public Result Succeed(string actor, DateTimeOffset occurredUtc)
  {
    if (Status != TenantDatabaseRestoreVerificationStatus.Restoring)
    {
      return Result.Failure(TenantStorageErrors.RestoreVerificationNotRunning);
    }

    Status = TenantDatabaseRestoreVerificationStatus.Succeeded;
    ErrorSummary = null;
    Complete(actor, occurredUtc);
    return Result.Success();
  }

  // The verification ran and did not establish recoverability. What this means for readiness depends on how
  // deep it reached, which is the caller's classification rather than this entity's
  // (TenantDatabaseVerificationFailure).
  public Result Fail(string? errorSummary, string actor, DateTimeOffset occurredUtc)
  {
    if (Status is TenantDatabaseRestoreVerificationStatus.Succeeded or
      TenantDatabaseRestoreVerificationStatus.Failed)
    {
      return Result.Failure(TenantStorageErrors.RestoreVerificationAlreadyCompleted);
    }

    Status = TenantDatabaseRestoreVerificationStatus.Failed;
    ErrorSummary = Truncate(errorSummary);
    Complete(actor, occurredUtc);
    return Result.Success();
  }

  // The attempt could not begin or complete for reasons independent of the artifacts.
  //
  // A SEPARATE TERMINAL STATE, not a flavour of Fail, because it must not degrade readiness: a verification
  // host that is down says nothing about whether the backups would restore (ADR-022 §17, v1.2).
  public Result AbandonUnavailable(string? reasonSummary, string actor, DateTimeOffset occurredUtc)
  {
    if (Status is TenantDatabaseRestoreVerificationStatus.Succeeded or
      TenantDatabaseRestoreVerificationStatus.Failed)
    {
      return Result.Failure(TenantStorageErrors.RestoreVerificationAlreadyCompleted);
    }

    Status = TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable;
    ErrorSummary = Truncate(reasonSummary);
    Complete(actor, occurredUtc);
    return Result.Success();
  }

  // Disposal outcome, recorded INDEPENDENTLY of the verification result.
  //
  // There is deliberately no path here that can change `Status`: a cleanup failure must never turn a
  // succeeded verification into a failed one, and the way to guarantee that is to make it inexpressible.
  public Result RecordCleanup(
    TenantDatabaseVerificationCleanupState state,
    string? errorSummary,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (state is not (TenantDatabaseVerificationCleanupState.Succeeded or
      TenantDatabaseVerificationCleanupState.Failed))
    {
      return Result.Failure(TenantStorageErrors.RestoreVerificationCleanupStateInvalid);
    }

    CleanupState = state;
    if (state == TenantDatabaseVerificationCleanupState.Failed)
    {
      // Appended rather than replacing: a cleanup failure on a SUCCEEDED verification must not look like a
      // verification error, and on a failed one the original reason is the more important of the two.
      ErrorSummary = Truncate(string.IsNullOrWhiteSpace(ErrorSummary)
        ? errorSummary
        : $"{ErrorSummary} | cleanup: {errorSummary}");
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

using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// WHAT protection should exist for ONE physical tenant database (ADR-022 §4).
//
// A separate entity rather than a column set on TenantDatabase, for two reasons. Policy is the part of this
// model that will grow — three schedules, retention, destination, verification cadence, age tolerance, later
// service tiers and provider settings — and TenantDatabase is read on the ROUTING PATH, where a dozen extra
// columns would bloat the row every tenant request touches.
//
// The second reason is that three things must stay distinguishable and are easy to conflate:
//
//   Policy           — what protection SHOULD exist   (this entity)
//   Run history      — what actually happened          (TenantDatabaseBackupRun)
//   Recovery readiness — whether evidence MEETS policy  (TenantDatabase.RecoveryReadinessStatus)
//
// PROTECTION IS NEVER INFERRED FROM Enabled = true. This entity carries no readiness state at all, which is
// the structural reason a policy cannot make a database look recoverable.
//
// Bound to the PHYSICAL database, never to a Tenant and never to a TenantDatabaseAssignment: a shared
// database hosting a thousand tenants is ONE backup target with one chain, and per-assignment policy rows
// could disagree with each other about the same physical database.
public sealed class TenantDatabaseBackupPolicy : AggregateRoot<long>, IAuditableEntity
{
  // A trusted configuration LOOKUP KEY, sized like ServerKey for the same reason.
  public const int DestinationKeyMaximumLength = 128;

  public const int ActorMaximumLength = 256;

  // Bounds that keep a policy value obviously wrong rather than subtly wrong. A year of minutes and a
  // decade of days are both far outside anything operationally sensible.
  private const int MaximumIntervalMinutes = 525_600;

  private const int MaximumRetentionDays = 3_650;

  private TenantDatabaseBackupPolicy(
    long tenantDatabaseId,
    bool enabled,
    TenantDatabaseBackupManagementMode managementMode,
    string? destinationKey,
    int? fullBackupIntervalMinutes,
    int? differentialBackupIntervalMinutes,
    int? transactionLogBackupIntervalMinutes,
    int retentionExpectationDays,
    int? restoreVerificationIntervalDays,
    int? maximumBackupAgeMinutes,
    TenantDatabaseBackupCompressionMode compressionMode,
    TenantDatabaseBackupEncryptionMode encryptionMode,
    string actor,
    DateTimeOffset occurredUtc)
    : base(0)
  {
    TenantDatabaseId = tenantDatabaseId;
    Enabled = enabled;
    ManagementMode = managementMode;
    DestinationKey = destinationKey;
    FullBackupIntervalMinutes = fullBackupIntervalMinutes;
    DifferentialBackupIntervalMinutes = differentialBackupIntervalMinutes;
    TransactionLogBackupIntervalMinutes = transactionLogBackupIntervalMinutes;
    RetentionExpectationDays = retentionExpectationDays;
    RestoreVerificationIntervalDays = restoreVerificationIntervalDays;
    MaximumBackupAgeMinutes = maximumBackupAgeMinutes;
    CompressionMode = compressionMode;
    EncryptionMode = encryptionMode;
    CreatedUtc = occurredUtc.ToUniversalTime();
    CreatedBy = actor;
    ModifiedUtc = CreatedUtc;
    ModifiedBy = actor;
  }

  private TenantDatabaseBackupPolicy()
    : base(0)
  {
  }

  // The physical database this policy protects. One policy per physical database, enforced by a unique
  // index as well as here.
  public long TenantDatabaseId { get; private set; }

  public bool Enabled { get; private set; }

  // Backup AUTHORITY, independent of migration authority and of hosting mode (ADR-022 §5).
  public TenantDatabaseBackupManagementMode ManagementMode { get; private set; }
    = TenantDatabaseBackupManagementMode.AutomaticByPlatform;

  // A TRUSTED CONFIGURATION KEY and nothing else. No path, no UNC, no URL, no storage endpoint, no SAS
  // token, no access key, no resolved destination — resolution to a physical location happens entirely in
  // Infrastructure at execution time (ADR-022 §11, compliance rule 23). This column is the reason a caller
  // cannot choose where a complete copy of the database gets written.
  public string? DestinationKey { get; private set; }

  // ---- Schedule CONFIGURATION only. Phase A stores it; nothing evaluates or runs it.
  //
  // Intervals in minutes rather than cron expressions or a library-specific schedule type: due evaluation
  // ("last successful run + interval <= now") is expressible from this without committing Phase C to any
  // scheduling library, and a richer representation can be added when there is a scheduler to justify it.
  //
  // These three are the SQL SERVER V1 managed chain (ADR-022 §9). SQL Server is the only supported V1
  // provider; a future provider would register its own policy shape rather than being forced into this one.
  public int? FullBackupIntervalMinutes { get; private set; }

  public int? DifferentialBackupIntervalMinutes { get; private set; }

  public int? TransactionLogBackupIntervalMinutes { get; private set; }

  // What the storage lifecycle is expected to keep. The platform does NOT delete artifacts in V1
  // (ADR-022 §16); this records the expectation so drift can be reported, not enforced by deletion.
  public int RetentionExpectationDays { get; private set; }

  // How often an ACTUAL restore verification must happen. Null means the policy does not require periodic
  // real restores — which is a legitimate configuration for a shared database, and never satisfies the
  // pre-cutover gate for a dedicated one (ADR-022 §18).
  public int? RestoreVerificationIntervalDays { get; private set; }

  // The operative V1 recovery-point field. Formal RPO/RTO service tiers are DEFERRED (ADR-022, Future
  // Considerations) and are deliberately not modelled here.
  public int? MaximumBackupAgeMinutes { get; private set; }

  public TenantDatabaseBackupCompressionMode CompressionMode { get; private set; }
    = TenantDatabaseBackupCompressionMode.PreferredWhereSupported;

  public TenantDatabaseBackupEncryptionMode EncryptionMode { get; private set; }
    = TenantDatabaseBackupEncryptionMode.StorageManaged;

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<TenantDatabaseBackupPolicy> Create(
    long tenantDatabaseId,
    bool enabled,
    TenantDatabaseBackupManagementMode managementMode,
    string? destinationKey,
    int? fullBackupIntervalMinutes,
    int? differentialBackupIntervalMinutes,
    int? transactionLogBackupIntervalMinutes,
    int retentionExpectationDays,
    int? restoreVerificationIntervalDays,
    int? maximumBackupAgeMinutes,
    string actor,
    DateTimeOffset occurredUtc,
    TenantDatabaseBackupCompressionMode compressionMode =
      TenantDatabaseBackupCompressionMode.PreferredWhereSupported,
    TenantDatabaseBackupEncryptionMode encryptionMode =
      TenantDatabaseBackupEncryptionMode.StorageManaged)
  {
    if (tenantDatabaseId <= 0)
    {
      return Result.Failure<TenantDatabaseBackupPolicy>(TenantStorageErrors.TenantDatabaseRequired);
    }

    var normalizedKey = destinationKey?.Trim();
    var validation = Validate(
      enabled,
      managementMode,
      normalizedKey,
      fullBackupIntervalMinutes,
      differentialBackupIntervalMinutes,
      transactionLogBackupIntervalMinutes,
      retentionExpectationDays,
      restoreVerificationIntervalDays,
      maximumBackupAgeMinutes,
      encryptionMode);
    if (validation.IsFailure)
    {
      return Result.Failure<TenantDatabaseBackupPolicy>(validation.Error);
    }

    return Result.Success(new TenantDatabaseBackupPolicy(
      tenantDatabaseId,
      enabled,
      managementMode,
      string.IsNullOrEmpty(normalizedKey) ? null : normalizedKey,
      fullBackupIntervalMinutes,
      differentialBackupIntervalMinutes,
      transactionLogBackupIntervalMinutes,
      retentionExpectationDays,
      restoreVerificationIntervalDays,
      maximumBackupAgeMinutes,
      compressionMode,
      encryptionMode,
      actor,
      occurredUtc));
  }

  // Every field reachable here is durability-affecting and audited (ADR-022, Audit): a change to the
  // management mode or the destination key can move a database from protected to unprotected without any
  // operation failing.
  public Result Update(
    bool enabled,
    TenantDatabaseBackupManagementMode managementMode,
    string? destinationKey,
    int? fullBackupIntervalMinutes,
    int? differentialBackupIntervalMinutes,
    int? transactionLogBackupIntervalMinutes,
    int retentionExpectationDays,
    int? restoreVerificationIntervalDays,
    int? maximumBackupAgeMinutes,
    string actor,
    DateTimeOffset occurredUtc,
    TenantDatabaseBackupCompressionMode compressionMode =
      TenantDatabaseBackupCompressionMode.PreferredWhereSupported,
    TenantDatabaseBackupEncryptionMode encryptionMode =
      TenantDatabaseBackupEncryptionMode.StorageManaged)
  {
    var normalizedKey = destinationKey?.Trim();
    var validation = Validate(
      enabled,
      managementMode,
      normalizedKey,
      fullBackupIntervalMinutes,
      differentialBackupIntervalMinutes,
      transactionLogBackupIntervalMinutes,
      retentionExpectationDays,
      restoreVerificationIntervalDays,
      maximumBackupAgeMinutes,
      encryptionMode);
    if (validation.IsFailure)
    {
      return validation;
    }

    Enabled = enabled;
    ManagementMode = managementMode;
    DestinationKey = string.IsNullOrEmpty(normalizedKey) ? null : normalizedKey;
    FullBackupIntervalMinutes = fullBackupIntervalMinutes;
    DifferentialBackupIntervalMinutes = differentialBackupIntervalMinutes;
    TransactionLogBackupIntervalMinutes = transactionLogBackupIntervalMinutes;
    RetentionExpectationDays = retentionExpectationDays;
    RestoreVerificationIntervalDays = restoreVerificationIntervalDays;
    MaximumBackupAgeMinutes = maximumBackupAgeMinutes;
    CompressionMode = compressionMode;
    EncryptionMode = encryptionMode;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // The default authority for a database, derived ONCE at policy creation from hosting mode and then stored
  // independently (ADR-022 §5). It is a starting value, not a derivation: a platform-hosted database whose
  // regulated customer requires their own DBA to run backups is an ordinary configuration, and so is a
  // customer-hosted database whose customer is happy for us to protect it.
  public static TenantDatabaseBackupManagementMode DefaultManagementModeFor(
    TenantDatabaseHostingMode hostingMode) =>
    hostingMode == TenantDatabaseHostingMode.CustomerManaged
      ? TenantDatabaseBackupManagementMode.CustomerDba
      : TenantDatabaseBackupManagementMode.AutomaticByPlatform;

  private static Result Validate(
    bool enabled,
    TenantDatabaseBackupManagementMode managementMode,
    string? destinationKey,
    int? fullBackupIntervalMinutes,
    int? differentialBackupIntervalMinutes,
    int? transactionLogBackupIntervalMinutes,
    int retentionExpectationDays,
    int? restoreVerificationIntervalDays,
    int? maximumBackupAgeMinutes,
    TenantDatabaseBackupEncryptionMode encryptionMode)
  {
    if (destinationKey is { Length: > DestinationKeyMaximumLength })
    {
      return Result.Failure(TenantStorageErrors.BackupDestinationKeyInvalid);
    }

    // A destination is required only where the PLATFORM may execute. A CustomerDba policy records an
    // arrangement the platform does not carry out, so demanding a platform destination key for it would be
    // asking for a value that has no meaning.
    var platformMayExecute = managementMode != TenantDatabaseBackupManagementMode.CustomerDba;
    if (enabled && platformMayExecute && string.IsNullOrEmpty(destinationKey))
    {
      return Result.Failure(TenantStorageErrors.BackupDestinationKeyRequired);
    }

    if (!IsValidInterval(fullBackupIntervalMinutes) ||
      !IsValidInterval(differentialBackupIntervalMinutes) ||
      !IsValidInterval(transactionLogBackupIntervalMinutes) ||
      !IsValidInterval(maximumBackupAgeMinutes))
    {
      return Result.Failure(TenantStorageErrors.BackupScheduleInvalid);
    }

    // TRANSACTION-LOG BACKUPS ALONE ARE NOT A BACKUP STRATEGY (ADR-022 §9), and neither are differentials:
    // both restore only onto a full baseline. A policy that schedules them without a full is configuring a
    // chain with no base — files that look like backups and restore nothing.
    if (fullBackupIntervalMinutes is null &&
      (differentialBackupIntervalMinutes is not null || transactionLogBackupIntervalMinutes is not null))
    {
      return Result.Failure(TenantStorageErrors.BackupFullBaselineRequired);
    }

    // An enabled platform-executed policy that schedules nothing protects nothing.
    if (enabled && platformMayExecute && fullBackupIntervalMinutes is null)
    {
      return Result.Failure(TenantStorageErrors.BackupFullBaselineRequired);
    }

    if (retentionExpectationDays <= 0 || retentionExpectationDays > MaximumRetentionDays)
    {
      return Result.Failure(TenantStorageErrors.BackupRetentionInvalid);
    }

    if (restoreVerificationIntervalDays is not null &&
      (restoreVerificationIntervalDays <= 0 || restoreVerificationIntervalDays > MaximumRetentionDays))
    {
      return Result.Failure(TenantStorageErrors.BackupVerificationIntervalInvalid);
    }

    // A declared extension point, not a capability. Accepting it would imply managed key material this
    // architecture does not hold, and ADR-022 forbids storing that material in the Platform database.
    if (encryptionMode == TenantDatabaseBackupEncryptionMode.ProviderNative)
    {
      return Result.Failure(TenantStorageErrors.BackupEncryptionModeNotSupported);
    }

    return Result.Success();
  }

  private static bool IsValidInterval(int? minutes) =>
    minutes is null || (minutes > 0 && minutes <= MaximumIntervalMinutes);

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

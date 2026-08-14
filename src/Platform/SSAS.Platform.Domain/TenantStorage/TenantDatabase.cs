using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// ONE physical tenant-storage database endpoint (ADR-017) — not a tenant. TenantId is deliberately absent:
// a shared database hosts many tenants, and the tenant -> endpoint mapping belongs to
// TenantDatabaseAssignment. This is Platform operational metadata, so it is deliberately NOT
// ITenantOwnedEntity: routing and bootstrap infrastructure must read it without an ambient tenant filter.
//
// Only trusted routing metadata is persisted. Complete connection strings, plaintext passwords,
// certificates and private keys must never be stored here; credential material for platform-managed
// servers comes from trusted application configuration keyed by ServerKey. Customer endpoint address and
// credential-reference fields belong to ADR-021 and are not modelled in this slice.
public sealed class TenantDatabase : AggregateRoot<long>, IAuditableEntity
{
  public const int ServerKeyMaximumLength = 128;

  // SQL Server catalog names are sysname (128 Unicode characters).
  public const int DatabaseNameMaximumLength = 128;

  public const int ActorMaximumLength = 256;

  // Safe, bounded operator-facing failure summary. Never credential or connection material.
  public const int ErrorSummaryMaximumLength = 512;

  // EF migration identifiers are timestamp-prefixed names; sized generously rather than guessed tightly.
  public const int MigrationIdentifierMaximumLength = 256;

  private TenantDatabase(
    long id,
    TenantDatabaseHostingMode hostingMode,
    TenantDatabaseStorageMode storageMode,
    string serverKey,
    string databaseName,
    TenantDatabaseProvisioningStatus provisioningStatus,
    TenantDatabaseMigrationManagementMode migrationManagementMode,
    string actor,
    DateTimeOffset occurredUtc)
    : base(id)
  {
    HostingMode = hostingMode;
    StorageMode = storageMode;
    ServerKey = serverKey;
    DatabaseName = databaseName;
    ProvisioningStatus = provisioningStatus;
    MigrationManagementMode = migrationManagementMode;
    CreatedUtc = occurredUtc.ToUniversalTime();
    CreatedBy = actor;
    ModifiedUtc = CreatedUtc;
    ModifiedBy = actor;
  }

  private TenantDatabase()
    : base(0)
  {
    ServerKey = string.Empty;
    DatabaseName = string.Empty;
  }

  public TenantDatabaseHostingMode HostingMode { get; private set; }

  public TenantDatabaseStorageMode StorageMode { get; private set; }

  // A trusted configuration LOOKUP KEY, never a hostname, endpoint or connection string. The Platform
  // database therefore stores no address and no credential for a platform-managed server: one
  // configuration entry serves many rows, and rotating a server or credential is a configuration change.
  public string ServerKey { get; private set; }

  public string DatabaseName { get; private set; }

  public TenantDatabaseProvisioningStatus ProvisioningStatus { get; private set; }

  // ---- Migration authority (ADR-018). Independent of HostingMode: a customer-hosted database we are
  // permitted to migrate is an ordinary configuration, and so is a platform-hosted one gated behind
  // approval.
  public TenantDatabaseMigrationManagementMode MigrationManagementMode { get; private set; }
    = TenantDatabaseMigrationManagementMode.AutomaticByPlatform;

  // ---- The three health dimensions (ADR-018). They are maintained independently and NEVER merged into a
  // single Ready flag: readiness is a conclusion drawn from all of them plus routing, and a settable flag
  // would drift from the dimensions it claims to summarise.
  //
  // Every one of them defaults to Unknown/Idle, which the gating table treats as DENY. A newly registered
  // database is therefore not servable until something has actually verified it.
  public TenantDatabaseConnectivityStatus ConnectivityStatus { get; private set; }
    = TenantDatabaseConnectivityStatus.Unknown;

  public DateTimeOffset? LastConnectivityCheckUtc { get; private set; }

  public TenantDatabaseSchemaCompatibilityStatus SchemaCompatibilityStatus { get; private set; }
    = TenantDatabaseSchemaCompatibilityStatus.Unknown;

  // Freshness anchor for the ADR-018 staleness model. A stale COMPATIBLE status may still allow traffic
  // inside the grace window; a stale INCOMPATIBLE one never upgrades to allow.
  public DateTimeOffset? LastSchemaCheckUtc { get; private set; }

  // Cached observation, never authority — `tenant.__EFMigrationsHistory` is the source of truth (ADR-018).
  public string? AppliedMigration { get; private set; }

  // The migration head this application expects, recorded at check time so a stale row is interpretable.
  public string? TargetMigration { get; private set; }

  public TenantDatabaseMigrationExecutionStatus MigrationExecutionStatus { get; private set; }
    = TenantDatabaseMigrationExecutionStatus.Idle;

  public DateTimeOffset? LastMigrationAttemptUtc { get; private set; }

  public DateTimeOffset? LastMigrationSuccessUtc { get; private set; }

  public DateTimeOffset? LastMigrationFailureUtc { get; private set; }

  // Safe summary only. Connection strings, credentials and endpoint material must never reach this column.
  public string? LastMigrationError { get; private set; }

  // ---- The FOURTH dimension: recovery readiness (ADR-022 §2). Durability, kept strictly apart from the
  // three above. It has its own writer, its own cadence, and its own meaning; it is never merged into
  // schema or connectivity state, and it never gates ordinary ERP traffic (ADR-022 §7).
  //
  // Defaults to Unknown, and EXISTING ROWS BACKFILL TO Unknown rather than Protected: nothing has proven
  // those databases recoverable, and the lifecycle gates that read this must fail closed on an unverified
  // assumption. Protected is a conclusion drawn from evidence, never from a policy existing.
  public TenantDatabaseRecoveryReadinessStatus RecoveryReadinessStatus { get; private set; }
    = TenantDatabaseRecoveryReadinessStatus.Unknown;

  public DateTimeOffset? LastRecoveryReadinessCheckUtc { get; private set; }

  // Cached OBSERVATIONS, not history. Full run history lives in TenantDatabaseBackupRun; these few
  // timestamps exist so a cutover gate and an operator view can answer "when was this last protected?"
  // without scanning a run table that grows every fifteen minutes.
  public DateTimeOffset? LastSuccessfulFullBackupUtc { get; private set; }

  public DateTimeOffset? LastSuccessfulDifferentialBackupUtc { get; private set; }

  public DateTimeOffset? LastSuccessfulLogBackupUtc { get; private set; }

  // An ACTUAL restore verification, not RESTORE VERIFYONLY (ADR-022 §17). This is the timestamp the
  // pre-cutover gate depends on, which is why it is distinct from any readability check.
  public DateTimeOffset? LastRestoreVerificationUtc { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<TenantDatabase> Register(
    TenantDatabaseHostingMode hostingMode,
    TenantDatabaseStorageMode storageMode,
    string serverKey,
    string databaseName,
    TenantDatabaseProvisioningStatus provisioningStatus,
    string actor,
    DateTimeOffset occurredUtc,
    // Defaults to AutomaticByPlatform so existing platform-managed registration is unchanged; a
    // customer-managed or approval-gated estate sets it explicitly.
    TenantDatabaseMigrationManagementMode migrationManagementMode =
      TenantDatabaseMigrationManagementMode.AutomaticByPlatform)
  {
    if (string.IsNullOrWhiteSpace(serverKey) || serverKey.Length > ServerKeyMaximumLength)
    {
      return Result.Failure<TenantDatabase>(TenantStorageErrors.ServerKeyRequired);
    }

    if (string.IsNullOrWhiteSpace(databaseName) || databaseName.Length > DatabaseNameMaximumLength)
    {
      return Result.Failure<TenantDatabase>(TenantStorageErrors.DatabaseNameRequired);
    }

    // The one invalid hosting/storage combination (ADR-017). Enforced here AND by a database CHECK, so
    // neither an application path nor a direct SQL write can create it.
    if (hostingMode == TenantDatabaseHostingMode.CustomerManaged &&
      storageMode == TenantDatabaseStorageMode.Shared)
    {
      return Result.Failure<TenantDatabase>(TenantStorageErrors.CustomerManagedMustBeDedicated);
    }

    return Result.Success(new TenantDatabase(
      0, hostingMode, storageMode, serverKey, databaseName, provisioningStatus,
      migrationManagementMode, actor, occurredUtc));
  }

  // ---- Health transitions (ADR-018). Each dimension is recorded on its own; none of them touches
  // another, because that independence is the whole point of the four-dimension model.

  public Result RecordConnectivity(
    TenantDatabaseConnectivityStatus status,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (status == TenantDatabaseConnectivityStatus.Unknown)
    {
      // Unknown is the pre-verification state. A completed check always concludes something; writing
      // Unknown back would erase evidence rather than record it.
      return Result.Failure(TenantStorageErrors.ConnectivityResultRequired);
    }

    ConnectivityStatus = status;
    LastConnectivityCheckUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  public Result RecordSchemaHealth(
    TenantDatabaseSchemaCompatibilityStatus status,
    string? appliedMigration,
    string? targetMigration,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (appliedMigration is { Length: > MigrationIdentifierMaximumLength } ||
      targetMigration is { Length: > MigrationIdentifierMaximumLength })
    {
      return Result.Failure(TenantStorageErrors.MigrationIdentifierInvalid);
    }

    SchemaCompatibilityStatus = status;
    AppliedMigration = appliedMigration;
    TargetMigration = targetMigration;
    LastSchemaCheckUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // Ownership has already been acquired by the caller; this records that a run now owns the database.
  // Traffic is denied while Migrating, so this transition is deliberately explicit rather than implied by
  // the first DDL statement.
  public Result BeginMigration(string actor, DateTimeOffset occurredUtc)
  {
    if (MigrationManagementMode == TenantDatabaseMigrationManagementMode.CustomerDba)
    {
      // Defence in depth: the orchestrator checks authority before acquiring ownership, but the aggregate
      // refuses independently so no future caller can migrate a database it may never touch.
      return Result.Failure(TenantStorageErrors.MigrationNotPermittedByManagementMode);
    }

    if (MigrationExecutionStatus == TenantDatabaseMigrationExecutionStatus.Migrating)
    {
      return Result.Failure(TenantStorageErrors.MigrationAlreadyRunning);
    }

    MigrationExecutionStatus = TenantDatabaseMigrationExecutionStatus.Migrating;
    LastMigrationAttemptUtc = occurredUtc.ToUniversalTime();
    LastMigrationError = null;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // Called only after post-migration verification has re-read the actual history (ADR-018): a successful
  // migration API call is not evidence on its own.
  public Result CompleteMigration(string appliedMigration, string actor, DateTimeOffset occurredUtc)
  {
    if (string.IsNullOrWhiteSpace(appliedMigration) ||
      appliedMigration.Length > MigrationIdentifierMaximumLength)
    {
      return Result.Failure(TenantStorageErrors.MigrationIdentifierInvalid);
    }

    MigrationExecutionStatus = TenantDatabaseMigrationExecutionStatus.Succeeded;
    LastMigrationSuccessUtc = occurredUtc.ToUniversalTime();
    LastMigrationError = null;

    // AppliedMigration is deliberately NOT written here: it is schema-observation state, owned by
    // RecordSchemaHealth. The orchestrator re-reads the actual history after migrating and records that
    // observation explicitly, so the value always comes from something that looked at the database rather
    // than from what a migration call believed it had done.
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  public Result FailMigration(string errorSummary, string actor, DateTimeOffset occurredUtc)
  {
    MigrationExecutionStatus = TenantDatabaseMigrationExecutionStatus.Failed;
    LastMigrationFailureUtc = occurredUtc.ToUniversalTime();
    LastMigrationError = Truncate(errorSummary);
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // A database with pending migrations that the platform is not permitted to apply. ADR-018 requires this
  // be its own reported category, distinct from failure — the release is not broken.
  public Result BlockPendingCustomer(string reasonSummary, string actor, DateTimeOffset occurredUtc)
  {
    MigrationExecutionStatus = TenantDatabaseMigrationExecutionStatus.BlockedPendingCustomer;
    LastMigrationError = Truncate(reasonSummary);
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // Recovers a database left Migrating by a crashed run whose ownership has since lapsed. It records a
  // failure rather than silently returning to Idle: something did abort mid-flight, and that is evidence.
  public Result AbandonMigration(string reasonSummary, string actor, DateTimeOffset occurredUtc)
  {
    if (MigrationExecutionStatus != TenantDatabaseMigrationExecutionStatus.Migrating)
    {
      return Result.Failure(TenantStorageErrors.MigrationNotRunning);
    }

    return FailMigration(reasonSummary, actor, occurredUtc);
  }

  // Records ONE recovery-readiness observation. Writes recovery-owned fields and nothing else: connectivity,
  // schema and migration state are untouched here, exactly as those dimensions leave this one untouched.
  //
  // The backup timestamps are passed as OPTIONAL observations rather than mandatory arguments, so an
  // evaluation that learned nothing new about a given backup type leaves that timestamp alone instead of
  // erasing it. That is the aggregate-level expression of "a check that observes nothing writes nothing".
  public Result RecordRecoveryReadiness(
    TenantDatabaseRecoveryReadinessStatus status,
    string actor,
    DateTimeOffset occurredUtc,
    DateTimeOffset? lastSuccessfulFullBackupUtc = null,
    DateTimeOffset? lastSuccessfulDifferentialBackupUtc = null,
    DateTimeOffset? lastSuccessfulLogBackupUtc = null,
    DateTimeOffset? lastRestoreVerificationUtc = null)
  {
    if (status == TenantDatabaseRecoveryReadinessStatus.Unknown)
    {
      // Unknown is the pre-verification state. A completed evaluation always concludes something, and
      // writing Unknown back would erase evidence rather than record it — the same rule connectivity follows.
      return Result.Failure(TenantStorageErrors.RecoveryReadinessResultRequired);
    }

    RecoveryReadinessStatus = status;
    LastRecoveryReadinessCheckUtc = occurredUtc.ToUniversalTime();

    if (lastSuccessfulFullBackupUtc is not null)
    {
      LastSuccessfulFullBackupUtc = lastSuccessfulFullBackupUtc.Value.ToUniversalTime();
    }

    if (lastSuccessfulDifferentialBackupUtc is not null)
    {
      LastSuccessfulDifferentialBackupUtc = lastSuccessfulDifferentialBackupUtc.Value.ToUniversalTime();
    }

    if (lastSuccessfulLogBackupUtc is not null)
    {
      LastSuccessfulLogBackupUtc = lastSuccessfulLogBackupUtc.Value.ToUniversalTime();
    }

    if (lastRestoreVerificationUtc is not null)
    {
      LastRestoreVerificationUtc = lastRestoreVerificationUtc.Value.ToUniversalTime();
    }

    Touch(actor, occurredUtc);
    return Result.Success();
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

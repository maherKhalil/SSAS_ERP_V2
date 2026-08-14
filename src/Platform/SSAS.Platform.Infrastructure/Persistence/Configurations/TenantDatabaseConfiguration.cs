using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDatabaseConfiguration : IEntityTypeConfiguration<TenantDatabase>
{
  public void Configure(EntityTypeBuilder<TenantDatabase> builder)
  {
    builder.ToTable("TenantDatabases", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantDatabases_HostingMode",
        "[HostingMode] IN (N'PlatformManaged', N'CustomerManaged')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_StorageMode",
        "[StorageMode] IN (N'Shared', N'Dedicated')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_ProvisioningStatus",
        "[ProvisioningStatus] IN (N'Registered', N'Provisioning', N'Onboarding', N'Ready', N'Disabled')");
      // ADR-017: CustomerManaged implies Dedicated. The one invalid combination is rejected by the
      // database, so no application path and no direct SQL write can create it.
      table.HasCheckConstraint(
        "CK_TenantDatabases_CustomerManagedIsDedicated",
        "NOT ([HostingMode] = N'CustomerManaged' AND [StorageMode] = N'Shared')");
      table.HasCheckConstraint("CK_TenantDatabases_ServerKey_NotBlank", "LEN(LTRIM(RTRIM([ServerKey]))) > 0");
      table.HasCheckConstraint("CK_TenantDatabases_DatabaseName_NotBlank", "LEN(LTRIM(RTRIM([DatabaseName]))) > 0");

      // ADR-018 health dimensions. Each is constrained to its own closed value set; deliberately NOT
      // cross-constrained into a single composite "ready" rule, because the whole point of four orthogonal
      // dimensions is that unusual combinations (schema-current but unreachable, compatible with a failed
      // last attempt) are legitimate states an operator needs to see rather than states to forbid.
      table.HasCheckConstraint(
        "CK_TenantDatabases_ConnectivityStatus",
        "[ConnectivityStatus] IN (N'Unknown', N'Healthy', N'Unreachable', N'AuthenticationFailed')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_SchemaCompatibilityStatus",
        "[SchemaCompatibilityStatus] IN (N'Unknown', N'UpToDate', N'PendingMigrations', N'AheadOfApplication', N'MigrationHistoryMismatch')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_MigrationExecutionStatus",
        "[MigrationExecutionStatus] IN (N'Idle', N'Migrating', N'Succeeded', N'Failed', N'BlockedPendingCustomer')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_MigrationManagementMode",
        "[MigrationManagementMode] IN (N'AutomaticByPlatform', N'PlatformAfterApproval', N'CustomerDba')");

      // The one combination that is genuinely incoherent rather than merely unusual: a status can only be
      // trusted alongside the timestamp that dates it, and the gating model reads that timestamp.
      table.HasCheckConstraint(
        "CK_TenantDatabases_SchemaCheckedWhenKnown",
        "[SchemaCompatibilityStatus] = N'Unknown' OR [LastSchemaCheckUtc] IS NOT NULL");
      table.HasCheckConstraint(
        "CK_TenantDatabases_ConnectivityCheckedWhenKnown",
        "[ConnectivityStatus] = N'Unknown' OR [LastConnectivityCheckUtc] IS NOT NULL");

      // ADR-022 recovery readiness — the FOURTH dimension. Constrained to its own closed set and
      // deliberately NOT cross-constrained against the other three: a database can legitimately be
      // schema-current and unprotected, or well-protected and awaiting a release, and forbidding those
      // combinations would forbid states an operator needs to see.
      table.HasCheckConstraint(
        "CK_TenantDatabases_RecoveryReadinessStatus",
        "[RecoveryReadinessStatus] IN (N'Unknown', N'Protected', N'Degraded', N'Unprotected', " +
        "N'RecoveryModelInvalid', N'VerificationOverdue')");

      // Same coherence rule the other dimensions carry: a status can only be trusted alongside the
      // timestamp that dates it.
      table.HasCheckConstraint(
        "CK_TenantDatabases_RecoveryCheckedWhenKnown",
        "[RecoveryReadinessStatus] = N'Unknown' OR [LastRecoveryReadinessCheckUtc] IS NOT NULL");

      // PROTECTED REQUIRES EVIDENCE, NEVER CONFIGURATION (ADR-022 §6, compliance rule 11). A full baseline
      // is the weakest claim that could possibly justify the word, so the database refuses the combination
      // outright — no application path and no direct SQL write can mark a database protected that has never
      // had a successful full backup recorded.
      table.HasCheckConstraint(
        "CK_TenantDatabases_ProtectedRequiresFullBackup",
        "[RecoveryReadinessStatus] <> N'Protected' OR [LastSuccessfulFullBackupUtc] IS NOT NULL");
    });

    builder.HasKey(database => database.Id);
    builder.Property(database => database.Id).HasColumnName("TenantDatabaseId").UseIdentityColumn();

    builder.Property(database => database.HostingMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.StorageMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.ProvisioningStatus)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // ---- ADR-018 health and migration state. Stored on the PHYSICAL database row, never duplicated per
    // assignment: a shared database has one schema and one migration state.
    builder.Property(database => database.MigrationManagementMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.ConnectivityStatus)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.SchemaCompatibilityStatus)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.MigrationExecutionStatus)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    // ---- ADR-022 recovery readiness. Cached observations only; full history lives in
    // TenantDatabaseBackupRun, and policy lives in TenantDatabaseBackupPolicy.
    builder.Property(database => database.RecoveryReadinessStatus)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.LastRecoveryReadinessCheckUtc);
    builder.Property(database => database.LastSuccessfulFullBackupUtc);
    builder.Property(database => database.LastSuccessfulDifferentialBackupUtc);
    builder.Property(database => database.LastSuccessfulLogBackupUtc);
    builder.Property(database => database.LastRestoreVerificationUtc);

    builder.Property(database => database.LastConnectivityCheckUtc);
    builder.Property(database => database.LastSchemaCheckUtc);
    builder.Property(database => database.LastMigrationAttemptUtc);
    builder.Property(database => database.LastMigrationSuccessUtc);
    builder.Property(database => database.LastMigrationFailureUtc);
    builder.Property(database => database.AppliedMigration)
      .HasMaxLength(TenantDatabase.MigrationIdentifierMaximumLength);
    builder.Property(database => database.TargetMigration)
      .HasMaxLength(TenantDatabase.MigrationIdentifierMaximumLength);
    // Safe operator-facing summary only; bounded so a provider exception can never blow up the row.
    builder.Property(database => database.LastMigrationError)
      .HasMaxLength(TenantDatabase.ErrorSummaryMaximumLength);

    // Trusted routing metadata only: a configuration lookup key and a catalog name. No host, no port, no
    // credential and no connection string is persisted for any hosting mode.
    builder.Property(database => database.ServerKey)
      .HasMaxLength(TenantDatabase.ServerKeyMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.DatabaseName)
      .HasMaxLength(TenantDatabase.DatabaseNameMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // (ServerKey, DatabaseName) is the physical identity of an endpoint, so it also makes the bootstrap
    // idempotent under concurrent hosts: a losing racer hits this index rather than creating a duplicate.
    builder.HasIndex(database => new { database.ServerKey, database.DatabaseName })
      .IsUnique()
      .HasDatabaseName("UX_TenantDatabases_ServerKey_DatabaseName");

    builder.Property(database => database.CreatedUtc).IsRequired();
    builder.Property(database => database.CreatedBy).HasMaxLength(TenantDatabase.ActorMaximumLength);
    builder.Property(database => database.ModifiedUtc).IsRequired();
    builder.Property(database => database.ModifiedBy).HasMaxLength(TenantDatabase.ActorMaximumLength);
    builder.Property(database => database.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}

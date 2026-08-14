using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDatabaseBackupPolicyConfiguration
  : IEntityTypeConfiguration<TenantDatabaseBackupPolicy>
{
  public void Configure(EntityTypeBuilder<TenantDatabaseBackupPolicy> builder)
  {
    builder.ToTable("TenantDatabaseBackupPolicies", PlatformPersistenceConstants.Schema, table =>
    {
      // Closed sets enforced by the database as well as the domain, following the ADR-018 pattern: neither
      // an application path nor a direct SQL write can introduce a value the model cannot interpret.
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_ManagementMode",
        "[ManagementMode] IN (N'AutomaticByPlatform', N'PlatformAfterApproval', N'CustomerDba')");
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_CompressionMode",
        "[CompressionMode] IN (N'PreferredWhereSupported', N'Required', N'Disabled')");
      // ProviderNative is a declared extension point, not a V1 capability: it would imply managed key
      // material the Platform database must never hold.
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_EncryptionMode",
        "[EncryptionMode] IN (N'StorageManaged')");

      // A platform-EXECUTED policy that is enabled must name the trusted destination key it writes to.
      // A CustomerDba policy records an arrangement the platform never carries out, so it needs none.
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_DestinationWhenPlatformExecutes",
        "[Enabled] = CAST(0 AS bit) OR [ManagementMode] = N'CustomerDba' OR " +
        "([DestinationKey] IS NOT NULL AND LEN(LTRIM(RTRIM([DestinationKey]))) > 0)");

      // Log and differential backups restore only onto a full baseline (ADR-022 §9). A chain configured
      // without one is a set of files that look like backups and restore nothing.
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_FullBaselineRequired",
        "[FullBackupIntervalMinutes] IS NOT NULL OR " +
        "([DifferentialBackupIntervalMinutes] IS NULL AND [TransactionLogBackupIntervalMinutes] IS NULL)");

      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_IntervalsPositive",
        "([FullBackupIntervalMinutes] IS NULL OR [FullBackupIntervalMinutes] > 0) AND " +
        "([DifferentialBackupIntervalMinutes] IS NULL OR [DifferentialBackupIntervalMinutes] > 0) AND " +
        "([TransactionLogBackupIntervalMinutes] IS NULL OR [TransactionLogBackupIntervalMinutes] > 0) AND " +
        "([MaximumBackupAgeMinutes] IS NULL OR [MaximumBackupAgeMinutes] > 0)");

      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupPolicies_RetentionPositive",
        "[RetentionExpectationDays] > 0 AND " +
        "([RestoreVerificationIntervalDays] IS NULL OR [RestoreVerificationIntervalDays] > 0)");
    });

    builder.HasKey(policy => policy.Id);
    builder.Property(policy => policy.Id)
      .HasColumnName("TenantDatabaseBackupPolicyId")
      .UseIdentityColumn();

    builder.Property(policy => policy.TenantDatabaseId).IsRequired();
    builder.Property(policy => policy.Enabled).IsRequired();

    builder.Property(policy => policy.ManagementMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(policy => policy.CompressionMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(policy => policy.EncryptionMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // A trusted configuration LOOKUP KEY. No path, UNC, URL, endpoint, SAS token or access key is stored
    // here or anywhere else on this row — resolution happens in Infrastructure at execution time, which is
    // what keeps a caller from choosing where a complete copy of the database is written (ADR-022 §11).
    builder.Property(policy => policy.DestinationKey)
      .HasMaxLength(TenantDatabaseBackupPolicy.DestinationKeyMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation);

    builder.Property(policy => policy.FullBackupIntervalMinutes);
    builder.Property(policy => policy.DifferentialBackupIntervalMinutes);
    builder.Property(policy => policy.TransactionLogBackupIntervalMinutes);
    builder.Property(policy => policy.RetentionExpectationDays).IsRequired();
    builder.Property(policy => policy.RestoreVerificationIntervalDays);
    builder.Property(policy => policy.MaximumBackupAgeMinutes);

    // ONE policy per PHYSICAL database (ADR-022 §1, §4). A shared database hosting a thousand tenants is one
    // backup target; a second policy row for the same database could disagree with the first about it.
    builder.HasIndex(policy => policy.TenantDatabaseId)
      .IsUnique()
      .HasDatabaseName("UX_TenantDatabaseBackupPolicies_TenantDatabase");

    // Restrict, not Cascade. Removing a physical database should not silently take its durability
    // configuration with it: an unexpected policy row is a visible error, whereas a silently vanished one
    // is exactly the kind of quiet durability loss this ADR exists to prevent.
    builder.HasOne<TenantDatabase>()
      .WithMany()
      .HasForeignKey(policy => policy.TenantDatabaseId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(policy => policy.CreatedUtc).IsRequired();
    builder.Property(policy => policy.CreatedBy).HasMaxLength(TenantDatabaseBackupPolicy.ActorMaximumLength);
    builder.Property(policy => policy.ModifiedUtc).IsRequired();
    builder.Property(policy => policy.ModifiedBy).HasMaxLength(TenantDatabaseBackupPolicy.ActorMaximumLength);
    builder.Property(policy => policy.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}

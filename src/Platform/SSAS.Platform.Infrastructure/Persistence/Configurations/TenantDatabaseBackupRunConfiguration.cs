using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDatabaseBackupRunConfiguration : IEntityTypeConfiguration<TenantDatabaseBackupRun>
{
  public void Configure(EntityTypeBuilder<TenantDatabaseBackupRun> builder)
  {
    builder.ToTable("TenantDatabaseBackupRuns", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_Status",
        "[Status] IN (N'Pending', N'Running', N'Succeeded', N'Failed', N'SkippedOwnershipHeld', " +
        "N'SkippedInFlightOperation', N'BlockedByPolicy', N'VerificationFailed')");
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_VerificationState",
        "[VerificationState] IN (N'NotVerified', N'ReadabilityVerified', N'RestoreVerified', N'VerificationFailed')");

      // The operation is a PROVIDER-SCOPED pair, never a universal enum (ADR-022 §10). The provider key is
      // constrained to the only provider that exists in V1; a future provider widens this constraint rather
      // than remapping its chain onto SQL Server's three names.
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_ProviderKey",
        "[OperationProviderKey] IN (N'SqlServer')");
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_OperationCode_NotBlank",
        "LEN(LTRIM(RTRIM([OperationCode]))) > 0");

      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_CompletedNotBeforeStarted",
        "[CompletedUtc] IS NULL OR [CompletedUtc] >= [StartedUtc]");

      // SUCCESS REQUIRES PROVIDER EVIDENCE (ADR-022 §14, §15). Enforced by the database as well as the
      // aggregate, because "the command returned" is the single most tempting way to record a backup that
      // does not exist.
      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_SucceededHasEvidence",
        "[Status] <> N'Succeeded' OR ([ProviderBackupIdentity] IS NOT NULL AND [CompletedUtc] IS NOT NULL)");

      table.HasCheckConstraint(
        "CK_TenantDatabaseBackupRuns_SizeNotNegative",
        "[SizeBytes] IS NULL OR [SizeBytes] >= 0");
    });

    builder.HasKey(run => run.Id);
    builder.Property(run => run.Id).HasColumnName("TenantDatabaseBackupRunId").UseIdentityColumn();

    builder.Property(run => run.TenantDatabaseId).IsRequired();

    // Provider-scoped operation identity, stored as two columns rather than one enum column.
    builder.ComplexProperty(run => run.Operation, operation =>
    {
      operation.Property(value => value.ProviderKey)
        .HasColumnName("OperationProviderKey")
        .HasMaxLength(TenantDatabaseBackupOperation.ProviderKeyMaximumLength)
        .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
        .IsRequired();
      operation.Property(value => value.OperationCode)
        .HasColumnName("OperationCode")
        .HasMaxLength(TenantDatabaseBackupOperation.OperationCodeMaximumLength)
        .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
        .IsRequired();
    });

    builder.Property(run => run.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(run => run.VerificationState)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(run => run.StartedUtc).IsRequired();
    builder.Property(run => run.CompletedUtc);
    builder.Property(run => run.LastVerifiedUtc);

    // The trusted KEY the run was directed at and a SAFE artifact handle — never a resolved path, UNC, URL
    // or signed link, and never anything credential-bearing.
    builder.Property(run => run.DestinationKey)
      .HasMaxLength(TenantDatabaseBackupRun.DestinationKeyMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation);
    builder.Property(run => run.ArtifactReference)
      .HasMaxLength(TenantDatabaseBackupRun.ArtifactReferenceMaximumLength);
    builder.Property(run => run.ProviderBackupIdentity)
      .HasMaxLength(TenantDatabaseBackupRun.ProviderBackupIdentityMaximumLength);
    builder.Property(run => run.SizeBytes);

    // Safe operator-facing summary only, bounded so a provider exception can never blow up the row.
    builder.Property(run => run.ErrorSummary)
      .HasMaxLength(TenantDatabaseBackupRun.ErrorSummaryMaximumLength);

    // Chain metadata. SQL Server LSNs are numeric(25,0), which no integral CLR type covers.
    builder.Property(run => run.FirstLsn).HasColumnType("decimal(25,0)");
    builder.Property(run => run.LastLsn).HasColumnType("decimal(25,0)");
    builder.Property(run => run.DatabaseBackupLsn).HasColumnType("decimal(25,0)");

    // Nullable by design: rows written before Phase D7 never captured it, and are left NULL rather than
    // backfilled from a column that merely happened to match (TS-Backup Phase D7).
    builder.Property(run => run.CheckpointLsn).HasColumnType("decimal(25,0)");
    builder.Property(run => run.BackupSetGuid);

    // The questions run history exists to answer: what happened to this database most recently, and what
    // was the last successful operation of a given type. Two indexes, deliberately — history grows fastest
    // of anything in this model and over-indexing it would tax every write.
    builder.HasIndex(run => new { run.TenantDatabaseId, run.StartedUtc })
      .HasDatabaseName("IX_TenantDatabaseBackupRuns_TenantDatabase_Started");
    builder.HasIndex(run => new { run.TenantDatabaseId, run.Status, run.StartedUtc })
      .HasDatabaseName("IX_TenantDatabaseBackupRuns_TenantDatabase_Status_Started");

    // Restrict, not Cascade: backup history is the evidence a database was protected, and the moment a
    // physical database row is removed is exactly when that evidence matters. No physical artifact deletion
    // exists in V1 anyway (ADR-022 §16), so cascading would destroy the record while leaving the files.
    builder.HasOne<TenantDatabase>()
      .WithMany()
      .HasForeignKey(run => run.TenantDatabaseId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(run => run.CreatedUtc).IsRequired();
    builder.Property(run => run.CreatedBy).HasMaxLength(TenantDatabaseBackupRun.ActorMaximumLength);
    builder.Property(run => run.ModifiedUtc).IsRequired();
    builder.Property(run => run.ModifiedBy).HasMaxLength(TenantDatabaseBackupRun.ActorMaximumLength);
    builder.Property(run => run.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}

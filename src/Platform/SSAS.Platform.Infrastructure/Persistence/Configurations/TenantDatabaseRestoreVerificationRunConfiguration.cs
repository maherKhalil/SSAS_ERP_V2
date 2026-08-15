using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDatabaseRestoreVerificationRunConfiguration
  : IEntityTypeConfiguration<TenantDatabaseRestoreVerificationRun>
{
  public void Configure(EntityTypeBuilder<TenantDatabaseRestoreVerificationRun> builder)
  {
    builder.ToTable("TenantDatabaseRestoreVerificationRuns", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantDatabaseRestoreVerificationRuns_Status",
        "[Status] IN (N'Admitted', N'Restoring', N'Succeeded', N'Failed', N'InfrastructureUnavailable')");

      table.HasCheckConstraint(
        "CK_TenantDatabaseRestoreVerificationRuns_CleanupState",
        "[CleanupState] IN (N'NotRequired', N'Pending', N'Succeeded', N'Failed')");

      table.HasCheckConstraint(
        "CK_TenantDatabaseRestoreVerificationRuns_Depth",
        "[Depth] IN (N'Full', N'FullWithDifferential', N'FullWithDifferentialAndLog')");

      table.HasCheckConstraint(
        "CK_TenantDatabaseRestoreVerificationRuns_CompletedNotBeforeStarted",
        "[CompletedUtc] IS NULL OR [CompletedUtc] >= [StartedUtc]");

      // A RESTORING RUN MUST NAME ITS DATABASE. This is the crash-survivability guarantee expressed in the
      // schema: the record identifies the database before the database exists, so a process that dies
      // mid-restore cannot leave one that nothing can correlate (ADR-022 §17).
      table.HasCheckConstraint(
        "CK_TenantDatabaseRestoreVerificationRuns_RestoringHasDatabaseName",
        "[Status] = N'Admitted' OR [VerificationDatabaseName] IS NOT NULL");

      // Cleanup can only be pending or resolved once a database has been named.
      table.HasCheckConstraint(
        "CK_TenantDatabaseRestoreVerificationRuns_CleanupRequiresDatabaseName",
        "[CleanupState] = N'NotRequired' OR [VerificationDatabaseName] IS NOT NULL");
    });

    builder.HasKey(run => run.Id);
    builder.Property(run => run.Id)
      .HasColumnName("TenantDatabaseRestoreVerificationRunId")
      .UseIdentityColumn();

    builder.Property(run => run.TenantDatabaseId).IsRequired();
    builder.Property(run => run.SourceBackupRunId).IsRequired();

    // Enum-as-string, matching the backup-run precedent: a readable row beats a byte, and the check
    // constraints above are expressible only against the text.
    builder.Property(run => run.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(run => run.CleanupState)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(run => run.Depth)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // NULLABLE, and only until the run is about to create a database.
    //
    // The alternative — naming the database at admission — would require the run identity before the row
    // exists. Assigning it inside the admission transaction keeps the identity in the name (so an operator
    // finding a stray database can reach its record by primary key) while every committed restoring row
    // still carries a name, which the check constraint above enforces.
    builder.Property(run => run.VerificationDatabaseName)
      .HasMaxLength(TenantDatabaseRestoreVerificationRun.VerificationDatabaseNameMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation);

    builder.Property(run => run.RestoreServerKey)
      .HasMaxLength(TenantDatabaseRestoreVerificationRun.RestoreServerKeyMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(run => run.StartedUtc).IsRequired();
    builder.Property(run => run.CompletedUtc);

    builder.Property(run => run.ErrorSummary)
      .HasMaxLength(TenantDatabaseRestoreVerificationRun.ErrorSummaryMaximumLength);

    // ---- THE ADMISSION INVARIANT, ENFORCED BY THE DATABASE (ADR-022 compliance rule 43).
    //
    // A FILTERED UNIQUE INDEX over the physical database, restricted to runs that are still active. Two
    // instances that both observe the same database as due will both attempt an INSERT; SQL Server admits
    // exactly one and the other receives a duplicate-key violation.
    //
    // THIS IS WHY IT IS AN INDEX AND NOT A CLAIM ON AN EXISTING ROW. A claim serialises workers competing
    // for the SAME record, which does nothing when each instance creates its own — that is Phase C's
    // stale-decision duplicate re-keyed. The serialising event has to be the CREATION, and a unique index is
    // the simplest mechanism the Platform database already offers that covers it.
    builder.HasIndex(run => run.TenantDatabaseId)
      .HasDatabaseName("UX_TenantDatabaseRestoreVerificationRuns_ActiveTenantDatabase")
      .IsUnique()
      .HasFilter("[Status] IN (N'Admitted', N'Restoring')");

    // No two runs may name the same verification database. Structurally redundant — the run identity is in
    // the name — but a database that is about to be dropped by automation deserves a uniqueness guarantee
    // that does not depend on a generator staying correct.
    builder.HasIndex(run => run.VerificationDatabaseName)
      .HasDatabaseName("UX_TenantDatabaseRestoreVerificationRuns_DatabaseName")
      .IsUnique()
      .HasFilter("[VerificationDatabaseName] IS NOT NULL");

    // NO HISTORY INDEX YET, deliberately. The queries this slice actually makes are admission (served by the
    // filtered unique index above), lifecycle by primary key, and the FK lookups EF indexes on its own. A
    // `(TenantDatabaseId, StartedUtc)` index would serve the readiness and orphan-reconciliation reads that
    // arrive with later slices — and adding it now would be indexing a query nobody has written, on a table
    // that grows with every verification.

    // Restrict, not Cascade, for the same reason backup history is: a verification run is the evidence a
    // database was demonstrably recoverable, and the moment a row is removed is when that matters most.
    builder.HasOne<TenantDatabase>()
      .WithMany()
      .HasForeignKey(run => run.TenantDatabaseId)
      .OnDelete(DeleteBehavior.Restrict);

    // The baseline this verification exercised. Restrict as well: losing the link would leave a verification
    // that cannot be related to the chain it proved, which is exactly what the cutover gate needs it for.
    builder.HasOne<TenantDatabaseBackupRun>()
      .WithMany()
      .HasForeignKey(run => run.SourceBackupRunId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(run => run.CreatedUtc).IsRequired();
    builder.Property(run => run.CreatedBy)
      .HasMaxLength(TenantDatabaseRestoreVerificationRun.ActorMaximumLength);
    builder.Property(run => run.ModifiedUtc).IsRequired();
    builder.Property(run => run.ModifiedBy)
      .HasMaxLength(TenantDatabaseRestoreVerificationRun.ActorMaximumLength);
    builder.Property(run => run.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}

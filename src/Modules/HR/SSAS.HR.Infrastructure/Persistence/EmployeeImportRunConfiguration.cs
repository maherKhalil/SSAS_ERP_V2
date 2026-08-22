using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Infrastructure.Persistence;

// The append-only record of one employee import (FP-009 data-model, DEC-DOC-0006).
//
// THERE IS NO RowVersion, NO ModifiedUtc/ModifiedBy. The row is written once, when the outcome is already
// known, and never updated — so it has no concurrency state to protect and no modification metadata to
// record. That is the `EmployeeBranchAssignment` mapping, deliberately, rather than the
// `TenantDatabaseBackupRun` one: the latter describes a run that starts, is observed and ends.
//
// NO `defaultValue` ON ANY COLUMN, per the FP-008 review finding: the scaffolder emits them and they
// silently blind data. Required columns are required from the first row, and neither table has an existing
// row a default could have served.
public sealed class EmployeeImportRunConfiguration : IEntityTypeConfiguration<EmployeeImportRun>
{
  public void Configure(EntityTypeBuilder<EmployeeImportRun> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("EmployeeImportRuns", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint(
        "CK_EmployeeImportRuns_Outcome",
        "[Outcome] IN (N'Validated', N'Applied', N'Refused')");

      // COUNTS DESCRIBE A POSSIBLE RUN. The domain refuses these first so the pipeline learns about its own
      // arithmetic before the database does; the constraint is the backstop for any path that does not go
      // through the factory.
      table.HasCheckConstraint(
        "CK_EmployeeImportRuns_Counts_NonNegative",
        "[ByteCount] >= 0 AND [RowCount] >= 0 AND [AcceptedCount] >= 0 AND [RejectedCount] >= 0");

      // ---- ALL-OR-NOTHING, ENFORCED IN THE SCHEMA (`OD-DOC-003`).
      //
      // `acceptedCount` is either `rowCount` or `0` and never anything between. An `Applied` run with 998 of
      // 1000 accepted is not a state this system can reach, and the row that would record one cannot be
      // written — not because every writer remembers the rule, but because the table refuses it.
      table.HasCheckConstraint(
        "CK_EmployeeImportRuns_AllOrNothing",
        "([Outcome] = N'Refused' AND [AcceptedCount] = 0) OR " +
        "([Outcome] <> N'Refused' AND [AcceptedCount] = [RowCount])");

      // A file cannot reject rows it did not contain.
      table.HasCheckConstraint(
        "CK_EmployeeImportRuns_RejectedWithinRowCount",
        "[RejectedCount] <= [RowCount]");
    });

    builder.HasKey(run => run.Id);
    builder.Property(run => run.Id)
      .HasColumnName("ImportRunId")
      .ValueGeneratedNever();

    builder.Property(run => run.TenantId).IsRequired();
    builder.Property(run => run.CompanyId).IsRequired();

    // ---- THE DISPLAY VALUE AND THE NORMALIZED VALUE ARE TWO COLUMNS, AND THAT IS `DEC-POS-0030`.
    //
    // EF translates a value-converted property in a PROJECTION but NOT in a PREDICATE. The unique index and
    // every replay lookup run on `NormalizedImportKey`, which is an ordinary string property EF can put in a
    // WHERE clause; the converted `ImportKey` exists so a run report echoes the caller's own casing back.
    builder.Property(run => run.ImportKey)
      .HasConversion(key => key.Value, value => ImportKey.Create(value).Value)
      .HasMaxLength(ImportKey.MaximumLength)
      .IsRequired();
    builder.Property(run => run.NormalizedImportKey)
      .HasMaxLength(ImportKey.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    // Audit-only. Never used to locate anything, never interpreted, never indexed.
    builder.Property(run => run.FileName)
      .HasMaxLength(EmployeeImportRun.FileNameMaximumLength)
      .IsRequired();

    builder.Property(run => run.ByteCount).IsRequired();
    builder.Property(run => run.RowCount).IsRequired();
    builder.Property(run => run.AcceptedCount).IsRequired();
    builder.Property(run => run.RejectedCount).IsRequired();

    builder.Property(run => run.Outcome)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    builder.Property(run => run.ExecutedUtc).IsRequired();
    builder.Property(run => run.ExecutedBy)
      .HasMaxLength(EmployeeImportRun.ActorMaximumLength)
      .IsRequired();

    builder.Property(run => run.CreatedUtc).IsRequired();
    builder.Property(run => run.CreatedBy)
      .HasMaxLength(EmployeeImportRun.ActorMaximumLength);

    // The Modified pair from IAuditableEntity is explicitly NOT persisted: this record is never modified, so
    // a column for it would be permanently equal to its created counterpart and imply otherwise.
    builder.Ignore("ModifiedUtc");
    builder.Ignore("ModifiedBy");

    // ---- THE IMPORT KEY IS UNIQUE WITHIN A COMPANY, AND THE INDEX IS FILTERED ON NOTHING.
    //
    // Company-scoped for the reason employee number and department code are: two companies in one tenant are
    // not obliged to coordinate their key choices.
    //
    // FILTERED ON NOTHING, DELIBERATELY. A key is consumed even by a REFUSED run. An index that excluded
    // refusals would release the key of a failed import and let the very submission the key exists to make
    // unrepeatable be replayed under it.
    //
    // Binary collation makes it authoritative under concurrent submission rather than merely advisory.
    builder.HasIndex(run => new { run.CompanyId, run.NormalizedImportKey })
      .IsUnique()
      .HasDatabaseName("UX_EmployeeImportRuns_Company_Key");

    // "What did this company import, most recent first" — the only query this table is read by.
    builder.HasIndex(run => new { run.TenantId, run.CompanyId, run.ExecutedUtc })
      .HasDatabaseName("IX_EmployeeImportRuns_TenantId_CompanyId_ExecutedUtc");
  }
}

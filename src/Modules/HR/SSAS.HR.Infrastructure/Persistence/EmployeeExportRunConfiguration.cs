using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Infrastructure.Persistence;

// The append-only record that employee data left the system (FP-009 data-model, DEC-DOC-0006, SEC-DOC-0404).
//
// ================================================================================================
// `CompanyId` IS MAPPED AS DATA. THE ENTITY IS NOT `ICompanyOwnedEntity`, AND THE MAPPING KEEPS IT SO.
// ================================================================================================
//
// The column is not mapped to `ICompanyOwnedEntity.CompanyId` and the type does not implement it, because
// `TenantDbContext` treats a tracked company-owned entity as a company-scoped WRITE — demanding a trusted
// company context and authorizing it. An export is a read, and its audit record must not be refusable by
// write authorization. The full reasoning is on the entity.
//
// The company foreign key below is therefore an ordinary referential constraint on an ordinary column, not
// an ownership declaration — the two are separate things and this table is where the difference is visible.
//
// THERE IS NO RowVersion AND NO ModifiedUtc/ModifiedBy, for the reason its sibling states.
public sealed class EmployeeExportRunConfiguration : IEntityTypeConfiguration<EmployeeExportRun>
{
  public void Configure(EntityTypeBuilder<EmployeeExportRun> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("EmployeeExportRuns", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint("CK_EmployeeExportRuns_RowCount_NonNegative", "[RowCount] >= 0");

      // A RECORD THAT NAMES NO COLUMNS RECORDS THAT NOTHING LEFT, which is not an export — and this table
      // exists precisely to say what did leave. `SEC-DOC-0404` is the whole reason the column is here, so an
      // empty one would be the failure it is meant to detect.
      table.HasCheckConstraint("CK_EmployeeExportRuns_ColumnSet_Present", "LEN([ColumnSet]) > 0");
    });

    builder.HasKey(run => run.Id);
    builder.Property(run => run.Id)
      .HasColumnName("ExportRunId")
      .ValueGeneratedNever();

    builder.Property(run => run.TenantId).IsRequired();
    builder.Property(run => run.CompanyId).IsRequired();

    builder.Property(run => run.RowCount).IsRequired();

    builder.Property(run => run.ColumnSet)
      .HasMaxLength(EmployeeExportRun.ColumnSetMaximumLength)
      .IsRequired();

    // ---- THE SCOPE SNAPSHOT: TWO TEXT COLUMNS, NOT TWO CHILD TABLES.
    //
    // `nvarchar(max)` because a scope is unbounded in principle and this value is never compared, joined or
    // filtered on — it is read by a human investigating an incident. Child tables would have added two
    // entities to the E3 manifest and two foreign-key edges to the copy order for data nothing queries.
    //
    // NOT NULL and possibly EMPTY: an empty list is a real answer ("the scope resolved to no branches"),
    // and NULL would make it indistinguishable from "not recorded".
    builder.Property(run => run.ScopeCompanyIds).IsRequired();
    builder.Property(run => run.ScopeBranchIds).IsRequired();

    builder.Property(run => run.ExecutedUtc).IsRequired();
    builder.Property(run => run.ExecutedBy)
      .HasMaxLength(EmployeeExportRun.ActorMaximumLength)
      .IsRequired();

    builder.Property(run => run.CreatedUtc).IsRequired();
    builder.Property(run => run.CreatedBy)
      .HasMaxLength(EmployeeExportRun.ActorMaximumLength);

    builder.Ignore("ModifiedUtc");
    builder.Ignore("ModifiedBy");

    // "Who exported from this company, most recent first" — the investigator's query, and the only one.
    // NOTHING INDEXES `ColumnSet` OR EITHER SCOPE COLUMN: they are written once and never queried, so an
    // index would serve nothing. Adding one later costs nothing, which is the test for leaving it out now.
    builder.HasIndex(run => new { run.TenantId, run.CompanyId, run.ExecutedUtc })
      .HasDatabaseName("IX_EmployeeExportRuns_TenantId_CompanyId_ExecutedUtc");
  }
}

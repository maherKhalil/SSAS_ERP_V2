using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Infrastructure.Persistence;

// PAYROLL'S SIX TABLES. Every string column is `nvarchar` (`DEC-PAY-0007`) and every monetary column is
// `decimal(19,4)` (`DEC-PAY-0004`). No foreign key leaves the tenant catalog (`DEC-PAY-0008`), and none
// points at GL's tables at all — the module boundary is kept at the schema layer as well as the assembly
// layer, or it would be a fiction in exactly the place that is hardest to notice.
public sealed class PayElementConfiguration : IEntityTypeConfiguration<PayElement>
{
  public void Configure(EntityTypeBuilder<PayElement> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollElements", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(element => element.Id);

    builder.Property(element => element.TenantId).IsRequired();
    builder.Property(element => element.CompanyId).IsRequired();

    // Display value, casing preserved. Value-converted, so projectable but NOT usable in a predicate — which
    // is what the normalized shadow is for (`DEC-POS-0030`).
    builder.Property(element => element.Code)
      .HasConversion(code => code.Value, value => PayElementCode.Create(value).Value)
      .HasMaxLength(PayElementCode.MaximumLength)
      .IsRequired();

    builder.Property(element => element.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(PayElementCode.MaximumLength)
      .UseCollation(PayrollPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(element => element.Name)
      .HasConversion(name => name.Value, value => PayElementName.Create(value).Value)
      .HasMaxLength(PayElementName.MaximumLength)
      .IsRequired();

    // The search column. No index: a leading-wildcard LIKE cannot seek, so an index would be write cost
    // buying nothing — the same reasoning HR and GL applied to their search columns.
    builder.Property(element => element.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(PayElementName.MaximumLength)
      .UseCollation(PayrollPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // Stored as their integer values. The enums are a closed set (`OD-PAY-0006`) and a string column would
    // invite a value nobody implemented.
    builder.Property(element => element.Kind).HasConversion<int>().IsRequired();
    builder.Property(element => element.Behaviour).HasConversion<int>().IsRequired();

    builder.Property(element => element.DefaultRateOrAmount)
      .HasPrecision(PayrollPersistenceConstants.MoneyPrecision, PayrollPersistenceConstants.MoneyScale)
      .IsRequired();

    builder.Property(element => element.CalculationOrder).IsRequired();

    // FP-013. Bounded to match `AttendanceRecord.OvertimeTierMaximumLength`: the two sides of the same label
    // must agree, and an unbounded nvarchar(max) here would let a tier be stored that Attendance could never
    // have recorded.
    builder.Property(element => element.OvertimeTier)
      .HasMaxLength(PayElement.OvertimeTierMaximumLength);

    // Nullable: an element can exist before anyone has decided where it posts. What is refused is APPROVING
    // a run containing an unmapped element (`OD-PAY-0012`) — a domain rule, not a column constraint, because
    // the database cannot know which elements a run actually used.
    builder.Property(element => element.GlAccountId);

    builder.Property(element => element.IsActive).IsRequired();
    builder.Property(element => element.CreatedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);
    builder.Property(element => element.ModifiedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);

    // Company-scoped uniqueness (`OD-PAY-0005`), which is the contrast with `Account`'s tenant-wide unique
    // code: two companies in one tenant may each have their own "BASIC".
    builder.HasIndex(element => new { element.TenantId, element.CompanyId, element.NormalizedCode })
      .IsUnique();
  }
}

public sealed class EmployeeCompensationConfiguration : IEntityTypeConfiguration<EmployeeCompensation>
{
  public void Configure(EntityTypeBuilder<EmployeeCompensation> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollEmployeeCompensation", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(record => record.Id);

    builder.Property(record => record.TenantId).IsRequired();
    builder.Property(record => record.CompanyId).IsRequired();

    // The HR employee by identifier only. There is NO foreign key to HR's `Employee` table and no navigation
    // property: `ADR-012` keeps the modules apart, and a database-level key would couple their migrations.
    builder.Property(record => record.EmployeeId).IsRequired();

    builder.Property(record => record.EffectiveFromUtc).IsRequired();

    builder.Property(record => record.BaseAmount)
      .HasPrecision(PayrollPersistenceConstants.MoneyPrecision, PayrollPersistenceConstants.MoneyScale)
      .IsRequired();

    builder.Property(record => record.WasOutsideGradeBand).IsRequired();
    builder.Property(record => record.GradeBandObservation)
      .HasMaxLength(PayrollPersistenceConstants.ObservationMaximumLength);

    builder.Property(record => record.CreatedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);
    builder.Property(record => record.ModifiedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);

    // ---- THE ONLY ACCESS PATH THAT MATTERS.
    //
    // "What was in force for this employee on this date" is the question every calculation asks, and the
    // descending effective date lets it be answered with a seek and a TOP 1 rather than a scan and a sort.
    builder.HasIndex(record => new { record.TenantId, record.CompanyId, record.EmployeeId, record.EffectiveFromUtc })
      .IsDescending(false, false, false, true);

    // ---- NO RowVersion COLUMN, DELIBERATELY (see the aggregate's comment).
    //
    // A history row is never updated, so there is no concurrent update for a version to detect. Adding one
    // would advertise an update path that does not exist.

    builder.HasMany(record => record.Assignments)
      .WithOne()
      .HasForeignKey(assignment => assignment.EmployeeCompensationId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Metadata
      .FindNavigation(nameof(EmployeeCompensation.Assignments))!
      .SetPropertyAccessMode(PropertyAccessMode.Field);
  }
}

public sealed class PayElementAssignmentConfiguration : IEntityTypeConfiguration<PayElementAssignment>
{
  public void Configure(EntityTypeBuilder<PayElementAssignment> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollElementAssignments", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(assignment => assignment.Id);

    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.EmployeeCompensationId).IsRequired();
    builder.Property(assignment => assignment.PayElementId).IsRequired();

    // Nullable: null means "use the element's default". Storing a copy of the default would freeze it, and a
    // later change to the element would then silently not apply to anyone.
    builder.Property(assignment => assignment.RateOrAmount)
      .HasPrecision(PayrollPersistenceConstants.MoneyPrecision, PayrollPersistenceConstants.MoneyScale);

    // One assignment per element per compensation record. The domain refuses a duplicate too; this makes the
    // database agree rather than leaving the two to disagree quietly, because a duplicate would double-count
    // the element in every run — silently, while the total still looks like a number.
    builder.HasIndex(assignment => new { assignment.EmployeeCompensationId, assignment.PayElementId })
      .IsUnique();
  }
}

public sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
  public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollPeriods", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(period => period.Id);

    builder.Property(period => period.TenantId).IsRequired();
    builder.Property(period => period.CompanyId).IsRequired();

    // GL's fiscal period by identifier. No foreign key into GL's tables (`ADR-012`, and the architecture
    // guard asserts it in both directions).
    builder.Property(period => period.FiscalPeriodId).IsRequired();

    builder.Property(period => period.Name)
      .HasMaxLength(PayrollPersistenceConstants.PeriodNameMaximumLength)
      .IsRequired();

    builder.Property(period => period.StartUtc).IsRequired();
    builder.Property(period => period.EndUtc).IsRequired();
    builder.Property(period => period.PayDateUtc).IsRequired();

    builder.Property(period => period.RowVersion).IsRowVersion();
    builder.Property(period => period.CreatedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);
    builder.Property(period => period.ModifiedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);

    // ---- ONE PAYROLL PERIOD PER FISCAL PERIOD PER COMPANY, ENFORCED BY THE DATABASE.
    //
    // `OD-PAY-0002` ruled 1:1 alignment. A unique index is what makes that a fact rather than an intention:
    // two payroll periods claiming the same fiscal period would make "which one is closed" ambiguous again,
    // which is precisely what alignment was ruled to prevent.
    builder.HasIndex(period => new { period.TenantId, period.CompanyId, period.FiscalPeriodId })
      .IsUnique();
  }
}

public sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
  public void Configure(EntityTypeBuilder<PayrollRun> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollRuns", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(run => run.Id);

    builder.Property(run => run.TenantId).IsRequired();
    builder.Property(run => run.CompanyId).IsRequired();
    builder.Property(run => run.PayrollPeriodId).IsRequired();
    builder.Property(run => run.Status).HasConversion<int>().IsRequired();

    builder.Property(run => run.CalculatedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);
    builder.Property(run => run.ApprovedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);
    builder.Property(run => run.PostedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);

    // The journal this run produced. A plain identifier column with NO foreign key to GL's `GlJournalEntries`
    // — see the aggregate's comment: a database-level FK would couple the two modules' migrations and make
    // the boundary a fiction at the schema layer even while `ADR-012` held at the assembly layer.
    builder.Property(run => run.JournalEntryId);

    builder.Property(run => run.RowVersion).IsRowVersion();
    builder.Property(run => run.CreatedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);
    builder.Property(run => run.ModifiedBy).HasMaxLength(PayrollPersistenceConstants.ActorMaximumLength);

    // One run per company per period. `OD-PAY-0011` ruled correction by reverse-and-rerun rather than by
    // superseding runs, so two runs claiming one period is never legitimate — and had superseding been ruled
    // instead, this index could not exist. The lifecycle decision and the schema constraint are the same
    // decision seen twice.
    builder.HasIndex(run => new { run.TenantId, run.CompanyId, run.PayrollPeriodId }).IsUnique();

    builder.HasMany(run => run.DraftLines)
      .WithOne()
      .HasForeignKey(line => line.PayrollRunId)
      .OnDelete(DeleteBehavior.Cascade);

    // ---- CASCADE ON DRAFT LINES, RESTRICT ON APPROVED LINES.
    //
    // The asymmetry is the whole design in one place. Draft lines are deleted wholesale on every
    // recalculation, so a cascade is what makes that cheap. Approved lines are `IAppendOnlyEntity`: the write
    // boundary refuses to delete them, so a cascade would be a constraint the application layer can never
    // exercise — and declaring one would suggest deleting a run is a thing that can happen.
    builder.HasMany(run => run.Lines)
      .WithOne()
      .HasForeignKey(line => line.PayrollRunId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Metadata.FindNavigation(nameof(PayrollRun.DraftLines))!
      .SetPropertyAccessMode(PropertyAccessMode.Field);
    builder.Metadata.FindNavigation(nameof(PayrollRun.Lines))!
      .SetPropertyAccessMode(PropertyAccessMode.Field);
  }
}

public sealed class PayrollRunDraftLineConfiguration : IEntityTypeConfiguration<PayrollRunDraftLine>
{
  public void Configure(EntityTypeBuilder<PayrollRunDraftLine> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollRunDraftLines", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(line => line.Id);

    builder.Property(line => line.TenantId).IsRequired();
    builder.Property(line => line.PayrollRunId).IsRequired();
    builder.Property(line => line.EmployeeId).IsRequired();
    builder.Property(line => line.PayElementId).IsRequired();
    builder.Property(line => line.Kind).HasConversion<int>().IsRequired();

    builder.Property(line => line.Amount)
      .HasPrecision(PayrollPersistenceConstants.MoneyPrecision, PayrollPersistenceConstants.MoneyScale)
      .IsRequired();

    builder.Property(line => line.Sequence).IsRequired();
    builder.Property(line => line.GlAccountId);

    builder.HasIndex(line => new { line.PayrollRunId, line.EmployeeId, line.Sequence });
  }
}

public sealed class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
  public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("PayrollRunLines", PayrollPersistenceConstants.TenantSchema);
    builder.HasKey(line => line.Id);

    builder.Property(line => line.TenantId).IsRequired();
    builder.Property(line => line.PayrollRunId).IsRequired();
    builder.Property(line => line.EmployeeId).IsRequired();
    builder.Property(line => line.PayElementId).IsRequired();
    builder.Property(line => line.Kind).HasConversion<int>().IsRequired();

    builder.Property(line => line.Amount)
      .HasPrecision(PayrollPersistenceConstants.MoneyPrecision, PayrollPersistenceConstants.MoneyScale)
      .IsRequired();

    builder.Property(line => line.Sequence).IsRequired();

    // Captured at calculation and carried through approval, so a posting uses the account that was in force
    // when the numbers were produced rather than whatever the element points at today.
    builder.Property(line => line.GlAccountId);

    // THE PAYSLIP'S ACCESS PATH (`OD-PAY-0015`). A payslip reads THIS table only, never the draft table,
    // which is why a payslip exists precisely when an approved record exists.
    builder.HasIndex(line => new { line.PayrollRunId, line.EmployeeId, line.Sequence });
  }
}

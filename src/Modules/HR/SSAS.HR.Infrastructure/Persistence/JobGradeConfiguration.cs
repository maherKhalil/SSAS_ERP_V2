using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// Job grade persistence in the tenant ERP database (FP-008 data-model, REQ-HR-0201).
//
// ================================================================================================
// THE REFERENCE RUNS ONE WAY: JobGrades -> SalaryGrades, AND THERE IS NO COLUMN THE OTHER WAY.
// ================================================================================================
//
// A `SalaryGrades.JobGradeId` column would make the two tables mutually dependent, and
// `TenantCutoverCopyPlan.Order` — which places principals before dependents — would find no table ready and
// return `CutoverCopyOrderUndecidable`. That is the same failure `RISK-DEP-001` verified in source for
// Department's naive manager column, in a place nobody would think to look for it, because the two grades
// are peers in every other respect.
//
// ---- ASSERTED SINCE 2026-09-01, NOT ONLY REASONED (244).
//
// `CutoverCopyOrderCycleTests.A_foreign_key_cycle_makes_the_copy_order_undecidable`, in
// `tests/Platform.Tests/TenantStorage/`, constructs a model whose two tenant-owned tables reference
// each other and asserts the failure. Its matched control asserts that the SAME two tables, with one
// of the two foreign keys removed, produce a plan -- so the failure is attributable to the cycle
// rather than to the tables being undescribable.
//
// PRECISION, BECAUSE THIS COMMENT NAMES `Order`: the test calls the public
// `TenantCutoverCopyPlan.Build`. `Order` is private and the cycle check -- the point at which no
// table is ready -- is inside it, so `Build` is the only way to reach it.
//
// AND THE BOUND, WHICH IS THE HALF A CITATION USUALLY LOSES: WHAT IS TESTED IS THAT THE PLANNER
// REFUSES A CYCLE. That THIS table's shape would produce one is still read from the model rather
// than executed. The mechanism is proven; applying it to this table is an argument.
//
// No BranchId and no EmployeeId, for the reasons recorded on `PositionConfiguration`.
public sealed class JobGradeConfiguration : IEntityTypeConfiguration<JobGrade>
{
  public void Configure(EntityTypeBuilder<JobGrade> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("JobGrades", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint("CK_JobGrades_Status", "[Status] IN (N'Active', N'Inactive')");
      table.HasCheckConstraint("CK_JobGrades_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
      table.HasCheckConstraint("CK_JobGrades_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");

      // ---- RANK IS POSITIVE, AND THE DATABASE SAYS SO TOO (BRULE-POS-0007, ruled 2026-08-21).
      //
      // Phase 1 left this to the domain alone because the package's constraint list for this table did not
      // name it, and adding an unlisted constraint would have been filling a gap the specification did not
      // leave. The gap was reported and ruled: a direct SQL insert could write a rank of zero, and a rule
      // the database does not know is a rule that holds only while every writer goes through the domain.
      table.HasCheckConstraint("CK_JobGrades_RankOrder_Positive", "[RankOrder] > 0");
    });

    builder.HasKey(grade => grade.Id);
    builder.Property(grade => grade.Id).HasColumnName("JobGradeId").ValueGeneratedNever();
    builder.Ignore(grade => grade.JobGradeId);

    builder.Property(grade => grade.TenantId).IsRequired();
    builder.Property(grade => grade.CompanyId).IsRequired();

    builder.Property(grade => grade.Code)
      .HasConversion(code => code.Value, value => JobGradeCode.Create(value).Value)
      .HasMaxLength(JobGradeCode.MaximumLength)
      .IsRequired();
    builder.Property(grade => grade.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(JobGradeCode.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    builder.Property(grade => grade.Name)
      .HasConversion(name => name.Value, value => JobGradeName.Create(value).Value)
      .HasMaxLength(JobGradeName.MaximumLength)
      .IsRequired();

    // The search column. See `PositionConfiguration` for why a value-converted property cannot carry a
    // search predicate, and why this one carries no index.
    builder.Property(grade => grade.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(JobGradeName.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    // ---- THE LADDER'S ORDER IS DATA (DEC-POS-0006), AND IT IS `int` RATHER THAN A VALUE OBJECT.
    //
    // The package specifies an integer, and positivity is enforced in the aggregate. **No check constraint
    // is declared for it**: the package's constraint list for this table does not include one, and adding an
    // unlisted constraint would be filling a gap the specification did not leave. The consequence is stated
    // rather than hidden — a direct SQL insert can write a zero or negative rank, and only the application
    // path refuses it.
    builder.Property(grade => grade.RankOrder).IsRequired();

    // NULL MEANS UNPRICED — this grade has not been mapped to a pay band yet.
    builder.Property(grade => grade.SalaryGradeId);

    builder.Property(grade => grade.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();
    builder.Property(grade => grade.StatusChangedUtc).IsRequired();
    builder.Property(grade => grade.StatusChangedBy)
      .HasMaxLength(JobGrade.ActorMaximumLength)
      .IsRequired();

    builder.Property(grade => grade.CreatedUtc).IsRequired();
    builder.Property(grade => grade.CreatedBy).HasMaxLength(JobGrade.ActorMaximumLength);
    builder.Property(grade => grade.ModifiedUtc).IsRequired();
    builder.Property(grade => grade.ModifiedBy).HasMaxLength(JobGrade.ActorMaximumLength);

    builder.Property(grade => grade.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.HasIndex(grade => new
      {
        grade.TenantId, grade.CompanyId, grade.NormalizedCode
      })
      .IsUnique()
      .HasDatabaseName("UX_JobGrades_TenantId_CompanyId_NormalizedCode");

    // ---- RANK IS UNIQUE WITHIN THE LADDER (DEC-POS-0006).
    //
    // Two grades in one company cannot share a rank, because a ladder with two rung sevens has no order at
    // all — which is the property `RankOrder` exists to provide. The index is what makes that authoritative
    // under concurrent insert rather than advisory.
    builder.HasIndex(grade => new
      {
        grade.TenantId, grade.CompanyId, grade.RankOrder
      })
      .IsUnique()
      .HasDatabaseName("UX_JobGrades_TenantId_CompanyId_RankOrder");

    builder.HasIndex(grade => new
      {
        grade.TenantId, grade.CompanyId, grade.Status
      })
      .HasDatabaseName("IX_JobGrades_TenantId_CompanyId_Status");

    // The pay-band reference, restricted and one-directional. No navigation property, for the reason
    // recorded on `PositionConfiguration`.
    builder.HasOne<SalaryGrade>()
      .WithMany()
      .HasForeignKey(grade => grade.SalaryGradeId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}

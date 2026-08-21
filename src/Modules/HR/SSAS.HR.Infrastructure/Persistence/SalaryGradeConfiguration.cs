using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// Salary grade persistence — THE PRODUCT'S FIRST MONEY COLUMNS (FP-008 data-model, ADR-027).
//
// ================================================================================================
// THE BAND IS AN OPTIONAL OWNED TYPE, AND THAT MAPPING **IS** THE ATOMICITY RULE (DEC-POS-0027).
// ================================================================================================
//
// `SalaryGrade.Band` is one value that may be absent, not three independently nullable columns. EF
// materializes it as `null` when the columns are null and writes all three together when it is not, so the
// MODEL cannot express a partial band at all — there is no code path that sets a minimum without a maximum,
// because there is no property to set.
//
// `CK_SalaryGrades_Band_Atomic` states the same rule to SQL Server, for writes that bypass the application
// entirely. Three statements of one rule, each covering what the others cannot: the value object covers
// construction, the mapping covers persistence, the constraint covers direct SQL.
//
// ================================================================================================
// `decimal(19,4)`, AND THERE IS NO CURRENCY COLUMN (ADR-027 decisions 1 and 2, DEC-POS-0015).
// ================================================================================================
//
// Four decimal places because `BaseCurrencyCode`'s own ISO-4217 set contains three-decimal currencies — BHD,
// IQD, JOD, KWD, LYD, OMR, TND — and a product configured for one must not lose its minor unit; the fourth
// place is a guard digit. Fifteen integer digits so high-denomination currencies need no scaling convention
// that some modules apply and others do not. Never `float`, `real` or `money`.
//
// The currency is the owning Company's `BaseCurrencyCode`, which `DEC-CMP-0009` makes required at creation
// and IMMUTABLE — so every row under one company already has exactly one unambiguous currency, and a per-row
// copy would be a second source of truth for a fact the Company owns. `SSAS.HR.Domain` cannot reference
// Platform's currency value object under `ADR-012` in any case. `ADR-027` decision 3 names the conditions
// under which this stops being sufficient.
public sealed class SalaryGradeConfiguration : IEntityTypeConfiguration<SalaryGrade>
{
  public void Configure(EntityTypeBuilder<SalaryGrade> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("SalaryGrades", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint("CK_SalaryGrades_Status", "[Status] IN (N'Active', N'Inactive')");
      table.HasCheckConstraint("CK_SalaryGrades_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
      table.HasCheckConstraint("CK_SalaryGrades_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");

      // ---- ALL THREE, OR NONE (DEC-POS-0027).
      table.HasCheckConstraint(
        "CK_SalaryGrades_Band_Atomic",
        "([MinimumAmount] IS NULL AND [MidpointAmount] IS NULL AND [MaximumAmount] IS NULL) OR " +
        "([MinimumAmount] IS NOT NULL AND [MidpointAmount] IS NOT NULL AND [MaximumAmount] IS NOT NULL)");

      // Guarded on the minimum alone because the atomic constraint above already guarantees the other two
      // are present whenever it is. Repeating the null checks would suggest a state the schema forbids.
      table.HasCheckConstraint(
        "CK_SalaryGrades_Amounts_NonNegative",
        "[MinimumAmount] IS NULL OR " +
        "([MinimumAmount] >= 0 AND [MidpointAmount] >= 0 AND [MaximumAmount] >= 0)");

      // NON-STRICT. A band whose three amounts are equal is a fixed-rate grade, which is a real structure.
      table.HasCheckConstraint(
        "CK_SalaryGrades_Amounts_Ordered",
        "[MinimumAmount] IS NULL OR " +
        "([MinimumAmount] <= [MidpointAmount] AND [MidpointAmount] <= [MaximumAmount])");
    });

    builder.HasKey(grade => grade.Id);
    builder.Property(grade => grade.Id).HasColumnName("SalaryGradeId").ValueGeneratedNever();
    builder.Ignore(grade => grade.SalaryGradeId);

    builder.Property(grade => grade.TenantId).IsRequired();
    builder.Property(grade => grade.CompanyId).IsRequired();

    builder.Property(grade => grade.Code)
      .HasConversion(code => code.Value, value => SalaryGradeCode.Create(value).Value)
      .HasMaxLength(SalaryGradeCode.MaximumLength)
      .IsRequired();
    builder.Property(grade => grade.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(SalaryGradeCode.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    builder.Property(grade => grade.Name)
      .HasConversion(name => name.Value, value => SalaryGradeName.Create(value).Value)
      .HasMaxLength(SalaryGradeName.MaximumLength)
      .IsRequired();

    builder.Property(grade => grade.RankOrder).IsRequired();

    // ---- THE BAND. Explicit column names, so the three read as the fields the package names rather than
    // as EF's default `Band_MinimumAmount`.
    //
    // The properties inside are NON-nullable, which is what lets EF decide presence: all three columns null
    // means the owned value is absent. That is the mapping half of `DEC-POS-0027`.
    builder.OwnsOne(grade => grade.Band, band =>
    {
      band.Property(amounts => amounts.MinimumAmount)
        .HasColumnName("MinimumAmount")
        .HasColumnType("decimal(19,4)")
        .IsRequired();

      band.Property(amounts => amounts.MidpointAmount)
        .HasColumnName("MidpointAmount")
        .HasColumnType("decimal(19,4)")
        .IsRequired();

      band.Property(amounts => amounts.MaximumAmount)
        .HasColumnName("MaximumAmount")
        .HasColumnType("decimal(19,4)")
        .IsRequired();
    });

    // The navigation is OPTIONAL: an unpriced grade is a grade with no band, not a grade with a zero band.
    builder.Navigation(grade => grade.Band).IsRequired(false);

    builder.Property(grade => grade.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();
    builder.Property(grade => grade.StatusChangedUtc).IsRequired();
    builder.Property(grade => grade.StatusChangedBy)
      .HasMaxLength(SalaryGrade.ActorMaximumLength)
      .IsRequired();

    builder.Property(grade => grade.CreatedUtc).IsRequired();
    builder.Property(grade => grade.CreatedBy).HasMaxLength(SalaryGrade.ActorMaximumLength);
    builder.Property(grade => grade.ModifiedUtc).IsRequired();
    builder.Property(grade => grade.ModifiedBy).HasMaxLength(SalaryGrade.ActorMaximumLength);

    builder.Property(grade => grade.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.HasIndex(grade => new
      {
        grade.TenantId, grade.CompanyId, grade.NormalizedCode
      })
      .IsUnique()
      .HasDatabaseName("UX_SalaryGrades_TenantId_CompanyId_NormalizedCode");

    builder.HasIndex(grade => new
      {
        grade.TenantId, grade.CompanyId, grade.RankOrder
      })
      .IsUnique()
      .HasDatabaseName("UX_SalaryGrades_TenantId_CompanyId_RankOrder");

    builder.HasIndex(grade => new
      {
        grade.TenantId, grade.CompanyId, grade.Status
      })
      .HasDatabaseName("IX_SalaryGrades_TenantId_CompanyId_Status");

    // NO FOREIGN KEY TO JobGrades. The reference is declared on the other side and runs one way only; a
    // column here would close the loop `DEC-POS-0002` exists to prevent.
  }
}

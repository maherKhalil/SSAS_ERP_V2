using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// Employee persistence in the tenant ERP database (FP-006 data-model, ADR-017).
//
// OWNED BY HR, APPLIED TO PLATFORM'S CONTEXT. HR maps its own entity through ITenantModelContributor, so
// tenant business data keeps ONE context and ONE migration stream (ADR-017) without Platform referencing HR
// or HR referencing Platform (ADR-012).
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
  // The tenant schema and its binary collation, restated here rather than imported: HR cannot reference
  // Platform's TenantPersistenceConstants, and the values are part of the tenant contract either way.
  public const string TenantSchema = "tenant";

  public const string OrdinalCollation = "Latin1_General_100_BIN2";

  public void Configure(EntityTypeBuilder<Employee> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("Employees", TenantSchema, table =>
    {
      table.HasCheckConstraint(
        "CK_Employees_Status",
        "[Status] IN (N'Active', N'Inactive', N'Terminated')");
      table.HasCheckConstraint(
        "CK_Employees_StatusChangeReasonCode",
        "[StatusChangeReasonCode] IN (N'Created', N'Administrative', N'Operational', N'Compliance', N'Resignation', N'Dismissal', N'EndOfContract')");

      // BR-HR-0003, enforced by the database as well as the domain: a date that violates it is wrong
      // whichever path wrote it, including a raw-SQL one.
      table.HasCheckConstraint(
        "CK_Employees_TerminationNotBeforeEmployment",
        "[TerminationDate] IS NULL OR [TerminationDate] >= [EmploymentDate]");

      // Status and TerminationDate cannot disagree in either direction.
      table.HasCheckConstraint(
        "CK_Employees_TerminationDateMatchesStatus",
        "([Status] = N'Terminated' AND [TerminationDate] IS NOT NULL) OR ([Status] <> N'Terminated' AND [TerminationDate] IS NULL)");

      table.HasCheckConstraint(
        "CK_Employees_EmployeeNumber_NotBlank", "LEN(LTRIM(RTRIM([EmployeeNumber]))) > 0");
      table.HasCheckConstraint("CK_Employees_FullName_NotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
    });

    builder.HasKey(employee => employee.Id);
    builder.Property(employee => employee.Id).HasColumnName("EmployeeId").ValueGeneratedNever();
    builder.Ignore(employee => employee.EmployeeId);

    // ---- THE THREE OWNERSHIP DIMENSIONS.
    //
    // TenantId is retained in every placement, including a dedicated database holding one tenant (ADR-017):
    // it is what the global query filter and the tenant write guard key on.
    builder.Property(employee => employee.TenantId).IsRequired();
    builder.Property(employee => employee.CompanyId).IsRequired();
    builder.Property(employee => employee.BranchId).IsRequired();

    // ---- DEPARTMENT IS REQUIRED, AND IS NOT A FOURTH OWNERSHIP DIMENSION.
    //
    // NOT NULL from FP-007 Phase 3 onward with no nullable grace period: the migration's temporary nullable
    // state exists only inside the migration itself, and every employee leaves it with a department. The
    // column sits here beside the ownership trio because it is on the same table, not because it joins them
    // — nothing scopes a read by department.
    builder.Property(employee => employee.DepartmentId).IsRequired();

    builder.Property(employee => employee.EmployeeNumber)
      .HasConversion(number => number.Value, value => EmployeeNumber.Create(value).Value)
      .HasMaxLength(EmployeeNumber.MaximumLength)
      .IsRequired();
    builder.Property(employee => employee.NormalizedEmployeeNumber)
      .HasField("normalizedEmployeeNumber")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(EmployeeNumber.MaximumLength)
      .UseCollation(OrdinalCollation)
      .IsRequired();

    builder.Property(employee => employee.NationalId)
      .HasConversion(
        nationalId => nationalId!.Value,
        value => NationalId.Create(value).Value)
      .HasMaxLength(NationalId.MaximumLength);
    builder.Property(employee => employee.NormalizedNationalId)
      .HasField("normalizedNationalId")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(NationalId.MaximumLength)
      .UseCollation(OrdinalCollation);

    builder.Property(employee => employee.FullName)
      .HasConversion(name => name.Value, value => EmployeeFullName.Create(value).Value)
      .HasMaxLength(EmployeeFullName.MaximumLength)
      .IsRequired();

    builder.Property(employee => employee.EmploymentDate).IsRequired();
    builder.Property(employee => employee.TerminationDate);

    builder.Property(employee => employee.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(OrdinalCollation)
      .IsRequired();
    builder.Property(employee => employee.StatusChangeReasonCode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(OrdinalCollation)
      .IsRequired();
    builder.Property(employee => employee.StatusChangedUtc).IsRequired();
    builder.Property(employee => employee.StatusChangedBy)
      .HasMaxLength(Employee.ActorMaximumLength)
      .IsRequired();

    builder.Property(employee => employee.CreatedUtc).IsRequired();
    builder.Property(employee => employee.CreatedBy).HasMaxLength(Employee.ActorMaximumLength);
    builder.Property(employee => employee.ModifiedUtc).IsRequired();
    builder.Property(employee => employee.ModifiedBy).HasMaxLength(Employee.ActorMaximumLength);

    builder.Property(employee => employee.RowVersion).IsRowVersion().IsConcurrencyToken();

    // ---- EMPLOYEE NUMBER IS UNIQUE WITHIN A COMPANY, AND BranchId DOES NOT PARTICIPATE.
    //
    // BR-HR-0001 scopes the rule to the company, and ADR-023 states that Employee uniqueness which is
    // company-wide must not include BranchId. The consequence is intended: two employees in different
    // branches of one company cannot share a number. Binary collation makes the index authoritative under
    // concurrent creation rather than merely advisory.
    builder.HasIndex(employee => new
      {
        employee.TenantId, employee.CompanyId, employee.NormalizedEmployeeNumber
      })
      .IsUnique()
      .HasDatabaseName("UX_Employees_TenantId_CompanyId_NormalizedEmployeeNumber");

    // ---- NATIONAL ID IS UNIQUE WHERE PRESENT, filtered so many employees without one remain possible while
    // every recorded value stays distinct within the company (BR-HR-0002).
    builder.HasIndex(employee => new
      {
        employee.TenantId, employee.CompanyId, employee.NormalizedNationalId
      })
      .IsUnique()
      .HasFilter("[NormalizedNationalId] IS NOT NULL")
      .HasDatabaseName("UX_Employees_TenantId_CompanyId_NormalizedNationalId");

    // The scoped-search index. Its leading key order matches the MANDATORY predicate order — tenant, then
    // company, then branch — so no scoped read can be served by a scan that ignores a scope column.
    builder.HasIndex(employee => new
      {
        employee.TenantId, employee.CompanyId, employee.BranchId, employee.Status
      })
      .HasDatabaseName("IX_Employees_TenantId_CompanyId_BranchId_Status");

    // ---- THE DEPARTMENT LOOKUP INDEX (FP-007 Phase 3).
    //
    // Tenant, then company, then department — the leading keys again match the mandatory predicate order,
    // so a department-filtered search still cannot be served by a plan that skipped a scope column.
    //
    // BranchId is deliberately NOT in this index. A department-filtered search still applies branch scope,
    // but it does so through the existing branch index; adding BranchId here would suggest the department
    // filter and the branch scope are one composite question, and they are two independent ones.
    builder.HasIndex(employee => new
      {
        employee.TenantId, employee.CompanyId, employee.DepartmentId
      })
      .HasDatabaseName("IX_Employees_TenantId_CompanyId_DepartmentId");

    // ---- THE DEPARTMENT FOREIGN KEY, DECLARED WITH THE TYPED API.
    //
    // Unlike Company and Branch below, Department is HR's OWN type, so no principal-type-name string is
    // needed and this relationship is expressed normally.
    //
    // RESTRICT, matching every other relationship in this module: a department is deactivated, never
    // deleted, and a cascade here would erase employment records along with an org-structure change.
    //
    // NO NAVIGATION PROPERTY. Employee holds no reference to Department: an aggregate that could walk to
    // another aggregate root invites a read that bypasses the department's own scope, which is exactly the
    // leak the Phase 2 manager-privacy design exists to prevent.
    builder.HasOne<Domain.Departments.Department>()
      .WithMany()
      .HasForeignKey(employee => employee.DepartmentId)
      .OnDelete(DeleteBehavior.Restrict);

    // The foreign keys to Company and Branch are declared in HrTenantModelContributor, by PRINCIPAL TYPE
    // NAME: HR cannot reference Platform's Company or Branch types, so the typed relationship API is not
    // available here. See the contributor for why they are nonetheless real, intra-catalog constraints.
  }
}

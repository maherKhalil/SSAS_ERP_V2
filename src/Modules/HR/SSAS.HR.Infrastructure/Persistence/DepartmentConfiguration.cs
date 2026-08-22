using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Infrastructure.Persistence;

// Department persistence in the tenant ERP database (FP-007 data-model, ADR-026, ADR-017).
//
// OWNED BY HR, APPLIED TO PLATFORM'S CONTEXT, exactly as `EmployeeConfiguration` is: HR maps its own
// entities through `ITenantModelContributor`, so tenant business data keeps ONE context and ONE migration
// stream without either module referencing the other.
//
// ================================================================================================
// THERE IS NO BranchId COLUMN, AND THAT IS THE CLASSIFICATION (ADR-026 decisions 1 and 2).
// ================================================================================================
//
// Employee carries all three ownership columns; this table carries two. An architecture guard asserts the
// absence from the composed model so a future convention or shadow property cannot add one silently.
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
  public void Configure(EntityTypeBuilder<Department> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("Departments", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint("CK_Departments_Status", "[Status] IN (N'Active', N'Inactive')");

      // ---- THE ONE PART OF BR-HR-0008 A CONSTRAINT CAN EXPRESS.
      //
      // An adjacency list cannot state acyclicity to SQL Server, but it can state that a row is not its own
      // parent — so the shallowest cycle is refused even against direct SQL that bypasses the application
      // entirely. The general descendant-as-parent rule is transactional and arrives in Phase 2. That
      // asymmetry is stated rather than papered over: one branch of the rule has a database guarantee and
      // the rest will have a transactional one.
      table.HasCheckConstraint(
        "CK_Departments_ParentIsNotSelf",
        "[ParentDepartmentId] IS NULL OR [ParentDepartmentId] <> [DepartmentId]");

      table.HasCheckConstraint("CK_Departments_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
      table.HasCheckConstraint("CK_Departments_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
    });

    builder.HasKey(department => department.Id);
    builder.Property(department => department.Id).HasColumnName("DepartmentId").ValueGeneratedNever();
    builder.Ignore(department => department.DepartmentId);

    // ---- THE TWO OWNERSHIP DIMENSIONS. There is deliberately no third.
    builder.Property(department => department.TenantId).IsRequired();
    builder.Property(department => department.CompanyId).IsRequired();

    builder.Property(department => department.Code)
      .HasConversion(code => code.Value, value => DepartmentCode.Create(value).Value)
      .HasMaxLength(DepartmentCode.MaximumLength)
      .IsRequired();
    builder.Property(department => department.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(DepartmentCode.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    builder.Property(department => department.Name)
      .HasConversion(name => name.Value, value => DepartmentName.Create(value).Value)
      .HasMaxLength(DepartmentName.MaximumLength)
      .IsRequired();

    // ---- THE SEARCH COLUMN, ADDED IN FP-008 PHASE 2 TO FIX A BREAK THAT SHIPPED IN FP-007.
    //
    // `Name` above is value-converted, and `DepartmentReadService.SearchAsync` filtered on `Name.Value
    // .Contains(text)` — which EF Core cannot translate inside a predicate. Every department search carrying
    // a `searchText` threw rather than returning rows, and no test covered it. See `DEC-POS-0030`.
    //
    // No index, for the same reason as the position columns: a leading-wildcard LIKE cannot seek.
    builder.Property(department => department.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(DepartmentName.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    // NULL MEANS ROOT, and a company may have more than one.
    builder.Property(department => department.ParentDepartmentId);

    builder.Property(department => department.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();
    builder.Property(department => department.StatusChangedUtc).IsRequired();
    builder.Property(department => department.StatusChangedBy)
      .HasMaxLength(Department.ActorMaximumLength)
      .IsRequired();

    builder.Property(department => department.CreatedUtc).IsRequired();
    builder.Property(department => department.CreatedBy).HasMaxLength(Department.ActorMaximumLength);
    builder.Property(department => department.ModifiedUtc).IsRequired();
    builder.Property(department => department.ModifiedBy).HasMaxLength(Department.ActorMaximumLength);

    builder.Property(department => department.RowVersion).IsRowVersion().IsConcurrencyToken();

    // ---- CODE IS UNIQUE WITHIN A COMPANY, AND NOTHING ELSE PARTICIPATES.
    //
    // Binary collation makes the index authoritative under concurrent creation rather than merely
    // advisory, which is why codes that normalize alike collide. Two companies in one tenant may each have
    // a `SALES`; two departments in one company may not.
    builder.HasIndex(department => new
      {
        department.TenantId, department.CompanyId, department.NormalizedCode
      })
      .IsUnique()
      .HasDatabaseName("UX_Departments_TenantId_CompanyId_NormalizedCode");

    // The hierarchy traversal index. Leading keys are the mandatory scope columns, so no scoped read of the
    // tree can be served by a scan that ignores one.
    builder.HasIndex(department => new
      {
        department.TenantId, department.CompanyId, department.ParentDepartmentId
      })
      .HasDatabaseName("IX_Departments_TenantId_CompanyId_ParentDepartmentId");

    // ---- THE SELF-REFERENCING PARENT, RESTRICTED.
    //
    // RESTRICT is not a preference here: SQL Server rejects a cascading self-reference outright, so this is
    // the only legal delete behaviour. It is also the correct one — departments are deactivated, never
    // deleted, and a cascade would silently erase organizational structure.
    //
    // No navigation property is declared. The hierarchy is walked deliberately, by a repository that says
    // it is walking a hierarchy, rather than by a lazy graph a caller can wander into unaware of its depth.
    builder.HasOne<Department>()
      .WithMany()
      .HasForeignKey(department => department.ParentDepartmentId)
      .OnDelete(DeleteBehavior.Restrict);

    // The foreign key to Company is declared in HrTenantModelContributor, by PRINCIPAL TYPE NAME: HR cannot
    // reference Platform's Company type, so the typed relationship API is not available here.
  }
}

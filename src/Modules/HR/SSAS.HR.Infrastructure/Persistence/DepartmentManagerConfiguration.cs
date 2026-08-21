using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// The current head of a department (FP-007 data-model, ADR-026 decision 7).
//
// ================================================================================================
// A SEPARATE TABLE, BECAUSE A COLUMN ON Departments WOULD MAKE CUTOVER UNDECIDABLE.
// ================================================================================================
//
// `Department.ManagerEmployeeId` plus `Employee.DepartmentId` is a cycle in the table-level foreign-key
// graph. `TenantCutoverCopyPlan.Order` orders principals before dependents and returns
// `CutoverCopyOrderUndecidable` when no table is ready; with a mutual reference neither ever is. Both of
// this table's foreign keys point OUTWARD, so it is a dependent of Department and of Employee and a
// principal of neither, and the graph stays acyclic with full referential integrity.
//
// ---- THE PRIMARY KEY IS THE DEPARTMENT.
//
// "At most one manager per department" is therefore a fact of the schema, not something a handler
// remembers. A surrogate key with a unique index would enforce the same rule one step further from the
// reader; keying on the identity the invariant is actually about states it directly.
public sealed class DepartmentManagerConfiguration : IEntityTypeConfiguration<DepartmentManager>
{
  public void Configure(EntityTypeBuilder<DepartmentManager> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("DepartmentManagers", EmployeeConfiguration.TenantSchema);

    builder.HasKey(manager => manager.Id);
    builder.Property(manager => manager.Id).HasColumnName("DepartmentId").ValueGeneratedNever();
    builder.Ignore(manager => manager.DepartmentId);

    builder.Property(manager => manager.TenantId).IsRequired();
    builder.Property(manager => manager.CompanyId).IsRequired();
    builder.Property(manager => manager.EmployeeId).IsRequired();

    builder.Property(manager => manager.AssignedUtc).IsRequired();
    builder.Property(manager => manager.AssignedBy)
      .HasMaxLength(DepartmentManager.ActorMaximumLength)
      .IsRequired();

    builder.Property(manager => manager.CreatedUtc).IsRequired();
    builder.Property(manager => manager.CreatedBy).HasMaxLength(DepartmentManager.ActorMaximumLength);
    builder.Property(manager => manager.ModifiedUtc).IsRequired();
    builder.Property(manager => manager.ModifiedBy).HasMaxLength(DepartmentManager.ActorMaximumLength);

    builder.Property(manager => manager.RowVersion).IsRowVersion().IsConcurrencyToken();

    // "Which departments does this employee head" — needed to refuse a manager who belongs to the
    // department, and to report departments whose manager has been terminated. Both are Phase 2 questions,
    // but the index belongs with the table rather than being retrofitted alongside its first query.
    builder.HasIndex(manager => new
      {
        manager.TenantId, manager.CompanyId, manager.EmployeeId
      })
      .HasDatabaseName("IX_DepartmentManagers_TenantId_CompanyId_EmployeeId");

    // ---- THE DEPARTMENT SIDE. Restricted: a department is deactivated, never deleted.
    //
    // The principal key is stated explicitly because the dependent's primary key IS the foreign key, and a
    // one-to-one shaped by shared identity is worth declaring rather than inferring.
    builder.HasOne<Department>()
      .WithOne()
      .HasForeignKey<DepartmentManager>(manager => manager.Id)
      .OnDelete(DeleteBehavior.Restrict);

    // ---- THE EMPLOYEE SIDE. Also restricted, and for a stronger reason: employees are never physically
    // deleted at all, so a cascade would describe an event that cannot occur.
    //
    // Declared with the typed API because Employee is HR's own type. Only the Company principal has to go
    // through the contributor's string-named form, and that is because HR cannot reference Platform.
    builder.HasOne<Employee>()
      .WithMany()
      .HasForeignKey(manager => manager.EmployeeId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}

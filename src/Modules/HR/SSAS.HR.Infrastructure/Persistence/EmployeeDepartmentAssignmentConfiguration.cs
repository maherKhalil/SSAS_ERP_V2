using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// The append-only Employee department history (FP-007 data-model, ADR-026).
//
// It is the department counterpart of `EmployeeBranchAssignmentConfiguration` and follows it deliberately.
//
// ================================================================================================
// THIS TABLE IS NOT BRANCH-OWNED, AND HAS NO BranchId COLUMN.
// ================================================================================================
//
// A department change says nothing about where the employee works. Stamping a branch here would attach an
// unrelated dimension and put an org-structure change inside the branch write boundary.
//
// THERE IS NO RowVersion, NO ModifiedUtc/ModifiedBy AND NO EffectiveToUtc. A record that is never updated
// has no concurrency state to protect and no modification metadata to record; closing an interval would
// mean UPDATING the previous row, which is the mutation this model exists to prevent.
public sealed class EmployeeDepartmentAssignmentConfiguration
  : IEntityTypeConfiguration<EmployeeDepartmentAssignment>
{
  public void Configure(EntityTypeBuilder<EmployeeDepartmentAssignment> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("EmployeeDepartmentAssignments", EmployeeConfiguration.TenantSchema, table =>
    {
      // A record can never describe a move to the department it came from.
      table.HasCheckConstraint(
        "CK_EmployeeDepartmentAssignments_SourceDiffersFromDestination",
        "[SourceDepartmentId] IS NULL OR [SourceDepartmentId] <> [DestinationDepartmentId]");
    });

    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id)
      .HasColumnName("EmployeeDepartmentAssignmentId")
      .ValueGeneratedNever();

    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.CompanyId).IsRequired();
    builder.Property(assignment => assignment.EmployeeId).IsRequired();

    // NULL ONLY ON THE INITIAL RECORD — the employee's first department. Nothing else distinguishes it,
    // which is why the check constraint above guards the one confusion that is otherwise possible.
    builder.Property(assignment => assignment.SourceDepartmentId);
    builder.Property(assignment => assignment.DestinationDepartmentId).IsRequired();

    builder.Property(assignment => assignment.EffectiveFromUtc).IsRequired();
    builder.Property(assignment => assignment.ChangedBy)
      .HasMaxLength(EmployeeDepartmentAssignment.ActorMaximumLength)
      .IsRequired();

    // Bounded audit metadata. Neither is used in a decision, compared, or emitted in a domain event.
    builder.Property(assignment => assignment.ReasonCode)
      .HasMaxLength(EmployeeDepartmentAssignment.ReasonCodeMaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation);
    builder.Property(assignment => assignment.ReasonText)
      .HasMaxLength(EmployeeDepartmentAssignment.ReasonTextMaximumLength);

    builder.Property(assignment => assignment.CreatedUtc).IsRequired();
    builder.Property(assignment => assignment.CreatedBy)
      .HasMaxLength(EmployeeDepartmentAssignment.ActorMaximumLength);

    // The Modified pair from IAuditableEntity is explicitly NOT persisted: this record is never modified,
    // so a column for it would be permanently equal to its created counterpart and imply otherwise.
    builder.Ignore("ModifiedUtc");
    builder.Ignore("ModifiedBy");

    // ---- EMPLOYEE HISTORY LOOKUP AND POINT-IN-TIME ATTRIBUTION.
    //
    // One index serves both: ordered history for an employee, and "the record with the greatest
    // EffectiveFromUtc less than or equal to T" for the same employee. Id is the deterministic tie-break,
    // matching the branch-history index exactly.
    builder.HasIndex(assignment => new
      {
        assignment.TenantId,
        assignment.CompanyId,
        assignment.EmployeeId,
        assignment.EffectiveFromUtc,
        assignment.Id
      })
      .HasDatabaseName(
        "IX_EmployeeDepartmentAssignments_TenantId_CompanyId_EmployeeId_EffectiveFromUtc_Id");

    // ---- FOREIGN KEYS, ALL RESTRICTED.
    //
    // Unlike the branch history — which deliberately carries NO foreign key on either branch column so a
    // deactivated branch cannot orphan history — both department columns ARE constrained here. The
    // difference is intentional: branch identifiers were left opaque so no modelling path could reinterpret
    // one as the record's ownership branch, a risk that does not exist for a department. Departments are
    // never deleted either, so RESTRICT costs nothing and buys real integrity.
    // FP-007 Phase 3 gave Employee the collection navigation, so the relationship names it. That is what
    // lets the aggregate append a row and EF persist it in the employee's own unit of work — the same
    // arrangement the branch history has, and the reason a department change cannot commit without its
    // record.
    builder.HasOne<Employee>()
      .WithMany(employee => employee.DepartmentAssignments)
      .HasForeignKey(assignment => assignment.EmployeeId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Department>()
      .WithMany()
      .HasForeignKey(assignment => assignment.SourceDepartmentId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Department>()
      .WithMany()
      .HasForeignKey(assignment => assignment.DestinationDepartmentId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}

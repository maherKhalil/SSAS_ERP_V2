using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// The append-only Employee position history (FP-008 data-model, DEC-POS-0008).
//
// It is the position counterpart of `EmployeeDepartmentAssignmentConfiguration` and follows it deliberately
// rather than inventing a third history shape.
//
// ================================================================================================
// THIS TABLE IS NOT BRANCH-OWNED, AND HAS NO BranchId COLUMN.
// ================================================================================================
//
// A position change says nothing about where the employee works. Stamping a branch here would attach an
// unrelated dimension and put an org-structure change inside the branch write boundary.
//
// THERE IS NO RowVersion, NO ModifiedUtc/ModifiedBy AND NO EffectiveToUtc. A record that is never updated
// has no concurrency state to protect and no modification metadata to record; closing an interval would mean
// UPDATING the previous row, which is the mutation this model exists to prevent.
//
// ---- IT IS THE ONE TABLE IN THIS PACKAGE THAT MAY HOLD AN EmployeeId.
//
// `DEC-POS-0002` forbids a Position→Employee reference because it would close a cycle in the foreign-key
// graph. This table closes nothing: it is a DEPENDENT of both `Employees` and `Positions` and a principal of
// neither, so the graph stays acyclic and `TenantCutoverCopyPlan.Order` still finds a topological answer.
// That is the same shape `tenant.DepartmentManagers` took for the same reason (`ADR-026` decision 7).
public sealed class EmployeePositionAssignmentConfiguration
  : IEntityTypeConfiguration<EmployeePositionAssignment>
{
  public void Configure(EntityTypeBuilder<EmployeePositionAssignment> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("EmployeePositionAssignments", EmployeeConfiguration.TenantSchema, table =>
    {
      // A record can never describe a move to the position it came from.
      table.HasCheckConstraint(
        "CK_EmployeePositionAssignments_SourceDiffersFromDestination",
        "[SourcePositionId] IS NULL OR [SourcePositionId] <> [DestinationPositionId]");
    });

    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id)
      .HasColumnName("EmployeePositionAssignmentId")
      .ValueGeneratedNever();

    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.CompanyId).IsRequired();
    builder.Property(assignment => assignment.EmployeeId).IsRequired();

    // NULL ONLY ON THE INITIAL RECORD — the employee's first position. Nothing else distinguishes it, which
    // is why the check constraint above guards the one confusion that is otherwise possible.
    builder.Property(assignment => assignment.SourcePositionId);
    builder.Property(assignment => assignment.DestinationPositionId).IsRequired();

    builder.Property(assignment => assignment.EffectiveFromUtc).IsRequired();
    builder.Property(assignment => assignment.ChangedBy)
      .HasMaxLength(EmployeePositionAssignment.ActorMaximumLength)
      .IsRequired();

    // Bounded audit metadata. Neither is used in a decision, compared, or emitted in a domain event.
    builder.Property(assignment => assignment.ReasonCode)
      .HasMaxLength(EmployeePositionAssignment.ReasonCodeMaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation);
    builder.Property(assignment => assignment.ReasonText)
      .HasMaxLength(EmployeePositionAssignment.ReasonTextMaximumLength);

    builder.Property(assignment => assignment.CreatedUtc).IsRequired();
    builder.Property(assignment => assignment.CreatedBy)
      .HasMaxLength(EmployeePositionAssignment.ActorMaximumLength);

    // The Modified pair from IAuditableEntity is explicitly NOT persisted: this record is never modified, so
    // a column for it would be permanently equal to its created counterpart and imply otherwise.
    builder.Ignore("ModifiedUtc");
    builder.Ignore("ModifiedBy");

    // ---- EMPLOYEE HISTORY LOOKUP AND POINT-IN-TIME ATTRIBUTION.
    //
    // One index serves both: ordered history for an employee, and "the record with the greatest
    // EffectiveFromUtc less than or equal to T" for the same employee. Id is the deterministic tie-break,
    // matching the branch and department history indexes exactly.
    builder.HasIndex(assignment => new
      {
        assignment.TenantId,
        assignment.CompanyId,
        assignment.EmployeeId,
        assignment.EffectiveFromUtc,
        assignment.Id
      })
      .HasDatabaseName(
        "IX_EmployeePositionAssignments_TenantId_CompanyId_EmployeeId_EffectiveFromUtc_Id");

    // ---- FOREIGN KEYS, ALL RESTRICTED.
    //
    // Positions are deactivated, never deleted, so RESTRICT costs nothing and buys real integrity.
    //
    // FP-008 Phase 3 gave `Employee` the collection navigation, so the relationship now names it — closing
    // the forward obligation Phase 1 recorded here when the constraint existed and the navigation did not.
    // That is what lets the aggregate append a row and EF persist it in the employee's own unit of work,
    // the same arrangement the branch and department histories have, and the reason a position change
    // cannot commit without its record (`BRULE-POS-0018`).
    builder.HasOne<Employee>()
      .WithMany(employee => employee.PositionAssignments)
      .HasForeignKey(assignment => assignment.EmployeeId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Position>()
      .WithMany()
      .HasForeignKey(assignment => assignment.SourcePositionId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Position>()
      .WithMany()
      .HasForeignKey(assignment => assignment.DestinationPositionId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}

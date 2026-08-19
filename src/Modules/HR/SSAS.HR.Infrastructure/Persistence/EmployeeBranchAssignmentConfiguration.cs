using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Infrastructure.Persistence;

// The append-only Employee branch history (FP-006 data-model, ADR-024 decisions 4 and 5).
//
// ================================================================================================
// THIS TABLE IS NOT BRANCH-OWNED, AND THE MAPPING IS PART OF HOW THAT STAYS TRUE.
// ================================================================================================
//
// Neither branch column is named `BranchId`, and neither is mapped to IBranchOwnedEntity.BranchId. That is
// deliberate defence against a future convention, shadow property or interface implementation silently
// reclassifying the table as branch-owned — which would put an append-only cross-branch record inside the
// branch write boundary and make transfer unrepresentable (TS-EMP-0113).
//
// THERE IS NO RowVersion, NO ModifiedUtc/ModifiedBy AND NO EffectiveToUtc. A record that is never updated
// has no concurrency state to protect and no modification metadata to record; closing an interval would
// mean UPDATING the previous row, which is the mutation this model exists to prevent.
public sealed class EmployeeBranchAssignmentConfiguration : IEntityTypeConfiguration<EmployeeBranchAssignment>
{
  public void Configure(EntityTypeBuilder<EmployeeBranchAssignment> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("EmployeeBranchAssignments", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint(
        "CK_EmployeeBranchAssignments_ReasonCode",
        "[ReasonCode] IN (N'InitialAssignment', N'Reorganisation', N'OperationalNeed', N'EmployeeRequest', N'BranchClosure', N'Correction')");

      // A record can never describe a move to the branch it came from.
      table.HasCheckConstraint(
        "CK_EmployeeBranchAssignments_SourceDiffersFromDestination",
        "[SourceBranchId] IS NULL OR [SourceBranchId] <> [DestinationBranchId]");

      // THE INITIAL RECORD AND A TRANSFER RECORD CANNOT BE CONFUSED. `InitialAssignment` occurs if and only
      // if there is no source branch — enforced in both directions so neither half can drift.
      table.HasCheckConstraint(
        "CK_EmployeeBranchAssignments_InitialAssignmentHasNoSource",
        "([SourceBranchId] IS NULL AND [ReasonCode] = N'InitialAssignment') OR ([SourceBranchId] IS NOT NULL AND [ReasonCode] <> N'InitialAssignment')");
    });

    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id)
      .HasColumnName("EmployeeBranchAssignmentId")
      .ValueGeneratedNever();

    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.CompanyId).IsRequired();
    builder.Property(assignment => assignment.EmployeeId).IsRequired();

    // NULL ONLY ON THE INITIAL RECORD, paired with the reason code by the check constraint above.
    builder.Property(assignment => assignment.SourceBranchId);
    builder.Property(assignment => assignment.DestinationBranchId).IsRequired();

    builder.Property(assignment => assignment.EffectiveFromUtc).IsRequired();
    builder.Property(assignment => assignment.TransferredBy)
      .HasMaxLength(EmployeeBranchAssignment.ActorMaximumLength)
      .IsRequired();

    builder.Property(assignment => assignment.ReasonCode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    // Audit-only free text. Never used in a decision, compared, indexed, or emitted in an event.
    builder.Property(assignment => assignment.ReasonText)
      .HasMaxLength(EmployeeBranchAssignment.ReasonTextMaximumLength);

    builder.Property(assignment => assignment.CreatedUtc).IsRequired();
    builder.Property(assignment => assignment.CreatedBy)
      .HasMaxLength(EmployeeBranchAssignment.ActorMaximumLength);

    // The Modified pair from IAuditableEntity is explicitly NOT persisted: this record is never modified,
    // so a column for it would be permanently equal to its created counterpart and imply otherwise.
    builder.Ignore("ModifiedUtc");
    builder.Ignore("ModifiedBy");

    // ---- EMPLOYEE HISTORY LOOKUP AND POINT-IN-TIME ATTRIBUTION.
    //
    // One index serves both: ordered history for an employee, and "the record with the greatest
    // EffectiveFromUtc less than or equal to T" for the same employee. Id is the deterministic tie-break.
    builder.HasIndex(assignment => new
      {
        assignment.TenantId, assignment.EmployeeId, assignment.EffectiveFromUtc, assignment.Id
      })
      .HasDatabaseName("IX_EmployeeBranchAssignments_TenantId_EmployeeId_EffectiveFromUtc_Id");

    // Company-scoped historical branch reporting: "who was in this branch during that period".
    builder.HasIndex(assignment => new
      {
        assignment.TenantId, assignment.CompanyId, assignment.DestinationBranchId, assignment.EffectiveFromUtc
      })
      .HasDatabaseName("IX_EmployeeBranchAssignments_TenantId_CompanyId_DestinationBranchId_EffectiveFromUtc");

    // ---- FK TO THE EMPLOYEE, RESTRICTED. Same catalog, and the one relationship this record genuinely has.
    //
    // There is deliberately NO foreign key on either branch column: both are retained as opaque identifiers
    // so history survives unchanged when a branch is deactivated, and so no modelling path can reinterpret
    // either as this record's ownership branch. Branch rows are never deleted (ADR-023), so referential
    // integrity is not at risk from their absence.
    builder.HasOne<Employee>()
      .WithMany(employee => employee.BranchAssignments)
      .HasForeignKey(assignment => assignment.EmployeeId)
      .HasPrincipalKey(employee => employee.Id)
      .OnDelete(DeleteBehavior.Restrict);
  }
}

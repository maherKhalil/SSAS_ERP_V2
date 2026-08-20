using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Tests.Departments;

// THE TWO RECORDS THAT HANG OFF A DEPARTMENT (FP-007 Phase 1, ADR-026 decision 7).
//
// `DepartmentManager` is CURRENT STATE keyed by the department; `EmployeeDepartmentAssignment` is APPEND-ONLY
// HISTORY. They are tested together because what distinguishes them is the point.
public sealed class DepartmentAssociationDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

  private const string Actor = "tester";

  // ================================================================================================
  // DepartmentManager
  // ================================================================================================

  // ---- THE IDENTITY IS THE DEPARTMENT, which is what makes "one manager per department"
  // unrepresentable rather than merely refused.
  [Fact]
  public void A_manager_assignment_is_identified_by_its_department()
  {
    var departmentId = Guid.NewGuid();
    var employeeId = Guid.NewGuid();

    var manager = DepartmentManager.Assign(
      departmentId, Guid.NewGuid(), Guid.NewGuid(), employeeId, Actor, Now);

    Assert.True(manager.IsSuccess);
    Assert.Equal(departmentId, manager.Value.DepartmentId);
    Assert.Equal(departmentId, manager.Value.Id);
    Assert.Equal(employeeId, manager.Value.EmployeeId);
    Assert.Equal(Now, manager.Value.AssignedUtc);
    Assert.Equal(Actor, manager.Value.AssignedBy);
  }

  [Fact]
  public void A_manager_assignment_requires_a_department_and_an_employee()
  {
    var withoutDepartment = DepartmentManager.Assign(
      Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Actor, Now);
    var withoutEmployee = DepartmentManager.Assign(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Actor, Now);

    Assert.True(withoutDepartment.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidManagerAssignment, withoutDepartment.Error);
    Assert.True(withoutEmployee.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidManagerAssignment, withoutEmployee.Error);
  }

  [Fact]
  public void A_manager_assignment_requires_a_trusted_actor()
  {
    var result = DepartmentManager.Assign(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "   ", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidActor, result.Error);
  }

  // ================================================================================================
  // EmployeeDepartmentAssignment
  // ================================================================================================

  // ---- THE INITIAL RECORD IS THE ONE WITH NO SOURCE. Nothing else distinguishes it.
  [Fact]
  public void The_initial_assignment_has_no_source_department()
  {
    var destinationId = Guid.NewGuid();

    var assignment = EmployeeDepartmentAssignment.CreateInitial(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), destinationId, Now, Actor);

    Assert.True(assignment.IsSuccess);
    Assert.Null(assignment.Value.SourceDepartmentId);
    Assert.Equal(destinationId, assignment.Value.DestinationDepartmentId);
    Assert.Null(assignment.Value.ReasonCode);
    Assert.Null(assignment.Value.ReasonText);
    Assert.Equal(Actor, assignment.Value.ChangedBy);
  }

  [Fact]
  public void The_initial_assignment_requires_a_destination()
  {
    var result = EmployeeDepartmentAssignment.CreateInitial(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now, Actor);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidDepartmentAssignment, result.Error);
  }

  [Fact]
  public void A_change_records_both_departments()
  {
    var sourceId = Guid.NewGuid();
    var destinationId = Guid.NewGuid();

    var assignment = EmployeeDepartmentAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sourceId, destinationId, Now, Actor,
      "Reorg", "Merged into the northern division");

    Assert.True(assignment.IsSuccess);
    Assert.Equal(sourceId, assignment.Value.SourceDepartmentId);
    Assert.Equal(destinationId, assignment.Value.DestinationDepartmentId);
    Assert.Equal("Reorg", assignment.Value.ReasonCode);
    Assert.Equal("Merged into the northern division", assignment.Value.ReasonText);
  }

  // A move to the department the employee is already in is not a move.
  [Fact]
  public void A_change_to_the_same_department_is_refused()
  {
    var departmentId = Guid.NewGuid();

    var result = EmployeeDepartmentAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), departmentId, departmentId, Now, Actor, null, null);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidDepartmentAssignment, result.Error);
  }

  [Theory]
  [InlineData(EmployeeDepartmentAssignment.ReasonCodeMaximumLength + 1, 0)]
  [InlineData(0, EmployeeDepartmentAssignment.ReasonTextMaximumLength + 1)]
  public void Overlength_audit_metadata_is_refused(int codeLength, int textLength)
  {
    var result = EmployeeDepartmentAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Actor,
      codeLength == 0 ? null : new string('A', codeLength),
      textLength == 0 ? null : new string('A', textLength));

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidDepartmentAssignment, result.Error);
  }

  // ---- APPEND-ONLY IS A PROPERTY OF THE TYPE, NOT A CONVENTION.
  //
  // The same assertion FP-006 makes about the branch history: if a public setter ever appears, history
  // becomes editable and the whole model quietly stops being a record of what happened.
  [Fact]
  public void The_department_history_exposes_no_public_setter()
  {
    var settable = typeof(EmployeeDepartmentAssignment)
      .GetProperties()
      .Where(property => property.SetMethod is { IsPublic: true })
      .Select(property => property.Name)
      .Where(name => name is not ("TenantId" or "CompanyId"))
      .ToArray();

    // TenantId and CompanyId are excluded above because the ownership interfaces require settable
    // properties for the persistence boundary to stamp through. Every other property must be immutable.
    Assert.Empty(settable);
  }

  [Fact]
  public void The_department_history_carries_no_row_version_and_no_modified_stamp()
  {
    var properties = typeof(EmployeeDepartmentAssignment)
      .GetProperties()
      .Select(property => property.Name)
      .ToArray();

    // No concurrency state, because the record is never updated; concurrent changes serialize on
    // Employee.RowVersion instead. No Modified pair, because it is never modified.
    Assert.DoesNotContain("RowVersion", properties);
    Assert.DoesNotContain("ModifiedUtc", properties);
    Assert.DoesNotContain("ModifiedBy", properties);

    // And no EffectiveToUtc: closing an interval would mean UPDATING the previous row.
    Assert.DoesNotContain("EffectiveToUtc", properties);
  }
}

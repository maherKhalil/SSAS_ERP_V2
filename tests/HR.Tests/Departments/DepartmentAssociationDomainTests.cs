using SSAS.HR.Domain.Employees;
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

  // ================================================================================================
  // REACHED THROUGH THE AGGREGATE, BECAUSE THAT IS NOW THE ONLY WAY TO REACH IT.
  // ================================================================================================
  //
  // Phase 1 called these factories directly; they were `public` only because Employee did not yet have the
  // operation that would call them, and the file said Phase 3 would tighten them. Phase 3 did.
  //
  // Driving them through `Employee` is not a workaround for that change — it is a strictly better test.
  // These guards now run on the SAME path production uses, so a rule that stopped being reachable from
  // Employee would fail here instead of passing against a factory nothing calls.

  // ---- THE INITIAL RECORD IS THE ONE WITH NO SOURCE. Nothing else distinguishes it.
  [Fact]
  public void The_initial_assignment_has_no_source_department()
  {
    var employee = Stamped(out var departmentId);

    var assignment = Assert.Single(employee.DepartmentAssignments);

    Assert.Null(assignment.SourceDepartmentId);
    Assert.Equal(departmentId, assignment.DestinationDepartmentId);
    Assert.Equal(departmentId, employee.DepartmentId);
    Assert.Null(assignment.ReasonCode);
    Assert.Null(assignment.ReasonText);
    Assert.Equal(Actor, assignment.ChangedBy);
  }

  [Fact]
  public void The_initial_assignment_requires_a_destination()
  {
    var employee = NewEmployee();

    var result = employee.StampInitialAssignment(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Actor,
      Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentRequired.Code, result.Error.Code);

    // AND NOTHING WAS APPENDED. The refusal has to leave the aggregate untouched, not merely report a
    // failure — a half-stamped employee with a branch record and no department would be worse than none.
    Assert.Empty(employee.DepartmentAssignments);
    Assert.Empty(employee.BranchAssignments);
    Assert.Equal(Guid.Empty, employee.DepartmentId);
  }

  [Fact]
  public void A_change_records_both_departments()
  {
    var employee = Stamped(out var sourceId);
    var destinationId = Guid.NewGuid();

    var changed = employee.ChangeDepartment(
      destinationId, "Reorg", "Merged into the northern division", Actor, Guid.NewGuid(), Now);

    Assert.True(changed.IsSuccess);
    Assert.Equal(sourceId, changed.Value.SourceDepartmentId);
    Assert.Equal(destinationId, changed.Value.DestinationDepartmentId);
    Assert.Equal("Reorg", changed.Value.ReasonCode);
    Assert.Equal("Merged into the northern division", changed.Value.ReasonText);
  }

  // A move to the department the employee is already in is not a move.
  [Fact]
  public void A_change_to_the_same_department_is_refused()
  {
    var employee = Stamped(out var departmentId);

    var result = employee.ChangeDepartment(departmentId, null, null, Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentUnchanged.Code, result.Error.Code);

    // Still exactly the initial record: a non-change appends no history.
    Assert.Single(employee.DepartmentAssignments);
  }

  [Theory]
  [InlineData(EmployeeDepartmentAssignment.ReasonCodeMaximumLength + 1, 0)]
  [InlineData(0, EmployeeDepartmentAssignment.ReasonTextMaximumLength + 1)]
  public void Overlength_audit_metadata_is_refused(int codeLength, int textLength)
  {
    var employee = Stamped(out _);

    var result = employee.ChangeDepartment(
      Guid.NewGuid(),
      codeLength == 0 ? null : new string('A', codeLength),
      textLength == 0 ? null : new string('A', textLength),
      Actor,
      Guid.NewGuid(),
      Now);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidDepartmentAssignment, result.Error);

    // The department did not move either. A rejected record must not leave the column ahead of the history.
    Assert.Single(employee.DepartmentAssignments);
  }

  private static Employee NewEmployee() =>
    Employee.Create(
      EmployeeNumber.Create("E-0001").Value,
      EmployeeFullName.Create("Person One").Value,
      nationalId: null,
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
      Actor,
      Guid.NewGuid(),
      Now).Value;

  // An employee whose ownership and initial assignments have been stamped, as the application does.
  private static Employee Stamped(out Guid departmentId)
  {
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    departmentId = Guid.NewGuid();

    var employee = NewEmployee();
    employee.TenantId = tenantId;
    employee.CompanyId = companyId;
    employee.BranchId = Guid.NewGuid();

    Assert.True(employee.StampInitialAssignment(
      tenantId, companyId, employee.BranchId, departmentId, Guid.NewGuid(), Actor,
      Guid.NewGuid(), Now).IsSuccess);

    return employee;
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

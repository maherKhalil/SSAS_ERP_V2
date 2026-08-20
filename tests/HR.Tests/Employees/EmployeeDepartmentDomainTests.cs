using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Tests.Employees;

// EMPLOYEE ↔ DEPARTMENT, AT THE DOMAIN LEVEL (FP-007 Phase 3, REQ-HR-0102, ADR-026).
//
// ================================================================================================
// THE ONE CLAIM THIS FILE EXISTS TO PROVE: THE TWO DIMENSIONS ARE INDEPENDENT.
// ================================================================================================
//
// Branch is WHERE an employee works and is an ownership dimension that decides who may see them.
// Department is WHERE THEY SIT in the org structure and decides nothing about visibility. A department
// spans the branches of its company, so the two cannot be derived from each other in either direction.
//
// Everything below is a consequence of that. A transfer leaves the department alone, a department change
// leaves the branch alone, and neither operation is expressible as the other.
public sealed class EmployeeDepartmentDomainTests
{
  private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid Company = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
  private static readonly Guid BranchB = Guid.Parse("44444444-4444-4444-4444-444444444444");
  private static readonly Guid Finance = Guid.Parse("55555555-5555-5555-5555-555555555555");
  private static readonly Guid Operations = Guid.Parse("66666666-6666-6666-6666-666666666666");
  private static readonly DateTimeOffset Hired = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

  private const string Actor = "hr-user";

  // ================================================================================================
  // CREATION
  // ================================================================================================

  [Fact]
  public void A_created_employee_has_its_department_and_exactly_one_initial_record()
  {
    var employee = Stamped();

    Assert.Equal(Finance, employee.DepartmentId);

    var assignment = Assert.Single(employee.DepartmentAssignments);

    Assert.Null(assignment.SourceDepartmentId);
    Assert.Equal(Finance, assignment.DestinationDepartmentId);
    Assert.Equal(employee.Id, assignment.EmployeeId);
    Assert.Equal(Tenant, assignment.TenantId);
    Assert.Equal(Company, assignment.CompanyId);
    Assert.Equal(Now, assignment.EffectiveFromUtc);
    Assert.Equal(Actor, assignment.ChangedBy);
  }

  // BOTH HISTORIES OR NEITHER. The branch record and the department record are produced by one call, so an
  // employee cannot reach persistence carrying one and not the other.
  [Fact]
  public void The_initial_stamp_produces_both_histories_together()
  {
    var employee = Stamped();

    Assert.Single(employee.BranchAssignments);
    Assert.Single(employee.DepartmentAssignments);
  }

  // A REFUSAL LEAVES NOTHING BEHIND. If the department is missing, the branch record must not have been
  // appended either — a half-stamped employee is worse than an unstamped one, because it looks complete.
  [Fact]
  public void A_refused_stamp_appends_no_history_at_all()
  {
    var employee = NewEmployee();

    var result = employee.StampInitialAssignment(
      Tenant, Company, BranchA, Guid.Empty, Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentRequired.Code, result.Error.Code);
    Assert.Empty(employee.BranchAssignments);
    Assert.Empty(employee.DepartmentAssignments);
    Assert.Equal(Guid.Empty, employee.DepartmentId);
  }

  [Fact]
  public void An_initial_department_cannot_be_stamped_twice()
  {
    var employee = Stamped();

    var second = employee.StampInitialAssignment(
      Tenant, Company, BranchA, Operations, Actor, Guid.NewGuid(), Now);

    Assert.True(second.IsFailure);
    Assert.Single(employee.DepartmentAssignments);
    Assert.Equal(Finance, employee.DepartmentId);
  }

  // ================================================================================================
  // CHANGE
  // ================================================================================================

  [Fact]
  public void A_change_moves_the_current_department_and_appends_one_record()
  {
    var employee = Stamped();

    var changed = employee.ChangeDepartment(
      Operations, "Reorg", "Moved to the northern division", Actor, Guid.NewGuid(), Now);

    Assert.True(changed.IsSuccess);
    Assert.Equal(Operations, employee.DepartmentId);
    Assert.Equal(2, employee.DepartmentAssignments.Count);

    Assert.Equal(Finance, changed.Value.SourceDepartmentId);
    Assert.Equal(Operations, changed.Value.DestinationDepartmentId);
    Assert.Equal("Reorg", changed.Value.ReasonCode);
    Assert.Equal("Moved to the northern division", changed.Value.ReasonText);
  }

  // ---- THE EARLIER RECORD IS NOT TOUCHED. Append-only means the previous row keeps saying what it said;
  // an interval is derived by ordering, never by closing a row.
  [Fact]
  public void A_change_leaves_the_previous_record_exactly_as_it_was()
  {
    var employee = Stamped();
    var initial = employee.DepartmentAssignments.Single();

    Assert.True(employee.ChangeDepartment(
      Operations, null, null, Actor, Guid.NewGuid(), Now).IsSuccess);

    var stillInitial = employee.DepartmentAssignments.Single(
      assignment => assignment.Id == initial.Id);

    Assert.Null(stillInitial.SourceDepartmentId);
    Assert.Equal(Finance, stillInitial.DestinationDepartmentId);
    Assert.Equal(Now, stillInitial.EffectiveFromUtc);
  }

  // A non-change is refused rather than silently succeeding, and appends nothing. Answering it with success
  // would either write a row describing no movement or return a success that did nothing.
  [Fact]
  public void A_change_to_the_current_department_is_refused_and_appends_nothing()
  {
    var employee = Stamped();

    var result = employee.ChangeDepartment(Finance, null, null, Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentUnchanged.Code, result.Error.Code);
    Assert.Single(employee.DepartmentAssignments);
    Assert.Equal(Finance, employee.DepartmentId);
  }

  [Fact]
  public void A_terminated_employee_cannot_change_department()
  {
    var employee = Stamped();

    Assert.True(employee.Terminate(
      Now, EmployeeStatusChangeReason.Resignation, Actor, Guid.NewGuid(), Now).IsSuccess);

    var result = employee.ChangeDepartment(Operations, null, null, Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.InvalidTransition.Code, result.Error.Code);
    Assert.Single(employee.DepartmentAssignments);
  }

  // AN INACTIVE EMPLOYEE STILL MOVES. Same rule as a branch transfer, for the same reason: someone on leave
  // is still employed and may still be reorganized.
  [Fact]
  public void An_inactive_employee_may_still_change_department()
  {
    var employee = Stamped();

    Assert.True(employee.Deactivate(
      EmployeeStatusChangeReason.Administrative, Actor, Guid.NewGuid(), Now).IsSuccess);

    Assert.True(employee.ChangeDepartment(
      Operations, null, null, Actor, Guid.NewGuid(), Now).IsSuccess);

    Assert.Equal(Operations, employee.DepartmentId);
  }

  [Fact]
  public void A_change_requires_a_trusted_actor()
  {
    var employee = Stamped();

    var result = employee.ChangeDepartment(Operations, null, null, "   ", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.InvalidActor.Code, result.Error.Code);
    Assert.Single(employee.DepartmentAssignments);
  }

  // ================================================================================================
  // THE INDEPENDENCE PROOFS (§14, §15)
  // ================================================================================================

  // ---- A BRANCH TRANSFER DOES NOT TOUCH THE DEPARTMENT.
  //
  // +1 branch record, +0 department records. The employee works somewhere else on Monday and reports into
  // the same department, which is what "independent dimensions" means in practice.
  [Fact]
  public void A_branch_transfer_preserves_the_department_and_appends_no_department_history()
  {
    var employee = Stamped();

    var transferred = employee.Transfer(
      BranchB, EmployeeBranchTransferReason.OperationalNeed, null, Actor, Guid.NewGuid(), Now);

    Assert.True(transferred.IsSuccess);
    Assert.Equal(BranchB, employee.BranchId);
    Assert.Equal(2, employee.BranchAssignments.Count);

    Assert.Equal(Finance, employee.DepartmentId);
    Assert.Single(employee.DepartmentAssignments);
  }

  // ---- AND THE CONVERSE. A department change does not touch the branch.
  [Fact]
  public void A_department_change_preserves_the_branch_and_appends_no_branch_history()
  {
    var employee = Stamped();

    Assert.True(employee.ChangeDepartment(
      Operations, null, null, Actor, Guid.NewGuid(), Now).IsSuccess);

    Assert.Equal(BranchA, employee.BranchId);
    Assert.Single(employee.BranchAssignments);
  }

  // ---- TERMINATION KEEPS THE DEPARTMENT.
  //
  // It is not moved to UNASSIGNED, not cleared, and no history is appended. Termination closes the record;
  // it does not rewrite where the person worked, which is what keeps reporting over earlier periods correct.
  [Fact]
  public void Termination_preserves_the_department_and_appends_no_history()
  {
    var employee = Stamped();

    Assert.True(employee.Terminate(
      Now, EmployeeStatusChangeReason.Resignation, Actor, Guid.NewGuid(), Now).IsSuccess);

    Assert.Equal(Finance, employee.DepartmentId);
    Assert.Single(employee.DepartmentAssignments);
  }

  // ================================================================================================
  // ORDERING (§12)
  // ================================================================================================

  // Deterministic by EffectiveFromUtc, with the identifier breaking a tie. V1 has no future-dated change,
  // so the sequence is monotonic per employee — but two changes inside the same clock tick are possible,
  // and without the tie-break the reconstructed history could disagree with itself between reads.
  [Fact]
  public void History_orders_deterministically_even_when_timestamps_collide()
  {
    var employee = Stamped();

    Assert.True(employee.ChangeDepartment(
      Operations, null, null, Actor, Guid.NewGuid(), Now).IsSuccess);
    Assert.True(employee.ChangeDepartment(
      Finance, null, null, Actor, Guid.NewGuid(), Now).IsSuccess);

    var ordered = employee.DepartmentAssignments
      .OrderBy(assignment => assignment.EffectiveFromUtc)
      .ThenBy(assignment => assignment.Id)
      .ToArray();

    var again = employee.DepartmentAssignments
      .OrderBy(assignment => assignment.EffectiveFromUtc)
      .ThenBy(assignment => assignment.Id)
      .ToArray();

    Assert.Equal(ordered.Select(assignment => assignment.Id), again.Select(assignment => assignment.Id));
    Assert.Equal(3, ordered.Length);
  }

  private static Employee NewEmployee() =>
    Employee.Create(
      EmployeeNumber.Create("E-0001").Value,
      EmployeeFullName.Create("Person One").Value,
      nationalId: null,
      Hired,
      Actor,
      Guid.NewGuid(),
      Now).Value;

  private static Employee Stamped()
  {
    var employee = NewEmployee();
    employee.TenantId = Tenant;
    employee.CompanyId = Company;
    employee.BranchId = BranchA;

    Assert.True(employee.StampInitialAssignment(
      Tenant, Company, BranchA, Finance, Actor, Guid.NewGuid(), Now).IsSuccess);

    return employee;
  }
}

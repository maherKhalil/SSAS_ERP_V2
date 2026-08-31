using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.Events;

namespace SSAS.HR.Tests.Employees;

// THE EMPLOYEE AGGREGATE, WITHOUT A DATABASE (FP-006 domain-model, lifecycle-model).
//
// These cover what the domain decides on its own: identifier rules, the transition graph, termination dates,
// what transfer refuses, and what the events do and do not carry. The rules that need authoritative state —
// uniqueness, the ownership boundaries, scope authorization and the sanctioned transfer channel — are proven
// against real SQL Server, because an in-memory provider would agree with all of them and prove none.
public sealed class EmployeeDomainTests
{
  private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid Company = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
  private static readonly Guid BranchB = Guid.Parse("44444444-4444-4444-4444-444444444444");
  private static readonly Guid DepartmentA = Guid.Parse("55555555-5555-5555-5555-555555555555");
  private static readonly Guid DepartmentB = Guid.Parse("66666666-6666-6666-6666-666666666666");

  private static readonly Guid PositionA = Guid.Parse("77777777-7777-7777-7777-777777777777");

  private static readonly Guid PositionB = Guid.Parse("88888888-8888-8888-8888-888888888888");
  private static readonly DateTimeOffset Hired = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

  // ---- CREATION.

  [Fact]
  public void A_created_employee_is_active_with_a_server_generated_identity()
  {
    var employee = NewEmployee();

    Assert.NotEqual(Guid.Empty, employee.Id);
    Assert.Equal(employee.Id, employee.EmployeeId);
    Assert.Equal(EmployeeStatus.Active, employee.Status);
    Assert.Equal(EmployeeStatusChangeReason.Created, employee.StatusChangeReasonCode);
    Assert.Null(employee.TerminationDate);
  }

  // Two employees created identically still get different identities: the identifier is generated, never
  // derived from the input.
  [Fact]
  public void Employee_identities_are_never_reused()
  {
    Assert.NotEqual(NewEmployee().Id, NewEmployee().Id);
  }

  [Fact]
  public void An_employee_cannot_be_created_without_a_valid_actor_or_employment_date()
  {
    Assert.Equal(
      EmployeeErrors.InvalidActor.Code,
      Employee.Create(Number("E1"), Name("A"), null, Hired, "  ", Guid.NewGuid(), Now).Error.Code);

    Assert.Equal(
      EmployeeErrors.InvalidEmploymentDate.Code,
      Employee.Create(Number("E1"), Name("A"), null, default, "actor", Guid.NewGuid(), Now).Error.Code);
  }

  // ---- EMPLOYEE NUMBER.

  [Fact]
  public void An_employee_number_is_trimmed_normalized_and_case_preserving()
  {
    var number = EmployeeNumber.Create("  emp-00147  ");

    Assert.True(number.IsSuccess);
    Assert.Equal("emp-00147", number.Value.Value);
    Assert.Equal("EMP-00147", number.Value.NormalizedValue);
  }

  // Two spellings that normalize alike ARE the same number, which is what makes the per-company unique
  // index refuse the second one.
  [Fact]
  public void Employee_numbers_that_normalize_alike_are_equal()
  {
    Assert.Equal(EmployeeNumber.Create(" emp-1 ").Value, EmployeeNumber.Create("EMP-1").Value);
    Assert.NotEqual(EmployeeNumber.Create("EMP-1").Value, EmployeeNumber.Create("EMP-2").Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("badcontrol")]
  public void An_unusable_employee_number_is_refused(string? raw)
  {
    Assert.Equal(EmployeeErrors.InvalidEmployeeNumber.Code, EmployeeNumber.Create(raw).Error.Code);
  }

  // The limit applies to the STORED normalized value as well as the input, so a value cannot pass validation
  // and then fail to fit its column.
  [Fact]
  public void An_employee_number_longer_than_the_column_is_refused()
  {
    Assert.True(EmployeeNumber.Create(new string('A', EmployeeNumber.MaximumLength)).IsSuccess);
    Assert.True(EmployeeNumber.Create(new string('A', EmployeeNumber.MaximumLength + 1)).IsFailure);
  }

  // ---- THE EMPLOYEE NUMBER IS IMMUTABLE. There is no operation, on the aggregate or anywhere else, that
  // changes it after creation.
  [Fact]
  // ⚠ CITED BY ITEM 218, body-confirmed: no operation can change `EmployeeNumber` after creation -- asserted over the type's MUTATORS, not one call site.
  [Trait("Criterion", "AC-EMP-0008")]
  public void No_operation_changes_the_employee_number_or_ownership_identifiers()
  {
    // Property accessors are excluded: this asks what OPERATIONS exist, and a getter is not one.
    var mutators = typeof(Employee)
      .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
      .Where(method => method.DeclaringType == typeof(Employee) && !method.IsSpecialName)
      .Select(method => method.Name)
      .ToArray();

    Assert.DoesNotContain(mutators, name => name.Contains("Number", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(mutators, name => name.Contains("Company", StringComparison.OrdinalIgnoreCase));

    // EmployeeNumber has no PUBLIC setter. A private one exists for EF materialisation only, which no
    // caller can reach.
    Assert.False(typeof(Employee).GetProperty(nameof(Employee.EmployeeNumber))!.SetMethod?.IsPublic ?? false);
  }

  // ---- NATIONAL ID IS OPTIONAL, which is a decision (DEC-EMP-0013): BR-HR-0002 constrains its uniqueness
  // without requiring its presence.
  [Fact]
  public void An_employee_may_be_created_without_a_national_id()
  {
    var employee = NewEmployee();

    Assert.Null(employee.NationalId);
    Assert.Null(employee.NormalizedNationalId);
  }

  [Fact]
  public void A_national_id_is_normalized_like_an_employee_number()
  {
    var nationalId = NationalId.Create("  2990112345678x ");

    Assert.True(nationalId.IsSuccess);
    Assert.Equal("2990112345678x", nationalId.Value.Value);
    Assert.Equal("2990112345678X", nationalId.Value.NormalizedValue);
  }

  // Unlike the employee number it IS mutable: a recorded identity may be corrected.
  [Fact]
  public void A_national_id_can_be_corrected_through_the_profile_update()
  {
    var employee = NewEmployee();

    Assert.True(employee.UpdateProfile(Name("Layla"), NationalId.Create("A1").Value, Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal("A1", employee.NormalizedNationalId);

    Assert.True(employee.UpdateProfile(Name("Layla"), NationalId.Create("b2").Value, Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal("B2", employee.NormalizedNationalId);
  }

  // ---- LIFECYCLE.

  [Fact]
  public void The_approved_transitions_are_permitted()
  {
    var employee = NewEmployee();

    Assert.True(employee.Deactivate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(EmployeeStatus.Inactive, employee.Status);

    Assert.True(employee.Activate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(EmployeeStatus.Active, employee.Status);

    Assert.True(employee.Terminate(Now, EmployeeStatusChangeReason.Resignation, "a", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(EmployeeStatus.Terminated, employee.Status);
  }

  [Fact]
  public void Inactive_employees_may_be_terminated_directly()
  {
    var employee = NewEmployee();
    Assert.True(employee.Deactivate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsSuccess);

    Assert.True(employee.Terminate(Now, EmployeeStatusChangeReason.Dismissal, "a", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(EmployeeStatus.Terminated, employee.Status);
  }

  [Fact]
  public void Repeated_and_unlisted_transitions_are_rejected()
  {
    var employee = NewEmployee();

    // Already Active.
    Assert.Equal(
      EmployeeErrors.InvalidTransition.Code,
      employee.Activate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).Error.Code);

    Assert.True(employee.Deactivate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsSuccess);

    // Already Inactive.
    Assert.Equal(
      EmployeeErrors.InvalidTransition.Code,
      employee.Deactivate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).Error.Code);
  }

  // ---- TERMINATED IS TERMINAL, AND THERE IS NO REHIRE.
  [Fact]
  public void A_terminated_employee_cannot_transition_again()
  {
    var employee = Terminated();

    Assert.True(employee.Activate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsFailure);
    Assert.True(employee.Deactivate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsFailure);
    Assert.True(employee.Terminate(Now, EmployeeStatusChangeReason.Resignation, "a", Guid.NewGuid(), Now).IsFailure);

    // And no operation named for rehire exists at all.
    Assert.DoesNotContain(
      typeof(Employee).GetMethods().Select(method => method.Name),
      name => name.Contains("Rehire", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public void A_terminated_employee_cannot_have_its_profile_updated()
  {
    Assert.Equal(
      EmployeeErrors.InvalidTransition.Code,
      Terminated().UpdateProfile(Name("New"), null, Guid.NewGuid(), Now).Error.Code);
  }

  // ---- BR-HR-0003.
  [Fact]
  // ⚠ CITED BY ITEM 218, body-confirmed: `TerminationDate` earlier than `EmploymentDate` is refused -- and the same day is permitted.
  [Trait("Criterion", "AC-EMP-0010")]
  public void Termination_cannot_precede_employment()
  {
    var employee = NewEmployee();

    var tooEarly = employee.Terminate(
      Hired.AddDays(-1), EmployeeStatusChangeReason.Resignation, "a", Guid.NewGuid(), Now);

    Assert.True(tooEarly.IsFailure);
    Assert.Equal(EmployeeErrors.TerminationBeforeEmployment.Code, tooEarly.Error.Code);
    Assert.Equal(EmployeeStatus.Active, employee.Status);

    // The same day is permitted: the rule is "not later than", not "strictly before".
    Assert.True(employee.Terminate(
      Hired, EmployeeStatusChangeReason.EndOfContract, "a", Guid.NewGuid(), Now).IsSuccess);
  }

  // `Created` records a creation and nothing else.
  [Fact]
  public void A_lifecycle_transition_cannot_be_recorded_as_the_hire()
  {
    Assert.Equal(
      EmployeeErrors.InvalidTransitionReason.Code,
      NewEmployee().Deactivate(EmployeeStatusChangeReason.Created, "a", Guid.NewGuid(), Now).Error.Code);
  }

  // ---- INITIAL ASSIGNMENT.

  [Fact]
  // ⚠ CITED BY ITEM 218, body-confirmed: exactly one `EmployeeBranchAssignment`, `SourceBranchId` null -- asserted as `Assert.Single` plus `Assert.Null`.
  [Trait("Criterion", "AC-EMP-0005")]
  public void Creation_produces_exactly_one_initial_assignment_naming_no_source()
  {
    var employee = Stamped();

    var assignment = Assert.Single(employee.BranchAssignments);
    Assert.Null(assignment.SourceBranchId);
    Assert.Equal(BranchA, assignment.DestinationBranchId);
    Assert.Equal(EmployeeBranchTransferReason.InitialAssignment, assignment.ReasonCode);
    Assert.Equal(employee.Id, assignment.EmployeeId);
  }

  // Stamping twice would mean an employee with two hires. Refused.
  [Fact]
  public void An_initial_assignment_cannot_be_stamped_twice()
  {
    var employee = Stamped();

    var second = employee.StampInitialAssignment(
      Tenant, Company, BranchB, DepartmentB, PositionB, "a", Guid.NewGuid(), Now);

    Assert.True(second.IsFailure);
    Assert.Equal(EmployeeErrors.BranchHistoryImmutable.Code, second.Error.Code);
    Assert.Single(employee.BranchAssignments);
  }

  // ---- TRANSFER.

  [Fact]
  public void A_transfer_moves_the_current_branch_and_appends_one_record()
  {
    var employee = Stamped();

    var moved = employee.Transfer(
      BranchB, EmployeeBranchTransferReason.Reorganisation, "consolidating", "a", Guid.NewGuid(), Now);

    Assert.True(moved.IsSuccess);
    Assert.Equal(BranchB, employee.BranchId);
    Assert.Equal(2, employee.BranchAssignments.Count);

    Assert.Equal(BranchA, moved.Value.SourceBranchId);
    Assert.Equal(BranchB, moved.Value.DestinationBranchId);
    Assert.Equal("consolidating", moved.Value.ReasonText);

    // THE EARLIER RECORD IS UNTOUCHED. History is appended, never rewritten.
    var initial = employee.BranchAssignments.Single(
      assignment => assignment.ReasonCode == EmployeeBranchTransferReason.InitialAssignment);
    Assert.Null(initial.SourceBranchId);
    Assert.Equal(BranchA, initial.DestinationBranchId);
  }

  [Fact]
  public void A_terminated_employee_cannot_be_transferred()
  {
    var employee = Stamped();
    Assert.True(employee.Terminate(Now, EmployeeStatusChangeReason.Resignation, "a", Guid.NewGuid(), Now).IsSuccess);

    var moved = employee.Transfer(
      BranchB, EmployeeBranchTransferReason.Reorganisation, null, "a", Guid.NewGuid(), Now);

    Assert.True(moved.IsFailure);
    Assert.Equal(EmployeeErrors.TransferAfterTermination.Code, moved.Error.Code);
    Assert.Equal(BranchA, employee.BranchId);
  }

  // An INACTIVE employee may still be transferred — notably when their branch is closing.
  [Fact]
  public void An_inactive_employee_may_still_be_transferred()
  {
    var employee = Stamped();
    Assert.True(employee.Deactivate(EmployeeStatusChangeReason.Administrative, "a", Guid.NewGuid(), Now).IsSuccess);

    Assert.True(employee.Transfer(
      BranchB, EmployeeBranchTransferReason.BranchClosure, null, "a", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(BranchB, employee.BranchId);

    // And it is not a lifecycle transition: the status is untouched.
    Assert.Equal(EmployeeStatus.Inactive, employee.Status);
  }

  [Fact]
  public void A_transfer_to_the_current_branch_is_refused()
  {
    var employee = Stamped();

    var moved = employee.Transfer(
      BranchA, EmployeeBranchTransferReason.Reorganisation, null, "a", Guid.NewGuid(), Now);

    Assert.True(moved.IsFailure);
    Assert.Equal(EmployeeErrors.TransferDestinationUnchanged.Code, moved.Error.Code);
    Assert.Single(employee.BranchAssignments);
  }

  // `InitialAssignment` belongs to creation alone: a transfer must not be able to masquerade as a hire.
  [Fact]
  public void A_transfer_cannot_be_recorded_as_an_initial_assignment()
  {
    var employee = Stamped();

    var moved = employee.Transfer(
      BranchB, EmployeeBranchTransferReason.InitialAssignment, null, "a", Guid.NewGuid(), Now);

    Assert.True(moved.IsFailure);
    Assert.Equal(EmployeeErrors.InvalidTransferReason.Code, moved.Error.Code);
    Assert.Equal(BranchA, employee.BranchId);
  }

  [Fact]
  public void An_over_long_transfer_reason_text_is_refused()
  {
    var employee = Stamped();

    var moved = employee.Transfer(
      BranchB,
      EmployeeBranchTransferReason.Reorganisation,
      new string('x', EmployeeBranchAssignment.ReasonTextMaximumLength + 1),
      "a",
      Guid.NewGuid(),
      Now);

    Assert.True(moved.IsFailure);
    Assert.Equal(BranchA, employee.BranchId);
  }

  // ---- CORRECTION IS ANOTHER TRANSFER, NEVER A REWRITE. V1 has no cancellation, so reversing a mistake
  // leaves three records rather than one.
  [Fact]
  public void A_reversed_transfer_appends_a_third_record_rather_than_removing_one()
  {
    var employee = Stamped();

    Assert.True(employee.Transfer(
      BranchB, EmployeeBranchTransferReason.Reorganisation, null, "a", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(employee.Transfer(
      BranchA, EmployeeBranchTransferReason.Correction, null, "a", Guid.NewGuid(), Now).IsSuccess);

    Assert.Equal(BranchA, employee.BranchId);
    Assert.Equal(3, employee.BranchAssignments.Count);
  }

  // ---- NO FUTURE-DATING AND NO CANCELLATION EXIST AS OPERATIONS AT ALL.
  [Fact]
  public void There_is_no_future_dated_or_cancellable_transfer()
  {
    var operations = typeof(Employee).GetMethods().Select(method => method.Name).ToArray();

    Assert.DoesNotContain(operations, name => name.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(operations, name => name.Contains("Schedule", StringComparison.OrdinalIgnoreCase));

    // Transfer takes no effective date: the commit instant is the effective instant.
    var transfer = typeof(Employee).GetMethod(nameof(Employee.Transfer))!;
    Assert.DoesNotContain(
      transfer.GetParameters(),
      parameter => parameter.Name?.Contains("effective", StringComparison.OrdinalIgnoreCase) == true);
  }

  // ---- THE HISTORY RECORD HAS NO WAY TO BE CHANGED.
  [Fact]
  public void A_branch_assignment_exposes_no_mutator_and_no_concurrency_state()
  {
    var type = typeof(EmployeeBranchAssignment);

    // No operations at all — only property accessors, which are excluded because a getter is not one.
    Assert.Empty(type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
      .Where(method => method.DeclaringType == type && !method.IsSpecialName));

    // No RowVersion, no Modified pair, no EffectiveToUtc.
    Assert.Null(type.GetProperty("RowVersion"));
    Assert.Null(type.GetProperty("ModifiedUtc"));
    Assert.Null(type.GetProperty("EffectiveToUtc"));

    // Only the ownership interfaces may set anything, and only what the persistence layer stamps.
    foreach (var property in type.GetProperties(
      System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
    {
      // The ownership interfaces require public setters so the persistence layer can stamp them.
      if (property.Name is nameof(EmployeeBranchAssignment.TenantId) or nameof(EmployeeBranchAssignment.CompanyId))
      {
        continue;
      }

      Assert.False(property.SetMethod?.IsPublic ?? false);
    }
  }

  // ---- DOMAIN EVENTS CARRY IDENTIFIERS AND CODES, NEVER ANYTHING PERSONAL.
  [Fact]
  public void Employee_events_carry_no_personal_data()
  {
    var eventTypes = typeof(EmployeeCreated).Assembly.GetTypes()
      .Where(type => typeof(SSAS.BuildingBlocks.Domain.DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.HR.Domain.Events")
      .ToArray();

    // EVERY HR domain event, not only Employee's: 6 from FP-006, 5 from FP-007 Phase 1,
    // EmployeeDepartmentChanged from FP-007 Phase 3, 12 from FP-008 Phase 1 — four each for Position,
    // JobGrade and SalaryGrade — and EmployeePositionChanged from FP-008 Phase 3. The count is asserted so
    // a new event type cannot be added without someone confirming it carries nothing sensitive, which is
    // exactly what it forced when the Department events arrived, again in FP-007 Phase 3, again at FP-008
    // Phase 1, and again here.
    //
    // FP-007 Phase 3's event carries the two department identifiers and NOT the reason text: that field is
    // free-form operator input persisted for the audit record alone, and putting it on an event would push
    // unbounded text into every consumer and whatever they log. `EmployeePositionChanged` carries the two
    // position identifiers on identical terms, and neither the reason code nor the reason text.
    Assert.Equal(25, eventTypes.Length);

    var leaked = eventTypes
      .SelectMany(type => type.GetProperties().Select(property => $"{type.Name}.{property.Name}"))
      .Where(name => name.Contains("Name", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Number", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("National", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ReasonText", StringComparison.OrdinalIgnoreCase))
      .ToArray();

    Assert.Empty(leaked);

    // ---- AND FROM FP-008, MONEY (ADR-027, DEC-POS-0018).
    //
    // Pay bands are the one thing in the product sensitive enough to warrant a permission of their own:
    // `HR.SalaryGrades.View` exists precisely so reading the org chart does not also mean reading the pay
    // structure. **A permission that guards a table while the event stream publishes its contents guards
    // nothing** — so `SalaryGradeCreated` and `SalaryGradeUpdated` carry `IsPriced`, a boolean, and never the
    // amounts.
    //
    // Matched on the PROPERTY name alone rather than on "{Type}.{Property}" as the filter above does,
    // because a type-qualified match on "Salary" would flag `SalaryGradeCreated.SalaryGradeId` — an
    // identifier, which is exactly what these events are supposed to carry.
    var money = eventTypes
      .SelectMany(type => type.GetProperties().Select(property => new { type, property }))
      .Where(candidate =>
        candidate.property.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
        candidate.property.Name.Contains("Minimum", StringComparison.OrdinalIgnoreCase) ||
        candidate.property.Name.Contains("Midpoint", StringComparison.OrdinalIgnoreCase) ||
        candidate.property.Name.Contains("Maximum", StringComparison.OrdinalIgnoreCase) ||
        candidate.property.PropertyType == typeof(decimal) ||
        candidate.property.PropertyType == typeof(decimal?))
      .Select(candidate => $"{candidate.type.Name}.{candidate.property.Name}")
      .ToArray();

    Assert.Empty(money);

    // Titles and codes are descriptive rather than sensitive, but they are excluded for the reason the whole
    // file records: an event is the most widely-fanned-out thing an aggregate produces, and anything
    // descriptive placed on one spreads to every consumer, log and trace that touches it.
    var descriptive = eventTypes
      .SelectMany(type => type.GetProperties().Select(property => $"{type.Name}.{property.Name}"))
      .Where(name => name.Contains("Title", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Code", StringComparison.OrdinalIgnoreCase))
      .ToArray();

    Assert.Empty(descriptive);
  }

  [Fact]
  public void Creation_and_transfer_raise_their_events()
  {
    var employee = Stamped();

    Assert.Contains(employee.DomainEvents, raised => raised is EmployeeCreated);

    Assert.True(employee.Transfer(
      BranchB, EmployeeBranchTransferReason.Reorganisation, "note", "a", Guid.NewGuid(), Now).IsSuccess);

    var transferred = Assert.Single(employee.DomainEvents.OfType<EmployeeTransferred>());
    Assert.Equal(BranchA, transferred.SourceBranchId);
    Assert.Equal(BranchB, transferred.DestinationBranchId);
    Assert.Equal(EmployeeBranchTransferReason.Reorganisation, transferred.TransferReason);
  }

  private static Employee NewEmployee() =>
    Employee.Create(Number("EMP-1"), Name("Layla Haddad"), null, Hired, "actor", Guid.NewGuid(), Now).Value;

  // An employee whose ownership and initial assignment have been stamped, as the application does.
  private static Employee Stamped()
  {
    var employee = NewEmployee();
    employee.TenantId = Tenant;
    employee.CompanyId = Company;
    employee.BranchId = BranchA;
    Assert.True(employee.StampInitialAssignment(
      Tenant, Company, BranchA, DepartmentA, PositionA, "actor", Guid.NewGuid(), Now).IsSuccess);
    return employee;
  }

  private static Employee Terminated()
  {
    var employee = Stamped();
    Assert.True(employee.Terminate(Now, EmployeeStatusChangeReason.Resignation, "a", Guid.NewGuid(), Now).IsSuccess);
    return employee;
  }

  private static EmployeeNumber Number(string value) => EmployeeNumber.Create(value).Value;

  private static EmployeeFullName Name(string value) => EmployeeFullName.Create(value).Value;
}

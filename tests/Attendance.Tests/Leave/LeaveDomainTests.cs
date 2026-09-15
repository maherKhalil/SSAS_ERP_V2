using System.Reflection;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Tests.Leave;

// TS-ATT-0010 to TS-ATT-0016. The leave half.
public sealed class LeaveTypeTests
{
  private static readonly Guid Company = Guid.NewGuid();

  [Fact]
  // ⚠ CITES `AC-ATT-0021` FOR ITS FIRST CLAUSE ONLY. *"A leave type's `Code` cannot be changed after
  // creation"* is asserted here. *"...and an update request carrying one is refused as an unknown property"*
  // is a TRANSPORT claim about the update DTO and is not asserted by this domain test — the id must not be
  // read as covering it.
  [Trait("Requirement", "REQ-ATT-0010")]
  [Trait("Criterion", "AC-ATT-0021")]
  public void A_leave_type_code_is_immutable_from_creation()
  {
    var leaveType = LeaveType.Create(Company, "ANN", "Annual", LeaveBehaviour.PaidFromBalance, false).Value;

    // The absent capability IS the rule. `Update` has no code parameter, following `Account` and
    // `PayElement`: re-coding a type would silently re-label leave people have already taken.
    var update = typeof(LeaveType).GetMethod(nameof(LeaveType.Update))!;
    Assert.DoesNotContain(update.GetParameters(), parameter =>
      parameter.Name!.Contains("code", StringComparison.OrdinalIgnoreCase) ||
      parameter.Name!.Contains("behaviour", StringComparison.OrdinalIgnoreCase));

    Assert.True(leaveType.Update("Annual Leave", isSensitive: false).IsSuccess);
    Assert.Equal("ANN", leaveType.Code.Value);
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0010")]
  public void A_leave_type_is_deactivated_never_deleted_and_reactivation_is_possible()
  {
    var leaveType = LeaveType.Create(Company, "SICK", "Sick", LeaveBehaviour.PaidFromBalance, true).Value;

    Assert.True(leaveType.IsActive);
    Assert.True(leaveType.SetActivation(false).IsSuccess);
    Assert.False(leaveType.IsActive);

    // Deactivating twice is a refusal rather than a no-op: the caller believed it was active.
    Assert.True(leaveType.SetActivation(false).IsFailure);

    Assert.True(leaveType.SetActivation(true).IsSuccess);
  }

  // ================================================================================================
  // TS-ATT-0016. THE `DEC-PAY-0002` GUARD IN ITS ATTENDANCE FORM.
  // ================================================================================================
  //
  // **A behaviour whose input does not exist must not be declared.** `OD-ATT-0006` ruled balances
  // ADMINISTERED and deferred accrual, so no accrual engine exists — and an `Accruing` member would be
  // `PayElementBehaviour`'s `OvertimeMultiple` mistake in a fresh costume: an enum value the code cannot
  // honour, sitting in the model looking implemented.
  //
  // This asserts the boundary rather than documenting it. A future member that added accrual would have to
  // delete this test, which is the point.
  [Fact]
  [Trait("Decision", "OD-ATT-0006")]
  public void No_accrual_behaviour_exists_because_accrual_is_deferred()
  {
    Assert.DoesNotContain(
      Enum.GetNames<LeaveBehaviour>(),
      name => name.Contains("Accru", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Carry", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Expir", StringComparison.OrdinalIgnoreCase));
  }

  // `PaidWithoutBalance` must NOT consume, or a zero balance would refuse a statutory entitlement the
  // company grants without metering.
  [Theory]
  [InlineData(LeaveBehaviour.PaidFromBalance, true)]
  [InlineData(LeaveBehaviour.Unpaid, true)]
  [InlineData(LeaveBehaviour.PaidWithoutBalance, false)]
  public void Only_metered_behaviours_consume_a_balance(LeaveBehaviour behaviour, bool consumes)
  {
    var leaveType = LeaveType.Create(Company, "X", "X", behaviour, false).Value;

    Assert.Equal(consumes, leaveType.ConsumesBalance);
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0014")]
  public void Leave_types_balances_and_requests_are_not_branch_owned()
  {
    // The negatives, asserted. `DEC-ATT-0014` forbids classification-by-omission, which is precisely how
    // Payroll's entities ended up tenant-global with no test saying so.
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(LeaveType)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(LeaveBalance)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(LeaveRequest)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(WorkingCalendar)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(CalendarHoliday)));
  }
}

public sealed class LeaveBalanceTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid Type = Guid.NewGuid();

  private static LeaveBalance Balance(decimal entitlement = 20m) =>
    LeaveBalance.Create(Company, Employee, Type, 2026, entitlement).Value;

  [Fact]
  [Trait("Criterion", "AC-ATT-0040")]
  public void Consumed_is_never_directly_settable()
  {
    // The private setter is the whole point: a settable consumed figure would let somebody reconcile a
    // balance by typing over it, while the leave that produced the discrepancy sat in the request table
    // saying otherwise.
    var consumed = typeof(LeaveBalance).GetProperty(nameof(LeaveBalance.ConsumedQuantity))!;
    Assert.False(consumed.SetMethod!.IsPublic);
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0015")]
  public void Consumption_moves_the_balance_and_release_returns_it()
  {
    var balance = Balance(20m);

    Assert.True(balance.Consume(5m).IsSuccess);
    Assert.Equal(5m, balance.ConsumedQuantity);
    Assert.Equal(15m, balance.RemainingQuantity);

    Assert.True(balance.Release(5m).IsSuccess);
    Assert.Equal(0m, balance.ConsumedQuantity);
  }

  [Fact]
  public void Consuming_beyond_the_entitlement_is_refused()
  {
    var balance = Balance(3m);

    var consumed = balance.Consume(4m);

    Assert.True(consumed.IsFailure);
    Assert.Equal(LeaveErrors.InsufficientBalance.Code, consumed.Error.Code);
    Assert.Equal(0m, balance.ConsumedQuantity);
  }

  [Fact]
  public void Releasing_more_than_was_consumed_is_refused()
  {
    var balance = Balance(10m);
    balance.Consume(2m);

    var released = balance.Release(3m);

    Assert.True(released.IsFailure);
    Assert.Equal(LeaveErrors.ReleaseExceedsConsumption.Code, released.Error.Code);
  }

  // ---- AN ENTITLEMENT MAY BE REDUCED BELOW WHAT IS ALREADY CONSUMED, DELIBERATELY.
  //
  // Refusing would be worse. The leave was genuinely taken and the entitlement was genuinely wrong; the
  // honest outcome is a negative remaining balance somebody can see and act on, not a refusal that leaves
  // the wrong figure standing because the right one is inconvenient.
  [Fact]
  public void An_entitlement_may_be_reduced_below_what_is_consumed_leaving_a_visible_negative()
  {
    var balance = Balance(20m);
    balance.Consume(15m);

    Assert.True(balance.SetEntitlement(10m).IsSuccess);
    Assert.Equal(-5m, balance.RemainingQuantity);
  }
}

public sealed class LeaveRequestTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid Manager = Guid.NewGuid();
  private static readonly Guid Type = Guid.NewGuid();

  private static LeaveRequest Request(decimal days = 3m, DateOnly? start = null) =>
    LeaveRequest.Submit(
      Company, Employee, Type,
      start ?? new DateOnly(2026, 9, 21), (start ?? new DateOnly(2026, 9, 21)).AddDays(4), days).Value;

  // ================================================================================================
  // TS-ATT-0010. LEAVE CONSUMES WORKING DAYS, COMPUTED FROM THE CALENDAR AT SUBMISSION.
  // ================================================================================================

  // ---- ⚠⚠⚠ WHAT A SUBMISSION RECORDS (AC-ATT-0041), AND FOUR OF ITS FIVE CLAUSES HAD NO ASSERTION.
  //
  // *"Submitting a request records the requester, the type, the range and the computed working-day
  // consumption, and refuses an end date before the start date."* **Five clauses.** The consumption was
  // covered — thoroughly, by the two tests below and by the holiday-after-approval freeze, and the
  // REFUSAL was covered too, by `A_request_cannot_end_before_it_starts`. ***THE THREE STORED VALUES
  // WERE NOT.***
  //
  // ⚠⚠⚠ I WROTE A DUPLICATE OF THAT REFUSAL TEST BEFORE FINDING IT, AND THE REASON IS A TOKEN
  // TRANSPOSITION WORTH RECORDING. I searched `tests/` for `RangeInvalid` — the fragment in the error's
  // CODE, `Attendance.LeaveRequestRangeInvalid` — and found only GL's. **The C# field is named
  // `InvalidRequestRange`: the same two words, transposed.** *A search for the code string cannot find
  // a test that references the field, and nothing about either name suggests the other.* The plant is
  // what found it: reddening the range guard failed TWO tests, one of which I did not know existed.
  //
  // ⚠ THE THREE STORED VALUES WERE NEVER READ BACK. `EmployeeId`, `LeaveTypeId`, `StartDate` and `EndDate`
  // are asserted nowhere in this suite — the only `Assert` on a request's own fields was
  // `Assert.Null(request.ApproverEmployeeId)`, which is about a decision rather than a submission.
  // **A `Submit` that transposed the employee and the type, or stored the start date twice, would have
  // produced a request every existing test accepted** — because every one of them reads only
  // `WorkingDaysConsumed`, which is passed IN rather than derived from the other four.
  //
  // ⚠⚠ AND THE VALUES ARE DELIBERATELY ALL DIFFERENT. `Company`, `Employee` and `Type` are distinct
  // constants and the two dates differ, so a transposition has somewhere to show. *Four fields checked
  // against four values that could be told apart is the assertion; four fields checked against one shared
  // value would not be.*
  [Fact]
  [Trait("Requirement", "REQ-ATT-0012")]
  [Trait("Criterion", "AC-ATT-0041")]
  public void A_submission_records_the_requester_the_type_the_range_and_the_consumption()
  {
    var start = new DateOnly(2026, 9, 14);
    var end = new DateOnly(2026, 9, 16);

    var request = LeaveRequest.Submit(Company, Employee, Type, start, end, workingDaysConsumed: 3m);

    Assert.True(request.IsSuccess, request.IsFailure ? request.Error.Message : string.Empty);

    Assert.Equal(Employee, request.Value.EmployeeId);
    Assert.Equal(Type, request.Value.LeaveTypeId);
    Assert.Equal(start, request.Value.StartDate);
    Assert.Equal(end, request.Value.EndDate);
    Assert.Equal(3m, request.Value.WorkingDaysConsumed);
  }

  [Fact]
  // ⚠ CITES `AC-ATT-0016`. The consumption is PINNED at 2, not bracketed — and "only the working days
  // inside it" quantifies over a COMPLEMENT, so the range deliberately contains two non-working days for
  // the word to mean anything.
  [Trait("Requirement", "REQ-ATT-0013")]
  [Trait("Criterion", "AC-ATT-0016")]
  public void A_request_spanning_a_weekend_consumes_only_the_working_days_inside_it()
  {
    var calendar = WorkingCalendar.Create(
      Company, "Standard", [DayOfWeek.Saturday, DayOfWeek.Sunday], isDefault: true).Value;

    // Friday to Monday: four calendar days, two working days.
    var days = calendar.WorkingDaysBetween(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14));
    Assert.Equal(2, days);

    var request = LeaveRequest.Submit(
      Company, Employee, Type, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14), days);

    Assert.True(request.IsSuccess);
    Assert.Equal(2m, request.Value.WorkingDaysConsumed);
  }

  [Fact]
  // ⚠ CITES `AC-ATT-0017`. `Assert.Equal(before - 1, after)` pins the DELTA rather than a literal, so
  // it stays exact if the range changes — a reduction of two fails it, which "fewer than before" would not.
  [Trait("Requirement", "REQ-ATT-0013")]
  [Trait("Criterion", "AC-ATT-0017")]
  public void A_request_spanning_a_holiday_consumes_one_fewer_day()
  {
    var calendar = WorkingCalendar.Create(
      Company, "Standard", [DayOfWeek.Saturday, DayOfWeek.Sunday], isDefault: true).Value;
    var from = new DateOnly(2026, 9, 7);
    var to = new DateOnly(2026, 9, 11);

    var before = calendar.WorkingDaysBetween(from, to);
    calendar.AddHoliday(new DateOnly(2026, 9, 9), "National Day");
    var after = calendar.WorkingDaysBetween(from, to);

    Assert.Equal(before - 1, after);
  }

  // ================================================================================================
  // TS-ATT-0012. THE FROZEN FIGURE (BR-ATT-0003, AC-ATT-0019).
  // ================================================================================================
  //
  // **A holiday added AFTER approval must not change what an approved request consumed.** Otherwise a
  // balance that was already settled would silently move, and so would what somebody was paid.
  //
  // The guarantee comes from `WorkingDaysConsumed` being STORED at submission rather than derived on read,
  // and this asserts exactly that: the calendar changes, the request does not.
  [Fact]
  [Trait("Criterion", "AC-ATT-0019")]
  public void A_holiday_added_after_approval_does_not_change_what_the_request_consumed()
  {
    var calendar = WorkingCalendar.Create(
      Company, "Standard", [DayOfWeek.Saturday, DayOfWeek.Sunday], isDefault: true).Value;
    var from = new DateOnly(2026, 9, 7);
    var to = new DateOnly(2026, 9, 11);

    var atSubmission = calendar.WorkingDaysBetween(from, to);
    var request = LeaveRequest.Submit(Company, Employee, Type, from, to, atSubmission).Value;
    request.Approve(Manager, "hr-admin", DateTimeOffset.UtcNow, note: null);

    // The world moves on: a public holiday is declared inside the range that was already approved.
    calendar.AddHoliday(new DateOnly(2026, 9, 9), "Declared later");

    Assert.NotEqual(atSubmission, calendar.WorkingDaysBetween(from, to));
    Assert.Equal(atSubmission, request.WorkingDaysConsumed);
  }

  // ================================================================================================
  // TS-ATT-0013. THE SELF-APPROVAL BAR, IN THE AGGREGATE (AC-ATT-0020, BR-ATT-0007).
  // ================================================================================================
  //
  // A permission check answers "may this person approve requests". It cannot answer "may this person approve
  // THIS request", because only the aggregate knows both parties. No endpoint is involved here, which is the
  // assertion.
  //
  // ⚠ CITES `AC-ATT-0020` — *"An approver who is the requester is refused **by the domain**, not by the
  // endpoint."* **The second half is carried by WHERE this test sits, not by an assertion**: it calls
  // `request.Approve` on the aggregate directly and constructs no endpoint, so the refusal it observes
  // cannot have come from one. Rejection is asserted too — deciding NO exercises the same authority — and
  // the status is asserted unchanged, which the failure value alone would not carry.
  [Fact]
  [Trait("Rule", "BR-ATT-0007")]
  [Trait("Criterion", "AC-ATT-0020")]
  public void An_employee_cannot_decide_their_own_request()
  {
    var request = Request();

    var approved = request.Approve(Employee, "someone", DateTimeOffset.UtcNow, note: null);
    Assert.True(approved.IsFailure);
    Assert.Equal(LeaveErrors.SelfApprovalBarred.Code, approved.Error.Code);

    // And rejection too. Deciding NO is as much an exercise of approval authority as deciding yes.
    var rejected = request.Reject(Employee, "someone", DateTimeOffset.UtcNow, note: null);
    Assert.True(rejected.IsFailure);
    Assert.Equal(LeaveErrors.SelfApprovalBarred.Code, rejected.Error.Code);

    Assert.Equal(LeaveRequestStatus.Submitted, request.Status);
  }

  [Fact]
  public void A_request_can_only_be_decided_once()
  {
    var request = Request();
    request.Approve(Manager, "hr-admin", DateTimeOffset.UtcNow, note: null);

    var second = request.Approve(Manager, "hr-admin", DateTimeOffset.UtcNow, note: null);

    Assert.True(second.IsFailure);
    Assert.Equal(LeaveErrors.RequestAlreadyDecided.Code, second.Error.Code);
  }

  // ---- THE ROOT-FALLBACK PATH RECORDS A NULL APPROVER, AND THE NULL IS A STATEMENT.
  //
  // The holder is authenticated as a USER and the decision is not attributed to an employee, so nothing is
  // recorded there — as opposed to writing `Guid.Empty` and letting a reader mistake it for an employee.
  //
  // **`ApproverEmployeeId` stays null even when the acting user IS resolvable (T-084).** Recording it would
  // give the column two meanings — *the root path was used* and *the user could not be resolved* — and
  // would silently reinterpret every row already written.
  [Fact]
  [Trait("Decision", "OD-ATT-0007")]
  public void A_root_fallback_decision_records_the_user_and_no_approver_employee()
  {
    var request = Request();

    Assert.True(request.ApproveAtRoot(
      ActingEmployee.Resolved(Guid.NewGuid()), "root-admin", DateTimeOffset.UtcNow, "No manager above this employee").IsSuccess);

    Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    Assert.Equal("root-admin", request.DecidedBy);
    Assert.Null(request.ApproverEmployeeId);
  }

  // ================================================================================================
  // THE SELF-APPROVAL BAR ON THE ROOT PATH (BR-ATT-0007, T-084).
  // ================================================================================================
  //
  // Until `UserEmployeeLink` existed, this could not be checked: the actor on this path is a user and
  // nothing could turn one into an employee. **The router's case 2 — "every manager in the chain is the
  // requester (a one-department company run by its manager)" — is the branch that produces exactly the
  // situation the bar exists to refuse.**
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void A_root_fallback_holder_who_is_the_requester_is_refused_on_both_verbs()
  {
    var approving = Request();
    var rejecting = Request();

    var approved = approving.ApproveAtRoot(ActingEmployee.Resolved(Employee), "root-admin", DateTimeOffset.UtcNow, "note");
    var rejected = rejecting.RejectAtRoot(ActingEmployee.Resolved(Employee), "root-admin", DateTimeOffset.UtcNow, "note");

    Assert.Equal(LeaveErrors.SelfApprovalBarred.Code, approved.Error.Code);
    Assert.Equal(LeaveErrors.SelfApprovalBarred.Code, rejected.Error.Code);

    // AND NEITHER REQUEST MOVED. A refusal that had already mutated the aggregate would leave a decided
    // request behind a failed Result, which the caller would never see.
    Assert.Equal(LeaveRequestStatus.Submitted, approving.Status);
    Assert.Equal(LeaveRequestStatus.Submitted, rejecting.Status);
  }

  // THE CONTROL. Without it the test above passes against a bar that refuses EVERY root decision, which
  // would break the fallback for the one holder it exists for.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void A_root_fallback_holder_who_is_a_different_employee_is_accepted_on_both_verbs()
  {
    var approving = Request();
    var rejecting = Request();
    var someoneElse = Guid.NewGuid();

    Assert.True(approving.ApproveAtRoot(ActingEmployee.Resolved(someoneElse), "root-admin", DateTimeOffset.UtcNow, "note").IsSuccess);
    Assert.True(rejecting.RejectAtRoot(ActingEmployee.Resolved(someoneElse), "root-admin", DateTimeOffset.UtcNow, "note").IsSuccess);

    Assert.Equal(LeaveRequestStatus.Approved, approving.Status);
    Assert.Equal(LeaveRequestStatus.Rejected, rejecting.Status);
  }

  // ---- AN UNRESOLVABLE USER IS NOT A REFUSAL, AND THIS IS THE ASSERTION THAT KEEPS IT THAT WAY.
  //
  // `ADR-030` Decision 5: the link is optional on both sides. A platform-support holder with no employee
  // record is a normal caller — *"a support administrator opening a self-service page is not a fault
  // condition; it is Tuesday"* — and **they cannot be the requester, so the bar does not apply.**
  //
  // Refusing on absence would break the root fallback for precisely the operator it exists for, and it
  // would turn the bar into a mapping requirement. **That is what this test forbids.**
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void An_unresolvable_acting_user_is_not_refused_on_either_verb()
  {
    var approving = Request();
    var rejecting = Request();

    Assert.True(approving.ApproveAtRoot(ActingEmployee.Unresolved(), "support-admin", DateTimeOffset.UtcNow, "note").IsSuccess);
    Assert.True(rejecting.RejectAtRoot(ActingEmployee.Unresolved(), "support-admin", DateTimeOffset.UtcNow, "note").IsSuccess);

    Assert.Equal(LeaveRequestStatus.Approved, approving.Status);
    Assert.Equal(LeaveRequestStatus.Rejected, rejecting.Status);
  }

  // An employee-identified approval REFUSES an empty approver, which is what keeps the root path from being
  // reachable by accident through the ordinary method.
  [Fact]
  public void An_employee_identified_approval_refuses_an_empty_approver()
  {
    var request = Request();

    var approved = request.Approve(Guid.Empty, "hr-admin", DateTimeOffset.UtcNow, note: null);

    Assert.True(approved.IsFailure);
    Assert.Equal(LeaveErrors.ApproverRequired.Code, approved.Error.Code);
  }

  // ================================================================================================
  // CANCELLATION, AND WHY THE DATES MATTER (REQ-ATT-0016, AC-ATT-0042).
  // ================================================================================================
  [Fact]
  [Trait("Requirement", "REQ-ATT-0016")]
  public void A_request_may_be_cancelled_before_it_starts()
  {
    var request = Request(start: new DateOnly(2026, 9, 21));

    var cancelled = request.Cancel(new DateOnly(2026, 9, 1));

    Assert.True(cancelled.IsSuccess);
    Assert.Equal(LeaveRequestStatus.Cancelled, request.Status);
  }

  // After the dates have started the absence is a FACT that occurred. Cancelling it is a correction, which
  // routes through `OD-ATT-0012`'s adjustment path — so this refuses rather than quietly reversing a balance
  // for days somebody actually took off.
  [Fact]
  [Trait("Criterion", "AC-ATT-0042")]
  public void A_request_that_has_already_started_cannot_be_cancelled()
  {
    var request = Request(start: new DateOnly(2026, 9, 21));

    var cancelled = request.Cancel(new DateOnly(2026, 9, 22));

    Assert.True(cancelled.IsFailure);
    Assert.Equal(LeaveErrors.RequestAlreadyStarted.Code, cancelled.Error.Code);
  }

  [Fact]
  public void A_rejected_request_cannot_be_cancelled()
  {
    var request = Request();
    request.Reject(Manager, "hr-admin", DateTimeOffset.UtcNow, note: null);

    var cancelled = request.Cancel(new DateOnly(2026, 9, 1));

    Assert.True(cancelled.IsFailure);
    Assert.Equal(LeaveErrors.RejectedRequestNotCancellable.Code, cancelled.Error.Code);
  }

  // A range with no working day would decrement nothing on approval. Refused at submission so the requester
  // learns immediately rather than holding an approved request that had no effect.
  [Fact]
  public void A_request_containing_no_working_day_is_refused()
  {
    var request = LeaveRequest.Submit(
      Company, Employee, Type, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13), workingDaysConsumed: 0m);

    Assert.True(request.IsFailure);
    Assert.Equal(LeaveErrors.RequestContainsNoWorkingDay.Code, request.Error.Code);
  }

  // ---- ⚠ CITES `AC-ATT-0041`'s FIFTH CLAUSE — *"...and refuses an end date before the start date."*
  //
  // **This already asserted the clause and carried no trait**, which is why a trait-derived count read
  // `AC-ATT-0041` as uncited while four of its five clauses were genuinely uncovered and the fifth was
  // covered here all along. *The other four are
  // `A_submission_records_the_requester_the_type_the_range_and_the_consumption` and the two consumption
  // tests above it.*
  //
  // ⚠⚠ THE DAY COUNT IS POSITIVE ON PURPOSE, and the reason is worth keeping: `Submit` checks the range
  // BEFORE it checks that a request contains a working day, and a reversed range would ordinarily carry a
  // count of zero. **Passing zero would leave two guards able to refuse and only the first observable.**
  [Fact]
  [Trait("Criterion", "AC-ATT-0041")]
  public void A_request_cannot_end_before_it_starts()
  {
    var request = LeaveRequest.Submit(
      Company, Employee, Type, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 11), 1m);

    Assert.True(request.IsFailure);
    Assert.Equal(LeaveErrors.InvalidRequestRange.Code, request.Error.Code);
  }
}

// ==================================================================================================
// THE ACTING-EMPLOYEE WRAPPER'S OWN INVARIANTS (T-085).
// ==================================================================================================
//
// The type exists to make "we do not know who this is" a NAMED act rather than a bare `null`. These pin
// the properties that claim rests on — **if any of them stops holding, the type is a name over the same
// silent skip** and the ruling behind it is void.
public sealed class ActingEmployeeTests
{
  // ---- THE CONSTRAINT THE WHOLE TYPE RESTS ON.
  //
  // If an unresolved instance can be obtained without writing `Unresolved()`, the wrapper has reintroduced
  // the defect wearing a type name. A struct could not satisfy this — C# guarantees a reachable
  // `default(T)` for every one — which is why this is a class, and this asserts the class keeps the
  // property the struct could never have had.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void There_is_no_way_to_construct_one_without_naming_which_kind_it_is()
  {
    var constructors = typeof(ActingEmployee)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

    Assert.Empty(constructors);

    // And no back door that would amount to one: an `Empty`-style static, or a conversion from the raw
    // identifier, would each let a caller obtain an actor without saying which kind they meant.
    Assert.Empty(typeof(ActingEmployee)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Where(method => method.Name is "op_Implicit" or "op_Explicit"));

    Assert.Empty(typeof(ActingEmployee)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.FieldType == typeof(ActingEmployee)));
  }

  // An unresolved actor matches nobody. The comparison lives inside the type precisely so this cannot be
  // got wrong at a call site — the operator case must never read as "this is the requester".
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void An_unresolved_actor_matches_nobody()
  {
    Assert.False(ActingEmployee.Unresolved().Matches(Guid.NewGuid()));
    Assert.False(ActingEmployee.Unresolved().Matches(Guid.Empty));
  }

  // The control: a resolved actor matches exactly one employee and no other. Without it the test above
  // passes against a `Matches` that always returns false, which would silently disable the bar.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void A_resolved_actor_matches_that_employee_and_no_other()
  {
    var employee = Guid.NewGuid();

    Assert.True(ActingEmployee.Resolved(employee).Matches(employee));
    Assert.False(ActingEmployee.Resolved(employee).Matches(Guid.NewGuid()));
  }

  // An empty identifier is refused rather than coerced. Accepting one would produce an actor that looks
  // resolved and matches nothing — which is the unresolved case, arrived at by accident.
  [Fact]
  [Trait("Decision", "BR-ATT-0007")]
  public void An_empty_identifier_is_not_a_resolved_actor()
  {
    Assert.Throws<ArgumentException>(() => ActingEmployee.Resolved(Guid.Empty));
  }
}

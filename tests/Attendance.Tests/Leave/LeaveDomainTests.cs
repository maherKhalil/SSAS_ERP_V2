using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Tests.Leave;

// TS-ATT-0010 to TS-ATT-0016. The leave half.
public sealed class LeaveTypeTests
{
  private static readonly Guid Company = Guid.NewGuid();

  [Fact]
  [Trait("Requirement", "REQ-ATT-0010")]
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
  [Fact]
  [Trait("Requirement", "REQ-ATT-0013")]
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
  [Trait("Requirement", "REQ-ATT-0013")]
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
  [Fact]
  [Trait("Rule", "BR-ATT-0007")]
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
  // The holder is authenticated as a USER, and no identity-to-employee mapping exists (`OD-ATT-0013`). There
  // is no employee to record, so nothing is recorded — as opposed to writing `Guid.Empty` and letting a
  // reader mistake it for an employee.
  [Fact]
  [Trait("Decision", "OD-ATT-0007")]
  public void A_root_fallback_decision_records_the_user_and_no_approver_employee()
  {
    var request = Request();

    Assert.True(request.ApproveAtRoot("root-admin", DateTimeOffset.UtcNow, "No manager above this employee").IsSuccess);

    Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    Assert.Equal("root-admin", request.DecidedBy);
    Assert.Null(request.ApproverEmployeeId);
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

  [Fact]
  public void A_request_cannot_end_before_it_starts()
  {
    var request = LeaveRequest.Submit(
      Company, Employee, Type, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 11), 1m);

    Assert.True(request.IsFailure);
    Assert.Equal(LeaveErrors.InvalidRequestRange.Code, request.Error.Code);
  }
}

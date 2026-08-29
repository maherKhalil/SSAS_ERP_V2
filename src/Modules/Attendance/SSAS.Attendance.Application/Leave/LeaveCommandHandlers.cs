using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.SharedKernel;
using SSAS.Attendance.Application.Abstractions;
using SSAS.Attendance.Application.Approval;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Contracts.Employment;

namespace SSAS.Attendance.Application.Leave;

public sealed record CreateLeaveTypeCommand(
  Guid CompanyId, string? Code, string? Name, LeaveBehaviour Behaviour, bool IsSensitive);

// No `code` and no `behaviour`. The code is immutable from creation, following `Account` and `PayElement`;
// the behaviour is absent because changing what a type DOES would redefine what past requests consumed
// while leaving their stored rows untouched. A caller who sends either gets a 400 from the strict reader
// rather than a silently ignored property.
public sealed record UpdateLeaveTypeCommand(Guid LeaveTypeId, string? Name, bool IsSensitive);

public sealed record SetLeaveTypeActivationCommand(Guid LeaveTypeId, bool IsActive);

public sealed record SetLeaveEntitlementCommand(
  Guid CompanyId, Guid EmployeeId, Guid LeaveTypeId, int PeriodYear, decimal EntitlementQuantity);

public sealed record SubmitLeaveRequestCommand(
  Guid CompanyId, Guid EmployeeId, Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate);

public sealed record DecideLeaveRequestCommand(Guid LeaveRequestId, string? DecisionNote);

public sealed record CancelLeaveRequestCommand(Guid LeaveRequestId);

public sealed class CreateLeaveTypeCommandHandler(
  ILeaveTypeRepository leaveTypes,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateLeaveTypeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageLeaveTypes, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var leaveType = LeaveType.Create(
      command.CompanyId, command.Code, command.Name, command.Behaviour, command.IsSensitive);
    if (leaveType.IsFailure)
    {
      return Result.Failure<Guid>(leaveType.Error);
    }

    if (await leaveTypes.CodeExistsAsync(
      command.CompanyId, leaveType.Value.Code.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(LeaveErrors.DuplicateLeaveTypeCode);
    }

    await leaveTypes.AddAsync(leaveType.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE LEAVE TYPE RACE: THE GUARD AND THE INDEX NAME THE SAME CONDITION (T-176).
      //
      // `ILeaveTypeRepository.CodeExistsAsync` is a read, so two callers can both pass it with the same value and both reach
      // this save. **the unique index on `(TenantId, CompanyId, NormalizedCode)` decides it at commit**, and the loser reached
      // `AttendanceApiErrorMapper` with an unmapped `Persistence.UniqueConstraint` — answered 500 for a
      // plain business conflict, while `LeaveErrors.DuplicateLeaveTypeCode` sat mapped to 409 and unreturned on
      // this path.
      //
      // ---- ⚠ THE SAME CODE HONESTLY SERVES BOTH, AND THAT IS NOT TRUE OF EVERY RACE.
      //
      // **The race and the pre-check produce an IDENTICAL caller-visible condition** — the name is taken —
      // so one code answers both without lying about either. **Retrying the identical request fails again:**
      // the caller must change the input, not repeat it.
      //
      // That is the opposite of the leave-entitlement race, where a retry finds the winner's row and
      // succeeds, and of the journal reversal, where two different conditions collapse into one exception
      // and neither can be named. **Same 409 in all three; three different things for a client to do.**
      //
      // ⚠ **SOUND ONLY WHILE THIS HANDLER CAN REACH EXACTLY ONE UNIQUE INDEX.** It writes a `LeaveType` and nothing else.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(LeaveErrors.DuplicateLeaveTypeCode);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    return Result.Success(leaveType.Value.Id);
  }
}

public sealed class UpdateLeaveTypeCommandHandler(
  ILeaveTypeRepository leaveTypes,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    UpdateLeaveTypeCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var leaveType = await leaveTypes.GetByIdAsync(command.LeaveTypeId, cancellationToken);
    if (leaveType is null)
    {
      return Result.Failure(LeaveErrors.LeaveTypeNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageLeaveTypes, leaveType.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var updated = leaveType.Update(command.Name, command.IsSensitive);
    if (updated.IsFailure)
    {
      return updated;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// Deactivated, never deleted (`BR-ATT-0009`). A deactivated type cannot be named on a NEW request while
// every existing request referencing it stays intact — the `PayElement` and `Account` precedent, and the
// only treatment that keeps historical leave readable.
public sealed class SetLeaveTypeActivationCommandHandler(
  ILeaveTypeRepository leaveTypes,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    SetLeaveTypeActivationCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var leaveType = await leaveTypes.GetByIdAsync(command.LeaveTypeId, cancellationToken);
    if (leaveType is null)
    {
      return Result.Failure(LeaveErrors.LeaveTypeNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageLeaveTypes, leaveType.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var changed = leaveType.SetActivation(command.IsActive);
    if (changed.IsFailure)
    {
      return changed;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// The administered half of `OD-ATT-0006`. Sets ENTITLEMENT; consumption is a consequence of approvals and
// is never settable — `LeaveBalance.ConsumedQuantity` has a private setter for exactly that reason.
public sealed class SetLeaveEntitlementCommandHandler(
  ILeaveBalanceRepository balances,
  ILeaveTypeRepository leaveTypes,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleAsync(
    SetLeaveEntitlementCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageLeave, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var leaveType = await leaveTypes.GetByIdAsync(command.LeaveTypeId, cancellationToken);
    if (leaveType is null || leaveType.CompanyId != command.CompanyId)
    {
      return Result.Failure<Guid>(LeaveErrors.LeaveTypeNotFound);
    }

    var existing = await balances.GetForEmployeeAsync(
      command.CompanyId, command.EmployeeId, command.LeaveTypeId, command.PeriodYear, cancellationToken);

    if (existing is not null)
    {
      var reset = existing.SetEntitlement(command.EntitlementQuantity);
      if (reset.IsFailure)
      {
        return Result.Failure<Guid>(reset.Error);
      }

      var updated = await unitOfWork.SaveChangesAsync(cancellationToken);
      return updated.IsFailure ? Result.Failure<Guid>(updated.Error) : Result.Success(existing.Id);
    }

    var balance = LeaveBalance.Create(
      command.CompanyId, command.EmployeeId, command.LeaveTypeId,
      command.PeriodYear, command.EntitlementQuantity);
    if (balance.IsFailure)
    {
      return Result.Failure<Guid>(balance.Error);
    }

    await balances.AddAsync(balance.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      // ---- THE ENTITLEMENT RACE, AND WHY THE LOSER IS TOLD TO RETRY (T-171).
      //
      // The read above is `GetForEmployeeAsync`, so two callers can both see null and both take this
      // branch. **`UX_AttendanceLeaveBalances_Employee_Type_Year` decides it at commit**, and before that
      // index the loser received `Persistence.UniqueConstraint` unmapped — a 500.
      //
      // ⚠ **AND A SECOND ROW WAS NOT A REPORTING PROBLEM.** `LeaveBalance.Consume` guards with
      // `ConsumedQuantity + quantity > EntitlementQuantity` **against that row's own counter**, and the
      // repository reads with `FirstOrDefaultAsync`. Two rows meant the guard passed twice against two
      // different counters: **an employee could take double their entitlement and nothing reported it.**
      //
      // ---- RETRYABLE, AND THE REASON IS NOT MERELY "TRY AGAIN".
      //
      // **The losing operation's intent is satisfied by the winner's row.** A retry finds it and takes the
      // `SetEntitlement` branch above — which is exactly what this caller asked for. Setting an
      // entitlement twice concurrently CONVERGES.
      //
      // That is the mirror of the journal reversal, where a lost race is TERMINAL because the reversal
      // already exists and no retry can succeed. **Same 409, opposite correct client action** — which is
      // why the code matters and the status alone does not.
      //
      // ⚠ **THE INDEX EXISTS AND ALWAYS HAS — T-171/T-172 SAID OTHERWISE AND WERE WRONG (T-173).**
      //
      // `IX_AttendanceLeaveBalances_TenantId_EmployeeId_LeaveTypeId_PeriodYear` is unique and unfiltered,
      // shipped in `AddAttendanceFoundation` on 2026-08-25, and `AttendanceConfigurations` states its
      // reasoning in place. **The race was never open; the loser was simply getting a 500** because
      // `Persistence.UniqueConstraint` reached the mapper unmapped. This branch is what makes it a 409.
      //
      // ⚠ **AND THE INDEX KEY IS NARROWER THAN THE READ.** The index omits `CompanyId`; the read
      // (`GetForEmployeeAsync`) includes it. So the constraint is STRICTER than the lookup: one balance
      // per employee, type and year across ALL companies in the tenant. If an employee id can ever appear
      // under two companies, the second company's entitlement is refused by an index nobody would think
      // to look at from here.
      //
      // ---- SOUND ONLY WHILE THAT TABLE CARRIES EXACTLY ONE UNIQUE INDEX.
      //
      // This handler writes nothing else, so a unique violation here can only be that one — and naming it
      // becomes a guess the day a second unique index is added. The coupling is invisible from here,
      // which is why it is written here.
      if (saved.Error.Code == PersistenceErrorCodes.UniqueConstraint)
      {
        return Result.Failure<Guid>(LeaveErrors.DuplicateBalance);
      }

      return Result.Failure<Guid>(saved.Error);
    }

    return Result.Success(balance.Value.Id);
  }
}

// ================================================================================================
// SUBMISSION — WHERE THE WORKING-DAY COUNT IS COMPUTED AND FROZEN (REQ-ATT-0013, BR-ATT-0003).
// ================================================================================================
public sealed class SubmitLeaveRequestCommandHandler(
  ILeaveRequestRepository requests,
  ILeaveSubmissionLock submissionLock,
  ILeaveTypeRepository leaveTypes,
  IWorkingCalendarRepository calendars,
  IEmployeeRoster roster,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant)
{
  public async Task<Result<Guid>> HandleAsync(
    SubmitLeaveRequestCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    // `Attendance.Leave.Manage`, not a self-service permission. Under `OD-ATT-0013` this route is an
    // ADMINISTRATOR submitting on an employee's behalf. The mapping exists (`UserEmployeeLink`, T-082) but
    // no submission path reads it —
    // which is why `EmployeeId` is mandatory in the command rather than inferred from the caller.
    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageLeave, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var leaveType = await leaveTypes.GetByIdAsync(command.LeaveTypeId, cancellationToken);
    if (leaveType is null || leaveType.CompanyId != command.CompanyId)
    {
      return Result.Failure<Guid>(LeaveErrors.LeaveTypeNotFound);
    }

    if (!leaveType.IsActive)
    {
      return Result.Failure<Guid>(LeaveErrors.LeaveTypeInactive);
    }

    // The same employment-window reading as attendance (`BR-HR-0004` per `OD-PAY-0010`), reported with
    // leave's own error codes so the message names the act the caller attempted.
    var window = await CheckEmploymentAsync(command, cancellationToken);
    if (window.IsFailure)
    {
      return Result.Failure<Guid>(window.Error);
    }

    var calendar = await calendars.GetForCompanyAsync(command.CompanyId, cancellationToken);
    if (calendar is null)
    {
      return Result.Failure<Guid>(WorkingCalendarErrors.NoCalendarForCompany);
    }

    // ---- COMPUTED HERE, STORED ON THE REQUEST, NEVER RECOMPUTED.
    //
    // `AC-ATT-0019`: a holiday added after this moment must not change what this request consumed. The
    // figure is a fact about a decision, not a derivation from mutable configuration.
    var workingDays = calendar.WorkingDaysBetween(command.StartDate, command.EndDate);

    // ---- ⚠ THE TRANSACTION AND THE LOCK, BOTH OF WHICH THIS HANDLER LACKED (T-151).
    //
    // The check below and the insert that follows were a plain read-then-write: no transaction, READ
    // COMMITTED, and the shared lock released at statement end. **Two concurrent submissions both read, both
    // found nothing, and both committed** — a double-clicked button was sufficient, and the result is
    // double-counted unpaid absence on a payslip.
    //
    // **T-150's unique index catches only IDENTICAL ranges.** Overlap is a range predicate and no index can
    // express it (`DEC-L-084`), so 7th–11th against 9th–15th needed this.
    //
    // The lock is EMPLOYEE-scoped: two employees submitting at the same instant never contend. It is
    // transaction-owned, so a commit or rollback releases it and **it refuses outright if no transaction is
    // open** rather than granting something ineffective.
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    var locked = await submissionLock.AcquireAsync(
      currentTenant.TenantId ?? Guid.Empty, command.EmployeeId, cancellationToken);

    if (locked.IsFailure)
    {
      return Result.Failure<Guid>(locked.Error);
    }

    // Overlap against requests that actually booked days. Cancelled and rejected ones are excluded by the
    // repository because they booked nothing.
    var overlapping = await requests.GetOverlappingAsync(
      command.CompanyId, command.EmployeeId, command.StartDate, command.EndDate, cancellationToken);
    if (overlapping.Count > 0)
    {
      return Result.Failure<Guid>(LeaveErrors.RequestOverlaps);
    }

    var request = LeaveRequest.Submit(
      command.CompanyId, command.EmployeeId, command.LeaveTypeId,
      command.StartDate, command.EndDate, workingDays);
    if (request.IsFailure)
    {
      return Result.Failure<Guid>(request.Error);
    }

    await requests.AddAsync(request.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<Guid>(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);

    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(request.Value.Id);
  }

  private async Task<Result> CheckEmploymentAsync(
    SubmitLeaveRequestCommand command, CancellationToken cancellationToken)
  {
    var start = new DateTimeOffset(command.StartDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    var end = new DateTimeOffset(command.EndDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    var employment = await roster.GetEmploymentAsync(command.CompanyId, start, end, cancellationToken);
    var record = employment.FirstOrDefault(candidate => candidate.EmployeeId == command.EmployeeId);

    if (record is null)
    {
      return Result.Failure(LeaveErrors.EmployeeNotInCompany);
    }

    if (start < record.EmploymentDateUtc)
    {
      return Result.Failure(LeaveErrors.RequestBeforeEmployment);
    }

    // The whole range must fall inside employment, so the END is checked against termination — a request
    // straddling a termination date would otherwise book leave for days after the employee had left.
    if (record.TerminationDateUtc is { } terminated && end > terminated)
    {
      return Result.Failure(LeaveErrors.RequestAfterTermination);
    }

    return Result.Success();
  }
}

// ================================================================================================
// THE DECISION (REQ-ATT-0014, OD-ATT-0007) — ROUTING, THEN THE AGGREGATE'S OWN BAR.
// ================================================================================================
//
// Approve and reject share a handler shape and differ only in the aggregate method they call, because
// everything before the decision — routing, permission, the self-approval bar, balance handling — is
// identical. Rejection simply moves no balance.
public sealed class ApproveLeaveRequestCommandHandler(
  ILeaveRequestRepository requests,
  ILeaveTypeRepository leaveTypes,
  ILeaveBalanceRepository balances,
  ILeaveApprovalRouter router,
  IAttendanceScopeResolver scope,
  ICurrentUser currentUser,
  ICurrentTenantUser currentTenantUser,
  IUserEmployeeResolver userEmployees,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    DecideLeaveRequestCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = await requests.GetByIdAsync(command.LeaveRequestId, cancellationToken);
    if (request is null)
    {
      return Result.Failure(LeaveErrors.RequestNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ApproveLeave, request.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var route = await router.ResolveApproverAsync(request.CompanyId, request.EmployeeId, cancellationToken);
    if (route.IsFailure)
    {
      return Result.Failure(route.Error);
    }

    // The aggregate applies the self-approval bar again on the employee-identified path. Not redundant:
    // the router decides ROUTING, the aggregate holds the INVARIANT, and removing either leaves a hole.
    //
    // ---- AND THE ROOT PATH NOW CARRIES THE SAME INVARIANT (BR-ATT-0007, T-084).
    //
    // Resolution happens HERE because this is the only layer permitted to call a cross-module contract;
    // the comparison happens in the aggregate for the reason stated one line above. `null` is an ordinary
    // answer and not a refusal — see `LeaveRequest.GuardNotSelfAtRoot`.
    var acting = await ResolveActingEmployeeAsync(cancellationToken);

    var decided = route.Value.UsedRootFallback
      ? request.ApproveAtRoot(acting, currentUser.UserId, DateTimeOffset.UtcNow, command.DecisionNote)
      : request.Approve(route.Value.ApproverEmployeeId, currentUser.UserId, DateTimeOffset.UtcNow, command.DecisionNote);
    if (decided.IsFailure)
    {
      return decided;
    }

    // ---- THE BALANCE MOVES HERE, AND ONLY HERE (REQ-ATT-0015, AC-ATT-0018).
    //
    // `PaidWithoutBalance` types consume nothing — statutory entitlements a company grants without metering
    // — so a zero balance must not refuse them. `LeaveType.ConsumesBalance` is what expresses that, rather
    // than a check on the behaviour enum repeated at each site.
    var leaveType = await leaveTypes.GetByIdAsync(request.LeaveTypeId, cancellationToken);
    if (leaveType is null)
    {
      return Result.Failure(LeaveErrors.LeaveTypeNotFound);
    }

    if (leaveType.ConsumesBalance)
    {
      var balance = await balances.GetForEmployeeAsync(
        request.CompanyId, request.EmployeeId, request.LeaveTypeId, request.StartDate.Year, cancellationToken);

      // Null is a refusal, not an implicit zero. An implicit zero would report "insufficient balance" when
      // the truth is that nobody administered one — two different problems with two different remedies.
      if (balance is null)
      {
        return Result.Failure(LeaveErrors.BalanceNotFound);
      }

      var consumed = balance.Consume(request.WorkingDaysConsumed);
      if (consumed.IsFailure)
      {
        return consumed;
      }
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }

  // ---- THE ACTING USER'S EMPLOYEE, OR null (ADR-030 Decision 5).
  //
  // No tenant session means no linked employee by definition — the operator case the root fallback exists
  // for — so it is `null` rather than a refusal, and the aggregate treats it as "the bar does not apply"
  // rather than "the caller failed to identify themselves".
  // THE ONE PLACE THE ANSWER BECOMES AN ActingEmployee. A value is `Resolved`, an absence is `Unresolved`,
  // and no other site performs that translation — so "who decided this was unresolved" has exactly two
  // answers in this file and both are named.
  private async Task<ActingEmployee> ResolveActingEmployeeAsync(CancellationToken cancellationToken)
  {
    if (currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return ActingEmployee.Unresolved();
    }

    var employeeId = await userEmployees.ResolveEmployeeIdAsync(tenantUserId, cancellationToken);

    return employeeId is { } resolved ? ActingEmployee.Resolved(resolved) : ActingEmployee.Unresolved();
  }
}

public sealed class RejectLeaveRequestCommandHandler(
  ILeaveRequestRepository requests,
  ILeaveApprovalRouter router,
  IAttendanceScopeResolver scope,
  ICurrentUser currentUser,
  ICurrentTenantUser currentTenantUser,
  IUserEmployeeResolver userEmployees,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    DecideLeaveRequestCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = await requests.GetByIdAsync(command.LeaveRequestId, cancellationToken);
    if (request is null)
    {
      return Result.Failure(LeaveErrors.RequestNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ApproveLeave, request.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    // Rejection routes through the same chain as approval. Deciding NO is as much an exercise of approval
    // authority as deciding yes, and a rejection path that skipped routing would let anyone holding the
    // permission reject anyone's request regardless of the management chain.
    var route = await router.ResolveApproverAsync(request.CompanyId, request.EmployeeId, cancellationToken);
    if (route.IsFailure)
    {
      return Result.Failure(route.Error);
    }

    // The same bar as approve, on the same path, for the same reason: `RejectAtRoot` reached the root
    // fallback through the identical router branch and had the identical hole (T-084).
    var acting = await ResolveActingEmployeeAsync(cancellationToken);

    var decided = route.Value.UsedRootFallback
      ? request.RejectAtRoot(acting, currentUser.UserId, DateTimeOffset.UtcNow, command.DecisionNote)
      : request.Reject(route.Value.ApproverEmployeeId, currentUser.UserId, DateTimeOffset.UtcNow, command.DecisionNote);

    // No balance movement. Rejection never consumed anything, because the balance moves at APPROVAL.
    return decided.IsFailure ? decided : await unitOfWork.SaveChangesAsync(cancellationToken);
  }

  // ---- THE ACTING USER'S EMPLOYEE, OR null (ADR-030 Decision 5).
  //
  // No tenant session means no linked employee by definition — the operator case the root fallback exists
  // for — so it is `null` rather than a refusal, and the aggregate treats it as "the bar does not apply"
  // rather than "the caller failed to identify themselves".
  // THE ONE PLACE THE ANSWER BECOMES AN ActingEmployee. A value is `Resolved`, an absence is `Unresolved`,
  // and no other site performs that translation — so "who decided this was unresolved" has exactly two
  // answers in this file and both are named.
  private async Task<ActingEmployee> ResolveActingEmployeeAsync(CancellationToken cancellationToken)
  {
    if (currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return ActingEmployee.Unresolved();
    }

    var employeeId = await userEmployees.ResolveEmployeeIdAsync(tenantUserId, cancellationToken);

    return employeeId is { } resolved ? ActingEmployee.Resolved(resolved) : ActingEmployee.Unresolved();
  }
}

public sealed class CancelLeaveRequestCommandHandler(
  ILeaveRequestRepository requests,
  ILeaveTypeRepository leaveTypes,
  ILeaveBalanceRepository balances,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
{
  public async Task<Result> HandleAsync(
    CancelLeaveRequestCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    var request = await requests.GetByIdAsync(command.LeaveRequestId, cancellationToken);
    if (request is null)
    {
      return Result.Failure(LeaveErrors.RequestNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      AttendancePermissionNames.ManageLeave, request.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var wasApproved = request.Status == LeaveRequestStatus.Approved;

    // The aggregate refuses once the dates have started: by then the absence is a fact that happened, and
    // cancelling it is a CORRECTION that routes through `OD-ATT-0012`'s adjustment path.
    var cancelled = request.Cancel(DateOnly.FromDateTime(DateTime.UtcNow));
    if (cancelled.IsFailure)
    {
      return cancelled;
    }

    // Only an APPROVED request had consumed anything. Cancelling a submitted one releases nothing, because
    // submission reserves nothing — the consequence of `OD-ATT-0006` putting the movement at approval.
    if (wasApproved)
    {
      // ---- ⚠ A NULL HERE IS REFUSED, NOT SKIPPED, AND THE DIFFERENCE IS LEAVE DAYS (T-189).
      //
      // Both lookups used to be `is not null` guards that fell through in silence. **Falling through means
      // the days consumed at approval are never returned and the caller is told the cancel SUCCEEDED** —
      // no error, no log, and a balance that is quietly wrong from then on.
      //
      // Approval already refuses on exactly these two nulls, with exactly these two errors. Mirroring it is
      // what makes consume and release the same shape rather than one strict and one forgiving.
      //
      // ---- NEITHER NULL IS REACHABLE TODAY, AND THAT IS WHY THE REFUSAL IS WORTH ITS LINES.
      //
      // The argument spans five files, so it is written once here rather than rediscovered:
      //
      //   1. `wasApproved` is true, and approval REFUSES both nulls — so both existed at approval.
      //   2. The key is IDENTICAL at both sites: company, employee, leave type, `StartDate.Year`. And
      //      `LeaveTypeId`, `StartDate` and `WorkingDaysConsumed` are assigned only in the constructor;
      //      every mutator (`Approve`, `Reject`, `Cancel`, the `*AtRoot` pair) moves STATUS alone.
      //   3. Nothing deletes either entity — neither repository interface has a remove, and no
      //      `Remove`/`RemoveRange` touches these sets anywhere in `src/`.
      //   4. Neither lookup filters on `IsActive`, so `SetActivation(false)` does not hide a type.
      //   5. `ConsumesBalance` is computed from `Behaviour`, which `Create` sets and no mutator changes.
      //      **A flip there would be the silent path**: consumed as metered, cancelled as unmetered.
      //
      // The only global query filter is on `TenantId`, identical across both operations.
      //
      // Every link is something a later change could break — a delete method, a `Behaviour` setter, an
      // `IsActive` predicate added to a lookup. **The refusal is what turns any of those from silently
      // losing leave days into a visible failure**, which is the whole reason it replaces a skip.
      var leaveType = await leaveTypes.GetByIdAsync(request.LeaveTypeId, cancellationToken);
      if (leaveType is null)
      {
        return Result.Failure(LeaveErrors.LeaveTypeNotFound);
      }

      if (leaveType.ConsumesBalance)
      {
        var balance = await balances.GetForEmployeeAsync(
          request.CompanyId, request.EmployeeId, request.LeaveTypeId, request.StartDate.Year, cancellationToken);
        if (balance is null)
        {
          return Result.Failure(LeaveErrors.BalanceNotFound);
        }

        var released = balance.Release(request.WorkingDaysConsumed);
        if (released.IsFailure)
        {
          return released;
        }
      }
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

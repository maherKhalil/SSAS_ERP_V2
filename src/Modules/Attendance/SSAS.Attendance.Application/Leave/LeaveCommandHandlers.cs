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
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(leaveType.Value.Id);
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
    return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(balance.Value.Id);
  }
}

// ================================================================================================
// SUBMISSION — WHERE THE WORKING-DAY COUNT IS COMPUTED AND FROZEN (REQ-ATT-0013, BR-ATT-0003).
// ================================================================================================
public sealed class SubmitLeaveRequestCommandHandler(
  ILeaveRequestRepository requests,
  ILeaveTypeRepository leaveTypes,
  IWorkingCalendarRepository calendars,
  IEmployeeRoster roster,
  IAttendanceScopeResolver scope,
  ITenantUnitOfWork unitOfWork)
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
      var leaveType = await leaveTypes.GetByIdAsync(request.LeaveTypeId, cancellationToken);
      if (leaveType is not null && leaveType.ConsumesBalance)
      {
        var balance = await balances.GetForEmployeeAsync(
          request.CompanyId, request.EmployeeId, request.LeaveTypeId, request.StartDate.Year, cancellationToken);
        if (balance is not null)
        {
          var released = balance.Release(request.WorkingDaysConsumed);
          if (released.IsFailure)
          {
            return released;
          }
        }
      }
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

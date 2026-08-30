using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.API;

// ================================================================================================
// ATTENDANCE'S DOMAIN ERRORS ON THE WIRE.
// ================================================================================================
//
// Every refusal a caller can provoke has a stable `attendance.*` code they can branch on, and a status that
// says what kind of problem it is. The mapping is **EXHAUSTIVE BY CONSTRUCTION**: the default arm is
// `ApiErrors.WriteFailure` (500), so an error added to the domain without a line here surfaces loudly and is
// caught by the mapper-arm tests rather than shipped as a plausible 400.
//
// ---- ITS OWN MAPPER, NOT A SHARED ONE, AND THAT IS A RECORDED SCAR.
//
// `DepartmentApiErrorMapper` carries the reason in its header: reusing `EmployeeApiErrorMapper` produced a
// defect where **a department manager error surfaced under an employee code.** Attendance gets its own.
//
// ---- ERRORS ABOUT THE EMPLOYEE ARE NOT ERRORS ABOUT THE RECORD.
//
// `Attendance.RecordAfterTermination` describes **the employee named in the body**, not the record addressed
// in the route. That is exactly the miscoding the department mapper was written to fix, and this module has
// four errors of that shape — the two employment-window refusals on each of attendance and leave. They keep
// their own codes rather than being folded into a generic `request_invalid`, so a client can tell "you
// mis-keyed the employee" from "that date is outside their employment".
//
// ---- AN OUT-OF-SCOPE RECORD IS A 404.
//
// `NotFound` covers both "no such record" and "a record you may not reach". Reporting the second as 403
// would let a caller enumerate whose attendance exists — and under `OD-ATT-0011` the branch boundary is
// precisely what stops a supervisor learning about another branch's people, so a distinguishable 403 would
// hand back what the boundary took away.
public static class AttendanceApiErrorMapper
{
  public static readonly ApiError NotFound = new(404, "attendance.not_found");

  // FP-015 (T-089), Payroll's precedent exactly. 404 because the route exists, the caller is authenticated
  // and permitted, and what is absent is the SUBJECT of the read. A distinct code from `attendance.not_found`,
  // which answers about a thing the caller named; this one answers about the caller.
  public static readonly ApiError NoLinkedEmployee = new(404, "attendance.no_linked_employee");
  public static readonly ApiError Conflict = new(409, "attendance.conflict");

  // ============================================================================================
  // ⚠ THE UNCLASSIFIED UNIQUE VIOLATION, WHICH USED TO BE A 500 (T-244).
  // ============================================================================================
  //
  // `Persistence.UniqueConstraint` is raised by the unit of work when SQL Server refuses a write with
  // 2601 or 2627. **It is not a domain error of this module**, so the exhaustiveness argument in this
  // file's header never covered it: the mapper-arm tests check that this module's own errors are mapped,
  // and a Platform persistence code arriving from below is outside what they look at. It fell to the
  // default arm and answered **500 `request.failed`** for a plain business conflict.
  //
  // Measured before the fix: two callers adding the same holiday, the loser got a 500.
  //
  // ---- ⚠ A DISTINCT CODE, NOT THIS MODULE'S GENERIC `Conflict`, AND THAT IS THE POINT.
  //
  // Handlers that anticipate a specific race translate the code themselves — `WorkingCalendarErrors
  // .DuplicateName`, `LeaveBalance` entitlement convergence — and those tell a caller WHICH constraint
  // lost and whether a retry can succeed. **This arm is the floor for the paths nobody classified**, and
  // giving it its own code means an operator can see how often an unclassified one fires. Folding it into
  // `Conflict` would hide exactly the signal that says another handler needs a translation.
  //
  // ---- WHAT THIS CODE DOES NOT PROMISE.
  //
  // A 409 usually implies the caller can do something about it. **On a path where a unique violation
  // means an internal invariant broke, that is not true**, and this arm cannot tell the difference — it
  // has only the code, not the index. That is the reason it is a floor rather than a replacement: the
  // right fix for any specific path is still a handler-level translation naming its constraint.
  public static readonly ApiError UniqueConflict = new(409, "attendance.unique_conflict");
  public static readonly ApiError PeriodClosed = new(409, "attendance.period_closed");
  public static readonly ApiError PeriodStateInvalid = new(409, "attendance.period_state_invalid");
  public static readonly ApiError EmploymentWindow = new(409, "attendance.employment_window");
  public static readonly ApiError LeaveStateInvalid = new(409, "attendance.leave_state_invalid");
  public static readonly ApiError BalanceInsufficient = new(409, "attendance.balance_insufficient");
  // ---- ⚠ ANOTHER SUBMISSION FOR THE SAME EMPLOYEE HOLDS THE LOCK, AND THIS ANSWERED 500 (T-197).
  //
  // `Attendance.LeaveSubmissionBusy` had NO ARM in the table below, so it fell to `ApiErrors.WriteFailure`
  // — **500 `request.failed`** — for a condition that is neither a failure nor the caller's fault.
  //
  // **A double-clicked submit button is sufficient to reach it.** That is the same observation that put
  // overlapping leave on the owner's decision list: the lock closed the DATA defect and left the ANSWER
  // wrong, so the employee saw a server error for a request they should simply retry.
  //
  // Distinguishable rather than folded into `Conflict`, following `department.hierarchy_busy` exactly: every
  // other 409 here means CHANGE THE REQUEST, and this one means SEND IT AGAIN. Collapsing them would tell an
  // employee to alter a leave request that was correct.
  public static readonly ApiError SubmissionBusy = new(409, "attendance.leave_submission_busy");

  public static readonly ApiError ApprovalDenied = new(403, "attendance.approval_denied");
  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");
  public static readonly ApiError BranchScopeDenied = new(403, "branch.scope_denied");
  public static readonly ApiError CalendarMissing = new(422, "attendance.calendar_missing");

  // ⚠ THE DOMAIN MESSAGE IS ATTACHED HERE BECAUSE THIS IS THE LAST PLACE IT EXISTS (T-261).
  //
  // Ninety-six call sites hand an already-mapped `ApiError` straight to `ApiProblems.Problem` and never
  // see the original `Error`. Attaching the message to the result is one edit per mapper; passing it
  // alongside would have been ninety-six.
  //
  // `ApiError.ShowsDetail` decides whether it reaches the caller: an authorization refusal (401/403)
  // drops it unless that code opted in, because `branch.scope_denied` has nine different messages behind
  // it and showing them would separate a branch that does not exist from one that is forbidden.
  public static ApiError Map(Error error) =>
    MapCore(error).Explaining(error.Message);

  private static ApiError MapCore(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- MALFORMED INPUT. The caller can fix these by sending something else.
      "Attendance.WorkingCalendarCompanyRequired" => ApiErrors.RequestInvalid,
      "Attendance.WorkingCalendarNameInvalid" => ApiErrors.RequestInvalid,
      "Attendance.WeekendPatternInvalid" => ApiErrors.RequestInvalid,
      "Attendance.WeekendPatternCoversEveryDay" => ApiErrors.RequestInvalid,
      "Attendance.HolidayNameInvalid" => ApiErrors.RequestInvalid,
      "Attendance.PeriodCompanyRequired" => ApiErrors.RequestInvalid,
      "Attendance.PeriodNameInvalid" => ApiErrors.RequestInvalid,
      "Attendance.PeriodRangeInvalid" => ApiErrors.RequestInvalid,
      "Attendance.RecordCompanyRequired" => ApiErrors.RequestInvalid,
      "Attendance.RecordPeriodRequired" => ApiErrors.RequestInvalid,
      "Attendance.RecordEmployeeRequired" => ApiErrors.RequestInvalid,
      "Attendance.RecordQuantityNegative" => ApiErrors.RequestInvalid,
      "Attendance.OvertimeTierInvalid" => ApiErrors.RequestInvalid,
      "Attendance.OvertimeTierRequired" => ApiErrors.RequestInvalid,
      "Attendance.RecordNoteInvalid" => ApiErrors.RequestInvalid,
      "Attendance.AdjustedRecordRequired" => ApiErrors.RequestInvalid,
      "Attendance.AdjustmentNoteRequired" => ApiErrors.RequestInvalid,
      "Attendance.AdjustmentChangesNothing" => ApiErrors.RequestInvalid,
      "Attendance.LeaveCompanyRequired" => ApiErrors.RequestInvalid,
      "Attendance.LeaveTypeCodeInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveTypeNameInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveBehaviourInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveBalanceSubjectRequired" => ApiErrors.RequestInvalid,
      "Attendance.LeaveBalanceYearInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveEntitlementNegative" => ApiErrors.RequestInvalid,
      "Attendance.LeaveConsumptionInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveRequestSubjectRequired" => ApiErrors.RequestInvalid,
      "Attendance.LeaveRequestRangeInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveRequestNoWorkingDay" => ApiErrors.RequestInvalid,
      "Attendance.LeaveDecisionActorInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveDecisionNoteInvalid" => ApiErrors.RequestInvalid,
      "Attendance.LeaveApproverRequired" => ApiErrors.RequestInvalid,

      // ---- ABSENT, OR NOT REACHABLE. Deliberately indistinguishable.
      "Attendance.WorkingCalendarNotFound" => NotFound,
      "Attendance.HolidayNotFound" => NotFound,
      "Attendance.PeriodNotFound" => NotFound,
      "Attendance.RecordNotFound" => NotFound,
      "Attendance.LeaveTypeNotFound" => NotFound,
      "Attendance.LeaveBalanceNotFound" => NotFound,
      "Attendance.LeaveRequestNotFound" => NotFound,
      "Attendance.LeaveSubmissionBusy" => SubmissionBusy,

      // ---- STATE CONFLICTS. The caller is not wrong; the world is not ready.
      "Attendance.WorkingCalendarNameConflict" => Conflict,
      "Attendance.HolidayDateConflict" => Conflict,
      "Attendance.PeriodOverlaps" => Conflict,
      "Attendance.LeaveTypeCodeConflict" => Conflict,
      "Attendance.LeaveBalanceConflict" => Conflict,
      "Attendance.LeaveRequestOverlaps" => Conflict,
      "Attendance.AdjustedRecordMismatch" => Conflict,
      "Attendance.LeaveTypeAlreadyActive" => Conflict,
      "Attendance.LeaveTypeAlreadyInactive" => Conflict,
      "Attendance.LeaveTypeInactive" => Conflict,

      // ---- PERIOD LIFECYCLE. Separate codes because the remedies differ: a closed period wants an
      // adjustment, an already-closed one wants nothing, and a missing open one wants somebody to create it.
      "Attendance.PeriodClosed" => PeriodClosed,
      "Attendance.NoOpenPeriod" => PeriodClosed,
      "Attendance.PeriodAlreadyClosed" => PeriodStateInvalid,
      "Attendance.PeriodAlreadyOpen" => PeriodStateInvalid,

      // ---- THE EMPLOYMENT WINDOW. Facts about THE EMPLOYEE, arriving on a route addressed to a record.
      "Attendance.RecordBeforeEmployment" => EmploymentWindow,
      "Attendance.RecordAfterTermination" => EmploymentWindow,
      "Attendance.RecordEmployeeNotInCompany" => EmploymentWindow,
      "Attendance.LeaveRequestBeforeEmployment" => EmploymentWindow,
      "Attendance.LeaveRequestAfterTermination" => EmploymentWindow,
      "Attendance.LeaveEmployeeNotInCompany" => EmploymentWindow,

      // ---- LEAVE LIFECYCLE AND APPROVAL.
      "Attendance.LeaveRequestAlreadyDecided" => LeaveStateInvalid,
      "Attendance.LeaveRequestAlreadyCancelled" => LeaveStateInvalid,
      "Attendance.LeaveRequestRejectedNotCancellable" => LeaveStateInvalid,
      "Attendance.LeaveRequestAlreadyStarted" => LeaveStateInvalid,
      "Attendance.LeaveBalanceInsufficient" => BalanceInsufficient,
      "Attendance.LeaveReleaseExceedsConsumption" => Conflict,

      // 403 rather than 409: both are statements about the CALLER's authority, not about the request.
      "Attendance.LeaveSelfApprovalBarred" => ApprovalDenied,
      "Attendance.LeaveNoApproverInChain" => ApprovalDenied,

      // ---- SCOPE. The two are distinguishable so an operator can tell which grant is missing — company
      // access and branch access are administered separately, and "denied" alone sends them hunting.
      "Attendance.CompanyScopeDenied" => CompanyScopeDenied,
      "Attendance.BranchScopeDenied" => BranchScopeDenied,
      "Attendance.ReadPermissionDenied" => ApiErrors.Forbidden,
      "Attendance.WritePermissionDenied" => ApiErrors.Forbidden,
      "Attendance.NoLinkedEmployee" => NoLinkedEmployee,
      "Attendance.InvalidActor" => ApiErrors.Forbidden,

      // 422 rather than 409: the request is well-formed and the state is not conflicting — the company is
      // simply not configured yet, and the remedy is an administrative act rather than a retry.
      "Attendance.WorkingCalendarMissing" => CalendarMissing,

      // See `UniqueConflict` above: the floor for a unique violation nobody classified. Better than the
      // 500 it replaces, and worse than a handler translation that names the constraint.
      "Persistence.UniqueConstraint" => UniqueConflict,
      _ => ApiErrors.WriteFailure
    };
  }
}

using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Leave;

public static class LeaveErrors
{
  // The caller is a tenant user with no linked employee (`ADR-030` Decision 5, FP-015). An ordinary state
  // rather than a fault — platform-support staff, and users created before their employee record exists.
  //
  // Mapped to `404 attendance.no_linked_employee`: a distinct per-module code, because the caller sees an
  // answer about THEMSELVES. Collapsing it into an existing not-found would tell them their records were
  // not found when the truth is that nobody linked their employee record.
  public static readonly Error NoLinkedEmployee = new(
    "Attendance.NoLinkedEmployee",
    "No employee record is linked to this user.");

  public static readonly Error CompanyRequired = new(
    "Attendance.LeaveCompanyRequired",
    "Leave records must belong to a company.",
    Field: "companyId");

  // ---- LEAVE TYPES.
  public static readonly Error InvalidLeaveTypeCode = new(
    "Attendance.LeaveTypeCodeInvalid",
    "A leave type code is required and must be at most 32 characters.",
    Field: "code");

  public static readonly Error InvalidLeaveTypeName = new(
    "Attendance.LeaveTypeNameInvalid",
    "A leave type name is required and must be at most 200 characters.",
    Field: "name");

  public static readonly Error InvalidLeaveBehaviour = new(
    "Attendance.LeaveBehaviourInvalid",
    "The leave behaviour is not one this product implements.",
    Field: "behaviour");

  public static readonly Error DuplicateLeaveTypeCode = new(
    "Attendance.LeaveTypeCodeConflict",
    "A leave type with this code already exists in this company.");

  public static readonly Error LeaveTypeNotFound = new(
    "Attendance.LeaveTypeNotFound",
    "The leave type does not exist.");

  public static readonly Error LeaveTypeInactive = new(
    "Attendance.LeaveTypeInactive",
    "The leave type is deactivated and cannot be named on a new request.");

  public static readonly Error LeaveTypeAlreadyActive = new(
    "Attendance.LeaveTypeAlreadyActive",
    "The leave type is already active.");

  public static readonly Error LeaveTypeAlreadyInactive = new(
    "Attendance.LeaveTypeAlreadyInactive",
    "The leave type is already deactivated.");

  // ---- BALANCES.
  public static readonly Error BalanceSubjectRequired = new(
    "Attendance.LeaveBalanceSubjectRequired",
    "A leave balance must name an employee and a leave type.");

  public static readonly Error InvalidPeriodYear = new(
    "Attendance.LeaveBalanceYearInvalid",
    "A leave balance year must be a four-digit calendar year.",
    Field: "periodYear");

  public static readonly Error NegativeEntitlement = new(
    "Attendance.LeaveEntitlementNegative",
    "A leave entitlement cannot be negative.",
    Field: "entitlementQuantity");

  public static readonly Error DuplicateBalance = new(
    "Attendance.LeaveBalanceConflict",
    "A leave balance already exists for this employee, leave type and year.");

  public static readonly Error BalanceNotFound = new(
    "Attendance.LeaveBalanceNotFound",
    "No leave balance exists for this employee, leave type and year.");

  public static readonly Error InsufficientBalance = new(
    "Attendance.LeaveBalanceInsufficient",
    "The leave balance does not cover this request.");

  public static readonly Error InvalidConsumption = new(
    "Attendance.LeaveConsumptionInvalid",
    "A leave balance movement must be a positive quantity.");

  public static readonly Error ReleaseExceedsConsumption = new(
    "Attendance.LeaveReleaseExceedsConsumption",
    "Cannot release more leave than the balance has consumed.");

  // ---- REQUESTS.
  public static readonly Error RequestSubjectRequired = new(
    "Attendance.LeaveRequestSubjectRequired",
    "A leave request must name an employee and a leave type.");

  public static readonly Error InvalidRequestRange = new(
    "Attendance.LeaveRequestRangeInvalid",
    "A leave request cannot end before it starts.");

  public static readonly Error RequestContainsNoWorkingDay = new(
    "Attendance.LeaveRequestNoWorkingDay",
    "The requested range contains no working day.");

  // Another submission for this employee holds the submission lock, or the caller reached the lock without
  // an open transaction. **A retry is the remedy for the first and a bug report for the second** — and the
  // caller cannot tell them apart, which is why both refuse rather than proceeding on an unheld lock.
  public static readonly Error SubmissionBusy = new(
    "Attendance.LeaveSubmissionBusy",
    "Another leave request for this employee is being submitted. Try again.");

  public static readonly Error RequestOverlaps = new(
    "Attendance.LeaveRequestOverlaps",
    "The employee already has a submitted or approved leave request covering these dates.");

  public static readonly Error RequestNotFound = new(
    "Attendance.LeaveRequestNotFound",
    "The leave request does not exist.");

  public static readonly Error RequestAlreadyDecided = new(
    "Attendance.LeaveRequestAlreadyDecided",
    "The leave request has already been decided.");

  public static readonly Error RequestAlreadyCancelled = new(
    "Attendance.LeaveRequestAlreadyCancelled",
    "The leave request is already cancelled.");

  public static readonly Error RejectedRequestNotCancellable = new(
    "Attendance.LeaveRequestRejectedNotCancellable",
    "A rejected leave request cannot be cancelled.");

  // The dates have passed, so the absence is a fact rather than a plan. The remedy is an attendance
  // adjustment under `OD-ATT-0012`, not a cancellation that would reverse a balance for days actually taken.
  public static readonly Error RequestAlreadyStarted = new(
    "Attendance.LeaveRequestAlreadyStarted",
    "The leave has already started; record a correction as an attendance adjustment instead.");

  public static readonly Error InvalidActor = new(
    "Attendance.LeaveDecisionActorInvalid",
    "A leave decision must record who made it.");

  public static readonly Error InvalidDecisionNote = new(
    "Attendance.LeaveDecisionNoteInvalid",
    "A leave decision note must be at most 1000 characters and cannot contain control characters.",
    Field: "decisionNote");

  // ---- APPROVAL ROUTING (OD-ATT-0007).
  public static readonly Error ApproverRequired = new(
    "Attendance.LeaveApproverRequired",
    "A leave decision must name the approver whose authority was used.");

  public static readonly Error SelfApprovalBarred = new(
    "Attendance.LeaveSelfApprovalBarred",
    "An employee cannot decide their own leave request.");

  // Every department in the chain above the requester is either unmanaged, managed by the requester, or
  // managed by a terminated employee — all three are reachable states in HR today. The ruling's fallback is
  // a permission holder at the root, and this is the refusal a caller without that permission meets.
  public static readonly Error NoApproverInChain = new(
    "Attendance.LeaveNoApproverInChain",
    "No department manager above this employee can approve the request; the root fallback permission is required.");

  public static readonly Error EmployeeNotInCompany = new(
    "Attendance.LeaveEmployeeNotInCompany",
    "The employee does not belong to this company.");

  public static readonly Error RequestBeforeEmployment = new(
    "Attendance.LeaveRequestBeforeEmployment",
    "Leave cannot be requested for dates before the employee's employment date.");

  public static readonly Error RequestAfterTermination = new(
    "Attendance.LeaveRequestAfterTermination",
    "Leave cannot be requested for dates after the employee's termination date.");
}

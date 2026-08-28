namespace SSAS.Attendance.Application.Permissions;

// ================================================================================================
// ATTENDANCE'S PERMISSION SURFACE (OD-ATT-0013, and the grammar <Plane>.<Resource>.<Action>).
// ================================================================================================
//
// ---- THE VERB GRANULARITY IS A CHOICE, NOT A DEFAULT, BECAUSE THE TWO EXISTING MODULES DISAGREE.
//
// HR is granular and per-act: `HR.Employees.Create`, `.Update`, `.Terminate`, `.Transfer`, `.Import`.
// Payroll is coarse with the sensitive acts split out: `View` / `Manage`, plus `Approve` and `Post`.
//
// Attendance follows **Payroll's** shape. It is closer to Payroll in character — a periodic operational
// cycle with a close and a downstream consumer — than to HR's master-data maintenance. Splitting the
// difference by accident is how a permission set ends up with no explicable rule.
public static class AttendancePermissionNames
{
  // ---- THE CALENDAR. Structural configuration, not personal data.
  public const string ViewCalendars = "Attendance.Calendars.View";
  public const string ManageCalendars = "Attendance.Calendars.Manage";

  // ---- RECORDS. Personal: where someone was and when they were absent.
  public const string ViewRecords = "Attendance.Records.View";
  public const string ManageRecords = "Attendance.Records.Manage";

  public const string ViewPeriods = "Attendance.Periods.View";
  public const string ManagePeriods = "Attendance.Periods.Manage";

  // ---- THE SENSITIVE ACT (BR-PLT-0103's shape, OD-PAY-0009's reasoning).
  //
  // `OD-PAY-0009` placed payroll's sensitivity at APPROVAL rather than calculation, because calculation
  // commits nothing while approval is the assertion *these are the amounts these people will be paid*.
  //
  // Closing an attendance period is the analogous act: **it is the moment the numbers Payroll will consume
  // stop moving.** Under `OD-ATT-0010` it is also the gate payroll calculation passes through, so it is the
  // same kind of act as `Payroll.Runs.Approve` and gets its own grant rather than riding on `ManagePeriods`.
  //
  // Reopen is deliberately gated by THIS permission and not by `ManagePeriods`, because reopening is
  // unclosing: whoever may freeze the numbers is whoever may unfreeze them.
  public const string ClosePeriods = "Attendance.Periods.Close";

  // ---- LEAVE.
  public const string ViewLeaveTypes = "Attendance.LeaveTypes.View";
  public const string ManageLeaveTypes = "Attendance.LeaveTypes.Manage";

  public const string ViewLeave = "Attendance.Leave.View";
  public const string ManageLeave = "Attendance.Leave.Manage";

  // Separate from `ManageLeave` on the `GL.Drafts.Manage` / `GL.Journals.Post` precedent, so preparing a
  // request and authorizing it can be different people. The self-approval bar is enforced in the AGGREGATE
  // as well (`BR-ATT-0007`), because a permission cannot express "may this person approve THIS request".
  public const string ApproveLeave = "Attendance.Leave.Approve";

  // ---- THE ROOT FALLBACK (OD-ATT-0007).
  //
  // The ruling: department-manager approval, parent-chain escalation, **permission-holder fallback at the
  // root**. This is that fallback made grantable.
  //
  // It exists because the chain has reachable holes — `ManagerNotAssigned` and `ManagerTerminated` are both
  // modelled errors in HR today, and a root department has no parent to escalate to. Without this grant a
  // request from an unmanaged root department could never be decided by anyone.
  //
  // Separate from `ApproveLeave` because it is strictly more authority: it approves for employees whose
  // management chain does not reach the holder at all.
  public const string ApproveLeaveAtRoot = "Attendance.Leave.ApproveAtRoot";

  // ---- THE SENSITIVITY SPLIT (REQ-ATT-0025, OD-ATT-0013(3)).
  //
  // Seeing *"absent 3 days"* and seeing *"sick leave, 3 days"* are different disclosures. The second is
  // health information about an identified person.
  //
  // Deliberately NOT folded into `ViewLeave`, on exactly the `ViewPayslips` precedent: a run's existence,
  // status and totals are operational, but the lines beneath them are an individual's pay. Leave OCCURRENCE
  // is operational — a scheduler needs to know somebody is away. Leave TYPE is not.
  //
  // Which types are sensitive is per-type configuration (`LeaveType.IsSensitive`), because that judgement is
  // a company's and not the product's.
  public const string ViewSensitiveLeave = "Attendance.Leave.ViewSensitive";

  // ================================================================================================
  // SELF-SERVICE (FP-015, T-089). TWO PERMISSIONS, AND THE SECOND ONE IS THE POINT.
  // ================================================================================================
  //
  // **The self plane inherits the administrative plane's divisions.** `ViewRecords` and `ViewLeave` are
  // separate above for a reason — a timesheet and a leave history disclose different things about a person
  // — and **a single self permission would grant leave visibility to everyone granted timesheet
  // visibility.**
  //
  // A coarser self permission is a WIDENING wearing the costume of a simplification, and it is the failure
  // FP-015 was written to prevent: this package's own drafts wrote one permission, and the split was found
  // by reading the administrative constants rather than the drafts.
  //
  // `TS-SS-0013` asserts the consequence directly: records-self does NOT grant leave-self.
  public const string ViewOwnRecords = "Attendance.Records.ViewOwn";

  public const string ViewOwnLeave = "Attendance.Leave.ViewOwn";

  // ================================================================================================
  // WHAT IS DELIBERATELY ABSENT, AND IT IS NOT AN OVERSIGHT.
  // ================================================================================================
  //
  // **There is no `ViewOwn` of any kind.** No `Attendance.Records.ViewOwn`, no
  // `Attendance.Leave.RequestOwn`.
  //
  // `OD-ATT-0013` deferred self-service because it depended on a mapping from the authenticated identity to
  // an employee record. **That mapping exists** — `UserEmployeeLink` (`ADR-030`, T-082).
  //
  // **So what keeps the absence true is no longer a missing input; it is that FP-015's permission and
  // endpoint have not been built.** The absence is asserted rather than merely intended by
  // **`AC-ATT-0032`** — which is the one thing that fails the day a `ViewOwn` is added here. The criterion
  // is the handle rather than the guard's method name: grep it and both this file and the guard answer,
  // because the guard cites it too (T-087). Cited rather than restated,
  // because restating it in a third file is exactly how this sentence went stale in nine at once.
  //
  // `OD-PAY-0016` deferred payroll self-service for the same reason, and `PayrollPermissionNames` records
  // the refusal in these words:
  //
  //   > Adding a `Payroll.Payslips.ViewOwn` on an unverified assumption is exactly the shape of the FP-011
  //   > near-miss.
  //
  // **A permission whose subject cannot be resolved must not be declared.** This is the third consecutive
  // feature to meet the same missing input, which is where a coincidence becomes a recorded future package.
  //
  // **And there is no permission for the summary contract.** It is consumed in-process by Payroll, whose own
  // `Payroll.Runs.Manage` already gates the calculation that reads it. A second permission would mean a
  // payroll operator needed an Attendance grant to run payroll, turning a module boundary into an
  // administrative one — `IEmployeeRoster` set that precedent and it is followed without argument.
  //
  // **And no approval-delegation permission.** Delegation — approve on behalf of while the manager is away —
  // is real in leave management and is not modelled, because `OD-ATT-0007` established the ordinary approver
  // and said nothing about standing in for one. Named here as an absence so it is not mistaken for a gap.
}

using SSAS.BuildingBlocks.Tenancy.Permissions;

namespace SSAS.Attendance.Application.Permissions;

// ATTENDANCE'S PERMISSION DEFINITIONS (ADR-012 r1.2, FP-006P).
//
// A role may only be granted a permission the COMPOSED catalog defines. A module the Host does not register
// contributes nothing here, and its endpoints then refuse every caller — a loud, reviewable omission rather
// than a silent one.
//
// The descriptions are written for the person GRANTING the permission, not for a developer reading code:
// they say what the holder can do and, where it matters, what they still cannot. On this surface the second
// half earns its keep twice — once for the branch split, and once for leave type, which can disclose health
// information.
public sealed class AttendancePermissionCatalogContributor : IPermissionCatalogContributor
{
  private static readonly ModulePermissionDefinition[] Definitions =
  [
    new(AttendancePermissionNames.ViewCalendars,
      "View the company's working calendar: which days are weekends and which dates are holidays. " +
      "Structural configuration, not anyone's personal data"),
    new(AttendancePermissionNames.ManageCalendars,
      "Define the working calendar and maintain its holiday list. Changes how many working days every " +
      "future leave request consumes; it does NOT change requests already decided"),

    new(AttendancePermissionNames.ViewRecords,
      "View attendance records for employees at the caller's authorized BRANCHES, within their authorized " +
      "companies. This is personal data: it shows who was present and who was absent, and on which days"),
    new(AttendancePermissionNames.ManageRecords,
      "Record attendance and record corrections as adjustments. A closed period is never edited: a " +
      "correction is a new adjustment record, and the original remains"),

    new(AttendancePermissionNames.ViewPeriods,
      "View attendance periods and whether each is open or closed"),
    new(AttendancePermissionNames.ManagePeriods,
      "Create attendance periods. Does NOT permit closing one"),

    new(AttendancePermissionNames.ClosePeriods,
      "Close an attendance period, and reopen a closed one. Closing is the moment the numbers Payroll will " +
      "consume stop moving, and payroll calculation is refused against an open period. Reopening cannot " +
      "change any existing record — records are append-only — it only permits further records to arrive"),

    new(AttendancePermissionNames.ViewLeaveTypes,
      "View the company's leave type catalog"),
    new(AttendancePermissionNames.ManageLeaveTypes,
      "Define leave types and mark which of them are sensitive. A type is deactivated, never deleted, so " +
      "leave already taken against it stays readable"),

    new(AttendancePermissionNames.ViewLeave,
      "View leave requests and balances within the caller's authorized companies. Shows that an employee " +
      "was or will be away; it does NOT reveal which TYPE of leave unless the sensitive-leave permission " +
      "is also held"),
    new(AttendancePermissionNames.ManageLeave,
      "Submit leave requests on an employee's behalf, maintain administered balances, and cancel a request " +
      "before it starts. Does NOT permit deciding a request"),

    new(AttendancePermissionNames.ApproveLeave,
      "Approve or reject a leave request for employees whose department-manager chain reaches the holder. " +
      "Nobody can decide their own request, whatever permissions they hold"),
    new(AttendancePermissionNames.ApproveLeaveAtRoot,
      "Decide a leave request when no department manager above the employee can — because the department " +
      "has no manager, because the only manager is the requester, or because the chain reaches the top of " +
      "the organisation. Strictly wider than ordinary approval: it reaches employees whose management " +
      "chain does not reach the holder at all"),

    new(AttendancePermissionNames.ViewSensitiveLeave,
      "Reveal the TYPE of leave on requests the holder can already see. Leave type can disclose health " +
      "information — sick leave is a medical fact about an identified person — so it is granted separately " +
      "from seeing that somebody is absent")
  ];

  // Enumerated once at composition and never re-read, so this is a property over a static array rather than
  // a method that could be tempted to compute something per call. `IPermissionCatalogContributor` exposes a
  // PROPERTY, not a method — a detail worth stating because assuming otherwise costs a compile cycle.
  public IReadOnlyCollection<ModulePermissionDefinition> Permissions => Definitions;
}

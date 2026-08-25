using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Records;

public static class AttendanceRecordErrors
{
  public static readonly Error CompanyRequired = new(
    "Attendance.RecordCompanyRequired",
    "An attendance record must belong to a company.");

  public static readonly Error PeriodRequired = new(
    "Attendance.RecordPeriodRequired",
    "An attendance record must belong to an attendance period.");

  public static readonly Error EmployeeRequired = new(
    "Attendance.RecordEmployeeRequired",
    "An attendance record must name an employee.");

  public static readonly Error NotFound = new(
    "Attendance.RecordNotFound",
    "The attendance record does not exist.");

  public static readonly Error NegativeObservation = new(
    "Attendance.RecordQuantityNegative",
    "An observed attendance quantity cannot be negative; record a correction as an adjustment instead.");

  public static readonly Error InvalidOvertimeTier = new(
    "Attendance.OvertimeTierInvalid",
    "An overtime tier must be at most 32 characters and cannot contain control characters.");

  // Overtime without a tier is a quantity Payroll cannot price: the tier is what a pay element's rate is
  // configured against (`OD-ATT-0008`). Recording it untiered would produce hours nobody could pay.
  public static readonly Error OvertimeTierRequired = new(
    "Attendance.OvertimeTierRequired",
    "Overtime must carry a tier label; the rate for each tier is configured in Payroll.");

  public static readonly Error InvalidNote = new(
    "Attendance.RecordNoteInvalid",
    "An attendance note must be at most 1000 characters and cannot contain control characters.");

  public static readonly Error AdjustedRecordRequired = new(
    "Attendance.AdjustedRecordRequired",
    "An adjustment must name the attendance record it corrects.");

  public static readonly Error AdjustmentNoteRequired = new(
    "Attendance.AdjustmentNoteRequired",
    "An adjustment must carry a note explaining the correction.");

  public static readonly Error AdjustmentChangesNothing = new(
    "Attendance.AdjustmentChangesNothing",
    "An adjustment must change at least one quantity.");

  public static readonly Error AdjustedRecordMismatch = new(
    "Attendance.AdjustedRecordMismatch",
    "An adjustment must correct a record for the same employee and the same date.");

  // ---- THE EMPLOYMENT WINDOW (REQ-ATT-0006, BR-HR-0004 as read by OD-PAY-0010).
  //
  // Two errors rather than one, because the remedies differ and a caller can act on the distinction. Both
  // describe THE EMPLOYEE named in the body, never the record addressed in the route — which is exactly the
  // miscoding `DepartmentApiErrorMapper` was written to fix.
  public static readonly Error BeforeEmployment = new(
    "Attendance.RecordBeforeEmployment",
    "Attendance cannot be recorded for a date before the employee's employment date.");

  public static readonly Error AfterTermination = new(
    "Attendance.RecordAfterTermination",
    "Attendance cannot be recorded for a date after the employee's termination date.");

  public static readonly Error EmployeeNotInCompany = new(
    "Attendance.RecordEmployeeNotInCompany",
    "The employee does not belong to this company.");

  public static readonly Error DateOutsidePeriod = new(
    "Attendance.RecordDateOutsidePeriod",
    "The attendance date does not fall inside the attendance period.");
}

using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Calendars;

// WORKING CALENDAR REFUSALS. Named rather than numbered, per the transport rules this module inherits from
// HR, GL and Payroll: a client branches on a stable name, and a message is for a human.
public static class WorkingCalendarErrors
{
  public static readonly Error CompanyRequired = new(
    "Attendance.WorkingCalendarCompanyRequired",
    "A working calendar must belong to a company.",
    Field: "companyId");

  public static readonly Error InvalidName = new(
    "Attendance.WorkingCalendarNameInvalid",
    "A working calendar name is required and must be at most 200 characters.",
    Field: "name");

  public static readonly Error DuplicateName = new(
    "Attendance.WorkingCalendarNameConflict",
    "A working calendar with this name already exists in this company.");

  public static readonly Error NotFound = new(
    "Attendance.WorkingCalendarNotFound",
    "The working calendar does not exist.");

  public static readonly Error InvalidWeekendPattern = new(
    "Attendance.WeekendPatternInvalid",
    "A weekend pattern must be a set of days of the week.",
    Field: "weekendDays");

  // A seven-day weekend means no working day ever exists, which would make every range count zero and every
  // leave request consume nothing — a configuration that silently disables the module rather than failing.
  public static readonly Error WeekendPatternCoversEveryDay = new(
    "Attendance.WeekendPatternCoversEveryDay",
    "A weekend pattern cannot cover every day of the week; no working day would ever exist.",
    Field: "weekendDays");

  public static readonly Error InvalidHolidayName = new(
    "Attendance.HolidayNameInvalid",
    "A holiday name is required and must be at most 200 characters.",
    Field: "name");

  public static readonly Error DuplicateHoliday = new(
    "Attendance.HolidayDateConflict",
    "A holiday is already recorded on this date in this calendar.");

  public static readonly Error HolidayNotFound = new(
    "Attendance.HolidayNotFound",
    "No holiday is recorded on this date in this calendar.");

  public static readonly Error NoCalendarForCompany = new(
    "Attendance.WorkingCalendarMissing",
    "This company has no working calendar; leave and attendance cannot be computed without one.");
}

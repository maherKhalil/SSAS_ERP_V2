using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Calendar;

// THE FISCAL CALENDAR'S NAMED REFUSALS (REQ-GL-0009, REQ-GL-0010, BR-GL-0003).
public static class CalendarErrors
{
  public static readonly Error InvalidCode = new(
    "Gl.FiscalYearCodeInvalid",
    "A fiscal year code is required and must be at most 32 characters.");

  public static readonly Error InvalidRange = new(
    "Gl.FiscalYearRangeInvalid",
    "A fiscal year must start before it ends.");

  public static readonly Error NoPeriods = new(
    "Gl.FiscalYearHasNoPeriods",
    "A fiscal year must define at least one period.");

  // ---- CONTIGUOUS AND NON-OVERLAPPING ARE ONE REFUSAL, NOT TWO (AC-GL-0011).
  //
  // A gap and an overlap are the same defect seen from opposite sides: in both cases the periods do not
  // partition the year, and in both cases an entry date would resolve to either no period or two. Splitting
  // them into separate errors would ask the caller to care about a distinction that changes nothing they do.
  public static readonly Error PeriodsNotContiguous = new(
    "Gl.FiscalPeriodsNotContiguous",
    "Fiscal periods must be contiguous and non-overlapping, and must exactly span the fiscal year.");

  public static readonly Error PeriodNotFound = new(
    "Gl.FiscalPeriodNotFound",
    "No open fiscal period covers this date.");

  public static readonly Error PeriodClosed = new(
    "Gl.FiscalPeriodClosed",
    "The fiscal period covering this date is closed and cannot receive postings.");

  public static readonly Error PeriodAlreadyClosed = new(
    "Gl.FiscalPeriodAlreadyClosed",
    "The fiscal period is already closed.");

  public static readonly Error PeriodAlreadyOpen = new(
    "Gl.FiscalPeriodAlreadyOpen",
    "The fiscal period is already open.");

  public static readonly Error YearNotFound = new(
    "Gl.FiscalYearNotFound",
    "The fiscal year does not exist.");

  public static readonly Error DuplicateCode = new(
    "Gl.FiscalYearCodeConflict",
    "A fiscal year with this code already exists for this company.");

  // ---- ANOTHER DEFINITION FOR THIS COMPANY HOLDS THE CALENDAR LOCK (T-184).
  //
  // **Transient and worth retrying**, which is what makes it a distinct code rather than a generic
  // conflict: the caller is not wrong and nothing about the request needs changing. That is the opposite
  // of `DuplicateCode` and `OverlappingYear`, which both mean the input must change.
  //
  // It is also returned when a caller reaches the lock with no open transaction — **a sequencing bug in
  // this module, not a busy system.** Refusing there is what stops that bug presenting as an intermittent
  // overlap much later, and `sp_getapplock` with `Transaction` ownership makes it unmissable.
  public static readonly Error CalendarDefinitionBusy = new(
    "Gl.FiscalCalendarBusy",
    "Another fiscal-year definition for this company is in progress. Retry the request.");

  public static readonly Error OverlappingYear = new(
    "Gl.FiscalYearOverlaps",
    "A fiscal year already covers part of this date range for this company.");
}

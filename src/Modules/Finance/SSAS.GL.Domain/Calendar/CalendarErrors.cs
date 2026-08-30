using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Calendar;

// THE FISCAL CALENDAR'S NAMED REFUSALS (REQ-GL-0009, REQ-GL-0010, BR-GL-0003).
public static class CalendarErrors
{
  public static readonly Error InvalidCode = new(
    "Gl.FiscalYearCodeInvalid",
    "A fiscal year code is required and must be at most 32 characters.",
    Field: "code");

  public static readonly Error InvalidRange = new(
    "Gl.FiscalYearRangeInvalid",
    "A fiscal year must start before it ends.");

  public static readonly Error NoPeriods = new(
    "Gl.FiscalYearHasNoPeriods",
    "A fiscal year must define at least one period.",
    Field: "periods");

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
  // ---- TWO FISCAL YEARS COVER ONE DATE, AND THAT IS A DIFFERENT CONDITION FROM NONE (T-187).
  //
  // `PeriodNotFound` means *no calendar covers this date*, and its remedy is to define or open one.
  // **This means TWO do, and its remedy is to fix the calendar.** Collapsing them sends an operator to
  // do the wrong thing with full confidence.
  //
  // ⚠ **WHY REFUSING BEATS PICKING ONE, EVEN DETERMINISTICALLY.** `GetCoveringAsync` had no ordering, so
  // the year returned could differ BETWEEN CALLS — and a journal and its reversal are SEPARATE calls
  // resolving separate dates. An entry could post into year A and the entry that cancels it into year B:
  // different period, different number sequence. **Adding an ORDER BY would make that consistent rather
  // than correct** — both still landing in a year chosen by a tiebreak nobody ratified, and the
  // consistency would make it look decided.
  //
  // `DEC-L-084` is why this is the last line of defence: no constraint can express range non-overlap, so
  // the guard is the only enforcement, and a guard that has ever been bypassed leaves data no guard can
  // retroactively fix. T-184 closed the race; it could not close what the race already wrote.
  public static readonly Error AmbiguousCoveringYear = new(
    "Gl.FiscalCalendarAmbiguous",
    "More than one fiscal year covers this date for this company. The calendar must be corrected before "
    + "entries can be posted to it.");

  public static readonly Error CalendarDefinitionBusy = new(
    "Gl.FiscalCalendarBusy",
    "Another fiscal-year definition for this company is in progress. Retry the request.");

  public static readonly Error OverlappingYear = new(
    "Gl.FiscalYearOverlaps",
    "A fiscal year already covers part of this date range for this company.");
}

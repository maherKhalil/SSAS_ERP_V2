namespace SSAS.Attendance.Contracts.Summaries;

// ================================================================================================
// THE ATTENDANCE SIDE OF THE PAYROLL BOUNDARY. TOTALS, NEVER A FEED.
// ================================================================================================
//
// `DEC-ATT-0002` settled the mechanism by triple precedent — `IEmployeeRoster` (HR to Payroll),
// `IJournalPoster` (GL to Payroll) and `InspectPostingWindowAsync` (a consumer-shaped addition to an
// existing contract). A published contract, shaped by its consumer, never an assembly reference
// (`ADR-012`).
//
// It also fixed the SHAPE: what payroll calculation actually needs is **period totals**. A contract exposing
// punch-level movement would let every future Payroll feature read minute-by-minute employee location **with
// no call-site change for anyone to review** — the same argument that kept `NationalId` out of
// `IEmployeeRoster`.
//
// And the register `SSAS.GL.Contracts` established applies here without amendment:
//
//     A CROSS-MODULE CONTRACT HAS NO BUSINESS BEING LAXER THAN THE OWNING MODULE'S OWN HTTP SURFACE.
//
// Which is why `LeaveDaysByType` is absent below. `Attendance.Leave.ViewSensitive` gates leave TYPE over
// HTTP because a type can disclose health information, so handing type to Payroll ungated would make the
// contract the widest door in the module. **Payroll needs paid-versus-unpaid day counts to compute pay; it
// does not need to know which of them were sick days.**

// ================================================================================================
// THE BRANCH DECISION, STATED AT THE SITE BECAUSE IT IS DELIBERATE AND LOOKS LIKE AN OMISSION.
// ================================================================================================
//
// `OD-ATT-0011` ruled THE SPLIT:
//
//   * Attendance RECORDS are branch-owned. A supervisor reads their own branch, exactly as `Employee` is
//     branch-scoped, resolved live from `ITenantBranchAccessResolver`.
//
//   * THIS CONTRACT IS BRANCH-BLIND AND COMPANY-COMPLETE. It applies NO branch predicate. None.
//
// **The asymmetry is intended and the hole in it is ruled intended.** A caller who cannot read a branch's
// records through the HTTP surface can still see that branch's hours inside a company payroll total. That is
// the point: payroll is a COMPANY-level act, and `DEC-PAY-0017` refused a branch filter on the roster
// precisely because a filter means a payroll-feeding query can silently omit employees.
//
// A branch predicate added here later — however reasonable it looked in isolation — would reintroduce that
// failure, and it would be invisible, because the numbers would still balance. They would simply be the
// wrong numbers for a smaller set of people.
//
// An architecture guard asserts the implementation's query applies no branch predicate. The comment explains
// the decision; the guard is what survives someone who has not read the comment.

// ---- THE OUTCOME SET, CLOSED (OD-ATT-0010).
//
// Modelled on `JournalPostingStatus`. Every outcome is a VALUE the caller must handle rather than an
// exception it may not catch — the `InspectPostingWindowAsync` pattern, which exists because a late failure
// in a posting chain is far more expensive than a refusal at the gate.
public enum AttendanceSummaryStatus
{
  // The period is closed and the totals are final.
  Available = 0,

  // ---- THE ONE THAT MATTERS.
  //
  // `OD-ATT-0010` ruled (a): Payroll REFUSES an open attendance period. A run calculated from a period that
  // is still being edited is a snapshot of a moving target, and payroll runs get approved and posted to GL —
  // so a wrong snapshot becomes a posted journal entry, and reversing one of those is a business event
  // rather than a fix.
  PeriodOpen = 1,

  // No attendance period covers the date the caller named. Distinct from `PeriodOpen` because the remedies
  // differ: one is "wait", the other is "somebody has to create the period".
  PeriodNotFound = 2,

  // The employee has no attendance in the period. NOT an error — a valid answer with zero quantities, kept
  // separate from `Available` so a caller can tell "nothing recorded" from "recorded as zero".
  EmployeeNotInScope = 3
}

// ---- THE TOTALS.
//
// Quantities only. **No money, no rate, no multiplier, no currency** (`DEC-ATT-0004`): Attendance records
// how much, Payroll decides what it is worth. A monetary field appearing here means the module boundary has
// drifted, not that a convenience was added.
public sealed record AttendanceSummaryResult(
  AttendanceSummaryStatus Status,
  Guid EmployeeId,
  Guid CompanyId,
  Guid AttendancePeriodId,
  DateTimeOffset PeriodStartUtc,
  DateTimeOffset PeriodEndUtc,
  decimal WorkedQuantity,

  // ---- OVERTIME BY TIER (OD-ATT-0008).
  //
  // Overtime is RECORDED with a tier label, and the rate for each tier is configured in Payroll as a pay
  // element. So this carries the QUANTITY per tier and Payroll supplies the money — which is the whole of
  // `DEC-ATT-0004` expressed in one field.
  //
  // A dictionary rather than fixed fields because the tier vocabulary is a company's business, and freezing
  // it into an enum here would put a jurisdictional list into a cross-module contract.
  IReadOnlyDictionary<string, decimal> OvertimeQuantityByTier,

  decimal PaidAbsenceQuantity,

  // The quantity Payroll deducts. The single most consequential number in this record.
  decimal UnpaidAbsenceQuantity)
{
  public static AttendanceSummaryResult NotAvailable(
    AttendanceSummaryStatus status, Guid employeeId, Guid companyId) =>
    new(status, employeeId, companyId, Guid.Empty,
      DateTimeOffset.MinValue, DateTimeOffset.MinValue, 0m,
      new Dictionary<string, decimal>(StringComparer.Ordinal), 0m, 0m);
}

// ---- THE INSPECTION RESULT.
//
// Deliberately separate from the summary. A caller asking "may I calculate against this period" is asking a
// different question from "what are this employee's totals", and `InspectPostingWindowAsync` earned that
// separation: it let Payroll check the window BEFORE composing a journal, so the refusal arrived before the
// work rather than after it.
public sealed record AttendancePeriodInspection(
  AttendanceSummaryStatus Status,
  Guid AttendancePeriodId,
  string PeriodName,
  DateTimeOffset PeriodStartUtc,
  DateTimeOffset PeriodEndUtc,
  bool IsClosed);

public interface IAttendanceSummary
{
  // ---- ONE EMPLOYEE, ONE PERIOD, NAMED BY A DATE INSIDE IT (OD-ATT-0009).
  //
  // `anyDateInPeriodUtc` rather than bounds, mirroring `GeneratePayrollPeriodCommand`. **Bounds a caller
  // could name are bounds a caller could misalign**, and the period lookup would then answer for a straddle
  // without anyone noticing. The module resolves which period covers the date; the caller cannot express a
  // range that spans two.
  Task<AttendanceSummaryResult> GetForPeriodAsync(
    Guid companyId,
    Guid employeeId,
    DateTimeOffset anyDateInPeriodUtc,
    CancellationToken cancellationToken = default);

  // ---- THE GATE (OD-ATT-0010, the InspectPostingWindowAsync pattern).
  //
  // Answers whether the period covering this date is closed, WITHOUT returning any employee's data. Payroll
  // calls this at approval and refuses the run when it comes back `PeriodOpen`.
  Task<AttendancePeriodInspection> InspectPeriodAsync(
    Guid companyId,
    DateTimeOffset anyDateInPeriodUtc,
    CancellationToken cancellationToken = default);
}

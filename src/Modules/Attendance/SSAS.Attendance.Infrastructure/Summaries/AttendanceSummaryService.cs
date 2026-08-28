using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Application.Abstractions;
using SSAS.HR.Contracts.Employment;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;

namespace SSAS.Attendance.Infrastructure.Summaries;

// ================================================================================================
// THE PAYROLL BOUNDARY, AND THE ONE PLACE IN THIS MODULE WITH NO BRANCH PREDICATE.
// ================================================================================================
//
// **`OD-ATT-0011` RULED THE SPLIT, AND THIS FILE IS THE HALF THAT LOOKS WRONG.**
//
// Everywhere else in Attendance, records are branch-scoped: `AttendanceRecord` is `IBranchOwnedEntity`, the
// write boundary stamps `BranchId` from the execution context, and `AttendanceReadScope` carries the
// caller's authorized branches into every record read. A supervisor sees their branch and no other.
//
// **`AttendanceScoped` below applies NO BRANCH PREDICATE. NONE. DELIBERATELY.**
//
// ---- WHY, IN THE WORDS THE RULING USED.
//
// Payroll is a COMPANY-level act. `DEC-PAY-0017` refused a branch filter on the employee roster because a
// branch filter means **a payroll-feeding query can silently omit employees** — and an omitted employee does
// not produce an error, it produces a payroll that balances perfectly and underpays somebody.
//
// The same argument applies with more force to hours than to headcount. A branch-filtered summary would
// return an employee with FEWER hours than they worked, and every downstream number — gross, deductions,
// net, the posted journal — would be internally consistent and wrong.
//
// ---- THE HOLE IS RULED INTENDED, AND HERE IS EXACTLY WHAT IT IS.
//
// A caller who cannot read Branch B's attendance through the HTTP surface **can still see Branch B's hours
// reflected in a company payroll total**. That is a real information path and it was weighed and accepted:
// the alternative is a payroll that is wrong, and a wrong payroll is worse than a coarse aggregate.
//
// This asymmetry is why `OD-ATT-0011` was the one decision the analysis package declined to offer a
// preference on. The owner took both halves rather than trading one away.
//
// ---- AND THE THREE OBLIGATIONS THAT CAME WITH THE RULING.
//
//   1. **Stated at the site.** This comment.
//   2. **Guard-asserted.** An architecture test asserts this file's query applies no branch predicate — the
//      comment explains the decision, the guard is what survives someone who has not read it.
//   3. **Live resolution.** Company authority is resolved LIVE from `ITenantCompanyAccessResolver` below,
//      the `RosterScoped` pattern, never cached and never accepted from a caller.
//
// **Do not add a branch predicate to this file.** However reasonable it looks in isolation, it reintroduces
// `DEC-PAY-0017`'s failure, and it would be invisible: the numbers would still balance.

// ---- WHAT IT DOES NOT RETURN, AND WHY THAT IS THE SAME PRINCIPLE.
//
// **No leave type.** `Attendance.Leave.ViewSensitive` gates leave type over HTTP because a type can disclose
// health information, and `SSAS.GL.Contracts` set the register this follows:
//
//     A CROSS-MODULE CONTRACT HAS NO BUSINESS BEING LAXER THAN THE OWNING MODULE'S OWN HTTP SURFACE.
//
// Payroll needs paid-versus-unpaid day counts to compute pay. It does not need to know which of them were
// sick days, and a contract that volunteered them would be the widest door in the module.
//
// **No punch-level or time-of-day data**, per `DEC-ATT-0002`. Totals, never a feed.
internal sealed class AttendanceSummaryService(
  ITenantDbContextAccessor contextAccessor,
  ITenantCompanyAccessResolver companyAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  // ---- THE CALENDAR, THROUGH THE SAME PORT LEAVE USES (T-108).
  //
  // Not a query written here. `GetForCompanyAsync` is the ONE resolution of "the company's calendar", and a
  // second implementation would eventually disagree with the one that decides what leave consumed — which
  // is the reason `AttendanceReadService` gives for delegating its own day count to the domain.
  IWorkingCalendarRepository calendars,
  // ---- HR's EMPLOYMENT FACT, THROUGH HR's OWN CONTRACT (T-119).
  //
  // Attendance already references `SSAS.HR.Contracts` and `LeaveCommandHandlers` already injects this, so
  // the seam exists and this is not a new cross-module dependency.
  IEmployeeRoster roster) : IAttendanceSummary
{
  public async Task<AttendanceSummaryResult> GetForPeriodAsync(
    Guid companyId,
    Guid employeeId,
    DateTimeOffset anyDateInPeriodUtc,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await AuthorizeCompanyAsync(companyId, cancellationToken);

    var onDate = DateOnly.FromDateTime(anyDateInPeriodUtc.UtcDateTime);
    var period = await FindPeriodAsync(context, companyId, onDate, cancellationToken);

    if (period is null)
    {
      return AttendanceSummaryResult.NotAvailable(
        AttendanceSummaryStatus.PeriodNotFound, employeeId, companyId);
    }

    // `OD-ATT-0010` ruled (a): Payroll refuses an OPEN period. Reported as a VALUE the caller must handle,
    // never as an exception it might not catch — the `InspectPostingWindowAsync` pattern.
    if (!period.IsClosed)
    {
      return AttendanceSummaryResult.NotAvailable(
        AttendanceSummaryStatus.PeriodOpen, employeeId, companyId);
    }

    var records = await AttendanceScoped(context, currentTenant.TenantId!.Value, companyId)
      .Where(record => record.AttendancePeriodId == period.Id)
      .Where(record => record.EmployeeId == employeeId)
      .Select(record => new
      {
        // T-119: carried so the employment-window filter below can see which day each record is for.
        record.AttendanceDate,
        record.WorkedQuantity,
        record.OvertimeQuantity,
        record.OvertimeTier,
        record.PaidAbsenceQuantity,
        record.UnpaidAbsenceQuantity
      })
      .ToListAsync(cancellationToken);

    // ---- RECORDS OUTSIDE THE EMPLOYMENT WINDOW DO NOT COUNT (T-119).
    //
    // **An employee terminated on the 15th did not have unpaid absence on the 20th.** Before T-119 the
    // summary summed every record in the PERIOD, so a leaver was deducted for absence recorded after they
    // left — T-115 clamped the working-day count and left the quantities unbounded, and T-116 measured the
    // result.
    //
    // ---- WHY HERE RATHER THAN AT THE WRITE, AND IT IS THE ORDINARY CASE THAT DECIDES IT.
    //
    // Refusing to RECORD attendance after termination covers only one ordering. **Termination is routinely
    // entered after the fact — you terminate on the 20th effective the 15th — and the records are already
    // written by then.** A write-time guard cannot see a termination that has not happened yet; a summary
    // computed when the run is calculated sees both.
    //
    // ---- AND WHY ATTENDANCE RATHER THAN PAYROLL.
    //
    // The summary returns TOTALS, and a total cannot be narrowed after the fact — the same wall T-115 hit
    // with the working-day count. **The clamp belongs on the side holding the per-record data.** Payroll's
    // alternative would have meant putting the employment window into this contract, which is the employee
    // dimension `EmploymentTypeAssumptionTests` guard 3 exists to keep out.
    // ---- IT NARROWS THE DEDUCTION AND NOTHING ELSE, AND THAT IS DELIBERATE.
    //
    // **A blanket window filter over the record set would have been one line shorter and would have made a
    // second, opposite decision.** Four quantities travel on these records and two of them are PAYMENTS:
    //
    //   UnpaidAbsenceQuantity   a DEDUCTION   excluding it protects the employee   <- ruled, done here
    //   WorkedQuantity          hourly PAY    excluding it WITHHOLDS money
    //   OvertimeQuantityByTier  overtime PAY  excluding it WITHHOLDS money
    //   PaidAbsenceQuantity     never reaches Payroll
    //
    // **The ruling was that an employee is not DEDUCTED for absence on days they were not employed. It said
    // nothing about not PAYING them for work recorded on those days, and the two are opposite errors.**
    //
    // ---- AND THE ASYMMETRY IS REAL, NOT JUST CAUTION.
    //
    // **Absence outside employment is NOISE**: a stale record for somebody who had left, meaning nothing.
    // **Work outside employment is a CONTRADICTION**: either they worked, so the termination date is wrong,
    // or they did not, so the record is. Silently resolving that either way moves money on data the product
    // knows is inconsistent — which is why it is reported rather than decided here.
    var employment = (await roster.GetEmploymentAsync(
        companyId, ToInstant(period.StartDate), ToInstant(period.EndDate), cancellationToken))
      .FirstOrDefault(record => record.EmployeeId == employeeId);

    var employedFrom = employment is null
      ? period.StartDate
      : DateOnly.FromDateTime(employment.EmploymentDateUtc.UtcDateTime);

    var employedTo = employment?.TerminationDateUtc is { } terminated
      ? DateOnly.FromDateTime(terminated.UtcDateTime)
      : period.EndDate;

    var unpaidAbsence = records
      .Where(record => record.AttendanceDate >= employedFrom && record.AttendanceDate <= employedTo)
      .Sum(record => record.UnpaidAbsenceQuantity);

    if (records.Count == 0)
    {
      // Not an error. `EmployeeNotInScope` is kept distinct from `Available`-with-zeroes so a caller can
      // tell "nothing was recorded" from "recorded as zero" — the first usually means somebody forgot, and
      // the second is a real fact about the period.
      return AttendanceSummaryResult.NotAvailable(
        AttendanceSummaryStatus.EmployeeNotInScope, employeeId, companyId) with
      {
        AttendancePeriodId = period.Id,
        PeriodStartUtc = ToInstant(period.StartDate),
        PeriodEndUtc = ToInstant(period.EndDate)
      };
    }

    // ---- OBSERVATIONS AND ADJUSTMENTS SUM TOGETHER, WHICH IS THE WHOLE OF OD-ATT-0012.
    //
    // Adjustments carry signed deltas, so summing every row for the employee gives the corrected truth
    // without special-casing anything. A query that filtered to observations would look like the answer and
    // silently omit every correction — which is precisely why the repository port has no such method.
    var overtimeByTier = records
      .Where(record => record.OvertimeQuantity != 0m && record.OvertimeTier is not null)
      .GroupBy(record => record.OvertimeTier!, StringComparer.Ordinal)
      .ToDictionary(group => group.Key, group => group.Sum(record => record.OvertimeQuantity), StringComparer.Ordinal);

    return new AttendanceSummaryResult(
      AttendanceSummaryStatus.Available,
      employeeId,
      companyId,
      period.Id,
      ToInstant(period.StartDate),
      ToInstant(period.EndDate),
      records.Sum(record => record.WorkedQuantity),
      overtimeByTier,
      records.Sum(record => record.PaidAbsenceQuantity),
      unpaidAbsence);
  }

  // ---- WORKING DAYS BETWEEN TWO DATES (T-115).
  //
  // The DOMAIN answers, through the same `GetForCompanyAsync` resolution leave and the summary already use.
  // A second implementation of the day count would eventually disagree with the one that decides what people
  // are owed — which is the reason `AttendanceReadService` gives for delegating its own.
  //
  // Zero when the company has no working calendar, matching `NotAvailable`'s fail-closed zero.
  public async Task<int> GetWorkingDaysAsync(
    Guid companyId,
    DateOnly fromDate,
    DateOnly toDate,
    CancellationToken cancellationToken = default)
  {
    if (toDate < fromDate)
    {
      return 0;
    }

    var calendar = await calendars.GetForCompanyAsync(companyId, cancellationToken);
    return calendar?.WorkingDaysBetween(fromDate, toDate) ?? 0;
  }

  public async Task<AttendancePeriodInspection> InspectPeriodAsync(
    Guid companyId,
    DateTimeOffset anyDateInPeriodUtc,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await AuthorizeCompanyAsync(companyId, cancellationToken);

    var onDate = DateOnly.FromDateTime(anyDateInPeriodUtc.UtcDateTime);
    var period = await FindPeriodAsync(context, companyId, onDate, cancellationToken);

    // Returns NO employee data at any point — the whole reason inspection is a separate method rather than a
    // flag on the summary. Payroll calls this at approval to decide whether to proceed, and the answer must
    // not require it to have read anyone's hours.
    if (period is null)
    {
      return new AttendancePeriodInspection(
        AttendanceSummaryStatus.PeriodNotFound, Guid.Empty, string.Empty,
        DateTimeOffset.MinValue, DateTimeOffset.MinValue, IsClosed: false);
    }

    return new AttendancePeriodInspection(
      period.IsClosed ? AttendanceSummaryStatus.Available : AttendanceSummaryStatus.PeriodOpen,
      period.Id,
      period.Name.Value,
      ToInstant(period.StartDate),
      ToInstant(period.EndDate),
      period.IsClosed);
  }

  private static Task<AttendancePeriod?> FindPeriodAsync(
    DbContext context, Guid companyId, DateOnly onDate, CancellationToken cancellationToken) =>
    context.Set<AttendancePeriod>()
      .AsNoTracking()
      .Where(period => period.CompanyId == companyId)
      .Where(period => period.StartDate <= onDate && period.EndDate >= onDate)
      .FirstOrDefaultAsync(cancellationToken);

  // ================================================================================================
  // THE BRANCH-BLIND SCOPED QUERY. TENANT AND COMPANY ONLY. READ THE HEADER BEFORE CHANGING THIS.
  // ================================================================================================
  //
  // Two predicates, stated explicitly. The tenant one is restated even though a global filter exists, for
  // the reason `RosterScoped` restates its own: the query declares the invariant it depends on rather than
  // inheriting a configuration a future change could alter without touching this file.
  //
  // **There is no third predicate, and its absence is the ruling.** An architecture guard asserts that.
  private static IQueryable<AttendanceRecord> AttendanceScoped(DbContext context, Guid tenantId, Guid companyId) =>
    context.Set<AttendanceRecord>()
      .AsNoTracking()
      .Where(record => record.TenantId == tenantId)
      .Where(record => record.CompanyId == companyId);

  // Live, every call. Never cached, never accepted from a parameter — the `RosterScoped` discipline.
  //
  // Refusal is an EXCEPTION, not an empty summary. An empty summary would claim "this employee worked no
  // hours", a statement about the DATA: payroll would calculate cleanly, pay nothing for the period, and
  // nobody would learn that an authorization check had failed. Reaching here unauthorized is also not a
  // business outcome — Payroll authorizes the company through its own resolver first, so a disagreement
  // between the two means a defect rather than a user who lacks a grant.
  private async Task AuthorizeCompanyAsync(Guid companyId, CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      throw new UnauthorizedAccessException("The request does not carry a resolved tenant user.");
    }

    var permitted = await companyAccess.GetPermittedCompaniesAsync(tenantId, tenantUserId, cancellationToken);

    // Fail closed, per `ITenantCompanyAccessResolver`'s own instruction: an empty answer is legitimate and
    // callers must not fall back to "all".
    if (permitted.IsFailure || permitted.Value.All(company => company.CompanyId != companyId))
    {
      throw new UnauthorizedAccessException(
        "The caller has no authorized access to the requested company's attendance summary.");
    }
  }

  // A period boundary is a calendar day; the contract speaks in instants because every other cross-module
  // contract does. Converted at the boundary rather than stored that way, so the conversion happens once and
  // in a place a reader can find it.
  private static DateTimeOffset ToInstant(DateOnly date) =>
    new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}

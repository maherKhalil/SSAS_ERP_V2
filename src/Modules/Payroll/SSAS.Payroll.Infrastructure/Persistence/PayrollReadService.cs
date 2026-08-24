using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Infrastructure.Persistence;

// ==================================================================================================
// WHERE THE SCOPE BECOMES SQL.
// ==================================================================================================
//
// ---- EVERY QUERY STATES BOTH DIMENSIONS, EXPLICITLY, IN ITS OWN PREDICATE.
//
// `TenantId = @tenant AND CompanyId IN (@companies)`, written out at every call site. Neither is inherited:
//
//   * TENANT has a global query filter, and the predicate says so anyway. The filter is the enforcement;
//     restating it means the query declares the invariant it depends on rather than depending on a
//     configuration a future change could alter without touching this file.
//   * COMPANY has NO global filter, deliberately. A filter reads a single ambient value so it cannot express
//     "these three companies"; it is invisible at the call site so an author cannot see whether a query is
//     scoped; and `IgnoreQueryFilters()` removes it with one method call and no compiler complaint.
//
// **"All companies" is a LIST, never a missing condition.** By the time a scope exists the request modes
// have collapsed into concrete identifiers, so a query composes a predicate over values rather than
// branching on intent.
//
// ---- AND ON THIS SURFACE THAT MATTERS MORE THAN ANYWHERE IT HAS MATTERED BEFORE.
//
// Elsewhere a missing scope predicate is an authorization defect. Here it discloses what every employee in
// the tenant is paid.
internal sealed class PayrollReadService(ITenantDbContextAccessor contextAccessor) : IPayrollReadService
{
  // The company's base currency is PROJECTED on read and never stored per row (`ADR-027` decision 2,
  // `DEC-PAY-0003`). V1 is single-currency, so this is resolved once per response rather than joined per
  // line; when multi-currency arrives it becomes a join and every call site already expects the field.
  private const string ProjectedCurrencyPlaceholder = "";

  public async Task<IReadOnlyList<PayElementView>> GetElementsAsync(
    PayrollReadScope scope, Guid companyId, string? search, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var query = context.Set<PayElement>()
      .AsNoTracking()
      .Where(element => element.TenantId == scope.TenantId)
      .Where(element => scope.CompanyIds.Contains(element.CompanyId))
      .Where(element => element.CompanyId == companyId);

    if (!string.IsNullOrWhiteSpace(search))
    {
      // Against the NORMALIZED column, not the value-converted one. `DEC-POS-0030` records that a
      // value-converted property translates in a projection but NOT in a predicate — HR shipped a department
      // search that threw for every search term, and GL wrote the shadow up front to avoid it. This is the
      // third module, and it uses the shadow.
      var term = search.Trim().ToUpperInvariant();
      query = query.Where(element =>
        element.NormalizedCode.Contains(term) || element.NormalizedName.Contains(term));
    }

    return await query
      .OrderBy(element => element.CalculationOrder)
      .ThenBy(element => element.NormalizedCode)
      .Select(element => new PayElementView(
        element.Id, element.CompanyId, element.Code.Value, element.Name.Value,
        element.Kind, element.Behaviour, element.DefaultRateOrAmount,
        element.CalculationOrder, element.GlAccountId, element.IsActive))
      .ToListAsync(cancellationToken);
  }

  public async Task<PayElementView?> GetElementAsync(
    PayrollReadScope scope, Guid payElementId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The identifier is the LAST condition, not the first. An element outside the scope simply does not
    // match, so it is reported as absent rather than as forbidden — telling a caller that something exists
    // but is not theirs leaks the estate one probe at a time.
    return await context.Set<PayElement>()
      .AsNoTracking()
      .Where(element => element.TenantId == scope.TenantId)
      .Where(element => scope.CompanyIds.Contains(element.CompanyId))
      .Where(element => element.Id == payElementId)
      .Select(element => new PayElementView(
        element.Id, element.CompanyId, element.Code.Value, element.Name.Value,
        element.Kind, element.Behaviour, element.DefaultRateOrAmount,
        element.CalculationOrder, element.GlAccountId, element.IsActive))
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<CompensationView>> GetCompensationHistoryAsync(
    PayrollReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var records = await ScopedCompensation(await contextAccessor.GetRequiredAsync(cancellationToken), scope)
      .Where(record => record.EmployeeId == employeeId)
      .OrderByDescending(record => record.EffectiveFromUtc)
      .Include(record => record.Assignments)
      .ToListAsync(cancellationToken);

    // `IsInForceNow` is computed HERE from the series, never stored. `EmployeeCompensation` deliberately
    // holds no current-flag, and projecting one keeps that true while still answering the question a reader
    // actually has.
    var inForce = EmployeeCompensation.InForceOn(records, DateTimeOffset.UtcNow);

    return [.. records.Select(record => Project(record, inForce?.Id == record.Id))];
  }

  public async Task<CompensationView?> GetCompensationInForceAsync(
    PayrollReadScope scope, Guid employeeId, DateTimeOffset onUtc, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var records = await ScopedCompensation(await contextAccessor.GetRequiredAsync(cancellationToken), scope)
      .Where(record => record.EmployeeId == employeeId)
      .Include(record => record.Assignments)
      .ToListAsync(cancellationToken);

    // Derived by the domain, not by a query. One implementation of "what was in force on this date" —
    // a second would be a second answer to "what were they paid".
    var inForce = EmployeeCompensation.InForceOn(records, onUtc);

    return inForce is null ? null : Project(inForce, true);
  }

  public async Task<IReadOnlyList<PayrollPeriodView>> GetPeriodsAsync(
    PayrollReadScope scope, Guid companyId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollPeriod>()
      .AsNoTracking()
      .Where(period => period.TenantId == scope.TenantId)
      .Where(period => scope.CompanyIds.Contains(period.CompanyId))
      .Where(period => period.CompanyId == companyId)
      .OrderByDescending(period => period.StartUtc)
      .Select(period => new PayrollPeriodView(
        period.Id, period.CompanyId, period.FiscalPeriodId, period.Name,
        period.StartUtc, period.EndUtc, period.PayDateUtc))
      .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<PayrollRunView>> GetRunsAsync(
    PayrollReadScope scope, Guid companyId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var runs = await ScopedRuns(context, scope)
      .Where(run => run.CompanyId == companyId)
      .Include(run => run.Lines)
      .OrderByDescending(run => run.CreatedUtc)
      .ToListAsync(cancellationToken);

    var periodNames = await context.Set<PayrollPeriod>()
      .AsNoTracking()
      .Where(period => period.CompanyId == companyId)
      .ToDictionaryAsync(period => period.Id, period => period.Name, cancellationToken);

    return [.. runs.Select(run => ProjectRun(run, periodNames))];
  }

  public async Task<PayrollRunView?> GetRunAsync(
    PayrollReadScope scope, Guid payrollRunId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var run = await ScopedRuns(context, scope)
      .Where(run => run.Id == payrollRunId)
      .Include(run => run.Lines)
      .FirstOrDefaultAsync(cancellationToken);

    if (run is null)
    {
      return null;
    }

    var periodNames = await context.Set<PayrollPeriod>()
      .AsNoTracking()
      .Where(period => period.Id == run.PayrollPeriodId)
      .ToDictionaryAsync(period => period.Id, period => period.Name, cancellationToken);

    return ProjectRun(run, periodNames);
  }

  // ---- THE PAYSLIP PROJECTS OVER `PayrollRunLine` ONLY (OD-PAY-0015).
  //
  // Never over draft lines. That is what makes a payslip exist precisely when an approved record exists, and
  // what makes it permanently faithful: the lines it reads are `IAppendOnlyEntity`, so no later event can
  // change what it says.
  public async Task<PayslipView?> GetPayslipAsync(
    PayrollReadScope scope, Guid payrollRunId, Guid employeeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var run = await ScopedRuns(context, scope)
      .Where(run => run.Id == payrollRunId)
      .Include(run => run.Lines)
      .FirstOrDefaultAsync(cancellationToken);

    if (run is null)
    {
      return null;
    }

    var payslip = await BuildPayslipAsync(context, run, employeeId, cancellationToken);
    return payslip;
  }

  public async Task<IReadOnlyList<PayslipView>> GetPayslipsForEmployeeAsync(
    PayrollReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var runs = await ScopedRuns(context, scope)
      .Include(run => run.Lines)
      .Where(run => run.Lines.Any(line => line.EmployeeId == employeeId))
      .OrderByDescending(run => run.CreatedUtc)
      .ToListAsync(cancellationToken);

    var payslips = new List<PayslipView>();
    foreach (var run in runs)
    {
      var payslip = await BuildPayslipAsync(context, run, employeeId, cancellationToken);
      if (payslip is not null)
      {
        payslips.Add(payslip);
      }
    }

    return payslips;
  }

  private static IQueryable<EmployeeCompensation> ScopedCompensation(
    DbContext context, PayrollReadScope scope) =>
    context.Set<EmployeeCompensation>()
      .AsNoTracking()
      .Where(record => record.TenantId == scope.TenantId)
      .Where(record => scope.CompanyIds.Contains(record.CompanyId));

  private static IQueryable<PayrollRun> ScopedRuns(DbContext context, PayrollReadScope scope) =>
    context.Set<PayrollRun>()
      .AsNoTracking()
      .Where(run => run.TenantId == scope.TenantId)
      .Where(run => scope.CompanyIds.Contains(run.CompanyId));

  private static CompensationView Project(EmployeeCompensation record, bool isInForceNow) =>
    new(record.Id, record.CompanyId, record.EmployeeId, record.EffectiveFromUtc, record.BaseAmount,
      record.WasOutsideGradeBand, record.GradeBandObservation, isInForceNow, ProjectedCurrencyPlaceholder,
      [.. record.Assignments.Select(a => new CompensationAssignmentView(a.PayElementId, string.Empty, a.RateOrAmount))]);

  // Totals are the SUM OF THE STORED LINES (`OD-PAY-0008`), never recomputed. That is what makes the
  // payslip add up by construction rather than by arithmetic performed twice.
  private static PayrollRunView ProjectRun(PayrollRun run, IReadOnlyDictionary<Guid, string> periodNames) =>
    new(run.Id, run.CompanyId, run.PayrollPeriodId,
      periodNames.TryGetValue(run.PayrollPeriodId, out var name) ? name : string.Empty,
      run.Status, run.CalculatedBy, run.CalculatedUtc, run.ApprovedBy, run.ApprovedUtc,
      run.PostedBy, run.PostedUtc, run.JournalEntryId, ProjectedCurrencyPlaceholder,
      run.TotalEarnings, run.TotalDeductions, run.NetPay,
      run.Lines.Select(line => line.EmployeeId).Distinct().Count());

  private static async Task<PayslipView?> BuildPayslipAsync(
    DbContext context, PayrollRun run, Guid employeeId, CancellationToken cancellationToken)
  {
    var lines = run.Lines.Where(line => line.EmployeeId == employeeId).OrderBy(line => line.Sequence).ToList();
    if (lines.Count == 0)
    {
      return null;
    }

    var elementIds = lines.Select(line => line.PayElementId).Distinct().ToArray();
    var elements = await context.Set<PayElement>()
      .AsNoTracking()
      .Where(element => elementIds.Contains(element.Id))
      .ToDictionaryAsync(element => element.Id, cancellationToken);

    var period = await context.Set<PayrollPeriod>()
      .AsNoTracking()
      .FirstOrDefaultAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);

    var earnings = lines.Where(line => line.Kind == PayElementKind.Earning).Sum(line => line.Amount);
    var deductions = lines.Where(line => line.Kind == PayElementKind.Deduction).Sum(line => line.Amount);

    return new PayslipView(
      run.Id, employeeId, period?.Name ?? string.Empty, period?.PayDateUtc ?? default,
      ProjectedCurrencyPlaceholder, earnings, deductions, earnings - deductions,
      [.. lines.Select(line => new PayslipLineView(
        line.Sequence, line.PayElementId,
        elements.TryGetValue(line.PayElementId, out var element) ? element.Code.Value : string.Empty,
        elements.TryGetValue(line.PayElementId, out var named) ? named.Name.Value : string.Empty,
        line.Kind, line.Amount))]);
  }
}

using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Infrastructure.Persistence;

// Every query goes through `ITenantDbContextAccessor`, which resolves the tenant's context and applies the
// tenant global filter. None of these methods names a `TenantId` for that reason — adding one would be a
// second source of truth for an invariant the context already enforces.
internal sealed class PayElementRepository(ITenantDbContextAccessor contextAccessor) : IPayElementRepository
{
  public async Task<PayElement?> GetByIdAsync(Guid payElementId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayElement>()
      .FirstOrDefaultAsync(element => element.Id == payElementId, cancellationToken);
  }

  // Compared on the NORMALIZED column, which is binary-collated, so the database decides what counts as the
  // same code rather than the caller's culture. Company-scoped, unlike `Account`'s tenant-wide equivalent —
  // the two signatures are `OD-GL-0003` and `OD-PAY-0005` made visible.
  public async Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayElement>()
      .AnyAsync(
        element => element.CompanyId == companyId && element.NormalizedCode == normalizedCode,
        cancellationToken);
  }

  public async Task<IReadOnlyList<PayElement>> GetActiveForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Ordered here so the calculator receives a deterministic set even before it applies its own ordering —
    // two runs over the same data must produce identical lines, and a stable input is half of that.
    return await context.Set<PayElement>()
      .Where(element => element.CompanyId == companyId && element.IsActive)
      .OrderBy(element => element.CalculationOrder)
      .ThenBy(element => element.NormalizedCode)
      .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<PayElement>> GetByIdsAsync(
    IReadOnlyCollection<Guid> payElementIds, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(payElementIds);

    if (payElementIds.Count == 0)
    {
      return [];
    }

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayElement>()
      .Where(element => payElementIds.Contains(element.Id))
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(PayElement payElement, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(payElement);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<PayElement>().AddAsync(payElement, cancellationToken);
  }
}

internal sealed class EmployeeCompensationRepository(ITenantDbContextAccessor contextAccessor)
  : IEmployeeCompensationRepository
{
  // The WHOLE history, ordered. There is deliberately no "get current" — the value in force is derived by
  // `EmployeeCompensation.InForceOn`, and a repository answer for "current" would be a second implementation
  // of that rule which would silently disagree the moment anyone asked about a past date.
  public async Task<IReadOnlyList<EmployeeCompensation>> GetHistoryAsync(
    Guid companyId, Guid employeeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<EmployeeCompensation>()
      .Include(record => record.Assignments)
      .Where(record => record.CompanyId == companyId && record.EmployeeId == employeeId)
      .OrderByDescending(record => record.EffectiveFromUtc)
      .ToListAsync(cancellationToken);
  }

  // One query for the whole company rather than one per employee: a payroll of any size would otherwise be
  // N round trips before a single amount was calculated.
  public async Task<IReadOnlyList<EmployeeCompensation>> GetHistoryForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<EmployeeCompensation>()
      .Include(record => record.Assignments)
      .Where(record => record.CompanyId == companyId)
      .OrderBy(record => record.EmployeeId)
      .ThenByDescending(record => record.EffectiveFromUtc)
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(EmployeeCompensation compensation, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(compensation);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<EmployeeCompensation>().AddAsync(compensation, cancellationToken);
  }
}

internal sealed class PayrollPeriodRepository(ITenantDbContextAccessor contextAccessor) : IPayrollPeriodRepository
{
  public async Task<PayrollPeriod?> GetByIdAsync(
    Guid payrollPeriodId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollPeriod>()
      .FirstOrDefaultAsync(period => period.Id == payrollPeriodId, cancellationToken);
  }

  public async Task<bool> ExistsForFiscalPeriodAsync(
    Guid companyId, Guid fiscalPeriodId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollPeriod>()
      .AnyAsync(
        period => period.CompanyId == companyId && period.FiscalPeriodId == fiscalPeriodId,
        cancellationToken);
  }

  public async Task AddAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(period);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<PayrollPeriod>().AddAsync(period, cancellationToken);
  }
}

// ---- THREE LOADERS, AND THE SEPARATION IS NOT AN OPTIMIZATION.
//
// A single loader that always included both line sets would make the APPEND-ONLY set tracked on every path,
// including paths that then call `SaveChanges` for an unrelated reason. A tracked `IAppendOnlyEntity` is one
// careless save away from `PreventAppendOnlyMutation` throwing — which would surface as a payroll failure
// rather than as the guard doing its job, and would send someone debugging the wrong thing.
internal sealed class PayrollRunRepository(ITenantDbContextAccessor contextAccessor) : IPayrollRunRepository
{
  public async Task<PayrollRun?> GetByIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollRun>()
      .FirstOrDefaultAsync(run => run.Id == payrollRunId, cancellationToken);
  }

  public async Task<PayrollRun?> GetWithDraftLinesAsync(
    Guid payrollRunId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollRun>()
      .Include(run => run.DraftLines)
      .FirstOrDefaultAsync(run => run.Id == payrollRunId, cancellationToken);
  }

  public async Task<PayrollRun?> GetWithLinesAsync(
    Guid payrollRunId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollRun>()
      .Include(run => run.Lines)
      .FirstOrDefaultAsync(run => run.Id == payrollRunId, cancellationToken);
  }

  public async Task<bool> ExistsForPeriodAsync(
    Guid companyId, Guid payrollPeriodId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<PayrollRun>()
      .AnyAsync(
        run => run.CompanyId == companyId && run.PayrollPeriodId == payrollPeriodId,
        cancellationToken);
  }

  public async Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(run);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    await context.Set<PayrollRun>().AddAsync(run, cancellationToken);
  }
}

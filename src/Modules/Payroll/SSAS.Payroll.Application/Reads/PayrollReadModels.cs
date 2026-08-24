using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Application.Reads;

// PAYROLL'S READ MODELS. Projections, never entities: a read surface that returned aggregates would leak
// mutability into a path that has no business writing anything.

public sealed record PayElementView(
  Guid PayElementId,
  Guid CompanyId,
  string Code,
  string Name,
  PayElementKind Kind,
  PayElementBehaviour Behaviour,
  decimal DefaultRateOrAmount,
  int CalculationOrder,
  Guid? GlAccountId,
  bool IsActive);

// A compensation record as history. `IsInForceNow` is COMPUTED at projection time from the series, never
// stored — `EmployeeCompensation` deliberately holds no current-flag, and a stored one would drift.
public sealed record CompensationView(
  Guid EmployeeCompensationId,
  Guid CompanyId,
  Guid EmployeeId,
  DateTimeOffset EffectiveFromUtc,
  decimal BaseAmount,
  bool WasOutsideGradeBand,
  string? GradeBandObservation,
  bool IsInForceNow,
  string CurrencyCode,
  IReadOnlyList<CompensationAssignmentView> Assignments);

public sealed record CompensationAssignmentView(Guid PayElementId, string PayElementCode, decimal? RateOrAmount);

public sealed record PayrollPeriodView(
  Guid PayrollPeriodId,
  Guid CompanyId,
  Guid FiscalPeriodId,
  string Name,
  DateTimeOffset StartUtc,
  DateTimeOffset EndUtc,
  DateTimeOffset PayDateUtc);

// A run's summary. Totals only — the LINES beneath them are personal data behind a different permission
// (`Payroll.Payslips.View`), which is why this type carries no line collection at all rather than an empty
// one a caller might expect to be populated.
public sealed record PayrollRunView(
  Guid PayrollRunId,
  Guid CompanyId,
  Guid PayrollPeriodId,
  string PeriodName,
  PayrollRunStatus Status,
  string? CalculatedBy,
  DateTimeOffset? CalculatedUtc,
  string? ApprovedBy,
  DateTimeOffset? ApprovedUtc,
  string? PostedBy,
  DateTimeOffset? PostedUtc,
  Guid? JournalEntryId,
  string CurrencyCode,
  decimal TotalEarnings,
  decimal TotalDeductions,
  decimal NetPay,
  int EmployeeCount);

// ---- THE PAYSLIP (OD-PAY-0015 — a read projection, not a document).
//
// Projected over `PayrollRunLine` ONLY, never over draft lines. That is what makes a payslip exist precisely
// when an approved record exists, and what makes it permanently faithful: the lines it reads are
// `IAppendOnlyEntity`, so no later event can change what it says.
//
// `TotalEarnings`, `TotalDeductions` and `NetPay` are the SUM OF THE RETURNED LINES (`OD-PAY-0008`), so a
// client can verify the payslip's own arithmetic without re-deriving anything. Under the ruled rounding
// this holds by construction — which is exactly why `AC-PAY-0026` is assertable.
public sealed record PayslipView(
  Guid PayrollRunId,
  Guid EmployeeId,
  string PeriodName,
  DateTimeOffset PayDateUtc,
  string CurrencyCode,
  decimal TotalEarnings,
  decimal TotalDeductions,
  decimal NetPay,
  IReadOnlyList<PayslipLineView> Lines);

public sealed record PayslipLineView(
  int Sequence,
  Guid PayElementId,
  string PayElementCode,
  string PayElementName,
  PayElementKind Kind,
  decimal Amount);

// EVERY METHOD TAKES A SCOPE, AND THERE IS NO OVERLOAD WITHOUT ONE.
//
// A read that omitted its scope predicate is not something a reviewer has to catch, because it is not
// something a caller can express. On this surface that is not a tidiness argument: a payroll read without a
// company predicate discloses what every employee in the tenant is paid.
public interface IPayrollReadService
{
  Task<IReadOnlyList<PayElementView>> GetElementsAsync(
    PayrollReadScope scope, Guid companyId, string? search, CancellationToken cancellationToken = default);

  Task<PayElementView?> GetElementAsync(
    PayrollReadScope scope, Guid payElementId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<CompensationView>> GetCompensationHistoryAsync(
    PayrollReadScope scope, Guid employeeId, CancellationToken cancellationToken = default);

  Task<CompensationView?> GetCompensationInForceAsync(
    PayrollReadScope scope, Guid employeeId, DateTimeOffset onUtc, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<PayrollPeriodView>> GetPeriodsAsync(
    PayrollReadScope scope, Guid companyId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<PayrollRunView>> GetRunsAsync(
    PayrollReadScope scope, Guid companyId, CancellationToken cancellationToken = default);

  Task<PayrollRunView?> GetRunAsync(
    PayrollReadScope scope, Guid payrollRunId, CancellationToken cancellationToken = default);

  Task<PayslipView?> GetPayslipAsync(
    PayrollReadScope scope, Guid payrollRunId, Guid employeeId, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<PayslipView>> GetPayslipsForEmployeeAsync(
    PayrollReadScope scope, Guid employeeId, CancellationToken cancellationToken = default);
}

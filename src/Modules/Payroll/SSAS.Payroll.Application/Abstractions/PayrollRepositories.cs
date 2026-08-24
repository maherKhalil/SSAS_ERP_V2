using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Application.Abstractions;

// PAYROLL'S WRITE-SIDE PORTS.
//
// One interface per aggregate root, each exposing only what its handlers need. The absences are as
// deliberate as the presences and are noted where a reader might expect a method.
public interface IPayElementRepository
{
  Task<PayElement?> GetByIdAsync(Guid payElementId, CancellationToken cancellationToken = default);

  // Company-scoped, which is the contrast with `IAccountRepository.CodeExistsAsync`: the chart of accounts is
  // tenant-level (`OD-GL-0003`) so its uniqueness question takes no company, and pay elements are
  // company-owned (`OD-PAY-0005`) so this one does. The shorter and longer signatures are the two rulings
  // made visible.
  Task<bool> CodeExistsAsync(Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  // Calculation needs every active element for a company in ONE round trip. A per-element lookup inside the
  // employee loop would be N x M queries for a payroll of any size.
  Task<IReadOnlyList<PayElement>> GetActiveForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default);

  // Approval needs the elements a run actually used, to check each is mapped and active (`OD-PAY-0012`).
  Task<IReadOnlyList<PayElement>> GetByIdsAsync(
    IReadOnlyCollection<Guid> payElementIds, CancellationToken cancellationToken = default);

  void Add(PayElement payElement);
}

public interface IEmployeeCompensationRepository
{
  // ---- THE WHOLE HISTORY, NOT "THE CURRENT ONE".
  //
  // There is deliberately no `GetCurrentAsync`. The value in force on a date is DERIVED by
  // `EmployeeCompensation.InForceOn`, and a repository method that answered "current" would be a second
  // implementation of that rule — one that would silently disagree the moment anyone asked about a past
  // date. `OD-PAY-0003` ruled dated history precisely so a past run can be reproduced; a "current" shortcut
  // is how that guarantee gets lost.
  Task<IReadOnlyList<EmployeeCompensation>> GetHistoryAsync(
    Guid companyId, Guid employeeId, CancellationToken cancellationToken = default);

  // The whole company's history, for calculation. One query rather than one per employee.
  Task<IReadOnlyList<EmployeeCompensation>> GetHistoryForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default);

  void Add(EmployeeCompensation compensation);

  // ---- THERE IS NO Update, AND NO Remove.
  //
  // A compensation record is never edited (`BR-PAY-0002`) and never deleted. A change is a new dated record,
  // which is an `Add`. The absence of the methods is the rule enforced by the shape of the port rather than
  // by everyone remembering it.
}

public interface IPayrollPeriodRepository
{
  Task<PayrollPeriod?> GetByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken = default);

  Task<bool> ExistsForFiscalPeriodAsync(
    Guid companyId, Guid fiscalPeriodId, CancellationToken cancellationToken = default);

  void Add(PayrollPeriod period);
}

public interface IPayrollRunRepository
{
  // Loads the run WITHOUT its line collections, for status-only operations.
  Task<PayrollRun?> GetByIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

  // Loads the run WITH its draft lines, for recalculation and approval.
  Task<PayrollRun?> GetWithDraftLinesAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

  // Loads the run WITH its approved lines, for posting and payslips.
  Task<PayrollRun?> GetWithLinesAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

  Task<bool> ExistsForPeriodAsync(
    Guid companyId, Guid payrollPeriodId, CancellationToken cancellationToken = default);

  void Add(PayrollRun run);

  // ---- THE THREE LOADERS ARE SEPARATE ON PURPOSE.
  //
  // A single `GetAsync` that always included both line sets would load an entire company's approved pay
  // history to answer "may this run be approved". Worse, it would make the append-only set TRACKED on every
  // path — and a tracked `IAppendOnlyEntity` is one careless `SaveChanges` away from an exception that
  // reports as a payroll failure rather than as the guard doing its job.
}

using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.GL.Contracts.Posting;
using SSAS.HR.Contracts.Employment;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.API.Tests.Payroll;

// Transport-layer stubs. They stand in for persistence and for the two cross-module contracts, so these
// tests are about the HTTP surface — routing, authorization, binding, and error mapping — and nothing else.
//
// **The two contract stubs are the point of the harness.** `IJournalPoster` and `IEmployeeRoster` are the
// only ways Payroll reaches GL and HR, so substituting them here proves the surface works without a ledger
// or an employee table — and proves, by their being substitutable at all, that the boundary is real.

public sealed class StubPayrollReads : IPayrollReadService
{
  public List<PayElementView> Elements { get; } = [];

  public List<CompensationView> Compensation { get; } = [];

  public List<PayrollPeriodView> Periods { get; } = [];

  public List<PayrollRunView> Runs { get; } = [];

  public PayslipView? Payslip { get; set; }

  public void Reset()
  {
    Elements.Clear();
    Compensation.Clear();
    Periods.Clear();
    Runs.Clear();
    Payslip = null;
  }

  public Task<IReadOnlyList<PayElementView>> GetElementsAsync(
    PayrollReadScope scope, Guid companyId, string? search, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<PayElementView>>(Elements);

  public Task<PayElementView?> GetElementAsync(
    PayrollReadScope scope, Guid payElementId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Elements.FirstOrDefault(element => element.PayElementId == payElementId));

  public Task<IReadOnlyList<CompensationView>> GetCompensationHistoryAsync(
    PayrollReadScope scope, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<CompensationView>>(Compensation);

  public Task<CompensationView?> GetCompensationInForceAsync(
    PayrollReadScope scope, Guid employeeId, DateTimeOffset onUtc, CancellationToken cancellationToken = default) =>
    Task.FromResult(Compensation.FirstOrDefault());

  public Task<IReadOnlyList<PayrollPeriodView>> GetPeriodsAsync(
    PayrollReadScope scope, Guid companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<PayrollPeriodView>>(Periods);

  public Task<IReadOnlyList<PayrollRunView>> GetRunsAsync(
    PayrollReadScope scope, Guid companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<PayrollRunView>>(Runs);

  public Task<PayrollRunView?> GetRunAsync(
    PayrollReadScope scope, Guid payrollRunId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Runs.FirstOrDefault(run => run.PayrollRunId == payrollRunId));

  public Task<PayslipView?> GetPayslipAsync(
    PayrollReadScope scope, Guid payrollRunId, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Payslip);

  public Task<IReadOnlyList<PayslipView>> GetPayslipsForEmployeeAsync(
    PayrollReadScope scope, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<PayslipView>>(Payslip is null ? [] : [Payslip]);
}

public sealed class StubPayElementRepository : IPayElementRepository
{
  public List<PayElement> Stored { get; } = [];

  public bool CodeTaken { get; set; }

  public void Reset()
  {
    Stored.Clear();
    CodeTaken = false;
  }

  public Task<PayElement?> GetByIdAsync(Guid payElementId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Stored.FirstOrDefault(element => element.Id == payElementId));

  public Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<IReadOnlyList<PayElement>> GetActiveForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<PayElement>>([.. Stored.Where(element => element.IsActive)]);

  public Task<IReadOnlyList<PayElement>> GetByIdsAsync(
    IReadOnlyCollection<Guid> payElementIds, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<PayElement>>([.. Stored.Where(element => payElementIds.Contains(element.Id))]);

  public Task AddAsync(PayElement payElement, CancellationToken cancellationToken = default)
  {
    Stored.Add(payElement);
    return Task.CompletedTask;
  }
}

public sealed class StubCompensationRepository : IEmployeeCompensationRepository
{
  public List<EmployeeCompensation> Stored { get; } = [];

  public void Reset() => Stored.Clear();

  public Task<IReadOnlyList<EmployeeCompensation>> GetHistoryAsync(
    Guid companyId, Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<EmployeeCompensation>>(
      [.. Stored.Where(record => record.EmployeeId == employeeId)]);

  public Task<IReadOnlyList<EmployeeCompensation>> GetHistoryForCompanyAsync(
    Guid companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<EmployeeCompensation>>(Stored);

  public Task AddAsync(EmployeeCompensation compensation, CancellationToken cancellationToken = default)
  {
    Stored.Add(compensation);
    return Task.CompletedTask;
  }
}

// ---- ONE-OFF PAY INSTRUCTIONS (T-110).
//
// `GetUnconsumedForPeriodAsync` filters on the reference exactly as the real repository does, so a test that
// approves a run and re-reads sees the same thing production would.
public sealed class StubOneOffPaymentRepository : IOneOffPaymentRepository
{
  public List<OneOffPayment> Stored { get; } = [];

  public void Reset() => Stored.Clear();

  public Task<IReadOnlyList<OneOffPayment>> GetUnconsumedForPeriodAsync(
    Guid companyId, Guid payrollPeriodId, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<OneOffPayment>>(
      [.. Stored.Where(payment => payment.CompanyId == companyId
        && payment.PayrollPeriodId == payrollPeriodId
        && !payment.IsConsumed)]);

  public Task<OneOffPayment?> GetByIdAsync(
    Guid oneOffPaymentId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Stored.FirstOrDefault(payment => payment.Id == oneOffPaymentId));

  public Task AddAsync(OneOffPayment payment, CancellationToken cancellationToken = default)
  {
    Stored.Add(payment);
    return Task.CompletedTask;
  }
}

public sealed class StubPayrollPeriodRepository : IPayrollPeriodRepository
{
  public List<PayrollPeriod> Stored { get; } = [];

  public bool Exists { get; set; }

  public void Reset()
  {
    Stored.Clear();
    Exists = false;
  }

  public Task<PayrollPeriod?> GetByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Stored.FirstOrDefault(period => period.Id == payrollPeriodId));

  public Task<bool> ExistsForFiscalPeriodAsync(
    Guid companyId, Guid fiscalPeriodId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Exists);

  public Task AddAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
  {
    Stored.Add(period);
    return Task.CompletedTask;
  }
}

public sealed class StubPayrollRunRepository : IPayrollRunRepository
{
  // ---- NOTHING TO DO HERE, AND THE EMPTINESS IS THE POINT.
  //
  // An in-memory stub has no change tracker, so it has no orphans for an explicit delete to remove. The
  // defect this method exists for is a PERSISTENCE fact — a platform-wide `Restrict` overriding a module's
  // configured cascade — and it is invisible to every stub by construction. That is why it took a real-SQL
  // end-to-end test to find, and why this override can be honestly empty.
  public Task RemoveDraftLinesAsync(PayrollRun run, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;

  public List<PayrollRun> Stored { get; } = [];

  public bool Exists { get; set; }

  public void Reset()
  {
    Stored.Clear();
    Exists = false;
  }

  public Task<PayrollRun?> GetByIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Stored.FirstOrDefault(run => run.Id == payrollRunId));

  public Task<PayrollRun?> GetWithDraftLinesAsync(
    Guid payrollRunId, CancellationToken cancellationToken = default) =>
    GetByIdAsync(payrollRunId, cancellationToken);

  public Task<PayrollRun?> GetWithLinesAsync(Guid payrollRunId, CancellationToken cancellationToken = default) =>
    GetByIdAsync(payrollRunId, cancellationToken);

  public Task<bool> ExistsForPeriodAsync(
    Guid companyId, Guid payrollPeriodId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Exists);

  public Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default)
  {
    Stored.Add(run);
    return Task.CompletedTask;
  }
}

// THE LEDGER, STUBBED AT THE CONTRACT. Every field a test needs to steer is settable, so a closed period or
// a refusal can be produced without a database — which is the whole reason the contract answers with a
// closed set of outcomes rather than an open problem code.
public sealed class StubJournalPoster : IJournalPoster
{
  public PostingWindow Window { get; set; } = new(PostingWindowStatus.Open, "January 2026", Guid.NewGuid(),
    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero));

  public JournalPostingOutcome PostOutcome { get; set; } = JournalPostingOutcome.Success(Guid.NewGuid());

  public JournalPostingOutcome ReverseOutcome { get; set; } = JournalPostingOutcome.Success(Guid.NewGuid());

  public JournalPostingRequest? LastPosted { get; private set; }

  public void Reset()
  {
    Window = new(PostingWindowStatus.Open, "January 2026", Guid.NewGuid(),
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero));
    PostOutcome = JournalPostingOutcome.Success(Guid.NewGuid());
    ReverseOutcome = JournalPostingOutcome.Success(Guid.NewGuid());
    LastPosted = null;
  }

  public Task<JournalPostingOutcome> PostAsync(
    JournalPostingRequest request, CancellationToken cancellationToken = default)
  {
    LastPosted = request;
    return Task.FromResult(PostOutcome);
  }

  public Task<JournalPostingOutcome> ReverseAsync(
    JournalReversalRequest request, CancellationToken cancellationToken = default) =>
    Task.FromResult(ReverseOutcome);

  public Task<PostingWindow> InspectPostingWindowAsync(
    Guid companyId, DateTimeOffset entryDateUtc, CancellationToken cancellationToken = default) =>
    Task.FromResult(Window);
}

public sealed class StubEmployeeRoster : IEmployeeRoster
{
  public List<EmploymentRecord> Employment { get; } = [];

  public void Reset() => Employment.Clear();

  public Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
    Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
    Task.FromResult<IReadOnlyList<EmploymentRecord>>(Employment);
}

// ================================================================================================
// THE THIRD ROUTE OUT OF PAYROLL (FP-013), AND ITS ABSENCE FAILED EVERY TEST IN THIS HARNESS.
// ================================================================================================
//
// The host's header says the point of stubbing `IJournalPoster` and `IEmployeeRoster` is that the module
// boundary is DEMONSTRATED rather than asserted: they can be replaced here with no GL or HR service
// registered at all.
//
// FP-013 added a third — `IAttendanceSummary`, consumed by calculation and by approval — and until this
// existed the host could not build its service provider. **Every Payroll API test failed**, which is the
// harness telling the truth about a new dependency rather than a defect in the module.
//
// It went unnoticed for one run because I had verified the new tests with
// `--filter FullyQualifiedName~Attendance`, and a filtered run cannot fail on what it does not execute.
//
// ---- IT DEFAULTS TO `Available` WITH ZERO QUANTITIES.
//
// So an existing test that says nothing about attendance behaves exactly as it did before FP-013: no
// overtime, no unpaid absence, and no refusal at approval. A default of `PeriodOpen` would have made every
// approval test fail for a reason none of them is about.
public sealed class StubAttendanceSummary : IAttendanceSummary
{
  public AttendanceSummaryStatus SummaryStatus { get; set; } = AttendanceSummaryStatus.Available;

  public AttendanceSummaryStatus InspectionStatus { get; set; } = AttendanceSummaryStatus.Available;

  public IDictionary<string, decimal> OvertimeByTier { get; } =
    new Dictionary<string, decimal>(StringComparer.Ordinal);

  public decimal UnpaidAbsenceQuantity { get; set; }

  public void Reset()
  {
    SummaryStatus = AttendanceSummaryStatus.Available;
    InspectionStatus = AttendanceSummaryStatus.Available;
    OvertimeByTier.Clear();
    UnpaidAbsenceQuantity = 0m;
  }

  // ---- WORKING DAYS (T-115). Configurable, defaulting to the fixtures' 21.
  //
  // A STUB answers what the test asks it to; the real service reads the company's calendar. Zero is the
  // fail-closed answer, and a test that needs it sets it explicitly.
  public int WorkingDays { get; set; } = 21;

  public Task<int> GetWorkingDaysAsync(
    Guid companyId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default) =>
    Task.FromResult(toDate < fromDate ? 0 : WorkingDays);

  public Task<AttendanceSummaryResult> GetForPeriodAsync(
    Guid companyId, Guid employeeId, DateTimeOffset anyDateInPeriodUtc,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(new AttendanceSummaryResult(
      SummaryStatus, employeeId, companyId, Guid.NewGuid(),
      anyDateInPeriodUtc, anyDateInPeriodUtc,
      WorkedQuantity: 0m,
      new Dictionary<string, decimal>(OvertimeByTier, StringComparer.Ordinal),
      PaidAbsenceQuantity: 0m,
      UnpaidAbsenceQuantity: UnpaidAbsenceQuantity));

  public Task<AttendancePeriodInspection> InspectPeriodAsync(
    Guid companyId, DateTimeOffset anyDateInPeriodUtc, CancellationToken cancellationToken = default) =>
    Task.FromResult(new AttendancePeriodInspection(
      InspectionStatus, Guid.NewGuid(), "Stub period",
      anyDateInPeriodUtc, anyDateInPeriodUtc,
      IsClosed: InspectionStatus == AttendanceSummaryStatus.Available));
}

// ==================================================================================================
// FP-015's TWO PLATFORM-AND-HR FACTS, IN ONE OBJECT (T-088).
// ==================================================================================================
//
// One class implementing both contracts, because the two answers are a chain: a test that set the link
// and forgot the company would produce a DANGLING LINK — a real state, but one that should arrive by
// intent rather than by omission. Here it takes one deliberate line.
//
// Defaults are the ordinary case: the caller is linked to `PayrollApiTestHost.EmployeeId`, who works at
// `CompanyA`. A test wanting the unmapped refusal sets `LinkedEmployee` to null and says so.
public sealed class StubSelfServiceDirectory : IUserEmployeeResolver, IEmployeePlacementDirectory
{
  public Guid? LinkedEmployee { get; set; } = PayrollApiTestHost.EmployeeId;

  public EmployeePlacement? EmployeePlacement { get; set; } =
    new(PayrollApiTestHost.CompanyA, Guid.NewGuid());

  public List<long> AskedForUser { get; } = [];

  public Task<Guid?> ResolveEmployeeIdAsync(long tenantUserId, CancellationToken cancellationToken = default)
  {
    AskedForUser.Add(tenantUserId);
    return Task.FromResult(LinkedEmployee);
  }

  public Task<EmployeePlacement?> GetPlacementAsync(
    Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(employeeId == LinkedEmployee ? EmployeePlacement : null);

  public void Reset()
  {
    LinkedEmployee = PayrollApiTestHost.EmployeeId;
    EmployeePlacement = new(PayrollApiTestHost.CompanyA, Guid.NewGuid());
    AskedForUser.Clear();
  }
}

// ================================================================================================
// THE FOURTH ROUTE OUT OF PAYROLL (T-153).
// ================================================================================================
//
// ---- ⚠ THE DEFAULT IS `FullTime`, AND IT IS A DELIBERATE CHOICE RATHER THAN A CONVENIENCE.
//
// `RecordCompensationCommandHandler` refuses an employee HR cannot resolve, so a stub defaulting to null
// would fail every existing compensation test with `CompensationEmployeeNotInHr` — **and each of those
// failures would be about this stub, not about the endpoint under test.**
//
// `FullTime` is what those tests have always implicitly assumed. **The two interesting answers — null and
// `Contract` — must be asked for by name**, which is what makes a test that sets one visibly about it.
public sealed class StubEmployeeEngagementDirectory : IEmployeeEngagementDirectory
{
  public EmploymentType? EmploymentType { get; set; } = SSAS.HR.Contracts.Employment.EmploymentType.FullTime;

  public void Reset() => EmploymentType = SSAS.HR.Contracts.Employment.EmploymentType.FullTime;

  public Task<EmploymentType?> GetEmploymentTypeAsync(
    Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(EmploymentType);
}

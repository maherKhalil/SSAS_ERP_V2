using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Attendance.Infrastructure.Summaries;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Posting;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.HR.Contracts.Employment;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Application.Runs;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ================================================================================================
// THE SPINE: HR -> ATTENDANCE -> PAYROLL -> GL, END TO END, AGAINST REAL SQL.
// ================================================================================================
//
// **This is the product's thesis as one test**, and until FP-013 it could not be written: `DEC-PAY-0002`
// refused overtime and absence deduction because the input did not exist, so the chain had a hole in the
// middle of it.
//
// Every other suite proves a SEGMENT. Domain tests prove the arithmetic with no database; schema tests
// prove the columns with no behaviour; API tests prove the wire with the cross-module contracts stubbed.
// **None of them can fail if every segment is individually right and a JOIN is wrong** — and the joins are
// where a modular monolith actually breaks, because each side of one is reviewed by somebody looking at a
// single module.
//
// So this drives the REAL services over ONE real context: HR's roster, Attendance's summary contract,
// Payroll's calculator and handlers, and GL's poster. **Nothing between the modules is stubbed.** The only
// doubles are the ambient facts a request would carry — who the caller is, which tenant, which companies
// and branches they may reach — and every one of them is named at the bottom of this file.
//
// ---- WHAT IT ASSERTS THAT NOTHING ELSE DOES.
//
// That six hours of overtime typed in by a supervisor become money in a posted journal, on the account its
// pay element was mapped to, in a journal that balances — and that two days of unpaid absence come back
// out of the same journal. **Those two numbers cross four modules and three contracts to get there.**
public sealed class PayrollChainSqlServerTests
{
  // January 2026: 31 calendar days, so the daily rate divides cleanly and the arithmetic below is legible
  // by inspection rather than by trusting the assertion.
  private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset PeriodEnd = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset PayDate = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

  private const decimal BaseSalary = 3100m;      // 3100 / 31 = 100 per calendar day
  private const decimal OvertimeRate = 25m;      // per hour, at tier NIGHT
  private const decimal OvertimeHours = 6m;      // -> 150
  private const decimal UnpaidAbsenceDays = 2m;  // -> 200 deducted
  private const decimal DailyRate = BaseSalary / 31m;

  // A daily-salaried employee's rate PER DAY (T-114). 100 a day keeps the arithmetic legible: 21 working
  // days less two unpaid is 19, so 1900.
  private const decimal DailySalaryRate = 100m;

  // ================================================================================================
  // A DAILY SALARY, END TO END (T-114). THE HIGHEST-RISK OF THE FOUR.
  // ================================================================================================
  //
  // **Its base arithmetic changed twice this week** — T-108 built it, and T-109 found it double-deducting
  // because the base excluded the unpaid days AND the deduction element took them again. Until now that
  // arithmetic has only ever run in memory.
  //
  // ---- THE NUMBERS, AND THEY ARE THE POINT.
  //
  // January 2026 with a Friday/Saturday weekend: 31 days less five Fridays and five Saturdays = **21
  // working days**. Two of them unpaid, so 19 paid at 100 = **1900**.
  //
  // **And NO absence deduction line.** T-109 ruled the deduction monthly-only precisely because a daily
  // base already prices the absence in the same unit as the rate. **A deduction line appearing here is the
  // T-109 defect returning, and it would be worth 1900/31 x 2 = 122.58 of somebody's money.**
  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public async Task A_daily_salary_is_paid_for_the_periods_working_days_less_the_unpaid_ones()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync(SalaryType.Daily, DailySalaryRate);
    await chain.SeedAttendanceAsync();
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(runId)).IsSuccess);
    Assert.True((await chain.PostAsync(runId)).IsSuccess);

    var run = await chain.RunAsync(runId);
    var lines = run.Lines.ToList();

    var basic = lines.Single(line => line.GlAccountId == chain.SalaryAccountId);
    Assert.Equal(1900m, basic.Amount);

    // Overtime is hours actually worked and is unaffected by the salary type: 6 x 25.
    Assert.Equal(150m, lines.Single(line => line.GlAccountId == chain.OvertimeAccountId).Amount);

    // ---- THE ONE THAT MATTERS: NO DEDUCTION LINE AT ALL.
    Assert.DoesNotContain(lines, line => line.GlAccountId == chain.AbsenceAccountId);
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0022")]
  [Trait("Decision", "DEC-PAY-0002")]
  public async Task Attendance_recorded_by_a_supervisor_becomes_money_in_a_posted_general_ledger_journal()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();          // HR
    await chain.SeedLedgerAsync();            // GL: four accounts and an OPEN period covering the pay date
    await chain.SeedPayrollConfigurationAsync();
    await chain.SeedAttendanceAsync();        // six NIGHT overtime hours, two unpaid absence days

    // ================================================================================================
    // THE GATE FIRST: PAYROLL REFUSES AN OPEN ATTENDANCE PERIOD (OD-ATT-0010).
    // ================================================================================================
    //
    // Asserted BEFORE the happy path, because a chain test that only ever ran the happy path would pass
    // just as well with the gate deleted.
    var runId = await chain.CreateAndCalculateRunAsync();

    var premature = await chain.ApproveAsync(runId);

    Assert.True(premature.IsFailure);
    Assert.Equal("Payroll.AttendancePeriodOpen", premature.Error.Code);

    // ---- CLOSE THE PERIOD. The numbers stop moving, and only now may they be approved.
    await chain.CloseAttendancePeriodAsync();

    // Recalculate. The first calculation ran against an OPEN period, so `IAttendanceSummary` answered
    // `PeriodOpen` and it carried no attendance-driven lines at all. That is by design — calculation
    // commits nothing and may be repeated — and it is exactly why the refusal sits at approval.
    await chain.CalculateAsync(runId);

    var approved = await chain.ApproveAsync(runId);
    Assert.True(approved.IsSuccess, approved.IsFailure ? approved.Error.Message : string.Empty);

    var posted = await chain.PostAsync(runId);
    Assert.True(posted.IsSuccess, posted.IsFailure ? posted.Error.Message : string.Empty);

    // ================================================================================================
    // A REAL JOURNAL, IN THE REAL LEDGER.
    // ================================================================================================
    var journal = await chain.PostedJournalAsync(runId);

    // ---- IT BALANCES (BR-GL-0001), enforced by `JournalDraft.EnsurePostable` — the same code a
    // user-posted journal passes through, because `GlJournalPoster` REUSES posting rather than
    // reimplementing it.
    Assert.Equal(journal.Lines.Sum(line => line.Debit), journal.Lines.Sum(line => line.Credit));
    Assert.True(journal.Lines.Sum(line => line.Debit) > 0m);

    // ---- THE OVERTIME, ON THE ACCOUNT ITS ELEMENT WAS MAPPED TO.
    //
    // 6 x 25 = 150. This number started as a supervisor recording "6" against a date and travelled through
    // `IAttendanceSummary`, `PayrollCalculator` and `IJournalPoster` to arrive here.
    var overtimeLine = Assert.Single(journal.Lines.Where(line => line.AccountId == chain.OvertimeAccountId));
    Assert.Equal(OvertimeHours * OvertimeRate, overtimeLine.Debit);

    // ---- THE UNPAID ABSENCE, DEDUCTED, ON ITS OWN ACCOUNT.
    //
    // 3100 / 31 CALENDAR days = 100/day; two days = 200. The calendar-day divisor is `OD-ATT-0015`'s
    // ruling — proration was left unchanged — so a day of absence and a day of proration are worth the
    // same. A working-day divisor here would make them disagree.
    var absenceLine = Assert.Single(journal.Lines.Where(line => line.AccountId == chain.AbsenceAccountId));
    Assert.Equal(DailyRate * UnpaidAbsenceDays, absenceLine.Credit);

    // ---- AND THE SALARY LINE IS THE FULL BASE, UNPRORATED.
    //
    // The employee was employed the whole period; absence is a DEDUCTION line, not a smaller salary.
    // Encoding it as reduced salary would leave the payslip unable to show what was withheld.
    var salaryLine = Assert.Single(journal.Lines.Where(line => line.AccountId == chain.SalaryAccountId));
    Assert.Equal(BaseSalary, salaryLine.Debit);

    // ---- NET PAY IS THE BALANCING CREDIT, AND IT CARRIES BOTH ATTENDANCE FACTS.
    //
    // 3100 + 150 - 200 = 3050. If either number had failed to cross a module boundary this is the figure
    // that would be wrong — and it is the figure a person is actually paid.
    var expectedNet = BaseSalary + (OvertimeHours * OvertimeRate) - (DailyRate * UnpaidAbsenceDays);
    var netPayLine = Assert.Single(journal.Lines.Where(line => line.AccountId == chain.NetPayAccountId));
    Assert.Equal(expectedNet, netPayLine.Credit);

    // ---- THE RUN AND THE JOURNAL AGREE.
    //
    // Two independently derived views of the same money, computed by different code in different modules.
    // Agreement is a real assertion rather than a tautology.
    var run = await chain.RunAsync(runId);
    Assert.Equal(expectedNet, run.NetPay);
    Assert.Equal(BaseSalary + (OvertimeHours * OvertimeRate), run.TotalEarnings);
    Assert.Equal(DailyRate * UnpaidAbsenceDays, run.TotalDeductions);
    Assert.Equal(journal.Id, run.JournalEntryId);
  }

  // ---- AND WHAT THE CHAIN POSTED IS APPEND-ONLY.
  //
  // Separate from the chain test so a failure here reads as "the ledger is mutable" rather than as "the
  // chain is broken". They are different claims and both matter.
  [Fact]
  [Trait("Decision", "DEC-ATT-0009")]
  public async Task The_journal_the_chain_posted_cannot_afterwards_be_modified()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync();
    await chain.SeedAttendanceAsync();
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(runId)).IsSuccess);
    Assert.True((await chain.PostAsync(runId)).IsSuccess);

    var journal = await chain.PostedJournalAsync(runId);

    await using var context = chain.CreateContext();
    var tracked = await context.Set<JournalEntry>().FirstAsync(entry => entry.Id == journal.Id);

    // Nothing on `JournalEntry` has a public setter -- the aggregate is properly sealed -- so the mutation
    // is forced at the tracker. That is stricter than editing a property would be: it proves the refusal
    // comes from the WRITE BOUNDARY rather than from the absence of a setter, which is the claim.
    context.Entry(tracked).State = EntityState.Modified;

    await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
  }

  // ================================================================================================
  // THE FIXTURE. ONE DATABASE, ALL FOUR CONTRIBUTORS, THE REAL SERVICES.
  // ================================================================================================
  private sealed class ChainFixture : IAsyncDisposable
  {
    private const string Actor = "fp013-chain-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string catalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid Company { get; } = Guid.NewGuid();

    public Guid BranchId { get; } = Guid.NewGuid();

    public Guid Employee { get; } = Guid.NewGuid();

    public Guid SalaryAccountId { get; private set; }

    public Guid OvertimeAccountId { get; private set; }

    public Guid AbsenceAccountId { get; private set; }

    public Guid NetPayAccountId { get; private set; }

    public Guid PayrollPeriodId { get; private set; }

    public Guid AttendancePeriodId { get; private set; }

    public static async Task<ChainFixture> CreateAsync()
    {
      var fixture = new ChainFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    // ---- ALL FOUR CONTRIBUTORS, BECAUSE THE CHAIN CROSSES ALL FOUR MODULES.
    //
    // Every other integration fixture composes ONE module's contributor, correctly: each proves something
    // about one schema, and adding the others would put irrelevant tables into its assertions. This one
    // cannot, and that is the whole point of it. It reuses `CutoverTenantModel.Contributors` rather than
    // building a fifth list — the same reasoning that file already records about lists that drift.
    public TenantDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(catalog))
        .Options;

      return new TenantDbContext(
        options, new FixtureUser([]), new FixtureTenant(Tenant), new FixtureClock(),
        branchAuthorizer: new GrantingBranch(BranchId),
        companyAuthorizer: new GrantingCompany(Company),
        modelContributors: CutoverTenantModel.Contributors);
    }

    // ---- HR, SEEDED THROUGH SQL, as every fixture outside HR seeds an employee.
    //
    // The chain then reads it through the REAL `IEmployeeRoster`, so what is proved is the contract rather
    // than the insert. `EmploymentDate` predates the period so nothing is prorated — proration has its own
    // tests, and mixing it in here would make the expected numbers a second calculation to check.
    public Task SeedEmployeeAsync()
    {
      var department = Guid.NewGuid();
      var position = Guid.NewGuid();

      return ExecuteAsync($"""
        INSERT INTO [tenant].[Departments]
          ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [NormalizedName],
           [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{department}', '{Tenant}', '{Company}', N'CHAIN', N'CHAIN', N'Chain', N'CHAIN',
           NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');

        INSERT INTO [tenant].[Positions]
          ([PositionId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Title], [NormalizedTitle],
           [JobGradeId], [Status], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{position}', '{Tenant}', '{Company}', N'CHAIN', N'CHAIN', N'Chain', N'CHAIN',
           NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');

        INSERT INTO [tenant].[Employees]
          ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [PositionId],
           [EmployeeNumber], [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [Status],
           [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{Employee}', '{Tenant}', '{Company}', '{BranchId}', '{department}', '{position}',
           N'CHAIN-1', N'CHAIN-1', N'Chain Person', '2020-01-01T00:00:00+00:00', N'Active',
           N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);
    }

    // ---- GL. Four accounts, and a fiscal year whose period covering the pay date is OPEN.
    public async Task SeedLedgerAsync()
    {
      await using var context = CreateContext();

      var salary = Account.Create("5100", "Salary Expense").Value;
      var overtime = Account.Create("5110", "Overtime Expense").Value;
      var absence = Account.Create("5120", "Unpaid Absence Recovery").Value;
      var netPay = Account.Create("2100", "Net Pay Payable").Value;

      foreach (var account in new[] { salary, overtime, absence, netPay })
      {
        context.Set<Account>().Add(account);
      }

      // One period spanning the whole year, so the pay date resolves without a month-boundary question
      // this test is not about. `FiscalYear.Create` requires the periods to partition the year exactly.
      var year = FiscalYear.Create(
        "FY2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
        [("FY2026-P1",
          new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
          new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero))]).Value;

      year.CompanyId = Company;
      context.Set<FiscalYear>().Add(year);

      await context.SaveChangesAsync();

      SalaryAccountId = salary.Id;
      OvertimeAccountId = overtime.Id;
      AbsenceAccountId = absence.Id;
      NetPayAccountId = netPay.Id;
    }

    // ---- PAYROLL. Four elements mapped to those accounts, plus the employee's compensation.
    // ---- THE SALARY TYPE IS A PARAMETER, DEFAULTED (T-114).
    //
    // Defaulted to `Monthly` so the two tests written before T-107 are untouched and their arithmetic is
    // unchanged — a spine that had to be edited to add a case would not be proving the same thing
    // afterwards.
    public async Task SeedPayrollConfigurationAsync(
      SalaryType salaryType = SalaryType.Monthly, decimal? baseAmount = null)
    {
      await using var context = CreateContext();

      var basic = Element("BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, 0m, 0, SalaryAccountId);

      var overtime = Element(
        "OT-NIGHT", PayElementKind.Earning, PayElementBehaviour.OvertimeHourly, OvertimeRate, 10, OvertimeAccountId);
      Assert.True(overtime.SetOvertimeTier("NIGHT").IsSuccess);

      var absence = Element(
        "UNPAID", PayElementKind.Deduction, PayElementBehaviour.UnpaidAbsenceDeduction, 0m, 50, AbsenceAccountId);

      var netPay = Element(
        "NETPAY", PayElementKind.Deduction, PayElementBehaviour.NetPayPayable, 0m, 99, NetPayAccountId);

      foreach (var element in new[] { basic, overtime, absence, netPay })
      {
        context.Set<PayElement>().Add(element);
      }

      // ---- ASSIGNED TO OVERTIME, AND DELIBERATELY NOT TO THE ABSENCE DEDUCTION.
      //
      // Overtime eligibility is a real per-employee decision, so it needs an assignment. The absence
      // deduction is EXEMPT from that requirement, and this seed is what proves the exemption end to end:
      // without it, an employee nobody remembered to assign would have their unpaid leave silently go
      // undeducted, and every number on the payslip would still look right.
      var compensation = EmployeeCompensation.Create(
        Company, Employee, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        baseAmount ?? BaseSalary, [(overtime.Id, (decimal?)null)], salaryType).Value;
      context.Set<EmployeeCompensation>().Add(compensation);

      var period = PayrollPeriod.CreateAlignedTo(
        Company, Guid.NewGuid(), "January 2026", PeriodStart, PeriodEnd, PayDate).Value;
      context.Set<PayrollPeriod>().Add(period);

      await context.SaveChangesAsync();

      PayrollPeriodId = period.Id;
    }

    private PayElement Element(
      string code, PayElementKind kind, PayElementBehaviour behaviour, decimal rate, int order, Guid account)
    {
      var element = PayElement.Create(Company, code, code, kind, behaviour, rate, order).Value;
      Assert.True(element.MapToAccount(account).IsSuccess);
      return element;
    }

    // ---- ATTENDANCE. A calendar, an open period, and the two facts the chain carries.
    public async Task SeedAttendanceAsync()
    {
      await using var context = CreateContext();

      var calendar = WorkingCalendar.Create(
        Company, "Standard", [DayOfWeek.Friday, DayOfWeek.Saturday], isDefault: true).Value;
      context.Set<WorkingCalendar>().Add(calendar);

      var period = AttendancePeriod.Create(
        Company, "January 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)).Value;
      context.Set<AttendancePeriod>().Add(period);

      // Two separate observations, exactly as a supervisor would enter them: a day with overtime, and a
      // day of unpaid absence.
      var worked = AttendanceRecord.Observe(
        Company, period.Id, Employee, new DateOnly(2026, 1, 14),
        workedQuantity: 8m, overtimeQuantity: OvertimeHours, overtimeTier: "NIGHT",
        paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: 0m, note: null).Value;

      var absent = AttendanceRecord.Observe(
        Company, period.Id, Employee, new DateOnly(2026, 1, 20),
        workedQuantity: 0m, overtimeQuantity: 0m, overtimeTier: null,
        paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: UnpaidAbsenceDays, note: "Unpaid leave").Value;

      foreach (var record in new[] { worked, absent })
      {
        // The write boundary stamps this in production, from the execution context. The fixture supplies it
        // because no branch context exists here — stated so nobody reads it as the application's path.
        record.BranchId = BranchId;
        context.Set<AttendanceRecord>().Add(record);
      }

      await context.SaveChangesAsync();

      AttendancePeriodId = period.Id;
    }

    public async Task CloseAttendancePeriodAsync()
    {
      await using var context = CreateContext();

      var period = await context.Set<AttendancePeriod>().FirstAsync(row => row.Id == AttendancePeriodId);
      Assert.True(period.Close(Actor, DateTimeOffset.UtcNow).IsSuccess);

      await context.SaveChangesAsync();
    }

    public async Task<Guid> CreateAndCalculateRunAsync()
    {
      Guid runId;

      await using (var context = CreateContext())
      {
        var run = PayrollRun.Create(Company, PayrollPeriodId).Value;
        context.Set<PayrollRun>().Add(run);
        await context.SaveChangesAsync();
        runId = run.Id;
      }

      await CalculateAsync(runId);
      return runId;
    }

    // ================================================================================================
    // THE REAL HANDLERS OVER THE REAL SERVICES. NOTHING BETWEEN THE MODULES IS STUBBED.
    // ================================================================================================
    public async Task CalculateAsync(Guid runId)
    {
      await using var graph = new ChainGraph(this);

      var calculated = await graph.Calculate.HandleAsync(new CalculatePayrollRunCommand(runId));
      Assert.True(calculated.IsSuccess, calculated.IsFailure ? calculated.Error.Message : string.Empty);
    }

    public async Task<Result> ApproveAsync(Guid runId)
    {
      await using var graph = new ChainGraph(this);
      return await graph.Approve.HandleAsync(new ApprovePayrollRunCommand(runId));
    }

    public async Task<Result> PostAsync(Guid runId)
    {
      await using var graph = new ChainGraph(this);
      return await graph.Post.HandleAsync(new PostPayrollRunCommand(runId));
    }

    public async Task<JournalEntry> PostedJournalAsync(Guid runId)
    {
      await using var context = CreateContext();

      var run = await context.Set<PayrollRun>().AsNoTracking().FirstAsync(row => row.Id == runId);
      Assert.NotNull(run.JournalEntryId);

      return await context.Set<JournalEntry>()
        .AsNoTracking()
        .Include(entry => entry.Lines)
        .FirstAsync(entry => entry.Id == run.JournalEntryId!.Value);
    }

    public async Task<PayrollRun> RunAsync(Guid runId)
    {
      await using var context = CreateContext();

      return await context.Set<PayrollRun>()
        .AsNoTracking()
        .Include(row => row.Lines)
        .FirstAsync(row => row.Id == runId);
    }

    // The composed graph, over ONE context per operation — which is how the application does it too: a
    // handler runs inside one unit of work.
    private sealed class ChainGraph : IAsyncDisposable
    {
      private readonly TenantDbContext context;

      public ChainGraph(ChainFixture fixture)
      {
        context = fixture.CreateContext();

        var accessor = new SingleContext(context);
        var unitOfWork = new SingleContextUnitOfWork(context);
        var currentTenant = new FixtureTenant(fixture.Tenant);
        var currentTenantUser = new FixtureTenantUser();
        var companyAccess = new GrantingCompanyAccess(fixture.Company);

        // Every payroll permission, because this test is about the CHAIN and not about authorization, which
        // has its own suites. The company boundary is still the real one: the write boundary authorizes
        // every company-owned save against the trusted execution context regardless of what is granted here.
        var currentUser = new FixtureUser(
        [
          PayrollPermissionNames.ManageRuns,
          PayrollPermissionNames.ApproveRuns,
          PayrollPermissionNames.PostRuns
        ]);

        var scope = new PayrollScopeResolver(companyAccess, currentTenant, currentTenantUser, currentUser);

        // ---- THE THREE CROSS-MODULE CONTRACTS, ALL REAL.
        //
        // If any one of these were stubbed, this test would be proving the segments again rather than the
        // joins, and there would be no reason for it to exist.
        IEmployeeRoster roster = new EmployeeRosterService(
          accessor, companyAccess, currentTenant, currentTenantUser);

        // T-113: `IWorkingCalendarRepository` is T-108's addition — the summary carries the period's
        // standard working days, which a daily salary is priced against.
        IAttendanceSummary attendance = new AttendanceSummaryService(
          accessor, companyAccess, currentTenant, currentTenantUser,
          new WorkingCalendarRepository(accessor));

        var ledger = new GlJournalPoster(
          new JournalEntryRepository(accessor), new AccountRepository(accessor),
          new FiscalCalendarRepository(accessor), unitOfWork);

        var runs = new PayrollRunRepository(accessor);
        var periods = new PayrollPeriodRepository(accessor);
        var elements = new PayElementRepository(accessor);
        var compensation = new EmployeeCompensationRepository(accessor);

        // T-113: `IOneOffPaymentRepository` is T-110's addition — a one-off instruction makes an employee
        // with no compensation payable, and approval is what consumes it.
        var oneOffPayments = new OneOffPaymentRepository(accessor);

        Calculate = new CalculatePayrollRunCommandHandler(
          runs, periods, elements, compensation, oneOffPayments, roster, attendance, scope, unitOfWork,
          currentUser);

        Approve = new ApprovePayrollRunCommandHandler(
          runs, periods, elements, oneOffPayments, ledger, attendance, scope, unitOfWork, currentUser);

        Post = new PostPayrollRunCommandHandler(
          runs, periods, elements, ledger, scope, unitOfWork, currentUser);
      }

      public CalculatePayrollRunCommandHandler Calculate { get; }

      public ApprovePayrollRunCommandHandler Approve { get; }

      public PostPayrollRunCommandHandler Post { get; }

      public async ValueTask DisposeAsync() => await context.DisposeAsync();
    }

    private async Task InitializeAsync()
    {
      catalog = $"SSAS_FP013_Chain_{token}";

      await MasterAsync($"CREATE DATABASE [{catalog}]");
      await MigrateAsync();
      await SeedCompanyAsync();
      await SeedBranchAsync();
    }

    private async Task MigrateAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));

      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;

      await using var context = new TenantDbContext(
        options, new FixtureUser([]), new FixtureTenant(Tenant), new FixtureClock(),
        modelContributors: CutoverTenantModel.Contributors);

      await context.Database.MigrateAsync();
    }

    // Status and StatusChangeReasonCode are STRINGS and the timestamps are SYSDATETIMEOFFSET. FP-012's
    // first attempt guessed integers and SYSUTCDATETIME and `CK_Companies_Status` refused it during setup,
    // which reads as an environment problem rather than the fixture bug it is.
    private Task SeedCompanyAsync() =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[Companies]
          ([CompanyId], [TenantId], [CompanyCode], [NormalizedCompanyCode], [CompanyName],
           [BaseCurrencyCode], [Status], [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{Company}', '{Tenant}', N'CHAIN', N'CHAIN', N'Chain Company',
           'SAR', N'Active', N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    // Branches carries `IsActive`, a plain bit — NOT the Status/StatusChangedUtc/StatusChangedBy triple
    // Companies uses. The two tables look alike and are not, and guessing cost FP-013 eight setup failures.
    private Task SeedBranchAsync() =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[Branches]
          ([BranchId], [TenantId], [BranchCode], [NormalizedBranchCode], [BranchName],
           [IsMainBranch], [IsActive], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{BranchId}', '{Tenant}', N'BR1', N'BR1', N'Branch One',
           1, 1, SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    private async Task ExecuteAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    private static async Task MasterAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    private static string ConnectionFor(string name) => IntegrationSqlEnvironment.ForCatalog(name);

    public async ValueTask DisposeAsync()
    {
      if (string.IsNullOrEmpty(catalog))
      {
        return;
      }

      await MasterAsync($"""
        IF DB_ID('{catalog}') IS NOT NULL
        BEGIN
          ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
          DROP DATABASE [{catalog}];
        END
        """);
    }

    // ================================================================================================
    // THE ONLY DOUBLES: THE AMBIENT FACTS A REQUEST WOULD CARRY.
    // ================================================================================================
    //
    // Who the caller is, which tenant, which companies and branches they may reach. Every one is supplied
    // by the Host from a token and a session, and no fixture has either.
    //
    // **NOTHING BETWEEN THE MODULES IS STUBBED** — the roster, the summary contract and the ledger poster
    // are all the production types.
    //
    // ---- NO ENTITY BELOW SETS TenantId, AND THAT IS NOT AN OVERSIGHT.
    //
    // `PersistenceDbContext.ApplyPersistenceRules` STAMPS `TenantId` on every Added `ITenantOwnedEntity`
    // from the trusted context, and REFUSES a Modified entity whose `TenantId` changed. Assigning it in the
    // fixture is at best redundant and at worst the thing that trips that refusal on a later save. The
    // first version of this fixture assigned it everywhere and failed on exactly that.
    private sealed class FixtureUser(IReadOnlyCollection<string> permissions) : ICurrentUser
    {
      public string? UserId => Actor;

      public string? UserName => Actor;

      public string? Email => null;

      public Guid? CompanyId => null;

      public string? SessionId => null;

      public string? TokenId => null;

      public IReadOnlyCollection<string> Roles => [];

      public IReadOnlyCollection<string> Permissions => permissions;
    }

    private sealed class FixtureTenant(Guid tenantId) : ICurrentTenant
    {
      public Guid? TenantId => tenantId;
    }

    private sealed class FixtureTenantUser : ICurrentTenantUser
    {
      public long? TenantUserId => 1;
    }

    private sealed class FixtureClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
    {
      public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DbContext>(context);
    }

    // Mirrors `TenantUnitOfWork`'s translation, per the standing rule that a double translating fewer
    // failures than the type it stands in for makes every test above it assert behaviour the Host lacks.
    private sealed class SingleContextUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
    {
      public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
      {
        try
        {
          return Result.Success(await context.SaveChangesAsync(cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
          return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception)
          when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
          return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.UniqueConstraintViolation);
        }
        catch (DbUpdateException)
        {
          return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.WriteFailure);
        }
      }

      public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new EfTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

      private sealed class EfTransaction(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : ITransaction
      {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
          transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
          transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
      }
    }

    private sealed class GrantingCompanyAccess(Guid permitted)
      : SSAS.BuildingBlocks.Tenancy.Companies.ITenantCompanyAccessResolver
    {
      public Task<Result<IReadOnlyList<SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary>>>
        GetPermittedCompaniesAsync(
          Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(
          Result.Success<IReadOnlyList<SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary>>(
            [new SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary(
              permitted, "CHAIN", "Chain Company")]));

      public Task<Result> AuthorizeCompanyAsync(
        Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(companyId == permitted
          ? Result.Success()
          : Result.Failure(new Error("Company.Denied", "Denied.")));
    }

    private sealed class GrantingCompany(Guid companyId) : ICompanyWriteAuthorizer
    {
      public Task<Result<Guid>> AuthorizeCurrentCompanyAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(companyId));
    }

    private sealed class GrantingBranch(Guid branchId) : IBranchWriteAuthorizer
    {
      public Task<Result<Guid>> AuthorizeCurrentBranchAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(branchId));
    }
  }
}

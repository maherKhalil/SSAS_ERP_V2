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

using SSAS.TestSupport.CutoverModel;

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

  // An hourly employee's rate PER HOUR (T-114). The fixture's supervisor recorded 8 worked hours, so 8 x 25
  // = 200 — and the SAME 25 is the overtime rate, which is what makes the two lines distinguishable only by
  // the quantity each is priced against.
  private const decimal HourlySalaryRate = 25m;

  // ================================================================================================
  // A DAILY-RATE JOINER (T-115). THE DEFECT THE OTHER FOUR GREENS COULD NOT SEE.
  // ================================================================================================
  //
  // **A daily employee is paid for working days they were EMPLOYED for.** Before T-115 the daily arm read
  // the PERIOD's working-day total and consulted employment dates nowhere at all — hourly reads observed
  // hours and monthly prorates, and daily did neither.
  //
  // **Hired on the 16th of a 21-working-day month:** 10 working days remain (the 16th and 17th are the
  // Friday and Saturday weekend), two of them unpaid, so **8 x 100 = 800.** Before T-115 this employee was
  // paid 21 - 2 = 19 days: **1900, an overpayment of 1100 for days they were not employed.**
  //
  // **The four T-114 greens could not have caught this**, because the fixture's employee is hired in 2020
  // and a full-period employee's clamped window is the whole period.
  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public async Task A_daily_rate_joiner_is_paid_only_for_the_working_days_they_were_employed_for()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync("2026-01-16T00:00:00+00:00");
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync(SalaryType.Daily, DailySalaryRate);

    // T-121: the 19th rather than the 14th. Hired on the 16th, so work on the 14th is a CONTRADICTION and
    // the run would refuse — which is the rule working, and it found this fixture's own inconsistency.
    await chain.SeedAttendanceAsync(workedOn: new DateOnly(2026, 1, 19));
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(runId)).IsSuccess);
    Assert.True((await chain.PostAsync(runId)).IsSuccess);

    var lines = (await chain.RunAsync(runId)).Lines.ToList();

    Assert.Equal(800m, lines.Single(line => line.GlAccountId == chain.SalaryAccountId).Amount);
    Assert.DoesNotContain(lines, line => line.GlAccountId == chain.AbsenceAccountId);
  }

  // ================================================================================================
  // WORK RECORDED AFTER TERMINATION REFUSES THE RUN (T-121).
  // ================================================================================================
  //
  // **T-119 dropped ABSENCE outside the employment window and this refuses WORK outside it, and the
  // asymmetry is the whole point.** Absence outside employment is noise — a stale record for somebody who
  // had left. **Work outside employment means one of two facts is wrong**: either they worked, so the
  // termination date is wrong, or they did not, so the record is.
  //
  // **Counting overpays if the record is wrong; dropping underpays if the date is wrong.** Neither module
  // can tell which, so neither chooses. **A refusal is found by an operator with the employee named; both
  // silent answers are found by somebody reading their own payslip.**
  [Fact]
  [Trait("Decision", "OD-PAY-0010")]
  public async Task Work_recorded_after_termination_refuses_the_run_rather_than_guessing()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync(terminationDate: "2026-01-15T00:00:00+00:00");
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync();
    await chain.SeedAttendanceAsync(workAfterTermination: true);
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateRunAsync();

    var calculated = await chain.TryCalculateAsync(runId);

    Assert.True(calculated.IsFailure);
    Assert.Equal(PayrollErrors.AttendanceContradictsEmployment.Code, calculated.Error.Code);
  }

  // ================================================================================================
  // A DAILY-RATE LEAVER (T-116). THE LAST UNTESTED ARM OF AN EXPRESSION THAT HAS BEEN WRONG TWICE.
  // ================================================================================================
  //
  // T-115 fixed the joiner and left the leaver **correct by construction** — `min(termination, period end)`
  // is in the same expression — **but asserted by reasoning rather than by a run.** That is exactly the
  // shape T-114 left, and within the hour it turned out to be a 1100-per-employee defect.
  //
  // **"Correct by construction" is also what `StandardWorkingDays` was**, two tasks before it turned out to
  // be the wrong quantity. The reasoning was sound each time.
  //
  // **Terminated on 15 January: 11 working days from the 1st to the 15th, at 100 = 1100.**
  //
  // ---- AND THE TWO UNPAID DAYS DO NOT DEDUCT, WHICH IS T-119 AND WAS T-116's FINDING.
  //
  // They are recorded on the 20th — five days after this employee left. **T-116 asserted 900 and passed**,
  // because T-115 clamped the working-day count and left the absence quantity unbounded, so a leaver was
  // deducted for absence on days they were not employed.
  //
  // **That assertion failing against T-119 is the proof the old behaviour deducted**: `Expected: 900,
  // Actual: 1100.0000`. The plant for this fix is the previous version of this line.
  //
  // Before T-115 the same employee was paid the whole period: **1900.**
  [Fact]
  [Trait("Decision", "OD-PAY-0010")]
  public async Task A_daily_rate_leaver_is_paid_only_to_their_termination()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync(terminationDate: "2026-01-15T00:00:00+00:00");
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync(SalaryType.Daily, DailySalaryRate);
    await chain.SeedAttendanceAsync();
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(runId)).IsSuccess);
    Assert.True((await chain.PostAsync(runId)).IsSuccess);

    var lines = (await chain.RunAsync(runId)).Lines.ToList();

    Assert.Equal(1100m, lines.Single(line => line.GlAccountId == chain.SalaryAccountId).Amount);
    Assert.DoesNotContain(lines, line => line.GlAccountId == chain.AbsenceAccountId);
  }

  // ================================================================================================
  // AND THE REFUSAL THAT HAD TO SURVIVE THE FIX (T-115).
  // ================================================================================================
  //
  // **T-115 moved the working-day count from the attendance summary to a call on the company's calendar,
  // and those two fail differently.** The summary knows whether THIS EMPLOYEE's attendance arrived; the
  // calendar does not and would happily answer 21 for someone whose summary was unavailable — while
  // `UnpaidAbsenceQuantity` stayed zero.
  //
  // **A daily employee would have gone from REFUSED to PAID IN FULL with no absence deduction, silently.**
  // That is the same failure class T-115 exists to remove, moved from the joiner path to the
  // summary-unavailable path — and it would have converted a deliberate, documented, VISIBLE refusal into
  // an invisible overpayment.
  //
  // Here the attendance period is left OPEN, so the summary reports `PeriodOpen` and never becomes
  // `Available`.
  [Fact]
  [Trait("Decision", "OD-ATT-0010")]
  public async Task A_daily_salary_is_refused_when_the_attendance_summary_did_not_arrive()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync(SalaryType.Daily, DailySalaryRate);
    await chain.SeedAttendanceAsync();

    // DELIBERATELY NOT CLOSED.
    var runId = await chain.CreateRunAsync();

    var calculated = await chain.TryCalculateAsync(runId);

    Assert.True(calculated.IsFailure);
    Assert.Equal(PayrollErrors.DailySalaryHasNoWorkingDays.Code, calculated.Error.Code);
  }

  // ================================================================================================
  // AN HOURLY SALARY (T-114). THE QUANTITY IS THE ADJUSTMENT.
  // ================================================================================================
  //
  // Rate times hours attended, with **no proration and no absence deduction** — the owner's ruling being
  // that an hourly employee is paid only for the time they attend, so the worked quantity has already
  // accounted for everything the other two adjustments would apply.
  //
  // **The fixture records two unpaid absence days, and that is what makes this test worth running.** A
  // deduction line appearing here would be the double-count T-107 excluded hourly from, priced against a
  // CALENDAR-day divisor that means nothing at all against an hourly rate.
  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public async Task An_hourly_salary_is_the_rate_times_hours_attended_and_takes_no_absence_deduction()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync(SalaryType.Hourly, HourlySalaryRate);
    await chain.SeedAttendanceAsync();
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(runId)).IsSuccess);
    Assert.True((await chain.PostAsync(runId)).IsSuccess);

    var lines = (await chain.RunAsync(runId)).Lines.ToList();

    // 8 hours attended at 25.
    Assert.Equal(200m, lines.Single(line => line.GlAccountId == chain.SalaryAccountId).Amount);

    // Overtime is priced on its own quantity: 6 hours at the same 25.
    Assert.Equal(150m, lines.Single(line => line.GlAccountId == chain.OvertimeAccountId).Amount);

    // ---- TWO UNPAID DAYS RECORDED, AND NO DEDUCTION LINE.
    Assert.DoesNotContain(lines, line => line.GlAccountId == chain.AbsenceAccountId);
  }

  // ================================================================================================
  // A ONE-OFF SURVIVES THE REVERSAL OF THE RUN THAT PAID IT (T-123).
  // ================================================================================================
  //
  // **T-110 ruled this predicate and could not implement it.** A reversal wrote nothing on the run, so
  // *"an APPROVED, UNREVERSED run holds this"* was not a question Payroll's data could answer, and the
  // instruction was recorded as consumed unconditionally — **stranding an unpaid obligation the moment its
  // run was reversed, which is extinguishing a debt by an accounting action.**
  //
  // **T-112 added `ReversedUtc` and T-123 made the predicate expressible.** This is the ruling arriving,
  // three tasks after it was made, and it is the first end-to-end proof of it.
  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public async Task A_one_off_is_payable_again_after_the_run_that_paid_it_is_reversed()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync();
    await chain.SeedAttendanceAsync();
    await chain.SeedOneOffPayeeAsync(4000m);
    await chain.CloseAttendancePeriodAsync();

    // ---- THE FIRST RUN PAYS IT.
    var first = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(first)).IsSuccess);
    Assert.True((await chain.PostAsync(first)).IsSuccess);

    Assert.Equal(4000m, (await chain.RunAsync(first)).Lines
      .Single(line => line.EmployeeId == chain.OneOffPayee).Amount);

    // ---- REVERSED, THROUGH THE REAL HANDLER AND THE REAL LEDGER.
    Assert.True((await chain.ReverseAsync(first)).IsSuccess);

    // ---- AND THE CORRECTION PAYS IT AGAIN. The obligation was never discharged.
    var second = await chain.CreateRunAsync();
    Assert.True((await chain.TryCalculateAsync(second)).IsSuccess);
    Assert.True((await chain.ApproveAsync(second)).IsSuccess);

    Assert.Equal(4000m, (await chain.RunAsync(second)).Lines
      .Single(line => line.EmployeeId == chain.OneOffPayee).Amount);
  }

  // ================================================================================================
  // REVERSE AND RERUN (T-114). T-112's FILTERED UNIQUE INDEX, AGAINST A REAL SERVER.
  // ================================================================================================
  //
  // **A filtered unique index is exactly the sort of thing that behaves differently on a real server than
  // in anyone's head**, and this one has never been exercised. `OD-PAY-0011` ruled reverse-and-rerun; the
  // constraint refused the rerun half from the day it was written, because it matched any run in any state.
  //
  // **Both halves are asserted, and the second is what keeps the fix honest:** a period whose run was
  // reversed accepts another, and a period with a LIVE run still refuses one. A change that only opened the
  // first would have re-admitted the two-live-runs case option 3 was rejected for.
  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public async Task A_reversed_period_accepts_another_run_and_a_live_one_still_does_not()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync();
    await chain.SeedAttendanceAsync();
    await chain.CloseAttendancePeriodAsync();

    var first = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(first)).IsSuccess);
    Assert.True((await chain.PostAsync(first)).IsSuccess);

    // ---- WHILE THE FIRST RUN IS LIVE, A SECOND IS REFUSED. The half that must not regress.
    Assert.True((await chain.TryCreateRunAsync()).IsFailure);

    // ---- REVERSE IT, THROUGH THE REAL HANDLER AND THE REAL LEDGER.
    Assert.True((await chain.ReverseAsync(first)).IsSuccess);
    Assert.True((await chain.RunAsync(first)).IsReversed);

    // ---- AND NOW THE PERIOD ACCEPTS A CORRECTION. The half that never worked.
    var second = await chain.TryCreateRunAsync();
    Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Message : string.Empty);

    // ---- AND THAT SECOND RUN IS ITSELF LIVE, so a THIRD is refused again.
    Assert.True((await chain.TryCreateRunAsync()).IsFailure);
  }

  // ================================================================================================
  // THE PERSON THE RUN USED TO DROP (T-114). T-110's ENTIRE REASON FOR EXISTING.
  // ================================================================================================
  //
  // An employee with a one-off instruction and **no compensation record at all**. Before T-110 the run
  // skipped them — `!byEmployee.TryGetValue(...) continue` — producing no line, no error and no payslip.
  // `OD-SS-0003` says such a person IS an employee, so HR's roster always carried them; only the
  // compensation lookup dropped them.
  //
  // **This is also the first time `OneOffPayment` has been saved to a real database.** T-113's guard caught
  // its key generation being wrong four tasks after it shipped, because nothing had ever persisted one.
  [Fact]
  [Trait("Decision", "OD-SS-0003")]
  public async Task An_employee_with_a_one_off_and_no_compensation_is_paid_rather_than_dropped()
  {
    await using var chain = await ChainFixture.CreateAsync();

    await chain.SeedEmployeeAsync();
    await chain.SeedLedgerAsync();
    await chain.SeedPayrollConfigurationAsync();
    await chain.SeedAttendanceAsync();
    await chain.SeedOneOffPayeeAsync(4000m);
    await chain.CloseAttendancePeriodAsync();

    var runId = await chain.CreateAndCalculateRunAsync();
    Assert.True((await chain.ApproveAsync(runId)).IsSuccess);
    Assert.True((await chain.PostAsync(runId)).IsSuccess);

    var run = await chain.RunAsync(runId);

    // ---- THE PAYEE IS ON THE RUN AT ALL, WHICH IS THE ASSERTION THAT MATTERS.
    var theirs = run.Lines.Where(line => line.EmployeeId == chain.OneOffPayee).ToList();
    var only = Assert.Single(theirs);

    Assert.Equal(4000m, only.Amount);
    Assert.Equal(chain.BonusElementId, only.PayElementId);

    // ---- AND NOTHING ELSE. No base, no proration, no absence deduction: they are on no rate.
    Assert.DoesNotContain(theirs, line => line.PayElementId != chain.BonusElementId);

    // ---- THE SALARIED EMPLOYEE IS UNAFFECTED, which is what makes the run's inclusion rule correct
    // rather than merely wider.
    Assert.Equal(BaseSalary, run.Lines.Single(
      line => line.EmployeeId == chain.Employee && line.GlAccountId == chain.SalaryAccountId).Amount);
  }

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
  // ⚠ CITED BY B18 pass 13, body-confirmed: `AC-PAY-0020` -- *"the created journal balances: total debits equal total credits"* -- asserted as
  // `Sum(Debit) == Sum(Credit)`. ⚠ AND ITS OWN CONTROL SITS BESIDE IT: `Sum(Debit) > 0`, without which
  // a journal with NO LINES would balance trivially and satisfy the criterion.
  [Trait("Criterion", "AC-PAY-0020")]
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

    // ---- THE ONE-OFF PAYEE (T-114). A SECOND PERSON, WITH NO COMPENSATION RECORD AT ALL.
    //
    // `OD-SS-0003`: an external accountant who is paid IS an employee. This is that person — in HR's
    // roster, employed through the period, and holding no monthly, daily or hourly rate. **Before T-110 the
    // run dropped them silently: no line, no error, no payslip.**
    public Guid OneOffPayee { get; } = Guid.NewGuid();

    public Guid BonusElementId { get; private set; }

    public Guid SalaryAccountId { get; private set; }

    public Guid OvertimeAccountId { get; private set; }

    public Guid AbsenceAccountId { get; private set; }

    public Guid NetPayAccountId { get; private set; }

    public Guid PayrollPeriodId { get; private set; }

    public Guid AttendancePeriodId { get; private set; }

    private Guid DepartmentId { get; } = Guid.NewGuid();

    private Guid PositionId { get; } = Guid.NewGuid();

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
    // ---- THE EMPLOYMENT DATE IS A PARAMETER (T-115), DEFAULTED TO 2020.
    //
    // The four cases written before T-115 assert numbers that assume a full-period employee, and a spine
    // that had to be edited to add a joiner would not be proving the same thing afterwards.
    // ---- AND A TERMINATION DATE (T-116), ALSO DEFAULTED.
    //
    // `CK_Employees_TerminationDateMatchesStatus` ties the two together, so a leaver is seeded as
    // `Terminated` WITH a date or `Active` with none — the database refuses any other combination, which is
    // why this takes one parameter and derives the status rather than taking both.
    public Task SeedEmployeeAsync(
      string employmentDate = "2020-01-01T00:00:00+00:00", string? terminationDate = null)
    {
      var status = terminationDate is null ? "Active" : "Terminated";
      var terminationValue = terminationDate is null ? "NULL" : $"'{terminationDate}'";
      var department = DepartmentId;
      var position = PositionId;

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
           [EmployeeNumber], [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [TerminationDate],
           [Status], [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{Employee}', '{Tenant}', '{Company}', '{BranchId}', '{department}', '{position}',
           N'CHAIN-1', N'CHAIN-1', N'Chain Person', '{employmentDate}', {terminationValue},
           N'{status}', N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
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

      // ---- A ONE-OFF'S ELEMENT (T-114). `FixedAmount`, and DELIBERATELY ASSIGNED TO NOBODY.
      //
      // A `FixedAmount` element with no assignment produces no line for any employee, so seeding it leaves
      // the two pre-T-107 tests' arithmetic untouched. **A one-off instruction names it, and that is the
      // only way it ever pays out** — the element supplies the KIND and the GL account, the instruction
      // supplies the amount.
      var bonus = Element(
        "BONUS", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 0m, 20, SalaryAccountId);
      BonusElementId = bonus.Id;

      foreach (var element in new[] { basic, overtime, absence, netPay, bonus })
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

    // ---- A PERSON WITH A ONE-OFF AND NO COMPENSATION (T-114).
    //
    // **What this cost to construct is itself the finding.** The employee is one more INSERT reusing the
    // department and position the first one created; the instruction is one `OneOffPayment.Create`. There is
    // no compensation row, no salary type, no rate — **because that is the whole point of the person.**
    public async Task SeedOneOffPayeeAsync(decimal amount)
    {
      await ExecuteAsync($"""
        INSERT INTO [tenant].[Employees]
          ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [PositionId],
           [EmployeeNumber], [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [Status],
           [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{OneOffPayee}', '{Tenant}', '{Company}', '{BranchId}', '{DepartmentId}', '{PositionId}',
           N'CHAIN-2', N'CHAIN-2', N'Contract Auditor', '2020-01-01T00:00:00+00:00', N'Active',
           N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      await using var context = CreateContext();

      var instruction = OneOffPayment.Create(
        Company, OneOffPayee, PayrollPeriodId, BonusElementId, amount, "settlement for the audit").Value;
      context.Set<OneOffPayment>().Add(instruction);

      await context.SaveChangesAsync();
    }

    private PayElement Element(
      string code, PayElementKind kind, PayElementBehaviour behaviour, decimal rate, int order, Guid account)
    {
      var element = PayElement.Create(Company, code, code, kind, behaviour, rate, order).Value;
      Assert.True(element.MapToAccount(account).IsSuccess);
      return element;
    }

    // ---- ATTENDANCE. A calendar, an open period, and the two facts the chain carries.
    // ---- AN OPTIONAL RECORD OF WORK ON THE 20th (T-121), DEFAULTED OFF.
    //
    // The fixture's existing 20th-of-January record is an ABSENCE — zero worked, zero overtime — which is
    // why the leaver case is noise rather than contradiction. **This adds WORK on the same day**, which for
    // an employee terminated on the 15th is the contradiction the run must refuse.
    // ---- AND THE WORKED DAY IS A PARAMETER (T-121), DEFAULTED TO THE 14th.
    //
    // **T-121's refusal found this: the joiner test hired on the 16th and the fixture recorded work on the
    // 14th — two days before they existed to the company.** Nobody noticed, because until T-121 nothing
    // compared the two. A joiner's attendance has to fall inside their employment or the run now refuses,
    // correctly.
    public async Task SeedAttendanceAsync(
      bool workAfterTermination = false, DateOnly? workedOn = null)
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
        Company, period.Id, Employee, workedOn ?? new DateOnly(2026, 1, 14),
        workedQuantity: 8m, overtimeQuantity: OvertimeHours, overtimeTier: "NIGHT",
        paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: 0m, note: null).Value;

      var absent = AttendanceRecord.Observe(
        Company, period.Id, Employee, new DateOnly(2026, 1, 20),
        workedQuantity: 0m, overtimeQuantity: 0m, overtimeTier: null,
        paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: UnpaidAbsenceDays, note: "Unpaid leave").Value;

      var records = new List<AttendanceRecord> { worked, absent };

      if (workAfterTermination)
      {
        records.Add(AttendanceRecord.Observe(
          Company, period.Id, Employee, new DateOnly(2026, 1, 22),
          workedQuantity: 8m, overtimeQuantity: 0m, overtimeTier: null,
          paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: 0m, note: "worked after termination").Value);
      }

      foreach (var record in records)
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

    // ---- CALCULATE, REPORTING THE OUTCOME (T-115). A REFUSAL is the assertion in one case.
    public async Task<Result> TryCalculateAsync(Guid runId)
    {
      await using var graph = new ChainGraph(this);
      return await graph.Calculate.HandleAsync(new CalculatePayrollRunCommand(runId));
    }

    public async Task<Guid> CreateRunAsync()
    {
      await using var context = CreateContext();

      var run = PayrollRun.Create(Company, PayrollPeriodId).Value;
      context.Set<PayrollRun>().Add(run);
      await context.SaveChangesAsync();
      return run.Id;
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

    // ---- REVERSAL THROUGH THE REAL HANDLER (T-114).
    public async Task<Result<Guid>> ReverseAsync(Guid runId)
    {
      await using var graph = new ChainGraph(this);
      return await graph.Reverse.HandleAsync(
        new ReversePayrollRunCommand(runId, PayDate, "chain reversal"));
    }

    // ---- A RUN CREATED THE WAY THE DATABASE SEES IT (T-114).
    //
    // Straight to the aggregate and the context, which is what puts the filtered unique index in the path.
    // Returns the save's result rather than throwing, because a REFUSAL is the assertion in half the cases.
    public async Task<Result<Guid>> TryCreateRunAsync()
    {
      await using var context = CreateContext();

      var run = PayrollRun.Create(Company, PayrollPeriodId).Value;
      context.Set<PayrollRun>().Add(run);

      try
      {
        await context.SaveChangesAsync();
        return Result.Success(run.Id);
      }
      catch (DbUpdateException)
      {
        // The unique index refused it. Reported as a value rather than an exception, so the test asserts
        // the REFUSAL rather than catching around it.
        return Result.Failure<Guid>(PayrollErrors.RunAlreadyExistsForPeriod);
      }
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
        // T-119: the summary resolves the employment window itself, so the deduction excludes absence
        // recorded on days the employee was not employed.
        IAttendanceSummary attendance = new AttendanceSummaryService(
          accessor, companyAccess, currentTenant, currentTenantUser,
          new WorkingCalendarRepository(accessor), roster);

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

        // T-114: the real reversal path, so the run's `ReversedUtc` is stamped by the handler after the
        // LEDGER has accepted — not by the test reaching into the aggregate.
        Reverse = new ReversePayrollRunCommandHandler(runs, ledger, scope, unitOfWork);
      }

      public CalculatePayrollRunCommandHandler Calculate { get; }

      public ApprovePayrollRunCommandHandler Approve { get; }

      public PostPayrollRunCommandHandler Post { get; }

      public ReversePayrollRunCommandHandler Reverse { get; }

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

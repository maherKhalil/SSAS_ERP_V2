using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests.Runs;

// ==================================================================================================
// A ONE-OFF PAY INSTRUCTION, AND THE PERSON IT MAKES PAYABLE (T-110).
// ==================================================================================================
//
// **The defect this closes is an omission, not a miscalculation.** An employee with no compensation record
// was skipped by every payroll run — no line, no error, no payslip — and a contractor paid once for a job
// has no monthly, daily or hourly rate, so no compensation record exists for them to have.
//
// `OD-SS-0003` puts such a person in HR's roster as an `Employee`, so the run always knew they existed. Only
// the compensation lookup dropped them.
public sealed class OneOffPaymentCalculationTests
{
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly Guid SalaryAccount = Guid.NewGuid();
  private static readonly Guid BonusAccount = Guid.NewGuid();

  private static readonly DateTimeOffset LongEmployed = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

  // ---- THE WHOLE POINT: NO COMPENSATION, AND STILL PAID.
  //
  // A null `Compensation` produces no base, no assignments, no proration and no absence deduction, so every
  // element yields zero and the zero-line rule suppresses them. **What remains is exactly the one-off.**
  [Fact]
  public void An_employee_with_no_compensation_is_paid_their_one_off()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var bonus = PayrollTestData.Element(
      "BONUS", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 0m, 10, BonusAccount);

    var lines = Calculate(compensation: null, [basic, bonus], (bonus.Id, 4000m));

    var line = Assert.Single(lines);
    Assert.Equal(4000m, line.Amount);
    Assert.Equal(bonus.Id, line.PayElementId);

    // NO BASE LINE AT ALL, not a zero one. There was never a rate to prorate.
    Assert.DoesNotContain(lines, candidate => candidate.PayElementId == basic.Id);
  }

  // ---- AND IT IS ADDITIVE FOR SOMEONE WHO DOES HAVE A SALARY.
  //
  // A one-off is not an alternative to being paid; it is another line. 3100 of monthly base and a 500
  // instruction produce both.
  [Fact]
  public void A_salaried_employee_receives_their_one_off_as_an_additional_line()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);
    var bonus = PayrollTestData.Element(
      "BONUS", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 0m, 10, BonusAccount);

    var lines = Calculate(Monthly(3100m), [basic, bonus], (bonus.Id, 500m));

    Assert.Equal(3100m, lines.Single(line => line.PayElementId == basic.Id).Amount);
    Assert.Equal(500m, lines.Single(line => line.PayElementId == bonus.Id).Amount);
  }

  // ---- AN INSTRUCTION THE RUN CANNOT PRICE REFUSES THE RUN.
  //
  // `ordered` excludes inactive elements and `NetPayPayable`. A one-off naming one of those would otherwise
  // produce no line — **an instruction somebody wrote, a person expecting money, and nothing anywhere.**
  //
  // **That is the exact shape of the defect this task exists to close**, so reproducing it one level in
  // would be the worst possible outcome of fixing it.
  [Fact]
  public void A_one_off_naming_an_element_the_run_is_not_pricing_refuses_the_run()
  {
    var basic = PayrollTestData.Element(
      "BASIC", PayElementKind.Earning, PayElementBehaviour.BaseSalary, account: SalaryAccount);

    var absent = Guid.NewGuid();

    var result = PayrollCalculator.Calculate(
      Guid.NewGuid(), PayrollTestData.Period(),
      [new PayrollEmployeeInput(
        Employee, LongEmployed, null, Monthly(3100m), null, 0m, 0m, 0,
        [new OneOffPaymentInput(Guid.NewGuid(), absent, 500m)])],
      [basic]);

    Assert.True(result.IsFailure);
    Assert.Equal(PayrollErrors.OneOffPaymentElementNotPayable.Code, result.Error.Code);
  }

  // ---- THE SAME RUN CANNOT CONSUME IT TWICE (T-123, narrowed from T-110).
  //
  // **T-110 refused ANY second consumption and this test asserted that.** It was right while it stood: a
  // reversal wrote nothing on the run, so *"paid, then unpaid"* was not a state Payroll could express, and
  // refusing outright was the only way to stop a double payment.
  //
  // **T-112 gave the run a `ReversedUtc` and T-123 made the ruled predicate expressible**, so what is
  // refused now is one run repeating itself — a defect in the approval path — rather than a correcting run
  // taking over an instruction whose first run was reversed.
  [Fact]
  public void The_same_run_cannot_consume_a_one_off_twice()
  {
    var payment = Instruction(500m);
    var run = Guid.NewGuid();

    Assert.True(payment.MarkConsumedBy(run).IsSuccess);
    Assert.True(payment.IsConsumed);

    var again = payment.MarkConsumedBy(run);

    Assert.True(again.IsFailure);
    Assert.Equal(OneOffPaymentErrors.AlreadyConsumed.Code, again.Error.Code);
    Assert.Equal(run, payment.ConsumedByPayrollRunId);
  }

  // ---- AND A CORRECTING RUN TAKES IT OVER, WHICH IS THE WHOLE OF T-110's RULING.
  //
  // **Refusing this would strand an unpaid obligation** — extinguishing a debt by an accounting action,
  // which the ruling rejected in terms.
  //
  // **The reference moves rather than being cleared.** It answers *"which run paid it"*, and after a
  // correction the honest answer is the correcting run. **A cleared reference would answer "nobody", which
  // was true of neither run.**
  //
  // **What stops this being a double payment is not this method.** The repository offers an instruction
  // only while it is unconsumed or its run is reversed, and the filtered unique index permits one
  // unreversed run per period — so two LIVE runs can never both reach it.
  [Fact]
  public void A_correcting_run_takes_over_a_one_off_whose_run_was_reversed()
  {
    var payment = Instruction(500m);
    var reversedRun = Guid.NewGuid();
    var correctingRun = Guid.NewGuid();

    Assert.True(payment.MarkConsumedBy(reversedRun).IsSuccess);

    var taken = payment.MarkConsumedBy(correctingRun);

    Assert.True(taken.IsSuccess, taken.IsFailure ? taken.Error.Message : string.Empty);
    Assert.Equal(correctingRun, payment.ConsumedByPayrollRunId);
  }

  // ---- ZERO IS REFUSED, NOT ONLY NEGATIVE.
  //
  // A zero instruction produces a zero line, which the zero-line rule then suppresses — leaving a record
  // somebody created and no line anywhere, indistinguishable from an instruction never written.
  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void A_one_off_must_be_a_positive_amount(decimal amount)
  {
    var created = OneOffPayment.Create(
      Guid.NewGuid(), Employee, Guid.NewGuid(), Guid.NewGuid(), amount);

    Assert.True(created.IsFailure);
    Assert.Equal(OneOffPaymentErrors.AmountNotPositive.Code, created.Error.Code);
  }

  // ---- A NEW INSTRUCTION IS UNCONSUMED, AND THAT IS THE ONLY STATE IT HAS.
  //
  // `IsConsumed` is derived from the reference rather than stored beside it, so there is exactly one fact
  // and it names the run. A boolean alongside would be a second answer to the same question.
  [Fact]
  public void A_new_one_off_is_unconsumed_and_names_no_run()
  {
    var payment = Instruction(500m);

    Assert.False(payment.IsConsumed);
    Assert.Null(payment.ConsumedByPayrollRunId);
  }

  private static OneOffPayment Instruction(decimal amount)
  {
    var created = OneOffPayment.Create(
      Guid.NewGuid(), Employee, Guid.NewGuid(), Guid.NewGuid(), amount, "settlement for the audit");

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : string.Empty);
    return created.Value;
  }

  private static EmployeeCompensation Monthly(decimal baseAmount)
  {
    var record = EmployeeCompensation.Create(
      Guid.NewGuid(), Employee, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), baseAmount);

    Assert.True(record.IsSuccess, record.IsFailure ? record.Error.Message : string.Empty);
    return record.Value;
  }

  private static IReadOnlyList<PayrollRunDraftLine> Calculate(
    EmployeeCompensation? compensation,
    PayElement[] elements,
    params (Guid PayElementId, decimal Amount)[] oneOffs)
  {
    var input = new PayrollEmployeeInput(
      Employee, LongEmployed, null, compensation, null, 0m, 0m, 0,
      [.. oneOffs.Select(entry => new OneOffPaymentInput(Guid.NewGuid(), entry.PayElementId, entry.Amount))]);

    var result = PayrollCalculator.Calculate(
      Guid.NewGuid(), PayrollTestData.Period(), [input], elements);

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : string.Empty);
    return result.Value;
  }
}

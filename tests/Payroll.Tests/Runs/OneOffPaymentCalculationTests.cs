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

  // ---- CONSUMPTION IS ONCE, AND A SECOND ATTEMPT FAILS RATHER THAN PASSING QUIETLY.
  //
  // A no-op would let a defect in the approval path pay one instruction through two runs and report success
  // both times. The aggregate refusing is what makes double payment structurally impossible instead of
  // guarded by whoever remembers to check.
  [Fact]
  public void A_one_off_is_consumed_once_and_a_second_run_is_refused()
  {
    var payment = Instruction(500m);
    var firstRun = Guid.NewGuid();

    Assert.True(payment.MarkConsumedBy(firstRun).IsSuccess);
    Assert.Equal(firstRun, payment.ConsumedByPayrollRunId);
    Assert.True(payment.IsConsumed);

    var second = payment.MarkConsumedBy(Guid.NewGuid());

    Assert.True(second.IsFailure);
    Assert.Equal(OneOffPaymentErrors.AlreadyConsumed.Code, second.Error.Code);

    // AND THE FIRST RUN STILL OWNS IT. A failed second attempt must not repoint the reference.
    Assert.Equal(firstRun, payment.ConsumedByPayrollRunId);
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

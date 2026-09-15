using SSAS.BuildingBlocks.Domain;
using SSAS.Payroll.API;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.API.Tests.Payroll;

// ==================================================================================================
// THE ERRORS `PayrollCalculator` RETURNS, AND WHAT THEY BECOME ON THE WIRE (T-118).
// ==================================================================================================
//
// ---- WHY THIS FILE EXISTS, AND IT IS NOT BECAUSE THE MAPPINGS ARE UNUSUAL.
//
// **`ModuleErrorMappingArchitectureTests` cannot see any of them.** Its closure walks CONSTRUCTOR
// PARAMETERS, resolving each to product classes assignable to it — and `PayrollCalculator` is a `static`
// class. It has no constructors and is nobody's parameter, **so it has never entered any site's closure in
// any run of that register** (T-117).
//
// **Every refusal the calculator returns was therefore unguarded**, and one of them —
// `OneOffPaymentElementNotPayable` — fell through to a **500** from T-110 until T-118 while the register
// reported 12/12 green.
//
// **So these are asserted here, directly, by the only means that does not depend on the instrument with the
// blind spot.** When the closure is widened (T-119) this file becomes redundant for three of the four and
// should be reconsidered rather than kept out of habit — **but it should not be deleted before the widened
// walk is shown to reach them**, which is the mistake it exists to prevent.
public sealed class PayrollCalculatorErrorWireContractTests
{
  // ---- THE FOUR THE CALCULATOR CAN RETURN. Enumerated by reading its source, not by reflection, because
  // reflection over a static class is exactly what the register already cannot do.
  [Theory]
  [InlineData("Payroll.NoIncludedEmployees", 422)]
  // 400, not 422: `PeriodBoundsInvalid` is reachable from the calculator AND from period creation, and
  // `DEC-L-079` says a status is a property of the CODE rather than of the site. The creation path is a
  // malformed request, so 400 is the answer it already had — asserted here as READ from the mapper rather
  // than as I first guessed, which was 422 and wrong.
  [InlineData("Payroll.PeriodBoundsInvalid", 400)]
  [InlineData("Payroll.DailySalaryHasNoWorkingDays", 409)]
  [InlineData("Payroll.OneOffPaymentElementNotPayable", 422)]
  // 409, not 422: assigning the element or correcting the tier makes the identical request succeed, which
  // is `DailySalaryHasNoWorkingDays`' shape rather than `OneOffPaymentElementNotPayable`'s (T-149).
  [InlineData("Payroll.OvertimeTierHasNoPricedElement", 409)]
  public void Every_error_the_calculator_returns_has_a_deliberate_status(string code, int expected)
  {
    var mapped = PayrollApiErrorMapper.Map(new Error(code, "irrelevant to the mapping"));

    Assert.Equal(expected, mapped.StatusCode);

    // NOT A 500. That is the claim worth making separately from the exact code: an unmapped error falls
    // through to a 500 for what is a business refusal, with no exception and no log entry.
    Assert.NotEqual(500, mapped.StatusCode);
  }

  // ---- THE ONE-OFF ROUTE'S REFUSALS (T-125). UNMAPPED FROM T-110 UNTIL NOW.
  //
  // **`POST /employees/{id}/one-off-payments` with `amount: 0` answered 500** — a validation refusal
  // arriving as a server fault. The register could not see it: T-110 added the route and its handler and
  // never seeded the handler, so every code it returns sat outside every closure.
  //
  // **Asserted here as well as seeded, for the same reason the calculator's four are**: this file exists to
  // cover what the register structurally could not, and a seed added today is not evidence about the
  // statuses themselves.
  [Theory]
  [InlineData("Payroll.OneOffPaymentCompanyRequired", 400)]
  [InlineData("Payroll.OneOffPaymentEmployeeRequired", 400)]
  [InlineData("Payroll.OneOffPaymentPeriodRequired", 400)]
  [InlineData("Payroll.OneOffPaymentPayElementRequired", 400)]
  [InlineData("Payroll.OneOffPaymentAmountNotPositive", 400)]
  [InlineData("Payroll.OneOffPaymentAlreadyConsumed", 409)]
  [InlineData("Payroll.OneOffPaymentConsumingRunIsForAnotherPeriod", 409)]
  public void Every_one_off_refusal_has_a_deliberate_status(string code, int expected)
  {
    var mapped = PayrollApiErrorMapper.Map(new Error(code, "irrelevant to the mapping"));

    Assert.Equal(expected, mapped.StatusCode);
    Assert.NotEqual(500, mapped.StatusCode);
  }

  // ---- AND THE PAY ELEMENT ONE ANSWERS 404, THROUGH THE CODE THAT ALREADY MEANT IT.
  //
  // T-110 declared `Payroll.OneOffPaymentPayElementNotFound` for a condition `Payroll.PayElementNotFound`
  // already covered — **the same fact under two names, and the second was unmapped**, so *the pay element
  // you named does not exist* answered 404 from one route and 500 from another. T-125 deleted the duplicate.
  [Fact]
  public void A_one_off_naming_an_unknown_element_answers_the_existing_not_found()
  {
    var mapped = PayrollApiErrorMapper.Map(PayElementErrors.NotFound);

    Assert.Equal(404, mapped.StatusCode);
  }

  // ---- AND THE TWO ARE NOT THE SAME REFUSAL, WHICH IS WHY THEY DIFFER.
  //
  // **`DailySalaryHasNoWorkingDays` is 409 because waiting helps** — close the attendance period, or give
  // the company a working calendar, and the identical request succeeds.
  //
  // **`OneOffPaymentElementNotPayable` is 422 because waiting never helps.** The instruction names an
  // inactive element or the derived net-pay element; somebody must change the instruction or the element.
  [Fact]
  public void The_two_calculator_refusals_differ_because_only_one_is_resolved_by_waiting()
  {
    var worldNotReady = PayrollApiErrorMapper.Map(PayrollErrors.DailySalaryHasNoWorkingDays);
    var requestNotSatisfiable = PayrollApiErrorMapper.Map(PayrollErrors.OneOffPaymentElementNotPayable);

    Assert.NotEqual(worldNotReady.StatusCode, requestNotSatisfiable.StatusCode);
  }
}

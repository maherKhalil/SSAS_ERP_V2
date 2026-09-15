using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests.Elements;

public sealed class PayElementDomainTests
{
  [Fact]
  // ⚠ CITED BY B18, body-confirmed: no writable code property exists -- asserted over the TYPE, so no call site can be the exception.
  [Trait("Criterion", "AC-PAY-0007")]
  public void An_element_code_cannot_be_changed_after_creation()
  {
    // Following `Account`'s precedent rather than re-deriving it: a code is a business identifier that pay
    // history was calculated against, so re-coding silently re-labels what people were paid. There is no
    // method to change it, and the wire shape has no field for it — the absence IS the rule.
    var writable = typeof(PayElement)
      .GetProperties()
      .Where(property => property.Name == nameof(PayElement.Code))
      .Where(property => property.SetMethod is { IsPublic: true });

    Assert.Empty(writable);
    Assert.DoesNotContain(
      typeof(PayElement).GetMethods().Select(method => method.Name),
      name => name.Contains("SetCode", StringComparison.Ordinal) ||
        name.Contains("Recode", StringComparison.Ordinal));
  }

  [Fact]
  public void Kind_and_behaviour_cannot_be_updated()
  {
    // Changing either would redefine what PAST runs computed while leaving their stored lines untouched, so
    // the record and its explanation would disagree. An element whose behaviour was wrong is deactivated and
    // replaced, which leaves history intact.
    var update = typeof(PayElement).GetMethod(nameof(PayElement.Update));

    Assert.NotNull(update);
    Assert.DoesNotContain(
      update!.GetParameters().Select(parameter => parameter.ParameterType),
      type => type == typeof(PayElementKind) || type == typeof(PayElementBehaviour));
  }

  [Fact]
  public void A_negative_default_amount_is_refused_rather_than_normalized()
  {
    // `Kind` already says whether an element earns or deducts, so a negative value is a caller who has
    // misunderstood the model — not a smaller number. Silently flipping it would hide that.
    var result = PayElement.Create(
      PayrollTestData.Company, "X", "X", PayElementKind.Deduction, PayElementBehaviour.FixedAmount, -5m, 0);

    Assert.True(result.IsFailure);
    Assert.Equal("Payroll.PayElementAmountNegative", result.Error.Code);
  }

  [Fact]
  public void A_negative_calculation_order_is_refused()
  {
    var result = PayElement.Create(
      PayrollTestData.Company, "X", "X", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 0m, -1);

    Assert.True(result.IsFailure);
    Assert.Equal("Payroll.PayElementCalculationOrderInvalid", result.Error.Code);
  }

  [Fact]
  public void An_element_must_belong_to_a_company()
  {
    // `OD-PAY-0005` made elements company-owned, which is the contrast with GL's tenant-wide chart.
    var result = PayElement.Create(
      Guid.Empty, "X", "X", PayElementKind.Earning, PayElementBehaviour.FixedAmount, 0m, 0);

    Assert.True(result.IsFailure);
    Assert.Equal("Payroll.PayElementCompanyRequired", result.Error.Code);
  }

  [Fact]
  public void Deactivation_is_idempotent_and_reversible()
  {
    // Following `Account.Deactivate`: deactivating an inactive element is the state the caller asked for,
    // not an error. And it never removes the element, because past run lines reference it.
    var element = PayrollTestData.Element("X", PayElementKind.Earning, PayElementBehaviour.FixedAmount);

    element.Deactivate();
    element.Deactivate();
    Assert.False(element.IsActive);

    element.Activate();
    Assert.True(element.IsActive);
  }

  [Fact]
  public void The_normalized_shadows_exist_so_a_search_can_use_a_predicate()
  {
    // `DEC-POS-0030`: a value-converted property translates in a PROJECTION but not in a PREDICATE, and HR
    // shipped a department search that threw for every search term. Payroll is the third module to face it
    // and writes the shadow up front rather than after the third occurrence.
    var element = PayrollTestData.Element("basic", PayElementKind.Earning, PayElementBehaviour.BaseSalary);

    Assert.Equal("BASIC", element.NormalizedCode);
    Assert.Equal("BASIC", element.NormalizedName);
  }

  [Fact]
  public void Mapping_requires_a_real_account()
  {
    var element = PayrollTestData.Element("X", PayElementKind.Earning, PayElementBehaviour.FixedAmount);

    var mapped = element.MapToAccount(Guid.Empty);

    Assert.True(mapped.IsFailure);
    Assert.Equal("Payroll.PayElementAccountRequired", mapped.Error.Code);
  }

  [Fact]
  // ⚠ CITED BY B18, body-confirmed: ⚠ the refusal message contains "HOUSING", the element's own code -- which is the criterion's
  // "the response NAMES the element" half, not merely that it refused.
  [Trait("Criterion", "AC-PAY-0021")]
  public void An_unmapped_element_names_itself_in_the_refusal()
  {
    // `OD-PAY-0012` put the mapping check at APPROVAL, and a refusal saying only "a pay element is unmapped"
    // makes the user hunt through the whole list. Naming it is the difference between fixing something and
    // filing a ticket.
    var error = PayElementErrors.Unmapped("HOUSING");

    Assert.Contains("HOUSING", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void The_period_refuses_a_pay_date_before_it_begins()
  {
    // Paying after a period ends is normal; paying before it starts is not a schedule, it is a mistake —
    // and unlike the grade band there is no legitimate business case on the other side.
    var result = PayrollPeriod.CreateAlignedTo(
      PayrollTestData.Company, Guid.NewGuid(), "Jan",
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2025, 12, 20, 0, 0, 0, TimeSpan.Zero));

    Assert.True(result.IsFailure);
    Assert.Equal("Payroll.PayDateBeforePeriod", result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0010")]
  // ⚠ CITED BY B18 pass 13, body-confirmed: TWO criteria, one predicate. `AC-PAY-0011` -- *an employee terminated DURING the period is
  // included* -- is the "terminated on exactly the first day" case. `AC-PAY-0012` -- *terminated BEFORE
  // the period begins is not included* -- is the "terminated the day before it began" case.
  // ⚠ Grouped by MECHANISM: both criteria are the same boundary predicate read from opposite sides.
  [Trait("Criterion", "AC-PAY-0011")]
  [Trait("Criterion", "AC-PAY-0012")]
  public void Inclusion_is_a_pure_function_of_dates_at_both_boundaries()
  {
    var period = PayrollTestData.Period();

    // Employed for exactly the last day.
    Assert.True(period.Includes(period.EndUtc, null));

    // Terminated on exactly the first day — still a settlement of work done.
    Assert.True(period.Includes(period.StartUtc.AddYears(-1), period.StartUtc));

    // Terminated the day before it began: the employment did not overlap at all.
    Assert.False(period.Includes(period.StartUtc.AddYears(-1), period.StartUtc.AddDays(-1)));

    // Hired the day after it ended.
    Assert.False(period.Includes(period.EndUtc.AddDays(1), null));
  }
}

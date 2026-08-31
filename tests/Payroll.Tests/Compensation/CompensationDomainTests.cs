using SSAS.Payroll.Domain.Compensation;

namespace SSAS.Payroll.Tests.Compensation;

// THE DEC-POS-0023 SLOT, AND THE DERIVATION THAT MAKES A PAST RUN REPRODUCIBLE (OD-PAY-0003).
public sealed class CompensationDomainTests
{
  private static readonly Guid Employee = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

  private static readonly DateTimeOffset Jan = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Apr = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Jul = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "OD-PAY-0003")]
  public void The_record_in_force_is_the_latest_one_not_after_the_date()
  {
    var history = new[]
    {
      PayrollTestData.Compensation(Employee, Jan, 1000m),
      PayrollTestData.Compensation(Employee, Apr, 2000m),
      PayrollTestData.Compensation(Employee, Jul, 3000m)
    };

    Assert.Equal(1000m, EmployeeCompensation.InForceOn(history, Jan)!.BaseAmount);
    Assert.Equal(1000m, EmployeeCompensation.InForceOn(history, Apr.AddDays(-1))!.BaseAmount);
    Assert.Equal(2000m, EmployeeCompensation.InForceOn(history, Apr)!.BaseAmount);
    Assert.Equal(3000m, EmployeeCompensation.InForceOn(history, Jul.AddYears(5))!.BaseAmount);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0003")]
  public void A_date_before_the_first_record_resolves_to_nothing_rather_than_the_earliest()
  {
    // Answering with the earliest would INVENT A FACT: the employee had no compensation on file before one
    // was recorded, and a payroll run for that period must find nothing rather than a plausible number.
    var history = new[] { PayrollTestData.Compensation(Employee, Apr, 2000m) };

    Assert.Null(EmployeeCompensation.InForceOn(history, Jan));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0003")]
  public void An_empty_history_resolves_to_nothing()
  {
    Assert.Null(EmployeeCompensation.InForceOn([], Jan));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0003")]
  // ⚠ CITED BY B18, body-confirmed: the earlier record keeps its amount AND its effective date after a second is recorded -- "does not
  // alter any prior record".
  [Trait("Criterion", "AC-PAY-0001")]
  public void Recording_a_change_leaves_every_earlier_record_intact()
  {
    // The whole reason a past run can be reproduced. There is no update path to test, and that absence is
    // the ruling: `BR-PAY-0002` makes a change a NEW record.
    var first = PayrollTestData.Compensation(Employee, Jan, 1000m);
    var second = PayrollTestData.Compensation(Employee, Apr, 2000m);

    Assert.Equal(1000m, first.BaseAmount);
    Assert.NotEqual(first.Id, second.Id);
    Assert.Equal(Jan, first.EffectiveFromUtc);
  }

  [Fact]
  public void The_aggregate_carries_no_current_flag_and_no_end_date()
  {
    // Both are derived state that drifts, and both would have to be maintained transactionally on every
    // insert. The end of one record is the start of the next, resolved by ordering.
    var properties = typeof(EmployeeCompensation).GetProperties().Select(p => p.Name).ToArray();

    Assert.DoesNotContain("IsCurrent", properties, StringComparer.Ordinal);
    Assert.DoesNotContain("EffectiveToUtc", properties, StringComparer.Ordinal);
    Assert.DoesNotContain("EndUtc", properties, StringComparer.Ordinal);
  }

  [Fact]
  public void A_history_row_carries_no_row_version_because_it_is_never_updated()
  {
    // Not an omission: `RowVersion` belongs on mutable aggregates, and putting one here would advertise an
    // update path that does not exist.
    Assert.DoesNotContain(
      "RowVersion",
      typeof(EmployeeCompensation).GetProperties().Select(p => p.Name),
      StringComparer.Ordinal);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0004")]
  // ⚠ CITED BY B18, body-confirmed: all three clauses: the amount is STORED, `WasOutsideGradeBand` is true, and an observation is
  // surfaced -- "accepted and recorded, and the out-of-band condition is surfaced to the caller".
  [Trait("Criterion", "AC-PAY-0004")]
  public void An_out_of_band_amount_is_recorded_and_warned_never_refused()
  {
    // The band is INFORMATIONAL. Promoting it to a control would change what `DEC-POS-0027` said a band is,
    // and would immediately require an override path, an override permission and an override audit.
    var record = PayrollTestData.Compensation(Employee, Jan, 999_999m);
    record.RecordGradeBandObservation(true, "Above grade C maximum; retention arrangement");

    Assert.True(record.WasOutsideGradeBand);
    Assert.Contains("retention", record.GradeBandObservation!, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(999_999m, record.BaseAmount);
  }

  [Fact]
  public void A_negative_base_amount_is_refused()
  {
    var result = EmployeeCompensation.Create(PayrollTestData.Company, Employee, Jan, -1m);

    Assert.True(result.IsFailure);
    Assert.Equal("Payroll.CompensationBaseAmountNegative", result.Error.Code);
  }

  [Fact]
  public void The_same_element_cannot_be_assigned_twice()
  {
    // A duplicate would double-count in every run, SILENTLY — the worst kind of payroll defect, because the
    // total still looks like a number.
    var element = Guid.NewGuid();

    var result = EmployeeCompensation.Create(
      PayrollTestData.Company, Employee, Jan, 1000m, [(element, 10m), (element, 20m)]);

    Assert.True(result.IsFailure);
    Assert.Equal("Payroll.CompensationAssignmentDuplicate", result.Error.Code);
  }

  [Fact]
  public void An_assignment_may_omit_its_amount_to_mean_use_the_element_default()
  {
    // Storing a copy of the default would FREEZE it, and a later change to the element would then silently
    // not apply to anyone.
    var element = Guid.NewGuid();
    var record = PayrollTestData.Compensation(Employee, Jan, 1000m, (element, null));

    Assert.Null(record.Assignments.Single().RateOrAmount);
  }
}

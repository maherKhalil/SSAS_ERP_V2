using SSAS.BuildingBlocks.Domain;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;

namespace SSAS.Attendance.Tests.Records;

// TS-ATT-0005 to TS-ATT-0009, plus the period lifecycle. The append-only shape and the observation/adjustment
// split are what `OD-ATT-0012` produced, so most of what is asserted here is that the split actually holds.
public sealed class AttendanceRecordTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly Guid Period = Guid.NewGuid();
  private static readonly Guid Employee = Guid.NewGuid();
  private static readonly DateOnly Date = new(2026, 9, 14);

  private static Result<AttendanceRecord> Observe(
    decimal worked = 8m, decimal overtime = 0m, string? tier = null,
    decimal paidAbsence = 0m, decimal unpaidAbsence = 0m) =>
    AttendanceRecord.Observe(
      Company, Period, Employee, Date, worked, overtime, tier, paidAbsence, unpaidAbsence, note: null);

  [Fact]
  [Trait("Requirement", "REQ-ATT-0004")]
  public void An_observation_records_the_quantities_and_carries_no_adjustment_target()
  {
    var record = Observe(worked: 7.5m);

    Assert.True(record.IsSuccess);
    Assert.Equal(AttendanceRecordKind.Observation, record.Value.Kind);
    Assert.Null(record.Value.AdjustedRecordId);
    Assert.Equal(7.5m, record.Value.WorkedQuantity);
  }

  // ---- AN OBSERVATION CANNOT BE NEGATIVE, AND THAT IS WHY `AttendanceRecordKind` EXISTS.
  //
  // Without the distinction, either corrections would be impossible or every quantity in the module would
  // have to accept a negative and nothing would catch a mis-keyed one.
  [Theory]
  [InlineData(-1, 0, 0, 0)]
  [InlineData(0, -1, 0, 0)]
  [InlineData(0, 0, -1, 0)]
  [InlineData(0, 0, 0, -1)]
  public void A_negative_observation_is_refused(decimal worked, decimal overtime, decimal paid, decimal unpaid)
  {
    var record = AttendanceRecord.Observe(
      Company, Period, Employee, Date, worked, overtime, "NIGHT", paid, unpaid, note: null);

    Assert.True(record.IsFailure);
    Assert.Equal(AttendanceRecordErrors.NegativeObservation.Code, record.Error.Code);
  }

  // TS-ATT-0007. Overtime without a tier is a quantity Payroll cannot price: the tier is what a pay
  // element's rate is configured against (`OD-ATT-0008`). Recording it untiered produces hours nobody can pay.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0007")]
  public void Overtime_without_a_tier_is_refused()
  {
    var record = Observe(overtime: 3m, tier: null);

    Assert.True(record.IsFailure);
    Assert.Equal(AttendanceRecordErrors.OvertimeTierRequired.Code, record.Error.Code);
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0007")]
  public void Overtime_carries_a_tier_and_no_multiplier()
  {
    var record = Observe(overtime: 3m, tier: "NIGHT");

    Assert.True(record.IsSuccess);
    Assert.Equal("NIGHT", record.Value.OvertimeTier);

    // `DEC-ATT-0004`. There is no rate, amount or currency anywhere on the type — the rate lives in Payroll.
    var names = typeof(AttendanceRecord).GetProperties().Select(property => property.Name).ToArray();
    Assert.DoesNotContain(names, name =>
      name.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Currency", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Multiplier", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0008")]
  public void Paid_and_unpaid_absence_are_separate_quantities()
  {
    var record = Observe(worked: 0m, paidAbsence: 1m, unpaidAbsence: 2m);

    Assert.True(record.IsSuccess);
    Assert.Equal(1m, record.Value.PaidAbsenceQuantity);
    Assert.Equal(2m, record.Value.UnpaidAbsenceQuantity);
  }

  // ================================================================================================
  // THE ADJUSTMENT PATH (TS-ATT-0009-adjacent, OD-ATT-0012).
  // ================================================================================================

  [Fact]
  [Trait("Requirement", "REQ-ATT-0019")]
  public void An_adjustment_carries_signed_deltas_and_names_what_it_corrects()
  {
    var original = Observe().Value;

    var adjustment = AttendanceRecord.Adjust(
      Company, Guid.NewGuid(), Employee, Date, original.Id,
      workedDelta: -2m, overtimeDelta: 0m, overtimeTier: null,
      paidAbsenceDelta: 0m, unpaidAbsenceDelta: 2m, note: "Mis-keyed; two hours were unpaid leave");

    Assert.True(adjustment.IsSuccess);
    Assert.Equal(AttendanceRecordKind.Adjustment, adjustment.Value.Kind);
    Assert.Equal(original.Id, adjustment.Value.AdjustedRecordId);

    // Negative is LEGAL here and refused on an observation. That asymmetry is the whole point of the kind.
    Assert.Equal(-2m, adjustment.Value.WorkedQuantity);

    // The DATE is the original's — the date says when it happened. The PERIOD is a different one, because
    // an adjustment lands in the currently open period.
    Assert.Equal(original.AttendanceDate, adjustment.Value.AttendanceDate);
    Assert.NotEqual(original.AttendancePeriodId, adjustment.Value.AttendancePeriodId);
  }

  // A note is REQUIRED on an adjustment: a number that changes what someone is paid, with nothing recorded
  // about why, is the one place in this module where that matters enough to enforce.
  [Fact]
  public void An_adjustment_without_a_note_is_refused()
  {
    var original = Observe().Value;

    var adjustment = AttendanceRecord.Adjust(
      Company, Period, Employee, Date, original.Id, -1m, 0m, null, 0m, 0m, note: null);

    Assert.True(adjustment.IsFailure);
    Assert.Equal(AttendanceRecordErrors.AdjustmentNoteRequired.Code, adjustment.Error.Code);
  }

  [Fact]
  public void An_adjustment_that_changes_nothing_is_refused()
  {
    var original = Observe().Value;

    var adjustment = AttendanceRecord.Adjust(
      Company, Period, Employee, Date, original.Id, 0m, 0m, null, 0m, 0m, note: "No change");

    Assert.True(adjustment.IsFailure);
    Assert.Equal(AttendanceRecordErrors.AdjustmentChangesNothing.Code, adjustment.Error.Code);
  }

  // ---- OBSERVATIONS AND ADJUSTMENTS SUM, WHICH IS WHY THE MODEL IS SIMPLE ENOUGH TO BE RIGHT.
  //
  // The summary contract sums every row for an employee in a period. This asserts the arithmetic the
  // contract depends on, without a database.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0019")]
  public void The_truth_for_an_employee_date_is_the_sum_of_its_rows()
  {
    var original = Observe(worked: 8m, unpaidAbsence: 0m).Value;
    var correction = AttendanceRecord.Adjust(
      Company, Guid.NewGuid(), Employee, Date, original.Id,
      workedDelta: -8m, overtimeDelta: 0m, overtimeTier: null,
      paidAbsenceDelta: 0m, unpaidAbsenceDelta: 1m, note: "Absent all day, unpaid").Value;

    AttendanceRecord[] rows = [original, correction];

    Assert.Equal(0m, rows.Sum(row => row.WorkedQuantity));
    Assert.Equal(1m, rows.Sum(row => row.UnpaidAbsenceQuantity));
  }

  // ---- THE APPEND-ONLY SHAPE, ASSERTED STRUCTURALLY.
  //
  // The runtime refusal lives in `PreventAppendOnlyMutation` and is proved against real SQL in
  // `TS-ATT-0029`. What is asserted here is the two consequences the analysis package called out, because
  // both are absences and absences do not fail on their own.
  [Fact]
  [Trait("Decision", "DEC-ATT-0009")]
  public void The_record_is_append_only_and_therefore_carries_no_row_version()
  {
    Assert.True(typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity)
      .IsAssignableFrom(typeof(AttendanceRecord)));

    // An append-only row has no update path to concurrency-check, and the column would imply one exists.
    Assert.DoesNotContain(
      typeof(AttendanceRecord).GetProperties(),
      property => property.Name == "RowVersion");
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0011")]
  public void The_record_is_branch_owned_and_the_branch_setter_is_public_for_stamping()
  {
    Assert.True(typeof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity)
      .IsAssignableFrom(typeof(AttendanceRecord)));

    // `IBranchOwnedEntity` requires a public setter so the write boundary can stamp it from the execution
    // context. Every other mutable property on this type has a private one.
    var branch = typeof(AttendanceRecord).GetProperty(nameof(AttendanceRecord.BranchId))!;
    Assert.NotNull(branch.SetMethod);
    Assert.True(branch.SetMethod!.IsPublic);

    var date = typeof(AttendanceRecord).GetProperty(nameof(AttendanceRecord.AttendanceDate))!;
    Assert.False(date.SetMethod!.IsPublic);
  }
}

// TS-ATT-0008, TS-ATT-0009. The period's lifecycle, including the reopen action whose safety rests entirely
// on the records being append-only.
public sealed class AttendancePeriodTests
{
  private static readonly Guid Company = Guid.NewGuid();

  private static AttendancePeriod Period() =>
    AttendancePeriod.Create(Company, "September 2026", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)).Value;

  [Fact]
  [Trait("Requirement", "REQ-ATT-0018")]
  public void Close_records_who_and_when()
  {
    var period = Period();
    var at = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    Assert.True(period.Close("auditor", at).IsSuccess);
    Assert.True(period.IsClosed);
    Assert.Equal("auditor", period.ClosedBy);
    Assert.Equal(at, period.ClosedUtc);
  }

  // Refusing a second close matters more than it looks: a repeat would overwrite `ClosedUtc` and `ClosedBy`,
  // silently rewriting who froze the numbers Payroll consumed.
  [Fact]
  [Trait("Criterion", "AC-ATT-0013")]
  public void Closing_an_already_closed_period_is_refused_rather_than_repeated()
  {
    var period = Period();
    var first = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    period.Close("auditor", first);

    var second = period.Close("someone else", first.AddDays(1));

    Assert.True(second.IsFailure);
    Assert.Equal(AttendancePeriodErrors.AlreadyClosed.Code, second.Error.Code);
    Assert.Equal("auditor", period.ClosedBy);
    Assert.Equal(first, period.ClosedUtc);
  }

  [Fact]
  public void Reopen_clears_the_close_stamps_and_a_second_reopen_is_refused()
  {
    var period = Period();
    period.Close("auditor", DateTimeOffset.UtcNow);

    Assert.True(period.Reopen().IsSuccess);
    Assert.False(period.IsClosed);
    Assert.Null(period.ClosedBy);
    Assert.Null(period.ClosedUtc);

    Assert.True(period.Reopen().IsFailure);
  }

  [Fact]
  public void Covers_is_inclusive_at_both_ends()
  {
    var period = Period();

    Assert.True(period.Covers(new DateOnly(2026, 9, 1)));
    Assert.True(period.Covers(new DateOnly(2026, 9, 30)));
    Assert.False(period.Covers(new DateOnly(2026, 8, 31)));
    Assert.False(period.Covers(new DateOnly(2026, 10, 1)));
  }

  [Fact]
  public void A_period_cannot_end_before_it_starts()
  {
    var created = AttendancePeriod.Create(
      Company, "Backwards", new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1));

    Assert.True(created.IsFailure);
    Assert.Equal(AttendancePeriodErrors.InvalidRange.Code, created.Error.Code);
  }

  // `DEC-ATT-0014` requires the classification to be ASSERTED, including negatively. A period is a
  // company-level accounting boundary; branch lives on the records inside it.
  [Fact]
  [Trait("Decision", "DEC-ATT-0014")]
  public void A_period_is_not_branch_owned()
  {
    Assert.False(typeof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity)
      .IsAssignableFrom(typeof(AttendancePeriod)));
  }
}

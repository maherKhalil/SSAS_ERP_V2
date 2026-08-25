using SSAS.Attendance.Domain.Calendars;

namespace SSAS.Attendance.Tests.Calendars;

// TS-ATT-0001 to TS-ATT-0004. The calendar is the foundation of both halves of this module, and
// `WorkingDaysBetween` is the query every leave request's consumed days are frozen from — so its boundaries
// get more attention here than anything else in the domain.
public sealed class WorkingCalendarTests
{
  private static readonly Guid Company = Guid.NewGuid();

  private static WorkingCalendar Calendar(params DayOfWeek[] weekend) =>
    WorkingCalendar.Create(Company, "Standard", weekend, isDefault: true).Value;

  // ================================================================================================
  // TS-ATT-0001. THREE WEEKENDS, BECAUSE ONE CASE WOULD PASS WITH A HARDCODED SAT/SUN.
  // ================================================================================================
  //
  // `BR-ATT-0001` exists because the weekend is not universal, and a single-case test is exactly how a
  // hardcoded constant survives review: it would pass, and it would be wrong for a large share of this
  // product's market in a way that produces plausible numbers rather than an error.
  [Theory]
  [Trait("Requirement", "REQ-ATT-0001")]
  // Fri/Sat: Sunday works, Friday does not.
  [InlineData(DayOfWeek.Friday, DayOfWeek.Saturday, "2026-09-13", true)]
  [InlineData(DayOfWeek.Friday, DayOfWeek.Saturday, "2026-09-11", false)]
  // Sat/Sun: Sunday does not work, Friday does.
  [InlineData(DayOfWeek.Saturday, DayOfWeek.Sunday, "2026-09-13", false)]
  [InlineData(DayOfWeek.Saturday, DayOfWeek.Sunday, "2026-09-11", true)]
  // Thu/Fri: Sunday works, Thursday does not.
  [InlineData(DayOfWeek.Thursday, DayOfWeek.Friday, "2026-09-13", true)]
  [InlineData(DayOfWeek.Thursday, DayOfWeek.Friday, "2026-09-10", false)]
  public void The_weekend_pattern_is_read_from_data(DayOfWeek first, DayOfWeek second, string date, bool isWorking)
  {
    var calendar = Calendar(first, second);

    Assert.Equal(isWorking, calendar.IsWorkingDay(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
  }

  [Fact]
  [Trait("Requirement", "REQ-ATT-0002")]
  public void A_holiday_on_a_working_day_reduces_the_count_by_exactly_one()
  {
    var calendar = Calendar(DayOfWeek.Saturday, DayOfWeek.Sunday);
    var from = new DateOnly(2026, 9, 7);   // Monday
    var to = new DateOnly(2026, 9, 11);    // Friday

    var before = calendar.WorkingDaysBetween(from, to);
    Assert.Equal(5, before);

    Assert.True(calendar.AddHoliday(new DateOnly(2026, 9, 9), "National Day").IsSuccess);

    Assert.Equal(4, calendar.WorkingDaysBetween(from, to));
  }

  // TS-ATT-0002, second half. `AC-ATT-0003`: a holiday on a weekend day changes NOTHING, because it was
  // never a working day. This falls out of COUNTING WORKING DAYS rather than counting days and subtracting
  // non-working ones — which is why the loop is written that way round, and why this test would catch it
  // being rewritten the other way.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0003")]
  public void A_holiday_falling_on_a_weekend_day_does_not_reduce_the_count_further()
  {
    var calendar = Calendar(DayOfWeek.Saturday, DayOfWeek.Sunday);
    var from = new DateOnly(2026, 9, 7);
    var to = new DateOnly(2026, 9, 13);

    Assert.Equal(5, calendar.WorkingDaysBetween(from, to));

    // 2026-09-12 is a Saturday.
    Assert.True(calendar.AddHoliday(new DateOnly(2026, 9, 12), "Falls on a Saturday").IsSuccess);

    Assert.Equal(5, calendar.WorkingDaysBetween(from, to));
  }

  // ================================================================================================
  // TS-ATT-0003. THE BOUNDARIES, STATED EXPLICITLY (AC-ATT-0005).
  // ================================================================================================
  //
  // Off-by-one at the range ends is the defect this class of code actually has, and it is invisible in
  // review: `<` versus `<=` reads identically and is wrong by one day per request, forever.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0003")]
  public void The_range_is_inclusive_at_both_ends()
  {
    var calendar = Calendar(DayOfWeek.Saturday, DayOfWeek.Sunday);

    // A single working day is 1, not 0.
    var monday = new DateOnly(2026, 9, 7);
    Assert.Equal(1, calendar.WorkingDaysBetween(monday, monday));

    // A single weekend day is 0.
    var saturday = new DateOnly(2026, 9, 12);
    Assert.Equal(0, calendar.WorkingDaysBetween(saturday, saturday));

    // A range whose first and last days are both weekends still counts what is between them.
    var sunday = new DateOnly(2026, 9, 6);
    var nextSaturday = new DateOnly(2026, 9, 12);
    Assert.Equal(5, calendar.WorkingDaysBetween(sunday, nextSaturday));

    // An inverted range is zero rather than negative or an exception: a caller asking for it has made a
    // mistake, and the honest answer to "how many working days between Friday and Monday" is none.
    Assert.Equal(0, calendar.WorkingDaysBetween(nextSaturday, sunday));
  }

  [Fact]
  [Trait("Criterion", "AC-ATT-0004")]
  public void A_duplicate_holiday_date_is_refused()
  {
    var calendar = Calendar(DayOfWeek.Saturday, DayOfWeek.Sunday);
    var date = new DateOnly(2026, 12, 25);

    Assert.True(calendar.AddHoliday(date, "Christmas").IsSuccess);

    var second = calendar.AddHoliday(date, "Christmas again");

    Assert.True(second.IsFailure);
    Assert.Equal(WorkingCalendarErrors.DuplicateHoliday.Code, second.Error.Code);
  }

  [Fact]
  public void A_holiday_can_be_removed_and_the_count_returns()
  {
    var calendar = Calendar(DayOfWeek.Saturday, DayOfWeek.Sunday);
    var from = new DateOnly(2026, 9, 7);
    var to = new DateOnly(2026, 9, 11);
    var holiday = new DateOnly(2026, 9, 9);

    calendar.AddHoliday(holiday, "Provisional");
    Assert.Equal(4, calendar.WorkingDaysBetween(from, to));

    Assert.True(calendar.RemoveHoliday(holiday).IsSuccess);
    Assert.Equal(5, calendar.WorkingDaysBetween(from, to));

    // Removing it twice is a refusal, not a silent no-op: the second caller believed a holiday was there.
    Assert.True(calendar.RemoveHoliday(holiday).IsFailure);
  }

  // ---- A SEVEN-DAY WEEKEND WOULD SILENTLY DISABLE THE MODULE.
  //
  // Every range would count zero working days, every leave request would consume nothing, and nothing
  // anywhere would report a problem. Refused at construction for that reason rather than on principle.
  [Fact]
  public void A_weekend_covering_every_day_is_refused()
  {
    var all = Enum.GetValues<DayOfWeek>();

    var created = WorkingCalendar.Create(Company, "Never works", all, isDefault: true);

    Assert.True(created.IsFailure);
    Assert.Equal(WorkingCalendarErrors.WeekendPatternCoversEveryDay.Code, created.Error.Code);
  }

  // ---- AND AN EMPTY ONE IS ALLOWED, WHICH IS THE OPPOSITE DECISION FOR A REASON.
  //
  // A seven-day operation — a hospital, a refinery — is a real thing. Refusing it would be this module
  // inventing a policy nobody ruled.
  [Fact]
  public void A_calendar_with_no_weekend_at_all_is_permitted()
  {
    var created = WorkingCalendar.Create(Company, "Continuous", [], isDefault: true);

    Assert.True(created.IsSuccess);
    Assert.Equal(7, created.Value.WorkingDaysBetween(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13)));
  }

  // The weekend pattern round-trips through its persisted form. It is stored as a string, so this is the one
  // place a serialization mistake would silently change which days a company works.
  [Fact]
  public void The_weekend_pattern_round_trips_through_its_persisted_form()
  {
    var pattern = WeekendPattern.Create([DayOfWeek.Friday, DayOfWeek.Saturday]).Value;

    var restored = WeekendPattern.FromPersisted(pattern.PersistedValue);

    Assert.True(restored.IsSuccess);
    Assert.True(restored.Value.IsWeekend(DayOfWeek.Friday));
    Assert.True(restored.Value.IsWeekend(DayOfWeek.Saturday));
    Assert.False(restored.Value.IsWeekend(DayOfWeek.Sunday));
  }

  [Fact]
  public void A_calendar_must_belong_to_a_company()
  {
    var created = WorkingCalendar.Create(Guid.Empty, "Orphan", [DayOfWeek.Sunday], isDefault: true);

    Assert.True(created.IsFailure);
    Assert.Equal(WorkingCalendarErrors.CompanyRequired.Code, created.Error.Code);
  }
}

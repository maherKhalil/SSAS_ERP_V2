using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Calendars;

// ================================================================================================
// THE WORKING CALENDAR (REQ-ATT-0001, REQ-ATT-0002, REQ-ATT-0003; OD-ATT-0004).
// ================================================================================================
//
// The foundation of both halves of this module. Attendance needs it to know which days SHOULD have records;
// leave needs it to know which days a request CONSUMES. `OD-ATT-0001` ruled one calendar serving both, which
// is most of why attendance and leave are one module rather than two.
//
// COMPANY-OWNED (`OD-ATT-0004`), and NOT branch-owned — the one place in this module where the branch
// question was answered "no". `OD-ATT-0011`'s split put branch ownership on RECORDS, because a supervisor
// observes their own branch. A calendar is company policy, following `Department`'s asserted classification
// rather than `Employee`'s. `DEC-ATT-0014` requires that answer to be asserted rather than merely true, so
// an architecture test asserts this type does NOT implement `IBranchOwnedEntity`.
//
// NOT append-only. A holiday list is maintained — public holidays get moved by decree more often than
// anyone would like — so its changes are audited rather than frozen. Contrast `AttendanceRecord`, which is
// append-only from creation because it states what happened.

// ---- THE WEEKEND IS DATA. THIS IS THE POINT OF THE TYPE.
//
// `BR-ATT-0001`: no code path may assume Saturday and Sunday. Fri/Sat, Sat/Sun and Thu/Fri weekends all
// occur in the regions `ADR-024`'s locale handling contemplates, and a constant here would be wrong for a
// large share of this product's market in a way that produces plausible numbers rather than an error.
//
// Persisted as a short ordinal string ("5,6") rather than a child table. It is a set drawn from a closed
// seven-value domain, never joined on, never range-queried and never aggregated: the only operation is "is
// this date a weekend", performed after the calendar is loaded. A child table would add a join to every
// calendar read to model seven possible values.
public sealed class WeekendPattern : ValueObject
{
  // Long enough for all seven ordinals comma-separated ("0,1,2,3,4,5,6").
  public const int MaximumLength = 13;

  private readonly HashSet<DayOfWeek> days;

  private WeekendPattern(HashSet<DayOfWeek> days) => this.days = days;

  public IReadOnlyCollection<DayOfWeek> Days => days;

  public string PersistedValue =>
    string.Join(',', days.Select(day => (int)day).OrderBy(ordinal => ordinal));

  public bool IsWeekend(DayOfWeek day) => days.Contains(day);

  public static Result<WeekendPattern> Create(IEnumerable<DayOfWeek>? days)
  {
    if (days is null)
    {
      return Result.Failure<WeekendPattern>(WorkingCalendarErrors.InvalidWeekendPattern);
    }

    var set = new HashSet<DayOfWeek>(days);

    // ---- A SEVEN-DAY WEEKEND IS REFUSED, AND A ZERO-DAY ONE IS NOT.
    //
    // All seven means no working day ever exists, which makes `WorkingDaysBetween` answer zero for every
    // range and every leave request consume nothing — a configuration that silently disables the module.
    //
    // The empty set is ALLOWED. A seven-day operation is a real thing (a hospital, a refinery), and refusing
    // it would be this module inventing a policy nobody ruled.
    if (set.Count >= 7)
    {
      return Result.Failure<WeekendPattern>(WorkingCalendarErrors.WeekendPatternCoversEveryDay);
    }

    return Result.Success(new WeekendPattern(set));
  }

  public static Result<WeekendPattern> FromPersisted(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return Result.Success(new WeekendPattern([]));
    }

    var days = new HashSet<DayOfWeek>();
    foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      if (!int.TryParse(part, out var ordinal) || ordinal is < 0 or > 6)
      {
        return Result.Failure<WeekendPattern>(WorkingCalendarErrors.InvalidWeekendPattern);
      }

      days.Add((DayOfWeek)ordinal);
    }

    return Result.Success(new WeekendPattern(days));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return PersistedValue;
  }
}

public sealed class WorkingCalendarName : ValueObject
{
  public const int MaximumLength = 200;

  private WorkingCalendarName(string value) => Value = value;

  public string Value { get; }

  public static Result<WorkingCalendarName> Create(string? value)
  {
    if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength || value.Any(char.IsControl))
    {
      return Result.Failure<WorkingCalendarName>(WorkingCalendarErrors.InvalidName);
    }

    return Result.Success(new WorkingCalendarName(value.Trim()));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}

// ---- A HOLIDAY IS A CALENDAR DAY, NOT AN INSTANT.
//
// `HolidayDate` is a `DateOnly` mapping to SQL `date`, and this is the one place the module departs from the
// `DateTimeOffset` convention used everywhere else. Deliberately: storing a public holiday as an instant
// invites an offset conversion to move it across midnight into the previous day, and a holiday that lands on
// the wrong side of midnight silently changes what every leave request spanning it consumed.
//
// The same reasoning governs `AttendanceRecord.AttendanceDate` and the leave request bounds.
public sealed class CalendarHoliday : Entity<Guid>, ITenantOwnedEntity
{
  private CalendarHoliday(Guid id, Guid workingCalendarId, DateOnly holidayDate, string name)
    : base(id)
  {
    WorkingCalendarId = workingCalendarId;
    HolidayDate = holidayDate;
    Name = name;
  }

  // EF materialization only.
  private CalendarHoliday(Guid id)
    : base(id) => Name = string.Empty;

  public const int NameMaximumLength = 200;

  public Guid TenantId { get; set; }

  public Guid WorkingCalendarId { get; private set; }

  public DateOnly HolidayDate { get; private set; }

  public string Name { get; private set; }

  internal static Result<CalendarHoliday> Create(Guid workingCalendarId, DateOnly holidayDate, string? name)
  {
    if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaximumLength || name.Any(char.IsControl))
    {
      return Result.Failure<CalendarHoliday>(WorkingCalendarErrors.InvalidHolidayName);
    }

    return Result.Success(new CalendarHoliday(Guid.NewGuid(), workingCalendarId, holidayDate, name.Trim()));
  }
}

public sealed class WorkingCalendar
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private readonly List<CalendarHoliday> holidays = [];
  private string normalizedName = string.Empty;

  private WorkingCalendar(Guid id, Guid companyId, WorkingCalendarName name, WeekendPattern weekend, bool isDefault)
    : base(id)
  {
    CompanyId = companyId;
    Name = name;
    normalizedName = name.Value.ToUpperInvariant();
    Weekend = weekend;
    IsDefault = isDefault;
  }

  // EF materialization only.
  private WorkingCalendar(Guid id)
    : base(id)
  {
    Name = null!;
    Weekend = null!;
  }

  public Guid WorkingCalendarId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public WorkingCalendarName Name { get; private set; }

  // The searchable shadow, written up front rather than after the failure. `DEC-POS-0030` records that a
  // value-converted property translates in a PROJECTION but not in a PREDICATE, and that HR shipped a
  // department search which threw for every search term. GL and Payroll both wrote these ahead of the
  // failure; this is the fourth module to do so.
  public string NormalizedName => normalizedName;

  public WeekendPattern Weekend { get; private set; }

  public bool IsDefault { get; private set; }

  public IReadOnlyCollection<CalendarHoliday> Holidays => holidays;

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<WorkingCalendar> Create(
    Guid companyId, string? name, IEnumerable<DayOfWeek>? weekendDays, bool isDefault)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<WorkingCalendar>(WorkingCalendarErrors.CompanyRequired);
    }

    var calendarName = WorkingCalendarName.Create(name);
    if (calendarName.IsFailure)
    {
      return Result.Failure<WorkingCalendar>(calendarName.Error);
    }

    var weekend = WeekendPattern.Create(weekendDays);
    if (weekend.IsFailure)
    {
      return Result.Failure<WorkingCalendar>(weekend.Error);
    }

    return Result.Success(new WorkingCalendar(Guid.NewGuid(), companyId, calendarName.Value, weekend.Value, isDefault));
  }

  public Result Rename(string? name)
  {
    var calendarName = WorkingCalendarName.Create(name);
    if (calendarName.IsFailure)
    {
      return Result.Failure(calendarName.Error);
    }

    Name = calendarName.Value;
    normalizedName = calendarName.Value.Value.ToUpperInvariant();
    return Result.Success();
  }

  public Result ChangeWeekend(IEnumerable<DayOfWeek>? weekendDays)
  {
    var weekend = WeekendPattern.Create(weekendDays);
    if (weekend.IsFailure)
    {
      return Result.Failure(weekend.Error);
    }

    Weekend = weekend.Value;
    return Result.Success();
  }

  public Result AddHoliday(DateOnly holidayDate, string? name)
  {
    // `AC-ATT-0004`. Two holidays on one date in one calendar is a data-entry mistake, and permitting it
    // would make `WorkingDaysBetween` correct only by accident — the set-based day count below would not
    // double-subtract, but every list a client rendered would show the day twice.
    if (holidays.Any(holiday => holiday.HolidayDate == holidayDate))
    {
      return Result.Failure(WorkingCalendarErrors.DuplicateHoliday);
    }

    var created = CalendarHoliday.Create(Id, holidayDate, name);
    if (created.IsFailure)
    {
      return Result.Failure(created.Error);
    }

    created.Value.TenantId = TenantId;
    holidays.Add(created.Value);
    return Result.Success();
  }

  public Result RemoveHoliday(DateOnly holidayDate)
  {
    var holiday = holidays.FirstOrDefault(candidate => candidate.HolidayDate == holidayDate);
    if (holiday is null)
    {
      return Result.Failure(WorkingCalendarErrors.HolidayNotFound);
    }

    holidays.Remove(holiday);
    return Result.Success();
  }

  public bool IsWorkingDay(DateOnly date) =>
    !Weekend.IsWeekend(date.DayOfWeek) && holidays.All(holiday => holiday.HolidayDate != date);

  // ================================================================================================
  // THE QUERY EVERYTHING ELSE DEPENDS ON (REQ-ATT-0003).
  // ================================================================================================
  //
  // INCLUSIVE AT BOTH ENDS. `AC-ATT-0005` states the boundary explicitly because off-by-one at the range
  // ends is the defect this class of code actually has: a single working day returns 1, and a single weekend
  // day returns 0.
  //
  // A holiday falling on a weekend day does NOT reduce the count further (`AC-ATT-0003`) — it was never a
  // working day, so there is nothing to subtract. That falls out of counting working days rather than
  // counting days and subtracting non-working ones, which is why the loop is written this way round.
  public int WorkingDaysBetween(DateOnly fromDate, DateOnly toDate)
  {
    if (toDate < fromDate)
    {
      return 0;
    }

    var holidayDates = holidays.Select(holiday => holiday.HolidayDate).ToHashSet();
    var count = 0;
    for (var date = fromDate; date <= toDate; date = date.AddDays(1))
    {
      if (!Weekend.IsWeekend(date.DayOfWeek) && !holidayDates.Contains(date))
      {
        count++;
      }
    }

    return count;
  }
}

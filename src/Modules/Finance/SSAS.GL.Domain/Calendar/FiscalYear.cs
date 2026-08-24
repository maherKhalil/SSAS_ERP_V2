using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Calendar;

// THE FISCAL CALENDAR (REQ-GL-0009..0011, BR-GL-0003, BR-GL-0005, OD-GL-0004).
//
// ================================================================================================
// COMPANY-OWNED. EACH COMPANY CLOSES ITS OWN BOOKS.
// ================================================================================================
//
// `OD-GL-0004` ruled the calendar COMPANY-level, which is what a legal entity normally requires: one company
// closing its December does not close another's. Implementing `ICompanyOwnedEntity` is what makes that real
// — every write here, including closing a period, runs `AuthorizeCurrentCompanyAsync` at the write boundary
// before anything reaches SQL.
//
// It also fixes `BR-GL-0005`'s uniqueness scope: journal numbers are unique within
// **(CompanyId, FiscalYear)**, not within a tenant-wide year.
//
// ---- THE YEAR OWNS ITS PERIODS, AND THE CONTIGUITY INVARIANT IS THE REASON.
//
// `AC-GL-0011` requires the periods to be contiguous and non-overlapping. That is a property of the SET, and
// no individual period can evaluate it — so the periods are created only through the year, in one call, and
// validated together. There is no `AddPeriod` that could leave the set momentarily invalid.
public sealed class FiscalYear : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private readonly List<FiscalPeriod> periods = [];

  private FiscalYear(Guid id, string code, DateTimeOffset startUtc, DateTimeOffset endUtc)
    : base(id)
  {
    Code = code;
    StartUtc = startUtc;
    EndUtc = endUtc;
  }

  // EF materialization only.
  private FiscalYear(Guid id)
    : base(id)
  {
    Code = null!;
  }

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public string Code { get; private set; }

  public DateTimeOffset StartUtc { get; private set; }

  // Exclusive, matching `FiscalPeriod`. See the note there on why half-open intervals are used.
  public DateTimeOffset EndUtc { get; private set; }

  public IReadOnlyCollection<FiscalPeriod> Periods => periods.AsReadOnly();

  public DateTimeOffset CreatedUtc { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[]? RowVersion { get; set; }

  public const int MaximumCodeLength = 32;

  public static Result<FiscalYear> Create(
    string? code,
    DateTimeOffset startUtc,
    DateTimeOffset endUtc,
    IReadOnlyList<(string Name, DateTimeOffset StartUtc, DateTimeOffset EndUtc)> periodDefinitions)
  {
    ArgumentNullException.ThrowIfNull(periodDefinitions);

    var trimmed = code?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumCodeLength)
    {
      return Result.Failure<FiscalYear>(CalendarErrors.InvalidCode);
    }

    var start = startUtc.ToUniversalTime();
    var end = endUtc.ToUniversalTime();
    if (start >= end)
    {
      return Result.Failure<FiscalYear>(CalendarErrors.InvalidRange);
    }

    if (periodDefinitions.Count == 0)
    {
      return Result.Failure<FiscalYear>(CalendarErrors.NoPeriods);
    }

    // ---- THE PERIODS MUST PARTITION THE YEAR EXACTLY.
    //
    // Sorted first, so the caller's ordering is not part of the contract — a definition list that is correct
    // but shuffled describes the same calendar. Then each period must begin exactly where the previous one
    // ended, the first at the year's start and the last at its end. That single walk rejects gaps, overlaps,
    // periods outside the year, and a set that does not reach the end, which is why `AC-GL-0011` is one
    // criterion rather than four.
    var ordered = periodDefinitions
      .OrderBy(definition => definition.StartUtc)
      .ToArray();

    var cursor = start;
    var year = new FiscalYear(Guid.NewGuid(), trimmed, start, end);

    foreach (var definition in ordered)
    {
      var periodStart = definition.StartUtc.ToUniversalTime();
      var periodEnd = definition.EndUtc.ToUniversalTime();

      if (periodStart != cursor || periodEnd <= periodStart)
      {
        return Result.Failure<FiscalYear>(CalendarErrors.PeriodsNotContiguous);
      }

      var name = definition.Name?.Trim();
      if (string.IsNullOrEmpty(name) || name.Length > MaximumCodeLength)
      {
        return Result.Failure<FiscalYear>(CalendarErrors.InvalidCode);
      }

      year.periods.Add(new FiscalPeriod(Guid.NewGuid(), year.Id, name, periodStart, periodEnd));
      cursor = periodEnd;
    }

    if (cursor != end)
    {
      return Result.Failure<FiscalYear>(CalendarErrors.PeriodsNotContiguous);
    }

    return Result.Success(year);
  }

  public bool Covers(DateTimeOffset instant) => instant >= StartUtc && instant < EndUtc;

  public FiscalPeriod? PeriodCovering(DateTimeOffset instant) =>
    periods.SingleOrDefault(period => period.Covers(instant));

  // ---- RESOLVED FROM THE ENTRY DATE, NEVER SUPPLIED BY THE CALLER (AC-GL-0002, TS-GL-0005).
  //
  // A caller who could name the period could post into a period the date does not belong to, which would
  // make `BR-GL-0003` unenforceable by inspection: the closed-period check would be guarding a field the
  // caller chose. The request contract therefore has no period property at all, and a request carrying one
  // is refused by the strict reader as an unknown property rather than ignored.
  public Result<FiscalPeriod> ResolveOpenPeriodFor(DateTimeOffset entryDateUtc)
  {
    var period = PeriodCovering(entryDateUtc.ToUniversalTime());
    if (period is null)
    {
      return Result.Failure<FiscalPeriod>(CalendarErrors.PeriodNotFound);
    }

    var open = period.EnsureOpenForPosting();
    return open.IsFailure
      ? Result.Failure<FiscalPeriod>(open.Error)
      : Result.Success(period);
  }
}

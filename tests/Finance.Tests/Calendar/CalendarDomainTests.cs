using SSAS.BuildingBlocks.Domain;
using SSAS.GL.Domain.Calendar;

namespace SSAS.Finance.Tests.Calendar;

// THE FISCAL CALENDAR (REQ-GL-0009..0011, BR-GL-0003, OD-GL-0004).
public sealed class CalendarDomainTests
{
  private static readonly DateTimeOffset YearStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset YearEnd = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

  private static (string, DateTimeOffset, DateTimeOffset)[] TwelveMonths()
  {
    var periods = new List<(string, DateTimeOffset, DateTimeOffset)>();
    for (var month = 1; month <= 12; month++)
    {
      var start = new DateTimeOffset(2026, month, 1, 0, 0, 0, TimeSpan.Zero);
      periods.Add(($"2026-{month:D2}", start, start.AddMonths(1)));
    }

    return [.. periods];
  }

  [Fact]
  [Trait("Decision", "OD-GL-0004")]
  public void A_fiscal_year_is_company_owned_which_is_what_makes_closing_a_company_scoped_write()
  {
    // The interface is the mechanism, not the column: ICompanyOwnedEntity is what makes
    // TenantDbContext.ApplyCompanyRulesAsync run AuthorizeCurrentCompanyAsync before a close reaches SQL.
    Assert.Contains(typeof(ICompanyOwnedEntity), typeof(FiscalYear).GetInterfaces());
    Assert.Contains(typeof(ITenantOwnedEntity), typeof(FiscalYear).GetInterfaces());
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0010")]
  public void Every_gl_entity_that_must_survive_cutover_carries_the_tenant_marker()
  {
    // ---- THE OWNED CHILDREN ARE THE POINT OF THIS TEST.
    //
    // TenantCutoverCopyPlan derives the E3 manifest by reflecting over ITenantOwnedEntity. A child that
    // does not implement it is absent from the manifest and therefore absent from Shared-to-Dedicated
    // cutover — which fails SILENTLY, taking the rows with it. Being owned is a domain fact; being copied
    // is a reflection fact, and only the interface expresses the second.
    Type[] mustBeCopied =
    [
      typeof(SSAS.GL.Domain.Accounts.Account),
      typeof(FiscalYear),
      typeof(FiscalPeriod),
      typeof(SSAS.GL.Domain.Journals.JournalDraft),
      typeof(SSAS.GL.Domain.Journals.JournalDraftLine),
      typeof(SSAS.GL.Domain.Journals.JournalEntry),
      typeof(SSAS.GL.Domain.Journals.JournalLine)
    ];

    Assert.All(mustBeCopied, type =>
      Assert.Contains(typeof(ITenantOwnedEntity), type.GetInterfaces()));

    // Seven, and the count is asserted so an eighth entity cannot be added without this list being read.
    Assert.Equal(7, mustBeCopied.Length);
  }

  [Fact]
  public void A_year_whose_periods_partition_it_exactly_is_accepted()
  {
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths());

    Assert.True(year.IsSuccess);
    Assert.Equal(12, year.Value.Periods.Count);
    Assert.All(year.Value.Periods, period => Assert.Equal(FiscalPeriodStatus.Open, period.Status));
  }

  [Fact]
  public void The_caller_ordering_of_periods_does_not_matter()
  {
    // A correct definition that arrives shuffled describes the same calendar. Sorting first means the
    // caller's ordering is not part of the contract.
    var shuffled = TwelveMonths().Reverse().ToArray();

    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, shuffled);

    Assert.True(year.IsSuccess);
    Assert.Equal(
      TwelveMonths().Select(period => period.Item1),
      year.Value.Periods.OrderBy(period => period.StartUtc).Select(period => period.Name));
  }

  [Fact]
  [Trait("Decision", "AC-GL-0011")]
  public void A_gap_between_periods_is_refused()
  {
    var withGap = TwelveMonths().Where(period => period.Item1 != "2026-06").ToArray();

    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, withGap);

    Assert.True(year.IsFailure);
    Assert.Equal("Gl.FiscalPeriodsNotContiguous", year.Error.Code);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0011")]
  public void An_overlap_between_periods_is_refused_by_the_same_rule_as_a_gap()
  {
    // A gap and an overlap are the same defect seen from opposite sides: in both cases the periods do not
    // partition the year, and an entry date resolves to either none or two. One error, not two.
    var periods = TwelveMonths();
    periods[5] = (periods[5].Item1, periods[5].Item2, periods[5].Item3.AddDays(10));

    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, periods);

    Assert.True(year.IsFailure);
    Assert.Equal("Gl.FiscalPeriodsNotContiguous", year.Error.Code);
  }

  [Fact]
  public void Periods_that_stop_short_of_the_year_end_are_refused()
  {
    var short_ = TwelveMonths().Take(11).ToArray();

    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, short_);

    Assert.True(year.IsFailure);
    Assert.Equal("Gl.FiscalPeriodsNotContiguous", year.Error.Code);
  }

  [Fact]
  public void A_year_with_no_periods_is_refused()
  {
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, []);

    Assert.True(year.IsFailure);
    Assert.Equal("Gl.FiscalYearHasNoPeriods", year.Error.Code);
  }

  [Fact]
  public void A_year_that_ends_before_it_starts_is_refused()
  {
    var year = FiscalYear.Create("FY2026", YearEnd, YearStart, TwelveMonths());

    Assert.True(year.IsFailure);
    Assert.Equal("Gl.FiscalYearRangeInvalid", year.Error.Code);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0002")]
  public void The_period_covering_a_date_is_resolved_from_the_date_alone()
  {
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths()).Value;

    var resolved = year.ResolveOpenPeriodFor(new DateTimeOffset(2026, 6, 15, 9, 30, 0, TimeSpan.Zero));

    Assert.True(resolved.IsSuccess);
    Assert.Equal("2026-06", resolved.Value.Name);
  }

  [Fact]
  public void The_half_open_interval_puts_a_boundary_instant_in_the_later_period()
  {
    // Inclusive start, EXCLUSIVE end. Chosen so contiguity is expressible without the resolution of the
    // underlying type becoming part of the business rule — with an inclusive end, "contiguous" would mean
    // "the next period starts one tick later" and the size of a tick would matter.
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths()).Value;

    var boundary = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    var resolved = year.ResolveOpenPeriodFor(boundary);

    Assert.True(resolved.IsSuccess);
    Assert.Equal("2026-07", resolved.Value.Name);
  }

  [Fact]
  public void A_date_outside_the_year_resolves_to_no_period()
  {
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths()).Value;

    var resolved = year.ResolveOpenPeriodFor(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

    Assert.True(resolved.IsFailure);
    Assert.Equal("Gl.FiscalPeriodNotFound", resolved.Error.Code);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  public void A_closed_period_refuses_posting_and_says_so_distinctly_from_being_absent()
  {
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths()).Value;
    var june = year.Periods.Single(period => period.Name == "2026-06");

    Assert.True(june.Close().IsSuccess);

    var resolved = year.ResolveOpenPeriodFor(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));

    // CLOSED, not NOT-FOUND. A caller who cannot tell the two apart cannot tell "reopen the period" from
    // "define the calendar", and those are very different actions.
    Assert.True(resolved.IsFailure);
    Assert.Equal("Gl.FiscalPeriodClosed", resolved.Error.Code);
  }

  [Fact]
  public void Closing_and_reopening_are_explicit_and_each_refuses_a_repeat()
  {
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths()).Value;
    var period = year.Periods.First();

    Assert.True(period.Close().IsSuccess);
    Assert.Equal("Gl.FiscalPeriodAlreadyClosed", period.Close().Error.Code);

    Assert.True(period.Reopen().IsSuccess);
    Assert.Equal("Gl.FiscalPeriodAlreadyOpen", period.Reopen().Error.Code);
  }

  [Fact]
  public void Periods_may_be_closed_out_of_order_because_no_rule_forbids_it()
  {
    // lifecycle-model.md raised "must periods close in order?" and recorded that nothing says which. No
    // ordering rule was invented — this test pins the ABSENCE so that adding one later is a deliberate
    // change with a failing test to notice it, rather than a silent tightening.
    var year = FiscalYear.Create("FY2026", YearStart, YearEnd, TwelveMonths()).Value;
    var may = year.Periods.Single(period => period.Name == "2026-05");

    Assert.True(may.Close().IsSuccess);
    Assert.All(
      year.Periods.Where(period => period.Name != "2026-05"),
      period => Assert.Equal(FiscalPeriodStatus.Open, period.Status));
  }
}

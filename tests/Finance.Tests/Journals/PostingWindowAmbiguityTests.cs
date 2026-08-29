using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Posting;
using SSAS.GL.Contracts.Posting;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;

namespace SSAS.Finance.Tests.Journals;

// ==================================================================================================
// THE POSTING WINDOW REPORTS AN AMBIGUOUS CALENDAR AS ITSELF (T-188).
// ==================================================================================================
//
// ---- ⚠ THIS EXISTS BECAUSE THE PAYROLL-SIDE TEST DOES NOT COVER THE EMITTER, AND I MEASURED THAT.
//
// `An_ambiguous_calendar_refuses_rather_than_falling_through_as_open` sets the window DIRECTLY on the
// payroll host's stub, so it proves the two CONSUMERS refuse an unknown status — and **making
// `GlJournalPoster` report `Open` instead leaves it green.** Planting the emitter is what showed that;
// planting only the consumers would have shipped a test I believed spanned both.
//
// **A plant tells you what reddens a test, not what the test covers.** A test whose name spans two layers
// needs a plant in each, and this is the other layer.
public sealed class PostingWindowAmbiguityTests
{
  private static readonly Guid Company = Guid.NewGuid();
  private static readonly DateTimeOffset EntryDate = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public async Task Two_covering_years_are_reported_as_ambiguous_rather_than_not_found()
  {
    var poster = new GlJournalPoster(
      new UnusedJournals(), new UnusedAccounts(),
      new AmbiguousCalendar(), new UnusedUnitOfWork());

    var window = await poster.InspectPostingWindowAsync(Company, EntryDate);

    // The specific status, not merely "not open". `PostingWindowStatus.CalendarAmbiguous` states why it
    // exists rather than collapsing into `PeriodNotFound`, and that argument is not restated here.
    Assert.Equal(PostingWindowStatus.CalendarAmbiguous, window.Status);

    // Everything but Status is null on a refusal, by the record's own rule — which is also what makes a
    // consumer that guards on `FiscalPeriodId is null` degrade safely against a status it has never seen.
    Assert.Null(window.FiscalPeriodId);
    Assert.False(window.IsOpen);
  }

  private sealed class AmbiguousCalendar : IFiscalCalendarRepository
  {
    public Task<Result<FiscalYear?>> GetCoveringAsync(
      Guid companyId, DateTimeOffset instantUtc, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Failure<FiscalYear?>(CalendarErrors.AmbiguousCoveringYear));

    public Task<FiscalYear?> GetByIdAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window query reads only the covering year.");

    public Task<FiscalPeriod?> GetPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window query reads only the covering year.");

    public Task<bool> CodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window query reads only the covering year.");

    public Task<bool> OverlapsExistingAsync(
      Guid companyId, DateTimeOffset startUtc, DateTimeOffset endUtc,
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window query reads only the covering year.");

    public Task AddAsync(FiscalYear year, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("The window query writes nothing.");
  }

  // The other three dependencies THROW rather than returning empties: this query must not touch them, and
  // a stub that quietly answered would hide the day one starts to.
  private sealed class UnusedJournals : IJournalEntryRepository
  {
    public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task<int> NextJournalNumberAsync(
      Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task<bool> ReversalExistsAsync(Guid originalJournalEntryId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");
  }

  private sealed class UnusedAccounts : IAccountRepository
  {
    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task<IReadOnlyList<Account>> GetManyAsync(
      IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task AddAsync(Account account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");
  }

  private sealed class UnusedUnitOfWork : ITenantUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("unused");
  }
}

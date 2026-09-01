using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY JOURNAL POSTER OPENS ITS TRANSACTION BEFORE THE CHECKS, NOT BETWEEN THEM AND THE WRITE (248).
// ==================================================================================================
//
// `BR-GL-0003` (post only into an OPEN period) and `BR-GL-0004` (post only to an ACTIVE account) are
// read-then-act rules: a period read as open, or an account read as active, must still be so when the
// row is written. Reading outside the transaction and writing inside it leaves exactly the window those
// rules exist to close.
//
// ---- ⚠ WHY THIS TEST EXISTS AT ALL, WHICH IS A FINDING RATHER THAN A GAP SOMEBODY LEFT.
//
// `PostJournalCommandHandlers.cs` cited `TS-GL-0011` for the closed-between case. THAT SCENARIO HAS NO
// TEST -- zero files in `tests/` -- and the only assertion that existed was the STATIC one:
// `CalendarDomainTests.A_closed_period_refuses_posting_and_says_so_distinctly_from_being_absent` closes a
// period and then resolves it. Nothing closed one BETWEEN the read and the write.
//
// ⚠⚠ AND THE SIBLING HANDLER WAS ALREADY GUARDED. `FiscalYearDefinitionOrderTests` asserts this exact
// ordering for `DefineFiscalYearCommandHandler`, and ITS OWN COMMENT SAYS
// *"`PostJournalCommandHandlers` RECORDS the same rule for its own aggregate."* Records. Written from the
// guarded side, describing the unguarded side, in a word that means DOCUMENTS and reads as ASSERTS.
//
// ---- ⚠⚠⚠ WHAT THIS PROVES, AND WHAT IT DOES NOT.
//
// IT PROVES THE CODE IS SHAPED TO CLOSE THE WINDOW. It does NOT prove the window is closed. Only a
// contention test -- a period open at read time and closed before the write -- proves that, and it is
// Integration-scoped and separate. DO NOT READ THIS FILE AS COVERING THAT.
//
// ---- THE POPULATION IS DERIVED, BECAUSE THE ASYMMETRY THAT PRODUCED THIS ITEM WAS A HAND-PICKED ONE.
//
// The sibling names ONE handler. Naming three here would reproduce the same defect one level down: there
// are THREE posters today -- `PostJournalDraftCommandHandler`, `ReverseJournalCommandHandler` and
// `GlJournalPoster`, the last of which is not a `CommandHandler` at all and would be missed by any filter
// keyed on that suffix. A FOURTH must be covered by arriving, not by being remembered.
//
// `DefineFiscalYearCommandHandler` reads no period -- it checks code and overlap -- so it is outside this
// population by construction and is guarded by `FiscalYearDefinitionOrderTests`.
//
// ---- ⚠⚠⚠ AND THE PREDICATE MUST NOT REQUIRE THE PROPERTY UNDER TEST. THE FIRST VERSION DID.
//
// It selected types that OPEN A TRANSACTION and read a period. That excluded, BY CONSTRUCTION, any
// handler which reads a period and FAILS to open one -- which is precisely the defect this test exists to
// find. THE POPULATION WAS SELF-SELECTING: the defect made a member invisible to the check.
//
// The predicate is now READS A FISCAL PERIOD AND WRITES. Membership is the situation; the transaction is
// the assertion. That change put a real member in scope that the first version could not see:
// `SetFiscalPeriodStateCommandHandler`, which reads a period, decides a transition is legal, and saves --
// WITH NO TRANSACTION AT ALL.
//
// ⚠⚠ IT IS CORRECT, BY A DIFFERENT MECHANISM, AND THAT IS WHY IT IS EXCLUDED BY NAME RATHER THAN BY THE
// PREDICATE. `FiscalPeriod.RowVersion` is mapped `.IsRowVersion().IsConcurrencyToken()`, the repository
// returns a TRACKED entity, and `TenantUnitOfWork` turns a `DbUpdateConcurrencyException` into
// `ConcurrencyConflict` which `GlApiErrorMapper` maps to a conflict response. Two concurrent state
// changes are resolved by optimistic concurrency; the second is refused rather than silently winning.
//
// ⚠ AN EXCLUSION BY REASON IS FINE; AN EXCLUSION BY THE PROPERTY UNDER TEST IS NOT -- AND THIS EXCLUSION
// ASSERTS ITS OWN REASON. If somebody removes `.IsRowVersion()` from the period, the justification
// collapses and `The_period_state_writer_is_serialised_by_a_concurrency_token_instead` goes red. An
// exemption whose grounds nothing checks is an exemption that outlives them.
//
// ⚠ NOTE WHICH SIDE OF THE RACE IT IS ON: this handler is THE WRITER THAT CLOSES A PERIOD. It is the
// other half of the contention the posters guard against, so whatever step 2 eventually asserts depends
// on how this one serialises.
public sealed class JournalPostingOrderTests
{
  private const string ApplicationRoot = "src/Modules/Finance/SSAS.GL.Application";

  // The one member excluded by reason rather than by predicate. See the header, and see
  // The_period_state_writer_is_serialised_by_a_concurrency_token_instead, which asserts the reason.
  private const string PeriodStateWriter = "SetFiscalPeriodStateCommandHandler";

  // The three that exist today. Named as a FLOOR, never as the population: a derivation that silently
  // returned nothing would make every assertion below vacuous, which is the failure this whole class of
  // guard keeps producing when nobody plants against it.
  private static readonly string[] KnownPosters =
  [
    "GlJournalPoster",
    "PostJournalDraftCommandHandler",
    "ReverseJournalCommandHandler",
  ];

  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  [Trait("Decision", "BR-GL-0004")]
  public void Every_journal_poster_opens_its_transaction_before_reading_the_period_and_the_accounts()
  {
    var posters = Posters();

    // ---- ANTI-VACUITY, TWO WAYS. The count catches a derivation that stopped matching; the names catch
    // a derivation that still matches something else. Neither alone is enough: a regex that broke and
    // found one unrelated class would satisfy a bare `Assert.NotEmpty`.
    Assert.True(
      posters.Count >= KnownPosters.Length,
      $"the derivation found {posters.Count} poster(s); it should find at least {KnownPosters.Length}. " +
      $"Found: {string.Join(", ", posters.Keys.OrderBy(name => name, StringComparer.Ordinal))}");

    foreach (var known in KnownPosters)
    {
      Assert.True(posters.ContainsKey(known), $"{known} is no longer recognised as a journal poster");
    }

    // ---- ⚠ AND THE CONDITIONAL BRANCH BELOW NEEDS ITS OWN FLOOR (B23 at the scale of one `if`).
    //
    // BR-GL-0004's ordering is asserted only where `EnsureAccountsCanReceive` is CALLED, because a
    // reversal reuses the original lines' accounts and has no reason to call it. But a floor that counts
    // POSTERS does not notice if every poster stopped calling it: the branch would then apply to zero
    // members and pass green, asserting nothing about BR-GL-0004 at all. Two call it today.
    var accountCheckers = posters
      .Where(poster => poster.Key != PeriodStateWriter)
      .Count(poster => FirstIndexOfAny(
        poster.Value, "EnsureAccountsCanReceiveAsync", "EnsureAccountsCanReceive") >= 0);

    Assert.True(
      accountCheckers >= 2,
      $"only {accountCheckers} poster(s) validate accounts before writing. BR-GL-0004's ordering is " +
      "asserted conditionally, so if the callers disappear the assertion silently applies to nobody.");

    foreach (var (name, body) in posters)
    {
      if (name == PeriodStateWriter)
      {
        // Excluded by REASON, asserted separately below -- never by the predicate.
        continue;
      }

      var transaction = body.IndexOf("BeginTransactionAsync", StringComparison.Ordinal);
      var period = FirstIndexOfAny(body, "ResolveOpenPeriodFor", "ResolvePeriodAsync");
      var save = body.IndexOf("SaveChangesAsync", StringComparison.Ordinal);

      // Without these the comparisons below would all be -1 and compare equal — the same vacuity the
      // sibling test guards with `Assert.NotEmpty(body)`.
      Assert.True(transaction >= 0, $"{name} opens no transaction");
      Assert.True(period >= 0, $"{name} reads no fiscal period");
      Assert.True(save >= 0, $"{name} never saves");

      Assert.True(
        transaction < period,
        $"{name}: THE PERIOD IS READ OUTSIDE THE TRANSACTION — BR-GL-0003 becomes read-then-act");

      Assert.True(
        period < save,
        $"{name}: the period is read after the write, which checks nothing");

      // The account check is not universal: a reversal re-uses the original lines' accounts and does not
      // re-validate them. Asserted only where it exists, so this cannot fail for a poster that has no
      // reason to make the call.
      var accounts = FirstIndexOfAny(body, "EnsureAccountsCanReceiveAsync", "EnsureAccountsCanReceive");
      if (accounts >= 0)
      {
        Assert.True(
          transaction < accounts,
          $"{name}: THE ACCOUNTS ARE READ OUTSIDE THE TRANSACTION — BR-GL-0004 becomes read-then-act");

        Assert.True(accounts < save, $"{name}: the account check runs after the write");
      }
    }
  }

  // ---- THE EXCLUSION'S OWN GROUNDS, ASSERTED — AND THEY CHANGED WHEN 249 LANDED.
  //
  // BEFORE 249 this handler took no transaction at all and was exempt because the period carries a mapped
  // concurrency token. It now takes a transaction AND the EXCLUSIVE side of the posting fence, and it is
  // STILL EXEMPT FROM THE ORDERING ASSERTION — for a reason that has to be stated rather than inherited:
  //
  // ⚠⚠ IT READS THE PERIOD BEFORE ITS TRANSACTION, DELIBERATELY. The fence resource is company-scoped and
  // `SetFiscalPeriodStateCommand` carries only a period id, so the company is not known until the year is
  // read. The read cannot follow the lock here, and does not need to:
  //
  //   the FENCE serialises POSTER against CLOSER, inside overlapping transactions;
  //   the TOKEN catches a STALE period read across SEPARATE requests.
  //
  // DIFFERENT PAIRS. The fence does not make the token redundant and the token never covered posters.
  // ⚠ NEITHER MAY BE REMOVED AS TIDYING, and this test asserts BOTH so that removing either is a red.
  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  public void The_period_state_writer_is_serialised_by_a_token_and_the_exclusive_fence()
  {
    var posters = Posters();

    Assert.True(
      posters.ContainsKey(PeriodStateWriter),
      $"{PeriodStateWriter} is no longer in the population, so its exemption is describing nothing");

    var configuration = File.ReadAllText(Path.Combine(
      FindRepositoryRoot(),
      "src", "Modules", "Finance", "SSAS.GL.Infrastructure", "Persistence", "CalendarConfigurations.cs"));

    var mapping = configuration
      .Split('\n')
      .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
      .FirstOrDefault(line =>
        line.Contains("period => period.RowVersion", StringComparison.Ordinal) &&
        line.Contains("IsRowVersion()", StringComparison.Ordinal));

    Assert.True(
      mapping is not null,
      "FiscalPeriod.RowVersion is no longer mapped IsRowVersion(). The period-state writer relies on the " +
      "token for stale reads across separate requests, and that half of its exemption is now false.");

    // ---- AND THE OTHER HALF: THE EXCLUSIVE FENCE, WHICH IS WHAT DRAINS IN-FLIGHT POSTERS.
    var handler = File.ReadAllText(Path.Combine(
      FindRepositoryRoot(),
      "src", "Modules", "Finance", "SSAS.GL.Application", "Calendar", "CalendarCommandHandlers.cs"));

    var body = string.Join(
      Environment.NewLine,
      handler.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    Assert.Contains("AcquireForStateChangeAsync", body, StringComparison.Ordinal);

    Assert.True(
      body.IndexOf("BeginTransactionAsync", StringComparison.Ordinal) <
      body.LastIndexOf("AcquireForStateChangeAsync", StringComparison.Ordinal),
      "the exclusive fence is taken outside a transaction, so it is released before the state change commits");
  }

  private static int FirstIndexOfAny(string body, params string[] needles)
  {
    var found = needles
      .Select(needle => body.IndexOf(needle, StringComparison.Ordinal))
      .Where(index => index >= 0)
      .ToArray();

    return found.Length == 0 ? -1 : found.Min();
  }

  // A poster is any type under the GL application that OPENS A TRANSACTION AND THEN READS A FISCAL
  // PERIOD. That is the mechanism, and it is what the two business rules turn on — not the class-name
  // suffix, which `GlJournalPoster` does not carry.
  private static Dictionary<string, string> Posters()
  {
    var posters = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var path in Directory.EnumerateFiles(
      Path.Combine(FindRepositoryRoot(), ApplicationRoot.Replace('/', Path.DirectorySeparatorChar)),
      "*.cs",
      SearchOption.AllDirectories))
    {
      foreach (var (name, body) in Classes(File.ReadAllText(path)))
      {
        var readsPeriod = FirstIndexOfAny(
          body, "ResolveOpenPeriodFor", "ResolvePeriodAsync", "GetPeriodAsync") >= 0;

        if (readsPeriod && body.Contains("SaveChangesAsync", StringComparison.Ordinal))
        {
          posters[name] = body;
        }
      }
    }

    return posters;
  }

  // ⚠ COMMENTS ARE STRIPPED BEFORE ANY ORDER IS READ, and the sibling records why: a comment naming the
  // members in prose satisfies every `IndexOf` and the order is then read from documentation rather than
  // from code. That mirror has been hit three times in this repository — and in this file it would be
  // especially easy, because the comments here NAME the very calls being ordered.
  private static IEnumerable<(string Name, string Body)> Classes(string source)
  {
    var stripped = string.Join(
      '\n',
      source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    var declarations = Regex.Matches(stripped, @"\b(?:public|internal)\s+sealed\s+class\s+(\w+)");

    for (var i = 0; i < declarations.Count; i++)
    {
      var start = declarations[i].Index;
      var end = i + 1 < declarations.Count ? declarations[i + 1].Index : stripped.Length;

      yield return (declarations[i].Groups[1].Value, stripped[start..end]);
    }
  }

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
         directory is not null;
         directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}

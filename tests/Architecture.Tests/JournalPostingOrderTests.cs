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
// `DefineFiscalYearCommandHandler` opens a transaction and reads no period -- it checks code and overlap
// -- so it is outside this population by construction and is guarded by `FiscalYearDefinitionOrderTests`.
public sealed class JournalPostingOrderTests
{
  private const string ApplicationRoot = "src/Modules/Finance/SSAS.GL.Application";

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

    foreach (var (name, body) in posters)
    {
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
        if (body.Contains("BeginTransactionAsync", StringComparison.Ordinal) &&
            FirstIndexOfAny(body, "ResolveOpenPeriodFor", "ResolvePeriodAsync") >= 0)
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

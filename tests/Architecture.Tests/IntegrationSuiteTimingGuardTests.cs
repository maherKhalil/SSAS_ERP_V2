using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// NO DATABASE-FREE TEST LIVES IN THE INTEGRATION SUITE (T-257).
// ==================================================================================================
//
// ---- ⚠ THIS EXISTS BECAUSE THE CLASS REGENERATED IN THREE DAYS.
//
// A duration sweep of the Integration suite on 2026-08-27 found eight tests that touch no database. They
// were moved. **Re-running the sweep on a current corpus three days later found a ninth — one that had not
// existed on the 27th**, arriving with the 42 tests added in between.
//
// **So the eight were never a backlog to clear; they are an ARRIVAL RATE.** A cleanup that runs once is the
// wrong instrument for a source that keeps producing, and a sweep only runs when somebody remembers to run
// it. This asserts the property continuously instead.
//
// ---- WHY IT COSTS ANYTHING TO GET WRONG.
//
// `GATE_SCOPE=TASK` never runs the Integration suite. A database-free test written there is checked by
// **nothing** during ordinary development, and it takes a ~24-minute SQL Server dependency to reach. The
// nine that were stranded asserted real invariants — cutover manifest coverage, copy ordering, permission
// catalogue composition, EF key configuration — none of which needed a server.
//
// ---- ⚠ THE SIGNAL IS SYNCHRONY, AND THE THRESHOLD BELOW IS WHY IT IS NOT A DURATION.
//
// The sweep measured DURATION and the distribution had a cliff with nothing in it:
//
//     0.0005 s ... 0.0102 s   the nine database-free tests
//     ---------- nothing at all between ----------
//     2.3936 s ... 262.8 s    every test that reaches the server
//
// **A gap that wide is a category, not a tail** — which is what made the finding unambiguous. But a
// duration threshold needs a RUN to evaluate, and this guard must fire when the test is WRITTEN, not when
// somebody next spends 24 minutes.
//
// **Synchrony is the same category, visible statically.** All nine were `public void`; every test that
// awaits a server is `async Task`. Measured after the moves: **772 test methods in the Integration suite,
// 772 of them `async Task`, zero synchronous.**
//
// ---- AND IT IS REFUSABLE, WHICH IS THE TEST OF WHETHER A GUARD MEANS ANYTHING.
//
// `public void` is not an exotic shape this repository never writes — `Architecture.Tests` is full of them,
// and that is the point: **the pattern is ordinary everywhere except here.** The next database-free test
// written into Integration reddens this on arrival.
public sealed class IntegrationSuiteTimingGuardTests
{
  // ⚠ EVERY ENTRY NEEDS A REASON, AND THE LIST IS EMPTY BECAUSE NOTHING NEEDED ONE.
  //
  // A synchronous Integration test would have to be doing something the async rule does not anticipate —
  // driving a process, asserting on a fixture already built. **If that arrives, name it here with why it
  // cannot move**, rather than relaxing the rule for everything.
  private static readonly (string Test, string Why)[] Allowed = [];

  private static readonly Regex TestMethod = new(
    @"\[(?:Fact|Theory)\][^\]]*?\]?\s*(?:\[[^\]]*\]\s*)*public\s+(async\s+Task|void)\s+(\w+)\s*\(",
    RegexOptions.Compiled | RegexOptions.Singleline);

  [Fact]
  public void No_integration_test_is_synchronous_because_a_synchronous_test_needs_no_database()
  {
    var synchronous = new List<string>();
    var asynchronous = 0;
    var files = 0;

    foreach (var file in IntegrationTestSources())
    {
      files++;
      var source = WithoutComments(File.ReadAllText(file));

      foreach (Match match in TestMethod.Matches(source))
      {
        if (match.Groups[1].Value == "void")
        {
          var name = match.Groups[2].Value;
          if (!Allowed.Any(entry => entry.Test == name))
          {
            synchronous.Add($"{Path.GetFileName(file)}: {name}");
          }
        }
        else
        {
          asynchronous++;
        }
      }
    }

    // ⚠ ANTI-VACUITY, AND THE SECOND HALF IS THE ONE THAT MATTERS.
    //
    // A file count catches the walk dying. **It does not catch the MATCHER dying** — a regex that stopped
    // recognising test methods would find zero synchronous ones and read as success, which is exactly the
    // shape this suite has been finding all week. So the asynchronous count is asserted too: the guard has
    // to prove it can still see tests before its zero means anything.
    Assert.True(files >= 40,
      $"only {files} Integration test files were scanned; the walk has degraded.");
    Assert.True(asynchronous >= 500,
      $"only {asynchronous} async test methods were recognised; the matcher has stopped matching and " +
      "'zero synchronous tests' would mean nothing rather than being reassuring.");

    Assert.True(synchronous.Count == 0,
      "a synchronous test lives in the Integration suite. Every test that reaches the database is " +
      "`async Task`, so a `public void` one almost certainly needs no server — and `GATE_SCOPE=TASK` " +
      "never runs this suite, so it is checked by nothing during ordinary development:\n  " +
      string.Join("\n  ", synchronous) +
      "\n\nMove it to Architecture.Tests, or add it to `Allowed` with the reason it cannot move.");
  }

  // Every allowlist entry justifies itself, or the list becomes somewhere to put inconvenient tests.
  [Fact]
  public void Every_allowed_synchronous_test_states_why_it_cannot_move()
  {
    foreach (var (test, why) in Allowed)
    {
      Assert.False(string.IsNullOrWhiteSpace(why), $"{test} is allowed without a reason.");
    }
  }

  // Comment content blanked, string literals left intact, length preserved. A commented-out test must not
  // count, and these files discuss `public void` in prose.
  private static string WithoutComments(string text)
  {
    var buffer = text.ToCharArray();
    var i = 0;

    while (i < buffer.Length)
    {
      if (buffer[i] == '"')
      {
        i++;
        while (i < buffer.Length && buffer[i] != '"')
        {
          if (buffer[i] == '\\' && i + 1 < buffer.Length)
          {
            i++;
          }

          i++;
        }

        i++;
        continue;
      }

      if (buffer[i] == '/' && i + 1 < buffer.Length && buffer[i + 1] == '/')
      {
        while (i < buffer.Length && buffer[i] != '\n')
        {
          buffer[i++] = ' ';
        }

        continue;
      }

      i++;
    }

    return new string(buffer);
  }

  private static IEnumerable<string> IntegrationTestSources()
  {
    var root = Path.Combine(RepositoryRoot(), "tests", "Integration.Tests");
    return Directory
      .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory!.FullName;
  }
}

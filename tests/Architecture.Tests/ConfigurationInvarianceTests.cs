using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// NOTHING IN THIS PRODUCT COMPILES DIFFERENTLY PER CONFIGURATION (259).
// ==================================================================================================
//
// No source file in `src/` or `tests/` may exclude code by build configuration. The consequence that makes
// it worth asserting: A SUITE'S TEST COUNT CANNOT DIFFER BETWEEN DEBUG AND RELEASE. Where the recorded
// totals do differ, the difference is not a property of the product — it is two measurements taken at
// different tree states.
//
// ---- ⚠⚠ THIS TEST EXISTS TO WATCH ANOTHER TEST'S PREMISE, AND THAT TEST IS NOT HERE YET.
//
// `Every_suite_reports_the_same_total_in_both_configurations` asserts the Debug and Release rows of
// `.claude/handoff/test-baseline.txt` are equal for every suite. IT IS CORRECT AND IT IS CURRENTLY RED,
// because `GATE_SCOPE=TASK` writes only Debug rows while Release rows move only on a green `PHASE` run, and
// no PHASE run has been possible on this box since `849835a` — the pre-leg memory floor has not been met.
// IT IS HELD, NOT ABANDONED, and lands with the first green PHASE run that makes the file self-consistent.
//
// ⚠ HAND-EDITING THOSE ROWS IS NOT THE FIX AND HAS ALREADY BEEN TRIED: `849835a`'s own subject is *eight
// stale Release baselines corrected*, and they had drifted again within a day. CORRECTING VALUES DOES
// NOTHING ABOUT THE MECHANISM THAT PRODUCES THEM.
//
// ---- ⚠⚠⚠ AND THE DAY THIS TEST FAILS, THE HELD ONE BECOMES WRONG RATHER THAN MERELY UNHAPPY.
//
// If anyone adds conditional compilation, a Debug/Release total difference becomes LEGITIMATE, and an
// equality guard would then report a real difference as staleness — confidently, and in the wrong
// direction. Nothing else in the tree would notice. A guard whose premise is unwatched is an exemption
// with no grounds, one level up.
public sealed class ConfigurationInvarianceTests
{
  [Fact]
  [Trait("Decision", "DEC-L-008")]
  public void No_source_file_compiles_differently_per_configuration()
  {
    var sources = ProductionAndTestSources().ToArray();

    // Anti-vacuity. A walk that found nothing would report success over an empty tree, which is exactly
    // how a path or filter change would present itself here.
    Assert.True(
      sources.Length >= 500,
      $"only {sources.Length} source files were walked; the enumeration has degraded and this guard is " +
      "asserting nothing rather than passing");

    var offenders = new List<string>();

    foreach (var path in sources)
    {
      var text = File.ReadAllText(path);

      // ⚠ ANCHORED AT LINE START, AND THE FIRST VERSION WAS NOT — IT MATCHED ITSELF.
      //
      // A preprocessor directive must begin its own line, and so must the attribute. Searching anywhere in
      // the text made THIS FILE its own first offender: the prose above names the tokens, and the patterns
      // themselves are string literals containing them. ⚠⚠ STRIPPING COMMENTS WOULD NOT HAVE BEEN ENOUGH —
      // the literals are code, not prose.
      //
      // ⚠⚠⚠ PREFER A MATCHER THAT CANNOT MATCH ITSELF BY CONSTRUCTION OVER ONE THAT EXEMPTS ITSELF. An
      // exemption is a claim somebody has to maintain; an anchor is a fact about the language.
      if (Regex.IsMatch(text, @"^[ \t]*#if\s+!?\s*(DEBUG|RELEASE)\b", RegexOptions.Multiline) ||
          Regex.IsMatch(text, @"^[ \t]*\[\s*Conditional\s*\(", RegexOptions.Multiline))
      {
        offenders.Add(Path.GetFileName(path));
      }
    }

    Assert.True(
      offenders.Count == 0,
      $"these compile differently per configuration: " +
      $"{string.Join(", ", offenders.Distinct().OrderBy(name => name, StringComparer.Ordinal))}. " +
      "A suite's Debug and Release totals may now legitimately differ, so any guard asserting they are " +
      "equal has just become wrong and must be changed rather than the baseline.");
  }

  private static IEnumerable<string> ProductionAndTestSources()
  {
    var root = FindRepositoryRoot();

    foreach (var area in new[] { "src", "tests" })
    {
      foreach (var path in Directory.EnumerateFiles(
        Path.Combine(root, area), "*.cs", SearchOption.AllDirectories))
      {
        if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
          continue;
        }

        yield return path;
      }
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

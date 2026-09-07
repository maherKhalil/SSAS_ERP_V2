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
  // ==================================================================================================
  // ⚠⚠⚠ THIS WAS ONE TEST WITH A SEMANTIC NAME OVER A TWO-TOKEN PREDICATE (T-094)
  // ==================================================================================================
  //
  // It was `No_source_file_compiles_differently_per_configuration` — a claim about the OUTCOME of a build.
  // The predicate was `#if [!]DEBUG|RELEASE` plus a line-anchored `[Conditional(`, over `*.cs` only.
  //
  // ⚠ THE MECHANISM IS MSBUILD, NOT C#. A custom constant defined only in the Release property group and
  // guarded by `#if TRACE_SQL`, or a `<Compile Remove Condition="'$(Configuration)'=='Release'">`, changes
  // what compiles without a single matching token in any `.cs` file. **The population was missing the
  // layer where the divergence is actually configured.**
  //
  // ---- ⚠⚠ AND THE TWO HALVES COMPOSE, WHICH IS WHY THIS IS NOT A WIDER VOCABULARY.
  //
  // `DEBUG` and `TRACE` are defined by the SDK for you, so a `.cs` guard must name them explicitly —
  // there is no build file to catch. ***EVERY OTHER SYMBOL REQUIRES `DefineConstants`***, which is a build
  // file concern and is caught below by MECHANISM rather than by spelling. So:
  //
  //   the C# test    catches the symbols that need no declaration  (DEBUG, RELEASE)
  //   the build test catches the declaration of every other symbol (DefineConstants), and the
  //                  configuration-conditioned item groups that need no symbol at all
  //
  // **Together they cover the mechanism; neither alone does, and widening either one's vocabulary would
  // not have closed the gap** — `#if TRACE_SQL` is harmless precisely because the build test forbids
  // anything that could define `TRACE_SQL`.
  //
  // ⚠ SEARCHED BEFORE WIDENING (T-094). Across every tracked `.csproj`, `.props`, `.targets` and `.sln`,
  // there is NO `DefineConstants` and NO `Condition` referencing `$(Configuration)` anywhere. The only
  // occurrence of `$(Configuration)` in the repository is a PATH — `SSAS.Integration.Tests.csproj:56`,
  // `Value="…\bin\$(Configuration)\net8.0\…"` — which selects where a helper binary is looked up and
  // changes nothing about what compiles. It is used as a discriminating control below.
  [Fact]
  [Trait("Decision", "DEC-L-008")]
  public void No_source_file_carries_a_debug_or_release_preprocessor_branch()
  {
    var sources = ProductionAndTestSources().ToArray();

    // Anti-vacuity. A walk that found nothing would report success over an empty tree, which is exactly
    // how a path or filter change would present itself here.
    Assert.True(
      sources.Length >= 500,
      $"only {sources.Length} source files were walked; the enumeration has degraded and this guard is " +
      "asserting nothing rather than passing");

    // ⚠⚠⚠ THE FLOOR ABOVE CANNOT SEE THIS WALK LOSE HALF ITS SUBJECT (T-098).
    //
    // The walk covers TWO areas, `src` and `tests`, and the floor is one number over their union. Drop
    // `tests` from the areas array and `src` alone still clears 500 comfortably — **a floor is a claim
    // about MAGNITUDE and the failure here is SHAPE.** That is not hypothetical: T-090 narrowed a walk's
    // pattern from `*` to `*.cs` and the population control failed FIRST, before the ban, while a floor of
    // 400 against 1181 would have sat green through it.
    //
    // So each area is asserted by a NAMED MEMBER that only that area can supply. A narrowing is then
    // caught by name — "the tests tree left the walk" — rather than by a ban silently passing.
    // ⚠ GROUNDED, NOT `Assert.Contains(collection, predicate)`. The first version of these two lines used
    // it and failed with *"Assert.Contains() Failure: Filter not matched in collection"* — a tier-1 silent
    // site over a 1,537-file walk, which is exactly what `AssertionMessageChoice.cs` rules against, written
    // in the same session as that ruling. The plant is what surfaced it: the message was only ever seen
    // because the assertion was deliberately made to fail.
    Assert.True(
      sources.Any(path => path.EndsWith("PersistenceDbContext.cs", StringComparison.Ordinal)),
      $"the walk returned {sources.Length} files but none from `src` — `PersistenceDbContext.cs` is " +
      "missing, so the production tree has left this population while the floor above stayed green.");

    Assert.True(
      sources.Any(path => path.EndsWith("ModelWalk.cs", StringComparison.Ordinal)),
      $"the walk returned {sources.Length} files but none from `tests` — `ModelWalk.cs` is missing, so " +
      "the TEST tree has left this population. ⚠ The floor above cannot see this: `src` alone is over " +
      "1,100 files and clears 500 on its own, which is why this assertion exists rather than a tighter " +
      "number. Restore the area to the walk; do not lower the floor.");

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

  // ---- THE MSBUILD LAYER, WHERE A PER-CONFIGURATION DIVERGENCE IS ACTUALLY CONFIGURED (T-094).
  //
  // ⚠ ITS OWN FLOOR, NOT A SHARE OF THE SOURCE FLOOR (T-263). The `.cs` walk and the build-file walk fail
  // on different days — a `*.cs` pattern that stops matching and a `*.csproj` pattern that stops matching
  // are different accidents — and 500 source files would carry a collapsed build walk straight past a
  // combined floor.
  //
  // ⚠⚠⚠ REACH PROBE, BOTH COLOURS MEASURED (T-094). A real per-configuration divergence was planted in
  // `Directory.Build.props` — the repository-wide file, so it would have applied to every project at once:
  //
  //     <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
  //       <DefineConstants>$(DefineConstants);TRACE_SQL</DefineConstants>
  //     </PropertyGroup>
  //
  //   THE SOURCE TEST -> GREEN. It walks `*.cs`; a build file is not one.
  //   THIS TEST       -> RED, and BOTH arms fired independently: *"defines a compilation symbol"* and
  //                     *"has an item or property conditioned on $(Configuration)"*.
  //
  // The run was **1 passed, 1 failed**. That is the whole argument for this test existing: the divergence
  // was live, repository-wide, and the guard named for catching exactly this reported success.
  [Fact]
  [Trait("Decision", "DEC-L-008")]
  public void No_build_file_makes_compilation_depend_on_the_configuration()
  {
    var buildFiles = BuildFiles();

    Assert.True(buildFiles.Length >= 30,
      $"only {buildFiles.Length} build files were walked; the enumeration has degraded and this guard is " +
      "asserting nothing rather than passing. There are 33 project files alone.");

    // ⚠ THE POSITIVE CONTROL ON THE POPULATION, not just its size: the two repository-wide files are the
    // ones that would set a constant for EVERY project at once, and a walk that found 30 `.csproj` while
    // silently missing these would look entirely healthy.
    // ⚠ GROUNDED (T-102). These were `Assert.Contains(buildFiles, path => …)` — silent over a 154-file
    // walk. All three population controls written tonight used that form, in the same session as the
    // ruling against it: **they were the newest idea in the room, and novelty suppressed review harder
    // than routine would have.**
    Assert.True(
      buildFiles.Any(path => path.EndsWith("Directory.Build.props", StringComparison.Ordinal)),
      $"the walk returned {buildFiles.Length} build files but no `Directory.Build.props`. That is the one " +
      "file that can set a property for EVERY project at once, so a walk that misses it is blind to the " +
      "broadest possible per-configuration divergence while still looking healthy.");

    Assert.True(
      buildFiles.Any(path => path.EndsWith("Directory.Packages.props", StringComparison.Ordinal)),
      $"the walk returned {buildFiles.Length} build files but no `Directory.Packages.props`. Central " +
      "package management lives there, so a walk that misses it cannot see a package version conditioned " +
      "on the configuration.");

    // The matcher controls, each against the form it would really appear in.
    Assert.Matches(CustomSymbol, "    <DefineConstants>$(DefineConstants);TRACE_SQL</DefineConstants>");
    Assert.Matches(ConfigurationConditioned,
      "  <Compile Remove=\"Diagnostics.cs\" Condition=\" '$(Configuration)' == 'Release' \" />");
    Assert.Matches(ConfigurationConditioned,
      "  <PropertyGroup Condition=\"'$(Configuration)'=='Debug'\">");

    // ⚠⚠ THE DISCRIMINATING CONTROL, AND IT IS THE ONE THAT MATTERS HERE. `$(Configuration)` appears
    // legitimately in a PATH — `bin\$(Configuration)\net8.0` — which selects where a built file is found
    // and changes nothing about what compiles. A predicate matching bare `$(Configuration)` would flag it,
    // and a guard that fires on correct code gets deleted rather than fixed. So the match is on
    // `Condition=` specifically, and this asserts the real line stays unflagged.
    Assert.DoesNotMatch(ConfigurationConditioned,
      "    <AssemblyMetadata Include=\"Host\" Value=\"$(MSBuildThisFileDirectory)..\\bin\\$(Configuration)\\net8.0\\h.exe\" />");

    var offenders = new List<string>();

    foreach (var path in buildFiles)
    {
      var text = File.ReadAllText(path);

      if (Regex.IsMatch(text, CustomSymbol))
      {
        offenders.Add($"{Path.GetFileName(path)}: defines a compilation symbol");
      }

      if (Regex.IsMatch(text, ConfigurationConditioned))
      {
        offenders.Add($"{Path.GetFileName(path)}: has an item or property conditioned on $(Configuration)");
      }
    }

    Assert.True(offenders.Count == 0,
      $"the build makes compilation depend on the configuration: {string.Join("; ", offenders)}.\n" +
      "  Debug and Release no longer produce the same assemblies, so any guard asserting their test " +
      "totals are equal has just become wrong and must be CHANGED rather than its baseline re-recorded. " +
      "`DEC-L-008` is the decision this belongs to.\n" +
      "  ⚠ And note what this closes that the C# guard cannot: a symbol declared here is what makes an " +
      "`#if SOMETHING` branch live in the first place. With no DefineConstants anywhere, every " +
      "preprocessor branch other than DEBUG/RELEASE is dead code by construction.");
  }

  // `DefineConstants` in any form. Its mere PRESENCE is the finding — a constant defined unconditionally
  // is still a constant some future property group can redefine per configuration, and the repository has
  // none today.
  private const string CustomSymbol = @"<\s*DefineConstants\s*>";

  // ⚠ ANCHORED ON `Condition=`, NOT ON `$(Configuration)`. See the discriminating control above: the only
  // real occurrence in this repository is inside a path VALUE and is legitimate.
  private const string ConfigurationConditioned = @"Condition\s*=\s*""[^""]*\$\(\s*Configuration\s*\)";

  // Every file kind that can carry an MSBuild instruction. `.sln` is included because solution
  // configurations map projects to Debug/Release and could exclude one from a configuration entirely.
  //
  // ⚠⚠ ONE OF THESE FOUR IS UNEXERCISED AND CANNOT BE EXERCISED TODAY (T-101). `git ls-files '*.targets'`
  // returns ZERO — this repository contains no `.targets` file at all. So a quarter of the declared
  // population has never been walked by anything, and the T-094 plant could not have reached it: it went
  // into `Directory.Build.props`, which is a `*.props` match.
  //
  // ***A PATTERN LIST IS A CLAIM ABOUT COVERAGE, AND THIS ONE IS THREE-QUARTERS TESTED.*** The entry stays
  // because a `.targets` file added tomorrow would carry exactly the same risk as a `.props` file does —
  // it is excluded by ABSENCE, not by kind, so the first one added must be checked against this guard
  // rather than assumed covered. **Do not add a `.targets` file to make this testable**; that manufactures
  // a subject to satisfy an instrument, which is the wrong direction.
  private static readonly string[] BuildFilePatterns = ["*.csproj", "*.props", "*.targets", "*.sln"];

  private static string[] BuildFiles()
  {
    var root = FindRepositoryRoot();

    return
    [.. BuildFilePatterns
      .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
      .Where(path =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(path => path, StringComparer.Ordinal)];
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

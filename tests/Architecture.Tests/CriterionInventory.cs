using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// DECLARED, CITED AND TRIPWIRED CRITERIA, DERIVED FROM THE TREE. AN INSTRUMENT, NOT A WITNESS.
// ==================================================================================================
//
// **This carries no criterion trait and asserts nothing about any product behaviour.** It exists because
// every coverage number this project quotes — *"FP-009 is 15 of 19"*, *"FP-014 is 31 of 54"* — was being
// computed by a script in a session scratchpad. ***A NUMBER WHOSE ONLY DERIVATION DIES WITH A SESSION IS
// RETIRED THE DAY THAT SESSION ENDS, WHATEVER ANYONE STILL BELIEVES ABOUT IT.***
//
// ---- ⚠⚠⚠ THE DEFINITIONS, WRITTEN OUT SO THE NUMBERS SURVIVE EVEN THIS FILE.
//
// **DECLARED** — ids matching `AC-<PREFIX>-dddd` at the START of a line in one of SIX shapes, inside a
// feature folder's markdown. *Six because two declaration conventions coexist in this tree:* a table row
// `| `ID` |`, a bullet `- **ID**` (optionally with a `(TAG)`, optionally colons), a bullet with an em dash,
// ``**`ID`** —``, ``**`ID` —``, and a heading `## `ID``. **Keying on one shape returns ZERO for half the
// tree, which reads identically to "nothing is declared".**
//
// **CITED** — a test method carrying `[Trait("K","ID")]` where K is one of FOUR keys: `Criterion`,
// `Acceptance`, `Decision`, `AcceptanceCriteria`. ***THE MATCH IS CONJUNCTIVE — KEY AND VALUE IN ONE
// PATTERN*** — so a fifth key is invisible to it by construction rather than by accident.
//
// **TRIPWIRED** — the same shape with key `Tripwire`. ***REPORTED SEPARATELY AND NEVER ADDED TO CITED:
// a guard asserting that a criterion's subject DOES NOT EXIST is the opposite of a witness for it.***
//
// ⚠⚠ **COMMENTS ARE STRIPPED BEFORE MATCHING.** This tree is prose-dense — a raw matcher over it measures
// the DOCUMENTATION, and a commented-out trait would count as a citation.
//
// ---- ⚠ WHAT THIS DELIBERATELY DOES NOT DO: IT ASSERTS NO COUNTS.
//
// **A test pinning "FP-009 has 15 citations" reddens on the next commit that adds one** — high rate, and
// the change is the intended work rather than a regression. *That fails the second question a strict guard
// must answer: is there a lower-noise design with the same detection?* **There is: assert the instrument's
// INTEGRITY and let the counts move freely.** The companion test checks that this can still find spec
// folders, still recognises all four keys, and still strips comments — ***so it reddens when the
// INSTRUMENT rots, which is the failure nobody would otherwise notice.***
internal static class CriterionInventory
{
  private const string IdPattern = @"AC-[A-Z]+-\d{4}";

  private static readonly Regex[] DeclarationShapes =
  [
    new(@"^\|\s*`(" + IdPattern + @")`\s*\|", RegexOptions.Multiline),
    new(@"^-\s+\*\*(" + IdPattern + @")(?:\s*\([A-Z]+\))?:?\*\*:?", RegexOptions.Multiline),
    new(@"^-\s+\*\*(" + IdPattern + @")\*\*\s*[-—]", RegexOptions.Multiline),
    new(@"^\*\*`(" + IdPattern + @")`\*\*\s*[-—]", RegexOptions.Multiline),
    new(@"^\*\*`(" + IdPattern + @")`\s*[-—]", RegexOptions.Multiline),
    new(@"^#{2,4}\s+`?(" + IdPattern + @")`?", RegexOptions.Multiline),
  ];

  private static readonly Regex CitedTrait =
    new(@"Trait\s*\(\s*""(?:Criterion|Acceptance|Decision|AcceptanceCriteria)""\s*,\s*""(" + IdPattern + @")""");

  private static readonly Regex TripwireTrait =
    new(@"Trait\s*\(\s*""Tripwire""\s*,\s*""(" + IdPattern + @")""");

  public static IReadOnlyCollection<string> FeatureFolders() =>
    [.. Directory.EnumerateDirectories(Path.Combine(RepositoryRoot(), "docs", "17-features"))
      .Select(Path.GetFileName)
      .Where(name => name is not null)
      .Select(name => name!)
      .OrderBy(name => name, StringComparer.Ordinal)];

  // A folder CLAIMS to have criteria by carrying the file that holds them. A folder without one is a
  // feature created before its specification — an ordinary state, and not the same thing as a folder
  // whose declarations have stopped being recognised.
  public static bool HasCriteriaFile(string featureFolder) =>
    File.Exists(Path.Combine(RepositoryRoot(), "docs", "17-features", featureFolder, "acceptance-criteria.md"));

  public static IReadOnlyCollection<string> DeclaredIn(string featureFolder)
  {
    var directory = Path.Combine(RepositoryRoot(), "docs", "17-features", featureFolder);
    var declared = new SortedSet<string>(StringComparer.Ordinal);

    foreach (var file in Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
    {
      var text = File.ReadAllText(file);
      foreach (var shape in DeclarationShapes)
      {
        foreach (Match match in shape.Matches(text))
        {
          declared.Add(match.Groups[1].Value);
        }
      }
    }

    return declared;
  }

  public static IReadOnlyCollection<string> Cited() => Scan(CitedTrait);

  public static IReadOnlyCollection<string> Tripwired() => Scan(TripwireTrait);

  private static SortedSet<string> Scan(Regex pattern)
  {
    var found = new SortedSet<string>(StringComparer.Ordinal);

    foreach (var file in TestSources())
    {
      foreach (Match match in pattern.Matches(StripComments(File.ReadAllText(file))))
      {
        found.Add(match.Groups[1].Value);
      }
    }

    return found;
  }

  public static IEnumerable<string> TestSources() =>
    Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "tests"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

  // Line comments only. A `[Trait(...)]` is written on one line in this tree, so a block-comment stripper
  // would add failure modes without adding reach.
  public static string StripComments(string source) =>
    string.Join(
      "\n",
      source.Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));

  public static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}

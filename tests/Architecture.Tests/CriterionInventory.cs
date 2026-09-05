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

  // Bare ids, for resolution and for the comment scan. ⚠ NOT a declaration shape and never a citation:
  // it matches an id ANYWHERE, which is exactly wrong for counting and exactly right for asking
  // "does this id exist" and "has anyone written about it".
  private static readonly Regex AnyId = new(IdPattern);

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

  // ---- EVERY CRITERION DECLARED ANYWHERE, AS ONE SET.
  //
  // `DeclaredIn` answers per feature because coverage is reported per feature. Resolution is a different
  // question — *does this id exist at all* — and asking it per folder would report a criterion cited from
  // another module's test as unresolvable. **The union is the right population for that and the wrong one
  // for counting, so both exist and neither is a substitute.**
  public static IReadOnlyCollection<string> DeclaredEverywhere() =>
    [.. FeatureFolders().SelectMany(DeclaredIn).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)];

  // ---- ⚠⚠⚠ THE THIRD STATE OF A CRITERION ID IN THIS TREE, AND IT HAD NO NAME UNTIL NOW.
  //
  // A criterion can be CITED (a trait claims a witness), TRIPWIRED (a guard claims the subject does not
  // exist), or neither. **"Neither" has been read as one thing and it is two.** *A criterion nobody has ever
  // written a word about is a completely different object from one with three paragraphs of reasoned refusal
  // written beside the test that would have cited it* — and both currently render as the same empty cell.
  //
  // ***THIS COLLECTS THE SECOND KIND.*** A comment naming a criterion is the trace that reading it leaves.
  // **The reasoning in that comment is not machine-readable and never will be; the fact that somebody wrote
  // it is.** Measured at `c945332`: **472 distinct ids named in comments, and 36 of them carried by no trait
  // at all** — thirty-six criteria that were read, considered, and left untagged.
  //
  // ⚠ IT IS EVIDENCE OF ATTENTION AND NOT OF COVERAGE, AND THE DIFFERENCE IS THE WHOLE POINT. A discussed
  // criterion may be discussed in order to REFUSE it. **Nothing here should ever be added to a cited count.**
  //
  // ---- ⚠⚠⚠ THIS POPULATION IS SELF-AFFECTING, AND THAT IS CORRECT RATHER THAN BROKEN. DO NOT "FIX" IT.
  //
  // ***WRITING ABOUT A CRITERION MAKES IT DISCUSSED. AN AUDITOR WHO TAKES NOTES MOVES THE POPULATION.***
  // Measured: **36 at `c945332`, 38 at `f99a28c`** — and the two new members are the two ids the commit in
  // between spelled out in its own prose. *The alternative, excluding the auditor's own writing, would make
  // the measure depend on who counts as an auditor, which is worse than the property it removes.*
  //
  // ***SO THE CAVEAT IS PERMANENT AND BELONGS HERE RATHER THAN IN WHOEVER'S REPORT: THIS COUNT CANNOT BE A
  // TREND LINE WITHOUT NAMING WHO WAS WRITING DURING THE INTERVAL.*** **A delta on it is a fact about
  // somebody's attention, not about the tree — and the first person to diff two runs will read their own
  // notes as the subject improving.**
  //
  // ---- ⚠⚠⚠ TWO BLINDNESSES, FOUND BY WORKING THE RESIDUE RATHER THAN BY READING THIS METHOD.
  //
  // ***BOTH MAKE THE COMPLEMENT — "declared, uncited, and in no comment" — OVERSTATE ITS SUBJECT. A BUCKET
  // NAMED FOR WHAT THE MATCHER MISSED IS A LABEL ABOUT THE MATCHER, NOT ABOUT THE CRITERION.***
  //
  // **1. A DISPOSITION IN `docs/` IS INVISIBLE.** This scans `src/` and `tests/`. `AC-POS-0054` carries
  // *"WITHDRAWN 2026-09-02"* and `AC-DEP-0016` carries *"SUPERSEDED, NOT MET (annotated 2026-08-22)"* — both
  // fully reasoned, **in their own declarations**. ⚠ *Widening the scan to `docs/` would not fix this and
  // would break the method: a criteria file names every criterion it declares, so every id would become
  // "discussed" and this population would collapse into the declared set.* **The signal would have to be an
  // ANNOTATION, which has no reliable form. Stated as a bound rather than chased.**
  //
  // **2. ⚠⚠ A HUMAN ABBREVIATION OF A LIST OF IDS MATCHES ONLY ITS FIRST MEMBER. TWO FORMS, BOTH PRESENT.**
  //
  //   ***THE RANGE***    `TenantLifecycleApplicationTests`: *"the `AC-TEN-0021..0030` block, deferred with
  //                the endpoints"* — ten criteria considered, one id matched.
  //   ***THE ELIDED PREFIX***  `PlatformSupportAuthenticationSurfaceArchitectureTests:163`:
  //                ``  `AC-TEN-0012`, `0021`, `0022`, `0023`, `0026`, `0027` and `0028` all describe  ``
  //                ``  AUTHORIZATION on `/api/platform/tenants` routes  `` — **seven considered, ONE matched.**
  //
  // ⚠⚠⚠ **CORRECTED 2026-09-05: THE RANGE WAS PUBLISHED AS THE CAUSE AND THE ELIDED PREFIX IS THE ONE THAT
  // ACTUALLY HID THEM.** *`AC-TEN-0022`, `0023`, `0026`, `0027` and `0028` sat in the "nobody has ever written
  // about this" bucket while the site above disposed of all seven over a CLOSED POPULATION — every
  // `"/api/platform/…"` literal in `src/` resolving to something other than `tenants`.* **That is a stronger
  // disposition than the range one, and it was invisible for a shallower reason: the author wrote the prefix
  // once, the way anyone would.**
  //
  // ***THIS IS THE FIFTH MATCHER FAILURE MODE THIS PROJECT HAS LOGGED — after CASE, WORD-BOUNDARY, WINDOW and
  // READING DIRECTION — AND IT IS NOTATION.*** **A range is not an enumeration; a list with the prefix written
  // once is not an enumeration either.** *The remedy is the same writing convention for both and it cannot be
  // a regex: a disposition covering several criteria must spell every id in full, because these are the forms
  // no reader's grep and no instrument can expand.*
  public static IReadOnlyCollection<string> DiscussedInComments() =>
    [.. AllSources()
      .SelectMany(file => AnyId.Matches(CommentsOnly(File.ReadAllText(file))))
      .Select(match => match.Value)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(id => id, StringComparer.Ordinal)];

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

  public static IEnumerable<string> TestSources() => SourcesUnder("tests");

  // ---- ⚠ `src/` AND `tests/`, WHICH IS A WIDER POPULATION THAN CITATION COUNTING USES, DELIBERATELY.
  //
  // A trait can only live in `tests/`, so `Cited()` scanning less is right. **A criterion is DISCUSSED
  // wherever somebody wrote about it**, and production code carries as much of this project's reasoning as
  // the tests do — `DepartmentManagerCommandHandlers` argues about `BRULE-DEP-0012` in a comment and no test
  // file mentions it. *Narrowing this to `tests/` would measure where we look, not where the tree knows.*
  public static IEnumerable<string> AllSources() => SourcesUnder("src").Concat(SourcesUnder("tests"));

  private static IEnumerable<string> SourcesUnder(string area) =>
    Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), area), "*.cs", SearchOption.AllDirectories)
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

  // ---- THE EXACT COMPLEMENT OF `StripComments`, AND WRITTEN BESIDE IT FOR THAT REASON.
  //
  // ⚠⚠ **THE TWO MUST AGREE ON WHAT A COMMENT IS, OR A LINE COULD BE NEITHER CODE NOR COMMENT AND FALL OUT
  // OF BOTH POPULATIONS WITHOUT ANY SEAM BREAKING.** *Sharing the `//` rule by writing it twice is how that
  // happens*, so the rule is written once and both callers take the same answer from it.
  //
  // ⚠ AND IT KEEPS THE TEXT AFTER `//`, NOT THE WHOLE LINE: `var x = 1; // AC-LOC-0005 explains this` is a
  // code line carrying a comment, and the id in it is discussion.
  //
  // ---- ⚠⚠⚠ A LINE WITH A QUOTE BEFORE THE `//` IS DISCARDED, AND THE FIRST RUN OF THE GUARD IS WHY.
  //
  // **`CriterionInventoryTests.A_commented_trait_is_not_a_citation` builds its fixture as a string literal
  // that CONTAINS `//` and two deliberately non-existent ids.** *Neither this method nor `StripComments`
  // understands literals*, so the naive version read that code line as a comment and reported both ids as
  // "discussed" — attention nobody had paid, to criteria that do not exist.
  // ***THE GUARD'S FIRST RUN CAUGHT IT, WHICH IS THE ONLY REASON IT IS NOT STILL TRUE.***
  //
  // ⚠ **And the ids are described here rather than quoted, because quoting them would put them in a comment
  // and the guard would catch this paragraph too — as it did, on the first attempt at writing it.** *The
  // rule the guard enforces is "do not write an id that does not exist into a comment", and prose explaining
  // the rule is not exempt from it.*
  //
  // ⚠⚠ **THE FIX IS DELIBERATELY CONSERVATIVE AND THE DIRECTION OF ITS ERROR IS THE WHOLE JUSTIFICATION.**
  // Skipping any line whose `//` is preceded by a quote also drops genuine trailing comments on lines that
  // contain a string — **so this UNDER-COLLECTS and can never INVENT.** *A discussed set missing an entry
  // understates attention; one containing an entry nobody wrote claims a person looked at a criterion when
  // nobody did, and that is the reading this population exists to support.* **The floor guard is what keeps
  // the under-collection honest.**
  //
  // ⚠ `StripComments` has the SAME blindness and there the error runs the safe way — it strips a little too
  // much, so a citation can be missed and never invented. **Left alone on purpose: changing it would move a
  // published citation count as a side effect of fixing a different method.**
  public static string CommentsOnly(string source) =>
    string.Join(
      "\n",
      source.Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 && !line[..comment].Contains('"') ? line[(comment + 2)..] : string.Empty;
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

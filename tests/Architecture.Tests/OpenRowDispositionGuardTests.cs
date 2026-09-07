namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A DISPOSITION CLEARS THE MARKER CLASS IT NAMES, AND NOTHING ELSE (T-074).
// ==================================================================================================
//
// ---- THE DEFECT.
//
// `FP-011/requirements.md` carried a legend clearing its `OWNER-DECISION-REQUIRED` markers. The same file
// carried **fourteen live `| **Open** |` table rows** — a second marker class the legend never mentioned and
// therefore never cleared. Eleven named a decision the package had already ruled and recorded in its own
// `README.md`; one reasoned from an option that lost.
//
// ***NOTHING DETECTED IT.*** The corpus sweep that was supposed to could not see the rows at all: its marker
// list held `**OPEN**` and the file writes `**Open**`. **A one-character difference, and the sweep reported
// a clean package for a fortnight.**
//
// ---- ⚠⚠⚠ THE QUOTING RULE IS ASYMMETRIC, AND A SYMMETRIC ONE IS BLIND BY CONSTRUCTION.
//
// In this corpus a VIOLATION is a plain table row and a DISPOSITION is a `>`-quoted block. So violations
// must SKIP `>` lines and dispositions must READ them. **A single exclusion applied to both is blind to
// every disposition in the repository and reddens every correctly remediated file** — it would report a
// verified fix as ineffective, which is the failure that teaches an author to delete the fix.
//
// ---- ⚠⚠ "NAMED" MEANS STRUCTURALLY NAMED. PROSE GREENS NOTHING.
//
// A predicate treating *"the id appears anywhere outside an open cell"* as dispositioned greens a stale row
// because the decision is merely DISCUSSED elsewhere in the file — and `FP-011` discusses `OD-GL-0007` in
// prose two lines below the table that disposes of it, so this is not hypothetical. The structure is
// required: a row pairing an id with a verdict.
//
// ---- ⚠⚠ LITERAL IDS ONLY. A RANGE READS AS AN ENUMERATION AND IS NOT ONE.
//
// `OD-GL-0001 … OD-GL-0009` names **two** ids and implies nine. `FP-011`'s own disposition made exactly that
// mistake and corrected it by enumerating all nine. **No range expansion, ever** — and a fixture asserts it
// so nobody later "improves" this into a range parser.
//
// ---- ⚠ A ROW IS RED IF *ANY* ID ON IT IS UNNAMED. A multi-id row is where a partial disposition hides.
//
// ==================================================================================================
// ---- ⚠⚠⚠ WHAT THIS PROVES, AND THE THREE LEVELS ARE NOT THE SAME STRENGTH. READ BEFORE TRUSTING IT.
// ==================================================================================================
//
//   THE PREDICATE   proven by the fixtures below, which call `Analyse` — **the same method the corpus test
//                   calls**, never a copy of the matcher. An inline re-implementation would certify its own
//                   vacuity.
//   THE WALK FINDS   proven by the corpus test: it enumerates real files and scores them.
//   FILES
//   THE WALK FINDS   ***NOT PROVEN HERE.*** A guard that reads zero files passes this file completely.
//   ALL OF THEM      The corpus test's green cannot distinguish "no package violates" from "no package was
//                    read", which is why `Every_feature_package_was_actually_read` below asserts a floor on
//                    the population rather than only on the findings.
//
// ⚠⚠ **AND THE HONEST LIMIT ON THE `>`-QUOTED AND PROSE-PRESERVED CASES.** The reach question — *does an
// honest correction get punished?* — is answered for quoted rows by a fixture. For the prose-preserved form
// (`FP-012/requirements.md` retires its whole body with one italic sentence and no quoting) it is answered
// only by ABSENCE: that file carries no `| **Open** |` row at all, so it cannot be flagged. ***THAT IS
// GREEN-BY-ABSENCE, NOT GREEN-BY-DISCRIMINATION, and the two are not equivalent evidence.***
public sealed class OpenRowDispositionGuardTests
{
  // ---- THE CORPUS TEST. State 3 of the plant: no violation present, fix present, GREEN.
  [Fact]
  [Trait("Tripwire", "FP-011-open-rows")]
  public void No_live_open_row_cites_a_decision_its_own_package_has_ruled()
  {
    var findings = OpenRowDisposition.Analyse(FeaturePackageFiles());

    Assert.True(findings.Count == 0, Describe(findings));
  }

  // ---- THE FLOOR. Without it the test above passes over an empty walk, which is the same green.
  [Fact]
  [Trait("Tripwire", "FP-011-open-rows")]
  public void Every_feature_package_was_actually_read()
  {
    var files = FeaturePackageFiles();

    // ==================================================================================================
    // ⚠⚠⚠ TWO LAYERS, TWO COLLAPSES — AND THE FILE FLOOR ALONE CANNOT SEE THE ONE ITS COMMENT CLAIMED
    // ==================================================================================================
    //
    // THE HISTORY IS THE ARGUMENT. This floor was 100 against an actual of 173, permitting seventy-three
    // files to leave in silence. T-099 tightened it to 150 and named the event "one feature-package folder
    // going missing." ***THE ARITHMETIC WAS NEVER RUN, AND IT DOES NOT HOLD:***
    //
    //     16 packages, 173 files, 2026-09-07  ->  ~10.8 files per package
    //     one average package disappears      ->  173 - 11 = 162   A FLOOR OF 150 IS GREEN
    //     150 fires at                        ->  149             which is TWO packages, nearly three
    //
    // **So the tightened floor discriminated "two packages gone" while its comment claimed "one folder".**
    // The number moved, the event was named, and nobody checked that the number discriminates the event —
    // which is the very defect the tightening was meant to cure, one rung up.
    //
    // ⚠⚠ AND A TIGHTER FILE FLOOR IS THE WRONG INSTRUMENT, NOT AN INSUFFICIENT ONE. To catch the SMALLEST
    // package leaving you would need a floor within a file or two of the actual, which then false-reds on
    // any legitimate deletion. **A count of files cannot discriminate a folder; only a count of folders
    // can.** So the layers are separated (T-263) and each floors its own:
    //
    //     PACKAGE layer   catches a directory REMOVED or MOVED OUT of docs/17-features/
    //     FILE layer      catches a package that is present and walked but contributing nothing
    //
    // ⚠⚠⚠ AND THE PACKAGE LAYER CANNOT SEE A RENAME. STATED BECAUSE THIS COMMENT ONCE CLAIMED IT COULD.
    //
    // The walk is `EnumerateDirectories` with NO pattern, and that is deliberate — a naming convention
    // would be a vocabulary, and a vocabulary drifts. **But a folder renamed IN PLACE is still a folder,
    // so the count is unchanged and this assertion is silent.** The earlier wording here said the layer
    // caught a package "renamed, moved, or stopped matching this walk": ***it detects one of those three,
    // and "stopped matching" is meaningless for a walk that matches everything.***
    //
    // ⚠ NOTHING IN THIS FILE CATCHES A RENAME IN PLACE. The disposition tests read whatever directories
    // exist, so a renamed package is still walked and still scored — its CONTENT is checked and its
    // IDENTITY is not. That is a real hole and it is left open rather than closed with a convention.
    //
    // ---- ⚠⚠ THE PLANT, AND IT PROVED SOMETHING STRONGER THAN THE ARITHMETIC PREDICTED (T-100).
    //
    // `FP-016-platform-support-surface` was moved out of the tree, 2026-09-07. Result: **package assertion
    // RED at 15, exactly one test failing of 724.** The file layer never executed — the package assertion
    // precedes it and ordered checks hide all but the first — but the counts settle what it would have
    // done: ***FP-016 HELD ONE `.md` FILE. The walk went 173 -> 172.***
    //
    // So a file floor of 150 was not merely too loose for this event; **a file count cannot discriminate
    // package loss AT ALL.** Package sizes are wildly uneven — one file against a mean of ten — so no
    // file-count threshold separates "a package left" from "someone deleted a paragraph". The arithmetic
    // that motivated this split assumed an average package; the smallest real one is a twentieth of that.
    var packages = FeaturePackageDirectories();

    Assert.True(
      packages.Length >= 16,
      $"docs/17-features/ holds {packages.Length} package directories; SIXTEEN were measured 2026-09-07. " +
      "A package directory has been REMOVED or MOVED OUT of docs/17-features/, and every disposition " +
      "check in this file is now reading a corpus that silently excludes it.\n" +
      "  ⚠ A RENAME IN PLACE WOULD NOT HAVE FIRED THIS — the count is unchanged by one — so if you are " +
      "here after renaming something, this is telling you about a different change than the one you made.\n" +
      "  If a package was deliberately retired, lower this number and say which one. Do not lower it to " +
      "make the red go away: the file floor below cannot cover for this one, because package sizes range " +
      "from one file to twenty and no file count separates a lost package from an edited paragraph.");

    // ⚠ THE FILE FLOOR STAYS, AND ITS EVENT IS NOW THE ONE IT CAN ACTUALLY DISCRIMINATE. 150 against 173
    // catches a package that is still present but has stopped contributing files, and any larger loss.
    Assert.True(
      files.Length >= 150,
      $"The walk found {files.Length} files under docs/17-features/; 173 were measured at T-099 across " +
      $"{packages.Length} packages. The package count above is intact, so a package is present and " +
      "contributing nothing — emptied, renamed away from `*.md`, or excluded by a changed pattern.");
  }

  // ---- THE BOUNDARY ASSERTION. This replaces an allow-list that would have been vacuous.
  //
  // An earlier form of this spec exempted files carrying a CELL-SCOPED disposition — a correction written
  // inline in the corrected cell with no delimiter. **Measured, none of those files contains a single
  // `| **Open** |` row**, so the exemption would have excused files this check cannot score anyway.
  //
  // Asserted instead: the two forms never collide. True today, so it ships green honestly, and it reddens
  // EXACTLY when the intra-cell blindness could first produce a wrong verdict.
  //
  // ⚠ Its detector is the `THIS SAID` / `~~` vocabulary and that vocabulary is KNOWN-INCOMPLETE — a fourth
  // phrasing was already found once. **The failure direction is safe:** an undetected cell-disposition means
  // the file is not excluded, so the main guard scores it and the preserved original trips a red. A false
  // positive that surfaces, never a false negative that hides.
  [Fact]
  [Trait("Tripwire", "FP-011-open-rows")]
  public void No_file_carrying_an_open_row_also_carries_a_cell_scoped_disposition()
  {
    var collisions = OpenRowDisposition.CellScopedCollisions(FeaturePackageFiles());

    Assert.True(
      collisions.Count == 0,
      "A file carries both a live `| **Open** |` row and a cell-scoped disposition. This guard cannot " +
      "score a correction written inside the cell it corrects, so its verdict on this file is not " +
      $"trustworthy: {string.Join(", ", collisions)}");
  }

  // ================================================================================================
  // THE FIXTURES. Every one calls `Analyse` — the method the corpus test calls.
  // ================================================================================================

  // STATE 1, RUN FIRST: a stale row citing a ruled id, with nothing disposing of it. The hole is real.
  [Fact]
  public void A_live_open_row_citing_an_undisposed_id_is_a_finding()
  {
    var findings = OpenRowDisposition.Analyse(One("| **Open** | `OD-GL-0002` (currency) |"));

    Assert.Single(findings);
    Assert.Contains("OD-GL-0002", findings[0]);
  }

  // STATE 2: the same row, with a disposition naming the id. The fix works.
  [Fact]
  public void A_disposition_row_naming_the_id_clears_it()
  {
    var findings = OpenRowDisposition.Analyse(One(
      "> | **`OD-GL-0002`** | **Single currency V1** | **stale** |",
      "| **Open** | `OD-GL-0002` (currency) |"));

    Assert.Empty(findings);
  }

  // REQUIREMENT 1, the asymmetry: a row INSIDE a quoted block is a preserved original, not a violation.
  // This is the reach case where a GREEN is the finding — if it reddens, the guard punishes honest
  // corrections and an author will delete the preserved text to silence it.
  [Fact]
  public void An_open_row_inside_a_quoted_block_is_a_preserved_original_and_not_a_violation()
  {
    var findings = OpenRowDisposition.Analyse(One("> | **Open** | `OD-GL-0002` (currency) |"));

    Assert.Empty(findings);
  }

  // REQUIREMENT 2: prose greens nothing. The id is discussed, and discussion is not disposition.
  [Fact]
  public void An_id_merely_discussed_in_prose_does_not_clear_the_row()
  {
    var findings = OpenRowDisposition.Analyse(One(
      "> The `OD-GL-0002` ruling settled the currency question and these cells are stale.",
      "| **Open** | `OD-GL-0002` (currency) |"));

    Assert.Single(findings);
  }

  // REQUIREMENT 3: a range names its endpoints and implies the rest. It must expand to nothing.
  [Fact]
  public void A_range_in_a_disposition_names_only_its_literal_endpoints()
  {
    var findings = OpenRowDisposition.Analyse(One(
      "> | **`OD-GL-0001`** | ruled through `OD-GL-0009` | **stale** |",
      "| **Open** | `OD-GL-0005` (branch dimension) |"));

    Assert.Single(findings);
    Assert.Contains("OD-GL-0005", findings[0]);
  }

  // REQUIREMENT 4: a multi-id row is red if ANY id on it is unnamed — the shape a partial fix hides in.
  [Fact]
  public void A_multi_id_row_is_a_finding_when_only_some_ids_are_disposed()
  {
    var findings = OpenRowDisposition.Analyse(One(
      "> | **`OD-GL-0002`** | **Single currency V1** | **stale** |",
      "| **Open** | `OD-GL-0002` (currency), `OD-GL-0005` (branch dimension) |"));

    Assert.Single(findings);
    Assert.Contains("OD-GL-0005", findings[0]);
    Assert.DoesNotContain("OD-GL-0002`", findings[0]);
  }

  // A row citing no decision at all cites nothing to look up. Calling it open or closed would supply a
  // referent the document does not.
  [Fact]
  public void A_row_citing_no_decision_id_is_not_scored()
  {
    var findings = OpenRowDisposition.Analyse(One("| **Open** | — |"));

    Assert.Empty(findings);
  }

  // ---- THE FAILURE MESSAGE CARRIES BOTH READINGS, AND THE FIXTURE PINS THAT.
  //
  // Never a single instruction. A reader given one instruction for two situations will take it in the
  // situation it does not fit, and the destructive path is the one that gets taken: the house convention
  // PRESERVES originals, so a message naming only "remove the row" destroys the record.
  [Fact]
  public void The_failure_message_offers_both_remedies_and_never_says_remove()
  {
    var findings = OpenRowDisposition.Analyse(One("| **Open** | `OD-GL-0002` |"));

    Assert.Contains("name the id", findings[0], StringComparison.OrdinalIgnoreCase);
    Assert.Contains("quoted block", findings[0], StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("remove the row", findings[0], StringComparison.OrdinalIgnoreCase);
  }

  // The collision message must locate the evidence, not only name the subject. Without this the boundary
  // assertion reports "somewhere in this file" over a file with five candidate cells.
  [Fact]
  public void A_collision_message_names_the_file_and_locates_both_halves()
  {
    var collisions = OpenRowDisposition.CellScopedCollisions(One(
      "# heading",
      "| `BR-X` | ⚠ **CORRECTED — THIS CELL SAID *OPEN — `OD-XX-0001`*** and it was closed |",
      "| **Open** | `OD-XX-0002` |"));

    Assert.Single(collisions);
    Assert.Contains("FP-FIXTURE/requirements.md", collisions[0], StringComparison.Ordinal);
    Assert.Contains("open row(s) at 3", collisions[0], StringComparison.Ordinal);
    Assert.Contains("cell-scoped disposition(s) at 2", collisions[0], StringComparison.Ordinal);
  }

  // A file with an open row and NO cell-scoped disposition is not a collision — the boundary assertion must
  // not fire on every file the main guard can score, or it would be a second copy of the main guard.
  [Fact]
  public void An_open_row_without_a_cell_scoped_disposition_is_not_a_collision()
  {
    var collisions = OpenRowDisposition.CellScopedCollisions(One("| **Open** | `OD-XX-0002` |"));

    Assert.Empty(collisions);
  }

  private static IReadOnlyList<(string Path, string Text)> One(params string[] lines) =>
    [("docs/17-features/FP-FIXTURE/requirements.md", string.Join(Environment.NewLine, lines))];

  private static string Describe(IReadOnlyList<string> findings) =>
    string.Join(Environment.NewLine, findings);

  // The PACKAGE layer. Directories only, one level down — a feature package is a folder under
  // `docs/17-features/`, which is a fact about the layout rather than a judgement about naming, so this
  // needs no pattern and cannot drift with a naming convention.
  private static string[] FeaturePackageDirectories() =>
    [.. Directory
      .EnumerateDirectories(Path.Combine(RepositoryRoot(), "docs", "17-features"))
      .Select(path => new DirectoryInfo(path).Name)
      .OrderBy(name => name, StringComparer.Ordinal)];

  private static (string Path, string Text)[] FeaturePackageFiles()
  {
    var root = RepositoryRoot();

    return Directory.EnumerateFiles(Path.Combine(root, "docs", "17-features"), "*.md", SearchOption.AllDirectories)
      .Select(path => (Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
      .ToArray();
  }

  private static string RepositoryRoot()
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

// The analyser, separated from the walk so the fixtures above and the corpus test below exercise ONE
// implementation. A control that re-expresses the matcher inline cannot witness the matcher changing.
internal static class OpenRowDisposition
{
  private static readonly System.Text.RegularExpressions.Regex OpenRow =
    new(@"\|\s*\*\*Open\*\*\s*\|", System.Text.RegularExpressions.RegexOptions.Compiled);

  // Reads quoted lines on purpose: a disposition is a quoted block in this corpus.
  private static readonly System.Text.RegularExpressions.Regex DispositionRow =
    new(@"^\s*>?\s*\|\s*\*\*`(OD-[A-Z]{2,4}-\d{3,4})`\*\*\s*\|.*\|\s*\*\*(?:stale|ruled|closed)\*\*\s*\|",
      System.Text.RegularExpressions.RegexOptions.Compiled);

  // LITERAL ids only. No range expansion — `OD-GL-0001 … OD-GL-0009` yields exactly two.
  private static readonly System.Text.RegularExpressions.Regex DecisionId =
    new(@"\bOD-[A-Z]{2,4}-\d{3,4}\b", System.Text.RegularExpressions.RegexOptions.Compiled);

  private static readonly System.Text.RegularExpressions.Regex CellScoped =
    new(@"THIS (?:CELL |ROW )?SAID|~~", System.Text.RegularExpressions.RegexOptions.Compiled);

  public static IReadOnlyList<string> Analyse(IReadOnlyList<(string Path, string Text)> files)
  {
    var findings = new List<string>();

    foreach (var package in files.GroupBy(PackageOf))
    {
      var disposed = package
        .SelectMany(file => Lines(file.Text))
        .Select(line => DispositionRow.Match(line))
        .Where(match => match.Success)
        .Select(match => match.Groups[1].Value)
        .ToHashSet(StringComparer.Ordinal);

      foreach (var (path, text) in package)
      {
        var number = 0;
        foreach (var line in Lines(text))
        {
          number++;

          // VIOLATIONS SKIP QUOTED LINES. A quoted row is a preserved original.
          if (IsQuoted(line) || !OpenRow.IsMatch(line))
          {
            continue;
          }

          var unnamed = DecisionId.Matches(line)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !disposed.Contains(id))
            .ToArray();

          if (unnamed.Length > 0)
          {
            findings.Add(
              $"{path}:{number} — a live `| **Open** |` row cites {string.Join(", ", unnamed)}, which no " +
              "disposition row in this package names. Either this row is stale — name the id in a " +
              "disposition table — or it was corrected in place; move the correction to a quoted block, " +
              "or add the id.");
          }
        }
      }
    }

    return findings;
  }

  // ---- THE MESSAGE NAMES THE SUBJECT *AND* THE EVIDENCE.
  //
  // The assertion's subject is the FILE, so the file stays. But a reader told only "somewhere in this file"
  // must find the collision themselves, and the two paths open to them are unequal: ignoring the red costs
  // nothing visible, and deleting the `| **Open** |` row silences it — **which is the destructive path this
  // guard exists to steer away from.** *A partial alarm teaches a reader to trust the entries left silent.*
  //
  // ⚠ THE LINE NUMBERS COME FROM THE SAME SCAN THAT PRODUCES THE VERDICT, never a second pass. A message
  // computed by a different instrument than the finding can name cells the guard did not actually count,
  // and then the evidence and the verdict are two claims rather than one.
  public static IReadOnlyList<string> CellScopedCollisions(IReadOnlyList<(string Path, string Text)> files)
  {
    var collisions = new List<string>();

    foreach (var (path, text) in files)
    {
      var openRows = new List<int>();
      var cells = new List<int>();
      var number = 0;

      foreach (var line in Lines(text))
      {
        number++;
        if (IsQuoted(line))
        {
          continue;
        }

        if (OpenRow.IsMatch(line))
        {
          openRows.Add(number);
        }

        if (CellScoped.IsMatch(line))
        {
          cells.Add(number);
        }
      }

      if (openRows.Count > 0 && cells.Count > 0)
      {
        collisions.Add(
          $"{path} (open row(s) at {string.Join(", ", openRows)}; cell-scoped disposition(s) at " +
          $"{string.Join(", ", cells)})");
      }
    }

    return collisions;
  }

  private static bool IsQuoted(string line) => line.TrimStart().StartsWith('>');

  private static string[] Lines(string text) => text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

  private static string PackageOf((string Path, string Text) file)
  {
    var parts = file.Path.Split('/');
    var index = Array.IndexOf(parts, "17-features");

    return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : file.Path;
  }
}

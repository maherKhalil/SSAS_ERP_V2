namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE INTEGRITY CHECKS ON `CriterionInventory`. NO CRITERION IS CITED HERE AND NONE SHOULD BE.
// ==================================================================================================
//
// **These assert that the instrument still sees what it is supposed to see.** *They are about the
// instrument; no product behaviour is the subject.*
//
// ⚠⚠ AND THEY ASSERT NO COUNTS, DELIBERATELY. **A test pinning a citation total reddens on the next commit
// that adds a citation** — which is the intended work, not a regression. ***WHAT REDDENS HERE IS THE
// INSTRUMENT ROTTING: a spec folder renamed, a declaration convention changed, a fifth trait key
// introduced, the comment stripper broken.*** Those are silent failures that would quietly shrink every
// coverage number this project quotes, in the reassuring direction.
public sealed class CriterionInventoryTests
{
  // ---- EVERY FOLDER THAT *CLAIMS* CRITERIA DECLARES SOME.
  //
  // ***A PER-FOLDER ZERO IS A DEFECT HYPOTHESIS, NOT A RESULT.*** If a feature's declarations stop being
  // recognised — a new heading style, a renamed file — the honest reading is "the matcher went blind",
  // and without this it reads as "that feature declares nothing" and its uncited count silently becomes
  // its declared count.
  //
  // ⚠⚠⚠ THE POPULATION IS FOLDERS CARRYING AN `acceptance-criteria.md`, NOT ALL FOLDERS — AND THE FIRST
  // VERSION OF THIS TEST GOT THAT WRONG AND FAILED ON ITS FIRST RUN.
  //
  // `FP-016-platform-support-surface` holds a `README.md` and nothing else; there is no `AC-` id anywhere
  // beneath it. **That is a feature folder created before its specification, which is an ordinary state and
  // not a blind matcher.** *So the honest subject is a folder that claims to have criteria and yields none.*
  //
  // ⚠ AND THE UNSPECIFIED FOLDERS ARE COUNTED RATHER THAN SILENTLY SKIPPED: an absent file produces no row,
  // not a zero row, and a filter that quietly drops its exceptions is how a whole feature sits outside a
  // table with nothing broken. **The assertion below names how many were excluded and why.**
  //
  // ==================================================================================================
  // ⚠⚠⚠ CORRECTED 2026-09-05. THE PARAGRAPH ABOVE WAS RIGHT ABOUT THE HAZARD AND THE CODE BELOW IT
  // COMMITTED THE HAZARD, FOR TWO WEEKS, IN THE INSTRUMENT WRITTEN TO PREVENT IT.
  // ==================================================================================================
  //
  // **The first version excluded every folder without an `acceptance-criteria.md` and justified it by
  // describing ONE of the two members of that exclusion.** *`FP-016` really does hold a README and nothing
  // else.* ***`FP-010-hr-employee-documents` HOLDS THREE FILES AND DECLARES FOUR CRITERIA*** — `AC-DOC-0017`
  // through `AC-DOC-0020`, as a first-shape table in `carried-analysis.md`, which the matcher already reads.
  //
  // ***MEASURED: THE GATED WALK RETURNS 602 AND THE UNGATED WALK RETURNS 606, AND THE DIFFERENCE IS EXACTLY
  // THOSE FOUR IDS.*** They are not placeholders — content-type verified against the bytes, metadata
  // visibility not granting content, document reads inheriting employee scope, one-way withdrawal. **Four
  // substantive criteria outside every coverage number this project has published.**
  //
  // ⚠⚠ THE MECHANISM, BECAUSE IT IS THE REUSABLE PART: **AN EXCLUSION RULE WAS DERIVED FROM ONE MEMBER OF A
  // TWO-MEMBER SET AND ITS JUSTIFICATION CHECKED AGAINST THAT MEMBER ONLY.** *`HasCriteriaFile` names a FILE;
  // declaring is a MECHANISM* — and a name-shaped filter over a mechanism is the same defect this file's own
  // header warns about for declaration shapes, one layer up.
  //
  // ---- ⚠ WHY THIS RECORDS FP-010 RATHER THAN COUNTING IT, WHICH IS NOT THE COWARD'S OPTION.
  //
  // **Folding four ids into the total would silently move a published number**, and whether `FP-010`'s
  // criteria are in force is an owner question — the folder carries a `decisions-open.md`, so it is PARKED
  // rather than unstarted, which is the one state the old justification did not cover. ***SO THE EXCEPTION IS
  // NAMED IN THE ASSERTION, WHERE IT REDDENS IF A SECOND FOLDER JOINS IT AND ALSO IF FP-010 IS FIXED*** — the
  // `KnownUnresolvable` shape from `CommentCitationGuardTests`, and its rule applies here too: **a recorded
  // exception that outlives its reason turns a record into a place where anything can hide.**
  [Fact]
  public void Every_folder_that_declares_criteria_is_visible_to_the_count_or_named_here()
  {
    var folders = CriterionInventory.FeatureFolders();

    // ANTI-VACUITY: a walk that found no folders would satisfy every set operation below by iterating
    // nothing, and would report perfect health while measuring an empty estate.
    Assert.NotEmpty(folders);

    var withFile = folders.Where(CriterionInventory.HasCriteriaFile).ToArray();
    var declaring = folders.Where(folder => CriterionInventory.DeclaredIn(folder).Count > 0).ToArray();

    // The excluded set is BOUND, not merely excluded.
    Assert.True(
      folders.Count - withFile.Length < 50,
      $"{folders.Count - withFile.Length} of {folders.Count} feature folders carry no " +
      "acceptance-criteria.md — the exclusion is no longer an exception.");

    var silent = withFile.Where(folder => CriterionInventory.DeclaredIn(folder).Count == 0).ToArray();

    Assert.True(
      silent.Length == 0,
      $"{silent.Length} feature folder(s) have an acceptance-criteria.md that declares nothing: " +
      $"{string.Join(", ", silent)}. A declaration shape has changed and the matcher has gone blind — " +
      "which is indistinguishable from 'declares nothing' in every count derived from it.");

    // ---- THE ROW THE FIRST VERSION HAD NO PLACE FOR: DECLARES, BUT NOT WHERE THE COUNT LOOKS.
    var invisible = declaring.Except(withFile, StringComparer.Ordinal).ToArray();

    Assert.True(
      invisible.SequenceEqual(KnownDeclaringWithoutTheFile, StringComparer.Ordinal),
      $"folders declaring criteria outside an acceptance-criteria.md changed. Expected " +
      $"[{string.Join(", ", KnownDeclaringWithoutTheFile)}], found [{string.Join(", ", invisible)}]. " +
      "Every id declared in such a folder is invisible to the per-feature counts, so either the folder " +
      "gains an acceptance-criteria.md and its criteria join the total, or it is recorded here with a " +
      "reason — and an entry that no longer applies must be removed in the same commit that fixes it.");
  }

  // FP-010 declares AC-DOC-0017..0020 in `carried-analysis.md` and has no `acceptance-criteria.md`. It also
  // carries a `decisions-open.md`: the feature is PARKED, not unstarted, and whether those four criteria are
  // in force is the owner's call rather than this instrument's.
  private static readonly string[] KnownDeclaringWithoutTheFile = ["FP-010-hr-employee-documents"];

  // ---- ALL FOUR CITED KEYS ARE LIVE, AND THE TRIPWIRE KEY IS SEPARATE.
  //
  // The matcher reads four keys because four are in use. ⚠ **If one fell out of use the matcher would keep
  // working and nobody would learn the convention had narrowed** — so this pins that the tree still uses
  // what the definition claims it reads. *It is the population check for the definition itself.*
  [Fact]
  public void The_trait_vocabulary_the_matcher_reads_is_the_vocabulary_the_tree_uses()
  {
    var sources = CriterionInventory.TestSources().Select(File.ReadAllText).ToArray();
    Assert.NotEmpty(sources);

    foreach (var key in new[] { "Criterion", "Acceptance", "Decision", "AcceptanceCriteria", "Tripwire" })
    {
      Assert.True(
        sources.Any(text => text.Contains($"Trait(\"{key}\"", StringComparison.Ordinal)),
        $"no test in the tree uses the '{key}' trait key any more. The matcher still reads it, so every " +
        "count derived from it is now describing a convention that has moved.");
    }
  }

  // ---- A CITATION AND A TRIPWIRE ARE OPPOSITE CLAIMS ABOUT THE SAME ID.
  //
  // A citation says the product does this and here is the witness. ***A TRIPWIRE SAYS THE SUBJECT DOES NOT
  // EXIST AND HERE IS THE ALARM FOR WHEN IT DOES.*** An id carrying both is a contradiction — and it is
  // exactly the mistake that gets made by tagging a disposition guard `Criterion` out of habit, which
  // would silently mark an unbuilt criterion as covered.
  [Fact]
  public void No_criterion_is_both_cited_and_tripwired()
  {
    var cited = CriterionInventory.Cited();
    var tripwired = CriterionInventory.Tripwired();

    // ANTI-VACUITY, BOTH SIDES. Two empty sets are trivially disjoint, and a broken matcher produces two
    // empty sets — so the intersection check alone would pass hardest exactly when it means least.
    Assert.NotEmpty(cited);
    Assert.NotEmpty(tripwired);

    var both = cited.Intersect(tripwired, StringComparer.Ordinal).ToArray();

    Assert.True(
      both.Length == 0,
      $"{string.Join(", ", both)} carry BOTH a citation and a tripwire trait. Those are opposite claims: " +
      "one says a witness exists, the other says the subject does not.");
  }

  // ---- THE COMMENT STRIPPER ACTUALLY STRIPS.
  //
  // ⚠⚠⚠ **THIS IS THE CONTROL THAT MATTERS MOST AND IT IS THE CHEAPEST.** A stripper that silently stopped
  // working would make every commented-out or merely DISCUSSED trait count as a citation — and this tree
  // discusses traits in prose constantly, including in this very file. *Coverage numbers would rise, which
  // is the direction nobody questions.*
  [Fact]
  public void A_commented_trait_is_not_a_citation()
  {
    const string source = "  // [Trait(\"Criterion\", \"AC-XXX-9999\")]\n  [Trait(\"Criterion\", \"AC-YYY-8888\")]";

    var stripped = CriterionInventory.StripComments(source);

    Assert.DoesNotContain("AC-XXX-9999", stripped, StringComparison.Ordinal);
    Assert.Contains("AC-YYY-8888", stripped, StringComparison.Ordinal);
  }

  // ---- THE SITE SCAN AGREES WITH THE CENSUS ON THE POPULATION, WHICH IS THE ONLY THING IT MAY DISAGREE
  // WITH IT ABOUT.
  //
  // `CitationSites()` and `Cited()` share ONE definition of a citation and differ only in what they report,
  // so this is **two implementations of one definition rather than two instruments** — it tests the walk,
  // not the population. *That is still the check worth having:* the site scan reads raw lines to count
  // comments and must strip them to MATCH, and getting that backwards is the exact failure it was written
  // to measure. **A site scan that admitted commented-out traits would report ids the census never counted**
  // — and it did, on the first run, by exactly one.
  //
  // ⚠ No count is asserted. The set EQUALITY moves only when the tree's citations move, which is the same
  // condition under which `Cited()` moves, so this cannot redden on its own for ordinary work.
  [Fact]
  public void Every_citation_site_belongs_to_the_cited_population_and_none_is_missing()
  {
    var sites = CriterionInventory.CitationSites();
    var cited = CriterionInventory.Cited();

    // ANTI-VACUITY BEFORE THE COMPARISON: two empty sets are equal, and an equality is the assertion most
    // easily satisfied by a walk that found nothing at all.
    Assert.NotEmpty(sites);
    Assert.NotEmpty(cited);

    Assert.Equal(
      cited.OrderBy(id => id, StringComparer.Ordinal),
      sites.Select(site => site.Id).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal));
  }

  // ---- BOTH SCOPES ARE STILL REACHABLE, AND BOTH DIRECTIONS OF THE ADJACENCY WALK STILL FIND SOMETHING.
  //
  // Three separate ways this degrades silently, each turning a real finding into a flat zero: the type
  // check stops matching and every citation reads as method-scoped; the upward walk breaks and the tree
  // looks undocumented; the downward walk breaks and **the convention that puts reasoning BETWEEN the trait
  // and the signature disappears** — which is the direction whose absence produced four false verdicts
  // before this method existed.
  //
  // ⚠ A floor rather than a count, and low: the claim is *the walk still works*, not *the tree is still
  // this documented*.
  [Fact]
  public void The_adjacency_walk_still_sees_both_scopes_and_both_directions()
  {
    var sites = CriterionInventory.CitationSites();

    Assert.Contains(sites, site => site.IsTypeScoped);
    Assert.Contains(sites, site => !site.IsTypeScoped);
    Assert.True(
      sites.Count(site => site.AdjacentCommentLines > 0) > 100,
      $"only {sites.Count(site => site.AdjacentCommentLines > 0)} of {sites.Count} citation sites have any " +
      "adjacent comment; 707 of 814 did at c4973ed. The adjacency walk has probably stopped walking rather " +
      "than the tree stopped explaining itself.");
  }
}

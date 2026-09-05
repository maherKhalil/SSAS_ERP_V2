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
  [Fact]
  public void Every_feature_folder_with_a_criteria_file_declares_at_least_one_criterion()
  {
    var folders = CriterionInventory.FeatureFolders();

    // ANTI-VACUITY: a walk that found no folders would satisfy the loop below by iterating nothing.
    Assert.NotEmpty(folders);

    var specified = folders.Where(CriterionInventory.HasCriteriaFile).ToArray();
    var unspecified = folders.Except(specified, StringComparer.Ordinal).ToArray();

    // The excluded set is BOUND, not merely excluded. If this ever covers most of the tree, the exclusion
    // has stopped being an exception and the instrument is measuring a fraction of the estate.
    Assert.True(
      unspecified.Length < specified.Length,
      $"{unspecified.Length} of {folders.Count} feature folders carry no acceptance-criteria.md " +
      $"({string.Join(", ", unspecified)}) — the exclusion is no longer an exception.");

    var silent = specified.Where(folder => CriterionInventory.DeclaredIn(folder).Count == 0).ToArray();

    Assert.True(
      silent.Length == 0,
      $"{silent.Length} feature folder(s) have an acceptance-criteria.md that declares nothing: " +
      $"{string.Join(", ", silent)}. A declaration shape has changed and the matcher has gone blind — " +
      "which is indistinguishable from 'declares nothing' in every count derived from it.");
  }

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
}

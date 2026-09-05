namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A COMMENT THAT NAMES A CRITERION MUST NAME ONE THAT EXISTS — AND THE SET OF CRITERIA ANYONE HAS
// WRITTEN ABOUT IS ITSELF A POPULATION WORTH HAVING.
// ==================================================================================================
//
// ---- WHY THIS EXISTS, AND IT IS A PROPAGATION STORY RATHER THAN A DEFECT ONE.
//
// `CommentCitationGuardTests` resolves backticked identifiers in comments against real code and **refuses
// criterion ids for one stated reason**, written into that file:
//
//     // Rule ids (`BR-GL-0002`, `DEC-L-084`, `AC-TEN-0020`) and prose references. **Resolving those needs a
//     // registry that does not exist, and inferring one would put a guess inside a guard**
//
// ***THAT REGISTRY EXISTS NOW.*** `CriterionInventory` was moved out of a session scratchpad and into the
// gate, and `DeclaredEverywhere()` is exactly the thing that file said it lacked. **A file named precisely
// what it needed, the thing was built, and nothing connected the two** — so this closes the join rather than
// finding a new gap, and the join is the interesting part.
//
// ---- ⚠⚠⚠ WHAT THIS DOES NOT CATCH, STATED FIRST BECAUSE A PARTIAL ALARM IS WORSE THAN NO ALARM.
//
// ***IT RESOLVES EXISTENCE, NOT APTNESS.*** A comment naming an id no feature declares fails here. **A
// comment naming a real criterion that says something else entirely PASSES** — and that is the commoner
// error and much the worse one, because a wrong-but-real id reads as authority to every subsequent reader
// while a dead one eventually trips over something.
//
// **So a green here means the ids resolve. It means nothing whatever about whether the sentences beside them
// are true of the criteria they name.** *Nothing mechanical can check that; it is read by a person, one
// criterion at a time, and the reading is what the second population below records the trace of.*
//
// ---- ⚠⚠ AND IT IS STRUCTURALLY IMMUNE TO THE TRAP THAT CAUGHT ITS PREDECESSOR, WHICH IS WHY IT RESOLVES
// AGAINST `docs/` RATHER THAN AGAINST CODE.
//
// That file's first version resolved citations against a blob that INCLUDED the comments, so a comment
// citing a dead identifier found it in its own line: **"an instrument whose search space contains the thing
// being checked is not an instrument, it is a mirror."** *Here the search space is the specification tree and
// the subject is C# comments — two disjoint corpora, so the loop cannot close.* **This file also needs no
// self-exclusion, because every id it names is a real one; naming a fake id in prose would make it fail
// against itself, which is the same mirror wearing the other face.**
//
// ⚠⚠⚠ **AND THAT LAST SENTENCE WAS FALSE WHEN IT WAS FIRST WRITTEN, WHICH IS EXACTLY WHY IT IS KEPT.** The
// first version of this header illustrated the existence check by naming a FABRICATED id, three paragraphs
// above the claim that it named none. ***THE GUARD'S FIRST RUN FAILED ON IT.*** *A file describing a trap,
// claiming immunity from it, and falling into it in the same breath* — and the only thing that separated the
// description from the fact was running the thing.
//
// It failed on two OTHER ids in the same run, and those were an instrument defect rather than a prose one:
// `CommentsOnly` read a `//` inside a STRING LITERAL as a comment and invented two discussions nobody had
// written. **Recorded on `CommentsOnly`, where the fix lives.** ⚠ *A first run that fails for two unrelated
// reasons is the cheapest evidence a guard ever gets that it is not vacuous* — and neither cause would have
// been found by reading this file, because one of them is this file.
//
// ==================================================================================================
// ---- ⚠⚠⚠ AND BECAUSE APTNESS IS READ BY A PERSON: FOUR ARRANGEMENTS THAT WORK, WITH WORKED EXAMPLES.
// THE COUNTERPART TO A FAILURE CATALOGUE, WHICH ON ITS OWN TELLS AN AUTHOR WHAT NOT TO DO AND NOTHING
// ABOUT WHAT TO DO.
// ==================================================================================================
//
// This tree has logged its citation failure modes at length — wrong subject, a description read as the
// thing it describes, a matcher that finds nothing and passes. **What it had never written down is the
// positive form**, so an author reaching for a citation had a list of traps and no pattern.
//
// *All three answer one question, which is the question the paragraph above says nobody can mechanise:*
// ***HOW DO I KNOW THIS GREEN WAS NOT FREE?*** They differ in what the surface under test gives you to
// work with, so the choice between them is made by the API, not by taste.
//
// ⚠ **Pointed at by PATH AND METHOD NAME, deliberately without line numbers** — a line number rots on the
// next edit made above it, and a rotted pointer in a catalogue of good practice is its own failure mode.
//
// ---- 1. PAIRED INCLUSION AND EXCLUSION AGAINST **ONE** RESULT SET.
//
// Assert that the thing that should be there IS and the thing that should not be ISN'T, **against the same
// returned collection**. The pair is what makes it discriminating: a query returning everything fails the
// exclusion, a query returning nothing fails the inclusion, and **a query that was never executed fails
// both**. One assertion alone survives all three.
//
//   `tests/Integration.Tests/EmployeeBoundarySqlServerTests.cs`
//     `A_search_without_a_status_filter_excludes_terminated_employees` (`AC-EMP-0016`) — seeds one active
//     and one terminated employee, then `Assert.Contains` / `Assert.DoesNotContain` over **the same
//     `page.Value.Items`**. The terminated one had to be created successfully first, so the exclusion
//     cannot be satisfied by the row's absence.
//
//   `tests/API.Tests/Positions/PositionEndpointTests.cs`
//     `A_stale_rowversion_is_refused_at_the_handler_pre_check_on_every_family` (`AC-POS-0047`) — the same
//     shape scaled to a whole request set, and **the strongest citation two hand-read samples produced**.
//     It walks `UpdateRequests(seeded, StaleRowVersion)` collecting anything that did not answer 409, then
//     walks the identical set with `CurrentRowVersion` collecting anything that DID — into one `offenders`
//     list closed by `Assert.Empty(offenders)`. ***Its second message names the confound out loud:*** *"the
//     CURRENT version also conflicted — the refusal is not about staleness"*. That is the discriminating-
//     test rule implemented by an author who had never read it.
//
// ---- 2. THE FIRST SUCCESS AS A CAUSAL CONTROL.
//
// When the assertion is that something is REFUSED, **perform the same operation successfully first**. The
// refusal then has a cause, because the only thing that changed between the two calls is the one variable.
// Without it, a refusal is equally consistent with a fixture that could never have succeeded at all — and
// that failure is invisible, because a test asserting a refusal is green either way.
//
//   `tests/Integration.Tests/EmployeeBoundarySqlServerTests.cs`
//     `Revoking_company_access_mid_session_refuses_the_next_employee_write` (`AC-EMP-0026`) — creates
//     `EMP-R5` and asserts success, revokes the assignment, creates `EMP-R6` and asserts failure. The first
//     create is not scenery; it is the proof that the graph could write before the revoke.
//
//   `tests/Platform.Tests/Subscriptions/TrialSubscriptionIssuanceTests.cs`
//     `Another_tenant_is_still_issued_one` (`AC-SUB-0054`) — the control as **its own test**, which is worth
//     noting as a variant: two sibling methods assert the issuer leaves an already-subscribed tenant alone,
//     and this one asserts `Assert.Equal(2, subscriptions.Added.Count)` for two fresh tenants. ***Without it
//     an issuer that had silently stopped issuing anything at all would turn both siblings green.***
//
// ---- 3. EXCLUSION ENCODED IN CARDINALITY, FOR WHEN THE API GIVES YOU NOWHERE TO NAME THE EXCLUDED THING.
//
// Sometimes the call takes no parameter that could name what must be left out. **Then the count is the
// exclusion**: seed the confound, and assert the exact number rather than non-emptiness.
//
//   `tests/Integration.Tests/PayrollSchemaSqlServerTests.cs`
//     `A_scope_authorized_for_one_company_reads_none_of_the_others_rows` (`AC-PAY-0005`) — seeds **the same
//     employee guid in both companies**, then `Assert.Single(await reads.GetCompensationHistoryAsync(
//     scope.Value, employee))`. That read takes a scope and an employee and no company, so there is no
//     argument with which to say *"and not company B"*; `Single` says it, and `NotEmpty` would not have.
//
//   `tests/Platform.Tests/IdentityAccess/IdentityAccessApplicationTests.cs`
//     `Permission_command_rejects_unknown_names_and_stale_versions` (`AC-IAM-0013`) — three attempts, one
//     valid, and `Assert.Equal(1, unitOfWork.SaveCount)`. **The error codes say the two bad attempts were
//     refused; the save count says they WROTE NOTHING**, which is the part a returned `Result` cannot
//     witness about itself.
//
// ---- 4. PROVE A CLASS IS CLOSED BY PLANTING A **NON**-MEMBER — AND THE RULE THAT TELLS YOU WHEN.
//
// ***AN ENUMERATION IN A CRITERION IS N SEPARATE CASES. THAT HOLDS FOR A DENY-LIST AND INVERTS FOR AN
// ALLOW-LIST.*** With a deny-list the implementation must name each banned thing, so every arm needs its
// own fixture and a missing arm is a real hole. **With an allow-list, ONE input from outside the permitted
// set exercises every arm at once — and planting one of the criterion's own named items tests LESS**,
// because it cannot distinguish *"this field is refused"* from *"everything outside the set is refused"*.
//
//   `tests/API.Tests/Infrastructure/PlatformSupportAuthenticationEndpointTests.cs`
//     `Login_rejects_unknown_input_fields_without_echoing_the_body` (`AC-TEN-0079`) — the criterion names
//     seven caller-supplied fields that must not confer authority. **The fixture plants `clientId`, which is
//     none of them.** `ReadLoginAsync` is an allow-list of exactly `loginEmail` and `password` (plus a
//     duplicate-property check), so the seven are seven members of a class the allow-list already
//     forecloses, and an eighth, arbitrary member proves the class is closed in a way any of the seven
//     could not.
//
// ⚠ **THE TRIGGER: before writing N fixtures for an N-item enumeration, read the implementation and ask
// whether it is an allow-list or a deny-list.** The criterion's grammar will not tell you — an enumeration
// reads identically either way, and it invites the reader to expect enumeration in the test.
//
// ⚠⚠⚠ **RECORDED HERE BECAUSE A REVIEWER READ IT AS THE OPPOSITE.** This citation was assessed from
// outside the file as *"the operand is not in the sample — the witness exercises none of the seven"*, which
// is the argument exactly inverted, and the proposed repair would have swapped the strong probe for a weak
// one. ***AN ARRANGEMENT THAT LOOKS LIKE UNDER-TESTING TO EVERY READER WHO HAS NOT SEEN THE IMPLEMENTATION
// NEEDS ITS REASONING WRITTEN BESIDE IT*** — that file's does, at length, which is the only reason the
// misreading was caught.
//
// ⚠⚠ **AND THE RESIDUAL IS REAL, SO RELYING ON A CLOSED CLASS MAKES THE CRITERION'S WORDING LOAD-BEARING.**
// `AC-TEN-0079` says *reject **or ignore***. The route only ever rejects, and rejects far more than the
// seven. **A maintainer could replace the two-name allow-list with a seven-name deny-list, satisfy every
// word of the criterion, and reopen the class** — the test would follow the implementation down, because
// its whole strength was the allow-list it never names. *When you take this arrangement, say in the comment
// which property of the implementation you are leaning on.*
//
// ---- ⚠⚠ WHAT THESE FOUR DO NOT ADDRESS, SAID HERE BECAUSE A CATALOGUE THAT OVERSELLS ITSELF IS THE
// PARTIAL ALARM THIS FILE OPENS BY WARNING ABOUT.
//
// **Every arrangement above defends against VACUITY — a green obtained for free. None of them defends
// against COVERING ONE CLAUSE OF THE CRITERION AND BEING SILENT ON THE REST**, and across twenty
// hand-read citations that was the *modal* outcome: markedly commoner than a wrong subject. `AC-TEN-0015`'s
// witness binds its population properly and cannot reach the word *raises* at all; `AC-AUTH-0035` is carried
// by a CLASS-level trait over six methods that between them touch three of its seven named destinations.
//
// ***A citation is recorded at CRITERION granularity by a single trait, while the gap is at CLAUSE
// granularity, and nothing in this tree computes the per-clause number.*** So the honest use of this
// catalogue is: it tells you how to make an assertion mean something, and it leaves entirely open whether
// your assertion means everything the criterion says. **For that, the only known instrument is to take the
// criterion one clause at a time and name the fixture in your method that would fail if that clause were
// false** — and if you cannot name one, the clause is uncited however green the test is.
//
// ---- ⚠⚠⚠ AND THE ONE THING THAT MAKES THIS TRACTABLE: **THE TREE IS ALREADY DOING IT. WRITE IT DOWN
// WHERE THE NEXT READER WILL FIND IT, BECAUSE THE TRAIT CANNOT CARRY IT.**
//
// ⚠ **STATED WITH ITS DENOMINATOR, WHICH IS SMALL: FOUR clause gaps, from ONE hand-read sample of ten
// gated citations. THREE of the four were already diagnosed in the comment beside the trait** — scoped to
// the exact clause, naming the OTHER witness by path (`AC-ATT-0015` → the Integration schema tests for
// *throws*; `AC-PAY-0024` → `PayrollChainSqlServerTests` for the second-run clause; `AC-TEN-0015` →
// *"NOT THE FIRST CLAUSE … a package that defined all seven events and raised none would pass this test
// completely"*). **Three of four is a direction, not a rate**, and the population that would give it one is
// *how many of all cited methods carry adjacent reasoning at all* — a single structural bit, unmeasured.
// ***BUT THE EXISTENCE CLAIM NEEDS NO DENOMINATOR: THE PER-CLAUSE INFORMATION IS IN THIS TREE TODAY, AND
// THE CENSUS THROWS IT AWAY AND KEEPS THE ID.***
//
// ⚠⚠ **The exception is the tell.** The one gap with no reasoning written anywhere was `AC-AUTH-0035` — and
// it is the CLASS-level trait, *which has no method to attach the reasoning to.* **The citation shape that
// records the least about coverage is also the shape that leaves the author nowhere to explain it.** Prefer
// a method-level trait even when every method in the file bears on the criterion.
//
// **So: when your witness reaches some clauses and not others — which is ordinary and often unavoidable —
// say which, and name what covers the rest.** *A reader can act on that. A bare id tells them the criterion
// is handled, which is the one thing it does not know.*
//
// ---- ⚠⚠⚠ THE MECHANISM, MEASURED IN FIVE PLACES: ***THE TRAIT IS A ONE-TO-ONE INSTRUMENT OVER A
// MANY-TO-MANY RELATION, SO IT ROUNDS — UP AT CLASS SCOPE AND DOWN AT METHOD SCOPE.***
//
// A criterion has several clauses; a clause may be witnessed by several methods; a method may witness
// clauses of several criteria. **The trait can say one thing: *this method, this criterion*.** Everything
// else is rounded away, and the census — reading one trait per criterion — cannot see which direction.
//
//   ***UP.*** ***The class-level cluster*** — five traits, each claiming its criterion of **every** method in
//         its file (six to fourteen), *including methods the author's own prose disclaims.* Now moved,
//         except `AC-AUTH-0031`, which is left class-scoped **because no single method witnesses it** and
//         narrowing it would round the other way.
//
//   ***DOWN.*** `AC-TEN-0093` — seven clauses, witnessed by **four methods in one file**, ***one tagged.***
//         The three untagged siblings carry *"does not re-enable a Disabled principal"* and *"fail-closed
//         when no eligible subject exists"*.
//   ***DOWN.*** `AC-ATT-0040` — three clauses, cited at the one its method names; the other two elsewhere.
//   ***DOWN.*** `AC-AUTH-0046` — its witness is named **by hand, in prose, from a different file**
//         (`AuthenticationSessionDomainTests` names `Access_token_issuance_failure_rolls_back_…`).
//         ***A cross-file citation a person maintains and no instrument here can read.***
//   ***DOWN, AND THE CLEAREST OF THE FIVE.*** `AC-AUTH-0034` names **eight** race classes. **Three are
//         covered** — selection, refresh, revocation — across four methods in two files, ***one tagged.***
//         **Five are not covered at all**: session-limit, password-reset, membership, Tenant and
//         `SecurityVersion` each have a `WITH (UPDLOCK, HOLDLOCK)` site in `src/` and no concurrent test.
//
// ⚠⚠⚠ **AND `AC-AUTH-0034` IS WHY THE TWO WORDS MUST STAY APART.** ***IT IS CITED SOUNDLY — the cited
// method really does prove the selection race, and pins a DOMAIN error code, which is what carries
// "without leaking SQL details" — AND IT IS NOT COVERED.*** **COVERED is a property of the criterion:
// every clause has a fixture somewhere. CITED is a claim about a method: this method proves this rule.**
// *A census of traits measures neither one cleanly, and calling both "sound" hides exactly this cell.*
//
// ⚠ **The down-rounding is NOT repairable by tagging the siblings.** A method witnessing one clause, tagged
// with the criterion id, reads as a whole-criterion witness — **which is the up-rounding recreated at method
// scope.** *Only a clause-level citation could say it, and the trait has no clause field.* **That is the
// convention decision this file cannot make for itself, and these five instances are what it should be
// decided on.**
//
// ---- ⚠⚠⚠ AND A POLARITY WARNING FOR WHOEVER MECHANISES ANY OF THIS, BECAUSE TWO INSTRUMENTS WOULD RUN
// OVER THE SAME CORPUS UNDER OPPOSITE RULES AND EACH IS THE OTHER'S FAILURE MODE.
//
//   ***THE CENSUS*** (`CriterionInventory.Cited`) **STRIPS comments and counts TRAITS.** Run it over raw
//              source and it measures the DOCUMENTATION of this prose-dense tree instead of its coverage —
//              a commented-out trait would count, and the first version of that matcher reported eight
//              criteria as covered on the strength of prose alone.
//   ***A HARVEST*** of the reasoning above would do the exact inverse: **READ ONLY comments, and count
//              REASONING.** Run it with the census's stripper and it returns zero, which reads identically
//              to *"nobody explains anything"*.
//
// **They are not variants of one scan; they are opposite polarities over one corpus, and each one run under
// the other's rule produces a confident, plausible, wrong number.** ⚠ *Note also what a harvest could and
// could not be: extracting per-clause reasoning from free prose has no mechanical operand. Asking whether a
// citation has ANY adjacent recorded reasoning is one structural bit, and that bit is the denominator the
// paragraph above is missing — as well as the census's first anti-vacuity control, since a citation with no
// reasoning beside it is one nobody has audited.*
public sealed class CriterionCommentGuardTests
{
  // ---- THE GUARD.
  //
  // ⚠ RESOLVED AGAINST THE UNGATED POPULATION — every folder, not only those with an `acceptance-criteria.md`.
  // **The question here is "does this id exist", and `AC-DOC-0017` exists whether or not the coverage
  // numbers can see it.** *Resolving against the gated 602 would report four real criteria as typos, which is
  // the exclusion defect corrected in `CriterionInventoryTests` arriving a second time by a different route.*
  [Fact]
  public void Every_criterion_named_in_a_comment_is_one_that_exists()
  {
    var declared = CriterionInventory.DeclaredEverywhere().ToHashSet(StringComparer.Ordinal);

    // ANTI-VACUITY, BOTH SIDES. An empty declared set makes every id unresolvable and this test loud for the
    // wrong reason; an empty discussed set makes it silent and green. **The second is the dangerous one.**
    Assert.NotEmpty(declared);
    Assert.NotEmpty(CriterionInventory.DiscussedInComments());

    var unresolved = CriterionInventory.DiscussedInComments()
      .Where(id => !declared.Contains(id))
      .ToArray();

    Assert.True(
      unresolved.Length == 0,
      $"{unresolved.Length} criterion id(s) are named in comments but declared nowhere in docs/17-features: " +
      $"{string.Join(", ", unresolved)}. A comment naming a criterion that does not exist reads as a " +
      "citation to everyone who meets it, and this tree keeps its reasoning in comments.");
  }

  // ---- THE FLOOR, AND IT BUYS INSTRUMENT HEALTH RATHER THAN COVERAGE.
  //
  // The failure it guards is the extractor going quiet — a comment-scan regression, a stripper inverted, a
  // path filter narrowed — which turns the assertion above green everywhere at once. **A guard whose subject
  // vanishes passes hardest exactly when it means least.**
  //
  // ⚠ TIGHT ON PURPOSE, FOR THE REASON ITS SIBLING WRITES OUT AT LENGTH: **extractors rarely stop dead, they
  // DEGRADE.** A loose floor passes for exactly the partial degradation that is the likely case. Measured
  // **472 distinct ids in comments across `src/` and `tests/` at `c945332`**; the floor is 460.
  //
  // **No exact count and no ceiling**: a criterion newly discussed is ordinary work, and a number bumped
  // reflexively trains its readers to update without reading.
  [Fact]
  public void The_comment_scan_has_not_gone_quiet()
  {
    var discussed = CriterionInventory.DiscussedInComments();

    Assert.True(
      discussed.Count >= 460,
      $"only {discussed.Count} criteria are named in comments; 472 were measured at c945332. " +
      "The comment scan has probably stopped matching rather than the tree stopped explaining itself.");
  }

  // ---- ⚠⚠⚠ THE CONTROL THAT KEEPS THE TWO AXES APART, AND IT IS THE ONE THAT WOULD BE MISSED.
  //
  // `A_commented_trait_is_not_a_citation` asserts that a commented-out trait does NOT count as coverage.
  // **This asserts the same line from the other side: it DOES count as discussion.** *Somebody who wrote a
  // trait and then commented it out has unmistakably considered that criterion.*
  //
  // ***THE TWO TOGETHER ARE WHAT STOPS THE AXES MERGING.*** Discussed answers **"did a person look?"**; cited
  // answers **"does a test witness it?"** — and a commented-out trait is the one input that lands on
  // opposite sides of the two. **If `CommentsOnly` and `StripComments` ever disagreed about what a comment
  // is, this input would fall out of both populations and no other assertion in the tree would notice.**
  [Fact]
  public void A_commented_out_trait_is_discussion_and_is_never_a_citation()
  {
    const string source = "  // [Trait(\"Criterion\", \"AC-EMP-0047\")]\n  [Trait(\"Criterion\", \"AC-EMP-0002\")]";

    var code = CriterionInventory.StripComments(source);
    var comments = CriterionInventory.CommentsOnly(source);

    // The commented-out trait is invisible to the citation side and visible to the discussion side.
    Assert.DoesNotContain("AC-EMP-0047", code, StringComparison.Ordinal);
    Assert.Contains("AC-EMP-0047", comments, StringComparison.Ordinal);

    // And the live trait is the exact mirror, which is what makes the pair discriminating rather than
    // merely true: a stripper that returned everything, or nothing, fails one of these four.
    Assert.Contains("AC-EMP-0002", code, StringComparison.Ordinal);
    Assert.DoesNotContain("AC-EMP-0002", comments, StringComparison.Ordinal);
  }

  // ---- DISCUSSED AND CITED ARE DIFFERENT QUESTIONS AND MUST NOT CONVERGE.
  //
  // ⚠ **Not disjoint — a well-cited criterion is usually discussed at length beside its trait, and that is
  // the healthy case.** What would be wrong is CONTAINMENT in either direction:
  //
  //   *discussed ⊇ cited*  would mean nobody ever tags without explaining — plausible, and not something to
  //                        pin, since it makes the discussed set a superset that adds no information.
  //   ***discussed ⊆ cited***  would mean **NO CRITERION IS EVER WRITTEN ABOUT WITHOUT BEING CLAIMED**, and
  //                        the discussed population would then be measuring nothing this tree does not
  //                        already count.
  //
  // **The second is what this pins.** Measured at `c945332`: **36 criteria are declared, discussed in a
  // comment, and carried by no trait at all** — read, considered, and deliberately or accidentally left
  // untagged. *That set is the whole reason `DiscussedInComments()` exists, and if it ever empties, the
  // method has stopped answering a question the citation count cannot.*
  [Fact]
  public void Some_criteria_are_written_about_without_being_claimed()
  {
    var cited = CriterionInventory.Cited().ToHashSet(StringComparer.Ordinal);
    var tripwired = CriterionInventory.Tripwired().ToHashSet(StringComparer.Ordinal);
    var declared = CriterionInventory.DeclaredEverywhere().ToHashSet(StringComparer.Ordinal);

    var discussedOnly = CriterionInventory.DiscussedInComments()
      .Where(declared.Contains)
      .Where(id => !cited.Contains(id) && !tripwired.Contains(id))
      .ToArray();

    Assert.True(
      discussedOnly.Length > 0,
      "no declared criterion is discussed in a comment without also carrying a trait. Either the tree has " +
      "tagged everything it has ever reasoned about — which would be new — or the comment scan and the " +
      "trait scan have converged on the same input and one of them is reading the wrong half of the file.");
  }
}

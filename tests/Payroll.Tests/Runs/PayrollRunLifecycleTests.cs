using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests.Runs;

// THE RUN STATE MACHINE (REQ-PAY-0010, REQ-PAY-0012, OD-PAY-0009, OD-PAY-0011).
//
// The three-type shape is what these assert against, and the amendment that produced it is worth
// remembering here: `PayrollRunLine` is `IAppendOnlyEntity` and written ONCE by `Approve`; draft lines are
// mutable and replaced wholesale. Tests that could pass under the superseded single-aggregate design would
// not be testing the thing that was ruled.
public sealed class PayrollRunLifecycleTests
{
  private static readonly Guid Employee = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private static readonly Guid Account = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

  [Fact]
  [Trait("Decision", "REQ-PAY-0010")]
  // ⚠ CITED BY B18, body-confirmed: the first half -- `Payroll.RunNotApprovable`.
  [Trait("Criterion", "AC-PAY-0015")]
  public void A_draft_run_cannot_be_approved_without_being_calculated()
  {
    var run = PayrollTestData.Run(Guid.NewGuid());

    var approved = run.Approve("approver");

    Assert.True(approved.IsFailure);
    Assert.Equal("Payroll.RunNotApprovable", approved.Error.Code);
    // The refusal NAMES the state it found, so a user is told what happened rather than sent looking.
    Assert.Contains("Draft", approved.Error.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "REQ-PAY-0010")]
  // ⚠ CITED BY B18, body-confirmed: the second half -- `Payroll.RunNotPostable`. The criterion names both gates, so it takes both tests.
  [Trait("Criterion", "AC-PAY-0015")]
  public void A_calculated_run_cannot_be_posted_without_being_approved()
  {
    var run = Calculated();

    var posted = run.MarkPosted(Guid.NewGuid(), "poster");

    Assert.True(posted.IsFailure);
    Assert.Equal("Payroll.RunNotPostable", posted.Error.Code);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  // ⚠ CITED BY B18, body-confirmed: `Assert.Single(run.DraftLines)` after a second calculation -- "replaces the previous line set
  // ENTIRELY", which a mere success assertion would not establish.
  [Trait("Criterion", "AC-PAY-0014")]
  public void Recalculation_is_free_before_approval_and_replaces_the_whole_line_set()
  {
    var run = Calculated();
    var firstSet = run.DraftLines.Select(line => line.Id).ToArray();

    var second = run.SetCalculation([DraftLine(run.Id, 900m)], "tester");

    Assert.True(second.IsSuccess);
    Assert.Single(run.DraftLines);
    // Replaced, not merged: no identifier from the first calculation survives.
    Assert.DoesNotContain(run.DraftLines.Single().Id, firstSet);
    Assert.Equal(900m, run.DraftLines.Single().Amount);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public void An_approved_run_refuses_recalculation()
  {
    var run = Approved();

    var again = run.SetCalculation([DraftLine(run.Id, 1m)], "tester");

    Assert.True(again.IsFailure);
    Assert.Equal("Payroll.RunNotRecalculable", again.Error.Code);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public void A_posted_run_refuses_recalculation_and_says_how_to_correct_it()
  {
    var run = Approved();
    Assert.True(run.MarkPosted(Guid.NewGuid(), "poster").IsSuccess);

    var again = run.SetCalculation([DraftLine(run.Id, 1m)], "tester");

    Assert.True(again.IsFailure);
    // The message carries the remedy, because there is no edit path and never will be.
    Assert.Contains("reversing", again.Error.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void A_calculated_run_with_no_lines_cannot_be_approved()
  {
    var run = PayrollTestData.Run(Guid.NewGuid());
    Assert.True(run.SetCalculation([], "tester").IsSuccess);

    var approved = run.Approve("approver");

    Assert.True(approved.IsFailure);
    Assert.Equal("Payroll.RunHasNoLines", approved.Error.Code);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0015")]
  public void Approval_writes_the_append_only_line_set_from_the_draft()
  {
    var run = Calculated();
    Assert.Empty(run.Lines);

    Assert.True(run.Approve("approver").IsSuccess);

    // The payslip projects over THESE, which is why a payslip exists precisely when an approved record does.
    Assert.Single(run.Lines);
    Assert.Equal(run.DraftLines.Single().Amount, run.Lines.Single().Amount);
    Assert.Equal(PayrollRunStatus.Approved, run.Status);
  }

  [Fact]
  public void Posting_records_the_journal_and_only_once()
  {
    var run = Approved();
    var journal = Guid.NewGuid();

    Assert.True(run.MarkPosted(journal, "poster").IsSuccess);
    Assert.Equal(journal, run.JournalEntryId);
    Assert.Equal(PayrollRunStatus.Posted, run.Status);

    // A second posting is refused: the run is no longer Approved.
    Assert.True(run.MarkPosted(Guid.NewGuid(), "poster").IsFailure);
    Assert.Equal(journal, run.JournalEntryId);
  }

  [Fact]
  public void Posting_requires_a_journal_identity()
  {
    var run = Approved();

    var posted = run.MarkPosted(Guid.Empty, "poster");

    Assert.True(posted.IsFailure);
    Assert.Equal("Payroll.RunJournalRequired", posted.Error.Code);
  }

  [Fact]
  [Trait("Decision", "AMENDMENT 2026-08-24")]
  public void The_approved_line_type_is_append_only_and_the_draft_type_is_not()
  {
    // ---- THE RULED SHAPE, ASSERTED STRUCTURALLY.
    //
    // `TenantDbContext.PreventAppendOnlyMutation` refuses Modified or Deleted for `IAppendOnlyEntity`
    // UNCONDITIONALLY. That is exactly why the draft type must NOT carry it — recalculation deletes rows —
    // and why the approved type must. Adding the interface to the draft type would silently make
    // `OD-PAY-0011`'s free recalculation impossible, and no unit test of the aggregate would notice.
    Assert.Contains(
      typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity),
      typeof(PayrollRunLine).GetInterfaces());

    Assert.DoesNotContain(
      typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity),
      typeof(PayrollRunDraftLine).GetInterfaces());
  }

  [Fact]
  [Trait("Decision", "AMENDMENT 2026-08-24")]
  public void The_run_itself_is_not_append_only_because_it_must_record_its_posting()
  {
    // If the run carried the marker, writing PostedUtc and JournalEntryId after approval would be refused by
    // the write boundary. Its immutability after Posted is a domain guard, and that is acceptable only
    // because the truth-bearing records — the approved lines and the journal — are structurally protected.
    Assert.DoesNotContain(
      typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity),
      typeof(PayrollRun).GetInterfaces());
  }

  [Fact]
  public void Nothing_outside_the_module_can_fabricate_an_approved_pay_record()
  {
    // `OD-GL-0007`'s boundary applied to payroll: the append-only line's constructor is internal, so an
    // approved pay record can only come from approving a run.
    var constructors = typeof(PayrollRunLine).GetConstructors(
      System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    Assert.Empty(constructors);
  }

  // ================================================================================================
  // REVERSAL, AND THE FACT THE RUN NOW RECORDS ABOUT ITSELF (T-112).
  // ================================================================================================
  //
  // Until T-112 a reversal wrote nothing here, so a reversed period and a live posted period were
  // indistinguishable in `PayrollRuns` — and the unique index refused the *rerun* half of
  // `OD-PAY-0011`'s reverse-and-rerun. These pin the fact that makes the filtered index possible.

  // ---- ONLY A POSTED RUN CAN BE REVERSED, AND THE ORDER IS THE POINT.
  //
  // The run records what the LEDGER did. A run that never posted has no journal to reverse, so stamping one
  // would be a claim about an entry that does not exist.
  [Fact]
  public void A_run_that_never_posted_cannot_be_marked_reversed()
  {
    var calculated = Calculated();
    var approved = Approved();

    Assert.Equal(PayrollErrors.RunNotReversible.Code, calculated.MarkReversed().Error.Code);
    Assert.Equal(PayrollErrors.RunNotReversible.Code, approved.MarkReversed().Error.Code);

    Assert.False(calculated.IsReversed);
    Assert.False(approved.IsReversed);
  }

  // ---- IT KEEPS ITS STATUS AND ITS JOURNAL, AND GAINS ONE FACT.
  //
  // `Status` stays `Posted` and `JournalEntryId` still names the original entry — **nothing here restates
  // what GL holds.** The run records only what Payroll's own uniqueness rule needs.
  [Fact]
  public void A_reversed_run_stays_posted_and_keeps_naming_its_journal()
  {
    var run = Posted(out var journalId);

    Assert.True(run.MarkReversed().IsSuccess);

    Assert.True(run.IsReversed);
    Assert.NotNull(run.ReversedUtc);
    Assert.Equal(PayrollRunStatus.Posted, run.Status);
    Assert.Equal(journalId, run.JournalEntryId);
  }

  // ---- A SECOND REVERSAL IS REFUSED RATHER THAN RESTAMPED.
  //
  // Two reversing entries for one posting, and the second timestamp would silently overwrite the record of
  // when the first happened — which is the one thing a lifecycle timestamp exists to preserve.
  [Fact]
  public void A_run_cannot_be_reversed_twice_and_the_first_timestamp_survives()
  {
    var run = Posted(out _);
    Assert.True(run.MarkReversed().IsSuccess);
    var first = run.ReversedUtc;

    var second = run.MarkReversed();

    Assert.Equal(PayrollErrors.RunAlreadyReversed.Code, second.Error.Code);
    Assert.Equal(first, run.ReversedUtc);
  }

  // ---- AND AN UNREVERSED RUN SAYS SO, WHICH IS WHAT THE INDEX FILTERS ON.
  [Fact]
  public void A_posted_run_is_not_reversed_until_it_is()
  {
    var run = Posted(out _);

    Assert.False(run.IsReversed);
    Assert.Null(run.ReversedUtc);
  }

  // ---- ⚠⚠⚠ A POSTED RUN REFUSES RE-APPROVAL, AND THE LINE SET IS THE HALF THAT MATTERS (AC-PAY-0017).
  //
  // `AC-PAY-0017` bans three verbs on a posted run: recalculated, edited, re-approved. The first is covered
  // by `A_posted_run_refuses_recalculation_and_says_how_to_correct_it`; the second by there being no edit
  // path at all. ***THE THIRD HAD NO DRIVER, AND THAT WAS MEASURED RATHER THAN GUESSED.***
  //
  // `Approve` guards on `Status != Calculated`. The gated suites drove that guard with **Draft**
  // (`A_draft_run_cannot_be_approved_without_being_calculated`) and drove recalculation with **Approved** —
  // never approval with **Posted**. Widening the guard to `Status != Calculated && Status != Posted` left
  // ALL SEVEN GATED SUITES GREEN, against a freshly rebuilt assembly. So the Draft case does not stand in
  // for this one: the defect is expressible with every existing test still passing.
  //
  // ⚠⚠ AND THE REFUSAL IS NOT THE INTERESTING HALF. `Approve`'s body runs `lines.Clear()` and rebuilds the
  // approved set from the drafts, so what this guard prevents is **a posted payroll's line records silently
  // rewritten** — not a wrong status code. *A test asserting only `RunNotApprovable` would pass on an
  // implementation that refuses AND clears the lines.*
  //
  // ⚠⚠⚠ THE LINE **IDS** CARRY THE ASSERTION; THE AMOUNTS CANNOT. Re-approval rebuilds from the SAME frozen
  // drafts — recalculation is already refused here — so every amount comes back identical and an amount
  // comparison would pass on the defect. The rebuilt lines take fresh `Guid.NewGuid()` identities, and that
  // is the only observable that moves.
  [Fact]
  [Trait("Criterion", "AC-PAY-0017")]
  public void A_posted_run_refuses_re_approval_and_keeps_the_line_set_it_posted()
  {
    var run = Posted(out _);
    var postedLineIds = run.Lines.Select(line => line.Id).ToArray();

    // THE PREMISE, so the comparison below is over a real line set rather than two empty sequences.
    Assert.NotEmpty(postedLineIds);

    var again = run.Approve("approver");

    Assert.True(again.IsFailure);
    Assert.Equal(PayrollErrors.RunNotApprovable(PayrollRunStatus.Posted).Code, again.Error.Code);

    // ***THE HALF THE RESULT VALUE CANNOT CARRY.***
    Assert.Equal(postedLineIds, run.Lines.Select(line => line.Id));
    Assert.Equal(PayrollRunStatus.Posted, run.Status);
  }

  // ---- ⚠⚠⚠ WHO PERFORMED EACH TRANSITION, AND WHEN (AC-PAY-0018).
  //
  // Six fields — `CalculatedBy`/`CalculatedUtc`, `ApprovedBy`/`ApprovedUtc`, `PostedBy`/`PostedUtc` — are
  // declared with private setters, given max-length column configuration, and carried through
  // `PayrollReadModels` and `PayrollTransportContracts` to the wire. ***AND NOTHING ASSERTED ANY OF THE
  // SIX.***
  //
  // ⚠ THE POPULATION IS CLOSED BY THE LANGUAGE, WHICH IS WHY THAT IS AN ENUMERATION AND NOT A TOKEN SEARCH.
  // The names are IDENTICAL in the domain type, the read model, the transport contract and the EF
  // configuration — nothing renames them at any boundary — so an assertion on these values anywhere in the
  // tree must spell one of the six. Case-insensitive, all six, whole test tree: one hit, and it was a
  // comment.
  [Fact]
  [Trait("Criterion", "AC-PAY-0018")]
  public void Each_transition_records_who_performed_it_and_when_and_the_earlier_stamps_survive()
  {
    var run = PayrollTestData.Run(Guid.NewGuid());

    // THE PREMISE. Unset beforehand, so each assertion below is about the transition WRITING the field
    // rather than about a value that happened to be there all along.
    Assert.Null(run.CalculatedBy);
    Assert.Null(run.ApprovedBy);
    Assert.Null(run.PostedBy);

    Assert.True(run.SetCalculation([DraftLine(run.Id, 1000m)], "the-calculator").IsSuccess);
    Assert.Equal("the-calculator", run.CalculatedBy);
    Assert.NotNull(run.CalculatedUtc);
    var calculatedUtc = run.CalculatedUtc;

    Assert.True(run.Approve("the-approver").IsSuccess);
    Assert.Equal("the-approver", run.ApprovedBy);
    Assert.NotNull(run.ApprovedUtc);
    var approvedUtc = run.ApprovedUtc;

    Assert.True(run.MarkPosted(Guid.NewGuid(), "the-poster").IsSuccess);
    Assert.Equal("the-poster", run.PostedBy);
    Assert.NotNull(run.PostedUtc);

    // ---- AND *"NONE OF IT IS SUBSEQUENTLY ALTERED"*, WHICH IS THE CLAUSE THE LAST TRANSITION TESTS.
    //
    // ⚠ THREE DISTINCT ACTOR NAMES ARE LOAD-BEARING. Reusing one string would make a later transition
    // overwriting an earlier field INVISIBLE — every assertion would still find exactly what it expected.
    // The same reasoning puts the timestamps in locals: `DateTimeOffset.UtcNow` moves between transitions,
    // so a restamped field is only detectable against the value captured at the time.
    Assert.Equal("the-calculator", run.CalculatedBy);
    Assert.Equal("the-approver", run.ApprovedBy);
    Assert.Equal(calculatedUtc, run.CalculatedUtc);
    Assert.Equal(approvedUtc, run.ApprovedUtc);
  }

  private static PayrollRun Posted(out Guid journalEntryId)
  {
    journalEntryId = Guid.NewGuid();
    var run = Approved();
    Assert.True(run.MarkPosted(journalEntryId, "poster").IsSuccess);
    return run;
  }

  private static PayrollRunDraftLine DraftLine(Guid runId, decimal amount) =>
    new(Guid.NewGuid(), runId, Employee, Guid.NewGuid(), PayElementKind.Earning, amount, 0, Account);

  private static PayrollRun Calculated()
  {
    var run = PayrollTestData.Run(Guid.NewGuid());
    Assert.True(run.SetCalculation([DraftLine(run.Id, 1000m)], "tester").IsSuccess);
    return run;
  }

  private static PayrollRun Approved()
  {
    var run = Calculated();
    Assert.True(run.Approve("approver").IsSuccess);
    return run;
  }
}

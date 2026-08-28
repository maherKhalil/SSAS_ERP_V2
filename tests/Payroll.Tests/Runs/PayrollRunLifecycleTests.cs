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
  public void A_calculated_run_cannot_be_posted_without_being_approved()
  {
    var run = Calculated();

    var posted = run.MarkPosted(Guid.NewGuid(), "poster");

    Assert.True(posted.IsFailure);
    Assert.Equal("Payroll.RunNotPostable", posted.Error.Code);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
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

using SSAS.GL.Domain.Journals;

namespace SSAS.Finance.Tests.Journals;

// THE JOURNAL AGGREGATES (BR-GL-0001, BR-GL-0002, OD-GL-0006, OD-GL-0007).
//
// Domain-layer tests: aggregate invariants, in memory, fast. What they CANNOT prove is anything enforced by
// the write boundary — `IAppendOnlyEntity` is refused by `TenantDbContext`, not by these types, so the
// append-only guarantee is asserted in `Integration.Tests` against real SQL and is deliberately not
// simulated here. A domain test asserting immutability would be asserting a property this layer does not
// own, which is the mistake FP-009 recorded when an API test claimed a transaction property its harness
// could not exercise.
public sealed class JournalDraftDomainTests
{
  private static readonly Guid AccountA = Guid.NewGuid();
  private static readonly Guid AccountB = Guid.NewGuid();

  private static JournalDraft NewDraft()
  {
    var draft = JournalDraft.Create(DateTimeOffset.UtcNow, "Opening entry", reference: null);
    Assert.True(draft.IsSuccess);
    return draft.Value;
  }

  [Fact]
  [Trait("Decision", "OD-GL-0007")]
  public void A_draft_is_created_without_lines_and_is_not_required_to_balance()
  {
    // The whole reason the draft is a separate aggregate: work in progress that does not satisfy
    // BR-GL-0001 needs somewhere to live. If a draft had to balance, it would be a posted journal with a
    // different name and OD-GL-0007's option 3 would buy nothing.
    var draft = NewDraft();

    Assert.Empty(draft.Lines);
    Assert.Equal(0m, draft.TotalDebits);
    Assert.Equal(0m, draft.TotalCredits);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0001")]
  public void A_balanced_draft_with_two_lines_is_postable()
  {
    var draft = NewDraft();

    var lines = draft.ReplaceLines(
    [
      (AccountA, 100m, 0m, "debit side"),
      (AccountB, 0m, 100m, "credit side")
    ]);

    Assert.True(lines.IsSuccess);
    Assert.True(draft.EnsurePostable().IsSuccess);
    Assert.Equal(100m, draft.TotalDebits);
    Assert.Equal(100m, draft.TotalCredits);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0001")]
  public void An_unbalanced_draft_is_refused_at_post_time_and_not_at_edit_time()
  {
    var draft = NewDraft();

    // The edit SUCCEEDS — an unbalanced draft is a legitimate intermediate state.
    var lines = draft.ReplaceLines(
    [
      (AccountA, 100m, 0m, null),
      (AccountB, 0m, 99m, null)
    ]);
    Assert.True(lines.IsSuccess);

    // The POST is what refuses it.
    var postable = draft.EnsurePostable();
    Assert.True(postable.IsFailure);
    Assert.Equal("Gl.JournalUnbalanced", postable.Error.Code);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0001")]
  public void A_single_line_draft_is_refused_because_one_line_cannot_balance()
  {
    var draft = NewDraft();
    Assert.True(draft.ReplaceLines([(AccountA, 100m, 0m, null)]).IsSuccess);

    var postable = draft.EnsurePostable();

    Assert.True(postable.IsFailure);
    Assert.Equal("Gl.JournalInsufficientLines", postable.Error.Code);
  }

  [Theory]
  [Trait("Decision", "BR-GL-0001")]
  [InlineData(100, 100)]  // both sides — ambiguous
  [InlineData(0, 0)]      // neither side — noise that still consumes a line number
  public void A_line_must_carry_exactly_one_side(decimal debit, decimal credit)
  {
    var draft = NewDraft();

    var lines = draft.ReplaceLines([(AccountA, debit, credit, null), (AccountB, 0m, 100m, null)]);

    Assert.True(lines.IsFailure);
    Assert.Equal("Gl.JournalLineNotSingleSided", lines.Error.Code);
  }

  [Fact]
  public void A_negative_amount_is_refused_rather_than_flipped()
  {
    // Refused rather than silently interpreted as the opposite side: a caller who sends -100 as a debit may
    // mean a 100 credit, or may have a bug. Guessing would turn a bug into a posting.
    var draft = NewDraft();

    var lines = draft.ReplaceLines([(AccountA, -100m, 0m, null), (AccountB, 0m, 100m, null)]);

    Assert.True(lines.IsFailure);
    Assert.Equal("Gl.JournalLineAmountNegative", lines.Error.Code);
  }

  [Fact]
  public void Replacing_lines_renumbers_them_densely_from_one()
  {
    var draft = NewDraft();
    Assert.True(draft.ReplaceLines(
      [(AccountA, 1m, 0m, null), (AccountB, 0m, 1m, null), (AccountA, 2m, 0m, null)]).IsSuccess);

    Assert.Equal([1, 2, 3], draft.Lines.Select(line => line.LineNumber).ToArray());

    // And replacement is wholesale: the previous set is gone, not merged.
    Assert.True(draft.ReplaceLines([(AccountA, 5m, 0m, null), (AccountB, 0m, 5m, null)]).IsSuccess);
    Assert.Equal([1, 2], draft.Lines.Select(line => line.LineNumber).ToArray());
    Assert.Equal(5m, draft.TotalDebits);
  }

  [Fact]
  public void An_entry_date_is_normalized_to_utc_and_is_not_the_creation_time()
  {
    var entryDate = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.FromHours(3));
    var draft = JournalDraft.Create(entryDate, "Dated entry", null);

    Assert.True(draft.IsSuccess);
    Assert.Equal(TimeSpan.Zero, draft.Value.EntryDateUtc.Offset);
    Assert.Equal(entryDate.ToUniversalTime(), draft.Value.EntryDateUtc);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void A_description_is_required(string? description)
  {
    var draft = JournalDraft.Create(DateTimeOffset.UtcNow, description, null);

    Assert.True(draft.IsFailure);
    Assert.Equal("Gl.JournalDescriptionInvalid", draft.Error.Code);
  }
}

public sealed class JournalEntryDomainTests
{
  private static readonly Guid AccountA = Guid.NewGuid();
  private static readonly Guid AccountB = Guid.NewGuid();

  private static JournalDraft BalancedDraft(Guid companyId)
  {
    var draft = JournalDraft.Create(DateTimeOffset.UtcNow, "Balanced", "REF-1").Value;
    draft.CompanyId = companyId;
    draft.ReplaceLines([(AccountA, 250m, 0m, "debit"), (AccountB, 0m, 250m, "credit")]);
    return draft;
  }

  [Fact]
  [Trait("Decision", "OD-GL-0007")]
  public void Posting_copies_the_draft_into_a_new_aggregate_rather_than_promoting_it_in_place()
  {
    var companyId = Guid.NewGuid();
    var draft = BalancedDraft(companyId);
    var yearId = Guid.NewGuid();
    var periodId = Guid.NewGuid();

    var entry = JournalEntry.Post(draft, yearId, periodId, "1");

    // A DIFFERENT identity. The draft still exists and is the caller's to discard — which is what lets
    // JournalEntry carry IAppendOnlyEntity from creation.
    Assert.NotEqual(draft.Id, entry.Id);
    Assert.Equal(companyId, entry.CompanyId);
    Assert.Equal(yearId, entry.FiscalYearId);
    Assert.Equal(periodId, entry.FiscalPeriodId);
    Assert.Equal("1", entry.JournalNumber);
    Assert.Equal(draft.EntryDateUtc, entry.EntryDateUtc);
    Assert.Equal(draft.Description, entry.Description);
    Assert.Equal("REF-1", entry.Reference);
    Assert.Null(entry.ReversesJournalEntryId);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0001")]
  public void A_posted_journal_carries_the_draft_lines_in_order_and_balances()
  {
    var entry = JournalEntry.Post(BalancedDraft(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), "7");

    Assert.Equal([1, 2], entry.Lines.Select(line => line.LineNumber).ToArray());
    Assert.Equal(250m, entry.TotalDebits);
    Assert.Equal(250m, entry.TotalCredits);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0006")]
  public void A_reversal_mirrors_every_line_and_links_back_to_the_original()
  {
    var original = JournalEntry.Post(BalancedDraft(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), "1");
    var reversalDate = original.EntryDateUtc.AddDays(3);

    var reversal = JournalEntry.Reverse(original, Guid.NewGuid(), "2", reversalDate, "Correction");

    Assert.Equal(original.Id, reversal.ReversesJournalEntryId);
    Assert.Equal(original.CompanyId, reversal.CompanyId);
    Assert.Equal(reversalDate, reversal.EntryDateUtc);
    Assert.Equal("Correction", reversal.Description);

    // ---- DEBITS AND CREDITS ARE EXCHANGED, LINE FOR LINE.
    //
    // Built FROM the original rather than from caller input, so a reversal cannot silently differ from what
    // it claims to reverse. Asserted per line rather than only on the totals, because equal totals would
    // also hold for a reversal that moved the amounts between the wrong accounts.
    foreach (var line in original.Lines)
    {
      var mirrored = reversal.Lines.Single(candidate => candidate.LineNumber == line.LineNumber);

      Assert.Equal(line.AccountId, mirrored.AccountId);
      Assert.Equal(line.Debit, mirrored.Credit);
      Assert.Equal(line.Credit, mirrored.Debit);
    }

    Assert.Equal(original.TotalDebits, reversal.TotalCredits);
    Assert.Equal(original.TotalCredits, reversal.TotalDebits);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0006")]
  public void The_original_is_untouched_by_being_reversed()
  {
    var original = JournalEntry.Post(BalancedDraft(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), "1");
    var debitsBefore = original.TotalDebits;

    _ = JournalEntry.Reverse(original, Guid.NewGuid(), "2", original.EntryDateUtc, "Correction");

    // "Reversed" is NEVER written onto the original — that would mean modifying an append-only row, which
    // the write boundary refuses. The fact is derived from the reversal's existence instead.
    Assert.Null(original.ReversesJournalEntryId);
    Assert.Equal(debitsBefore, original.TotalDebits);
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0002")]
  public void Posted_journals_and_their_lines_are_marked_append_only()
  {
    // The MARKER is what this layer owns; the ENFORCEMENT belongs to TenantDbContext and is proven against
    // real SQL in Integration.Tests. Asserting the marker here is what makes a future removal of it loud.
    Assert.Contains(
      typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity), typeof(JournalEntry).GetInterfaces());
    Assert.Contains(
      typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity), typeof(JournalLine).GetInterfaces());

    // And the mutable half deliberately is NOT.
    Assert.DoesNotContain(
      typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity), typeof(JournalDraft).GetInterfaces());
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0007")]
  public void A_posted_journal_has_no_row_version_and_a_draft_does()
  {
    // RowVersion on an append-only type would advertise a mutation that cannot happen and invite someone to
    // write the update path it implies.
    // ⚠ BOTH HALVES BOUND (258). They shared a bare string: a rename broke the positive loudly, but a typo
    // at the NEGATIVE alone left the positive green and this one passing over a lookup that could not hit.
    Assert.Null(typeof(JournalEntry).GetProperty(nameof(JournalDraft.RowVersion)));
    Assert.NotNull(typeof(JournalDraft).GetProperty(nameof(JournalDraft.RowVersion)));
  }
}

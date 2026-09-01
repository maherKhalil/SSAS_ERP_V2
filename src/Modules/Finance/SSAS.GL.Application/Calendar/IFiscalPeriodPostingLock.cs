using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Application.Calendar;

// ==================================================================================================
// POSTING AND PERIOD-STATE CHANGES ARE SERIALISED AGAINST EACH OTHER (249, ADR-020's fence shape).
// ==================================================================================================
//
// `BR-GL-0003` is read-then-act: a period read as OPEN must still be open when the journal row is
// written. Opening a transaction before the read does NOT provide that — it provides atomicity, not
// isolation.
//
// ---- ⚠⚠⚠ THIS WAS MEASURED, NOT REASONED. 2026-09-01.
//
// A probe held a poster's transaction open, read the period as OPEN through the ordinary path, closed the
// period from a SECOND connection, and let the poster commit. THE CLOSE SUCCEEDED IMMEDIATELY — the
// probe used `SET LOCK_TIMEOUT 3000` precisely so that blocking would have surfaced as error 1222, and
// it did not block. A JOURNAL WAS COMMITTED INTO A PERIOD WHOSE STATUS WAS `Closed`.
//
// So the absence of a lock was demonstrated by behaviour, not inferred from the absence of an
// `IsolationLevel` setting — of which there are, separately, zero in `src/`.
//
// ---- THE SHAPE IS ALREADY IN THIS TREE, AND ITS COMMENT DESCRIBES THIS DEFECT WITH THE NOUNS SWAPPED.
//
// `TenantCutoverWriteFence` (ADR-020): *"A SHARED, TRANSACTION-OWNED APPLICATION LOCK … the freezer takes
// the same resource EXCLUSIVELY … this is what closes the race a boolean cannot: check "not frozen" ->
// freezer freezes -> old writer commits."*
//
// Substitute the nouns: check "period open" -> closer closes -> poster commits. `@LockMode = "Shared"`
// appears exactly once in `src/` today, in that fence. This is its second use, for the same shape.
//
// ---- ⚠⚠ WHY SHARED FOR POSTERS AND EXCLUSIVE FOR THE STATE WRITER.
//
// Shared is compatible with Shared, so POSTS DO NOT SERIALISE AGAINST EACH OTHER — the hot path keeps its
// concurrency. A period close takes the resource EXCLUSIVELY and therefore waits for in-flight posts to
// commit, which is the ordering the rule wants rather than a cost it pays.
//
// ⚠ AND THAT IS WHY IT IS NOT THE ROWVERSION. Engaging the period's existing concurrency token would have
// closed the race against a CLOSER and, in the same stroke, made two concurrent POSTS to one period
// conflict with each other — turning a rare correctness bug into a routine false refusal on the hot path.
//
// ---- ⚠⚠⚠ AND A RE-READ INSIDE THE TRANSACTION IS NOT A SMALLER VERSION OF THIS. DO NOT PROPOSE IT.
//
// Under READ COMMITTED a re-read NARROWS this window and does not close it: the closer can still commit
// between the re-read and the insert. It is the most tempting fix because it is the smallest diff, and it
// would leave a defect that reproduces LESS OFTEN AND IS THEREFORE HARDER TO FIND. The fence's own second
// mechanism is "a DURABLE READ … performed AFTER the lock is held" — the read must FOLLOW the lock, and
// that ordering is the whole of it.
//
// ---- WHY THE RESOURCE IS COMPANY-SCOPED, WHICH IS FORCED RATHER THAN CHOSEN.
//
// The period is not supplied by the caller. `PostJournalCommandHandlers` resolves it from the entry date
// AFTER reading the year: `year.ResolveOpenPeriodFor(draft.EntryDateUtc)`. So the period id does not
// exist until after the read, and a read that must follow the lock cannot be keyed on its own result.
// COMPANY IS THE COARSEST KEY KNOWN BEFORE THE READ AND THEREFORE THE ONLY AVAILABLE ONE.
//
// ⚠ A finer key is possible and costs more machinery than this defect justifies: lock the company, read,
// then re-lock on the period and RE-READ to confirm nothing moved in between. Recorded so the coarse key
// reads as a decision rather than as carelessness.
public interface IFiscalPeriodPostingLock
{
  // Taken by a POSTER, INSIDE its transaction, BEFORE the period is read. Released by commit or rollback.
  Task<Result> AcquireForPostingAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);

  // Taken by the PERIOD-STATE WRITER, inside its transaction, before the period is read. Waits a bounded
  // time for in-flight posts and then refuses RETRYABLY rather than hanging: an operator who is told
  // "posting in progress" can act, and one whose request never returns cannot.
  Task<Result> AcquireForStateChangeAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}

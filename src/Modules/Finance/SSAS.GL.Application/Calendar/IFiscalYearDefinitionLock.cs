using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Application.Calendar;

// ==================================================================================================
// THE COMPANY FISCAL-CALENDAR LOCK (T-184).
// ==================================================================================================
//
// ---- WHAT NO CONSTRAINT CAN HOLD, AND WHY THAT IS STILL TRUE.
//
// **`DEC-L-084` is untouched by this: SQL Server still cannot express "these ranges must not overlap".**
// Overlap is a range predicate across rows, not an equality on a key, and `CalendarConfigurations` records
// that there is deliberately no index on `(StartUtc, EndUtc)`. **Nothing here adds a constraint.**
//
// What this reverses is the SECOND half of that reasoning — that the alternative was unacceptable.
//
// ---- ⚠ WHAT AN OVERLAP ACTUALLY COSTS, WHICH IS NOT A DUPLICATE ROW.
//
// `IFiscalCalendarRepository.GetCoveringAsync` resolves a journal's period with `FirstOrDefaultAsync`, and
// `PostJournalDraftCommandHandler` numbers each journal from the year that call returns. **So two
// overlapping years mean journals for the same date land in an ARBITRARILY CHOSEN year, non-deterministically,
// with their numbering drawn from that year's sequence** — postings scattered across two numbering
// sequences with no rule saying which.
//
// **That is ledger integrity, not a rare annoyance**, and it is why the original judgement — that the
// exposure was small because defining a year is rare — was weighing the wrong thing: not how often it
// happens, but what it costs when it does.
//
// **`GetCoveringAsync`'s soundness now rests on this lock.** It is not being changed to
// `SingleOrDefaultAsync`: once the race is closed a second covering year cannot exist, and a throwing read
// would then be a worse failure mode than this lock's clean refusal.
//
// ---- THE PRECEDENT IS `IDepartmentHierarchyLock` AND THE SHAPE IS IDENTICAL.
//
// A cycle is a predicate across many rows that no constraint can express; so is an overlap. That lock is
// company-scoped — *"exactly as narrow as correctness allows"* — `Transaction`-owned, and five seconds.
// Its handler states the gap in words that fit here exactly: **"two individually legal moves combining
// into a cycle that neither transaction could have detected."** Substitute years for moves and an overlap
// for a cycle.
//
// ⚠ **The rarity argument was used to reach the opposite conclusion in each module.** GL: *"defining a
// fiscal year is rare and deliberate"* — therefore do not lock. HR: *"hierarchy moves are rare, so
// contention is rare"* — therefore locking is cheap, so lock. **Rarity is an argument about the COST of a
// lock, never about the acceptability of the gap.**
public interface IFiscalYearDefinitionLock
{
  // Taken INSIDE the caller's transaction and released by its commit or rollback.
  //
  // ⚠ **Both the code check and the overlap check must run while this is held.** Acquiring after them
  // would serialise only the insert and leave the read racing — the gap would move, not close.
  Task<Result> AcquireAsync(
    Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
}

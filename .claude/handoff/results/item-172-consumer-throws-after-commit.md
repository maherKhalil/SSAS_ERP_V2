# item 172 — a domain-event consumer that throws after commit

**Gated work.** Three tests added to `tests/Platform.Tests/Persistence/DomainEventFlowTests.cs` (now 10),
`GATE_SCOPE=TASK` **green**. **No `src/` change — behaviour is reported, not altered.**

## ⚠ The answer: the behaviour is WRONG, and worse than the shape suggests

**The commit succeeds, the data is written, the caller is told the command failed, and the consumer's
exception is destroyed on the way out.** Three failures compound:

| # | what happens | test |
|---|---|---|
| 1 | the consumer throws `InvalidOperationException("consumer failed after commit")` | — |
| 2 | ⚠ the `catch` calls `RollbackAsync` on the **already-committed** transaction, **that throws**, and the provider error propagates **instead of** the consumer's — `throw;` is never reached | `A_consumer_that_throws_after_commit_loses_its_exception_to_a_rollback_failure` |
| 3 | ⚠ `CommitAsync` threw before setting `completed`, so **disposal believes the transaction is open and rolls back again** — refused, because `EfUnitOfWork`'s `finally` already cleared its field. In an `await using` block this **replaces** whatever the body was propagating | `Disposing_after_that_failure_throws_again_and_masks_the_first_exception` |
| 4 | the row is in the database throughout | `The_row_is_committed_even_though_the_caller_saw_a_failure` |

**Measured, not read.** The exception surfacing from `CommitAsync` is
`"This SqliteTransaction has completed; it is no longer usable."` — the rollback's error. **The consumer's
message never reaches the caller at all.** The assertion is written on shape rather than on the SQLite
wording, which is provider-specific.

## ⚠ Is the behaviour right? No — and the ranking matters

**The commit has already succeeded. A consumer failure afterwards is not a reason to report the command as
failed** — the write is durable, and a caller told "failed" may retry an operation that already happened.
That is the defect, and it outranks whatever `RollbackAsync` does.

**The masking is the second defect and it is what makes the first hard to diagnose.** An operator sees a
transaction-provider error. Nothing in it names the consumer, the event, or the fact that the commit
succeeded. Both the cause and the outcome are hidden.

## The plant, which is also the candidate fix — and it is incomplete

Removing `await currentTransaction.RollbackAsync(CancellationToken.None);` from the `catch`:

- `A_consumer_that_throws_after_commit_loses_its_exception_to_a_rollback_failure` **FAILED** — because the
  consumer's exception then does surface. That confirms the rollback is what destroys it.
- ⚠ **`Disposing_after_that_failure_throws_again` still PASSED.** Removing the rollback does **not** stop
  disposal from throwing, because `completed` is still false and the unit of work's field is still cleared.

**So a fix needs both halves:** do not roll back a transaction that has committed, **and** mark the
transaction completed so disposal does not try. **Fixing only the visible half leaves the masking in
place.**

## What I recommend, and have not done

**Dispatch belongs outside the `try`, after the transaction is complete.** A consumer failure would then
be a post-commit concern — logged, surfaced as itself, or swallowed by policy — and could not be confused
with a transaction failure. That is a behaviour change to `EfUnitOfWork.CommitAsync` affecting every
command in the product, and the item said report before changing.

**Whether a consumer failure should reach the caller at all is a design question, not a measurement.**
Item 166 established that dispatch after commit is deliberate — an event announcing rolled-back work is
worse than no event. The same reasoning says a caller should not be told a committed command failed.

## What these tests do NOT establish

- **The tests pin current behaviour, not correct behaviour.** They will need changing when this is fixed,
  and they say so at the declaration.
- **SQLite, not SQL Server.** The masking mechanism is EF/`EfUnitOfWork` logic and provider-independent,
  but the exact exception text is not, which is why nothing asserts on it.
- **A consumer that throws OUTSIDE a transaction is untested.** There, `SaveChangesAsync` dispatches
  directly with no `try`, so the consumer's exception should surface unmasked — expected, not measured.
- **Multiple consumers**: `DomainEventDispatcher` loops consumers sequentially, so a throw abandons the
  remainder. Not exercised here.

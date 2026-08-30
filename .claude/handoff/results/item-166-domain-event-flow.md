# item 166 — the domain-event flow, exercised rather than read

**Gated work.** `tests/Platform.Tests/Persistence/DomainEventFlowTests.cs`, **5 tests**,
`GATE_SCOPE=TASK` **green**. No `src/` change.

## ⚠ The check-first question: events raised inside a transaction are NOT lost

`TerminateEmployeeCommandHandler` says *"`EfUnitOfWork` dispatches domain events only when no transaction
is open"*. **That sentence is true and the inference it invites is false.**

| | |
|---|---|
| `SaveChangesAsync` with a transaction open | **withholds** — `if (transaction is null)` guards the dispatch |
| `ITransaction.CommitAsync` | saves, commits, **then dispatches** |
| rollback or dispose without commit | **never dispatches** |

**Withheld, then released — not dropped.** And the rollback behaviour is the reason the design is that way:
an event announcing a termination that was rolled back would be worse than no event, which is what the
handler's comment goes on to say.

**The wrapper does not break this.** `TenantUnitOfWork` caches a single inner `EfUnitOfWork` and delegates
both `BeginTransactionAsync` and `SaveChangesAsync` to it, so the transaction field they share is the same
one — had it opened a transaction on the `DbContext` directly, `transaction` would have stayed null and
events would have dispatched **before** commit. That was the hazard worth checking and it does not occur.

## What the tests establish

| test | establishes |
|---|---|
| `A_saved_aggregate_reaches_a_registered_consumer_with_metadata` | the flow works end to end, with `CorrelationId`, `ActorId` and `RequestId` populated |
| `A_dispatched_event_is_cleared_and_is_not_announced_twice` | `ClearDomainEvents` runs, so a second save announces nothing |
| `A_save_inside_an_open_transaction_dispatches_nothing_yet` | the withholding half |
| `Committing_the_transaction_dispatches_what_the_save_withheld` | **the releasing half — the one that shows nothing is lost** |
| `A_rolled_back_transaction_never_dispatches` | undone work is never announced |

Real `EfUnitOfWork`, real `DomainEventDispatcher`, real `PlatformDbContext` on SQLite, a registered
consumer. **Asserting only the withholding half would have pinned "not dispatched" as the whole truth and
read as a defect** — which is how the concern arose in the first place.

## ⚠ The plants, and the second one is the feared bug itself

| plant | result |
|---|---|
| removed `if (transaction is null)` so the save dispatches regardless | **3 FAILED** — including `A_rolled_back_transaction_never_dispatches`, i.e. the plant announces work that is then undone, which is the real-world harm the guard prevents |
| removed `DispatchDomainEventsAsync` from `CommitAsync` | **1 FAILED** — `Committing_the_transaction_dispatches_what_the_save_withheld` |

**The second plant IS the scenario the item was dispatched to check.** Had events genuinely been lost
inside transactions, this test would have failed on the first run rather than needing a plant — which is
what makes the green meaningful rather than merely reassuring.

Both reverted, 5 green. The test file was staged before planting, so the revert restored it from the index.

## ⚠ What this population excludes

- **The aggregate is a probe and no command handler is involved.** What is real is everything the event
  passes *through*: change tracker, unit of work, dispatcher, consumer registration, metadata. A
  production handler adds its own dependency graph without changing any of that. **A handler-level test
  belongs in `Integration.Tests` against a real database and is not in the TASK gate** — say the word and
  I will add one.
- **Only aggregates in the change tracker are dispatched from.** `DispatchDomainEventsAsync` reads
  `dbContext.ChangeTracker.Entries()`, so an aggregate that raised events and was never attached — read
  `AsNoTracking`, or mutated on a detached instance — is invisible to dispatch. Not tested here, and it is
  the most plausible remaining way for an event to be silently dropped.
- **Consumer failure semantics are untested.** A consumer that throws after `CommitAsync` propagates into
  the `catch` in `EfUnitOfWork.CommitAsync`, which then calls `RollbackAsync` on an already-committed
  transaction. What that does is not established here.
- SQLite is not SQL Server; this exercises the dispatch mechanism, not provider behaviour.

## Note on the `DequeueDomainEvents` account

Confirmed: `AggregateRoot` declares both `DequeueDomainEvents()` and `ClearDomainEvents()`, and the
dispatch path reads the `DomainEvents` property then calls `ClearDomainEvents()`. **`DequeueDomainEvents`
genuinely has no production consumer** — so the earlier enumeration was correct about the member it
searched for, and that member was not the one the flow uses.

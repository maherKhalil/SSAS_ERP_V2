# item 173 — a committed command is no longer reported as failed

**Gated work.** `GATE_SCOPE=TASK` **green**; condition 4 satisfied — 1 of 7 suite totals moved against 42
non-comment `src/` lines. Item 172's three tests are rewritten; the suite is now 12.

## What changed

| # | change | file |
|---|---|---|
| 1 | **dispatch moved outside the `try`**, after the transaction completes | `EfUnitOfWork.CommitAsync` |
| 2 | **`completed` set BEFORE the commit attempt**, not after | `EfUnitOfWork.EfTransaction.CommitAsync` and `.RollbackAsync` |
| 3 | **each consumer isolated** — a throw no longer abandons the rest | `DomainEventDispatcher` |
| 4 | **the failure is logged**, at `Error`, with consumer type, event type and correlation id | `DomainEventDispatcher` |
| 5 | cancellation stops dispatch **without throwing** | `DomainEventDispatcher` |

**(2) is the disposal half.** `EfUnitOfWork.CommitAsync` disposes the transaction and clears its field in a
`finally`, so the transaction is finished with **whether or not the commit succeeded**. Setting the flag
afterwards left a failed commit with `completed == false`, and disposal then attempted a rollback that
`EnsureActiveTransaction` refused — a second exception which, in an `await using` block, **replaces** the
one the body was propagating.

**(5)** is included because `ThrowIfCancellationRequested` from that position would report a committed
command as cancelled — the same defect wearing a different exception.

## ⚠ The plants — and the first one reddened NOTHING, which is the finding

| plant | result |
|---|---|
| dispatch moved back **inside** the `try` | ⚠ **all 12 still passed** |
| `completed` set after the attempt again | `A_failed_commit_keeps_its_own_exception_through_disposal` **FAILED** |
| consumer isolation removed | **4 FAILED** |
| the log line removed, isolation kept | **2 FAILED** |

⚠ **The first plant means the ordering change is not what delivers the fix — the non-throwing dispatcher
is.** With per-consumer catches in place nothing throws from that position, so the `try` boundary makes no
observable difference. **Two fixes were ruled and the second hides the first.**

## ⚠ And the hole that discovery exposed, which I did NOT close

I wrote a test injecting a dispatcher that throws outright — a failure no per-consumer catch can intercept
— to test the ordering independently. **It failed.** Moving dispatch outside the `try` stops the *rollback
masking*; it does **not** stop a dispatch-level exception from reaching the caller over a durable write.

**So the headline rule holds only while `IDomainEventDispatcher` honours a contract that nothing
enforces.** The reachable paths are narrow — `DomainEventDispatcher` catches every consumer exception, and
its own body can realistically throw only from the metadata construction — but the guarantee is
conditional, not absolute.

**The fix is a `try`/`catch` around the post-commit dispatch in `EfUnitOfWork`, logging the failure. I
built it, measured its cost, and reverted it: it needs an `ILogger` on `EfUnitOfWork<TDbContext>`, which
means threading one through `PlatformUnitOfWork`, `TenantUnitOfWork` and 33 test construction sites,
30 of them in `Integration.Tests`.** That is a larger diff than the ruling asked for, and item 164's
lesson was to report a scope surprise rather than absorb it. **Ruling wanted; the code is one commit.**

## The rewritten tests

| test | what it now pins |
|---|---|
| `A_consumer_that_throws_after_commit_does_not_fail_the_command` | `CommitAsync` does not throw — with two assertions proving the consumer actually ran, so the pass is not vacuous |
| `The_consumer_failure_is_logged_with_its_consumer_and_event_type` | the log names `RecordingConsumer`, `FlowProbeAnnounced` and the correlation id |
| `The_row_is_committed_and_the_caller_saw_no_failure` | the write is durable and no exception surfaced |
| `A_failing_consumer_does_not_stop_the_ones_after_it` | **new** — isolation |
| `A_failed_commit_keeps_its_own_exception_through_disposal` | **new** — the disposal half, using a real commit failure (the connection is closed) rather than a consumer one |

Each declaration says what item 172 asserted and why it inverted. **The old assertions were not weakened —
they described a defect.**

## One test composition changed, and why it is not a weakening

`PlatformInfrastructureRegistrationTests` builds a container by hand and did not register logging, so it
could not resolve `ILogger<DomainEventDispatcher>`. `services.AddLogging()` was added. **Every real host
registers logging; without it that composition was not the one that ships.**

## Scope

- **SQLite, not SQL Server.** The disposal test induces a commit failure by closing the connection; the
  mechanism under test is `EfUnitOfWork` logic and provider-independent, but the failure injection is not.
- **A consumer throwing outside a transaction** now behaves the same way — the dispatcher is non-throwing
  everywhere — but that path is not separately tested.
- **Nothing asserts that `DomainEventDispatcher` cannot throw.** That is the conditional guarantee above.

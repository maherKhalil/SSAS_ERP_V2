# B18 pass 11 — `AC-EMP-0044` is satisfied and was unasserted

**TASK gate green, 0 warnings. ⚠ FP-006 → 41 of 47.**

## ⚠ THE ANSWER: SATISFIED, NOT A DEFECT — AND THE DISTINCTION MATTERED

I took this pass because it was **the only candidate gap with a security shape.** ⚠ **It is not a security
defect.** The handler is generic **by construction**:

`GlobalExceptionHandler` maps `InvalidOperationException` to the `_ =>` arm — **500, "An unexpected error
occurred."** — and hands `ProblemDetailsWriter` **the mapped TITLE, never the exception's message.**

**So the boundary's `"Branch ownership cannot be changed…"` cannot reach a caller.** ⚠ **The criterion was
satisfied and unasserted, which is the sixth time this sweep has found that pair** — and it is the reason
*candidate gap* was the right label rather than *gap*.

## The searches, recorded (B18 clause 1, first use)

| # | search | result |
|---|---|---|
| 1 | `UseExceptionHandler\|IExceptionHandler\|ProblemDetails` in `src/Host/` | found `GlobalExceptionHandler.cs`, `Program.cs:145` |
| 2 | `throw new InvalidOperationException` in the persistence BuildingBlocks | `EfUnitOfWork`, wrong thrower |
| 3 | `ownership cannot be changed\|ownership must match` in `src/` | ⚠ `TenantDbContext:350`, `PersistenceDbContext:141,161` — **the real throwers** |
| 4 | `UseExceptionHandler` across `src/` | `Program.cs:145` — one handler, globally installed |
| 5 | `GlobalExceptionHandler` in `tests/` | **one file**, `ProblemDetailsTests.cs` |
| 6 | `"An unexpected error occurred"` in `tests/` | ⚠ **nothing** — the generic title was asserted nowhere |

⚠ **Search 6 is the one that settled it, and it is a negative recorded so a later reader can re-run it.**

## Why nothing reached it before

`Exception_handler_writes_status_and_correlation_id` covers **six exception kinds** and asserts **status and
correlation id.** ⚠ **It never asserts what the body does NOT contain — and absence is the entire content
of this criterion.**

**The existing test is not weak; it is about something else.** A test can cover the same handler, the same
code path and the same exception type, **and still not touch the property.**

## The new test

`A_write_boundary_refusal_discloses_nothing_to_the_caller`, a `[Theory]` over the **three real boundary
messages** taken verbatim from the throwers — branch, tenant and company ownership.

**Two-sided, and the second half is load-bearing:**
- ⚠ the disclosure terms are **absent** — the criterion;
- ⚠ **`"An unexpected error occurred"` is PRESENT** — **without which a handler writing an EMPTY body would
  satisfy every other assertion.**

**Plant:** `ProblemDetailsWriter.WriteAsync(…, exception.Message, …)` — **the literal leak the criterion
forbids** — and all three cases redden. Restored; 9 of 9 green.

⚠ **The boundary messages are correct where they are.** They are read by developers in test failures and
logs; the criterion is about what a **caller** sees. **Two audiences, one string, and only one of them is
the criterion's subject.**

## Scope
- **The test exercises the handler directly**, as its neighbour does, rather than through a live request —
  so it asserts the handler's contract, not that every route reaches this handler. ⚠ **`Program.cs:145`
  installs it globally and `UseExceptionHandler` appears once; that is evidence, not an assertion.**
- FP-006: **41 of 47 cited.** Six remain, all searched twice: `0001`, `0002`, `0003`, `0004`, `0007`, `0013`.

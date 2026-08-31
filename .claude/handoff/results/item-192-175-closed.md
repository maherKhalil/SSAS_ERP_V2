# item 192 — 175's hole is closed, and the row stayed open because the history hides it

**Report. Nothing fixed, because nothing needed fixing.**

## ⚠ IT LANDED. THE ROW WAS OPEN FOR A DOCUMENTATION REASON, NOT A CODE ONE

`EfUnitOfWork.DispatchAfterCommitAsync` has its `try`/`catch` with `LogDispatchFailure`. `completed = true`
is set **before** the commit attempt. Both halves of 175 are in the product.

⚠ **They landed in commit `0beb577`, whose message is `docs(handoff): record item 176`.**

**That is why nobody could say whether the thing 175 was for was done.** The work is invisible in the
history: a reader auditing this thread sees a docs commit and no implementation. **The hole has been closed
since `0beb577` while the row implied otherwise** — the inverse of the ruling's fear, and the same root
cause as item 190's: **the record and the reality diverged, and only the record was consulted.**

## The test is green today

`A_dispatcher_that_throws_outright_still_does_not_fail_the_committed_command` — **passes**, in a suite of
**13 of 13** (`DomainEventFlowTests`, Platform.Tests, in TASK scope).

It injects a failure **no per-consumer catch can intercept** — the dispatcher itself — which is the only
way to observe the `catch` in `DispatchAfterCommitAsync`. It failed before that catch existed.

## ⚠ THE ORDERING QUESTION I RAISED: STILL REDUNDANT, AND DELIBERATELY SO

**The answer is already written at `EfUnitOfWork.cs:99–107`, measured twice by plant:**

> *"With `DispatchAfterCommitAsync`'s catch in place, moving this call back INSIDE the `try` above changes
> no test: the catch swallows the failure, so the outer `catch` never runs."*

**So the ordering is NOT load-bearing today.** It is kept because the guarantee then rests on **two**
things rather than one:

> *"If that catch were ever narrowed to a specific exception type — the ordinary way a broad catch gets
> 'tidied' — dispatch inside the `try` would resurrect the rollback-masking of item 172."*

⚠ **It is defence-in-depth against a specific, likely future edit, not redundancy to be cleaned up** — and
the file says so, so a future reader who notices it changes no test will find the reason before deleting it.

## Recommendation

**Close 175's row.** Both halves shipped, the regression test that could only ever have been written for
this is green, and the open question it left is answered in the code with its measurement attached.

## Scope
- **Verified by running the test, not by reading it** — `0beb577`'s message would have misled a reader who
  stopped at the log, which is the point of this item.
- I did not audit whether other rows in the 166→175 thread have the same message/content mismatch.

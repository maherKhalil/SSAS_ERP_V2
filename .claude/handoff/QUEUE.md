# The work queue — authoritative

**This file is the queue. Messages are notifications about it, not the queue itself.**

⚠ **Read this before going idle, and read it whenever a message from the architect seems to be missing.**
Roughly half the architect's inbound messages are lost, and the coder's outbound reports are not always
received. **A silence is a lost message, not a decision.** This file is in the repository, so neither
window can lose it.

⚠ **IF `git fetch` FAILS, READ THIS FILE FROM DISK INSTEAD — IT DOES NOT NEED THE NETWORK:**
`C:/Users/User/Documents/SSAS_ERP_V2/SSAS_ERP_V2-board/.claude/handoff/QUEUE.md`

**That is the architect's worktree, on this machine, always current with its last commit.** This file was
put in the repository so a dropped message could not stall the coder — **and was then read through `origin`,
the one dependency that fails at the same time as everything else.** On 2026-08-30 a GitHub outage left the
coder believing the queue held one item when it held three. **A local path closes that.**

**Standing authority:** work down the list without waiting between items. If one is blocked, skip it, start
the next, and say which in the following message. Every item here is gated (`src/` + `tests/`) unless
marked otherwise, so **`DEC-L-007` applies: green gate, merge immediately — no `MERGE` word is required.**

**Refill:** the architect refills at TWO remaining, not at zero. If this file drops to two open items and no
refill has arrived, **say so and start the highest-value thing you can defend** rather than idling.

## ⚠ CORRECTION — ITEM 90 WAS FINISHED, NOT IN FLIGHT. THE WORKING TREE IS CLEAN.

**An earlier version of this section said 90 was part-done and uncommitted on `ClaudeBranch`, and listed
eight files to inspect. That was wrong.** 90 gated green and merged as **PR #347** in the same minutes; the
files seen modified were the finished change immediately before its commit. **Nothing is uncommitted and
there is nothing to recover.**

**Recorded rather than deleted, because it is the failure this file exists to prevent** — a status written
from a snapshot taken at the wrong instant, published as fact, three minutes after the architect finished
correcting the same class of staleness elsewhere. **A working-tree observation is a measurement with a
timestamp, and it decays in seconds.**

**True state: `ClaudeBranch` carries #345, #346 and #347. 91, 92 and 93 are unstarted and fully specified
below. Nothing is in flight.**

| # | item | status | detail |
|---|---|---|---|
| 105 | **Do any per-handler unique-constraint translations now disagree with the generic arms added in 96 and 98?** Three modules gained a floor that did not exist when those handlers were written. **Check for a handler mapping the same condition to a different status or a contradictory message**, and for translations the generic arm has made redundant. Report; do not delete a per-handler arm without ruling — they name which constraint lost and whether a retry can converge, which the generic arm cannot. | **open** | BOARD 2026-08-30 |
| 106 | **Move the eight structural assertions out of Integration into `Architecture.Tests`.** They do no database work — `public void`, in-memory EF model — yet assert real invariants (cutover manifest coverage, principals before dependents, rowversion excluded, composed catalogue) **behind a 24-minute SQL Server suite that `GATE_SCOPE=TASK` never runs.** Before moving: (1) name any that reference Integration-only fixtures and stop on those; (2) confirm all eight **currently pass** — the TRX is from 2026-08-27, before the suite rework; (3) both baselines in the same commit; (4) **plant each after the move** — *"always green in a suite nobody reads"* is the weakest evidence there is. | **open — ruled** | BOARD 2026-08-30 |
| 107 | **⚠ A full gate run has written every TRX to `C:\c\Users\…` — outside the repository, where nothing looks.** Doubled `C:\c\`, almost certainly MSYS translating an absolute path handed to `dotnet --logger`. Not reproducing in TASK scope; a full set dated 2026-08-27 exists at that path. **This manufactures false absences in the gate itself** — a reader checking `TestResults/gate/` sees nothing or sees stale files and concludes the suite did not run or did not fail, **which is how this loop already lost eight days.** Confirm with one full-scope run, then prefer handing `--logger` a **repo-relative path** over suppressing the conversion. | **open** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78, 103 + 104 (**#354 — and the timing tell found ZERO false greens in 806 tests; what it found instead was eight structural assertions misfiled into Integration**), 102 (**and TWO OF THE FOUR FLAGGED GUARDS NEEDED NOTHING — an exact-list assertion and a fixed type array are anti-vacuous by construction**), 97 + 99 (**#353 — EF DOES log it: one Error entry, exception attached, category `Microsoft.EntityFrameworkCore.Update` and NOT the predicted `Database.Command`; closes as a comment, no remedy**), 100 (**`correlationId` is populated in production — the empty value was the Attendance test host omitting `UseCorrelationId`; the architect's concern INVERTED, and cost nothing because it was reported unverified**), 101 (**#352 — the false green closed, two rules converted to reflection, five plants recorded IN THE FILE**), 95 (**⚠ demonstrated a LIVE FALSE GREEN: `PersistenceArchitectureTests` passes all nine when its file walk finds nothing; 18 text-matching guards, 12 assert an absence, and ALL TWELVE have no recorded plant**), 98 (**#351 — GL under the amended DEC-DEP-0027**), 94 (**212 clauses, 16 unreasoned, groups unchanged in SHAPE — and the architect's published 291 was worse than the original 207**), 93 (**#349 — no route registered with a non-literal pattern**), 96 (**#350 — 409 for Attendance and Payroll; GL STOPPED against a recorded decision**), 97 (**measured: no logger on either unit of work, and `Error(Code, Message)` cannot carry the `SqlException` that holds the index name**), 92 (**measured: a duplicate key returns 500 in three modules; and the architect's premise was FALSE — the unit of work does discriminate 2601/2627 from a deadlock, so Group B is harmless**), 91 (**#348 — 13 teardowns reasoned, tests-side unreasoned 13 to 0, and a REFUSABLE guard replacing the vacuous one the architect specified; it found a genuinely empty body whose reason sat on a `#pragma` line**), 90 (**#347 — the auth-path defect: both overloads log the cause, the caller's answer is unchanged, and the test asserts BOTH halves because either alone is satisfied by the wrong fix; the other nine group-C catches were sound and now say so**), 89 (**31 literal-matching instruments, only ONE exposed — the rest are structurally immune because C# forbids composing a type name in a type position**), 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
(**cancelled — already measured**), 83 (#344), 84 (**overturned 80's premise**), 85 (**#345 — 71 constraints
removed, product-wide total 0, architecture test planted**), 86 (**#346 — 3 real gaps called, 2 allowlisted
with the segment check; the tightening caught a false green in its own implementation**), 87 (**the ruling
survived falsification and is pinned by a test**), 88 (**census: 207 catch clauses, 94 discard, 40
unreasoned; two instrument defects found and corrected before the number was trusted**).

**Group C of the 88 census is CLOSED (#347). Group A — the ~13 where the exception type IS the reason — is ruled LEAVE.** No comments
there; **the single collective statement is the architect's to write.**

**Division of labour, set by the owner 2026-08-30:** the coder does **coding and testing only**; planning
and documentation are the architect's. **A docs-shaped item is pushed back, not done.** When something
found while coding belongs in a document, **report the finding and the architect writes it.**

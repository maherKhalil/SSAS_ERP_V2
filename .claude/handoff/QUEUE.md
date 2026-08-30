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

⚠ **AN EMPTY QUEUE IS NOT A REASON TO STOP — IT IS A REASON TO PULL FROM `BACKLOG.md`.** Take the top item,
start it, and say which one in your next message. **No permission needed.** If the backlog is also empty,
pick the highest-value thing you can defend, state the reasoning in one line, and start.

**Never send a message whose only content is that you have no work.** If you are reporting an empty queue,
you should already be working and naming what.

**Refill:** the architect refills at TWO remaining. ⚠ **That rule has now failed ten times, and the reason
is structural: it fires only when the architect is looking.** A reactive refill always leaves a gap between
the coder finishing and the architect noticing — and the earlier wording covered *"drops to two with no
refill"*, which is the case that does not happen, while saying nothing about **zero**, which is the case
that does. **`BACKLOG.md` is the part that depends on nobody being awake.**

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
| 118 | **Move `ConstructorKeyedEntityModelTests` (both tests) to `Architecture.Tests`.** T-253 unblocked it by sharing `CutoverTenantModel`. `public void`, builds a composed EF model, context uses `"Server=unused;Database=model-only"` — **a deliberately unusable connection string, so it never opens a connection.** Same conditions as 106: both baselines in one commit, plants that compile. | **open — ruled** | BOARD 2026-08-30 |
| 119 | **Turn the timing tell into a guard, because the class REGENERATES.** 114 found 8 → 1 and **the one was not in the Aug-27 corpus at all** — it arrived with the 42 tests added since. **Assert no Integration test completes under a threshold without a stated exemption**, one reason per entry, the shape `RoutelessContractDocuments` uses. ⚠ **Set the threshold from the measured cliff — nothing between 10 ms and 2.4 s — not from a round number, and record in the file that the cliff is why.** Refusable by construction: the next database-free test written into Integration reddens it on arrival. | **open — ruled** | BOARD 2026-08-30 |
| 112 | **Fix the memory sampler's absolute path (relative, as T-251 established), then a BOUNDED extension.** `powershell.exe -File "$ROOT/scripts/sample-mem.ps1"` is the one real site — **it refuses to start rather than relocating, because PowerShell validates `-File` while `dotnet --results-directory` creates whatever it is given.** Then **enumerate `Process.Start` / `ProcessStartInfo` sites in `src/` and `tests/`** and report whether any receives a path assembled from a POSIX-style string. **Expect zero; report the count and stop — fix nothing unless something turns up.** | **open — ruled** | BOARD 2026-08-30 |
| 113 | **A code fact needed before a document can land.** PR **#171** (open since 2026-08-27, branch **384 commits behind**) adds five FP-015 files and two task files absent from `ClaudeBranch`. **Read `docs/17-features/FP-015-self-service/authorization-model.md` on `agent/T-072-spec-and-authorization-model` and report where it disagrees with `src/` today** — permission names, how a permission reaches a user, anything about grants. Known from code: `Attendance.Records.ViewOwn`, `Attendance.Leave.ViewOwn`, `Payroll.Payslips.ViewOwn` exist, two are wired to live routes, **none appears in any seeded role.** ⚠ **Report discrepancies only — do not edit the document, that is the architect's.** | **open** | BOARD 2026-08-30 |
| 114 | **Re-run the timing analysis on a CURRENT Integration TRX.** The run analysed in 104 is **2026-08-27, before the suite went 43.9 → 24.2 minutes** — so the eight misfiled tests were the population of a **stale corpus** and anything added since was never examined. **Re-derive on current data; if it is still eight, say so and the class is closed.** | **open** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78, 109 (**#358 — shared `TestSupport.CutoverModel`; the six C6 checks now run in every gate**), 110 (**#359 — TWO reasons, not one gap: empty scaffolds counting an accurate zero, and rows a mechanism postdating the only qualifying run by two hours could not have written**), 111 (**one real site, and it REFUSES TO START rather than relocating**), 106 (**#355 — two moved, six blocked on a shared type**), 107 (**#356 — the TRX escape REPRODUCED and fixed**), 108 (**#357 pending — guard built; the arm deliberately NOT added, and the architect overruled itself**), 105 (**no handler disagrees with the generic arms — 14 translations across 12 handlers, every reachable one resolving to 409; one LATENT gap found**), 103 + 104 (**#354 — and the timing tell found ZERO false greens in 806 tests; what it found instead was eight structural assertions misfiled into Integration**), 102 (**and TWO OF THE FOUR FLAGGED GUARDS NEEDED NOTHING — an exact-list assertion and a fixed type array are anti-vacuous by construction**), 97 + 99 (**#353 — EF DOES log it: one Error entry, exception attached, category `Microsoft.EntityFrameworkCore.Update` and NOT the predicted `Database.Command`; closes as a comment, no remedy**), 100 (**`correlationId` is populated in production — the empty value was the Attendance test host omitting `UseCorrelationId`; the architect's concern INVERTED, and cost nothing because it was reported unverified**), 101 (**#352 — the false green closed, two rules converted to reflection, five plants recorded IN THE FILE**), 95 (**⚠ demonstrated a LIVE FALSE GREEN: `PersistenceArchitectureTests` passes all nine when its file walk finds nothing; 18 text-matching guards, 12 assert an absence, and ALL TWELVE have no recorded plant**), 98 (**#351 — GL under the amended DEC-DEP-0027**), 94 (**212 clauses, 16 unreasoned, groups unchanged in SHAPE — and the architect's published 291 was worse than the original 207**), 93 (**#349 — no route registered with a non-literal pattern**), 96 (**#350 — 409 for Attendance and Payroll; GL STOPPED against a recorded decision**), 97 (**measured: no logger on either unit of work, and `Error(Code, Message)` cannot carry the `SqlException` that holds the index name**), 92 (**measured: a duplicate key returns 500 in three modules; and the architect's premise was FALSE — the unit of work does discriminate 2601/2627 from a deadlock, so Group B is harmless**), 91 (**#348 — 13 teardowns reasoned, tests-side unreasoned 13 to 0, and a REFUSABLE guard replacing the vacuous one the architect specified; it found a genuinely empty body whose reason sat on a `#pragma` line**), 90 (**#347 — the auth-path defect: both overloads log the cause, the caller's answer is unchanged, and the test asserts BOTH halves because either alone is satisfied by the wrong fix; the other nine group-C catches were sound and now say so**), 89 (**31 literal-matching instruments, only ONE exposed — the rest are structurally immune because C# forbids composing a type name in a type position**), 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
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

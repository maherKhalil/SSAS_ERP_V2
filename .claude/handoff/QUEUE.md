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
## Session ended 2026-08-30 — where to pick up

**The owner signed out. Both windows stopped cleanly.** ⚠ **This section records COMMITTED state only** — a
working-tree observation decays within one commit and has already misled this loop once today.

**Item 129 was in progress** — `Company.SelectionRequired` gaining a distinct code while keeping its 400.
The coder was asked to commit whatever it held onto its branch before stopping, so **look for an
`agent/T-268-*` branch and check whether it is pushed** before assuming the work is lost or duplicating it.
**130 is a measurement with no edits, so nothing there is half-done.**

**Both rulings are inline in the table below and neither needs re-deriving.**

**What remains after 129 and 130 is owner-gated, and that is an enumeration result rather than an empty
queue:** five ERP decisions blocking 41 capability rows, and three HIS placement decisions. `BACKLOG.md`
is empty of open B-items **deliberately** — the loop spent 2026-08-30 on instrument hardening because that
is where the defects were, and inventing another sweep to avoid saying *"this needs the owner"* would be
the failure this board has recorded under several other names.

**Also open, unchanged, and not urgent:** the 13 local-only `codex/*` branches on a PUBLIC repository, and
`agent/T-072-spec-and-authorization-model`, whose content was landed directly on 2026-08-30 and whose PR
was closed with an explanation — **the branch is left in place on purpose.**


| # | item | status | detail |
|---|---|---|---|
| 129 | **`Company.SelectionRequired` gets a distinct code — not a distinct status.** It is a **precondition**, not a correction: every other candidate says *fix your input*, this says *you are not in a state where this input means anything*, **and a client that cannot tell it from a bad field name cannot offer the company picker.** Reaches four modules — the widest single win on the list. ⚠ **400 stays: it IS a client error, and the actionable difference is carried by the CODE. The status is the category; the code is the instruction.** | **open — ruled** | BOARD 2026-08-30 |
| 130 | **Measure the two designs for Class A's 19 field-attribution codes; do not implement either yet.** **(a) 19 distinct wire codes** — works within the current shape, **adds 22% to an 86-code vocabulary**, each a permanent contract entry. **(b) a `field` extension on the problem document** — RFC 7807 allows it, we already pass `code`/`correlationId`/`resourceKey`, and **it serves Classes A, B and E uniformly (an array covers B's pair)**. ⚠ **(b) is NOT what Principle 18 refused: that rejected a free-text DETAIL field because prose absorbs the pressure to mint codes. A structured field IDENTIFIER is machine-readable and does not compete — the code says which RULE, the field says which INPUT.** **Can the domain `Error` carry a field identifier to the mapper at all, and what does it cost?** Report both costs. | **open — ruled** | BOARD 2026-08-30 |
| 118 | **Move `ConstructorKeyedEntityModelTests` (both tests) to `Architecture.Tests`.** T-253 unblocked it by sharing `CutoverTenantModel`. `public void`, builds a composed EF model, context uses `"Server=unused;Database=model-only"` — **a deliberately unusable connection string, so it never opens a connection.** Same conditions as 106: both baselines in one commit, plants that compile. | **open — ruled** | BOARD 2026-08-30 |
| 119 | **Turn the timing tell into a guard, because the class REGENERATES.** 114 found 8 → 1 and **the one was not in the Aug-27 corpus at all** — it arrived with the 42 tests added since. **Assert no Integration test completes under a threshold without a stated exemption**, one reason per entry, the shape `RoutelessContractDocuments` uses. ⚠ **Set the threshold from the measured cliff — nothing between 10 ms and 2.4 s — not from a round number, and record in the file that the cliff is why.** Refusable by construction: the next database-free test written into Integration reddens it on arrival. | **open — ruled** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78, 127 (**PR 372 — load-bearing controls renamed for what they hold up**), 128 (**28 of 129 are candidates, in five classes**), 126 (**the remaining matcher controls — and the count went 25 → 11 → 5, every step down from READING**), 125 (**PR 369 — route precedence pinned for the three literal/parameter pairs**), 124 (**PR 368 — the eight matcher controls; the FIRST one failed on its first run and found a hole where the canonical JWT type should have been**), 121 (**pagination split at both layers — and it surfaced a FOURTH condition: an export ceiling refused with the pagination code**), 123 (**PR 366 — `detail` shipped, and a second fail-closed class was found by an existing test**), 122 (**the domain message NEVER reaches a caller — no `detail` in any of 40 problem-document call sites**), 120 (**#363 — six structural guards floored, 34 tests, every guard planted twice; and probing the seventh caught the PROBE lying**), 115 (**all five FP-015 docs re-verified: 4, 3, 0, 0, 1 — every discrepancy an absence claim**), 118 + 119 (**#362 — the model-only tests moved, and the timing tell is now a guard set at the measured cliff**), 112 (**#360 — sampler path; the Process.Start sweep found nothing**), 113 (**FP-015 authorization-model: four of eight sections overtaken, all absence claims**), 114 (**8 → 1 on a current corpus — and the one is NEW, so the class regenerates**), 116 (**catch census closes at 11, all Group A**), 109 (**#358 — shared `TestSupport.CutoverModel`; the six C6 checks now run in every gate**), 110 (**#359 — TWO reasons, not one gap: empty scaffolds counting an accurate zero, and rows a mechanism postdating the only qualifying run by two hours could not have written**), 111 (**one real site, and it REFUSES TO START rather than relocating**), 106 (**#355 — two moved, six blocked on a shared type**), 107 (**#356 — the TRX escape REPRODUCED and fixed**), 108 (**#357 pending — guard built; the arm deliberately NOT added, and the architect overruled itself**), 105 (**no handler disagrees with the generic arms — 14 translations across 12 handlers, every reachable one resolving to 409; one LATENT gap found**), 103 + 104 (**#354 — and the timing tell found ZERO false greens in 806 tests; what it found instead was eight structural assertions misfiled into Integration**), 102 (**and TWO OF THE FOUR FLAGGED GUARDS NEEDED NOTHING — an exact-list assertion and a fixed type array are anti-vacuous by construction**), 97 + 99 (**#353 — EF DOES log it: one Error entry, exception attached, category `Microsoft.EntityFrameworkCore.Update` and NOT the predicted `Database.Command`; closes as a comment, no remedy**), 100 (**`correlationId` is populated in production — the empty value was the Attendance test host omitting `UseCorrelationId`; the architect's concern INVERTED, and cost nothing because it was reported unverified**), 101 (**#352 — the false green closed, two rules converted to reflection, five plants recorded IN THE FILE**), 95 (**⚠ demonstrated a LIVE FALSE GREEN: `PersistenceArchitectureTests` passes all nine when its file walk finds nothing; 18 text-matching guards, 12 assert an absence, and ALL TWELVE have no recorded plant**), 98 (**#351 — GL under the amended DEC-DEP-0027**), 94 (**212 clauses, 16 unreasoned, groups unchanged in SHAPE — and the architect's published 291 was worse than the original 207**), 93 (**#349 — no route registered with a non-literal pattern**), 96 (**#350 — 409 for Attendance and Payroll; GL STOPPED against a recorded decision**), 97 (**measured: no logger on either unit of work, and `Error(Code, Message)` cannot carry the `SqlException` that holds the index name**), 92 (**measured: a duplicate key returns 500 in three modules; and the architect's premise was FALSE — the unit of work does discriminate 2601/2627 from a deadlock, so Group B is harmless**), 91 (**#348 — 13 teardowns reasoned, tests-side unreasoned 13 to 0, and a REFUSABLE guard replacing the vacuous one the architect specified; it found a genuinely empty body whose reason sat on a `#pragma` line**), 90 (**#347 — the auth-path defect: both overloads log the cause, the caller's answer is unchanged, and the test asserts BOTH halves because either alone is satisfied by the wrong fix; the other nine group-C catches were sound and now say so**), 89 (**31 literal-matching instruments, only ONE exposed — the rest are structurally immune because C# forbids composing a type name in a type position**), 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
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

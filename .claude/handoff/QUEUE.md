# The work queue — authoritative

**This file is the queue. Messages are notifications about it, not the queue itself.**

⚠ **Read this before going idle, and read it whenever a message from the architect seems to be missing.**
Roughly half the architect's inbound messages are lost, and the coder's outbound reports are not always
received. **A silence is a lost message, not a decision.** This file is in the repository, so neither
window can lose it.

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
| 94 | **Re-run the catch census with the corrected matcher and produce COUNTS, not floors.** The published figures in Principle 13 are lower bounds: 291 clauses (132 `src`, 159 `tests`) against a first pass of 207. **Re-derive discarding / reasoned / unreasoned on the corrected matcher**, and say whether Groups A and B changed shape or only size. Numbers go to the architect; the principles document is corrected by the architect, not by you. | **open** | BOARD 2026-08-30 |
| 95 | **Enumerate every text-scanning guard in `tests/` and establish which have been planted against a real instance.** Five defects were found in such guards across two sittings — a `when` filter's property pattern read as a body, a nested-parenthesis capture 25% low, a `#pragma` between declaration and brace, a CRLF-blind literal matching a file boundary, and measuring on a normalised buffer. **Report the list and which are unplanted before fixing anything.** Where a compiler-enforced or structural check could replace a text scan, say so. | **open** | BOARD 2026-08-30 |
| 98 | **GL's unique-conflict arm, under an AMENDED `DEC-DEP-0027`.** Add the arm, move `Persistence.UniqueConstraint` off GL's `KnownUnmapped`, and **rewrite the comment block to read as amended** — cite T-165, keep its six-index argument (still true, still why no message may name an index), and record what changed: the generic code names no index, so it satisfies T-165's own rule that *a handler which cannot tell which index it hit must not name one*. **Per-handler translations stay primary; the arm is a floor under what no caller classified.** | **open — ruled** | BOARD 2026-08-30 |
| 99 | **Does EF Core's `Database.Command` logger actually emit `CommandError` for a unique violation?** Needs a real database and the Integration suite — **measure it, do not reason from framework defaults.** *"Nothing is logged"* and *"the framework logs it and we add nothing"* are different products and only one needs a fix. If it does not log, the remedy is a logger at the unit of work **while the `SqlException` is still in hand** — the only place the index name exists before `Error(Code, Message)` discards it. | **open** | BOARD 2026-08-30 |
| 100 | **Is `correlationId` empty outside the test host?** A measured problem body carried `"correlationId":""`. **If it is empty in production, an EF log entry cannot be tied to the request that caused it** — which makes 99's "the framework already logs it" answer far weaker than it sounds. **Coupled to 99; do not close 99 without this.** | **open** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78, 93 (**#349 — no route registered with a non-literal pattern**), 96 (**#350 — 409 for Attendance and Payroll; GL STOPPED against a recorded decision**), 97 (**measured: no logger on either unit of work, and `Error(Code, Message)` cannot carry the `SqlException` that holds the index name**), 92 (**measured: a duplicate key returns 500 in three modules; and the architect's premise was FALSE — the unit of work does discriminate 2601/2627 from a deadlock, so Group B is harmless**), 91 (**#348 — 13 teardowns reasoned, tests-side unreasoned 13 to 0, and a REFUSABLE guard replacing the vacuous one the architect specified; it found a genuinely empty body whose reason sat on a `#pragma` line**), 90 (**#347 — the auth-path defect: both overloads log the cause, the caller's answer is unchanged, and the test asserts BOTH halves because either alone is satisfied by the wrong fix; the other nine group-C catches were sound and now say so**), 89 (**31 literal-matching instruments, only ONE exposed — the rest are structurally immune because C# forbids composing a type name in a type position**), 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
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

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
| 93 | **Close the composed-pattern class in `RouteConstraintArchitectureTests`.** It matches `.MapGet("literal")` and would not see `MapGet(Prefix + "/{id:guid}")`. **Assert that no route is registered with a non-literal pattern** — zero instances today, so this forecloses the class at its cheapest. **Its anti-vacuity floor does not cover this**: 151 literal routes clear a floor of 120 while one composed route hides completely. Principle 12 already claims this rule is *enforced*, so the claim must be true. | **open — ruled** | BOARD 2026-08-30 |
| 94 | **Re-run the catch census with the corrected matcher and produce COUNTS, not floors.** The published figures in Principle 13 are lower bounds: 291 clauses (132 `src`, 159 `tests`) against a first pass of 207. **Re-derive discarding / reasoned / unreasoned on the corrected matcher**, and say whether Groups A and B changed shape or only size. Numbers go to the architect; the principles document is corrected by the architect, not by you. | **open** | BOARD 2026-08-30 |
| 95 | **Enumerate every text-scanning guard in `tests/` and establish which have been planted against a real instance.** Five defects were found in such guards across two sittings — a `when` filter's property pattern read as a body, a nested-parenthesis capture 25% low, a `#pragma` between declaration and brace, a CRLF-blind literal matching a file boundary, and measuring on a normalised buffer. **Report the list and which are unplanted before fixing anything.** Where a compiler-enforced or structural check could replace a text scan, say so. | **open** | BOARD 2026-08-30 |
| 96 | **A duplicate key returns 500 in Attendance, GL and Payroll — make it 409.** Three of ten module mappers have no arm for `Persistence.UniqueConstraint`, so it falls through to `WriteFailure` = `new(500, "request.failed")`. **Add the generic arm to the three, KEEP every per-handler translation** — those name which constraint and whether a retry can converge, which the generic arm cannot, and a 409 on a path where the violation means a broken invariant tells the caller to retry something that can never succeed. **The guard asserting every module mapper handles the code is red for 3 of 10 today, so it lands in the same commit as the fix.** Do not narrow the 31; the remedy does not depend on the number. | **open — ruled GO** | BOARD 2026-08-30 |
| 97 | **Does a unique violation log anything today?** If the generic arm from 96 fires on a path nobody anticipated, the client gets a tidy 409 and **the operator learns nothing** — the `AccessTokenIssuer` shape one layer up. The `SqlException` is in hand at the unit of work and its message carries the index name; the error code reaching the mapper probably does not. **Report what exists before proposing anything.** | **open** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78, 92 (**measured: a duplicate key returns 500 in three modules; and the architect's premise was FALSE — the unit of work does discriminate 2601/2627 from a deadlock, so Group B is harmless**), 91 (**#348 — 13 teardowns reasoned, tests-side unreasoned 13 to 0, and a REFUSABLE guard replacing the vacuous one the architect specified; it found a genuinely empty body whose reason sat on a `#pragma` line**), 90 (**#347 — the auth-path defect: both overloads log the cause, the caller's answer is unchanged, and the test asserts BOTH halves because either alone is satisfied by the wrong fix; the other nine group-C catches were sound and now say so**), 89 (**31 literal-matching instruments, only ONE exposed — the rest are structurally immune because C# forbids composing a type name in a type position**), 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
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

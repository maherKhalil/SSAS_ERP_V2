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

| # | item | status | detail |
|---|---|---|---|
| 89 | **Which other instruments are blind to composed identifiers?** Enumerate guards and inventory tests matching identifiers as LITERALS; for each ask what BUILDS those identifiers instead of spelling them. **Report each instrument's error direction before proposing fixes** — over-reporting is noise, under-reporting is a false green. | **open** | BOARD 2026-08-30 |
| 90 | **`AccessTokenIssuer:61` and `:110` — a real defect, not a documentation gap.** Missing key, bad algorithm and oversized token collapse to one observable in the auth path, in a file with no logger. **Log the cause server-side; leave the caller's `AccessTokenIssuanceUnavailable` unchanged** — opaque outward, diagnosable inward. Then judge the remaining nine group-C catches individually, and comment `CompromisedPasswordOptionsValidator` (it refuses startup; the generic message protects the dataset path). | **open** | BOARD 2026-08-30 |
| 91 | **Reasons on the 13 test-teardown discards**, then count **bare `catch` with an empty body in `src/`** — a purely syntactic set needing no comment classifier. **If it reaches zero, add a guard asserting zero.** If not, report what is left and why. **No guard requiring a comment on every discarding catch** — that classifier was wrong twice in an hour in both directions. | **open** | BOARD 2026-08-30 |
| 92 | **What does the API return today for a duplicate key violation?** Group B discards the inner SQL error, so a unique violation and a deadlock arrive identically. **Measure the boundary before documenting it as acceptable:** a duplicate surfacing as a generic failure rather than a conflict is a product defect, not a comment. | **open** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78, 79 (docs — architect's, #341), 80 (#342/#343), 81 (**dissolved**), 82
(**cancelled — already measured**), 83 (#344), 84 (**overturned 80's premise**), 85 (**#345 — 71 constraints
removed, product-wide total 0, architecture test planted**), 86 (**#346 — 3 real gaps called, 2 allowlisted
with the segment check; the tightening caught a false green in its own implementation**), 87 (**the ruling
survived falsification and is pinned by a test**), 88 (**census: 207 catch clauses, 94 discard, 40
unreasoned; two instrument defects found and corrected before the number was trusted**).

**Group A of the 88 census — the ~13 where the exception type IS the reason — is ruled LEAVE.** No comments
there; **the single collective statement is the architect's to write.**

**Division of labour, set by the owner 2026-08-30:** the coder does **coding and testing only**; planning
and documentation are the architect's. **A docs-shaped item is pushed back, not done.** When something
found while coding belongs in a document, **report the finding and the architect writes it.**

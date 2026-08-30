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
| 86 | **Uncalled routes: 3 real, 2 instrument artefacts.** Write calls for the 3; allowlist the 2 — **entry names the test method AND the action segment, guard asserts that method's source contains it** (naming the method alone catches a rename and misses a rot). No redundant literal calls to satisfy the instrument. | **in flight — ruled** | BOARD 2026-08-30 |
| 88 | **Enumerate the swallowed-exception class across `src/` and `tests/`.** Two passes have fixed a swallow and missed its neighbour. Report the count and the list; rule with the architect on which need reasons. Stop fixing instances. | **open** | BOARD 2026-08-30 |
| 89 | **Which other instruments are blind to composed identifiers?** `ApiContractRowGuardTests` over-reported because a route segment came from `[InlineData]` rather than being spelled out. **Enumerate the guards and inventory tests that match identifiers as LITERALS, and for each ask what in this codebase builds those identifiers instead of writing them.** Report the list and the direction of each instrument's error before proposing fixes — an instrument that over-reports is noise, one that under-reports is a false green. | **open** | BOARD 2026-08-30 |

**Closed 2026-08-30:** 77, 78 (memory index and descriptions), 79 (docs — architect's, #341), 80 (twelve
routes, #342/#343), 81 (**dissolved — nothing unowned to build**), 82 (**cancelled — already measured, board
row 1095**), 83 (bare neighbour catch, #344), 84 (**overturned 80's premise: 71 constrained, 25
unconstrained**), 85 (**#345 — 71 constraints removed, product-wide total now 0, architecture test
planted, sibling gate clean with an empty allowlist as a measured claim**), 87 (**the 400s carry a problem
document naming the parameter — the ruling survived falsification and is now pinned by a test**).

**Queue is at three and the architect is enumerating for more rather than inventing them.** If it reaches
two with no refill, say so and start the highest-value thing you can defend.

**Division of labour, set by the owner 2026-08-30:** the coder does **coding and testing only**; planning
and documentation are the architect's. **A docs-shaped item is pushed back, not done.** When something
found while coding belongs in a document, **report the finding and the architect writes it.**

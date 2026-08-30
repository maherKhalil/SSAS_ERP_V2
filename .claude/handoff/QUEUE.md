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
| 85 | **Remove route constraints across the five remaining modules — 400 everywhere.** Gate per module first: any sibling parameter route differing only by constraint keeps its constraint and gets an allowlist entry with a stated reason. Architecture test lands last and cannot be a blanket ban. | **open — ruled** | BOARD 2026-08-30, `OWNER-DECISIONS.md` closing section |
| 86 | **The five remaining uncalled routes.** `ApiContractRowGuardTests` is at 5; write the calls and lower it deliberately. | **open** | BOARD 2026-08-30 |
| 87 | **Falsify the 85 ruling's justification.** Assert the 400s carry a problem document naming the offending parameter. If they are bare, the status change was cosmetic and that becomes the real work. | **open** | BOARD 2026-08-30 |
| 88 | **Enumerate the swallowed-exception class across `src/` and `tests/`.** Two passes have fixed a swallow and missed its neighbour. Report the count and the list; rule with the architect on which need reasons. Stop fixing instances. | **open** | BOARD 2026-08-30 |

**Closed today:** 77, 78 (memory index and descriptions), 79 (docs — architect's, #341), 80 (twelve routes,
#342/#343), 81 (**dissolved — nothing unowned to build**), 82 (**cancelled — already measured, board row
1095**), 83 (bare neighbour catch, #344), 84 (**overturned 80's premise: 71 constrained, 25 unconstrained**).

**Division of labour, set by the owner 2026-08-30:** the coder does **coding and testing only**; planning
and documentation are the architect's. **A docs-shaped item is pushed back, not done.** When something
found while coding belongs in a document, **report the finding and the architect writes it.**

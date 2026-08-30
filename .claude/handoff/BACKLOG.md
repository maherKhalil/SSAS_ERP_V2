# The backlog — pull from here whenever the queue is empty

⚠ **AN EMPTY QUEUE IS NOT A REASON TO STOP. TAKE THE TOP ITEM HERE, START IT, AND SAY WHICH ONE IN YOUR
NEXT MESSAGE.** No permission needed. Everything here is gated (`src/` + `tests/`) unless marked otherwise,
so `DEC-L-007` applies: green gate, merge immediately.

**If this file is also empty**, pick the highest-value thing you can defend from your own knowledge of the
tree, state the reasoning in one line, and start. **Never send a message whose only content is that you
have no work** — if you are reporting an empty queue, you should already be working and naming what.

**Offline:** read this from `C:/Users/User/Documents/SSAS_ERP_V2/SSAS_ERP_V2-board/.claude/handoff/` when
`git fetch` fails.

---

## Standing work, roughly in value order

| # | item | why it is real |
|---|---|---|
| ~~B1~~ **DONE 2026-08-30** | **Re-verify `api-contracts.md`, `data-model.md`, `domain-model.md`, `lifecycle-model.md` in `docs/17-features/FP-015-self-service/` against the tree.** Report discrepancies only; the documents are the architect's to edit. One at a time, reporting each. | Landed 2026-08-30 **explicitly marked as never re-checked**. The one document that WAS checked lost four of eight sections in three days, so the base rate is high. | **All five re-verified: 4, 3, 0, 0, 1 discrepancies. Every one an absence claim; no descriptive section wrong.** |
| ~~B2~~ **ALREADY DONE (item 116)** | **Group B's five persistence-discard comments must name the MECHANISM that makes them safe** — the `when (e.InnerException is SqlException { Number: 2601 or 2627 })` filter — not "acceptable information loss", which 92 proved false. Establish whether it landed; if not, write them. | The original instruction was amended after measurement and may never have been carried out. |
| ~~B3~~ **PROMOTED to queue item 120** | **Audit a guard BECAUSE it has never failed.** Principle 16's own trigger, applied to the guards not covered by 95's inventory — anything outside the 18 text-matching ones: reflection-based, type-based, assembly-based. **Has each been observed to fail for the reason it claims to test?** | 95 scoped itself to text scans. Principle 16 says the rule is not about text; it is about whether a check has ever been seen to fail. |
| ~~B4~~ **DONE 2026-08-30 — now Principle 18** | **The `Error(Code, Message)` record cannot carry structured detail.** Noted twice — 97 (the `SqlException` dies at the catch) and Group B. **Establish the cost: how many places would want a third field, and what do they do instead today?** Report; propose nothing. | Recorded as a limitation twice without anyone measuring how often it bites. |
| ~~B5~~ **DONE — 3 pairs found, promoted to queue item 125** | **Re-run the sibling-route ambiguity enumeration** across all modules now that 71 constraints are gone. 84's check was per-module at the time of removal. **Confirm the current tree still has zero method+shape groups with two patterns.** | The constraint removals landed module by module; nothing has re-checked the whole surface since. |
| ~~B6~~ **DONE — MEASURED, NOTHING TO DO: all five already redden on a dead filter** | **Were the guards fixed in 102 and 103 planted for a dead FILTER, or only a dead ROOT?** The post-filter insight arrived *after* most of those plants. `src` → `sources` empties a root; `Platform` → `PlatformX` empties a filter — **and only the second is the failure that a refactor actually causes.** Re-read each planted guard and establish which mutation its recorded evidence describes. **Report the split before fixing; where only a root plant exists, add the filter one.** | The rule that says a filter is the likelier silent failure was published after those files were planted, so nothing establishes they meet it. |
| ~~B7~~ **STATED — 20 files, ZERO floors; middle number being finished** | **Guards that enumerate something other than assemblies or files.** 95 covered text scans (18) and B3 covered assembly enumerations (21). **What enumerates DI registrations, configuration sections, route tables, or EF model metadata — and can any of those collapse to empty and still pass?** Enumerate the class first and report it; probe second. | Two inventories have each found a vacuum-prone majority in their own class. **Nothing has looked at the guards that enumerate neither files nor types.** |
| ~~B8~~ **DONE — all 25 pass, and that IS the finding: the question was too weak** | **Run the cheap test across every existing floor: is the floored quantity the one its failing assertion reads?** Principle 16's new form. **Five guards are confirmed correct; nothing establishes the rest are** — a floor on files enumerated rather than routes matched looks prudent and covers nothing. **Report the split before changing anything.** | The rule that makes a floor meaningful was published *after* most floors were written. |
| ~~B9~~ **DONE (PR 364)** | **Is the `.Map(Get\|Post\|…)` regex complete?** It is a filter, so **a route registered in a form it does not match is invisible to the constraint guard.** `MapMethods`, `MapFallback`, `MapGroup`, a bare `Map`, anything else ASP.NET offers. **Enumerate the registration forms actually used in `src/`, then say which the regex covers.** | Same blind-spot family as composed identifiers: the guard is exhaustive over the forms it knows about, and nothing has enumerated the forms. |

---
| ~~B10~~ **DONE — 1 genuine defect, made caller-visible by item 123** | **For the 21 multi-condition messages, does every raise site actually match its code's NAME?** The pagination code was raised for an **export ceiling** — a cap on total rows, not a page of anything — **and nobody noticed because the name made the mismatch invisible.** A caller told *"page size invalid"* for a bad ceiling goes looking at a parameter it never sent, **which is worse than an ambiguous message: it is a wrong one.** 21 codes, raise sites enumerated, **report mismatches only.** | The fourth condition behind `InvalidPagination` was only visible once the code was split. **The number of conditions hiding behind an ambiguous code is unknown until you split it** — so the same may be true of the other 20. |
| B11 | **Now that messages are caller-visible, which others name a specific domain while being raised from SHARED code?** `Persistence.*` is the family B10 found — an identity-flavoured message returned for every aggregate in the product. **The question is whether `BuildingBlocks` and other shared assemblies hold more.** Enumerate errors declared in shared code, check each message for module nouns, **report before changing wording.** | ⚠ **Item 123 raised the quality bar on all 344 messages retroactively, in one commit, without inspecting a single string.** They were written when nobody could read them. |

## How this file is maintained

**The architect adds items; the coder removes them by doing them and says so.** An item that turns out to
be already done, dissolved on measurement, or not worth doing is a **legitimate outcome** — report that and
strike it, exactly as 81 dissolved and 82 was cancelled. **A backlog that only grows is a wish list.**

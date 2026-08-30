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
| B2 | **Group B's five persistence-discard comments must name the MECHANISM that makes them safe** — the `when (e.InnerException is SqlException { Number: 2601 or 2627 })` filter — not "acceptable information loss", which 92 proved false. Establish whether it landed; if not, write them. | The original instruction was amended after measurement and may never have been carried out. |
| B3 | **Audit a guard BECAUSE it has never failed.** Principle 16's own trigger, applied to the guards not covered by 95's inventory — anything outside the 18 text-matching ones: reflection-based, type-based, assembly-based. **Has each been observed to fail for the reason it claims to test?** | 95 scoped itself to text scans. Principle 16 says the rule is not about text; it is about whether a check has ever been seen to fail. |
| B4 | **The `Error(Code, Message)` record cannot carry structured detail.** Noted twice — 97 (the `SqlException` dies at the catch) and Group B. **Establish the cost: how many places would want a third field, and what do they do instead today?** Report; propose nothing. | Recorded as a limitation twice without anyone measuring how often it bites. |
| B5 | **Re-run the sibling-route ambiguity enumeration** across all modules now that 71 constraints are gone. 84's check was per-module at the time of removal. **Confirm the current tree still has zero method+shape groups with two patterns.** | The constraint removals landed module by module; nothing has re-checked the whole surface since. |

---

## How this file is maintained

**The architect adds items; the coder removes them by doing them and says so.** An item that turns out to
be already done, dissolved on measurement, or not worth doing is a **legitimate outcome** — report that and
strike it, exactly as 81 dissolved and 82 was cancelled. **A backlog that only grows is a wish list.**

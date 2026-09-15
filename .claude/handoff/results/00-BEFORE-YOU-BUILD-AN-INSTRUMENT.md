# ⚠ BEFORE YOU BUILD AN INSTRUMENT: GREP THIS DIRECTORY

**On 2026-08-30 nine instruments were built over two days to re-derive a conclusion that was already
written, already corrected, and already reasoned through — in this directory.**

## What happened

The completeness audit's first axis found **67 contract rows documenting a route the application does not
serve**, decomposing to 41 owner-blocked / 15 deferred / 9 documentation errors / 2 real gaps. `BOARD.md`
and `NEXT-SESSION.md` carried those counts. An item was then opened to re-derive them, and concluded:

> *"The original derivation is not recorded. No file carries the members or the method."*

**That was false.** `T-201.md`, in this directory, 142 lines, carries all of it: the members, the method,
the per-package breakdown, and **two self-corrections**.

Three sources were searched before declaring the absence — FP traceability matrices, documented route
strings, `api-contracts.md` bullets. **`.claude/handoff/results/` was not one of them.**

⚠ **And a later item in the same session used this directory as EVIDENCE** — tracing sixteen undocumented
routes to T-072, T-073, T-110, T-112 and T-175 to prove they were tracked work. **The convention was
trusted as proof while never being searched for the thing that had been declared missing.**

## What the nine instruments cost, and what T-201 already said

| re-derived at length | already in T-201 |
|---|---|
| "9 documentation errors" | **corrected to 10**, with the tenth named: the row begins "**New.**" and ends `[BUILT as POST …/change-department]` |
| "the ten should not be edited" | *"That was wrong and is withdrawn"* — each row carries what was specified, what was built, and the deciding reference; rewriting deletes two of three |
| "the documents are right, my matcher was wrong" | *"**The documents are not wrong. The instrument was.**"* |
| "shape-matchers miss what people write in the margin" | *"an instrument that reads only the machine-readable columns will miss the corrections people wrote in the margin — and so will a reader who stops at the first word"* |
| "a residual of one, inside an unrecoverable measurement" | the residual is **zero**: the one unblocked member, `GET /api/platform/permissions`, shipped 2026-08-29 |

## The rules

1. ⚠ **Grep `.claude/handoff/results/` before measuring anything.** 139 files. It is cheaper than every
   instrument in this thread combined.
2. ⚠ **An absence claim names the population searched.** "Not recorded anywhere" is a claim about your
   search, not about the repository. State which directories you looked in, in the artefact.
3. **Read the artefact's own commentary first.** Declarations, test headers and result files routinely
   assess themselves more precisely than a scan will — including declaring their own guards vacuous.
4. **A convention you cite as evidence is a convention you must search.**

**Related:** `item-152-route-table.md` (what the instruments eventually produced), and `T-201.md` itself,
which is the thing to read.

# item 219 — the six rewrites, drafted and NOT applied

⚠⚠ **PUSHED BACK AS DOCUMENTATION, PER THE OWNER'S DIVISION OF LABOUR — the judgement is here, the edit is
the architect's.**

## Why this is not mine to apply

**The owner set it on 2026-08-30:** *the coder does **coding and testing only**; planning and documentation
are the architect's. A docs-shaped item is pushed back, not done. When something found while coding belongs
in a document, **report the finding and the architect writes it.***

**Rewriting acceptance-criteria headings is editing a specification document.** ⚠ **The ruling itself draws
the line one inch further in — *"the body is the criterion and must not change… that is amending a
specification under a formatting item, and it is not yours or mine to do that way"* — and the same reason
covers the heading: it is the criterion's title in the spec, not a code comment.**

**So the analysis is below, complete and quotable, and the file is untouched.** ⚠ **A peer cannot move an
owner-set boundary, and this is the shape where that matters.**

## The six, with before and after

| id | before | proposed | verdict after |
|---|---|---|---|
| `AC-IAM-0013` | *Permission assignment* | **Only catalog permissions are assignable, and only to an eligible role** | AGREES |
| `AC-TEN-0007` | *Suspension* | **Suspension ends authentication eligibility and blocks selection, new sessions and refresh** | AGREES |
| `AC-LOC-0001` | *Complete bilingual catalog* | **An Active resource is accepted in Production only with both `en` and `ar`** | ⚠ **NARROWER — unavoidably** |
| `AC-LOC-0007` | *Placeholder compatibility* | **Placeholder reorder is accepted; missing, unknown or malformed placeholders are rejected** | AGREES |
| `AC-LOC-0013` | *Restore default* | **Restore keeps one inactive row, nulls the value, appends `RestoredDefault`, and resolves the default** | AGREES |
| ⚠ `AC-LOC-0019` | *Cache coherence* | **Revalidation and eviction observe their bounds and never cross tenant or culture** | AGREES |

**Bodies are quoted below unchanged, so the architect can check each proposal against the criterion without
opening the file:**

- `AC-IAM-0013` — *"Only code-catalog permissions can be assigned to an eligible role."*
- `AC-TEN-0007` — *"Suspending an `Active` Tenant makes current authentication eligibility false and blocks
  subsequent tenant selection, new-session, and refresh…"*
- `AC-LOC-0001` — *"Production validation accepts an Active resource only with both `en` and `ar`;
  incomplete non-Production output is flagged, diagnoses culture…"*
- `AC-LOC-0007` — *"Exact parser/escaping/repetition/case/set rules accept reorder and reject every
  missing/unknown/malformed placeholder."*
- `AC-LOC-0013` — *"Restore retains one inactive row, null current value, appends RestoredDefault, and
  resolves current default."*
- `AC-LOC-0019` — *"Version revalidation/eviction observes 15s/30s/5m/60s bounds and never crosses
  Tenant/culture."*

## ⚠ Five of six become AGREES. One cannot, and saying so is the point

**`AC-LOC-0001`'s body carries TWO rules** — what Production accepts, *and* what non-Production output does
with an incomplete one. **A heading naming both is a sentence, not a title.** So the proposal states the
primary rule and is honestly **NARROWER-BUT-COMPATIBLE**.

⚠ **NARROWER is not a defect. It leads a reader to the right criterion and withholds a detail** — which is
what the other five NARROWER cases in item 217's sample already did, and none of them misled anyone.
**Only MISLEADING is a defect, and all six proposals eliminate that.**

**Projected effect on item 217's sample: 40/5/1 → 45/1/0.**

## ⚠⚠ AND A HYPOTHESIS OF MY OWN, TESTED AND WITHDRAWN

I expected FP-004's headings to be terse **because its bodies pack several rules into one criterion**, and
measured it: **semicolons per body — FP-003 0.73, FP-004 0.33, FP-006 0.09, FP-015 0.00.**

⚠ **FP-003 has TWICE the clause density of FP-004 and its headings agree.** **So density does not explain
it and the hypothesis is wrong.** What survives is item 217's measurement — **FP-004's headings are 2.4
words against FP-015's 6.5** — which is a fact about how the titles were written, not about what they
describe. **Style, not substance.**

## Scope
- **Six headings, from item 217's 46-criterion sample.** FP-004 has 64 criteria and the sample saw 11;
  **more terse headings almost certainly exist and were not enumerated.**
- **Each proposal was checked against its body**, and each verdict is my own re-judging under the same
  three-verdict scheme — **the same instrument that produced the original counts, so the before/after is
  comparable.**
- **No file under `docs/` was modified by this item.**

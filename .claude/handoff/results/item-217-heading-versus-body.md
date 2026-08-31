# item 217 — MISLEADING is rare: 1 in 46, and it is concentrated by STYLE

**Report only.** ⚠ **The good answer: heading-level scanning is broadly safe, and the record built on it
stands. But the risk is not evenly spread, and where it concentrates is predictable.**

## ⚠ FIRST: THE QUESTION ONLY APPLIES TO SIX PACKAGES

**A heading/body divergence requires a heading and a body.** Of the fourteen packages with criteria:

| form | packages | can diverge? |
|---|---|---|
| **heading** (`### AC-… — label`, then a paragraph) | FP-001, FP-003, FP-004, FP-005, FP-006, FP-015 | ⚠ **YES** |
| **bullet** (`- **AC-AUTH-0002:** One active membership is selected automatically.`) | FP-002, FP-007, FP-008 | **no — the label IS the text** |
| **table row** | FP-012, FP-013, FP-014 | **no** |
| other | FP-009, FP-011 | not examined |

**So the hazard is confined to six packages, 261 criteria** — and everything measured in FP-002, FP-012,
FP-013 and FP-014 is structurally immune to it.

## The sampling method, stated because it is not random

**Every 6th criterion from each of the six heading-form packages, in file order.** Not the first N — that
would sample each file's opening section, which is where specifications are most carefully written.
**46 criteria: FP-001 ×4, FP-003 ×16, FP-004 ×11, FP-005 ×4, FP-006 ×8, FP-015 ×3.** Proportional to size,
so FP-003 dominates as it should.

⚠ **A stride is still not random** — it cannot see clustering at a stride's period. It does spread across
each file, which is what the first-N method fails at.

## The verdicts

| verdict | count | share |
|---|---|---|
| **AGREES** | **40** | 87 % |
| **NARROWER-BUT-COMPATIBLE** | **5** | 11 % |
| ⚠ **MISLEADING** | **1** | **2 %** |

**MISLEADING is rare, and heading-level scanning is defensible.** ⚠ **Every reading in this record that
used a heading to identify a subject is very likely correct** — which is the answer the ruling hoped for
and it is worth stating plainly rather than hedging.

**The one:** `AC-LOC-0019` — heading *"Cache coherence"*, body *"version revalidation/eviction observes
15s/30s/5m/60s bounds and never crosses Tenant/culture."* Already caught in item 215; **the sample found no
second instance.**

**The five NARROWER cases** are headings naming a topic where the body states a specific rule —
`AC-IAM-0013` *"Permission assignment"* → *only code-catalog permissions can be assigned*;
`AC-TEN-0007` *"Suspension"* → what suspension does to current authentication. **A reader is led to the
right place and would not mistake the subject; they would simply not know the rule.**

## ⚠⚠ AND THE DIVERGENCES CONCENTRATE, MEASURABLY

**Four of the six non-AGREES are FP-004's**, including the only MISLEADING. That is not chance — it is
**style**, and it is measurable:

| package | avg heading | words | divergences in sample |
|---|---|---|---|
| ⚠ **FP-004** | **20.1 chars** | **2.4** | ⚠ **4** (1 misleading, 3 narrower) |
| FP-001 | 22.2 | 3.0 | 1 |
| FP-005 | 27.3 | 3.7 | 0 |
| FP-006 | 32.0 | 4.3 | 0 |
| FP-003 | 33.4 | 4.2 | 1 |
| **FP-015** | **44.4** | **6.5** | **0** |

⚠ **Heading length predicts reliability.** FP-004 writes **labels** — *"Cache coherence"*, *"Restore
default"*, *"Placeholder compatibility"* — over dense multi-clause bodies. FP-015 writes **claims** — *"A
mapped identity holding the self permission reads its own records"* — which cannot diverge from a body
because it already IS one.

**So the finding is about the SPECIFICATIONS, not anyone's method, and it names the fixable thing: a
heading that states a claim rather than a topic cannot mislead.**

## What this licenses, and what it does not

**Licensed:** using a heading to identify a criterion's subject when scanning, in five of the six
heading-form packages.

⚠ **NOT licensed:** using a heading to establish a MAPPING to a test. **The one miss in 46 was exactly
that use**, and it produced a citation that would have been confidently wrong. **Item 215's rule stands
unchanged: a mapping is confirmed against a body.** A 2 % error rate is fine for orientation and
unacceptable for a claim that persists in code.

## Scope
- **46 of 261 heading-form criteria — an 18 % sample.** At a 1-in-46 rate, the true count of MISLEADING
  headings across all 261 could plausibly be anywhere from 0 to ~15; **this establishes that it is rare,
  not how many there are.**
- **Judged from the heading and the FIRST body line.** A body whose second paragraph changes the subject
  would read as AGREES here.
- FP-009 and FP-011 use a fourth layout and were not sampled.

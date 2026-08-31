# item 221 — half a citation, half a test, and the wording decided which

**Gated work.** One citation, one new test, plant verified. **TASK gate green, 0 warnings.**
⚠ **`AC-EMP-0015` is now fully pinned — all three clauses.**

## ⚠ THE WORDING FIRST: *RESERVED* MEANS REFUSED AT CREATION

The ruling asked which of two readings applies — *refused at creation* or *merely still present on the
terminated row*. **The criterion's own sentence settles it:**

> *"Its employee number and national ID remain **reserved within the company**."*

**"Within the company" is a uniqueness scope, not a persistence claim.** The row still carrying its values
is `AC-EMP-0015`'s first clause (*remains retrievable*), already pinned in item 220. ⚠ **So *reserved*
means a second employee cannot take them — a refusal at creation, which is a different and stronger test.**

## ⚠⚠ AND LOOKING FIRST SPLIT THE CLAUSE IN TWO

**It names TWO identifiers, and they were in different states:**

| identifier | state | disposal |
|---|---|---|
| **employee number** | ⚠ **ALREADY PINNED** by `Termination_retains_the_employee_and_its_history` — it terminates `EMP-1000` and asserts re-creating that number **fails** | **CITED** |
| **national ID** | ⚠ **pinned by nothing** | **new test** |

**The number half was pinned before the clause was ever noticed**, with the intent written in its own
comment: *"The number stays reserved: a terminated employee still occupies it."* ⚠ **A test that already
knew what it was proving, with no citation to say so — which is the whole case for Principle 29, found
again.**

## Why the national-ID half genuinely needed a test

**A national-ID uniqueness test exists** and refuses `nid-1` against a live `NID-1`. **It uses two LIVE
employees**, so it proves the constraint holds between actives.

⚠ **It cannot see whether termination RELEASES the value** — and releasing it is the plausible
implementation mistake, because a terminated employee looks like a row that no longer needs its
identifiers. **The uniqueness test and the reservation clause are different claims, and only the first was
asserted.**

`A_terminated_employees_national_id_remains_reserved` creates `EMP-1100`/`NID-9`, terminates it, then
creates **a different employee number** with `nid-9`. ⚠ **The differing number is the point: it makes the
national ID the only thing that can refuse the create**, so a pass cannot come from the number constraint.

**Plant:** added `&& employee.Status != Terminated` to the production `NationalIdExistsAsync` — **the exact
mistake described above** — and the test reddens. Restored.

## `AC-EMP-0015` is now complete

| clause | pinned by |
|---|---|
| cannot be updated / activated / deactivated / transferred | three domain tests (B18 pass 01) |
| remains retrievable by id | `A_terminated_employee_remains_retrievable_by_id` (item 220) |
| returnable by search | `AC-EMP-0016`'s own subject, not swept in |
| ⚠ **number reserved** | `Termination_retains_the_employee_and_its_history` — **cited here** |
| ⚠ **national ID reserved** | **new here** |

## Scope
- **The reservation is asserted within ONE company.** The criterion says *within the company*, and
  `AC-EMP-0006`'s sibling test already covers the same number being free in a different company —
  **so the cross-company direction is pinned elsewhere and not duplicated here.**
- **Case-normalisation is exercised** (`NID-9` reserved against `nid-9`), which is the same normalisation
  the number half relies on.
- FP-006 remains **10 of 47** cited: this item added no new ids, it completed one.

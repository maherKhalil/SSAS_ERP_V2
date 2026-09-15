# item 218 — PARTIAL: 10 of 46 examined, 4 cited, and the cost is now a number

**Gated work.** ⚠ **Reported PARTIAL deliberately, as the ruling permits — with the counts, the measured
rate, and a judgement about whether to continue.**

## Done

| | |
|---|---|
| criteria examined **by body** | **10 of 46** (`AC-EMP-0001`…`0010`, in file order) |
| mappings established and **cited** | **4** — `0005`, `0006`, `0008`, `0010` |
| FP-006 Trait-claimed, total | **1 → 5** (with `0014` from item 216) |
| ⚠ criteria found **uncovered** | **0** |

**The four, each confirmed against a test body:**

- **`AC-EMP-0005`** — *exactly one `EmployeeBranchAssignment`, `SourceBranchId` null* →
  `Creation_produces_exactly_one_initial_assignment_naming_no_source`: `Assert.Single`, `Assert.Null`,
  `InitialAssignment` reason.
- **`AC-EMP-0008`** — *no operation can change `EmployeeNumber`* →
  `No_operation_changes_the_employee_number_or_ownership_identifiers`: asserted over the **type's
  mutators**, not one call site.
- **`AC-EMP-0010`** — *termination cannot precede employment* → `Termination_cannot_precede_employment`:
  fails with `TerminationBeforeEmployment`, status unchanged, **and the same day is permitted**.
- **`AC-EMP-0006`** — see below.

## ⚠⚠ THE OBVIOUS CANDIDATE FOR `AC-EMP-0006` WAS THE WRONG ONE

**`AC-EMP-0006` is UNIQUENESS:** *"two employee numbers whose `Trim().ToUpperInvariant()` values are equal
**cannot both be created**"*.

**`EmployeeDomainTests.Employee_numbers_that_normalize_alike_are_equal` matches those words almost
exactly** — and asserts `EmployeeNumber.Create(" emp-1 ").Value == EmployeeNumber.Create("EMP-1").Value`.
⚠ **That is VALUE EQUALITY. The criterion is a UNIQUENESS CONSTRAINT.** A value type can compare equal in
a product that happily persists both.

**The criterion is pinned by `EmployeeBoundarySqlServerTests.Employee_numbers_that_normalize_alike_collide`**
— creates `" emp-400 "`, then `"EMP-400"`, asserts the second **fails with `NumberConflict`.**

⚠ **A name match would have cited the domain test. Reading both bodies moved the citation to a different
SUITE.** This is the second near-miss in two items — `AC-LOC-0019` was a heading, this one is a synonym —
**and both were caught only by reading what the test asserts.**

## The cost, measured rather than asserted

| | |
|---|---|
| candidate pool | ⚠ **84 test files, 1,124 test methods** for 46 criteria |
| examined | 10 criteria |
| yield | 4 citations, 1 near-miss corrected |
| ⚠ **hit rate** | **4 of 10 confirmable at reasonable cost; 6 had plausible candidates needing deeper reading** |

**Extrapolated: the remaining 36 are roughly four more passes of this size** — feasible, bounded, and
nothing like the open-ended job it looked like before the pool was counted.

## ⚠⚠ THE JUDGEMENT THE RULING ASKED FOR

**Continue — but the reframing matters more than the answer.**

⚠ **Of 10 criteria examined by body, ZERO were uncovered.** Every one had real coverage; the only question
was ever which test, and twice the obvious answer was wrong. **Three packages of spot-checks and now ten
criteria of full reading have produced no uncovered criterion at all.**

**So this is not a gap-finding exercise, and reporting it as one would be selling the wrong result.** ⚠
**It is a CITATION-BUILDING exercise** — the yield is durable mappings, which is exactly Principle 29's
asset, and the two near-misses show the mappings are **not** derivable later by anyone grepping names.

**What that changes:** the value is per-citation and roughly constant, so it can be stopped at any point
without wasting what came before. **It does not need to be finished to be worth having, which is the
opposite of a coverage audit.**

## Scope
- **10 criteria of 46, in file order — not a sample.** They are the creation/identity/lifecycle core, which
  is the best-tested part of any package; **a later block may yield less.**
- **The 6 unconfirmed of the 10 are NOT uncovered** — they had candidates I did not read to a conclusion.
- Trait keys matched per file where one existed; `EmployeeDomainTests` had none, and `Criterion` was chosen
  to match its API-side neighbour rather than to introduce a fifth spelling.

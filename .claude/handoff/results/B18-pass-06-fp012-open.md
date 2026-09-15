# B18 pass 06 — FP-012 opened: 31 read, 6 examined, 5 cited, and a stale count found

**TASK gate green, 0 warnings. Control: 137 cited across all packages, zero dangling.**

## ⚠ WHY FP-012 AND NOT FP-014

The ruling offered FP-014 as *"the cheapest start"* because `item-161` already measured it. **I picked
FP-012, and the reasoning is the substance of this pass:**

| | FP-012 | FP-014 |
|---|---|---|
| criteria | **31** | 54 |
| Trait-cited before this pass | **0** | **0** |
| format | ⚠ **table rows — structurally immune to heading/body divergence (item 217)** | table rows |
| prior measurement | none | `item-161`: 20 pinned, 11 unpinned, **19 not implemented, 4 subject-undefined** |

⚠ **FP-014's prior split is what makes it EXPENSIVE, not cheap: 23 of its 54 are CORRECTLY uncited, so
citing there means first establishing which 23 to skip** — from a measurement dated 2026-08-30, on a
package then described as *partly built*, **whose mapping item 210 already declined as class-granular.**
**FP-012 has no such bookkeeping and is small enough to finish.**

## The numbers

| | this pass |
|---|---|
| criteria **read** | **31 of 31** |
| **examined by body** (criterion + candidate test bodies) | **6** |
| **cited** | **5** — `0025`, `0028`, `0029`, `0030`, `0031` |
| ⚠ **partly pinned** | **1** (`0028`) |
| **unexamined** | **25** |
| found **uncovered** | **0** |

## ⚠⚠ THE FINDING: `AC-PAY-0029`'s COUNT IS STALE

> *"**All five payroll tables** appear in the E3 cutover manifest…"*

**The manifest's exact list carries SEVEN payroll tables** — `EmployeeCompensation`, `PayElement`,
`PayElementAssignment`, `PayrollPeriod`, `PayrollRun`, `PayrollRunDraftLine`, `PayrollRunLine` — and
`CutoverManifestArchitectureTests` **says so in its own comment**: *"SEVEN from Payroll (FP-012)"*.

⚠ **The property holds and the number does not.** The criterion is not violated — it is **out of date**, and
a reader auditing it against the manifest would find a mismatch and not know which side was wrong.

**Recorded in the citation and reported here. Not fixed: it is a specification edit, which is the
architect's** — the same boundary as item 219.

## Two supersets, named as such

- **`AC-PAY-0031`** bans a foreign key to a **Platform-database** table; `No_payroll_foreign_key_points_at_another_modules_table`
  bans one to **any other module's** table. **Strictly wider.**
- **`AC-PAY-0025`** names **Payroll and GL** specifically; `No_other_module_learns_about_payroll` asserts
  **no module at all** references Payroll.

**Both cited as supersets rather than shadowed by narrower copies that would assert less.**

## `AC-PAY-0028` is partly pinned

Two halves: *a read scope cannot be **supplied by the caller***, and *a request attempting to **widen its
own scope** is refused.* **The first is asserted structurally** — no public constructors, no factories
outside the assembly. ⚠ **The runtime refusal half is pinned by nothing found so far**, and the citation
says so.

## ⚠ The architecture suite's own controls are population floors

`Every_payroll_string_column_is_unicode` asserts **`strings.Length >= 12`** before checking them;
`Every_monetary_column_is_decimal_19_4` asserts **`>= 4`**; `No_payroll_foreign_key_points_at_another_modules_table`
asserts **`>= 8`**. ⚠ **Anti-vacuity built into the arrangement rather than into a separate control test** —
the same shape as FP-006's revocation trio, arrived at independently by a different author.

## Next

**25 criteria unexamined**, and they are the behavioural core — compensation history, run lifecycle,
approval gates, GL posting, payslips. **The architecture suite is now exhausted for this package**, so the
next pass moves to `Payroll.Tests` and `PayrollChainSqlServerTests`, **where the rate should fall to
something like FP-006's early passes rather than its late ones.** ⚠ **Stated as an expectation, not a
forecast — the last one cost three refutations.**

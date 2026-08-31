# B18 pass 05 — FP-006 complete: 47 examined, 36 cited

**TASK gate green, 0 warnings. ⚠ Every FP-006 criterion has now been examined by body.**

## The two numbers — and the third the amended B18 asks for

| | this pass | FP-006 final |
|---|---|---|
| criteria **examined by body** | **7** (`0041`–`0047`) | ⚠ **47 of 47** |
| **cited** | **6** | **36 of 47** |
| ⚠ **examined, unresolved** | **1** (`0044`) | **11** |
| ⚠ **unexamined** | **0** | ⚠ **0** |
| found **uncovered** | **0** | **0** |
| ⚠ **partly pinned** | **1** (`0047`) | **3** |

**Rate this pass: 6 of 7 = 0.86**, matching pass 04 and confirming it was not a fluke.

## ⚠⚠ ARCHITECTURE-SUITE-FIRST FOUND FOUR IN A SINGLE LISTING

`EmployeeArchitectureTests`' method list alone resolved **`0043`, `0045`, `0046`, `0047`** — before a
single body was opened. **Two are near-verbatim:**

| criterion | test |
|---|---|
| `0046` *"no numbering-sequence table, service, or configuration"* | **`No_employee_number_generator_exists`** |
| `0045` *"no department, position, or manager association"* | **`Employee_has_no_position_or_manager_and_exactly_one_department_property`** |

⚠ **The amended B18's first clause paid for itself immediately.** These seven were the criteria I had never
examined — and they were the *easiest* of the package, because structural criteria are pinned by tests that
must name their structure.

## ⚠ `AC-EMP-0047` IS PARTLY PINNED, AND THE CITATION SAYS SO

The criterion bans route, command, handler, permission **and** table for **four** things: *rehire, employee
documents, import, and export.*

**`No_rehire_operation_exists` asserts the REHIRE clause only.** ⚠ **Documents, import and export are
pinned by nothing here — recorded in the citation comment rather than implied by it.**

⚠ **And the other three are interesting rather than alarming:** FP-009 *is* employee import/export, and
FP-010 *was* employee documents before being closed. **So this criterion asserts FP-006's boundary against
packages that exist**, which is a scope question rather than a missing guard — **and not one I can settle
from inside FP-006.**

## The one unresolved

**`AC-EMP-0044`** — *"refusals originating in the branch or company write boundaries are surfaced as
generic scope denials; no response discloses table names, database…"*

⚠ **The nearest matches are `CompanyApplicationTests.Create_propagates_a_generic_write_failure` — a foreign
subject — and `EmployeeErrorWireContractTests`, which turns out to hold only position and import cases.**
**Left uncited.**

## FP-006 closed out

**47 examined. 36 cited. 11 examined-but-unresolved. 0 unexamined. 0 uncovered.**

⚠ **The eleven unresolved are not eleven gaps.** They are criteria whose pinning test I could not identify
with confidence — and across five passes and 47 criteria, **not one criterion was found to be genuinely
unasserted by the product.** The three specified-but-unasserted **halves** found along the way
(`0015`'s retrievability, `0017`'s structural bans, `0015`'s reservation) all came from criteria that were
otherwise well covered.

**`AC-EMP-0035` stays uncited deliberately** — nothing asserts branch-history **order**, only scope, and
the ruling agrees it should stay that way until something does.

## Scope
- **Six citations, six bodies read.** `0042`'s home is `BranchSessionArchitectureTests`, outside the HR
  suites entirely — ⚠ **found only because the architecture-first search is not scoped to a module.**
- **The final rate held at 0.86 across two consecutive passes**, both of which used architecture-first.
  **The three earlier passes averaged 0.57 without it.**

# B18 pass 19 — `DEP` groups G, H and J. All ten groups searched. SEARCH ONLY.

⚠ **No citations, no test file touched — the floor has not cleared (1675 → 1717 → 1671).**
⚠⚠ **AND EVERY IDENTIFIER BELOW WAS EXTRACTED WITH `grep -o` ON THE WHOLE TOKEN. NO `cut` TOUCHED THIS
PASS**, after four invented names in pass 18.

## Group G — employee membership: 5 of 6

| criterion | test |
|---|---|
| `AC-DEP-0033` | `A6b_Create_without_a_department_is_refused` |
| `AC-DEP-0035` | `The_department_cannot_be_changed_through_an_ordinary_employee_update` |
| `AC-DEP-0036` | `A6e_Change_department_succeeds_with_employee_update_authority` **+** `D8_A_stale_row_version_is_refused_and_appends_nothing` |
| ⚠ `AC-DEP-0037` | **both directions, both written**: `D11_A_branch_transfer_preserves_the_department_and_writes_no_department_history` **and** `D12_A_department_change_preserves_the_branch_and_writes_no_branch_history` |
| `AC-DEP-0038` | `D13_Termination_preserves_the_department_and_writes_no_department_history` |

**`AC-DEP-0039` (`OD-DEP-001`'s chosen strategy and its terminal state) — not searched.**

## Group H — authorization: 2 of 3

- ⚠ **`AC-DEP-0041`** — `Tenant_administration_alone_does_not_grant_the_department_read` **and**
  `Tenant_administration_alone_grants_no_department_write`. **The criterion says *any department
  operation*; read and write are both asserted.**
- ⚠⚠ **`AC-DEP-0042`** — **a three-way discrimination, all three already written**:
  `A6e_Change_department_succeeds_with_employee_update_authority` (holds `HR.Employees.Update` → works),
  `A6f_Change_department_without_the_update_permission_is_forbidden`,
  `A6g_Change_department_with_only_department_permissions_is_forbidden`. **The criterion's *NOT a
  department permission* half is `A6g` exactly.**

**`AC-DEP-0040` (each permission required by exactly the listed operations) — candidates exist in the
route/permission join, not settled. Left open.**

## Group J — concurrency: `AC-DEP-0048` covered

`A_stale_row_version_is_refused_on_every_family` — ⚠ **the criterion is *every department mutation*, and
that test's name is the same quantifier.** Supported by `A_stale_row_version_refuses_a_move`,
`D8_A_stale_row_version_is_refused_and_appends_nothing`, and the structural
`Every_department_mutation_requires_a_row_version`.

**`AC-DEP-0049` (a real cutover carries departments, managers, employees and branch history) — not
searched.**

## ⚠⚠⚠ AND A SHARPENING THAT NEARLY WENT THE OTHER WAY — `AC-DEP-0050`

**This pass surfaced `Rowversion_columns_are_excluded_from_the_copy_mapping`.** ⚠ **The name reads as
UNIVERSAL — "columns", "the copy mapping" — and I was one step from closing `AC-DEP-0050` with it.**

**Its body is `Assert.Single(plan.Value, table => table.EntityName == nameof(Company))`.** ⚠⚠ **ONE
ENTITY, HAND-NAMED, over the Platform-only model.**

**So the guard now stands at THREE INSTANCES, each hand-naming ONE entity:**

| test | entity | model |
|---|---|---|
| `Rowversion_columns_are_excluded_from_the_copy_mapping` | `Company` | Platform-only |
| `C6_7_The_employee_rowversion_is_excluded_from_the_copy_projection` | `Employee` **+** `EmployeeBranchAssignment` | composed tenant |
| — | ⚠⚠ **`Department`: NONE** | |

⚠⚠ **The finding is STRONGER, not weaker: the same guard has been written THREE TIMES, each time for
hand-named entities, and never once derived.** **Three occasions, three authors' hands, one omission.**

⚠ **And the name is the trap.** *`Rowversion_columns_are_excluded_from_the_copy_mapping`* would close
`AC-DEP-0050` on a name search. **Reading the body is what stopped it** — the same discipline that caught
`An_inactive_employee_may_still_change_department` in pass 17, in the opposite direction: **there a name
promised MORE than the body delivered; here a name promises a POPULATION the body does not have.**

## Where `DEP` stands: all ten groups searched

**4 cited. 33 citable and body-confirmed or name-confirmed. Ten criteria open or unsearched.**

| open | why |
|---|---|
| `AC-DEP-0002`, `0044`, `0050` | ⚠ **the three gaps — each exists for Employee and not Department** |
| `AC-DEP-0008` | ⚠ **company × read unasserted PRODUCT-WIDE** |
| `AC-DEP-0012`, `0016`, `0029` | partial — one clause each unpinned |
| `AC-DEP-0026`, `0030`, `0031`, `0039`, `0040`, `0046`, `0049` | not searched or not body-confirmed |

**The grouping and every recorded search survive this stop.**

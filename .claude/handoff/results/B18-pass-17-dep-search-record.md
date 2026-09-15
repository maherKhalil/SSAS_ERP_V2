# B18 pass 17 — `DEP` groups B, D and F searched. SEARCH ONLY: no citations applied.

⚠⚠ **NO TEST FILE WAS TOUCHED. The gate cannot run — free memory has been below the 2048 MB floor for
every reading taken (1745 → 1937 → 1775 → 1296).** **Citations wait for a green gate; the SEARCH does
not, and this file is the part that survives the block.**

**`DEP` remains 4 of 52 by the strict `[Trait]` count. This record names 13 more that a gated pass can
cite immediately, and 5 that cannot be cited as they stand.**

## Group B — create and identity: 4 of 4, all body-confirmed

| criterion | test | note |
|---|---|---|
| `AC-DEP-0001` | `A_valid_department_is_created_active_at_the_root` | asserts `Active`, `ParentDepartmentId` null, non-empty id — **verbatim** |
| `AC-DEP-0003` | `A_duplicate_normalized_code_is_refused_within_the_company` | ⚠ creates `"sales"` then `"SALES"` — **the arrangement discriminates on NORMALIZATION, which is the criterion's own word** |
| `AC-DEP-0004` | `The_same_code_is_free_in_another_company` | verbatim |
| `AC-DEP-0005` | `An_empty_code_is_refused` **+** `An_empty_name_is_refused` | ⚠ **two `[Theory]`s over `null`, `""`, `"   "` — all four of blank/whitespace × name/code** |

## Group D — hierarchy: 6 pinnable, 2 partial

| criterion | test |
|---|---|
| `AC-DEP-0010` | `A_department_may_be_created_beneath_a_parent` |
| `AC-DEP-0011` | `A_parent_from_another_company_is_refused` |
| `AC-DEP-0013` | `A_department_cannot_be_moved_beneath_its_own_grandchild` **+** `The_cycle_check_walks_an_arbitrarily_deep_chain` |
| `AC-DEP-0014` | `Moving_a_department_carries_its_subtree` |
| `AC-DEP-0015` | `An_inactive_parent_is_refused` |
| ⚠ `AC-DEP-0017` | `Two_concurrent_moves_cannot_jointly_create_a_cycle` — **and `Two_concurrent_legal_moves_both_succeed` is its CONTROL, already written** |

⚠ **`AC-DEP-0012` PARTIAL** — *"…and the database check constraint refuses it too."* `A_department_cannot_become_its_own_parent` (SQL) and `A_department_cannot_be_its_own_parent` (domain) cover the refusal. **Whether a CHECK CONSTRAINT is asserted separately is unsearched.**

⚠ **`AC-DEP-0016` PARTIAL** — *ancestors root-to-parent, descendants in a stated order*.
`Children_are_returned_for_one_level_only` reaches the descendant side only. **Recorded search: test names matching `ancestor|descend|hierarch|order` in the department SQL suite → three hits, none asserting ancestor ORDER.**

## Group F — lifecycle: 3 pinnable, 2 partial, 2 unsearched

| criterion | test |
|---|---|
| `AC-DEP-0025` | `A_valid_department_is_created_active_at_the_root` (shared with `0001`) |
| `AC-DEP-0027` | `Deactivation_is_refused_while_an_active_child_remains` |
| ⚠ `AC-DEP-0028` | `D2_Creating_an_employee_into_an_inactive_department_is_refused` **and** `A6c_Create_into_an_inactive_department_is_refused` — **two layers, SQL and API** |

⚠ **`AC-DEP-0029` PARTIAL, and the missing half is the interesting one.**
`D6_A_change_into_an_inactive_department_is_refused_and_appends_nothing` covers *into* — **with an
append-nothing control already present.** ⚠ **The criterion's second clause — *changing an employee OUT
of an inactive department is permitted* — is asserted by nothing found.** **`An_inactive_employee_may_
still_change_department` is a DIFFERENT claim: an inactive EMPLOYEE, not an inactive DEPARTMENT.**

**`AC-DEP-0026`, `0030`, `0031` — not yet searched.**

## ⚠ What this pass did NOT do, stated rather than implied

- **No citations.** ⚠ **A `[Trait]` is a test-file edit and every test-file edit tonight has gone through a
  green gate first. That is not a rule I will bend because a gate is inconvenient.**
- **Groups C, E, G, H, I, J remain named and unsearched** — company isolation, manager, employee
  membership, authorization, reads, concurrency/cutover.
- ⚠ **The two partials in D and the one in F are RECORDED SEARCHES, not conclusions**: each names what was
  grepped, so a later pass can falsify by re-executing rather than by re-reading.

## ⚠⚠ And one observation about the group boundaries themselves

**Group B and Group F share `A_valid_department_is_created_active_at_the_root`: it pins `AC-DEP-0001`
(*creating … produces an `Active` root*) and `AC-DEP-0025` (*a new department is `Active`*).**

⚠ **Two criteria in two different documented sections, one assertion.** **The grouping ordered the search
correctly and the SECTIONS did not — which is the argument for grouping by mechanism rather than by the
specification's own headings.** *The group orders the search; it never justifies a citation.*

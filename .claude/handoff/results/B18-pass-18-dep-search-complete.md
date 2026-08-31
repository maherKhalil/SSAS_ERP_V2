# B18 pass 18 — `DEP` groups C, E and I searched. SEARCH ONLY. Nine of ten groups now searched.

⚠⚠ **STILL NO CITATIONS AND NO TEST FILE TOUCHED.** Free memory across this pass's readings: **1719 →
1296 → 1775**, against a 2048 MB floor. **`DEP` remains 4 of 52 by the strict `[Trait]` count.**

**Passes 16–18 together name 26 criteria a gated pass can cite immediately, and 7 that cannot be cited
as they stand.**

## Group E — manager: 7 of 7

| criterion | test |
|---|---|
| `AC-DEP-0018` | `A_manager_can_be_assigned_replaced_and_cleared` |
| `AC-DEP-0019` | `A_manager_from_another_company_is_refused` |
| `AC-DEP-0020` | `A_terminated_employee_is_refused_as_a_manager` |
| ⚠ `AC-DEP-0021` | `A_terminated_sitting_manager_is_retained_but_never_reported_as_active` — **the criterion's two halves in one test name** |
| `AC-DEP-0022` | `A_manager_can_be_assigned_replaced_and_cleared` **+** `A_department_with_no_manager_reports_no_manager_at_all` |
| ⚠ `AC-DEP-0023 (OD)` | `A_manager_outside_the_callers_branch_scope_is_assigned_but_undisclosed` — **`OD-DEP-003` reading (i), which is what the criterion names** |
| ⚠ `AC-DEP-0024` | `Concurrent_manager_assignment_cannot_produce_two_rows` — **the criterion says *enforced by the primary key*, and a CONCURRENCY test is the only thing that can show a key doing the enforcing** |

## Group C — company isolation: 2 pinnable, 1 superset, ⚠ 1 GAP

| criterion | finding |
|---|---|
| `AC-DEP-0006` | `A_department_in_an_unauthorized_company_is_not_found` |
| `AC-DEP-0007` | `An_empty_authorized_company_set_is_refused_rather_than_unfiltered` |
| `AC-DEP-0009` | ⚠ **SUPERSET** — `CompanyOwnershipBoundarySqlServerTests.Deactivating_the_company_mid_session_refuses_the_next_write` covers **any** company-owned write, departments included |

⚠⚠ **`AC-DEP-0008` IS A GAP, AND IT IS TONIGHT'S SHAPE AGAIN.** The criterion is *revoking the caller's
company access mid-session refuses the next department **READ** without a restart.*

**Recorded search — `mid_session` test names, tree-wide, 9 hits:**
- **`CompanyOwnershipBoundary…`** — revocation and deactivation refuse the next **WRITE** ⚠ *not a read*
- **`EmployeeBoundary…`** — ⚠ **`Revoking_company_access_mid_session_refuses_the_next_employee_read`**
- **no department read equivalent anywhere**

⚠ **`DepartmentScopeResolverTests.The_company_authority_is_consulted_on_every_resolution` proves the
MECHANISM that makes the criterion true — the resolver re-asks live — but asserts nothing about the next
read being refused.** *The mechanism is not the claim.*

**So: the mid-session read guard exists for EMPLOYEE and not for DEPARTMENT. Fifth instance today of a
guard present for a sibling and absent here.**

## Group I — reads: `AC-DEP-0047` covered three ways

`A_department_member_count_includes_only_employees_in_scope`,
`An_empty_department_counts_zero_while_an_unscoped_count_would_not`,
`A_member_count_never_reaches_outside_the_company`.

⚠ **The middle one is the anti-vacuity control, already written: a zero that would be non-zero without
the scope.**

**`AC-DEP-0046` — candidates found (`An_employee_from_another_branch_of_the_same_company_may_manage`,
`A_manager_outside_the_callers_branch_scope_is_assigned_but_undisclosed`) but NOT body-confirmed. Left
open rather than claimed.**

## Where `DEP` stands after three search passes

| group | state |
|---|---|
| A structural | **4 cited** (`0032`, `0045`, `0051`, `0052`); `0043`, `0034` searched and citable |
| B create/identity | **4 citable**, body-confirmed |
| C company isolation | **2 citable + 1 superset**; ⚠ **`0008` GAP** |
| D hierarchy | **6 citable**; `0012`, `0016` partial |
| E manager | **7 citable** |
| F lifecycle | **3 citable**; ⚠ `0029` half-built; `0026`, `0030`, `0031` unsearched |
| I reads | **1 citable** (`0047`); `0046` open |
| ⚠ **G, H, J** | **named and UNSEARCHED** — employee membership, authorization, concurrency/cutover |

## ⚠ The four candidate gaps this sweep has produced, all one shape

**`AC-DEP-0002`** (no `CompanyId` assertion + behavioural clause), **`AC-DEP-0008`** (mid-session read),
**`AC-DEP-0044`** (predicate guard), **`AC-DEP-0050`** (RowVersion copy exclusion).

⚠⚠ **Every one exists for EMPLOYEE and not for DEPARTMENT.** **FP-007 inherited FP-006's criteria and not
its guards** — and `AC-DEP-0034`'s test is the counterexample that shows why: **it derives its population
from the composed model, so it covered Department without anybody touching it.**

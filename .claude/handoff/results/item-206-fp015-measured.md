# item 206 — FP-015 measured, and the claim understates the product twice over

**Report only. Nothing built, nothing changed.**

## Why FP-015 and not another package

**It is the only FALSE claim item 205 found**, and it is an **absence claim** — *"specification only, no
code"* — which Principle 20 says rots fastest and which cannot be true of a package whose in-scope surface
is already routed and tested. **No other package's claim survived as a candidate**: three make no
implementation claim, five are approval-only, and the five checkable ones are true.

## The denominator

**14 acceptance criteria**, `AC-SS-0001`…`AC-SS-0014`, written as **headings**. Counted by distinct
identifier, which is format-blind (item 205).

## What exists

| | |
|---|---|
| routes | `/me/records`, `/me/leave-requests` (Attendance), `/me/payslips` (Payroll) — **all dated 2026-08-28 by blame** |
| permissions | `ViewOwnRecords`, `ViewOwnLeave`, `ViewOwnPayslips`, `PayrollOwnPayslipsList` |
| dedicated suites | `AttendanceSelfServiceTests`, `PayrollSelfServiceTests` — **14 test methods** |
| a shared rule | ⚠ `SelfServiceContractRule` — *"a self-service route may bind FILTERS, never a SUBJECT"*, generalised in T-089 across two modules |

## The measurement, by criterion

| criteria | what pins them | bucket |
|---|---|---|
| **0001, 0003** — reads own records / payslips | `The_records_self_permission_alone_reads_the_callers_own_records`, `…own_leave`, `…own_payslips` | **PINNED** |
| **0002, 0004** — another's records/payslip **unreachable, not forbidden** | ⚠ **structural** — the route binds no subject; `The_self_route_contract_names_no_employee_on_any_surface` + `SelfServiceContractRule` | **PINNED, and by construction** |
| **0005** — admin permission alone grants no self access | `The_administrative_permission_alone_is_refused_on_a_self_route` **in both modules** | **PINNED** |
| **0006** — self alone grants no admin access | `Without_the_self_permission_the_route_is_refused`; cross-route: `The_records_self_permission_does_not_open_the_leave_route` | **PINNED** |
| **0007** — route carries no employee identifier | ⚠ the shared contract rule exists **for this criterion** | **PINNED, strongest of the fourteen** |
| **0008** — unmapped identity gets an ordinary refusal | `An_unlinked_caller_is_refused_with_the_named_condition`, `A_link_naming_an_employee_with_no_placement_is_refused_identically` | **PINNED** |
| **0009** — it is not an exception | `An_unmapped_caller_is_told_so_rather_than_receiving_a_server_error` | **PINNED** |
| **0010, 0011, 0012** — termination | ⚠ **NOT ESTABLISHED — see below** | **unmeasured** |
| **0013, 0014** — module entitlement / expired subscription | ⚠ **NOT ESTABLISHED — see below** | **unmeasured** |

**Nine of fourteen are pinned by a named test I read.**

## ⚠⚠ THE OTHER FIVE ARE **UNMEASURED**, NOT UNBUILT — AND THE DISTINCTION IS THE RULING'S FIRST RULE

*"Not implemented" is an absence claim, so grade it by the method that established it.*

**My method was NAME MATCHING across test files, and it cannot establish absence.** Candidate coverage
exists and I did not verify it criterion by criterion: `EmployeeReadScopeArchitectureTests` and
`ModuleErrorMappingArchitectureTests` join termination to self-service concepts;
`AttendanceRouteInventoryTests`, `AttendanceArchitectureTests` and `AttendanceApiTestHost` join entitlement
to them. **Reporting those five as "not covered" would be asserting an absence on an instrument that cannot
see one.**

## ⚠ And the ruling's second rule: THE PRODUCT HAS BOTH SUBJECTS

**Neither gap is "a criterion whose subject the product does not have."** Termination is named in **32** HR
source files; entitlement in **47** source files. **Both are criteria an engineer could satisfy today** —
which is a different and much cheaper bucket than FP-014's criteria that had no subject at all.

## ⚠⚠ WHAT THE CLAIM GETS WRONG, TWICE

1. **"No code"** — three routes, four permissions, two API surfaces.
2. ⚠ **The subtler one: the claim implies the package's boundary is UNDEFENDED. It is the opposite.**
   `SelfServiceContractRule` enforces `AC-SS-0007` **as a generalised architectural rule across modules**,
   and it was *strengthened* in T-089 when a second self route made the original form untenable. **A
   package described as "specification only" has a live invariant that has already survived one revision.**

## Scope
- **Criterion-to-test mapping is by reading test names and the contract rule's own comment**, not by
  executing each criterion against the product. A named test can fail to assert what its name claims —
  this loop has found that exact shape before.
- **The five unmeasured criteria are not claimed to be uncovered.** Settling them means reading three
  architecture suites against five criteria, which is a measurement I did not run.
- Nothing was changed. **The disposal — correcting FP-015's front matter — is documentation.**

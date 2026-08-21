---
document_id: FP-008-TS
title: HR Position — Test Scenarios
status: Approved for Implementation
version: 1.0
---

# FP-008 — Test Scenarios

Layer conventions follow FP-006 and FP-007: **A** = architecture guard (`Architecture.Tests`), **U** =
domain/application (`HR.Tests`), **P** = API (`API.Tests`), **S** = real SQL (`Integration.Tests`). Rules
whose enforcement depends on the database — uniqueness, check constraints, concurrency, cutover — are proven
at **S**, because a unit test over a fake proves the handler and not the rule.

## Create and identity

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0001 | S | Create a position; tenant and company are stamped from context and it is `Active` | AC-POS-0001, AC-POS-0023 |
| TS-POS-0002 | S | **Negative control.** A create request carrying another tenant's `TenantId` produces a position in the caller's tenant, not the named one | AC-POS-0002 |
| TS-POS-0003 | S | Duplicate normalized code in the same company is refused | AC-POS-0003 |
| TS-POS-0004 | S | Two concurrent creates of the same code: exactly one succeeds, and the loser's refusal originates in the unique index | AC-POS-0003 |
| TS-POS-0005 | S | Codes differing only by case or surrounding whitespace collide | AC-POS-0003 |
| TS-POS-0006 | S | The same code succeeds in a second company | AC-POS-0004 |
| TS-POS-0007 | U | Blank title and blank code are refused by the value objects | AC-POS-0005 |
| TS-POS-0008 | A | `tenant.Positions` has no column referencing `tenant.Employees`, read from the **composed model** | AC-POS-0006 |

## Company isolation

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0009 | P | Reading a position in an unauthorized company returns `404`, not `403` | AC-POS-0007 |
| TS-POS-0010 | U | A resolver producing an empty company set refuses rather than returning an unfiltered scope | AC-POS-0008 |
| TS-POS-0011 | S | Revoking company access mid-session refuses the next position read | AC-POS-0009 |
| TS-POS-0012 | S | Deactivating the company mid-session refuses the next position write | AC-POS-0010 |
| TS-POS-0013 | S | **Negative control.** A position may not reference a grade from company B | AC-POS-0011 |

## Grades

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0014 | S | Create a grade with code, name and rank; read all three back | AC-POS-0012 |
| TS-POS-0015 | S | Duplicate `RankOrder` in one company and ladder is refused by the unique index | AC-POS-0013 |
| TS-POS-0016 | U | **The lexical trap, made executable.** Grades `G9` (rank 90) and `G10` (rank 100) order correctly by rank and *incorrectly* by code. Without `RankOrder` this test cannot pass | AC-POS-0014 |
| TS-POS-0017 | S | A position references an active grade and reads back with it | AC-POS-0015 |
| TS-POS-0018 | S | A position may not reference an inactive grade | AC-POS-0016 |
| TS-POS-0019 | A | **The composed model contains no `SalaryGrade → JobGrade` foreign key.** The reference is one-directional, and a mutual one would restore the cycle `DEC-POS-0002` prevents | AC-POS-0017 |

## Salary amounts

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0020 | S | A three-decimal amount round-trips through `decimal(19,4)` without loss | AC-POS-0018 |
| TS-POS-0021 | S | Amounts out of order are refused **by the check constraint**, written directly in SQL, bypassing the application entirely | AC-POS-0019 |
| TS-POS-0022 | U | Amounts out of order are refused by the aggregate before any I/O | AC-POS-0019 |
| TS-POS-0023 | S | A negative amount is refused | AC-POS-0020 |
| TS-POS-0024 | S | **A salary grade with no amounts is created and read back with nulls.** Nullability is the residual choice `DEC-POS-0016` flags as open, not a backfill accommodation — the `OD-POS-001` ruling seeds nothing | AC-POS-0021 |
| TS-POS-0025 | A | `tenant.SalaryGrades` has no currency column in the composed model | AC-POS-0022 |
| TS-POS-0026 | P | Sending `currencyCode` on a salary-grade write is rejected as an unknown property; reading one echoes the Company's `BaseCurrencyCode` | AC-POS-0022 |

## Lifecycle

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0027 | S | Deactivate and reactivate; both transitions stamp `StatusChangedUtc`/`By` and both require `HR.Positions.Deactivate` | AC-POS-0024 |
| TS-POS-0028 | S | An inactive position refuses a new employee on creation | AC-POS-0025 |
| TS-POS-0029 | S | An inactive position refuses an employee position change into it | AC-POS-0025 |
| TS-POS-0030 | S | An inactive position remains readable and appears in lists, marked inactive | AC-POS-0026 |
| TS-POS-0031 | A | **Token guard.** No `MapDelete` appears anywhere in the HR surface, and no repository method deletes a position or grade | AC-POS-0027 |
| TS-POS-0032 | S | *(`OD-POS-005`, assignment reading)* Deactivating a position with incumbents **succeeds**, and every incumbent still holds it afterwards | AC-POS-0028 |
| TS-POS-0033 | S | A grade with active positions may not be deactivated, and no cascade occurs | AC-POS-0029 |

## Employee assignment

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0034 | S | Creating an employee requires a same-company active position | AC-POS-0030 |
| TS-POS-0035 | P | `positionId` on the ordinary profile update is rejected as an unknown property | AC-POS-0031 |
| TS-POS-0036 | S | A position change updates the column and appends exactly one history record **in one transaction**; forcing a failure after the column write leaves neither | AC-POS-0032 |
| TS-POS-0037 | S | Exactly one record in an employee's history has a null `SourcePositionId`, and it is the earliest | AC-POS-0033 |
| TS-POS-0038 | U | Changing to the position already held is refused, and no history record is produced | AC-POS-0034 |
| TS-POS-0039 | S | **The three-dimension independence proof.** Branch transfer, department change and position change each leave the other two untouched — all three directions | AC-POS-0035 |
| TS-POS-0040 | S | Terminating an employee leaves position and history intact | AC-POS-0036 |
| TS-POS-0041 | A | `EmployeePositionAssignment` implements `IAppendOnlyEntity`, has no `RowVersion`, and no update or delete path reaches it | AC-POS-0037 |
| TS-POS-0042 | S | **The happy path, on an empty database.** The migration creates all four tables and adds `Employees.PositionId` **`NOT NULL` in one step** — no nullable phase, no later `ALTER COLUMN`, no backfill `UPDATE` | AC-POS-0038 |
| TS-POS-0043 | S | **`DEC-POS-0026`: the precondition fires.** Against a database holding one Employee row, the migration **fails and writes nothing** — asserted by checking that none of the four tables exists and `Employees` has no `PositionId` afterwards. The message names the database, the count, and the decision | AC-POS-0039, AC-POS-0040 |
| TS-POS-0067 | S | **No synthetic residue.** After a successful migration, no Position, JobGrade or SalaryGrade row exists at all, and `EmployeePositionAssignments` is empty. The FP-007 `UNASSIGNED` cohort has no counterpart here | AC-POS-0065 |

## Cutover and ownership classification

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0044 | S | **The cycle proof, written before the schema.** A constructed model carrying a direct `Position → Employee` foreign key makes `TenantCutoverCopyPlan.Build` fail with `CutoverCopyOrderUndecidable`. This is the executable form of `RISK-POS-001` | AC-POS-0055 |
| TS-POS-0045 | S | The exact manifest assertion names every new tenant-owned entity; adding one without updating it fails loudly | AC-POS-0052 |
| TS-POS-0046 | S | The derived copy order places grades before Position, Position before Employee, and Employee before every assignment table | AC-POS-0053 |
| TS-POS-0047 | S | The restore-verification drop succeeds against a real database carrying the new foreign keys, with the drop list the copy order read backwards | AC-POS-0054 |
| TS-POS-0048 | A | `Position` does not implement `IBranchOwnedEntity`, and no table this package adds has a `BranchId` — read from the **composed model, not migration files** | AC-POS-0056 |
| TS-POS-0049 | A | `EmployeePositionAssignment` is tenant- and company-owned and not branch-owned | AC-POS-0057 |

## Authorization

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0050 | P | Each position permission authorizes exactly its own operations; `View` alone cannot create, update or deactivate | AC-POS-0041 |
| TS-POS-0051 | P | **Permission bleed, both directions.** `HR.Positions.*` does not reach the employee change-position route; `HR.Employees.*` does not reach any position route | AC-POS-0042 |
| TS-POS-0052 | P | **Every permission this package names is defined in the composed catalog and can be granted to a role.** The FP-006P regression — names that authorize nothing — must not recur | AC-POS-0043 |
| TS-POS-0053 | P | `Platform.Tenant.Administer` widens scope and grants none of these | AC-POS-0044 |
| TS-POS-0054 | P | `HR.Positions.View` alone does not read salary amounts | AC-POS-0045 |
| TS-POS-0055 | A | `PositionReadScope` has a private constructor, an internal resolver-only factory, no branch scope, and no read overload omits it | AC-POS-0046 |

## Persistence and concurrency

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0056 | S | Every mutation refuses a stale `RowVersion` with `409` | AC-POS-0047 |
| TS-POS-0057 | S | **Two concurrent position changes for one employee.** Exactly one succeeds; the loser fails with a concurrency conflict; no second current position exists afterwards | AC-POS-0048, AC-POS-0049 |
| TS-POS-0058 | S | Assigning to a position being concurrently deactivated never yields an employee holding an inactive position | AC-POS-0050 |
| TS-POS-0059 | S | **Query-plan proof.** Each scoped read is served by an index whose leading keys are tenant then company | AC-POS-0051 |

## Transport

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0060 | P | **The exact route inventory matches in both the module harness and the Host composition.** FP-007 shipped an unreachable route because the harness did not mirror the Host, and the absence test that should have caught it was passing vacuously | AC-POS-0058 |
| TS-POS-0061 | P | Position errors answer in `position.*` / `job_grade.*` / `salary_grade.*` and never in `employee.*` or `department.*` | AC-POS-0059 |
| TS-POS-0062 | A | `HR.API` references no Platform assembly | AC-POS-0060 |
| TS-POS-0063 | S | **A grade rank-order collision answers the rank problem code, not the code problem code.** The Department precedent had one unique index per operation and does not cover this | AC-POS-0061 |

## Scope boundaries

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-POS-0064 | A | `tenant.Employees` has no salary, wage, rate or compensation column, and no such value is persisted anywhere in this package | AC-POS-0062 |
| TS-POS-0065 | A | Neither aggregate gained a manager or reporting reference | AC-POS-0063 |
| TS-POS-0066 | S | Two employees may hold the same position simultaneously; no constraint prevents it | AC-POS-0064 |

## A note on what these scenarios are worth

**`TS-POS-0043` is the one to write first**, and it is the only test here that defends a *ruling* rather than a
design. `OD-POS-001` licensed `NOT NULL`-from-day-one on an operational fact — that no production tenant holds
Employee rows. If that fact is ever false in some database, the migration must say so rather than fail on a
constraint or, worse, succeed against a database it should never have touched. A green suite without
`TS-POS-0043` proves the design works where the premise holds, which is exactly the case that was never in
doubt.

Three more exist because of specific defects this codebase has already produced, and they come next:

- **TS-POS-0044** — the cutover cycle. FP-007 found the equivalent in design review, and the executable
  assertion (`TS-DEP-0044`) is what keeps it found. One convenience column reintroduces it.
- **TS-POS-0060** — the route inventory across **both** harnesses. FP-007 shipped a route that was reachable
  in tests and unreachable in production, and the guard that should have caught it was *vacuously* green.
- **TS-POS-0052** — permissions defined, not merely named. FP-006 shipped constants that authorized nothing,
  so every employee endpoint refused every caller.

A green suite that omits these three would prove the same things FP-006's and FP-007's green suites proved
immediately before each of those defects shipped.

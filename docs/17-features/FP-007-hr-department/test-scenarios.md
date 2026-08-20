---
document_id: FP-007-TS
title: HR Department — Test Scenarios
status: Draft — Owner Decision Required
version: 0.1
---

# FP-007 — Test Scenarios

Layer conventions follow FP-006: **A** = architecture guard (`Architecture.Tests`), **U** = domain/application
(`HR.Tests`), **P** = API (`API.Tests`), **S** = real SQL (`Integration.Tests`). Rules whose enforcement
depends on the database — uniqueness, check constraints, concurrency, cutover — are proven at **S**, because a
unit test over a fake proves the handler and not the rule.

## Create and identity

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0001 | S | Create a root department; tenant and company are stamped from context, and it is `Active` | AC-DEP-0001, AC-DEP-0025 |
| TS-DEP-0002 | S | **Negative control.** A create request carrying another tenant's `TenantId` produces a department in the caller's tenant, not the named one | AC-DEP-0002 |
| TS-DEP-0003 | S | Duplicate normalized code in the same company is refused | AC-DEP-0003 |
| TS-DEP-0004 | S | Two concurrent creates of the same code: exactly one succeeds, and the loser's refusal originates in the unique index | AC-DEP-0003 |
| TS-DEP-0005 | S | Codes differing only by case or surrounding whitespace collide | AC-DEP-0003 |
| TS-DEP-0006 | S | The same code succeeds in a second company | AC-DEP-0004 |
| TS-DEP-0007 | U | Blank name and blank code are refused by the value objects | AC-DEP-0005 |

## Company isolation

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0008 | P | Reading a department in an unauthorized company returns `404`, not `403` | AC-DEP-0006 |
| TS-DEP-0009 | U | A resolver producing an empty company set refuses rather than returning an unfiltered scope | AC-DEP-0007 |
| TS-DEP-0010 | S | Revoking company access mid-session refuses the next department read | AC-DEP-0008 |
| TS-DEP-0011 | S | Deactivating the company mid-session refuses the next department write | AC-DEP-0009 |
| TS-DEP-0012 | S | A department may not be parented under one from another company | AC-DEP-0011 |

## Hierarchy — the core proofs

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0025 | U | Self-parent refused in the aggregate, with no I/O | AC-DEP-0012 |
| TS-DEP-0026 | S | Self-parent refused by `CK_Departments_ParentIsNotSelf` when written directly in SQL, bypassing the application entirely | AC-DEP-0012 |
| TS-DEP-0027 | S | Build `A → B → C` by creating each with a parent, then read the hierarchy: ancestors of `C` are `[A, B]` and descendants of `A` are `{B, C}` | AC-DEP-0010, AC-DEP-0016 |
| TS-DEP-0028 | S | **`A → B → C`, then move `A` beneath `C` — refused.** The named `BR-HR-0008` proof | AC-DEP-0013 |
| TS-DEP-0029 | S | Move `B` beneath new root `D`; `C` moves with it and `C`'s parent is untouched | AC-DEP-0014 |
| TS-DEP-0030 | S | Move beneath an `Inactive` parent is refused | AC-DEP-0015 |
| TS-DEP-0031 | S | **Concurrency.** Two sessions simultaneously attempt `move A under B` and `move B under A`. Exactly one succeeds; the surviving hierarchy is acyclic, verified by walking it afterwards. Without the serialization in `DEC-DEP-0006` this test fails | AC-DEP-0017 |
| TS-DEP-0032 | U | `ChangeParent` cannot be invoked without repository-produced ancestry evidence — asserted by the type signature, so a handler that skipped the check would not compile | AC-DEP-0013 |

## Manager

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0033 | S | Assign a same-company employee as manager; read it back | AC-DEP-0018 |
| TS-DEP-0034 | S | **Negative control.** Manager from Company B refused for a department in Company A | AC-DEP-0019 |
| TS-DEP-0035 | S | Terminated employee refused as a new manager | AC-DEP-0020 |
| TS-DEP-0036 | S | Terminating a sitting manager leaves the assignment in place and surfaces `isTerminated` | AC-DEP-0021 |
| TS-DEP-0037 | S | Clearing a manager leaves the department readable with a null manager | AC-DEP-0022 |
| TS-DEP-0038 | S | A second manager assignment replaces the first; the primary key makes two rows impossible | AC-DEP-0024 |
| TS-DEP-0039 | S | **(OD-DEP-003 reading (i))** An employee cannot manage their own department, in both directions: assign-manager-who-is-member, and move-member-into-department-they-manage | AC-DEP-0023 |
| TS-DEP-0040 | S | The manager's branch transfer changes nothing about the department | AC-DEP-0021 |

## Lifecycle

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0013 | S | Deactivate with assigned employees: succeeds, employees keep their department | AC-DEP-0026 |
| TS-DEP-0014 | S | Deactivate with active children: refused | AC-DEP-0027 |
| TS-DEP-0015 | S | **Employee creation into an inactive department is refused** — the `BR-HR-0009` proof | AC-DEP-0028 |
| TS-DEP-0016 | S | Employee change *into* an inactive department refused; change *out of* one succeeds | AC-DEP-0029 |
| TS-DEP-0017 | P | An inactive department still appears in list results, marked inactive | AC-DEP-0030 |
| TS-DEP-0018 | S | Reactivate, then successfully create an employee into it | AC-DEP-0031 |
| TS-DEP-0019 | A | **No delete path exists** — no route, command, handler, or repository method deletes a department | AC-DEP-0032 |

## Employee membership

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0020 | S | **(OD)** Employee create without a department refused; with a foreign-company department refused; with a valid one succeeds | AC-DEP-0033 |
| TS-DEP-0021 | P | **Negative control.** `PUT /api/hr/employees/{id}` carrying `departmentId` is **rejected**, not silently ignored — the difference matters, because silent ignoring looks like success | AC-DEP-0035 |
| TS-DEP-0022 | S | `POST .../department` changes the department; a stale `RowVersion` is refused with `409` | AC-DEP-0036 |
| TS-DEP-0023 | S | Branch transfer leaves department unchanged; department change leaves branch unchanged | AC-DEP-0037 |
| TS-DEP-0024 | S | Terminating an employee leaves the department intact | AC-DEP-0038 |
| TS-DEP-0041 | S | **(OD)** The chosen `OD-DEP-001` end state holds: no employee has a null department after migration under A or D; under B or C the named follow-up migration exists | AC-DEP-0039 |

## Authorization — negative controls throughout

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0042 | P | Each operation refuses a caller holding every HR permission **except** the one it requires | AC-DEP-0040 |
| TS-DEP-0043 | P | **A Tenant Administrator with no HR permission is refused every department operation** | AC-DEP-0041 |
| TS-DEP-0045 | P | Changing an employee's department with `HR.Departments.Update` but not `HR.Employees.Update` is refused | AC-DEP-0042 |
| TS-DEP-0046 | U | Every constant in `HrPermissionNames` appears in `HrPermissionCatalogContributor` — the FP-006P regression guard, extended | AC-DEP-0043 |

## Reads

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0047 | A | Every department query composes explicit tenant and company predicates | AC-DEP-0044 |
| TS-DEP-0048 | A | `DepartmentReadScope` has a private constructor and an `internal` factory with exactly one call site | AC-DEP-0045 |
| TS-DEP-0049 | S | **(OD-DEP-005)** A branch-A-only caller sees a department whose members are all in branch B | AC-DEP-0046 |
| TS-DEP-0050 | S | Two callers with different branch authority read different `employeeCount` values for the same department | AC-DEP-0047 |

## Concurrency and cutover

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0051 | S | Every mutation refuses a stale `RowVersion` | AC-DEP-0048 |
| TS-DEP-0044 | U | **The copy plan builds with the FP-007 model**, and a constructed model in which `Department` holds a direct `ManagerEmployeeId` foreign key **fails with `CutoverCopyOrderUndecidable`** — so `DEC-DEP-0022`'s reason is executable and cannot be reverted by accident | AC-DEP-0034 |
| TS-DEP-0052 | U | Derived order: Department after Company, Employee after Department, `DepartmentManagers` after Employee | AC-DEP-0034 |
| TS-DEP-0053 | S | A real cutover carries departments, managers, employees and branch history; counts agree on both sides | AC-DEP-0049 |
| TS-DEP-0054 | U | Department's `RowVersion` is excluded from the copy projection | AC-DEP-0050 |

## Ownership classification

| ID | Layer | Scenario | AC |
|---|---|---|---|
| TS-DEP-0055 | A | `Department` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity` and **not** `IBranchOwnedEntity` | AC-DEP-0051 |
| TS-DEP-0056 | A | No `BranchId` property or column exists on `Department`, asserted **from the composed EF model** rather than by reading a migration file — the lesson from TEST-001, where enumerating files gave a guard that was green and blind | AC-DEP-0052 |
| TS-DEP-0057 | A | `DepartmentReadScope` carries no branch dimension | AC-DEP-0051 |

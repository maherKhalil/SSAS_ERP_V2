---
document_id: FP-007-TS
title: HR Department — Test Scenarios
status: Approved for Implementation
version: 1.0
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

---

## As-built test inventory (2026-08-21)

The scenarios above were written as design intent. What follows names the tests that shipped, so a reader
can go from a rule to the assertion that defends it.

### Domain (`HR.Tests`, 126)

| Area | Tests |
|---|---|
| Department aggregate | `DepartmentDomainTests` — code and name validation, parent refusals, lifecycle transitions |
| Associations | `DepartmentAssociationDomainTests` — manager identity keyed by department; history initial-record and change shapes, reached through the aggregate |
| Employee ↔ Department | `EmployeeDepartmentDomainTests` — creation writes column and first history row together; change appends exactly one; **branch transfer preserves department**; **termination preserves department**; deterministic ordering under colliding timestamps |
| Scope resolution | `DepartmentScopeResolverTests` — permission before scope; no branch dependency at all |

### Application, real SQL (`Integration.Tests`)

| Area | Tests |
|---|---|
| Department operations | `DepartmentApplicationSqlServerTests` (32) — including the concurrent-cycle proof, manager privacy, and the fixture/production translation-parity guard |
| Employee ↔ Department | `EmployeeBoundarySqlServerTests` `D1`–`D15` — create/change validation, stale rowversion, permission, **concurrency (one winner, one new history row)**, branch and termination regressions, and the branch-scope filter proof |
| Migration and backfill | `EmployeeDepartmentMigrationSqlServerTests` (12) — one `UNASSIGNED` per affected company, none for unaffected ones, per-employee history, NOT NULL and FK and index after migration, and the **collision case**: migration fails, customer department untouched, no second department, no partial history |
| Cutover | `TenantCutoverCopySqlServerTests` — exact seven-entity manifest, contributor-loss detector, and `C6_15` topological order (Departments before Employees) |

### HTTP (`API.Tests`, 466)

`DepartmentEndpointTests` (33) covers every route for success, missing token, missing permission, and
permission bleed. Four are worth naming:

- **`D6`** — a unique-constraint violation on **create** answers `409 department.code_conflict`.
- **`D23`** — the same persistence error on **assign-manager** answers `409 concurrency.conflict`.
- **`D24`** — a rowversion conflict on assign-manager answers **identically** to `D23`. The pair is the
  proof of `DEC-DEP-0027`: a caller cannot tell which check refused them.
- **`D32`** — an undisclosed manager reaches the wire as *assigned, without an identity*, and does not
  collapse into *no manager*.

`HrRouteInventoryTests` enumerates the routes from the harnesses, which call the production mapping
extensions: every route carries a permission, the twenty-one-route inventory matches method, pattern and
permission exactly, and no `DELETE` verb exists.

### A finding worth keeping: what a test cannot prove

`A_large_tenant_copies_by_streaming_and_every_query_seeks` asserted that a copy allocated under 256 MB, on
the premise that materialising the tenant would allocate "row payload times row count". The first Release
run failed it at 287 MB under parallel load while it passed in isolation, and measurement showed the
assertion had never been able to discriminate:

| Quantity | Value |
|---|---|
| Streaming baseline, isolated | **74 MB** (3889 bytes/row) |
| Materialisation, 20 000 rows | **≈12 MB** entities, ≈36 MB with change tracking |

**The materialisation cost is smaller than the streaming baseline.** `GC.GetTotalAllocatedBytes` counts
cumulative allocation, and the transient reader buffers a streaming copy already churns dwarf the cost of
retaining the entities. A materialising implementation would have landed near 110 MB and passed. The ratio
is row-count-invariant, so raising the volume does not restore the margin.

Two replacements were considered and refuted. A retention probe measures at the wrong *time*: the
materialised graph is unreachable by the time the operation returns. A server statement-shape probe measures
the wrong *thing*: the copier issues one unbounded `SELECT` and `BatchSize` is a write-side `SqlBulkCopy`
option, so the statement shape is identical for both designs.

The assertion was removed and the figure kept as diagnostic output. The property it named — that the live
reader reaches `WriteToServerAsync` and nothing drains it into a collection — is a property of the *source*,
and is now guarded structurally by `The_table_copier_streams_the_reader_it_opens` (`2ef7ca3`). The two tests
reference each other by name.

The general lesson is recorded here because it generalises: **a budget assertion whose noise band overlaps
its signal is worse than no assertion**, because it reads as a passing guard.

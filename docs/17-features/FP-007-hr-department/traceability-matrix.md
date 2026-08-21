---
document_id: FP-007-RTM
title: HR Department — Traceability Matrix
status: Approved for Implementation
version: 1.0
---

# FP-007 — Traceability Matrix

## Source requirement coverage

| Source requirement | Coverage in FP-007 |
|---|---|
| `REQ-HR-0100` Department CRUD | `FR-DEP-0101`–`FR-DEP-0104`, `FR-DEP-0108` — realized |
| `REQ-HR-0101` Department Hierarchy | `FR-DEP-0105`, `FR-DEP-0106` — realized |
| `REQ-HR-0102` Department Manager | `FR-DEP-0107` — realized, via `tenant.DepartmentManagers` (`DEC-DEP-0022`) |
| `REQ-HR-0006` Employee History | **Not extended** — department history deferred (`DEC-DEP-0016`, `OD-DEP-004`) |
| `REQ-HR-0200`–`REQ-HR-0202` Position | **Out of scope** (`DEC-DEP-0020`) |

## Business rule coverage

| Rule | Decision | Acceptance criteria | Test scenarios | Status |
|---|---|---|---|---|
| `BR-HR-0005` — one department per employee | `DEC-DEP-0009`, `DEC-DEP-0010` | AC-DEP-0033, AC-DEP-0039 | TS-DEP-0020, TS-DEP-0041 | **OPEN — `OD-DEP-001`.** Realized for new employees; existing rows undecided |
| `BR-HR-0007` — no self-management | `DEC-DEP-0014` | AC-DEP-0023 | TS-DEP-0039 | **OPEN — `OD-DEP-003`.** Partially realizable at best; the personal reporting line has no field and no requirement |
| `BR-HR-0008` — no circular hierarchies | `DEC-DEP-0006` | AC-DEP-0012, AC-DEP-0013, AC-DEP-0017 | TS-DEP-0025, TS-DEP-0026, TS-DEP-0028, TS-DEP-0031, TS-DEP-0032 | **Realized** |
| `BR-HR-0009` — inactive departments receive nobody | `DEC-DEP-0012` | AC-DEP-0028, AC-DEP-0029 | TS-DEP-0015, TS-DEP-0016 | **Realized** |
| `BR-PLT-0002` — company isolation | `DEC-DEP-0001` | AC-DEP-0006, AC-DEP-0011, AC-DEP-0019 | TS-DEP-0008, TS-DEP-0012, TS-DEP-0034 | **Realized** |
| `BR-PLT-0003` — soft delete | `BRULE-DEP-0016` | AC-DEP-0032 | TS-DEP-0019 | **Realized** |
| `BR-PLT-0004` — audit trail | `DEC-DEP-0003` | AC-DEP-0001 | TS-DEP-0001 | **Realized** |
| `BR-PLT-0013` — branch owns transactions | `DEC-DEP-0001` | AC-DEP-0051, AC-DEP-0052 | TS-DEP-0055, TS-DEP-0056 | **Respected by exclusion**, guarded |
| `BR-HR-0006` — one active position | `DEC-DEP-0020` | — | — | **Transferred** to the Position package |

## Obligations carried into FP-007 from FP-006

Every obligation FP-006 transferred here is traceable to a first-class acceptance criterion **and** a
first-class test scenario, or is explicitly recorded as open pending an owner decision. **None exists only in
prose.**

| Carried obligation | Origin | Decision | AC | TS | Status |
|---|---|---|---|---|---|
| `BR-HR-0005` enforcement, including for V1 employees | `DEC-EMP-0017`, `BRULE-EMP-0026` | `DEC-DEP-0009` | AC-DEP-0033, AC-DEP-0039 | TS-DEP-0020, TS-DEP-0041 | **OPEN — `OD-DEP-001`** |
| `BR-HR-0007` once a reporting line exists | `DEC-EMP-0031` | `DEC-DEP-0014` | AC-DEP-0023 | TS-DEP-0039 | **OPEN — `OD-DEP-003`**; partially transferred onward |
| Department must not be a placeholder on Employee | `DEC-EMP-0017` | `DEC-DEP-0010`, `DEC-DEP-0015` | AC-DEP-0035 | TS-DEP-0021 | Realized — `DepartmentId` is real and immutable outside the sanctioned channel |
| Department history | FP-006 RTM, `REQ-HR-0006` partial | `DEC-DEP-0016` | — | — | **OPEN — `OD-DEP-004`**; transferred with an explicit statement of what is lost |

## Obligations FP-007 transfers onward

| Obligation | To | Terms |
|---|---|---|
| `BR-HR-0006` — every employee has one active position | The package introducing Position | Binding, deferred, no placeholder introduced (`DEC-DEP-0020`) |
| `BR-HR-0007` personal reporting line | The package introducing an employee reporting line — **which no current requirement asks for** | Must decide whether such a line exists at all before it can be enforced (`DEC-DEP-0014`) |
| Employee department history | The package introducing employee history | The gap between FP-007 and that package is unrecoverable (`DEC-DEP-0016`) |
| Direct `Department → Employee` manager FK with a two-pass cutover copy | An ADR-level change to Platform's cutover engine | Only if that engine gains cycle-aware copying; conditions in `ADR-026` (`DEC-DEP-0022`) |

## Non-functional requirement coverage

| NFR | Subject | Where realized | AC | TS |
|---|---|---|---|---|
| `NFR-DEP-0301` | Scoped reads are indexed | `IX_Departments_TenantId_CompanyId_Status` ([`data-model.md`](data-model.md)) | AC-DEP-0044 | TS-DEP-0047 |
| `NFR-DEP-0302` | Hierarchy queries bounded by a recursion limit | `DEC-DEP-0005` ([`domain-model.md`](domain-model.md)) | AC-DEP-0016 | TS-DEP-0027 |
| `NFR-DEP-0303` | Optimistic concurrency on every mutation | `RowVersion` transport ([`api-contracts.md`](api-contracts.md)) | AC-DEP-0048 | TS-DEP-0051 |
| `NFR-DEP-0304` | Authorization resolves live | `DepartmentReadScope` ([`authorization-model.md`](authorization-model.md)) | AC-DEP-0008, AC-DEP-0009 | TS-DEP-0010, TS-DEP-0011 |
| `NFR-DEP-0305` | Cutover coverage derived, not declared | `DEC-DEP-0022`, `RISK-DEP-001` | AC-DEP-0034, AC-DEP-0049 | TS-DEP-0044, TS-DEP-0052, TS-DEP-0053 |
| `NFR-DEP-0306` | Acyclicity holds under concurrency | `DEC-DEP-0006` serialization | AC-DEP-0017 | TS-DEP-0031 |

## Architecture decision coverage

| Concern | Authority | AC | TS |
|---|---|---|---|
| Ownership classification is explicit | Principle 11, `ADR-026` | AC-DEP-0051, AC-DEP-0052 | TS-DEP-0055, TS-DEP-0056, TS-DEP-0057 |
| Hierarchy representation | `ADR-026` | AC-DEP-0014, AC-DEP-0016 | TS-DEP-0027, TS-DEP-0029 |
| Acyclicity invariant | `ADR-026`, `BR-HR-0008` | AC-DEP-0013, AC-DEP-0017 | TS-DEP-0028, TS-DEP-0031, TS-DEP-0032 |
| Manager association shape and cutover | `ADR-026`, `ADR-020` | AC-DEP-0034 | TS-DEP-0044, TS-DEP-0052 |
| Company scope resolved live | `ADR-025` d8, d10 | AC-DEP-0008, AC-DEP-0009 | TS-DEP-0010, TS-DEP-0011 |
| Permissions actually reachable | FP-006P regression | AC-DEP-0043 | TS-DEP-0046 |
| Cutover coverage derived, not declared | `ADR-020`, FP-006C6 | AC-DEP-0034, AC-DEP-0049, AC-DEP-0050 | TS-DEP-0044, TS-DEP-0052, TS-DEP-0053, TS-DEP-0054 |

## Orphan check

- **Every acceptance criterion** `AC-DEP-0001` … `AC-DEP-0052` is referenced by at least one test scenario.
- **Every test scenario** `TS-DEP-0001` … `TS-DEP-0057` names at least one acceptance criterion.
- **Every functional requirement** `FR-DEP-0101` … `FR-DEP-0111` is covered by at least one acceptance
  criterion.
- **Every business rule** `BRULE-DEP-0001` … `BRULE-DEP-0022` is either covered above or is a definitional
  statement supporting one that is.

## What this matrix does not claim

`BR-HR-0005` and `BR-HR-0007` are recorded as **OPEN**, not as covered. FP-007 cannot claim to satisfy either
until `OD-DEP-001` and `OD-DEP-003` are answered, and stating otherwise would be the exact failure this
matrix exists to prevent.

---

## As-built traceability (2026-08-21)

Requirement and rule to shipped code to the test that defends it. Every row was verified against the source
rather than inferred from the design.

| Requirement / rule | Decision | Shipped code | Proven by |
|---|---|---|---|
| `REQ-HR-0100` create and edit a department | `DEC-DEP-0007`, `DEC-DEP-0023` | `Department.Create`, `CreateDepartmentCommandHandler`, `UpdateDepartmentCommandHandler`, `POST`/`PUT /api/hr/departments` | `DepartmentDomainTests`, `DepartmentApplicationSqlServerTests`, `D1`–`D7`, `D15`–`D18` |
| `REQ-HR-0101` department hierarchy | `DEC-DEP-0005`, `DEC-DEP-0006`, `DEC-DEP-0008`, `DEC-DEP-0023` | `Department.ChangeParent`, `ChangeDepartmentParentCommandHandler`, `MoveDepartmentToRootCommandHandler`, `SqlServerDepartmentHierarchyLock`, `/move`, `/move-to-root`, `/children` | concurrent-cycle proof in `DepartmentApplicationSqlServerTests`; `D19`–`D21`; `D13`–`D14` |
| `REQ-HR-0102` manager and employee assignment | `DEC-DEP-0013`, `DEC-DEP-0018`, `DEC-DEP-0022`, `DEC-DEP-0028` | `DepartmentManager`, `AssignDepartmentManagerCommandHandler`, `ClearDepartmentManagerCommandHandler`, `Employee.ChangeDepartment`, `/manager`, `/manager/remove`, `/change-department` | `D22`–`D25`; `A6e`–`A6i`; `D4`–`D10` in `EmployeeBoundarySqlServerTests` |
| `BR-HR-0005` every employee has a department | `DEC-DEP-0009`, `DEC-DEP-0010` | `Employee.DepartmentId` NOT NULL, required on creation; `20260820140653_AddEmployeeDepartment` | `EmployeeDepartmentMigrationSqlServerTests` (12); `D1`–`D3` |
| `BR-HR-0007` manager scope | `DEC-DEP-0014` | `DepartmentManager` only; **no** `Employee.ManagerId` | `EmployeeDepartmentArchitectureTests.Neither_aggregate_gained_a_manager_reference` |
| `BR-HR-0008` hierarchy acyclicity | `DEC-DEP-0006` | ancestry walk under a per-company app lock | concurrent-cycle proof; `AssertAcyclicAsync` |
| `BR-PLT-0003` no physical deletion | `DEC-DEP-0011` | no delete on any department path | `DepartmentApplicationSqlServerTests` |
| `BR-PLT-0016` reporting scope | `DEC-DEP-0019` | company-scoped department reads; branch filters membership | `D15_A_department_filter_still_obeys_branch_scope` |
| `BRULE-DEP-0016` manager association | `DEC-DEP-0022` | `tenant.DepartmentManagers`, primary key on `DepartmentId` | `A_department_can_have_at_most_one_manager`; `C6_15` |
| `ADR-012` module isolation | `DEC-DEP-0026` | `HR.API` references no Platform assembly | `DepartmentApiArchitectureTests.The_hr_api_references_no_platform_assembly` |
| `ADR-020` / `ADR-023` d.21 cutover manifest | `DEC-DEP-0029` | manifest derived by reflection over `ITenantOwnedEntity` | `C6_1`/`C6_2`, `C6_14`, `C6_15` |
| `ADR-024` boundary — department is not a partition | `DEC-DEP-0002`, `DEC-DEP-0018` | branch transfer and department change are independent operations | `D11`, `D12`, `D13`; `EmployeeDepartmentDomainTests` |
| `ADR-025` d.10 scoped reads | `DEC-DEP-0019` | `DepartmentReadScope`, resolver-only construction | `DepartmentApiArchitectureTests` (four transport-boundary guards) |
| `ADR-026` d.1 department spans branches | `DEC-DEP-0001` | `Department` is not `IBranchOwnedEntity` | `Department_and_its_history_are_never_branch_owned` |
| `ADR-026` d.7 manager table split | `DEC-DEP-0022`, `DEC-DEP-0029` | separate `DepartmentManagers` table keeps the copy graph acyclic | `C6_15_The_copy_order_places_every_principal_before_its_dependents` |

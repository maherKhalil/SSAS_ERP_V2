---
document_id: FP-007-DATA
title: HR Department — Data Model
status: Approved for Implementation
version: 1.0
---

# FP-007 — Data Model

> Draft. This document describes the **target schema** and the migration to be authored during
> implementation. **No migration is created by this documentation package.** The migration's shape depends on
> `OD-DEP-001` and cannot be finalized before it is answered.

All persisted strings are `nvarchar`. No foreign key crosses a database boundary (`ADR-017`). Both principals
Department references — `tenant.Companies` (`ADR-014` revision 1.1 Correction A) and `tenant.Employees` — live
in the tenant catalog, so every constraint here is intra-catalog.

## `tenant.Departments`

| Column | Type | Null | Notes |
|---|---|---|---|
| `DepartmentId` | `uniqueidentifier` | NO | PK, `ValueGeneratedNever` (`ADR-013`) |
| `TenantId` | `uniqueidentifier` | NO | Stamped by the persistence boundary |
| `CompanyId` | `uniqueidentifier` | NO | Stamped by the persistence boundary |
| `Code` | `nvarchar(32)` | NO | User-entered, as displayed |
| `NormalizedCode` | `nvarchar(32)` | NO | Binary collation, backs the uniqueness index |
| `Name` | `nvarchar(128)` | NO | Not unique |
| `ParentDepartmentId` | `uniqueidentifier` | YES | Null = root |
| `Status` | `nvarchar(32)` | NO | Binary collation; `Active` \| `Inactive` |
| `StatusChangedUtc` | `datetimeoffset` | NO | |
| `StatusChangedBy` | `nvarchar(256)` | NO | Matches `Employee.ActorMaximumLength` |
| `CreatedUtc` | `datetimeoffset` | NO | `IAuditableEntity` |
| `CreatedBy` | `nvarchar(256)` | YES | `IAuditableEntity` |
| `ModifiedUtc` | `datetimeoffset` | NO | `IAuditableEntity` |
| `ModifiedBy` | `nvarchar(256)` | YES | `IAuditableEntity` |
| `RowVersion` | `rowversion` | NO | Concurrency token; **never copied by cutover** |

### Normalization

`NormalizedCode` is the trimmed, upper-invariant form of `Code`, stored under a binary collation
(`OrdinalCollation`, the same constant `EmployeeConfiguration` uses). This makes the uniqueness index
authoritative under concurrent insert rather than advisory, and it is why codes that normalize alike collide
(`BRULE-DEP-0004`) — the same behaviour, and the same rationale, as `NormalizedEmployeeNumber`.

### Indexes

| Index | Columns | Kind |
|---|---|---|
| `UX_Departments_TenantId_CompanyId_NormalizedCode` | `TenantId, CompanyId, NormalizedCode` | UNIQUE — `BRULE-DEP-0004` |
| `IX_Departments_TenantId_CompanyId_Status` | `TenantId, CompanyId, Status` | Scoped list; leading keys match the mandatory predicate order (`NFR-DEP-0301`) |
| `IX_Departments_TenantId_CompanyId_ParentDepartmentId` | `TenantId, CompanyId, ParentDepartmentId` | Hierarchy traversal in both directions |
| `IX_DepartmentManagers_TenantId_CompanyId_ManagerEmployeeId` | on `tenant.DepartmentManagers`: `TenantId, CompanyId, ManagerEmployeeId` | Finds departments a given employee heads — needed by `BRULE-DEP-0012` and by the terminated-manager report |

### Check constraints

| Constraint | Definition |
|---|---|
| `CK_Departments_ParentIsNotSelf` | `[ParentDepartmentId] <> [DepartmentId]` |
| `CK_Departments_Code_NotBlank` | `LEN(LTRIM(RTRIM([Code]))) > 0` |
| `CK_Departments_Name_NotBlank` | `LEN(LTRIM(RTRIM([Name]))) > 0` |
| `CK_Departments_Status` | `[Status] IN (N'Active', N'Inactive')` |

`CK_Departments_ParentIsNotSelf` is the only part of `BR-HR-0008` a constraint can express. The general
acyclicity rule is transactional — see [`domain-model.md`](domain-model.md), which states that asymmetry
plainly.

### Foreign keys

| FK | Target | Delete behaviour |
|---|---|---|
| `CompanyId` | `tenant.Companies` | `RESTRICT` |
| `ParentDepartmentId` | `tenant.Departments` (self) | `RESTRICT` |

**`RESTRICT` on both, deliberately.** Companies are archived and departments deactivated, never deleted;
a cascade would silently erase organizational structure along with them. This matches the reasoning already
recorded in `HrTenantModelContributor` for Employee's own foreign keys.

**The self-referencing FK must be declared without a cascade.** SQL Server rejects a cascading self-reference
outright, so `RESTRICT` here is both correct and the only legal option — worth stating so nobody tries to
"fix" it later.

## `tenant.Employees` — the change

| Column | Type | Null | Notes |
|---|---|---|---|
| `DepartmentId` | `uniqueidentifier` | **depends on `OD-DEP-001`** | FK → `tenant.Departments`, `RESTRICT` |

| Index | Columns |
|---|---|
| `IX_Employees_TenantId_CompanyId_DepartmentId` | `TenantId, CompanyId, DepartmentId` — backs `FR-DEP-0111` and the `BRULE-DEP-0012` check |

The existing `IX_Employees_TenantId_CompanyId_BranchId_Status` is **not** altered. Department is not an
authorization dimension (`DEC-DEP-0019`), so it does not belong in the scoped-search index; adding it there
would suggest it were part of the mandatory predicate.

## Migration shape by owner decision

The migration cannot be written until `OD-DEP-001` is answered, because its steps differ materially:

| `OD-DEP-001` | Migration steps |
|---|---|
| **A — default department + backfill** | 1. Create `Departments`. 2. Add `Employees.DepartmentId` **nullable**. 3. Insert one `Unassigned` department per existing company. 4. `UPDATE Employees SET DepartmentId = <that company's Unassigned>`. 5. `ALTER COLUMN … NOT NULL`. 6. Add FK and index. Steps 3 and 4 are **data migration inside a schema migration**, which the repository has not done before and which deserves its own review |
| **B — nullable now, required later** | 1. Create `Departments`. 2. Add nullable `DepartmentId` + FK + index. The `NOT NULL` alteration is a **named later migration** that must not be forgotten |
| **C — nullable indefinitely** | As B, with no committed follow-up. Not recommended |
| **D — block until assigned** | 1. Create `Departments`. 2. Add nullable `DepartmentId` + FK + index. 3. A **separate, later** migration alters to `NOT NULL`, which fails loudly if any null remains. If no production employees exist, steps 2 and 3 collapse into one and the column is `NOT NULL` immediately |

**Under every option the column is added nullable first.** `ALTER TABLE ADD` of a non-nullable column without
a default fails on a non-empty table, so the ordering above is a constraint of the engine, not a preference.

## Shared→Dedicated cutover

Coverage is **derived, not declared**. `TenantCutoverCopyPlan` computes the manifest from `TenantDbContext`'s
own model — every non-owned entity implementing `ITenantOwnedEntity` with a table name — and orders it by the
foreign-key graph, principals before dependents. Department implements `ITenantOwnedEntity` and is contributed
by `HrTenantModelContributor`, so **it is covered by construction**; no list needs editing for the copy to
find it.

The resulting order, derived from the FK graph rather than asserted, **once `RISK-DEP-001` below is
resolved**:

```
                                             ┌→  DepartmentManagers
Company  →  Branch  →  Department  →  Employee
                                             └→  EmployeeBranchAssignment
```

The last two are siblings, and the copy places them in deterministic table-name order.

### RISK-DEP-001 — the naive manager foreign key breaks cutover outright

**This is confirmed against the source, not a hypothetical.** The naive model has
`Department.ManagerEmployeeId → Employee` and `Employee.DepartmentId → Department`, which is a **cycle in the
table-level foreign-key graph**. `TenantCutoverCopyPlan.Order` builds a dependency set per entity and
repeatedly takes those whose principals are all already placed:

```csharp
if (ready.Length == 0)
{
  return Result.Failure<...>(TenantStorageErrors.CutoverCopyOrderUndecidable);
}
```

With a mutual reference, neither table is ever ready, `ready.Length` is zero on the first pass, and the plan
**fails hard**. It does not degrade, warn, or fall back — by design, because disabling constraints to make a
copy fit produces a database the application cannot trust. **Shared→Dedicated cutover would stop working for
every tenant the moment this schema shipped.**

Resolutions considered:

| | Approach | Keeps `REQ-HR-0102` | Keeps DB integrity | Platform change |
|---|---|---|---|---|
| **(a)** | Two-pass copy: Department rows first with a null manager, manager updated after Employees land | Yes | Yes | **Yes** — changes the cutover engine, a Platform-owned component, from HR's feature package |
| **(b)** | Drop the manager feature | **No** — abandons an in-scope requirement | — | No |
| **(c)** | No FK on `ManagerEmployeeId`; validate in the application only | Yes | **No** — one column loses referential integrity | No |
| **(d)** | **`tenant.DepartmentManagers`: a separate table with FKs to both Department and Employee** | Yes | Yes | No |

**Decision `DEC-DEP-0022`: option (d).** Classification: **ENGINEERING-RECOMMENDATION.**

Both of the new table's foreign keys point *outward*, so it is a dependent of Department and of Employee and a
principal of neither. The graph becomes acyclic and the derived order is
`Company → Branch → Department → Employee → {EmployeeBranchAssignment, DepartmentManagers}`, with the last two
in deterministic name order. Referential integrity is fully preserved, the copy engine is untouched, and no
constraint is ever disabled.

```
tenant.DepartmentManagers
  DepartmentId       uniqueidentifier  NO   PK and FK → tenant.Departments, RESTRICT
  TenantId           uniqueidentifier  NO   stamped
  CompanyId          uniqueidentifier  NO   stamped
  ManagerEmployeeId  uniqueidentifier  NO   FK → tenant.Employees, RESTRICT
  AssignedUtc/By     …                 NO   who assigned the manager and when
  RowVersion         rowversion        NO
```

`DepartmentId` as the primary key is what enforces "at most one manager per department" in the database
rather than in a handler. Clearing a manager deletes the row — this table is a *current-state* projection, not
history, and `BRULE-DEP-0016`'s no-physical-delete rule governs Departments, not this association.

Option (a) is the model a greenfield design would choose, and it is the right answer eventually. It is
rejected **here** because an HR feature package must not reach into Platform's cutover engine to make its own
schema fit; that is an ADR-level change to a component with its own proven guards, and it should be taken
deliberately rather than as a side effect. `ADR-026` records this and names the conditions under which (a)
should replace (d).

There is precedent for declining a convenient foreign key on classification grounds: `ADR-024` gave
`EmployeeBranchAssignment` no branch FK, and `C6_12_The_assignment_has_no_branch_foreign_key` records that the
convenience was declined rather than overlooked. This decision is recorded the same way, as `AC-DEP-0034` and
`TS-DEP-0044` — not as prose.

⚠⚠⚠ **CORRECTED 2026-09-01, AND THE PARAGRAPH ABOVE WAS THE SHARPEST INSTANCE OF ITS OWN SUBJECT: IT CLAIMS
THE DECISION IS RECORDED *NOT AS PROSE*, AND IT IS PROSE ABOUT A TEST THAT DOES NOT EXIST.** The precedent it
cites is real — `C6_12_The_assignment_has_no_branch_foreign_key` is an executed test. **`TS-DEP-0044` is not:
it is an APPROVED, UNIMPLEMENTED scenario**, and `AC-DEP-0034`'s second clause was carried by nothing until
2026-09-01.

**WHAT IS TRUE TODAY:** `CutoverCopyOrderCycleTests` (`tests/Platform.Tests/TenantStorage/`) proves the
copy planner returns `CutoverCopyOrderUndecidable` for a foreign-key cycle among tenant-owned entities,
isolated from the two unrelated conditions that share that error value by a matched acyclic control.
⚠ **That `Department` carrying a direct manager foreign key WOULD produce such a cycle is still argued from
the model, not executed** — see `AC-DEP-0034`, which carries the bound, and backlog `B25` for why the
Department-shaped test has no project it can live in.

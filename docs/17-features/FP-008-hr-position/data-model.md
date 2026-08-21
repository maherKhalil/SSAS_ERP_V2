---
document_id: FP-008-DATA
title: HR Position — Data Model
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — Data Model

> Draft. This document describes the **target schema** and the migration to be authored during
> implementation. **No migration is created by this documentation package.** Its shape depends on
> `OD-POS-001` and `OD-POS-002` and cannot be finalized before both are answered.

All persisted application strings are `nvarchar`. No foreign key crosses a database boundary (`ADR-017`).
Every principal referenced here — `tenant.Companies`, `tenant.Employees`, `tenant.Departments` — lives in the
tenant catalog, so every constraint below is intra-catalog.

## `tenant.Positions`

| Column | Type | Null | Notes |
|---|---|---|---|
| `PositionId` | `uniqueidentifier` | NO | PK, `ValueGeneratedNever` (`ADR-013`) |
| `TenantId` | `uniqueidentifier` | NO | Stamped by the persistence boundary |
| `CompanyId` | `uniqueidentifier` | NO | Stamped by the persistence boundary |
| `Code` | `nvarchar(32)` | NO | User-entered, as displayed |
| `NormalizedCode` | `nvarchar(32)` | NO | Binary collation, backs the uniqueness index |
| `Title` | `nvarchar(128)` | NO | Not unique |
| `JobGradeId` | `uniqueidentifier` | YES | FK → `tenant.JobGrades`; exists only under `OD-POS-002` (i)–(iii) |
| `Status` | `nvarchar(32)` | NO | Binary collation; `Active` \| `Inactive` |
| `StatusChangedUtc` | `datetimeoffset` | NO | |
| `StatusChangedBy` | `nvarchar(256)` | NO | Matches `Employee.ActorMaximumLength` |
| `CreatedUtc` | `datetimeoffset` | NO | `IAuditableEntity` |
| `CreatedBy` | `nvarchar(256)` | YES | `IAuditableEntity` |
| `ModifiedUtc` | `datetimeoffset` | NO | `IAuditableEntity` |
| `ModifiedBy` | `nvarchar(256)` | YES | `IAuditableEntity` |
| `RowVersion` | `rowversion` | NO | Concurrency token; **never copied by cutover** |

**There is no employee column of any kind** — no `CurrentHolderEmployeeId`, no `IncumbentEmployeeId`. See
`DEC-POS-0002` and the cutover section below; this absence is load-bearing.

`DepartmentId` appears here **only** if `OD-POS-003` selects option (c) or (d), and its presence changes that
decision's whole consequence set.

### Indexes

| Index | Columns | Kind |
|---|---|---|
| `UX_Positions_TenantId_CompanyId_NormalizedCode` | `TenantId, CompanyId, NormalizedCode` | UNIQUE — `BRULE-POS-0004` |
| `IX_Positions_TenantId_CompanyId_Status` | `TenantId, CompanyId, Status` | Scoped list; leading keys match the mandatory predicate order (`NFR-POS-0301`) |
| `IX_Positions_TenantId_CompanyId_JobGradeId` | `TenantId, CompanyId, JobGradeId` | Grade filter and the `BRULE-POS-0015` dependent check |

### Check constraints

| Constraint | Definition |
|---|---|
| `CK_Positions_Code_NotBlank` | `LEN(LTRIM(RTRIM([Code]))) > 0` |
| `CK_Positions_Title_NotBlank` | `LEN(LTRIM(RTRIM([Title]))) > 0` |
| `CK_Positions_Status` | `[Status] IN (N'Active', N'Inactive')` |

### Foreign keys

| FK | Target | Delete behaviour |
|---|---|---|
| `CompanyId` | `tenant.Companies` | `RESTRICT` |
| `JobGradeId` | `tenant.JobGrades` | `RESTRICT` |

`RESTRICT` on both, deliberately and for the recorded reason: companies are archived and positions and grades
are deactivated, never deleted, so a cascade would silently erase organizational structure. This matches
`HrTenantModelContributor`'s existing treatment of Employee's and Department's foreign keys.

## `tenant.JobGrades` and `tenant.SalaryGrades` **(OD-POS-002)**

Both follow the `Positions` shape exactly — identifier, stamped ownership, normalized code, name, status,
audit stamps, rowversion — plus:

| Column | Type | Null | Notes |
|---|---|---|---|
| `RankOrder` | `int` | NO | Authoritative order; unique within `(TenantId, CompanyId)` per ladder (`DEC-POS-0006`) |
| `SalaryGradeId` | `uniqueidentifier` | YES | **On `JobGrades` only.** FK → `tenant.SalaryGrades`, `RESTRICT`. The reference is one-directional and must stay so (`BRULE-POS-0010`) |
| `MinimumAmount` | `decimal(19,4)` | YES | **On `SalaryGrades` only, and only under `OD-POS-004`** (ii)/(iii) |
| `MidpointAmount` | `decimal(19,4)` | YES | as above |
| `MaximumAmount` | `decimal(19,4)` | YES | as above |

| Constraint | Definition |
|---|---|
| `UX_JobGrades_TenantId_CompanyId_RankOrder` | UNIQUE `(TenantId, CompanyId, RankOrder)` |
| `CK_SalaryGrades_Amounts_NonNegative` | all three `>= 0` when present |
| `CK_SalaryGrades_Amounts_Ordered` | `Minimum <= Midpoint AND Midpoint <= Maximum` when all present |

**The amount columns are nullable, and that is not laziness.** `OD-POS-001` Option A must seed a synthetic
grade for the backfill, and there is no honest minimum or maximum for a grade nobody designed. A mandatory
range and a seeded-default backfill are incompatible unless either the range is nullable or the seeded row is
exempt — and an exemption means a system-origin discriminator column, which `DEC-DEP-0009`'s amendment
explicitly declined to add. **Nullable amounts are the only combination of `OD-POS-001` Option A and
`OD-POS-004` option (ii) that does not require reversing an FP-007 decision.**

### No currency column

Amounts are denominated in the owning Company's `BaseCurrencyCode`. `DEC-POS-0015` records why: HR cannot
reference `SSAS.Platform.Domain`, so the product's ISO-4217 value object is unreachable; duplicating it gives
the product two answers to "is XYZ a currency"; and `DEC-CMP-0009` makes a company's base currency required
at creation and immutable, so every row under one company already has exactly one unambiguous currency. The
condition for revisiting is stated in `DEC-POS-0015` and in `ADR-027`.

**Note on the `nvarchar` guardrail, stated so it is not read as an inconsistency.** `Company.BaseCurrencyCode`
is `char(3)`, fixed-length, `Latin1_General_100_BIN2`, with `CK_Companies_BaseCurrencyCode` restricting it to
`[A-Z][A-Z][A-Z]`. That is a deliberate exception for a constraint-validated ASCII code, not a precedent for
application text. FP-008 adds **no** currency column, so the question does not arise here; if it ever does,
matching `char(3)` exactly is the answer, and it should be recorded as inheriting the exception rather than
creating a new one.

## `tenant.EmployeePositionAssignments`

| Column | Type | Null | Notes |
|---|---|---|---|
| `EmployeePositionAssignmentId` | `uniqueidentifier` | NO | PK, `ValueGeneratedNever` |
| `TenantId` | `uniqueidentifier` | NO | Stamped |
| `CompanyId` | `uniqueidentifier` | NO | Stamped |
| `EmployeeId` | `uniqueidentifier` | NO | FK → `tenant.Employees`, `RESTRICT` |
| `SourcePositionId` | `uniqueidentifier` | YES | FK → `tenant.Positions`, `RESTRICT`. **NULL only on the initial record** |
| `DestinationPositionId` | `uniqueidentifier` | NO | FK → `tenant.Positions`, `RESTRICT` |
| `EffectiveFromUtc` | `datetimeoffset` | NO | |
| `ChangedBy` | `nvarchar(256)` | NO | |
| `ReasonCode` | `nvarchar(32)` | YES | |
| `ReasonText` | `nvarchar(512)` | YES | |
| `CreatedUtc` / `CreatedBy` | | | `IAuditableEntity` |

| Constraint | Definition |
|---|---|
| `CK_EmployeePositionAssignments_SourceNotDestination` | `[SourcePositionId] IS NULL OR [SourcePositionId] <> [DestinationPositionId]` |

| Index | Columns |
|---|---|
| `IX_EmployeePositionAssignments_TenantId_CompanyId_EmployeeId_EffectiveFromUtc` | history read in effective order |

**No `RowVersion`, no `ModifiedUtc`, no `ModifiedBy`, no `EffectiveToUtc`, and no branch column** — each
absence for the reason recorded on `EmployeeDepartmentAssignment`, restated in
[`domain-model.md`](domain-model.md). It implements `IAppendOnlyEntity`.

## `tenant.Employees` — the change

| Column | Type | Null | Notes |
|---|---|---|---|
| `PositionId` | `uniqueidentifier` | **depends on `OD-POS-001`** | FK → `tenant.Positions`, `RESTRICT` |

| Index | Columns |
|---|---|
| `IX_Employees_TenantId_CompanyId_PositionId` | `TenantId, CompanyId, PositionId` — backs `FR-POS-0213` and the incumbent lookup that replaces the reference `DEC-POS-0002` refuses to store |

The existing `IX_Employees_TenantId_CompanyId_BranchId_Status` is **not** altered, and neither is
`IX_Employees_TenantId_CompanyId_DepartmentId`. Position is not an authorization dimension
(`DEC-POS-0020`), so it does not belong in the scoped-search index; adding it there would suggest it were
part of the mandatory predicate.

## Migration shape by owner decision

The migration cannot be written until `OD-POS-001` is answered, because its steps differ materially:

| `OD-POS-001` | Migration steps |
|---|---|
| **A — seeded default + backfill** | 1. Create the grade tables, `Positions`, `EmployeePositionAssignments`. 2. Add `Employees.PositionId` **nullable**. 3. **Collision pass over every affected company before any write.** 4. Insert the synthetic chain per company. 5. `UPDATE Employees SET PositionId = …`. 6. Insert one initial history row per employee. 7. `ALTER COLUMN … NOT NULL`. 8. Add FK and index. Steps 3–6 are data migration inside a schema migration — `20260820140653_AddEmployeeDepartment` is the working template, including its `THROW`-with-remedy failure |
| **B — nullable now, required later** | Create tables; add nullable `PositionId` + FK + index. The `NOT NULL` alteration is a **named later migration** that must not be forgotten |
| **C — nullable indefinitely** | As B, with no committed follow-up. Recommended against by `OD-DEP-001` in every case |
| **D — block until assigned** | Create tables; add nullable `PositionId` + FK + index; a **separate later** migration alters to `NOT NULL` and fails loudly if any null remains. **If no production tenant holds Employee rows, steps collapse and the column is `NOT NULL` immediately** |
| **E — amend `BR-HR-0006`** | As B, but with no follow-up owed, and with a corresponding edit to `Business-Rules.md` |

**Under every option the column is added nullable first.** `ALTER TABLE ADD` of a non-nullable column without
a default fails on a non-empty table; the ordering is a constraint of the engine, not a preference.

### The collision case, if Option A is chosen

FP-007's migration fails loudly and transactionally when a company already holds a Department whose
`NormalizedCode` is `UNASSIGNED` — it does not reuse, rename, modify, delete, or suffix it. **The identical
rule applies here, multiplied by the chain length:** a company may already hold a Position, a Job Grade, or a
Salary Grade whose normalized code collides, and each is a separate way for the same migration to fail. The
collision check must be one pass over all affected companies and all three codes **before any write**, so the
common failure never writes at all rather than relying on rollback, and the error must name the offending
companies, the offending codes, and the one remedy.

## Shared→Dedicated cutover

Coverage is **derived, not declared**. `TenantCutoverCopyPlan.Build` computes the manifest from
`TenantDbContext`'s composed model — every non-owned entity implementing `ITenantOwnedEntity` with a table
name — and orders it by the foreign-key graph, principals before dependents. Every table in this package
implements `ITenantOwnedEntity` and is contributed by `HrTenantModelContributor`, so **all of them are covered
by construction**; no list needs editing for the copy to find them.

The derived order, under the recommended readings:

```
Company → Branch → Department → SalaryGrade → JobGrade → Position → Employee → { EmployeeBranchAssignment,
                                                                                 EmployeeDepartmentAssignment,
                                                                                 EmployeePositionAssignment }
```

The three terminal assignment tables are siblings, and the copy places them in deterministic table-name
order.

### The three sites that pin the set by name

Derivation guarantees the engine cannot **miss** a table. Three assertions guarantee a human **sees** a new
one, and all three must be updated in one deliberate act (`DEC-POS-0022`, `NFR-POS-0304`):

| Site | Today | After FP-008 |
|---|---|---|
| `C6_1_C6_2_The_cutover_manifest_covers_every_contributed_tenant_owned_entity` | exact list of **7** entities | **11** under `OD-POS-002` (i); 10 under (ii)/(iii); 9 under (iv) |
| `C6_15_The_copy_order_places_every_principal_before_its_dependents` | Departments before Employees | plus Positions before Employees, and grades before Positions |
| `TenantRestoreVerificationProviderSqlServerTests` `DROP TABLE` list | **6** tables | the new copy order read backwards |

The third is the one that has broken twice already in this codebase's history, both times because a new
foreign key changed the required order rather than merely lengthening the list. The rule recorded there — *the
drop list is the cutover copy topological order read backwards* — is the thing to apply, not the current
answer to copy.

### RISK-POS-001 — the cycle trap, restated because it is one decision away

`DEC-POS-0002` keeps Position free of any Employee reference. The moment anything in this package gains one —
an incumbent column, a "position holder" association table, or a `Position.CreatedByEmployeeId` convenience
field — the foreign-key graph acquires a cycle, `TenantCutoverCopyPlan.Order` returns
`CutoverCopyOrderUndecidable`, and **Shared→Dedicated cutover stops working for every tenant**. It does not
degrade or warn.

FP-007 found this in design review rather than in a red nightly, and `TS-DEP-0044` asserts the failure mode
executably. The equivalent assertion for Position — a constructed model with a direct incumbent foreign key
must fail with `CutoverCopyOrderUndecidable` — is `TS-POS-0044`, and it should be written before the schema
is, for the same reason.

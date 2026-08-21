---
document_id: FP-008-DATA
title: HR Position — Data Model
status: Approved for Implementation
version: 1.0
---

# FP-008 — Data Model

> Approved. This document describes the **target schema** and the migration to be authored during
> implementation. **No migration is created by this documentation package.** `OD-POS-001` … `OD-POS-004` were
> answered on 2026-08-21 and this document is written to those rulings: three grade-bearing aggregates, money
> as informational bands, no `DepartmentId` on Position, and `Employee.PositionId` `NOT NULL` from day one
> with the fail-loud precondition in `DEC-POS-0026`.

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
| `NormalizedTitle` | `nvarchar(128)` | NO | Binary collation. **Search only** (`DEC-POS-0030`) — upper-invariant trimmed `Title`, maintained by the domain, backing no index and no uniqueness rule |
| `JobGradeId` | `uniqueidentifier` | YES | FK → `tenant.JobGrades`, `RESTRICT`. Null until the position is graded |
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

**There is no `DepartmentId` either.** `OD-POS-003` ruled Position independent of Department, so an employee's
department has exactly one authority — `Employee.DepartmentId`, unchanged from FP-007 — and no invariant has
to keep two copies of that fact in step.

### Indexes

| Index | Columns | Kind |
|---|---|---|
| `UX_Positions_TenantId_CompanyId_NormalizedCode` | `TenantId, CompanyId, NormalizedCode` | UNIQUE — `BRULE-POS-0004` |
| `IX_Positions_TenantId_CompanyId_Status` | `TenantId, CompanyId, Status` | Scoped list; leading keys match the mandatory predicate order (`NFR-POS-0301`) |
| `IX_Positions_TenantId_CompanyId_JobGradeId` | `TenantId, CompanyId, JobGradeId` | Grade filter and the `BRULE-POS-0015` dependent check |

**There is deliberately no index on `NormalizedTitle`.** The title half of the search is a CONTAINS, so its
`LIKE` pattern begins with a wildcard and no B-tree index can seek on it — SQL Server would scan the index
instead of the table and save nothing. The same holds for the two grade ladders' `NormalizedName`. Recorded as
a decision rather than left as an omission, because an index here would read as due diligence
(`DEC-POS-0030`).

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

## `tenant.JobGrades` and `tenant.SalaryGrades`

Both follow the `Positions` shape exactly — identifier, stamped ownership, normalized code, name, status,
audit stamps, rowversion — plus:

| Column | Type | Null | Notes |
|---|---|---|---|
| `RankOrder` | `int` | NO | Authoritative order; unique within `(TenantId, CompanyId)` per ladder (`DEC-POS-0006`) |
| `SalaryGradeId` | `uniqueidentifier` | YES | **On `JobGrades` only.** FK → `tenant.SalaryGrades`, `RESTRICT`. The reference is one-directional and must stay so (`BRULE-POS-0010`) |
| `MinimumAmount` | `decimal(19,4)` | YES | **On `SalaryGrades` only.** Informational band (`OD-POS-004`) |
| `MidpointAmount` | `decimal(19,4)` | YES | as above |
| `MaximumAmount` | `decimal(19,4)` | YES | as above |
| `NormalizedName` | `nvarchar(128)` | NO | Binary collation. **Search only** (`DEC-POS-0030`), on both ladders |

| Constraint | Definition |
|---|---|
| `UX_JobGrades_TenantId_CompanyId_RankOrder` | UNIQUE `(TenantId, CompanyId, RankOrder)` |
| `CK_JobGrades_RankOrder_Positive` | `[RankOrder] > 0` — **added 2026-08-21, `DEC-POS-0028`** |
| `CK_SalaryGrades_RankOrder_Positive` | `[RankOrder] > 0` — **added 2026-08-21, `DEC-POS-0028`** |
| `CK_SalaryGrades_Amounts_NonNegative` | all three `>= 0` when present |
| `CK_SalaryGrades_Amounts_Ordered` | `Minimum <= Midpoint AND Midpoint <= Maximum` when all present |
| `CK_SalaryGrades_Band_Atomic` | all three amounts null, or all three present (`DEC-POS-0027`) |

> **Amendment 2026-08-21 — the rank checks were added by ruling, and the sequence is worth keeping.**
> `BRULE-POS-0007` has always required a positive rank, and Phase 1 enforced it in the aggregate alone
> because this list named no rank constraint — adding an unlisted one would have been filling a gap the
> specification did not leave. Phase 1 **reported** the consequence instead, with a test asserting that a
> direct SQL insert could write a rank of zero. The architect ruled the constraint in, Phase 2 added it as
> the additive migration `AddHrGradeRankConstraint`, and that test now asserts the opposite.
>
> This is the intended loop: the specification is authority, a gap in it is reported rather than filled, and
> the ruling that closes it is recorded where the next reader will look — here, beside the constraint.

**The amount columns are nullable, and the reason originally given for that is discharged.** The draft argued
that `OD-POS-001` Option A would have to seed a synthetic grade with no honest minimum or maximum, so a
mandatory range and a seeded backfill were incompatible unless the range was nullable or the seeded row was
exempt — and an exemption means a system-origin discriminator column, which `DEC-DEP-0009`'s amendment
explicitly declined to add.

**The `OD-POS-001` ruling creates no seeded grade, so that argument no longer applies.** Nullability now rests
on a different and weaker ground: a job ladder may legitimately be defined before it is priced, and a grade
awaiting benchmarking has no honest amounts. That is a real case but a smaller one, and **`DEC-POS-0016`
records it as an open question** for `ADR-026`/`ADR-027` review — the amounts could reasonably be made
mandatory now that nothing forces them to be optional.

`CK_SalaryGrades_Amounts_Ordered` is written as "when all three are present" under either answer, so
tightening later is a nullability change rather than a constraint rewrite.

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
| `PositionId` | `uniqueidentifier` | **NO** | FK → `tenant.Positions`, `RESTRICT`. `NOT NULL` from the first migration (`OD-POS-001`) |

| Index | Columns |
|---|---|
| `IX_Employees_TenantId_CompanyId_PositionId` | `TenantId, CompanyId, PositionId` — backs `FR-POS-0213` and the incumbent lookup that replaces the reference `DEC-POS-0002` refuses to store |

The existing `IX_Employees_TenantId_CompanyId_BranchId_Status` is **not** altered, and neither is
`IX_Employees_TenantId_CompanyId_DepartmentId`. Position is not an authorization dimension
(`DEC-POS-0020`), so it does not belong in the scoped-search index; adding it there would suggest it were
part of the mandatory predicate.

## Migration shape

`OD-POS-001` established that no production tenant holds Employee rows, so the migration is the short one and
there is exactly one:

| Step | Action |
|---|---|
| **1** | **Assert the precondition.** `SELECT COUNT(*) FROM [tenant].[Employees]`. **If it is not zero, `THROW` and write nothing** — see `DEC-POS-0026` |
| 2 | Create `tenant.SalaryGrades` |
| 3 | Create `tenant.JobGrades`, with its FK to `SalaryGrades` |
| 4 | Create `tenant.Positions`, with its FK to `JobGrades` |
| 5 | Create `tenant.EmployeePositionAssignments`, with its FKs to `Employees` and `Positions` |
| 6 | `ALTER TABLE [tenant].[Employees] ADD [PositionId] uniqueidentifier NOT NULL` |
| 7 | Add the FK and `IX_Employees_TenantId_CompanyId_PositionId` |

**Step 6 is legal only because step 1 passed.** `ALTER TABLE ADD` of a non-nullable column without a default
fails on a non-empty table — so on any database holding employees, step 6 would fail anyway, just with an
engine message instead of an explanation. Step 1 turns that into a diagnosis: it names the database, the row
count, and the decision, before any DDL has run.

**No `UNASSIGNED` row of any kind is created.** No synthetic Position, no synthetic JobGrade, no synthetic
SalaryGrade, no backfill `UPDATE`, and no migration-authored history row. Every
`EmployeePositionAssignment` in the product will describe a real assignment made through the application.

### Why step 1 is a separate pass rather than a constraint

`DEC-POS-0026` requires the check **before any write**, not as a consequence of one. The reasoning is
`DEC-DEP-0009`'s: the common failure must never write at all, rather than writing and relying on rollback.

The check is per **tenant database**, every time the migration runs — not once at design time. The operational
fact is about a moment, and the moment differs per database: a tenant provisioned after the ruling, a restored
database, a development or demo catalog, or an `ADR-021` customer-managed database can each hold rows the
ruling did not contemplate.

**What the migration must never do in that case:** supply a default, delete rows, skip the column, or degrade
to nullable. Each would silently attach real employees to a position nobody chose, or quietly abandon
`BR-HR-0006` — and no later migration could know what was meant. The one remedy is to reconsider the backfill
strategy `OD-POS-001` declined, for that tenant, as a decision. **The migration is not to be edited to force
it through.**

FP-007's collision pass in `20260820140653_AddEmployeeDepartment` is the working template for the shape,
including its `THROW`-with-remedy message.

## Shared→Dedicated cutover

Coverage is **derived, not declared**. `TenantCutoverCopyPlan.Build` computes the manifest from
`TenantDbContext`'s composed model — every non-owned entity implementing `ITenantOwnedEntity` with a table
name — and orders it by the foreign-key graph, principals before dependents. Every table in this package
implements `ITenantOwnedEntity` and is contributed by `HrTenantModelContributor`, so **all of them are covered
by construction**; no list needs editing for the copy to find them.

The derived order, with `OD-POS-003` ruling Position independent of Department — so the two are unordered
siblings with respect to each other, and the sequence below is one valid topological answer rather than the
only one:

```
Company → Branch → Department → SalaryGrade → JobGrade → Position → Employee → { EmployeeBranchAssignment,
                                                                                 EmployeeDepartmentAssignment,
                                                                                 EmployeePositionAssignment }
```

The three terminal assignment tables are siblings, and the copy places them in deterministic table-name
order.

### The sites that pin the set by name

Derivation guarantees the engine cannot **miss** a table. A set of assertions guarantees a human **sees** a
new one, and they must be updated in one deliberate act (`DEC-POS-0022`, `NFR-POS-0304`).

**`DEC-POS-0022` carries the authoritative inventory** — nine sites across eight tests, with test names — and
this document deliberately does not repeat it. The first draft of that decision named three, an earlier draft
of this section repeated the same three, and duplicating a list is how a wrong list survives being corrected
in one place. The three headline movements are:

| What moves | Before FP-008 | After FP-008 |
|---|---|---|
| The manifest's exact entity list | **7** entities | **11** — adds `EmployeePositionAssignment`, `JobGrade`, `Position`, `SalaryGrade` |
| The derived copy order | Departments before Employees | plus `SalaryGrade → JobGrade → Position`, and the history after both `Position` and `Employee` |
| The `DROP TABLE` list | **6** tables | **10** — the new copy order read backwards |

**Position IS ordered before Employee as of Phase 3** (`C6_15`). Phase 1 could not claim it and deliberately
did not: nothing linked the two until `Employee.PositionId` became a required foreign key, and a topological
sort cannot order two entities with no path between them, so an assertion written early would have passed on
the sort's tie-breaking rather than on a constraint. The claim and the assertion became true in the same
commit, which is how the forward obligation Phase 1 recorded was discharged.

The drop list needed no reordering for it: it already placed `Positions` after `Employees`, so the Phase 3
foreign key lengthened that list without inverting a pair inside it.

The drop list is the one that has broken twice already in this codebase's history, both times because a new
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

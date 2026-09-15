---
document_id: FP-006-DATA
title: HR Employee Data Model
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Data Model

> Approved for Implementation — model reflecting the settled FP-006A decisions. This document describes the target schema and the migration to be authored during implementation; no migration is created by this documentation package.

## Ownership

The existing **tenant ERP** persistence boundary owns the Employee model:

- `TenantDbContext`;
- `tenant` schema;
- the tenant SQL Server connection resolved by tenant routing (`ADR-017`);
- `tenant.__EFMigrationsHistory`;
- the tenant EF Core migration stream;
- the tenant Unit of Work;
- aggregate-specific repositories and read services owned by HR.

No second context, connection, migration-history table, or Unit of Work is introduced. Employee entity configurations are HR-owned but are applied to the existing `TenantDbContext` model.

Employee is placed in the tenant database because it is tenant business data and because both of its scoping parents — `tenant.Companies` and `tenant.Branches` — already live there. This is what makes both foreign keys intra-catalog and legal.

## Table

### `tenant.Employees`

| Column | SQL definition | Purpose |
|---|---|---|
| `EmployeeId` | `UNIQUEIDENTIFIER` primary key | Server-generated authoritative employee identifier (`ADR-013`) |
| `TenantId` | `UNIQUEIDENTIFIER`, required | Owning tenant; `ITenantOwnedEntity` |
| `CompanyId` | `UNIQUEIDENTIFIER`, required | Owning company; `ICompanyOwnedEntity` |
| `BranchId` | `UNIQUEIDENTIFIER`, required | Current operating branch; `IBranchOwnedEntity` |
| `EmployeeNumber` | `NVARCHAR(64)`, required | Trimmed display number with casing preserved |
| `NormalizedEmployeeNumber` | `NVARCHAR(64)` with `Latin1_General_100_BIN2`, required | Exact `Trim().ToUpperInvariant()` value, unique per company |
| `NationalId` | `NVARCHAR(64)`, nullable | Trimmed display national identity |
| `NormalizedNationalId` | `NVARCHAR(64)` with `Latin1_General_100_BIN2`, nullable | Exact normalized value, unique per company where present |
| `FullName` | `NVARCHAR(200)`, required | Trimmed mutable display name with casing preserved |
| `EmploymentDate` | `DATETIMEOFFSET`, required | Employment start, UTC |
| `TerminationDate` | `DATETIMEOFFSET`, nullable | Employment end, UTC; null until terminated |
| `Status` | bounded `NVARCHAR`, binary collation, required | `Active`, `Inactive`, `Terminated` (created `Active`) |
| `StatusChangedUtc` | `DATETIMEOFFSET`, required | Trusted last lifecycle-change time |
| `StatusChangedBy` | bounded `NVARCHAR`, nullable only where approved system bootstrap applies | Trusted lifecycle actor |
| `StatusChangeReasonCode` | bounded `NVARCHAR`, required | Safe bounded lifecycle reason code |
| `CreatedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `CreatedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |
| `ModifiedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `ModifiedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |
| `RowVersion` | SQL Server `rowversion` | Optimistic concurrency; the transfer serialization point |

**Every persisted application string is `nvarchar`.** No `varchar`, `char`, or `text` column is introduced. The `NVARCHAR(64)` and `NVARCHAR(200)` limits follow the established `CompanyCode` and `CompanyName` conventions (`DEC-CMP-0007`, `DEC-CMP-0008`).

There is **no** `DepartmentId`, `PositionId`, or `ManagerId` column (`BRULE-EMP-0026`).

The Guid `EmployeeId` is the primary key and is never reused. There is no unrelated BIGINT identity key. Because Employee is a higher-write root than Company, the implementation must apply the sequential-`Guid` guidance in `ADR-013` for the clustered key, or justify a nonclustered primary key with a separate clustering choice, and validate the decision against measured insert behavior.

### `tenant.EmployeeBranchAssignments`

| Column | SQL definition | Purpose |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` primary key | Server-generated record identifier |
| `TenantId` | `UNIQUEIDENTIFIER`, required | Owning tenant; `ITenantOwnedEntity` |
| `CompanyId` | `UNIQUEIDENTIFIER`, required | Owning company; `ICompanyOwnedEntity` |
| `EmployeeId` | `UNIQUEIDENTIFIER`, required | The Employee the record describes |
| `SourceBranchId` | `UNIQUEIDENTIFIER`, **nullable** | Branch left; null **only** on the initial assignment record |
| `DestinationBranchId` | `UNIQUEIDENTIFIER`, required | Branch entered |
| `EffectiveFromUtc` | `DATETIMEOFFSET`, required | Commit instant; never a future value |
| `TransferredBy` | bounded `NVARCHAR`, required | Acting principal |
| `ReasonCode` | bounded `NVARCHAR`, binary collation, required | Bounded transfer reason code |
| `ReasonText` | `NVARCHAR(512)`, nullable | Audit-only free text; never used in a decision, compared, indexed, or emitted in an event |
| `CreatedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `CreatedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |

**This table carries no `RowVersion`, no `ModifiedUtc`, and no `ModifiedBy`**, because the record is append-only and is never updated. Serialization of concurrent transfers is provided by `Employees.RowVersion`.

#### It must not become branch-owned

`EmployeeBranchAssignment` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity` and **not** `IBranchOwnedEntity` (`ADR-024` decision 4).

This is a modelling constraint, not an accident of column naming. Neither `SourceBranchId` nor `DestinationBranchId` may be mapped to the `IBranchOwnedEntity.BranchId` property, and **neither column may be named `BranchId`**. Naming either column `BranchId` would risk a future convention, shadow property, or interface implementation silently classifying the table as branch-owned, which would place an append-only cross-branch record inside the branch write boundary and make transfer unrepresentable.

An architecture test asserts the classification (`TS-EMP-0113`).

## Constraints and indexes

### `tenant.Employees`

- Primary key on `EmployeeId`.
- Required `TenantId`.
- Required `CompanyId` with a **restricted foreign key to `tenant.Companies(CompanyId)`** — intra-catalog and legal, because Company lives in the tenant database (`ADR-014` revision 1.1, Correction A).
- Required `BranchId` with a **restricted foreign key to `tenant.Branches(BranchId)`** — intra-catalog.
- **Per-company** unique index on `(TenantId, CompanyId, NormalizedEmployeeNumber)` using `Latin1_General_100_BIN2`, matching the column collation. `BranchId` **does not participate** (`BRULE-EMP-0009`). This binary-collation unique index is authoritative under concurrent creation.
- **Per-company** filtered unique index on `(TenantId, CompanyId, NormalizedNationalId)` using `Latin1_General_100_BIN2`, filtered to `NormalizedNationalId IS NOT NULL`, so that multiple employees without a recorded national ID are permitted while recorded values stay unique within the company.
- Check constraint limiting `Status` to `Active`, `Inactive`, and `Terminated`.
- Check constraint limiting `StatusChangeReasonCode` to `Created`, `Administrative`, `Operational`, `Compliance`, `Resignation`, `Dismissal`, and `EndOfContract`.
- Check constraint requiring `TerminationDate IS NULL OR TerminationDate >= EmploymentDate` (`BR-HR-0003`).
- Check constraint requiring `Status <> 'Terminated' OR TerminationDate IS NOT NULL`, and `Status = 'Terminated' OR TerminationDate IS NULL`, so status and termination date cannot disagree.
- Check constraints preventing an empty employee number, national ID, or name after trimming where practical.
- Coherent lifecycle-metadata constraints, including a required status-change timestamp.
- Rowversion concurrency token.
- Index on `(TenantId, CompanyId, BranchId, Status, FullName, EmployeeId)` supporting the bounded scoped search and its deterministic ordering, subject to measured query validation. The leading key order matches the mandatory predicate order — tenant, then company, then branch — so that no scoped search can be served by a scan that ignores a scope column.
- Restricted foreign-key deletion behavior (the global `DeleteBehavior.Restrict` convention applies).
- No soft-delete flag and no physical-delete operation; `Terminated` is the terminal retained state. A persistence guard rejects any attempt to physically delete an Employee row, mirroring the existing Company deletion guard.

### `tenant.EmployeeBranchAssignments`

- Primary key on `Id`.
- Required `TenantId` and `CompanyId`.
- Required `EmployeeId` with a **restricted foreign key to `tenant.Employees(EmployeeId)`**.
- **No foreign key from `SourceBranchId` or `DestinationBranchId` to `tenant.Branches`.** Both are retained as opaque identifiers so that history survives unchanged when a branch is deactivated, and so that no modelling path can reinterpret either column as the record's ownership branch. Branch rows are never deleted (`ADR-023`), so referential integrity is not at risk from their absence.
- Check constraint requiring `SourceBranchId IS NULL OR SourceBranchId <> DestinationBranchId`, so a record can never describe a transfer to the branch it came from.
- Check constraint limiting `ReasonCode` to `InitialAssignment`, `Reorganisation`, `OperationalNeed`, `EmployeeRequest`, `BranchClosure`, and `Correction`.
- Check constraint requiring `SourceBranchId IS NOT NULL OR ReasonCode = 'InitialAssignment'`, and `SourceBranchId IS NULL OR ReasonCode <> 'InitialAssignment'`, so the initial record and transfer records cannot be confused.
- Index on `(TenantId, EmployeeId, EffectiveFromUtc, Id)` supporting **employee history lookup** in effective order and **point-in-time branch attribution** (the greatest `EffectiveFromUtc <= T` for one employee), with `Id` as the deterministic tie-break.
- Index on `(TenantId, CompanyId, DestinationBranchId, EffectiveFromUtc)` supporting company-scoped historical branch reporting.
- Restricted foreign-key deletion behavior.
- No physical-delete operation; a persistence guard rejects deletion, and no update path exists (`BRULE-EMP-0018`).

## No cross-database foreign key

No foreign key crosses the platform/tenant database boundary. `tenant.Employees` and `tenant.EmployeeBranchAssignments` reference only tenant-catalog tables.

`UserCompanyAccess` and `UserBranchAccess` remain in the **platform** database and hold no foreign key to `tenant.Companies` or `tenant.Branches` (`ADR-023` decision 4, `ADR-025` decision 5). The existing architecture guard asserting that no migration introduces `principalTable: "Branches"` from the platform stream continues to apply, and an equivalent expectation holds for `Companies`.

## CompanyId and BranchId are independent sibling dimensions

`CompanyId` and `BranchId` are two independent dimensions beneath `TenantId`. Neither is derived from the other, there is no foreign key between `tenant.Companies` and `tenant.Branches`, and **no constraint relates an Employee's company to its branch**. A branch is an operating location of the tenant, not of a company (`ADR-023`).

Carrying `TenantId` alongside both is deliberate: either identifier implies a tenant, but storing `TenantId` preserves the existing global tenant filter and reuses the proven machinery without special cases.

## Normalization and uniqueness

- Normalization is exactly `Trim().ToUpperInvariant()`, with **no** Unicode NFC/NFD/NFKC/NFKD normalization.
- The 64-character limit applies to the normalized value as well as the accepted input; a value whose normalized form exceeds 64 characters is rejected before persistence.
- SQL uniqueness is enforced on the stored normalized columns under `Latin1_General_100_BIN2` (exact binary comparison of the already-uppercased value). The database constraint is authoritative under races; an application pre-check is an optimization, not the authority.
- Employee numbers and national IDs may contain Unicode characters; they are not restricted to ASCII. Display casing is preserved separately.

## Query filters and write guards

`Employee` and `EmployeeBranchAssignment` implement `ITenantOwnedEntity` and therefore automatically receive, through the existing `TenantDbContext` machinery:

- the global tenant query filter (`CurrentTenantId == entity.TenantId`);
- server-side tenant assignment on insert, which requires a trusted current tenant and rejects a mismatched `TenantId`;
- rejection of any post-creation `TenantId` change;
- audit-metadata stamping;
- restricted delete behavior.

`Employee` additionally implements `IBranchOwnedEntity` and therefore enters `ApplyBranchRulesAsync`, which stamps `BranchId` on insert, confirms rather than trusts a supplied value, refuses an unsanctioned `BranchId` modification, and refuses cross-branch modification and deletion.

Both types implement `ICompanyOwnedEntity` and enter the company write boundary introduced by this package (`ADR-025` decision 9): stamp on insert from the trusted company context, confirm rather than trust a supplied value, refuse post-creation change, refuse cross-company modification and deletion.

### No global company or branch query filter

There is **no global company query filter and no global branch query filter**. Only the tenant dimension is filtered globally.

`ADR-025` decision 10 rejects a global company filter because a filter pinned to one company makes authorized multi-company reads unexpressible. `ADR-023` has never defined a global branch filter, for the same reason at the branch level. Company and branch scoping are enforced by **explicit predicates plus executable architecture guards** (`TS-EMP-0110`, `TS-EMP-0111`).

⚠⚠ **CORRECTED 2026-09-01 — THE IDENTIFIER DOES NOT RESOLVE, AND THE CLAIM IS TRUE. THESE ARE DIFFERENT FINDINGS AND ONLY THE FIRST IS A DEFECT.** `TS-EMP-0110` and `TS-EMP-0111` return **zero files in `tests/`**. **The guards they promise DO exist,
under descriptive names:** `EmployeeReadScopeArchitectureTests.No_global_query_filter_scopes_company_or_branch`
(`tests/Architecture.Tests/EmployeeReadScopeArchitectureTests.cs:278`) and
`CompanyOwnershipArchitectureTests.The_company_dimension_adds_no_global_query_filter`
(`tests/Architecture.Tests/CompanyOwnershipArchitectureTests.cs:194`). **Read those names instead of the
scenario ids.**

This is a deliberate divergence from the machinery sketched in `ADR-014` decision 6, superseded and recorded in `ADR-014` revision 1.1, Correction D.

## Shared→Dedicated cutover

`Employee` and `EmployeeBranchAssignment` are tenant-owned, so the model-derived copy plan includes them **by construction** (`ADR-020`, `ADR-023` decision 21). No engine change is required.

The **declared tenant-owned inventory** asserted separately in the architecture tests must be extended deliberately from `["Branch", "Company"]` to include `Employee` and `EmployeeBranchAssignment`. That assertion is designed to fail on any new tenant-owned entity precisely so that copy order, identity keys, and computed columns are decided rather than assumed.

Expected dependency order, principals before dependents:

```
Company
Branch
Employee
EmployeeBranchAssignment
```

`Employee` depends on both `Company` and `Branch`; `EmployeeBranchAssignment` depends on `Employee`. Ordering is derived from foreign keys, so this order follows automatically — but it must be **verified**, not assumed.

`RowVersion` remains excluded from the copy mapping: it is the target database's own concurrency state. `EmployeeBranchAssignment` has no rowversion to exclude.

This is an implementation obligation of FP-006. **No production code or test is modified by this documentation package** (`NFR-EMP-0310`, `AC-EMP-0037` … `AC-EMP-0039`).

## Migration

Proposed migration name, in the tenant stream:

```text
AddHrEmployee
```

The migration creates `tenant.Employees` and `tenant.EmployeeBranchAssignments`, their columns, the per-company unique indexes, the filtered national-ID unique index, the check constraints, the search and history indexes, the rowversion token on `Employees`, and the restricted foreign keys to `tenant.Companies(CompanyId)`, `tenant.Branches(BranchId)`, and `tenant.Employees(EmployeeId)`.

It is tested against an empty database and against the current tenant schema with representative preexisting companies and branches. It creates exactly two tables, backfills no data, adds no column to any existing table, changes no existing identifier, and introduces **no foreign key whose principal table lives in the platform database**.

No migration runs automatically at application startup.

## Prohibited in Milestone 1

`tenant.Employees` and `tenant.EmployeeBranchAssignments` are the only tables introduced. FP-006 creates no department table, no position table, no manager or reporting-line column, no employee-document table, no numbering-sequence table, no import or export staging table, no audit-store table, and no outbox table. It adds no column to any existing table and changes no existing identifier.

The one deliberate change outside these tables is the extension of the **declared** tenant-owned copy inventory in the architecture tests, which is an implementation obligation recorded above and is not a schema change.

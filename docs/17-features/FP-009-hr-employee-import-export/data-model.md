---
document_id: FP-009-DATA
title: HR Employee Import and Export — Data Model
status: Approved for Implementation
version: 1.0
---

# FP-009 — Data Model

> **Two tables.** `OD-DOC-001` split the documents out, so nothing here stores bytes and the storage question
> that dominated the original analysis belongs to FP-010 and `ADR-028`.
>
> Both tables are tenant-owned, which under `DEC-DEP-0029`'s mechanism means every one of them enters the E3 cutover manifest
> **by construction** — and that is where the interesting problem is.

## Conventions inherited without restatement

| Convention | Source |
|---|---|
| `uniqueidentifier` primary keys, non-sequential | `ADR-013` |
| `TenantId` on every row; global query filter plus explicit predicates | `ADR-005`, `ADR-025` d.10 |
| **All persisted application strings are `nvarchar`** | Standing platform rule |
| `rowversion` on any updatable entity; append-only entities carry none | `FP-006` rowversion transport; `EmployeeBranchAssignment` precedent |
| `CreatedUtc`/`CreatedBy`/`ModifiedUtc`/`ModifiedBy` on every table | `BR-PLT-0004` |
| **No foreign key crosses a database boundary** | `ADR-017`, `DEC-EMP-0027` |
| Searchable text needs a persisted normalized column | `DEC-POS-0030`, `DEC-POS-0031` |

## `tenant.EmployeeImportRuns`

| Column | Type | Notes |
|---|---|---|
| `ImportRunId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | Ownership; `CompanyId` FK to `tenant.Companies` |
| `ImportKey` | `nvarchar(128)` | Caller-supplied (`DEC-DOC-0004`) |
| `NormalizedImportKey` | `nvarchar(128)` | Uniqueness and lookup run on this, never on the display value |
| `FileName` | `nvarchar(260)` | Recorded for audit; never used to locate anything |
| `ByteCount`, `RowCount`, `AcceptedCount`, `RejectedCount` | `int` | |
| `Outcome` | `nvarchar(32)` | Enum name, following every other status column in the module |
| `ExecutedUtc` | `datetimeoffset` | |
| `ExecutedBy` | `nvarchar(256)` | |

**Unique index** `UX_EmployeeImportRuns_Company_Key` on `(CompanyId, NormalizedImportKey)` — company-scoped,
the same shape as employee number and department code. **Filtered on nothing**: a key is consumed even by a
refused run, or a failed import could be replayed under the key that was supposed to prevent exactly that.

No `rowversion`: the row is written once and never updated (`DEC-DOC-0006`).

## `tenant.EmployeeExportRuns`

| Column | Type | Notes |
|---|---|---|
| `ExportRunId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `RowCount` | `int` | |
| `ColumnSet` | `nvarchar(1024)` | The ordered column names that left the system (`SEC-DOC-0404`) |
| `ScopeCompanyIds`, `ScopeBranchIds` | `nvarchar(max)` | The materialized scope at execution time, as sorted identifier lists |
| `ExecutedUtc`, `ExecutedBy` | | |

**Why the scope is denormalized into two text columns rather than child tables.** The value is an immutable
historical snapshot, never joined and never filtered on — it exists to be read by a human investigating an
incident. Child tables would add two entities to the E3 manifest and two FK edges to the copy order for data
nothing queries.

## Document tables — transferred to FP-010

`tenant.EmployeeDocuments` and `tenant.EmployeeDocumentContents`, the `binary(32)` content hash, the
separate-content-table reasoning and the explicit refusal of `FILESTREAM`/`FILETABLE` all moved to
[FP-010](../FP-010-hr-employee-documents/carried-analysis.md) under the `OD-DOC-001` split.

## E3 cutover manifest — the movement, and the part that does not move

Under `DEC-DEP-0029`'s mechanism, `TenantCutoverCopyPlan.Build` reflects over the composed model and includes
**every non-owned `ITenantOwnedEntity` with a table name**. There is no list to edit. Adding these tables
therefore moves the manifest **automatically**, and the obligation is to update every site that asserts the
count or the exact set.

| Manifest state | Entities |
|---|---|
| After FP-007 | 7 |
| After FP-008 | 11 |
| **After FP-009** | **13** — `EmployeeImportRuns`, `EmployeeExportRuns` |
| After FP-010, if documents ship | 15 — FP-010's arithmetic, not this package's |

**The site-inventory obligation applies unchanged** (`DEC-POS-0022`, as corrected). The sites are derived by
grep at implementation time, never recalled from this document — the FP-008 correction exists because a
remembered inventory named three sites where nine existed.

> **As built, 2026-08-22.** The grep found **ten** sites, not nine: `DEC-POS-0022`'s table carried nine rows
> plus a prose note recording `EmployeeHostCompositionTests.H9` as a tenth that had sat outside the inventory
> and failed silently for two phases. That note is now a row, and the same grep found the table naming the
> wrong method for site 9 (`PrepareTenantSchemaAsync`, which migrates and asserts nothing, instead of
> `BreakApplicationSchemaAsync`, which carries the `DROP TABLE` list). Both corrections are recorded in
> `DEC-POS-0022` itself, so the next package greps against a table that matches the code.
>
> All ten moved in one act. Neither run record joins the row-bearing set of the cutover fixtures, and unlike
> `Department` and `Position` neither ever can: both joined it when a later phase made an `Employee` column
> required, and no employee column will ever reference a run record.

**Copy order.** Both run tables depend only on `Companies`, so they sort ahead of `Employees` and introduce
no new constraint on the order — and no cycle, because neither points at `Employee` at all. A run record
names *who ran what*, never *which employees resulted*, which is what keeps the copy graph unchanged in shape
as well as valid.

### What does not move — transferred to FP-010

`ADR-020`'s warning about "large objects, and future file/document storage… that a row copy will not move",
the fail-fast obligation, the per-option cutover table and the `ADR-022` backup-custody statement
(`NFR-DOC-0504`, `NFR-DOC-0505`) all belong to
[FP-010](../FP-010-hr-employee-documents/carried-analysis.md#cutover-and-backup-custody).

**Nothing in FP-009 is affected by them**, and that is the substance of the `OD-DOC-001` ruling rather than a
convenience: both tables here are ordinary rows of scalars, and the existing copy path moves them with no new
machinery, no new failure mode and no new promise to the customer.

## Migration obligations

* **Two** new tables: ordinary `CreateTable` migrations, no existing table altered, so the
  add-nullable/backfill/tighten pattern (`DEC-POS-0026` lineage) is **not** needed here.
* **No `defaultValue` on any column**, per the FP-008 review finding: the scaffolder emits them and they
  silently blind data. Required columns are required from the first row.
* `ColumnSet` and the two scope columns are written once and never queried, so they need no index — and
  adding one later costs nothing, which is the test for leaving it out now.
* No migration in this package can fail on existing data, because neither table has any.

---
document_id: FP-009-DATA
title: HR Employee Data Exchange — Data Model
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Data Model

> Three tables under the in-database storage option, two if `OD-DOC-001` splits documents out. Every table is
> tenant-owned, which under `DEC-DEP-0029`'s mechanism means every one of them enters the E3 cutover manifest
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

## `tenant.EmployeeDocuments` *(conditional)*

| Column | Type | Notes |
|---|---|---|
| `DocumentId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `EmployeeId` | `uniqueidentifier` | FK to `tenant.Employees`; **one employee** (`BRULE-DOC-0607`) |
| `DocumentType` | `nvarchar(32)` | Enum name (`DEC-DOC-0012`) |
| `FileName` | `nvarchar(260)` | |
| `NormalizedFileName` | `nvarchar(260)` | Because file name is the only searchable text here, and `DEC-POS-0030` is the reason it needs its own column |
| `ContentType` | `nvarchar(128)` | Allowlisted (`DEC-DOC-0011`) |
| `ByteCount` | `bigint` | |
| `ContentHash` | `binary(32)` | SHA-256; `binary`, not a hex string — it is bytes, and storing it as text doubles it and invites case bugs |
| `ContentLocation` | `nvarchar(512)` | **Null under the in-database option**; the pointer under the others |
| `Status` | `nvarchar(32)` | `Active` \| `Withdrawn` |
| `StatusChangedUtc`, `StatusChangedBy` | | |
| audit + `RowVersion` | | Updatable: status changes |

**Index** `IX_EmployeeDocuments_Employee` on `(TenantId, CompanyId, EmployeeId, Status)` — the shape of the
only query that matters, listing one employee's active documents.

**No unique constraint on `(EmployeeId, FileName)`.** Two documents may legitimately share a name; the same
scanned form uploaded twice under a corrected type is not a conflict to refuse. Deduplication by
`ContentHash` is *reportable*, not enforceable, and inventing a uniqueness rule the business never stated is
exactly what [`business-rules.md`](business-rules.md#rules-this-package-does-not-propose-and-why) declines to
do.

## `tenant.EmployeeDocumentContents` *(in-database option only)*

| Column | Type |
|---|---|
| `DocumentId` | `uniqueidentifier` PK, FK to `EmployeeDocuments` |
| `TenantId` | `uniqueidentifier` |
| `Content` | `varbinary(max)` |

**A separate table, not a column on the metadata row.** Two reasons, and the first is enough: EF Core
materializes every mapped property of an entity it loads, so a `varbinary(max)` on the metadata entity means
every document *listing* reads every byte of every document. `FILESTREAM` and `FILETABLE` are deliberately
not proposed — both bind the schema to filesystem configuration on the host, which is an `ADR-028` decision
and not one a feature package may take by choosing a column type.

## E3 cutover manifest — the movement, and the part that does not move

Under `DEC-DEP-0029`'s mechanism, `TenantCutoverCopyPlan.Build` reflects over the composed model and includes
**every non-owned `ITenantOwnedEntity` with a table name**. There is no list to edit. Adding these tables
therefore moves the manifest **automatically**, and the obligation is to update every site that asserts the
count or the exact set.

| Manifest state | Entities |
|---|---|
| After FP-007 | 7 |
| After FP-008 | 11 |
| **After FP-009, import/export only** | **13** (`EmployeeImportRuns`, `EmployeeExportRuns`) |
| **After FP-009 with documents (in-database)** | **15** (`EmployeeDocuments`, `EmployeeDocumentContents`) |

**The nine-site inventory obligation applies unchanged** (`DEC-POS-0022`, as corrected). The sites are
derived by grep at implementation time, never recalled from this document — the FP-008 correction exists
because a remembered inventory named three sites where nine existed.

**Copy order.** `EmployeeDocuments` depends on `Employees`, which depends on `Departments` and `Positions`;
`EmployeeDocumentContents` depends on `EmployeeDocuments`. The topological sort handles this by construction,
and `C6_15`-style guards assert it. No cycle is introduced: nothing points back at `Employee`.

### The part that does not move — `NFR-DOC-0504`

`ADR-020` names **"large objects, and future file/document storage — out-of-row or out-of-database content
that a row copy will not move"** and requires the tooling to **fail fast** rather than copy an unsupported
object type, because "a silent partial copy is the worst available outcome".

| `OD-DOC-007` option | What cutover does |
|---|---|
| **In-database `varbinary(max)`** | The content table is ordinary table data and copies with everything else. `ADR-020`'s warning is about out-of-row storage the copy misses; a `varbinary(max)` column read and inserted by the copy path is moved. **This must be proven by a test that copies a tenant holding a document and compares content hashes**, not assumed |
| **Filesystem** | Nothing moves. The copy must **detect** that the tenant holds documents and **fail fast** with a message naming the tenant, the document count and the remedy — the `DEC-POS-0026` fail-loud migration is the precedent for the shape |
| **External object store** | Depends on whether the store is addressed per placement. If one store serves both, the pointer moving is sufficient and nothing else is needed; if not, it is the filesystem case. **`ADR-028` must state which** |

### Backup custody — `NFR-DOC-0505`

`ADR-022` §1 attaches backup policy, chain and recovery readiness to the **physical `TenantDatabase`**.

* **In-database** — content is inside the chain. `Protected` means what §6 says it means.
* **Filesystem or external store** — content is **outside** the chain. A tenant reported `Protected` has its
  metadata protected and its bytes protected by something else, or by nothing. Whatever `OD-DOC-007` rules,
  **the readiness vocabulary must not be allowed to imply a guarantee it does not carry** — that is a
  documentation obligation on `ADR-028`, and it is named here so the ruling is made with it in view.

## Migration obligations

* Three or five new tables: ordinary `CreateTable` migrations, no existing table altered, so the
  add-nullable/backfill/tighten pattern (`DEC-POS-0026` lineage) is **not** needed here.
* **No `defaultValue` on any column**, per the FP-008 review finding: the scaffolder emits them and they
  silently blind data. Required columns are required from the first row.
* `ContentHash` as `binary(32)` and `ByteCount` as `bigint` are chosen once — widening a `binary` column
  later is a data migration, and `int` overflows at 2 GB.
* If documents ship under a non-database storage option, the migration is the *smaller* half of the work; the
  content-custody machinery is the larger half and belongs to `ADR-028`.

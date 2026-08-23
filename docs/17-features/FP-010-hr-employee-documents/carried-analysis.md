---
document_id: FP-010-ANALYSIS
title: HR Employee Documents — Carried Analysis
status: Deferred — gated on ADR-028
version: 0.1
---

# FP-010 — Carried Analysis

> The documents material from the FP-009 analysis, moved here by the `OD-DOC-001` split (2026-08-22) and
> **not rewritten**. It was written before `OD-DOC-007` was ruled and it reads that way: where a design choice
> depends on the storage ruling, it says so instead of assuming one.
>
> **This is not a specification.** It is the work already done, kept where the next person will look for it.

## Requirements held here

**`FR-DOC-0301` — Upload a document against an employee.** Content plus metadata: document type
(`DEC-DOC-0012`), file name, content type, byte count, content hash. Refused unless the employee is inside
the caller's scope, the content type is on the allowlist **and** the magic bytes agree with it
(`SEC-DOC-0406`), and the size is within ceiling (`DEC-DOC-0011`).

**`FR-DOC-0302` — List an employee's documents.** Metadata only. Never content, and never a content URL that
would function as one.

**`FR-DOC-0303` — Download document content.** Separately authorized from the listing (`DEC-DOC-0013`),
through a scope type only the content-permission resolver can construct.

**`FR-DOC-0304` — Withdraw a document.** A named `POST`, never a `DELETE` (`DEC-DEP-0024`). Whether
withdrawal destroys the bytes is `OD-DOC-008`; the *route* is the same either way, which is why it can be
specified before that ruling lands.

| | Requirement |
|---|---|
| `SEC-DOC-0405` | Document content is unreachable without the content scope type; a metadata-only caller has no code path to bytes (`DEC-POS-0018` mechanism) |
| `SEC-DOC-0406` | Uploaded content is validated by **magic bytes** as well as declared content type, because the declared type is caller input |
| `NFR-DOC-0504` | **Cutover custody.** Any storage option that leaves binary content outside the tenant database must make the E3 copy **fail fast** for a tenant holding documents, per `ADR-020`. A silent partial copy is the worst available outcome |
| `NFR-DOC-0505` | **Backup custody.** The package states plainly, per option, whether content is inside the physical database's backup chain (`ADR-022` §1). Where it is not, "recovery readiness" does not cover it and the documentation must not imply it does |

## Business rules held here

**`BRULE-DOC-0607` — A document belongs to exactly one employee, and never moves.** There is no
re-association route. A document attached to the wrong employee is withdrawn and re-uploaded, which leaves
both facts in the audit trail rather than rewriting one of them.

*Derivation.* The same reasoning `ADR-024` applies to assignment history: a correction that overwrites is
indistinguishable from the mistake never having happened.

**`BRULE-DOC-0608` — Stored document content is immutable.** Replacing a document is a new document, not a
new body under the same identifier. A metadata row's content hash therefore describes the bytes for as long
as the row exists.

*Derivation.* Required by any content-addressed or externally-stored option in `OD-DOC-007`, and harmless
under the in-database option. Deciding it early keeps the storage ruling from being constrained by a
mutable-content assumption baked into the model.

## Domain model

```
EmployeeDocument (aggregate root)
├── DocumentId              identity
├── TenantId                ITenantOwnedEntity
├── CompanyId               ICompanyOwnedEntity
├── EmployeeId              the one employee it belongs to (BRULE-DOC-0607)
├── DocumentType            closed enum (DEC-DOC-0012)
├── FileName                value object — normalized for search (DEC-POS-0030)
├── ContentType             from the allowlist, verified against magic bytes (SEC-DOC-0406)
├── ByteCount               recorded, not derived at read time
├── ContentHash             SHA-256 of the stored bytes (BRULE-DOC-0608)
├── ContentLocation         opaque to the domain — see below
├── Status                  Active | Withdrawn
├── audit + RowVersion
```

**It is company-owned and NOT branch-owned.** The employee it belongs to is branch-owned; the document is a
fact *about that employee*, and it names no branch of its own. This is exactly the distinction `ADR-024`
decision 4 draws for `EmployeeBranchAssignment` and `DEC-DEP-0001` draws for `Department`: an entity that
belongs to neither of two branches cannot carry a branch predicate, so it inherits its scope from the
employee it describes. **The scoping consequence is the one that matters** — a document read proves the
*employee* is in scope first, then loads documents, precisely as `GetEmployeeBranchHistoryAsync` does.

**`ContentLocation` is opaque to the domain, deliberately.** Whatever `OD-DOC-007` rules — a `varbinary`
column, a filesystem path, an object key — the aggregate holds a reference it does not interpret. A domain
that knows about file paths cannot be tested without a filesystem, and a domain that knows about object keys
has an infrastructure decision compiled into it.

**No `EmployeeDocument` navigation on `Employee`.** The same rule `DEC-DEP-0014` applied to the manager: a
collection navigation invites a caller to reach documents through an employee load, which bypasses the
content permission (`DEC-DOC-0013`). Documents are reached through their own scoped read.

| Event | Carries |
|---|---|
| `EmployeeDocumentUploaded` | Identifiers, document type, byte count. **Never the content, never the file name** — file names carry PII with striking regularity (`passport-scan-layla-haddad.pdf`) |
| `EmployeeDocumentWithdrawn` | Identifiers and the actor |

## Data model

### `tenant.EmployeeDocuments`

| Column | Type | Notes |
|---|---|---|
| `DocumentId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `EmployeeId` | `uniqueidentifier` | FK to `tenant.Employees`; **one employee** (`BRULE-DOC-0607`) |
| `DocumentType` | `nvarchar(32)` | Enum name (`DEC-DOC-0012`) |
| `FileName` | `nvarchar(260)` | |
| `NormalizedFileName` | `nvarchar(260)` | File name is the only searchable text here, and `DEC-POS-0030` is why it needs its own column |
| `ContentType` | `nvarchar(128)` | Allowlisted (`DEC-DOC-0011`) |
| `ByteCount` | `bigint` | `int` overflows at 2 GB |
| `ContentHash` | `binary(32)` | SHA-256; bytes rather than a hex string, which would double it and invite case bugs |
| `ContentLocation` | `nvarchar(512)` | **Null under the in-database option**; the pointer under the others |
| `Status` | `nvarchar(32)` | `Active` \| `Withdrawn` |
| audit + `RowVersion` | | Updatable: status changes |

**Index** `IX_EmployeeDocuments_Employee` on `(TenantId, CompanyId, EmployeeId, Status)` — the shape of the
only query that matters.

**No unique constraint on `(EmployeeId, FileName)`.** Two documents may legitimately share a name.
Deduplication by `ContentHash` is *reportable*, not enforceable, and inventing a uniqueness rule the business
never stated is not this package's to do.

### `tenant.EmployeeDocumentContents` *(in-database option only)*

| Column | Type |
|---|---|
| `DocumentId` | `uniqueidentifier` PK, FK to `EmployeeDocuments` |
| `TenantId` | `uniqueidentifier` |
| `Content` | `varbinary(max)` |

**A separate table, not a column on the metadata row.** EF Core materializes every mapped property of an
entity it loads, so a `varbinary(max)` on the metadata entity means every document *listing* reads every byte
of every document. `FILESTREAM` and `FILETABLE` are deliberately not proposed — both bind the schema to
filesystem configuration on the host, which is an `ADR-028` decision and not one a feature package may take
by choosing a column type.

### Cutover and backup custody

E3 manifest arithmetic: FP-009 leaves it at **13**; documents would take it to **15** under the in-database
option. Entry is automatic (`DEC-DEP-0029` reflects over the composed model), and the **nine-site inventory
obligation** (`DEC-POS-0022`, as corrected) applies unchanged — derived by grep at implementation time, never
recalled from a document.

Copy order: `EmployeeDocuments` depends on `Employees`, which depends on `Departments` and `Positions`;
`EmployeeDocumentContents` depends on `EmployeeDocuments`. No cycle — nothing points back at `Employee`.

**What does not move.** `ADR-020` names "large objects, and future file/document storage — out-of-row or
out-of-database content that a row copy will not move" and requires fail-fast.

| `OD-DOC-007` option | What cutover does |
|---|---|
| **In-database `varbinary(max)`** | The content table is ordinary table data and copies with everything else. **This must be proven by a test that copies a tenant holding a document and compares content hashes**, not assumed |
| **Filesystem** | Nothing moves. The copy must **detect** that the tenant holds documents and **fail fast**, naming the tenant, the document count and the remedy — `DEC-POS-0026`'s fail-loud migration is the precedent for the shape |
| **External object store** | Depends on whether the store is addressed per placement. If one store serves both, the pointer moving is sufficient; if not, it is the filesystem case. **`ADR-028` must state which** |

## Lifecycle

```
        upload                withdraw
(none) ────────▶ Active ──────────────▶ Withdrawn
                   ▲                        │
                   └──────── ✗ ─────────────┘
                     no reinstatement route
```

**`Active` → `Withdrawn` is one-way, and there is no reinstate operation.** Every other lifecycle in HR is
reversible, so the difference needs a reason rather than an assumption. The reason is `OD-DOC-008`: if
withdrawal destroys content, reinstatement is *impossible* rather than merely disallowed, and a route that
exists but fails for half of all rows is worse than one that does not exist. If soft-only is ruled,
reinstatement becomes possible and can be added — an additive change with no contract break.

**Withdrawn metadata always survives.** Whatever happens to the bytes, who uploaded a document and who
withdrew it is audit data under `BR-PLT-0004`; destroying it would erase the record of the erasure.

| Employee event | Effect |
|---|---|
| **Terminated** | Documents untouched. A terminated employee's contract is exactly the document most likely to be needed afterwards |
| **Transfers branches** | No effect. The document names no branch; scope is inherited from the employee, so visibility follows automatically |
| **Changes department or position** | No effect. Classification is not custody |

## Authorization

| Permission | Governs |
|---|---|
| `HR.EmployeeDocuments.View` | `FR-DOC-0302` — metadata listing |
| `HR.EmployeeDocuments.Download` | `FR-DOC-0303` — content |
| `HR.EmployeeDocuments.Upload` | `FR-DOC-0301` |
| `HR.EmployeeDocuments.Withdraw` | `FR-DOC-0304` |

```
EmployeeDocumentScope         ← resolver checked HR.EmployeeDocuments.View
EmployeeDocumentContentScope  ← resolver checked HR.EmployeeDocuments.Download
```

`GetContentAsync` takes an `EmployeeDocumentContentScope` and nothing else, so a metadata-only caller cannot
reach content by any route — including a future one written by someone who never read this document, which
is the point of encoding it as a type rather than as a rule.

**Both scopes derive from the employee's scope**, not from a document scope of their own: the read proves the
employee is in scope first and returns not-found if not. Without that step, a document read keyed by
`DocumentId` would be an unscoped read of employee data.

| Condition | Answer |
|---|---|
| Employee outside scope | `404 employee.not_found` |
| Document unknown or out of scope | `404 employee_document.not_found` |
| Content requested without the download permission | `403 authorization.forbidden` — never `404`: the caller can already see the document exists |

## Contracts

| Method | Route | Permission | Requirement |
|---|---|---|---|
| `POST` | `/api/hr/employees/{employeeId}/documents` | `HR.EmployeeDocuments.Upload` | `FR-DOC-0301` |
| `GET` | `/api/hr/employees/{employeeId}/documents` | `HR.EmployeeDocuments.View` | `FR-DOC-0302` |
| `GET` | `/api/hr/employee-documents/{documentId}/content` | `HR.EmployeeDocuments.Download` | `FR-DOC-0303` |
| `POST` | `/api/hr/employee-documents/{documentId}/withdraw` | `HR.EmployeeDocuments.Withdraw` | `FR-DOC-0304` |

**Why two prefixes.** Upload and list hang off the employee, because that is the only way to address "this
person's documents". Content and withdrawal hang off the document identifier, which is already unique;
repeating the employee in the path would create a mismatched pair to validate and a mismatch to answer.

Upload response `201` with the metadata representation — `documentId`, `employeeId`, `documentType`,
`fileName`, `contentType`, `byteCount`, `contentHash`, `status`, `uploadedUtc`, `uploadedBy`, `rowVersion`.

**No content URL in the representation.** A field holding a link to the bytes would be a second
authorization surface — one that outlives the response, travels in logs, and is checked by whatever code
happens to serve it. Content is fetched from the content route, which resolves the content scope every time.

| Condition | Status | Code |
|---|---|---|
| Document exceeds the size ceiling | `400` | `employee_document.too_large` |
| Content type not allowlisted, or bytes disagree with it | `400` | `employee_document.content_type_rejected` |
| Document unknown or out of scope | `404` | `employee_document.not_found` |
| Withdrawing an already-withdrawn document | `409` | `employee_document.transition_invalid` — `DEC-DEP-0030` |

## Criteria and scenarios held here

| | Criterion |
|---|---|
| `AC-DOC-0017` | **Content type is verified against the bytes.** A PDF renamed `.png` and declared `image/png` is refused |
| `AC-DOC-0018` | **Metadata visibility does not grant content.** `View` without `Download` can list and cannot obtain bytes through any route; the refusal is `403`, not `404` |
| `AC-DOC-0019` | **Document reads inherit the employee's scope.** A document belonging to an out-of-scope employee answers `404`, identically whether it exists, belongs to another company, or never existed |
| `AC-DOC-0020` | **Withdrawal is one-way and metadata survives it.** A second withdrawal is `409`; uploader and timestamp remain readable regardless of what happened to the bytes |

| | Layer | Scenario |
|---|---|---|
| `TS-DOC-0019` | H | Upload a PDF renamed `.png` declared `image/png` → `400 employee_document.content_type_rejected` |
| `TS-DOC-0020` | H | `View` without `Download` lists a document then requests content → `403`, and the metadata response contains no URL serving as an alternative route to the bytes |
| `TS-DOC-0021` | S | A document belonging to an out-of-scope employee → `404`, byte-identical to the answer for one that never existed |
| `TS-DOC-0022` | A | Withdraw twice → `409`; uploader and timestamp still readable |
| `TS-DOC-0023` | **S** | **Cutover custody.** A tenant holding one document is copied Shared→Dedicated. In-database: the content table copies and hashes match. Any other option: the copy **fails fast**, naming the tenant and the document count, and writes nothing at the destination |
| `TS-DOC-0024` | S | **Manifest arithmetic.** The E3 manifest count and exact set match the new inventory, and the order places `Employees` before `EmployeeDocuments` before `EmployeeDocumentContents` |

**`TS-DOC-0023` is the one that cannot be deferred with the rest.** Whatever `OD-DOC-007` rules, this
scenario has to exist and pass before a single document is stored in production — under the in-database
option it proves content survives cutover, and under every other option it proves the platform refuses
loudly instead of losing bytes quietly.

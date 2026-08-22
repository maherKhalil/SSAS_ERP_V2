---
document_id: FP-009-DEC
title: HR Employee Data Exchange — Decisions and Classification
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Decisions and Classification

> **Nothing in this document is approved.** Every topic below carries one of three classifications, and the
> classification is the point: it says whether a question is already answered elsewhere, answered here by
> engineering and awaiting ratification, or not ours to answer.
>
> | Classification | Meaning |
> |---|---|
> | **SETTLED-BY-PRECEDENT** | An existing ADR or ratified decision already answers this. The citation is given. Re-deciding it here would be a second opinion that could disagree with shipped code |
> | **PROPOSED** | An engineering decision, made here, ratifiable as drafted. It is labeled so nobody mistakes it for precedent |
> | **OWNER-DECISION-REQUIRED** | A business, product or compliance call. Raised, never made |
>
> **Nothing is settled by resemblance.** Where FP-006/007/008 did something similar but not the same, the
> difference is stated rather than smoothed over.

## Classification table

### Import

| # | Topic | Classification | Basis |
|---|---|---|---|
| 1 | File format | **PROPOSED** `DEC-DOC-0001` | No precedent — no file has ever crossed this boundary |
| 2 | Header and column contract | **PROPOSED** `DEC-DOC-0002` | Extends the strict-JSON convention (`FP-006` api-contracts) to a new medium |
| 3 | Validation-report shape | **PROPOSED** `DEC-DOC-0003` | ProblemDetails precedent does not reach a per-row report |
| 4 | Atomicity — partial vs all-or-nothing | **OWNER-DECISION-REQUIRED** `OD-DOC-003` | A business call about what HR wants to happen to 998 good rows |
| 5 | Create-only vs upsert | **OWNER-DECISION-REQUIRED** `OD-DOC-002` | Changes which permission the operation needs and what it can destroy |
| 6 | Idempotency / re-run semantics | **PROPOSED** `DEC-DOC-0004` | Depends on `OD-DOC-002` and `OD-DOC-003`; the mechanism is engineering, the policy is not |
| 7 | Resolving department / position / branch | **OWNER-DECISION-REQUIRED** `OD-DOC-004` | Whether a file may create organizational structure is not an engineering question |
| 8 | Import permission | **OWNER-DECISION-REQUIRED** `OD-DOC-005` | Sensitivity judgement; engineering recommendation recorded |
| 9 | Size and row caps | **PROPOSED** `DEC-DOC-0005` | Bounded-read precedent (`FP-006`, page size ≤ 200) extends naturally |
| 10 | Audit trail of who imported what | **PROPOSED** `DEC-DOC-0006` | `BR-PLT-0004` requires audit; the shape is engineering |
| 11 | Tenant/company/branch ownership of import records | **SETTLED-BY-PRECEDENT** | `ADR-023`, `ADR-025`; `ICompanyOwnedEntity` stamping |
| 12 | Synchronous vs asynchronous execution | **PROPOSED** `DEC-DOC-0007` | Follows from the row cap; revisitable without contract change |

### Export

| # | Topic | Classification | Basis |
|---|---|---|---|
| 13 | **Export runs under the caller's employee read scope** | **SETTLED-BY-PRECEDENT** | `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029`. Stated below in full because it is the one thing that must not be reopened |
| 14 | Export format | **PROPOSED** `DEC-DOC-0008` | Symmetry with import |
| 15 | PII surface — `nationalId` in an export | **OWNER-DECISION-REQUIRED** `OD-DOC-006` | Compliance call; `DEC-EMP-0030` is the sensitivity precedent, not the answer |
| 16 | Export permission | **OWNER-DECISION-REQUIRED** `OD-DOC-005` | Same decision as import, taken once |
| 17 | Terminated employees in exports | **PROPOSED** `DEC-DOC-0009` | The search default (`FP-006` api-contracts) extends; the exception is stated |
| 18 | Export bounded by the same paging maxima | **SETTLED-BY-PRECEDENT** | `FP-006` api-contracts — "reads are bounded"; an export is a read |
| 19 | Audit trail of who exported what | **PROPOSED** `DEC-DOC-0006` | Same record as import; an export is the higher-risk half |

### Documents *(recorded even if `OD-DOC-001` splits them out — the doors must be documented before they are chosen)*

| # | Topic | Classification | Basis |
|---|---|---|---|
| 20 | Binary storage location | **OWNER-DECISION-REQUIRED** `OD-DOC-007` | Cost, custody and compliance; drives `ADR-028` |
| 21 | `ADR-028` required | **PROPOSED — YES** `DEC-DOC-0010` | Binds every future module; not a feature-level choice |
| 22 | Metadata is a tenant-owned entity in the E3 manifest | **SETTLED-BY-PRECEDENT** | `ADR-005` names Attachment Metadata; `DEC-DEP-0029` makes manifest entry automatic |
| 23 | Binary content is **not** moved by the E3 row copy | **SETTLED-BY-PRECEDENT** | `ADR-020` — "large objects, and future file/document storage… a row copy will not move"; fail fast |
| 24 | Size ceiling and content-type allowlist | **PROPOSED** `DEC-DOC-0011`, subject to `ADR-028` raising them to platform constraints |
| 25 | Document-type taxonomy | **PROPOSED** `DEC-DOC-0012` | An HR question with an obvious V1 answer; not an owner call unless the owner has a list |
| 26 | Retention and deletion vs no-physical-delete | **OWNER-DECISION-REQUIRED** `OD-DOC-008` | `BR-PLT-0003` says never delete; erasure law may say otherwise |
| 27 | Document permissions | **PROPOSED** `DEC-DOC-0013` | Follows `DEC-POS-0018`'s scope-type mechanism |
| 28 | Roadmap ownership vs V5 Document Management | **OWNER-DECISION-REQUIRED** `OD-DOC-009` | Product sequencing |
| 29 | Malware scanning | **OWNER-DECISION-REQUIRED** — deferred into `ADR-028` | Named here so it is not discovered late |

### Cross-cutting

| # | Topic | Classification | Basis |
|---|---|---|---|
| 30 | Concurrency — rowversion on every mutation | **SETTLED-BY-PRECEDENT** | `Development-Standards.md`; `FP-006` rowversion transport |
| 31 | Lifecycle — named `POST` routes, no `DELETE` verb | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0024` |
| 32 | Problem codes in a per-resource namespace | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0026` |
| 33 | State-conflict refusals answer `409` | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0030` |
| 34 | Uniqueness normalized, scoped per company | **SETTLED-BY-PRECEDENT** | `DEC-EMP` employee-number pattern; `DEC-DEP` code pattern |
| 35 | Searchable text needs a normalized column | **SETTLED-BY-PRECEDENT** | `DEC-POS-0030`, `DEC-POS-0031` |
| 36 | Persisted application strings are `nvarchar` | **SETTLED-BY-PRECEDENT** | Standing platform rule |
| 37 | No cross-database foreign key | **SETTLED-BY-PRECEDENT** | `ADR-017`, `DEC-EMP-0027` |
| 38 | E3 manifest movements and the **nine-site** inventory | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0029`, `DEC-POS-0022` — the mechanism is reflection, the obligation is to update every dependent site |
| 39 | Migration obligations for required columns on populated tables | **SETTLED-BY-PRECEDENT** | `DEC-POS-0026` lineage — add nullable, backfill, tighten; never `defaultValue` |
| 40 | Module isolation — HR references only BuildingBlocks | **SETTLED-BY-PRECEDENT** | `ADR-012`, `DEC-DEP-0026` |

**Forty topics: 15 settled by precedent, 16 proposed, 9 owner decisions.**

---

## Settled by precedent — the two worth writing out

**Export runs under the caller's employee read scope, and this is not open.** `ADR-023` decision 22 and
`ADR-025` decision 10 require every employee read to carry a materialized scope; `DEC-EMP-0029` implements it
as a type (`EmployeeReadScope`) that only the resolver can construct. An export is a read — the widest one
the module will ever perform — so it obtains a scope exactly as search does, and a caller authorized for one
branch exports one branch. **A "full export" mode that bypasses scope would contradict every decision since
`ADR-023` and is not on the table at any classification.** If a tenant-wide extract is ever needed, it is a
platform operation with its own authority model, not an HR route.

**Binary content does not move during Shared→Dedicated cutover, and the platform already knows it.**
`ADR-020` lists "large objects, and future file/document storage — out-of-row or out-of-database content that
a row copy will not move" among the object types the tooling must account for, and requires it to **fail
fast** where an object type is unsupported. The consequence for `OD-DOC-007` is concrete: any storage option
that puts bytes outside the tenant database makes every document-holding tenant un-cutoverable until
`ADR-028` says what moves them. That is not an argument against those options — it is the work they carry.

---

## Proposed decisions

**`DEC-DOC-0001` — Import accepts UTF-8 CSV only in V1.** Comma-delimited, `CRLF` or `LF`, RFC 4180 quoting,
a UTF-8 BOM tolerated and stripped. **XLSX is rejected** — it is a zip container requiring a parsing
dependency with its own vulnerability surface, and the requirement says "import", not "import from
spreadsheets". The response to an unsupported format is `400`, naming CSV.

**`DEC-DOC-0002` — The header row is a strict contract, order-independent.** Column names are matched
case-insensitively against a fixed set; a **missing required column** and an **unrecognized column** are both
refusals, before any row is read. This is the strict-JSON rule (`FP-006` api-contracts: unknown fields are
`400 request.invalid`) applied to a new medium, and for the same reason — a silently ignored column is a
false belief about what was imported.

**`DEC-DOC-0003` — The validation report is per row, and every row is validated.** Not first-failure. A
report naming one bad row in a thousand costs the operator a thousand round trips to find the rest. The shape
is a list of `{ rowNumber, column, code, message }`, where `code` is a problem code from the module's own
namespace, and `rowNumber` is the **1-based line number in the submitted file including the header**, because
that is what the operator's editor shows them.

**`DEC-DOC-0004` — Idempotency by explicit import key.** The caller supplies an `importKey` (a client-chosen
identifier, unique per company); a second submission carrying a key already recorded returns the **original**
run's result rather than importing again. Without this, a timeout on a 5,000-row import leaves the operator
unable to tell whether to retry. The mechanism is engineering; **what a re-run means for rows that already
exist depends on `OD-DOC-002` and `OD-DOC-003`.**

**`DEC-DOC-0005` — Caps: 5,000 rows and 10 MB per file.** Aligned with the platform's bounded-read posture
(`FP-006` caps a page at 200) and with `DEC-DOC-0007`'s synchronous execution. Both are configuration values,
not architectural constants; exceeding either is `400` and names the limit and the actual size.

**`DEC-DOC-0006` — Every import and every export writes a durable run record.** `EmployeeImportRun` and
`EmployeeExportRun`: who, when, which company, the file name and byte count, the row counts (submitted,
accepted, rejected), the outcome, and — for exports — **the row count and the column set that left the
system**. `BR-PLT-0004` requires an audit trail; for export it is the *only* control that survives the data
leaving. Both are append-only and never updated.

**`DEC-DOC-0007` — Import and export execute synchronously in V1.** Within the caps above this is a
sub-second to few-second operation, and an asynchronous job introduces a queue, a status route, a retention
policy for results and a failure mode where the caller never learns the outcome. When the caps rise, this is
revisited — the contract is written so an async variant is additive.

**`DEC-DOC-0008` — Export produces UTF-8 CSV with the same column contract import accepts.** The
round-trip property is the point: an export, edited and re-imported, must be a legal import. Where
`OD-DOC-006` removes a column from exports, that column is *optional* on import, so the property holds.

**`DEC-DOC-0009` — Exports exclude terminated employees unless explicitly requested.** The employee search
already behaves this way (`FP-006` api-contracts: "search defaults to excluding Terminated"), and an export
is a search that leaves the building. `status=Terminated` includes them by name, so audit and payroll
extracts remain possible.

**`DEC-DOC-0010` — `ADR-028` is required if documents ship in any form.** Scope named in
[`README.md`](README.md#adr-028--required-and-what-it-would-own).

**`DEC-DOC-0011` — Content-type allowlist and a 10 MB ceiling per document.** PDF, PNG, JPEG and plain text
in V1; everything else refused by content type **and** by magic-byte inspection, because a declared
content-type is caller input. `ADR-028` may raise these to platform constraints; until it exists they are
feature-level and conservative.

**`DEC-DOC-0012` — Document type is a closed enum in V1** — `Contract`, `Identification`, `Certificate`,
`Correspondence`, `Other` — persisted as its string name, following every other status/reason enum in the
module. A customer-defined taxonomy is a V5 Document Management concern (`OD-DOC-009`).

**`DEC-DOC-0013` — Document metadata and document content carry different permissions.** Listing an
employee's documents is `HR.EmployeeDocuments.View`; **downloading content** is a separate authority, and the
mechanism is `DEC-POS-0018`'s: a distinct scope type that only the resolver checking the content permission
can construct, so a metadata-only caller cannot reach content through any code path. Uploading is
`HR.EmployeeDocuments.Upload`.

---

## Owner decisions required

**`OD-DOC-001` — Packaging.** Stated in full in [`README.md`](README.md#od-doc-001--the-packaging-question-raised-first),
with both options, their consequences and a labeled engineering recommendation (Option B).

**`OD-DOC-002` — Does import create only, or also update existing employees?**

| Option | Consequence |
|---|---|
| **Create-only** | An import naming an existing employee number is a rejected row. Simplest; needs only creation authority; cannot destroy data |
| **Upsert** | The file becomes an instrument of bulk *modification*. It needs update authority as well, and a mis-keyed column can overwrite a thousand records at once |

**Why it is not ours.** "Import employees" reads naturally as either. The difference is what an operator can
destroy with one mistake, and how much of the reversal burden falls on backups.

**`OD-DOC-003` — All-or-nothing, or partial success?**

| Option | Consequence |
|---|---|
| **All-or-nothing** | 999 valid rows are rejected because of one bad row. Predictable: the import either happened or it did not, and re-running after a fix is unambiguous |
| **Partial success** | 999 rows land; the operator fixes one and re-submits. Faster in practice, but the system is now in a state no single file describes, and re-running the corrected file must not duplicate the 999 (see `DEC-DOC-0004`) |

**Engineering note, not a recommendation:** all-or-nothing is materially simpler to make correct, because it
needs no idempotency reasoning about partially applied files. Partial success is what most operators expect.

**`OD-DOC-004` — How do `department`, `position` and `branch` resolve from file values, and may an import
create a missing one?**

Every employee requires all three (`BR-HR-0005`, `BR-HR-0006`, `ADR-023`). A file must name them somehow.

| Sub-question | Options |
|---|---|
| **By what** | By **code** (`FIN`, human-readable, unique per company) or by **identifier** (a GUID nobody types) |
| **Missing referent** | Reject the row, or **create** the department/position on the fly |

**Engineering recommendation (labeled):** resolve **by code**, and **never create**. Creating organizational
structure from a spreadsheet would let an import invent a department that no one approved, and the typo
`FINANCE`/`FINANCE ` becomes a permanent org unit. Rejection is recoverable; invention is not.

**`OD-DOC-005` — Are import and export separately-granted permissions?**

| Option | Consequence |
|---|---|
| **Reuse `HR.Employees.Create` / `.View`** | Anyone who may add one employee may add five thousand; anyone who may view an employee may extract the whole authorized set to a file |
| **Separate `HR.Employees.Import` / `.Export`** | Two more permissions to administer, and the sensitive capability can be granted to the few people who need it |

**Engineering recommendation (labeled): separate.** The precedent is `DEC-DEP-0025`'s reasoning —
`Deactivate` was separated from `Update` because it changes a materially different thing. Bulk creation and
bulk extraction are materially different from single-record work, and export in particular is the only
operation in the module that takes data *out* of it.

**`OD-DOC-006` — May `nationalId` leave the system in an export?**

`DEC-EMP-0030` already treats the national identifier as the module's sensitive field: it is on the employee
detail and deliberately **not** on the search list row. An export is closer to a list than to a detail, and
it persists outside the system's control the moment it is downloaded.

| Option | Consequence |
|---|---|
| **Never** | Exports cannot serve payroll or statutory filing, which are the usual reasons to export |
| **Always** | The most sensitive field in the module is one route away from a spreadsheet on a laptop |
| **Only under a distinct permission** | Two export shapes to specify and test; the capability exists where it is needed and nowhere else |

**This is a compliance decision, and the applicable law depends on the deployment.** It is raised, not
answered. Whatever is ruled, `DEC-DOC-0006`'s run record captures which column set left the system.

**`OD-DOC-007` — Where does binary content live?**

| Option | Cutover (`ADR-020`) | Backup (`ADR-022`) | Other consequences |
|---|---|---|---|
| **In-database `varbinary(max)`** | **Moves with the row copy** — the only option that does. It is table data, ordered by the same FK rules | **Covered by the physical database's chain** — `ADR-022` §1 attaches backup to the database, so content is protected by construction | Database size grows with file volume; backup windows and restore times grow with it; a 10 MB ceiling matters |
| **Filesystem beside the database** | **Does not move.** `ADR-020` requires fail-fast; cutover of a document-holding tenant is blocked until `ADR-028` specifies a content move step | **Not covered.** The backup chain protects the database; the files need an independent, separately verified backup, and "recovery readiness" no longer means what `ADR-022` §6 says it means | Cheapest storage; hardest custody story; multi-node hosting needs shared storage |
| **External object store** | **Does not move** — but the pointer does, and if the store is shared across placements the content may not need to move at all. That is exactly the kind of thing an ADR must state rather than assume | **Not covered** by the database chain; the store has its own durability guarantees, which must be reconciled with `ADR-022`'s readiness model | Adds an external dependency and a credential-custody problem (`ADR-022` §11: keys never in the Platform database); the natural long-term answer |

**Engineering note (labeled, not a recommendation):** in-database storage is the only option under which the
platform's *existing* cutover and backup guarantees continue to hold with no new machinery. It is also the
option that scales worst. Whichever is ruled, `ADR-028` is what records it.

**`OD-DOC-008` — Retention and erasure, against `BR-PLT-0003`.**

The platform does not physically delete (`BR-PLT-0003`, and `ADR-022` §16 — the platform does not delete
backups in V1). An employee document may be subject to a legal erasure obligation, and a soft-deleted row
still holds the bytes. Backups hold them for as long as the retention policy says.

| Option | Consequence |
|---|---|
| **Soft delete only** | Consistent with every other entity. Erasure obligations are not met, and the platform should say so plainly rather than imply otherwise |
| **Physical destruction of content, metadata retained** | The audit trail survives — who uploaded what and when — while the bytes go. Backups still hold them until they age out, which must be stated to the customer rather than papered over |
| **Full erasure including backups** | Requires backup-chain surgery that `ADR-022` explicitly does not do in V1 |

**This is legal and contractual before it is technical.** Raised.

**`OD-DOC-009` — Does V5 Document Management own this?**

The Product Roadmap places **Document Management at Version 5**. If employee documents ship now, either they
are a deliberate stopgap that V5 replaces — with a migration to specify — or V5's scope shrinks to exclude
what HR already has.

| Option | Consequence |
|---|---|
| **Build now as a stopgap** | HR gets documents years earlier. A migration into the V5 capability must be planned, and V5 inherits data shaped by a feature package rather than by its own design |
| **Wait for V5** | `REQ-HR-0005` stays unimplemented and openly deferred, exactly as `DEC-EMP-0032` already deferred it once. No stopgap to migrate |
| **Build now, and V5 is scoped around it** | Requires a product commitment made now about a version far away |

**This decision and `OD-DOC-001` are linked**: if V5 owns documents, Option B in `OD-DOC-001` is not merely
tidier — it is the only coherent answer.

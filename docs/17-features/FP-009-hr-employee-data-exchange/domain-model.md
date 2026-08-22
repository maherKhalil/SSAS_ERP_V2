---
document_id: FP-009-DOM
title: HR Employee Data Exchange — Domain Model
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Domain Model

> Three candidate entities, and the more useful half of this document is what is **not** an entity. Import
> and export are *operations*, not aggregates; only their records persist.

## What is an aggregate here

| Candidate | Verdict | Reason |
|---|---|---|
| `EmployeeImportRun` | **Entity, append-only** | A durable fact about something that happened (`DEC-DOC-0006`). It has no lifecycle after it is written and no invariant to protect, so it is an entity rather than an aggregate root with behaviour |
| `EmployeeExportRun` | **Entity, append-only** | Same shape, higher stakes: it is the only record that data left the system |
| `EmployeeDocument` | **Aggregate root** *(conditional on `OD-DOC-001`/`OD-DOC-007`)* | It has state (`Active`, `Withdrawn`), an invariant (content immutability, `BRULE-DOC-0608`), and a lifecycle operation (`Withdraw`) |
| "Import" as an aggregate | **No** | An import is a use case that composes `Employee.Create` N times. Modelling it as an aggregate would create a second place where an employee can come into existence |
| "Export" as an aggregate | **No** | An export is a read |

## `EmployeeDocument`

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
has an infrastructure decision compiled into it. Under the in-database option the content column lives on a
**separate table** keyed by `DocumentId` (see [`data-model.md`](data-model.md)) so that metadata reads never
drag megabytes into memory.

**No `EmployeeDocument` navigation on `Employee`.** The same rule `DEC-DEP-0014` applied to the manager and
`DEC-POS` applied to grades: a collection navigation invites a caller to reach documents through an employee
load, which bypasses the content permission (`DEC-DOC-0013`). Documents are reached through their own scoped
read.

## `EmployeeImportRun`

```
EmployeeImportRun (entity, append-only)
├── ImportRunId, TenantId, CompanyId
├── ImportKey               the caller's idempotency key (DEC-DOC-0004), unique per company
├── FileName, ByteCount, RowCount
├── AcceptedCount, RejectedCount
├── Outcome                 Validated | Applied | Refused
├── ExecutedUtc, ExecutedBy
```

**Why the key is on the run rather than in a separate table.** The run *is* the record of the key having been
used; a second table would let the two disagree. Uniqueness is `(CompanyId, ImportKey)` — the same
company-scoped uniqueness shape as employee number and department code, and for the same reason: two
companies in one tenant are not obliged to coordinate their key choices.

**Rejected rows are reported, not persisted.** The report is the response (`DEC-DOC-0003`); the run keeps
counts. Persisting every rejected row would mean storing rejected PII indefinitely, which is a worse outcome
than making the operator keep their own file.

## `EmployeeExportRun`

```
EmployeeExportRun (entity, append-only)
├── ExportRunId, TenantId, CompanyId
├── RowCount
├── ColumnSet               what actually left the system (SEC-DOC-0404)
├── ScopeSummary            the company and branch sets the scope resolved to
├── ExecutedUtc, ExecutedBy
```

**`ScopeSummary` is what makes the record answer the question that will actually be asked.** "Who exported
employee data?" is answerable from the actor alone; "could that person have exported *this* employee?" is
not, unless the scope in force at the time is recorded. Scope changes over time — `AC-EMP-0026` and
`AC-EMP-0024` exist because it does — so reconstructing it later from current authorization is unsound.

## Domain events

| Event | Raised by | Carries |
|---|---|---|
| `EmployeeDocumentUploaded` | `FR-DOC-0301` | Identifiers, document type, byte count. **Never the content, never the file name** — file names carry PII with striking regularity (`passport-scan-layla-haddad.pdf`) |
| `EmployeeDocumentWithdrawn` | `FR-DOC-0304` | Identifiers and the actor |
| `EmployeesImported` | `FR-DOC-0102` | Counts and the run identifier. **Not the rows** |
| Export | **none** | An export changes no state. The run record is written directly; a domain event announcing a read would be a notification, not a domain fact |

The reason-text exclusion follows `DEC-POS`'s and `ADR-024`'s handling of free text on events: what rides on
an event is what a subscriber may act on, and free text is not that.

## Relationship to existing aggregates

| Aggregate | Relationship |
|---|---|
| `Employee` | Import creates them through the existing command handler. Export reads them through the existing scoped read. Documents point at one by identifier, with no navigation in either direction |
| `Department`, `Position` | Referenced by import rows for resolution only (`OD-DOC-004`). Never created, never modified (`BRULE-DOC-0601`) |
| `EmployeeBranchAssignment` | Written by the ordinary create path when an import creates an employee — the initial assignment is not special-cased for imports |

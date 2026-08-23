---
document_id: FP-009-DOM
title: HR Employee Import and Export — Domain Model
status: Approved for Implementation
version: 1.0
---

# FP-009 — Domain Model

> **Approved 2026-08-22.** Two entities, and the more useful half of this document is what is **not** one:
> import and export are *operations*, not aggregates, and only their records persist.
>
> `EmployeeDocument` was the third candidate. It travelled to
> [FP-010](../FP-010-hr-employee-documents/) under the `OD-DOC-001` split, together with the reasoning about
> its ownership dimensions and its opaque content reference.

## What is an aggregate here

| Candidate | Verdict | Reason |
|---|---|---|
| `EmployeeImportRun` | **Entity, append-only** | A durable fact about something that happened (`DEC-DOC-0006`). It has no lifecycle after it is written and no invariant to protect, so it is an entity rather than an aggregate root with behaviour |
| `EmployeeExportRun` | **Entity, append-only** | Same shape, higher stakes: it is the only record that data left the system |
| `EmployeeDocument` | **Transferred to FP-010** | It was the only real aggregate of the three — state, an invariant and a lifecycle operation — and it left with the documents material |
| "Import" as an aggregate | **No** | An import is a use case that composes `Employee.Create` N times. Modelling it as an aggregate would create a second place where an employee can come into existence |
| "Export" as an aggregate | **No** | An export is a read |

## `EmployeeDocument` — transferred to FP-010

Its model, its ownership analysis (company-owned, not branch-owned, scope inherited from the employee), the
opaque `ContentLocation`, and the decision to keep no navigation from `Employee` all moved to
[FP-010](../FP-010-hr-employee-documents/carried-analysis.md) unchanged.

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
| `EmployeesImported` | `FR-DOC-0102` | Counts and the run identifier. **Not the rows** |
| Export | **none** | An export changes no state. The run record is written directly; a domain event announcing a read would be a notification, not a domain fact |

The row exclusion follows `DEC-POS`'s and `ADR-024`'s handling of free text on events: what rides on an event
is what a subscriber may act on. Five thousand employee records on a message bus are not that, and under
`OD-DOC-003` the count alone is complete information — an `EmployeesImported` event means every row applied,
because no other outcome writes anything.

*(The two document events the analysis proposed travelled to FP-010 with their aggregate.)*

## Relationship to existing aggregates

| Aggregate | Relationship |
|---|---|
| `Employee` | Import creates them through the existing command handler. Export reads them through the existing scoped read. Documents point at one by identifier, with no navigation in either direction |
| `Department`, `Position` | Referenced by import rows for resolution **by code, under the importer's own authority** (`OD-DOC-004`, ruled). Never created, never modified (`BRULE-DOC-0601`) |
| `EmployeeBranchAssignment` | Written by the ordinary create path when an import creates an employee — the initial assignment is not special-cased for imports |

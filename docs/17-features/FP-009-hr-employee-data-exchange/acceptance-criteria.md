---
document_id: FP-009-AC
title: HR Employee Data Exchange — Acceptance Criteria
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Acceptance Criteria

> Twenty criteria. Each names a behaviour a test can fail on; none restates a requirement in different words.
> Criteria that cannot be determinate until an owner decision lands **say which one**, and say what is
> already fixed regardless of how it is ruled — because a criterion that waits entirely on a ruling teaches
> nobody anything in the meantime.

## Import

**`AC-DOC-0001` — Header contract.** A file missing a required column is refused before any row is read, and
so is a file carrying an unrecognized column. Both answer `400 request.invalid`, and the response names the
offending column. Column order does not matter and header casing does not matter.

**`AC-DOC-0002` — Ownership columns are absent by construction.** A file carrying `companyId`, `branchId`,
`tenantId` or `status` is refused by the unknown-column rule — not accepted-and-ignored, and not
accepted-and-validated. There is no code path that reads such a column.

**`AC-DOC-0003` — Every row is validated.** A file with errors in rows 14 and 902 reports **both**. The
report is not truncated at the first failure, and `rejectedCount` equals the number of distinct rows in
error.

**`AC-DOC-0004` — Row numbers are file line numbers.** An error in the first data row of a file with a header
reports `rowNumber: 2`.

**`AC-DOC-0005` — Validation writes nothing.** After `FR-DOC-0101` against a wholly valid file, the employee
count is unchanged, no branch-assignment rows exist for the file's employees, and a subsequent real import of
the same file succeeds. A run record with outcome `Validated` exists.

**`AC-DOC-0006` — An imported employee is indistinguishable from a created one.** Same normalized uniqueness,
same stamped branch, exactly one initial branch assignment, one department assignment and one position
assignment, same audit fields. No import-specific relaxation is observable in the resulting rows.

**`AC-DOC-0007` — Caps are enforced before parsing.** A file over 10 MB or over 5,000 data rows is refused
with a message naming the limit and the actual value, and the refusal happens without the file being parsed.

**`AC-DOC-0008` — Idempotent replay.** Submitting a file under an `importKey` already recorded for the
company returns the **original** run's result and creates no additional employees. The second call answers
`200`, not a conflict status.

**`AC-DOC-0009` — A refused submission still consumes its key.** After a refusal, replaying the same key
returns the refusal rather than importing, so a failed run cannot be silently retried under the key meant to
prevent exactly that.

**`AC-DOC-0010` — An import cannot cross a company boundary.** *(`SEC-DOC-0403`)* Employees are created in
the caller's established company context. There is no file value that changes which company they land in.

> **Waiting on `OD-DOC-002` (create-only vs upsert):** whether a row naming an existing employee number is a
> rejected row or an update. **Fixed either way:** it never changes tenant, company or branch
> (`BRULE-DOC-0602`), and it never performs a transfer.
>
> **Waiting on `OD-DOC-003` (atomicity):** whether 998 valid rows land beside 2 invalid ones. **Fixed either
> way:** a rejected row leaves nothing behind (`BRULE-DOC-0604`), and the report shape does not change.
>
> **Waiting on `OD-DOC-004` (classification resolution):** whether `departmentCode` or a department
> identifier is the column. **Fixed either way:** a missing referent is not created by the import unless the
> owner rules otherwise, and a referent in another company matches nothing.

## Export

**`AC-DOC-0011` — Export is scoped, and demonstrably narrower for a narrower caller.** Two callers with
different branch authorizations exporting the same company get different row sets, and the narrower caller's
rows are a subset of the wider caller's. *(`SEC-DOC-0402`, `BRULE-DOC-0605`)*

**`AC-DOC-0012` — There is no unscoped export path.** No route, parameter or permission produces an export
whose SQL omits the tenant, company or branch predicate. An empty authorized branch set answers `403`, not an
unfiltered file.

**`AC-DOC-0013` — Terminated employees are excluded by default and includable by name.** *(`BRULE-DOC-0606`)*

**`AC-DOC-0014` — Export accepts exactly the search vocabulary.** Every filter employee search accepts is
accepted here with the same meaning and the same refusals; a parameter search rejects is rejected here. **No
filter is implemented below the transport without being reachable through it** — the failure FP-009's own
audit found in `FR-DEP-0111`.

**`AC-DOC-0015` — Every export writes a run record naming the column set and the scope in force.**
*(`SEC-DOC-0404`)* A failed export writes none, because nothing left the system.

**`AC-DOC-0016` — Round trip.** A file produced by export, unmodified, is a legal import file: its header
satisfies the column contract and its rows parse. Where `OD-DOC-006` removes a column from exports, that
column is optional on import so the property still holds.

> **Waiting on `OD-DOC-006` (PII):** whether `nationalId` is a column at all. **Fixed either way:** whatever
> the column set is, it is recorded on the run record.

## Documents *(conditional on `OD-DOC-001`)*

**`AC-DOC-0017` — Content type is verified against the bytes.** A PDF renamed `.png` and declared
`image/png` is refused. *(`SEC-DOC-0406`)*

**`AC-DOC-0018` — Metadata visibility does not grant content.** A caller holding
`HR.EmployeeDocuments.View` and not `.Download` can list a document and cannot obtain its bytes through any
route. The refusal is `403`, not `404` — the caller already knows the document exists.

**`AC-DOC-0019` — Document reads inherit the employee's scope.** A document belonging to an employee outside
the caller's scope answers `404`, and answers it identically whether the document exists, belongs to another
company, or never existed.

**`AC-DOC-0020` — Withdrawal is one-way and metadata survives it.** Withdrawing an already-withdrawn document
answers `409 employee_document.transition_invalid`. After withdrawal, who uploaded the document and when
remains readable regardless of what happened to the bytes.

> **Waiting on `OD-DOC-007` (storage) and `OD-DOC-008` (retention):** whether withdrawal destroys content,
> and what a cutover does with a document-holding tenant. **Fixed either way:** metadata survives, the route
> is a named `POST`, and — per `NFR-DOC-0504` — a copy that cannot move content **fails fast** rather than
> completing silently. Under the in-database option this is proven by copying a tenant holding a document and
> comparing content hashes; under the others, by asserting the refusal.

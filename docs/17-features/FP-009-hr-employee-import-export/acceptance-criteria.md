---
document_id: FP-009-AC
title: HR Employee Import and Export — Acceptance Criteria
status: Approved for Implementation
version: 1.0
---

# FP-009 — Acceptance Criteria

> **Approved 2026-08-22. Sixteen criteria** — `AC-DOC-0017`–`0020` travelled to FP-010 with the documents
> material. Each names a behaviour a test can fail on; none restates a requirement in different words.
>
> The three "waiting on" notes the analysis carried are now **resolved in place**: the ruling is stated where
> the question was, so a reader sees both what was open and what closed it.

## Import

**`AC-DOC-0001` — Header contract.** A file missing a required column is refused before any row is read, and
so is a file carrying an unrecognized column. Both answer `400 request.invalid`, and the response names the
offending column. Column order does not matter and header casing does not matter.

> **`status` AMENDED 2026-08-22 by `OD-DOC-010`.** It is now a RECOGNIZED optional column rather than an
> unknown one. The criterion's substance is untouched: an import still creates only `Active` employees and no
> file can create a terminated one — the refusal is now a named ROW error instead of a header rejection.
> `companyId`, `branchId` and `tenantId` are unchanged and still absent by construction.

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

> **RESOLVED — `OD-DOC-002` → create-only.** A row naming an existing employee number is a **rejected row**,
> never an update. What was fixed either way still holds: it never changes tenant, company or branch
> (`BRULE-DOC-0602`), and it never performs a transfer.
>
> **RESOLVED — `OD-DOC-003` → all-or-nothing.** 998 valid rows do **not** land beside 2 invalid ones; the
> file is refused whole and every error is reported. A rejected row leaves nothing behind
> (`BRULE-DOC-0604`), and the report shape is unchanged from the drafted one.
>
> **RESOLVED — `OD-DOC-004` → by code, never creates.** The column is `departmentCode`, resolved against
> existing records under the importer's own authority. A missing referent is a row error; a referent in
> another company matches nothing, which is the same answer for the same reason.

**`AC-DOC-0021` — All-or-nothing is observable, not just documented.** A 1,000-row file with one invalid row
creates **zero** employees, and the count of employees in the company is identical before and after. On an
applied run, `acceptedCount` equals `rowCount` exactly — there is no reachable response in which they differ.

**`AC-DOC-0022` — Codes resolve under the caller's own authority.** A `departmentCode` that exists only in a
company the caller cannot see is reported as unresolvable, in a message indistinguishable from one for a code
that exists nowhere. An import cannot be used to enumerate another company's organizational structure one
rejection at a time.

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

> **CLOSED for an all-`Active` export by `OD-DOC-010` (2026-08-22), with a stated limit.** A default export
> is `Active` AND `Inactive`, not all-`Active`, so a default export containing an inactive employee refuses
> on re-import with a named row error. Narrowing `DEC-DOC-0009`'s default would close it unconditionally and
> is a different decision.

**`AC-DOC-0016` — Round trip.** A file produced by export, unmodified, is a legal import file: its header
satisfies the column contract and its rows parse. Where `OD-DOC-006` removes a column from exports, that
column is optional on import so the property still holds.

> **RESOLVED — `OD-DOC-006` → never exported.** `nationalId` is not a column of any export, for any caller,
> under any parameter. What was fixed either way still holds and now does the enforcing: whatever the column
> set is, it is recorded on the run record — so the exclusion is auditable rather than merely asserted.

**`AC-DOC-0023` — No export carries `nationalId`.** Asserted over **every** export the surface can produce —
every filter combination, every scope mode, every permission set — rather than over one representative call.
A rule with no exceptions is testable as a rule, and this is the one field in the module where "we checked the
usual path" is not good enough.

## Documents — transferred to FP-010

`AC-DOC-0017` (content type verified against bytes), `AC-DOC-0018` (metadata visibility does not grant
content), `AC-DOC-0019` (document reads inherit the employee's scope) and `AC-DOC-0020` (withdrawal is
one-way and metadata survives) moved to [FP-010](../FP-010-hr-employee-documents/) keeping their identifiers.

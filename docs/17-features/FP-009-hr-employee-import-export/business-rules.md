---
document_id: FP-009-BR
title: HR Employee Import and Export — Business Rules
status: Approved for Implementation
version: 1.0
---

# FP-009 — Business Rules

> **Approved 2026-08-22.** `BRULE-DOC-0601`, `0602`, `0604` and `0606` were ratified; `BRULE-DOC-0605` was
> settled by precedent from the start; `BRULE-DOC-0607` and `0608` travelled to FP-010 with the documents
> material.
>
> **No business rule in the product specification touches import or export.** `BR-HR-0001`
> through `BR-HR-0009` cover employee identity, status, department, position and manager; none mentions a
> file, an upload, an import or an extract. `BR-PLT-*` covers the platform-wide conventions that apply to
> everything and say nothing specific here.
>
> Every rule below is therefore **derived**, and carries the derivation. A `BRULE-DOC` identifier means "a
> rule this package proposes", not "a rule the product stated".

## Derived rules

**`BRULE-DOC-0601` — An import never creates organizational structure.** A row naming a department,
position or branch that does not exist in the caller's company is **rejected**; the import does not create
the referent.

*Derivation.* `BR-HR-0005` and `BR-HR-0006` make department and position required classifications of an
employee, and both are governed by their own packages with their own uniqueness and lifecycle rules
(`FP-007`, `FP-008`). Nothing in either package contemplates creation as a side effect of something else.
The failure mode is concrete: `FINANCE` and `FINANCE ` (trailing space) become two permanent org units, and
nobody approved either.

> **RULED 2026-08-22 (`OD-DOC-004`).** Adopted as recommended, with one clause the recommendation did not
> spell out: resolution runs **under the importer's own authority**, so a code the caller could not read is a
> code they cannot import against. Resolving outside their scope would have let a file enumerate department
> codes in companies the caller cannot see, one rejection message at a time.

**`BRULE-DOC-0602` — An import never changes an existing employee's ownership dimensions.** Tenant, company
and branch are stamped from trusted context on creation and are not writable afterwards (`SEC-EMP-0202`,
`SEC-EMP-0203`, `BRULE-EMP-0015`). If `OD-DOC-002` rules for upsert, an import may update profile fields; it
may not move an employee between companies or branches. **A branch move is a transfer**, which is a
different operation with its own reason code, its own history record and its own permission (`ADR-024`,
`FP-006` transfer contract). A file must not be able to perform one silently.

**`BRULE-DOC-0603` — An imported employee satisfies every rule a single created employee satisfies.**
Employee number uniqueness per company, national-ID uniqueness per company, the required classifications, the
value-object validation, the branch write boundary, the audit stamping. The import path composes the existing
domain operation; it does not reimplement it.

*Derivation.* This is the only rule here that needs no business input, because the alternative — a bulk path
with its own weaker validation — is how two versions of "a valid employee" come to exist in one product.
`DEC-DEP-0026` records the same failure in a smaller form: a second path that reused the wrong mapper
produced a wrong answer that nobody could see.

**`BRULE-DOC-0604` — A rejected row leaves nothing behind.** Whatever `OD-DOC-003` rules about the file as a
whole, an individual row that fails validation writes no employee, no partial employee and no history record.

**`BRULE-DOC-0605` — An export never returns an employee the caller could not read individually.** The
export scope is the read scope; there is no export-only widening, no "administrative export", and no mode in
which the branch predicate is omitted.

*Derivation.* `ADR-023` decision 22 and `ADR-025` decision 10, as implemented by `DEC-EMP-0029`. This is the
one rule in this package that is **settled rather than proposed** — see
[`decisions-approved.md`](decisions-approved.md#settled-by-precedent--the-two-worth-writing-out).

**`BRULE-DOC-0606` — An export excludes terminated employees unless they are asked for by name.** The
default matches employee search (`FP-006` api-contracts); `status=Terminated` includes them explicitly, so
statutory and payroll extracts remain possible. **Ratified** (`DEC-DOC-0009`).

**`BRULE-DOC-0609` — No export carries `nationalId`.** Ruled by `OD-DOC-006`, and stated as a rule rather
than only as a contract detail because it is unconditional: no permission, parameter or caller produces the
column. The export run record's column set is what makes the rule auditable after the fact.

**`BRULE-DOC-0607` and `BRULE-DOC-0608`** — one document belongs to one employee and never moves; stored
content is immutable — **transferred to [FP-010](../FP-010-hr-employee-documents/) under the `OD-DOC-001`
split**, keeping their identifiers.

## Rules this package does NOT propose, and why

| Not proposed | Why |
|---|---|
| A rule about how long documents are kept | FP-010's question (`OD-DOC-008`), and legal before it is technical |
| A rule about which employees *must* have documents | Nothing in the specification says a document is ever required; inventing a completeness rule would invent policy |
| A rule about export frequency or volume limits per caller | Rate limiting is a platform concern, not an HR business rule; `NFR-DOC-0501` bounds a single operation and stops there |
| A rule making import the authoritative source of employee data | Nothing suggests it. An import is one way to create employees, not a synchronization contract with an external system |

## Interaction with existing rules

| Existing rule | Interaction |
|---|---|
| `BR-HR-0005` — every employee has a department | Import must supply one per row; `OD-DOC-004` decides how it is named |
| `BR-HR-0006` — every employee has a position | Same, and `DEC-POS-0026`'s fail-loud posture is the precedent for refusing rather than defaulting |
| `BR-PLT-0002` — company isolation | `SEC-DOC-0403`; a file cannot reach across companies |
| `BR-PLT-0003` — no physical deletion | **Untouched here.** Nothing in this package deletes: an import creates, an export reads, and run records are append-only. The tension with erasure travelled to FP-010 with `OD-DOC-008` |
| `BR-PLT-0004` — audit trail | `DEC-DOC-0006`; both run records exist because of this rule |
| `BR-PLT-0013` — branch owns transactions | An import creates employees in the caller's execution branch; it does not distribute them across branches by file value (`BRULE-DOC-0602`) |
| `BR-PLT-0016` — reporting scope | `BRULE-DOC-0605`; an export is a report and obeys it |

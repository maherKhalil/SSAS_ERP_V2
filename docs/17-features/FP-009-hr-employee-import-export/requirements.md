---
document_id: FP-009-REQ
title: HR Employee Import and Export — Requirements
status: Approved for Implementation
version: 1.0
---

# FP-009 — Requirements

> **Approved 2026-08-22.** Six owner decisions were ruled and three deferred to FP-010; requirements that
> were provisional are now determinate, and the ruling that determined each is cited inline.
>
> **The source requirements have no body text.** `REQ-HR-0009` and `REQ-HR-0010` appear in the requirement
> catalog as titles only, and **no business rule anywhere in the specification touches either of them**. Everything below is derived — from platform precedent, from the four written statements listed in
> [`README.md`](README.md#the-whole-of-the-written-authority), and from the owner decisions this package
> raises. Where a requirement rests on a reading rather than a written statement, it says so.
>
> This is a weaker footing than FP-008 had. FP-008's requirements were also bodiless, but `BR-HR-0006` stated
> a rule to derive from. Here there is no rule at all, which is why nine questions go to the owner rather
> than three.

## Source requirements

| Source | Name | Body text | Coverage |
|---|---|---|---|
| `REQ-HR-0005` | Employee Documents | **None** | **Transferred to FP-010** (`OD-DOC-001` → SPLIT), carrying `FR-DOC-0301`–`FR-DOC-0304` |
| `REQ-HR-0009` | Employee Import | **None** | `FR-DOC-0101`–`FR-DOC-0103` |
| `REQ-HR-0010` | Employee Export | **None** | `FR-DOC-0201`–`FR-DOC-0202` |
| `REQ-HR-0001` | Create Employee | — | **Reused, not re-specified.** Import creates employees through the same domain path a single create uses (`BRULE-DOC-0603`) |
| `REQ-HR-0008` | Employee Search | — | **Reused.** Export is a search that leaves the system, under the same scope and the same bounds |

`DEC-EMP-0032` deferred all three of these requirements out of FP-006 explicitly, "traceable, not discarded"
(`AC-EMP-0047`, `TS-EMP-0118`). This package is that deferral coming due.

## Functional requirements

### Import

**`FR-DOC-0101` — Validate an import file without importing it.** A caller submits a file and receives the
same per-row validation report a real import would produce, with **nothing written**. This exists because
`DEC-DOC-0003` makes the report the operator's working document: they fix the file against it and re-submit.
Requiring a real import to obtain one would mean the first attempt is always the expensive way to find out.

### `FR-DOC-0102` — Import employees from a file

A caller submits a CSV (`DEC-DOC-0001`) whose header matches the column contract (`DEC-DOC-0002`); each row
becomes an employee created through the ordinary domain path. Every rule that governs a single create governs
an imported row — no relaxations, no bypass of the write boundary (`BRULE-DOC-0603`).

**Determinate as of 2026-08-22.** `OD-DOC-002` → **create-only**: a row whose employee number already exists
in the company is rejected. `OD-DOC-003` → **all-or-nothing**: every row is validated, every error reported,
and nothing is applied unless every row passes. `OD-DOC-004` → department, position and branch resolve **by
code against existing records under the importer's authority**, and the import never creates one.
`OD-DOC-005` → **`HR.Employees.Import`**, granted separately.

**One consequence worth stating where an implementer will read it:** because a single unresolvable code is a
row error, and a row error fails the file, an import is refused whole for one bad department code. That is
intended — a file whose classifications are wrong is one the operator wants back.

### `FR-DOC-0103` — Read import run history

The durable record of imports for the caller's company (`DEC-DOC-0006`): who, when, counts, outcome. Append-
only. Scoped like every other read.

### Export

### `FR-DOC-0201` — Export employees within the caller's scope

A bounded, deterministic-ordered extract of the employees the caller's **materialized employee read scope**
admits, in the column contract of `DEC-DOC-0008`. The scope requirement is
[settled](decisions-approved.md#settled-by-precedent--the-two-worth-writing-out), not proposed.

**Determinate as of 2026-08-22.** `OD-DOC-005` → **`HR.Employees.Export`**, granted separately.
`OD-DOC-006` → **`nationalId` is never exported**; the column is absent from the export contract for every
caller, and import keeps it optional so the round-trip property holds.

### `FR-DOC-0202` — Read export run history

The same record from the other direction, and the more important one: an export is the only operation in the
module that moves data outside the system's control, so the record of *what column set left* is the control
that survives it.

### Documents — transferred to FP-010

`FR-DOC-0301` (upload), `FR-DOC-0302` (list), `FR-DOC-0303` (download content) and `FR-DOC-0304` (withdraw)
**moved to [FP-010](../FP-010-hr-employee-documents/) under the `OD-DOC-001` split**, keeping their
identifiers. Nothing in this package anticipates them.

## Security requirements

| | Requirement |
|---|---|
| `SEC-DOC-0401` | No exchange payload — file column, form field, query parameter or header — accepts a writable `TenantId`, `CompanyId` or `BranchId` on an entity. Ownership is stamped from trusted context, exactly as `FP-006` established |
| `SEC-DOC-0402` | Export obtains a materialized `EmployeeReadScope` and applies it as explicit predicates. There is no export mode that omits a scope predicate (`ADR-023` d.22, `ADR-025` d.10, `BRULE-EMP-0025`) |
| `SEC-DOC-0403` | An import writes only into the caller's trusted company context. A file row naming another company does not import into it — the value is refused, never adopted |
| `SEC-DOC-0404` | The column set of every export is recorded on the run (`DEC-DOC-0006`), so what left the system is knowable after the fact whatever `OD-DOC-006` rules |
| `SEC-DOC-0405`, `SEC-DOC-0406` | **Transferred to FP-010** — the content scope type and magic-byte verification are document requirements |

## Non-functional requirements

| | Requirement |
|---|---|
| `NFR-DOC-0501` | Caps are enforced before parsing: 5,000 rows, 10 MB per file, 10 MB per document (`DEC-DOC-0005`, `DEC-DOC-0011`). Exceeding one names the limit and the actual value |
| `NFR-DOC-0502` | Import and export complete synchronously within the caps (`DEC-DOC-0007`). If that ceases to hold, the async variant is additive rather than a contract change |
| `NFR-DOC-0503` | Run records are durable and append-only, and survive the operation they describe failing |
| `NFR-DOC-0504`, `NFR-DOC-0505` | **Transferred to FP-010** — cutover and backup custody are properties of stored bytes, and this package stores none |

## What is explicitly out of scope

* **Documents** — FP-010 (`OD-DOC-001`).
* **Upsert.** `OD-DOC-002` ruled create-only; a future additive mode is recorded, not designed.
* Import or export of **anything but employees**. Departments, positions and grades have their own packages
  and are not smuggled in through a file format.
* A general document-management capability — versioning, workflow, sharing, retention policies. The roadmap
  places that at V5 (`OD-DOC-009`).
* Scheduled or recurring exports, and any delivery channel other than the response to the request.
* Employee-number generation for imported rows. `DEC-EMP-0011` deferred generation entirely; an import file
  carries the numbers.

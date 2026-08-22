---
document_id: FP-009
title: HR Employee Import and Export
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
depends_on:
  - ADR-012
  - ADR-017
  - ADR-020
  - ADR-023
  - ADR-024
  - ADR-025
  - FP-005
  - FP-006
  - FP-007
  - FP-008
---

# Feature Package 009 — HR Employee Import and Export

> **Approved for Implementation.** This package began as analysis of three requirements with nine open owner
> decisions. **All nine were disposed on 2026-08-22**: six were ruled and are binding; three were deferred
> under the packaging ruling and travel to FP-010 with their options tables intact. All thirteen engineering
> proposals (`DEC-DOC-0001`–`0013`) were ratified as drafted.
>
> The original analysis text is preserved throughout; each owner decision carries the ruling that closed it,
> **appended rather than written over**.

## The packaging ruling — `OD-DOC-001` → SPLIT

**Employee Documents left this package.** `REQ-HR-0009` (Import) and `REQ-HR-0010` (Export) are FP-009;
`REQ-HR-0005` (Employee Documents) is **FP-010**, gated on `ADR-028`.

| Package | Covers | Status |
|---|---|---|
| **FP-009 — HR Employee Import and Export** *(this package)* | `REQ-HR-0009`, `REQ-HR-0010` | Approved for Implementation |
| **[FP-010 — HR Employee Documents](../FP-010-hr-employee-documents/)** | `REQ-HR-0005` | Deferred, gated on `ADR-028` |

The analysis was not rewritten to make this split — it was cut along the line it was written along. Every
documents-related requirement, rule, criterion, scenario and decision **moved to FP-010 keeping its
identifier**, because a decision identifier is a citation and renumbering one is how a traceability matrix
starts lying. `DEC-DOC`/`OD-DOC` is therefore one space spanning two packages, exactly as
[the original analysis said it would be](#identifier-space--dec-doc--od-doc).

## What this package covers

| Source | Name | Body text exists? | Coverage |
|---|---|---|---|
| `REQ-HR-0009` | Employee Import | **No** | `FR-DOC-0101`–`FR-DOC-0103` |
| `REQ-HR-0010` | Employee Export | **No** | `FR-DOC-0201`–`FR-DOC-0202` |
| `REQ-HR-0005` | Employee Documents | **No** | **Transferred to FP-010** (`OD-DOC-001`) |

**No business rule in the product specification touches either requirement.** `BR-HR-*` says nothing about
import or export; neither does `BR-PLT-*` beyond the platform-wide conventions that apply to everything.

That absence was the defining fact of the analysis, and it is why nine questions went to the owner rather
than three. What survives it is a package built almost entirely on precedent: **import and export need
nothing this platform does not already have.** They read and write the Employee aggregate FP-006 shipped,
under the read scope FP-006 built, through the transport conventions FP-006C5 established.

## The written authority — what existed before the rulings

1. **The requirement catalog** lists `REQ-HR-0009 Employee Import` and `REQ-HR-0010 Employee Export` under
   *Employee Management*. Titles only.
2. **`DEC-EMP-0032`** deferred both out of FP-006 explicitly, "traceable, not discarded" (`AC-EMP-0047`,
   `TS-EMP-0118`). This package is that deferral coming due.

Two statements that shaped the original analysis — `ADR-005`'s "Attachment Metadata" example and the
Product-Roadmap's V5 "Document Management" line — belong to FP-010 now and travelled there.

## The six rulings that close this package

| | Decision | Ruling |
|---|---|---|
| `OD-DOC-001` | Packaging | **SPLIT.** FP-009 is import/export; documents become FP-010 behind `ADR-028` |
| `OD-DOC-002` | Import mode | **CREATE-ONLY.** A duplicate employee number rejects the row (`BR-HR-0001`); updates stay on the audited single-record routes. Upsert is recorded as a possible future **additive** mode and is not designed |
| `OD-DOC-003` | Atomicity | **ALL-OR-NOTHING, with the full report.** Every row is validated and every error named; nothing is applied unless every row passes |
| `OD-DOC-004` | Classification resolution | **By code, against existing records, under the importer's authority. An import never creates organizational structure.** An unresolvable code is a row error — which under `OD-DOC-003` fails the file |
| `OD-DOC-005` | Permissions | **SEPARATE.** `HR.Employees.Import` and `HR.Employees.Export`, granted independently |
| `OD-DOC-006` | Export PII | **`nationalId` is NEVER exported.** Excluded from every export unconditionally; optional on import so the round-trip property holds |

Each is stated in full, with the options that were weighed and the ruling appended, in
[`decisions-approved.md`](decisions-approved.md#owner-decisions--disposed).

### What `OD-DOC-003` settled beyond atomicity

All-or-nothing **closed `DEC-DOC-0004`'s open dependency**. Idempotency was the one proposal whose meaning
depended on an unruled question: what a re-run means for rows that already exist. Under all-or-nothing there
are no partially applied files, so:

* a **failed** run wrote nothing, and re-running the corrected file is an ordinary first import;
* a **successful** run created every employee in the file, so re-running it is a file of duplicate employee
  numbers — which under `OD-DOC-002` is a file of rejected rows, and under `OD-DOC-003` a refused file.

The import key remains, and its job is now narrow and precise: it protects against the **ambiguous
timeout** — the caller who never learned whether their import applied — rather than against partial
application, which can no longer happen. `DEC-DOC-0004` is restated on those terms.

## The three deferred decisions

`OD-DOC-007` (binary storage location), `OD-DOC-008` (retention and erasure) and `OD-DOC-009` (V5 Document
Management ownership) are **OPEN-DEFERRED**, not closed. Their options tables travelled to
[FP-010](../FP-010-hr-employee-documents/) intact, and they are that package's starting inventory rather
than questions this one answered.

`ADR-028` is required before FP-010 can proceed, and **not required by this package** — which is precisely
the asymmetry the split ruling acted on.

## Identifier space — `DEC-DOC` / `OD-DOC`

One space, two packages, no renumbering. Identifiers allocated by the original analysis stay where they were
assigned:

| Range | Where it lives now |
|---|---|
| `DEC-DOC-0001`–`0009` | FP-009 — ratified as drafted |
| `DEC-DOC-0010`–`0013` | FP-010 — ratified as drafted, annotated FP-010-scoped |
| `OD-DOC-001`–`006` | FP-009 — closed |
| `OD-DOC-007`–`009` | FP-010 — open-deferred |
| `FR-DOC-0101`–`0202`, `SEC-DOC-0401`–`0404`, `NFR-DOC-0501`–`0503`, `BRULE-DOC-0601`–`0606`, `AC-DOC-0001`–`0016`, `TS-DOC-0001`–`0018` | FP-009 |
| `FR-DOC-0301`–`0304`, `SEC-DOC-0405`–`0406`, `NFR-DOC-0504`–`0505`, `BRULE-DOC-0607`–`0608`, `AC-DOC-0017`–`0020`, `TS-DOC-0019`–`0024` | FP-010 |

**Neither package reallocates a number the other used.** New decisions in either continue the same monotonic
sequence from `DEC-DOC-0014`.

## Precedent stack this package inherits

| Precedent | What it settles here |
|---|---|
| `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029` | Every read is scoped and materialized — **including export** |
| `ADR-023` d.8, `ADR-024` | Branch execution context; a transfer is its own operation and a file cannot perform one |
| `ADR-017`, `DEC-EMP-0027` | No cross-database foreign key |
| `ADR-020`, `DEC-DEP-0029`, `DEC-POS-0022` | Tenant-owned entities enter the E3 copy manifest by construction; the nine-site inventory obligation |
| `DEC-DEP-0023`, `DEC-DEP-0024`, `DEC-DEP-0026`, `DEC-DEP-0030` | Route/handler 1:1; named `POST`s and no `DELETE`; per-resource problem-code namespace; `409` for state-conflict refusals |
| `DEC-DEP-0025` | The separation test `OD-DOC-005` was ruled on |
| `DEC-EMP-0030` | The sensitivity precedent `OD-DOC-006` was ruled on |
| `DEC-POS-0034` | Specified-but-never-shipped is a real failure mode, and `AC-DOC-0014` exists because of it |

## What this package deliberately does not do

* **Documents.** They are FP-010, and nothing here anticipates them.
* **Upsert.** `OD-DOC-002` ruled create-only. A future additive mode is *recorded*, not designed — designing
  it now would specify a route nobody has approved, which is the `DEC-POS-0034` failure in advance.
* **Import or export of anything but employees.** Departments, positions and grades have their own packages
  and are not smuggled in through a file format.
* **Scheduled or recurring exports**, or any delivery channel other than the response to the request.
* **Employee-number generation.** `DEC-EMP-0011` deferred it entirely; an import file carries the numbers.

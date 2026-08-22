---
document_id: FP-009-RTM
title: HR Employee Import and Export — Traceability Matrix
status: Approved for Implementation
version: 1.0
---

# FP-009 — Traceability Matrix

> **Approved 2026-08-22.** Every identifier this package defines is traced below, and the orphan check at the
> end is mechanical rather than asserted — enumerated, never ranged, because a range hides its middle.
>
> **No as-built section exists**, because nothing is built. FP-007 is the reason that matters: its as-built
> pass marked a document "matched" while the field it described had never shipped.

## Source requirement coverage

| Source | Coverage |
|---|---|
| `REQ-HR-0009` Employee Import | `FR-DOC-0101`–`FR-DOC-0103` |
| `REQ-HR-0010` Employee Export | `FR-DOC-0201`–`FR-DOC-0202` |
| `REQ-HR-0005` Employee Documents | **Transferred to FP-010** (`OD-DOC-001` → SPLIT) |
| `REQ-HR-0001` Create Employee | Reused unchanged — import composes the existing path (`BRULE-DOC-0603`) |
| `REQ-HR-0008` Employee Search | Reused unchanged — export is a scoped search (`BRULE-DOC-0605`) |

## Functional requirement coverage

| Requirement | Rules | Criteria | Scenarios | Determined by |
|---|---|---|---|---|
| `FR-DOC-0101` validate a file | `BRULE-DOC-0604` | `AC-DOC-0005` | `TS-DOC-0006` | — |
| `FR-DOC-0102` import employees | `BRULE-DOC-0601`, `BRULE-DOC-0602`, `BRULE-DOC-0603`, `BRULE-DOC-0604` | `AC-DOC-0001`, `AC-DOC-0002`, `AC-DOC-0003`, `AC-DOC-0004`, `AC-DOC-0006`, `AC-DOC-0007`, `AC-DOC-0008`, `AC-DOC-0009`, `AC-DOC-0010`, `AC-DOC-0021`, `AC-DOC-0022` | `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003`, `TS-DOC-0004`, `TS-DOC-0005`, `TS-DOC-0007`, `TS-DOC-0008`, `TS-DOC-0009`, `TS-DOC-0010`, `TS-DOC-0011`, `TS-DOC-0012`, `TS-DOC-0025`, `TS-DOC-0026` | `OD-DOC-002`, `OD-DOC-003`, `OD-DOC-004`, `OD-DOC-005` |
| `FR-DOC-0103` import run history | — | `AC-DOC-0005`, `AC-DOC-0008` | `TS-DOC-0006`, `TS-DOC-0009` | — |
| `FR-DOC-0201` export employees | `BRULE-DOC-0605`, `BRULE-DOC-0606`, `BRULE-DOC-0609` | `AC-DOC-0011`, `AC-DOC-0012`, `AC-DOC-0013`, `AC-DOC-0014`, `AC-DOC-0016`, `AC-DOC-0023` | `TS-DOC-0013`, `TS-DOC-0014`, `TS-DOC-0015`, `TS-DOC-0016`, `TS-DOC-0018`, `TS-DOC-0027` | `OD-DOC-005`, `OD-DOC-006` |
| `FR-DOC-0202` export run history | `BRULE-DOC-0609` | `AC-DOC-0015` | `TS-DOC-0017` | `OD-DOC-006` |

## Business rule coverage

| Rule | Criteria | Scenarios | Status |
|---|---|---|---|
| `BRULE-DOC-0601` no structure created by import | `AC-DOC-0010`, `AC-DOC-0022` | `TS-DOC-0011`, `TS-DOC-0026` | **Ruled** — `OD-DOC-004` |
| `BRULE-DOC-0602` no ownership change by import | `AC-DOC-0010` | `TS-DOC-0011`, `TS-DOC-0012` | Ratified |
| `BRULE-DOC-0603` imported = created | `AC-DOC-0006` | `TS-DOC-0007` | Ratified |
| `BRULE-DOC-0604` a rejected row leaves nothing | `AC-DOC-0003`, `AC-DOC-0005`, `AC-DOC-0021` | `TS-DOC-0004`, `TS-DOC-0006`, `TS-DOC-0025` | Ratified; **strengthened** by `OD-DOC-003` |
| `BRULE-DOC-0605` export never widens scope | `AC-DOC-0011`, `AC-DOC-0012` | `TS-DOC-0013`, `TS-DOC-0014` | **Settled by precedent** — `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029` |
| `BRULE-DOC-0606` terminated excluded by default | `AC-DOC-0013` | `TS-DOC-0015` | Ratified (`DEC-DOC-0009`) |
| `BRULE-DOC-0609` no export carries `nationalId` | `AC-DOC-0023` | `TS-DOC-0027` | **Ruled** — `OD-DOC-006` |
| `BRULE-DOC-0607`, `BRULE-DOC-0608` | — | — | **Transferred to FP-010** |

## Security requirement coverage

| | Criteria | Scenarios |
|---|---|---|
| `SEC-DOC-0401` no writable ownership fields | `AC-DOC-0002` | `TS-DOC-0002` |
| `SEC-DOC-0402` export is scoped | `AC-DOC-0011`, `AC-DOC-0012` | `TS-DOC-0013`, `TS-DOC-0014` |
| `SEC-DOC-0403` import writes only in context | `AC-DOC-0010`, `AC-DOC-0022` | `TS-DOC-0011`, `TS-DOC-0012`, `TS-DOC-0026` |
| `SEC-DOC-0404` export column set recorded | `AC-DOC-0015`, `AC-DOC-0023` | `TS-DOC-0017`, `TS-DOC-0027` |
| `SEC-DOC-0405`, `SEC-DOC-0406` | — | **Transferred to FP-010** |

## Non-functional requirement coverage

| | Criteria | Scenarios |
|---|---|---|
| `NFR-DOC-0501` caps | `AC-DOC-0007` | `TS-DOC-0008` |
| `NFR-DOC-0502` synchronous within caps | — | *Measured, not asserted — see [`test-scenarios.md`](test-scenarios.md#scenarios-deliberately-not-written)* |
| `NFR-DOC-0503` durable run records | `AC-DOC-0015` | `TS-DOC-0017` |
| `NFR-DOC-0504`, `NFR-DOC-0505` | — | **Transferred to FP-010** — cutover and backup custody are properties of stored bytes, and this package stores none |

## Decision coverage

| Decision | Where it lands |
|---|---|
| `DEC-DOC-0001` CSV only | `AC-DOC-0001`; `employee_import.format_unsupported` |
| `DEC-DOC-0002` strict header | `AC-DOC-0001`, `AC-DOC-0002`; `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003` |
| `DEC-DOC-0003` per-row report | `AC-DOC-0003`, `AC-DOC-0004`; `TS-DOC-0004`, `TS-DOC-0005` |
| `DEC-DOC-0004` idempotency key *(restated)* | `AC-DOC-0008`, `AC-DOC-0009`; `TS-DOC-0009`, `TS-DOC-0010` |
| `DEC-DOC-0005` caps | `AC-DOC-0007`; `TS-DOC-0008` |
| `DEC-DOC-0006` run records | `AC-DOC-0005`, `AC-DOC-0015`; `TS-DOC-0006`, `TS-DOC-0017` |
| `DEC-DOC-0007` synchronous | Lifecycle — the absence of an `InProgress` state |
| `DEC-DOC-0008` export format and round trip | `AC-DOC-0016`; `TS-DOC-0018` |
| `DEC-DOC-0009` terminated excluded | `AC-DOC-0013`; `TS-DOC-0015` |
| `DEC-DOC-0010`, `DEC-DOC-0011`, `DEC-DOC-0012`, `DEC-DOC-0013` | **Transferred to FP-010**, ratified as drafted and annotated FP-010-scoped |

## Owner decisions — disposition

| | Ruling | What it determined |
|---|---|---|
| `OD-DOC-001` | **SPLIT** | This package's scope; FP-010's existence |
| `OD-DOC-002` | **Create-only** | `FR-DOC-0102`; the resolved note on the import criteria |
| `OD-DOC-003` | **All-or-nothing + full report** | `FR-DOC-0102`; `AC-DOC-0021`; `TS-DOC-0025`; the restatement of `DEC-DOC-0004`; the `outcome` semantics |
| `OD-DOC-004` | **By code, never creates, under the importer's authority** | `BRULE-DOC-0601`; `AC-DOC-0022`; `TS-DOC-0026`; the column contract |
| `OD-DOC-005` | **Separate permissions** | The authorization model; `FR-DOC-0101`, `FR-DOC-0102`, `FR-DOC-0201` |
| `OD-DOC-006` | **`nationalId` never exported** | `BRULE-DOC-0609`; `AC-DOC-0023`; `TS-DOC-0027`; the export column list |
| `OD-DOC-007`, `OD-DOC-008`, `OD-DOC-009` | **OPEN-DEFERRED** | FP-010's starting inventory; `ADR-028` |

## What transferred to FP-010

One table, so nothing is lost by being in two places at once.

| Kind | Identifiers |
|---|---|
| Requirements | `FR-DOC-0301`, `FR-DOC-0302`, `FR-DOC-0303`, `FR-DOC-0304` |
| Security | `SEC-DOC-0405`, `SEC-DOC-0406` |
| Non-functional | `NFR-DOC-0504`, `NFR-DOC-0505` |
| Business rules | `BRULE-DOC-0607`, `BRULE-DOC-0608` |
| Criteria | `AC-DOC-0017`, `AC-DOC-0018`, `AC-DOC-0019`, `AC-DOC-0020` |
| Scenarios | `TS-DOC-0019`, `TS-DOC-0020`, `TS-DOC-0021`, `TS-DOC-0022`, `TS-DOC-0023`, `TS-DOC-0024` |
| Decisions | `DEC-DOC-0010`, `DEC-DOC-0011`, `DEC-DOC-0012`, `DEC-DOC-0013` |
| Owner decisions | `OD-DOC-007`, `OD-DOC-008`, `OD-DOC-009` |

**Every one kept its number.** Neither package will reallocate an identifier the other used, and new
decisions in either continue the same sequence from `DEC-DOC-0014`.

## Precedent citations — what this package takes rather than decides

| Precedent | Taken as |
|---|---|
| `ADR-012` | HR references no Platform assembly |
| `ADR-017` | No cross-database FK; SQL Server only |
| `ADR-020` | E3 manifest by construction; the copy order |
| `ADR-023` d.8, d.22 | Branch execution context; scoped reads |
| `ADR-024` | A transfer is its own operation; a file cannot perform one |
| `ADR-025` d.8, d.10 | Permission and scope are independent; materialized predicates |
| `DEC-EMP-0011`, `DEC-EMP-0027`, `DEC-EMP-0029`, `DEC-EMP-0030`, `DEC-EMP-0032` | No number generation; no cross-database FK; scope as a type; national-ID sensitivity; the deferral this package answers |
| `DEC-DEP-0023`, `DEC-DEP-0024`, `DEC-DEP-0025`, `DEC-DEP-0026`, `DEC-DEP-0029`, `DEC-DEP-0030` | Route/handler 1:1; named POSTs and no DELETE; the separation test; own problem-code namespace; manifest by construction; `409` for state conflicts |
| `DEC-POS-0018`, `DEC-POS-0022`, `DEC-POS-0026`, `DEC-POS-0030`, `DEC-POS-0031`, `DEC-POS-0034` | Scope-type-is-the-permission; the nine-site inventory; no `defaultValue` on required columns; normalized search columns; specified-but-unshipped is a real failure mode |

## Orphan check

Every identifier this package defines appears at least once above, enumerated rather than ranged.

* **9 functional requirements** — 5 defined here: `FR-DOC-0101`, `FR-DOC-0102`, `FR-DOC-0103`,
  `FR-DOC-0201`, `FR-DOC-0202`; 4 transferred and listed in the transfer table.
* **6 security requirements** — 4 here: `SEC-DOC-0401`, `SEC-DOC-0402`, `SEC-DOC-0403`, `SEC-DOC-0404`;
  2 transferred.
* **5 non-functional requirements** — 3 here: `NFR-DOC-0501`, `NFR-DOC-0502`, `NFR-DOC-0503`; 2 transferred.
* **9 business rules** — 7 here: `BRULE-DOC-0601`, `BRULE-DOC-0602`, `BRULE-DOC-0603`, `BRULE-DOC-0604`,
  `BRULE-DOC-0605`, `BRULE-DOC-0606`, `BRULE-DOC-0609`; 2 transferred.
* **23 acceptance criteria** — 19 here: `AC-DOC-0001`, `AC-DOC-0002`, `AC-DOC-0003`, `AC-DOC-0004`,
  `AC-DOC-0005`, `AC-DOC-0006`, `AC-DOC-0007`, `AC-DOC-0008`, `AC-DOC-0009`, `AC-DOC-0010`, `AC-DOC-0011`,
  `AC-DOC-0012`, `AC-DOC-0013`, `AC-DOC-0014`, `AC-DOC-0015`, `AC-DOC-0016`, `AC-DOC-0021`, `AC-DOC-0022`,
  `AC-DOC-0023`; 4 transferred.
* **27 test scenarios** — 21 here: `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003`, `TS-DOC-0004`,
  `TS-DOC-0005`, `TS-DOC-0006`, `TS-DOC-0007`, `TS-DOC-0008`, `TS-DOC-0009`, `TS-DOC-0010`, `TS-DOC-0011`,
  `TS-DOC-0012`, `TS-DOC-0013`, `TS-DOC-0014`, `TS-DOC-0015`, `TS-DOC-0016`, `TS-DOC-0017`, `TS-DOC-0018`,
  `TS-DOC-0025`, `TS-DOC-0026`, `TS-DOC-0027`; 6 transferred.
* **13 decisions** — 9 here: `DEC-DOC-0001`, `DEC-DOC-0002`, `DEC-DOC-0003`, `DEC-DOC-0004`,
  `DEC-DOC-0005`, `DEC-DOC-0006`, `DEC-DOC-0007`, `DEC-DOC-0008`, `DEC-DOC-0009`; 4 transferred.
* **9 owner decisions** — 6 ruled here: `OD-DOC-001`, `OD-DOC-002`, `OD-DOC-003`, `OD-DOC-004`,
  `OD-DOC-005`, `OD-DOC-006`; 3 open-deferred to FP-010.

**`AC-DOC-0021`, `AC-DOC-0022`, `AC-DOC-0023` and `TS-DOC-0025`, `TS-DOC-0026`, `TS-DOC-0027` are new**,
added by the rulings rather than carried from the analysis: all-or-nothing, authority-scoped code resolution
and the unconditional `nationalId` exclusion are each properties that nothing in the drafted set would have
caught failing.

**No identifier is referenced that is not defined**, and no defined identifier is left untraced. The lists
above are the check, not a summary of it.

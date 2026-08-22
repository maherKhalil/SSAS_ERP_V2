---
document_id: FP-009-RTM
title: HR Employee Data Exchange — Traceability Matrix
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Traceability Matrix

> Every identifier this package defines is traced below, and the orphan check at the end is mechanical rather
> than asserted. **No as-built section exists**, because nothing is built — and FP-007 is the reason that
> matters: its as-built pass marked a document "matched" while the field it described had never shipped.

## Source requirement coverage

| Source | Coverage |
|---|---|
| `REQ-HR-0005` Employee Documents | `FR-DOC-0301`–`FR-DOC-0304` — **conditional** on `OD-DOC-001`, `OD-DOC-007`, `OD-DOC-009` |
| `REQ-HR-0009` Employee Import | `FR-DOC-0101`–`FR-DOC-0103` |
| `REQ-HR-0010` Employee Export | `FR-DOC-0201`–`FR-DOC-0202` |
| `REQ-HR-0001` Create Employee | Reused unchanged — import composes the existing path (`BRULE-DOC-0603`) |
| `REQ-HR-0008` Employee Search | Reused unchanged — export is a scoped search (`BRULE-DOC-0605`) |

## Functional requirement coverage

| Requirement | Rules | Criteria | Scenarios | Blocked by |
|---|---|---|---|---|
| `FR-DOC-0101` validate a file | `BRULE-DOC-0604` | AC-DOC-0005 | TS-DOC-0006 | — |
| `FR-DOC-0102` import employees | `BRULE-DOC-0601`–`0604` | AC-DOC-0001–0004, 0006–0010 | TS-DOC-0001–0005, 0007–0012 | `OD-DOC-002`, `OD-DOC-003`, `OD-DOC-004`, `OD-DOC-005` |
| `FR-DOC-0103` import run history | — | AC-DOC-0005, AC-DOC-0008 | TS-DOC-0006, TS-DOC-0009 | — |
| `FR-DOC-0201` export employees | `BRULE-DOC-0605`, `BRULE-DOC-0606` | AC-DOC-0011–0014, AC-DOC-0016 | TS-DOC-0013–0016, TS-DOC-0018 | `OD-DOC-005`, `OD-DOC-006` |
| `FR-DOC-0202` export run history | — | AC-DOC-0015 | TS-DOC-0017 | — |
| `FR-DOC-0301` upload a document | `BRULE-DOC-0607`, `BRULE-DOC-0608` | AC-DOC-0017 | TS-DOC-0019 | `OD-DOC-001`, `OD-DOC-007`, `OD-DOC-009` |
| `FR-DOC-0302` list documents | `BRULE-DOC-0607` | AC-DOC-0018, AC-DOC-0019 | TS-DOC-0020, TS-DOC-0021 | Same |
| `FR-DOC-0303` download content | `BRULE-DOC-0608` | AC-DOC-0018 | TS-DOC-0020 | Same |
| `FR-DOC-0304` withdraw a document | — | AC-DOC-0020 | TS-DOC-0022 | Same, plus `OD-DOC-008` |

## Business rule coverage

| Rule | Criteria | Scenarios | Status |
|---|---|---|---|
| `BRULE-DOC-0601` no structure created by import | AC-DOC-0010 | TS-DOC-0011 | **Proposed**, subject to `OD-DOC-004` |
| `BRULE-DOC-0602` no ownership change by import | AC-DOC-0010 | TS-DOC-0011, TS-DOC-0012 | Proposed |
| `BRULE-DOC-0603` imported = created | AC-DOC-0006 | TS-DOC-0007 | Proposed |
| `BRULE-DOC-0604` a rejected row leaves nothing | AC-DOC-0003, AC-DOC-0005 | TS-DOC-0004, TS-DOC-0006 | Proposed |
| `BRULE-DOC-0605` export never widens scope | AC-DOC-0011, AC-DOC-0012 | TS-DOC-0013, TS-DOC-0014 | **Settled** — `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029` |
| `BRULE-DOC-0606` terminated excluded by default | AC-DOC-0013 | TS-DOC-0015 | Proposed (`DEC-DOC-0009`) |
| `BRULE-DOC-0607` one document, one employee | AC-DOC-0019 | TS-DOC-0021 | Proposed |
| `BRULE-DOC-0608` content is immutable | AC-DOC-0020 | TS-DOC-0022 | Proposed |

## Security requirement coverage

| | Criteria | Scenarios |
|---|---|---|
| `SEC-DOC-0401` no writable ownership fields | AC-DOC-0002 | TS-DOC-0002 |
| `SEC-DOC-0402` export is scoped | AC-DOC-0011, AC-DOC-0012 | TS-DOC-0013, TS-DOC-0014 |
| `SEC-DOC-0403` import writes only in context | AC-DOC-0010 | TS-DOC-0011, TS-DOC-0012 |
| `SEC-DOC-0404` export column set recorded | AC-DOC-0015 | TS-DOC-0017 |
| `SEC-DOC-0405` content behind its own scope type | AC-DOC-0018 | TS-DOC-0020 |
| `SEC-DOC-0406` magic-byte verification | AC-DOC-0017 | TS-DOC-0019 |

## Non-functional requirement coverage

| | Criteria | Scenarios |
|---|---|---|
| `NFR-DOC-0501` caps | AC-DOC-0007 | TS-DOC-0008 |
| `NFR-DOC-0502` synchronous within caps | — | *Measured, not asserted — see [`test-scenarios.md`](test-scenarios.md#scenarios-deliberately-not-written)* |
| `NFR-DOC-0503` durable run records | AC-DOC-0015 | TS-DOC-0017 |
| `NFR-DOC-0504` cutover custody / fail fast | AC-DOC-0020 | TS-DOC-0023, TS-DOC-0024 |
| `NFR-DOC-0505` backup custody statement | — | *A documentation obligation on `ADR-028`, not a testable behaviour. Recorded here so it is not mistaken for one* |

## Decision coverage

| Decision | Where it lands |
|---|---|
| `DEC-DOC-0001` CSV only | AC-DOC-0001; `employee_import.format_unsupported` |
| `DEC-DOC-0002` strict header | AC-DOC-0001, AC-DOC-0002; TS-DOC-0001–0003 |
| `DEC-DOC-0003` per-row report | AC-DOC-0003, AC-DOC-0004; TS-DOC-0004, TS-DOC-0005 |
| `DEC-DOC-0004` idempotency key | AC-DOC-0008, AC-DOC-0009; TS-DOC-0009, TS-DOC-0010 |
| `DEC-DOC-0005` caps | AC-DOC-0007; TS-DOC-0008 |
| `DEC-DOC-0006` run records | AC-DOC-0005, AC-DOC-0015; TS-DOC-0006, TS-DOC-0017 |
| `DEC-DOC-0007` synchronous | Lifecycle — the absence of an `InProgress` state |
| `DEC-DOC-0008` export format | AC-DOC-0016; TS-DOC-0018 |
| `DEC-DOC-0009` terminated excluded | AC-DOC-0013; TS-DOC-0015 |
| `DEC-DOC-0010` `ADR-028` required | `README.md`; `NFR-DOC-0504`, `NFR-DOC-0505` |
| `DEC-DOC-0011` size and type limits | AC-DOC-0017; TS-DOC-0019 |
| `DEC-DOC-0012` document type enum | Data model; api-contracts |
| `DEC-DOC-0013` metadata vs content permissions | AC-DOC-0018; TS-DOC-0020 |

## Owner decisions and what they block

| | Blocks |
|---|---|
| `OD-DOC-001` | Whether this package exists in this shape; everything documents-related |
| `OD-DOC-002` | `FR-DOC-0102`; the AC-DOC-0001–0010 group's "waiting on" note |
| `OD-DOC-003` | `FR-DOC-0102`; the `outcome` semantics in api-contracts |
| `OD-DOC-004` | `FR-DOC-0102`; the column contract; `BRULE-DOC-0601` |
| `OD-DOC-005` | `FR-DOC-0101`, `FR-DOC-0102`, `FR-DOC-0201`; the authorization model |
| `OD-DOC-006` | `FR-DOC-0201`; AC-DOC-0016's round-trip property |
| `OD-DOC-007` | `ADR-028`; `NFR-DOC-0504`, `NFR-DOC-0505`; TS-DOC-0023 |
| `OD-DOC-008` | `FR-DOC-0304`; the one-way lifecycle |
| `OD-DOC-009` | Whether `FR-DOC-0301`–`0304` should be built before V5 at all |

## Precedent citations — what this package takes rather than decides

| Precedent | Taken as |
|---|---|
| `ADR-005` | Attachment metadata is tenant-owned |
| `ADR-012` | HR references no Platform assembly |
| `ADR-017` | No cross-database FK; SQL Server only |
| `ADR-020` | E3 manifest by construction; **binary content does not move; fail fast** |
| `ADR-022` §1, §6, §16 | Backup attaches to the physical database; readiness vocabulary; the platform does not delete |
| `ADR-023` d.8, d.22 | Branch execution context; scoped reads |
| `ADR-024` | Transfer is its own operation; history is not branch-owned |
| `ADR-025` d.8, d.10 | Permission and scope are independent; materialized predicates |
| `DEC-EMP-0011`, `DEC-EMP-0029`, `DEC-EMP-0030`, `DEC-EMP-0032` | No number generation; scope as a type; national-ID sensitivity; the deferral this package answers |
| `DEC-DEP-0023`–`0026`, `DEC-DEP-0029`, `DEC-DEP-0030` | Route/handler 1:1; named POSTs and no DELETE; permission mapping; own problem-code namespace; manifest by construction; `409` for state conflicts |
| `DEC-POS-0018`, `DEC-POS-0022`, `DEC-POS-0026`, `DEC-POS-0030`/`0031`, `DEC-POS-0034` | Scope-type-is-the-permission; the nine-site inventory; fail-loud migrations and no `defaultValue`; normalized search columns; specified-but-unshipped is a real failure mode |

## Orphan check

Every identifier defined by this package appears at least once above.

* **`FR-DOC-0101`–`0103`, `0201`–`0202`, `0301`–`0304`** — all 9 functional requirements traced.
* **`SEC-DOC-0401`–`0406`** — all 6 security requirements traced.
* **`NFR-DOC-0501`–`0505`** — all 5 non-functional requirements traced; two carry an explicit "not a testable
  behaviour" note rather than a false scenario reference.
* **`BRULE-DOC-0601`–`0608`** — all 8 business rules traced.
* **All 20 acceptance criteria traced**, enumerated rather than ranged so the check is literal:
  `AC-DOC-0001`, `AC-DOC-0002`, `AC-DOC-0003`, `AC-DOC-0004`, `AC-DOC-0005`, `AC-DOC-0006`, `AC-DOC-0007`, `AC-DOC-0008`, `AC-DOC-0009`, `AC-DOC-0010`, `AC-DOC-0011`, `AC-DOC-0012`, `AC-DOC-0013`, `AC-DOC-0014`, `AC-DOC-0015`, `AC-DOC-0016`, `AC-DOC-0017`, `AC-DOC-0018`, `AC-DOC-0019`, `AC-DOC-0020`.
* **All 24 test scenarios traced**, likewise:
  `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003`, `TS-DOC-0004`, `TS-DOC-0005`, `TS-DOC-0006`, `TS-DOC-0007`, `TS-DOC-0008`, `TS-DOC-0009`, `TS-DOC-0010`, `TS-DOC-0011`, `TS-DOC-0012`, `TS-DOC-0013`, `TS-DOC-0014`, `TS-DOC-0015`, `TS-DOC-0016`, `TS-DOC-0017`, `TS-DOC-0018`, `TS-DOC-0019`, `TS-DOC-0020`, `TS-DOC-0021`, `TS-DOC-0022`, `TS-DOC-0023`, `TS-DOC-0024`.
* **`DEC-DOC-0001`–`0013`** — all 13 proposed decisions traced.
* **`OD-DOC-001`–`009`** — all 9 owner decisions traced, each with what it blocks.

**No identifier is referenced that is not defined**, and no defined identifier is left untraced. The numbers
above are the check, not a summary of it: if a document adds an identifier without adding a row here, the two
lists stop agreeing and the discrepancy is visible.

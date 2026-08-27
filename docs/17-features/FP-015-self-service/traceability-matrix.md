---
package: FP-015
title: Self Service — Traceability Matrix
status: DRAFT
version: 0.1
date: 2026-08-27
---

# Traceability Matrix — FP-015

**Modern shape**: four cells, anchored by `REQ-` in cell one, criteria in cell three, scenarios in
cell four. `trace-check.py` parses this without a convention exception.

| Requirement | Ruling | Criteria | Scenarios |
|---|---|---|---|
| `REQ-SS-0001` | `OD-SS-0002` | `AC-SS-0001`, `AC-SS-0002`, `AC-SS-0015` | `TS-SS-0001`, `TS-SS-0003`, `TS-SS-0013` |
| `REQ-SS-0002` | `OD-SS-0002` | `AC-SS-0003`, `AC-SS-0004` | `TS-SS-0002`, `TS-SS-0003` |
| `REQ-SS-0003` | `OD-SS-0001` | `AC-SS-0005`, `AC-SS-0006` | `TS-SS-0004`, `TS-SS-0005` |
| `REQ-SS-0004` | `OD-SS-0001` | `AC-SS-0007` | `TS-SS-0003` |
| `REQ-SS-0005` | `OD-SS-0003` | `AC-SS-0008`, `AC-SS-0009` | `TS-SS-0006`, `TS-SS-0007` |
| `REQ-SS-0006` | `OD-SS-0004` | `AC-SS-0010`, `AC-SS-0011` | `TS-SS-0008`, `TS-SS-0009` |
| `REQ-SS-0007` | `OD-SS-0004` | `AC-SS-0012` | `TS-SS-0010` |
| `REQ-SS-0008` | `OD-SS-0005` | `AC-SS-0013`, `AC-SS-0014` | `TS-SS-0011`, `TS-SS-0012` |

---

## Two rows that must be read together

**`REQ-SS-0006` and `REQ-SS-0007`** are separate rows and a single implementation decision. The
obvious way to satisfy `0007` — severing the `ADR-030` mapping at termination — **makes `TS-SS-0009`
fail**, because a payslip whose employee cannot be resolved is no longer attributable.

**The matrix cannot express that, so it is written here.** `TS-SS-0009` carries the same note.

## `TS-SS-0003` covers three requirements and that is deliberate

`REQ-SS-0001`, `0002` and `0004` all rest on the endpoint carrying **no employee identifier.** One
scenario asserts it against the transport contract rather than three asserting it against three
handlers — **a property of the contract cannot be satisfied by a handler that remembers to check.**

## External references

`REQ-ATT-0023` (FP-013) and `OD-PAY-0016` (FP-012) are the deferrals this package closes. **Neither
is redefined here** — `REQ-SS-0001` and `REQ-SS-0002` are this package's own requirements, and the
closure is recorded in `README.md`.

## Declared gaps

**None.** Eight requirements, fifteen criteria, thirteen scenarios, every cell populated.

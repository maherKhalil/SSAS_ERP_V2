---
document_id: FP-010
title: HR Employee Documents
status: Deferred — gated on ADR-028
version: 0.1
module: HR
milestone: TBD
depends_on:
  - ADR-005
  - ADR-020
  - ADR-022
  - ADR-028
  - FP-006
  - FP-009
---

# Feature Package 010 — HR Employee Documents

> **Deferred, not open.** This package exists because `OD-DOC-001` ruled a **split** on 2026-08-22: FP-009 is
> Employee Import and Export, and `REQ-HR-0005` Employee Documents became FP-010, **gated on `ADR-028`**.
>
> It is **not a complete feature package** and does not pretend to be. It is the analysis that was already
> done, moved here intact, plus the three owner decisions that were deferred rather than answered. Its job is
> to be the starting inventory for whoever picks documents up — not to look finished.

## Status and what unblocks it

| | |
|---|---|
| **Blocked by** | `ADR-028` — Binary Content Storage, Custody and Movement, which does not exist |
| **`ADR-028` is blocked by** | `OD-DOC-007` — where binary content lives |
| **Also open** | `OD-DOC-008` (retention and erasure), `OD-DOC-009` (V5 Document Management ownership) |
| **Not blocked by** | FP-009. The two packages share an identifier space and nothing else |

**`OD-DOC-009` may end this package rather than start it.** The Product Roadmap places **Document Management
at Version 5**. If the owner rules that V5 owns this capability, `REQ-HR-0005` stays deferred — exactly as
`DEC-EMP-0032` already deferred it once — and FP-010 is closed rather than built. That question is asked
before the storage question is worth answering.

## What is here

| Document | Contents |
|---|---|
| [`decisions-open.md`](decisions-open.md) | The three deferred owner decisions with their options tables intact, and the four ratified decisions carried in |
| [`carried-analysis.md`](carried-analysis.md) | The domain, data, lifecycle, authorization and contract material for documents, moved from the FP-009 analysis unchanged |

**What is deliberately absent**: requirements, acceptance criteria and test scenarios as documents of their
own. Their *identifiers* travelled and are listed below, but writing them out as a finished package would be
specifying a feature whose foundational decision has not been made — and a specification that outruns its
decisions is how a package acquires content nobody approved.

## Identifiers this package holds

One `DEC-DOC`/`OD-DOC` space spans FP-009 and FP-010, and **nothing was renumbered** when the split
happened. A decision identifier is a citation; re-pointing one at a different document later is how a
traceability matrix starts lying.

| Kind | Held here |
|---|---|
| Requirements | `FR-DOC-0301` upload, `FR-DOC-0302` list, `FR-DOC-0303` download content, `FR-DOC-0304` withdraw |
| Security | `SEC-DOC-0405` content behind its own scope type, `SEC-DOC-0406` magic-byte verification |
| Non-functional | `NFR-DOC-0504` cutover custody, `NFR-DOC-0505` backup custody |
| Business rules | `BRULE-DOC-0607` one document, one employee; `BRULE-DOC-0608` content is immutable |
| Criteria | `AC-DOC-0017`, `AC-DOC-0018`, `AC-DOC-0019`, `AC-DOC-0020` |
| Scenarios | `TS-DOC-0019`, `TS-DOC-0020`, `TS-DOC-0021`, `TS-DOC-0022`, `TS-DOC-0023`, `TS-DOC-0024` |
| Decisions | `DEC-DOC-0010`, `DEC-DOC-0011`, `DEC-DOC-0012`, `DEC-DOC-0013` — ratified as drafted, FP-010-scoped |
| Owner decisions | `OD-DOC-007`, `OD-DOC-008`, `OD-DOC-009` — **open-deferred** |

New decisions in either package continue the same monotonic sequence from `DEC-DOC-0014`.

## The written authority

Two statements, and they pull in opposite directions:

1. **`ADR-005` (Multi-Tenancy)** names **"Attachment Metadata"** among its tenant-owned entity examples. It
   is an example in an enumeration rather than a decision about attachments — but it does establish that when
   attachment metadata exists, it is tenant-owned like everything else.
2. **`Product-Roadmap.md`** places **Document Management at Version 5**.

`REQ-HR-0005` itself is a **title with no body**, and no business rule in the specification touches it.

## The two facts that make `ADR-028` unavoidable

Both were established during the FP-009 analysis and neither is negotiable by a feature package:

**`ADR-020` already says binary content does not move.** Among the object types the Shared→Dedicated copy
must handle, it names *"large objects, and future file/document storage — out-of-row or out-of-database
content that a row copy will not move"*, and requires the tooling to **fail fast** where an object type is
unsupported, because "a silent partial copy is the worst available outcome". Any storage option that puts
bytes outside the tenant database makes every document-holding tenant un-cutoverable until something
specifies what moves them.

**`ADR-022` attaches backup to the physical database.** Backup policy, chain and recovery readiness belong to
the physical `TenantDatabase` (§1). Content stored anywhere else is **outside the chain the platform
promises**, and a tenant reported `Protected` would have its metadata protected and its bytes protected by
something else — or by nothing. Whatever `OD-DOC-007` rules, the readiness vocabulary must not be allowed to
imply a guarantee it does not carry.

`ADR-028`'s scope — what it owns and what stays in this feature package — is stated in
[`decisions-open.md`](decisions-open.md#dec-doc-0010--adr-028-is-required).

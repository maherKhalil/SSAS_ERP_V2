---
document_id: FP-010-DEC
title: HR Employee Documents — Open and Carried Decisions
status: CLOSED — transferred to V5 Document Management (OD-DOC-009, 2026-08-23)
version: 0.1
---

# FP-010 — Open and Carried Decisions

> Moved here unchanged by the `OD-DOC-001` split (2026-08-22).
>
> **CLOSED 2026-08-23 by `OD-DOC-009`: V5 Document Management owns this capability.**
>
> `OD-DOC-009` is **answered**. `OD-DOC-007` and `OD-DOC-008` are **transferred to V5 as its starting
> inventory** — still open questions, but no longer HR's, and their options tables are intact below precisely
> so V5 does not re-derive them. `DEC-DOC-0010`–`0013` remain ratified engineering decisions describing how
> the feature *would* work; V5 may adopt, adapt or discard them, but it should not have to rediscover them.
>
> **`ADR-028` is not written, and is not HR's to write.** It is annotated throughout this document as
> **V5's to author when Document Management is built** — the module that owns a capability writes the ADR
> that binds it, and an ADR written speculatively by the module that decided *not* to build the thing would
> bind V5 to reasoning nobody validated against a real design.

## Classification carried from the FP-009 analysis

| # | Topic | Classification |
|---|---|---|
| 20 | Binary storage location | **OWNER-DECISION-REQUIRED** `OD-DOC-007` |
| 21 | `ADR-028` required | **RATIFIED — YES** `DEC-DOC-0010` |
| 22 | Metadata is a tenant-owned entity in the E3 manifest | **SETTLED-BY-PRECEDENT** — `ADR-005`; `DEC-DEP-0029` makes manifest entry automatic |
| 23 | Binary content is **not** moved by the E3 row copy | **SETTLED-BY-PRECEDENT** — `ADR-020`; fail fast |
| 24 | Size ceiling and content-type allowlist | **RATIFIED** `DEC-DOC-0011`, subject to `ADR-028` raising them to platform constraints |
| 25 | Document-type taxonomy | **RATIFIED** `DEC-DOC-0012` |
| 26 | Retention and deletion vs no-physical-delete | **OWNER-DECISION-REQUIRED** `OD-DOC-008` |
| 27 | Document permissions | **RATIFIED** `DEC-DOC-0013` |
| 28 | Roadmap ownership vs V5 Document Management | **OWNER-DECISION-REQUIRED** `OD-DOC-009` |
| 29 | Malware scanning | **OWNER-DECISION-REQUIRED — deferred into `ADR-028`** |

## Ratified decisions carried into FP-010

All four were **ratified as drafted on 2026-08-22** and annotated FP-010-scoped. They are ratified
*engineering* decisions inside a package whose *owner* decisions are open — which is a coherent state, and
worth saying plainly: they describe how the feature would work, not whether it will be built.

**`DEC-DOC-0010` — `ADR-028` is required** if documents ship in any form. Not required for import and export,
which is what the `OD-DOC-001` split acted on.

> **AS OF `OD-DOC-009` (2026-08-23): `ADR-028` IS V5'S TO WRITE.** The requirement stands — documents in any
> form still need it — but the obligation moved with the capability. What follows is the scope analysis,
> preserved so V5 inherits it rather than repeating it.

An ADR is warranted when a decision binds more than one module and cannot be revisited cheaply. Binary
content is both: the first module to store a file sets the platform's answer for every module after it, and
the choice is embedded in the cutover and backup machinery rather than in a feature.

**`ADR-028` — Binary Content Storage, Custody and Movement** would own exactly:

* **Where binary content lives** for `PlatformManaged`/`Shared`, `PlatformManaged`/`Dedicated` and
  `CustomerManaged` placements — the three are not obliged to answer the same way, and `ADR-017`'s topology
  is what makes that a real question.
* **The custody guarantee under `ADR-020`**: whether a tenant holding binary content can be cut over at all,
  what moves it, and — where it cannot move — the fail-fast obligation `ADR-020` already states.
* **The custody guarantee under `ADR-022`**: whether content is inside the physical database's backup chain,
  and if not, what independently backs it up and what "recovery readiness" then means.
* **Integrity and addressing** — content hash, immutability, and what a metadata row guarantees about the
  bytes it points at.
* **Platform-wide ceilings**: maximum object size and the content-type allowlist as architectural constraints
  rather than per-feature configuration.
* **Encryption at rest and key custody**, consistent with `ADR-022` §11's rule that keys never live in the
  Platform database.
* **Malware scanning stance** — required, optional, or explicitly out of scope for V1.
* **Erasure** — whether binary content can be physically destroyed on request, which is the one place the
  platform's no-physical-delete convention (`BR-PLT-0003`) may have to bend, and where that bending is
  allowed to be visible.

**What `ADR-028` would NOT own**, and what stays here: the employee-document taxonomy, the permissions, the
routes, and which documents an employee may have. Those are HR decisions about HR data.

**`DEC-DOC-0011` — Content-type allowlist and a 10 MB ceiling per document.** PDF, PNG, JPEG and plain text;
everything else refused by content type **and** by magic-byte inspection, because a declared content type is
caller input (`SEC-DOC-0406`). `ADR-028` may raise these to platform constraints; until it exists they are
feature-level and conservative.

**`DEC-DOC-0012` — Document type is a closed enum** — `Contract`, `Identification`, `Certificate`,
`Correspondence`, `Other` — persisted as its string name, following every other status and reason enum in the
module. A customer-defined taxonomy is a V5 Document Management concern (`OD-DOC-009`).

**`DEC-DOC-0013` — Metadata and content carry different permissions.** Listing an employee's documents is
`HR.EmployeeDocuments.View`; **downloading content** is a separate authority, and the mechanism is
`DEC-POS-0018`'s: a distinct scope type that only the resolver checking the content permission can
construct, so a metadata-only caller cannot reach content through any code path — not a check they might
bypass, a type they cannot construct. Uploading is `HR.EmployeeDocuments.Upload`; withdrawal is
`HR.EmployeeDocuments.Withdraw`.

## The deferred owner decisions — now V5's starting inventory

> **`OD-DOC-009` is ANSWERED (V5 owns this).** The two below are **transferred, not resolved**: they remain
> genuinely open, and they are recorded here in full so that whoever builds Document Management starts from
> the analysis rather than from the question.

### `OD-DOC-007` — Where does binary content live?

| Option | Cutover (`ADR-020`) | Backup (`ADR-022`) | Other consequences |
|---|---|---|---|
| **In-database `varbinary(max)`** | **Moves with the row copy** — the only option that does. It is table data, ordered by the same FK rules | **Covered by the physical database's chain** — `ADR-022` §1 attaches backup to the database, so content is protected by construction | Database size grows with file volume; backup windows and restore times grow with it; a 10 MB ceiling matters |
| **Filesystem beside the database** | **Does not move.** `ADR-020` requires fail-fast; cutover of a document-holding tenant is blocked until `ADR-028` specifies a content move step | **Not covered.** The backup chain protects the database; the files need an independent, separately verified backup, and "recovery readiness" no longer means what `ADR-022` §6 says it means | Cheapest storage; hardest custody story; multi-node hosting needs shared storage |
| **External object store** | **Does not move** — but the pointer does, and if the store is shared across placements the content may not need to move at all. That is exactly the kind of thing an ADR must state rather than assume | **Not covered** by the database chain; the store has its own durability guarantees, which must be reconciled with `ADR-022`'s readiness model | Adds an external dependency and a credential-custody problem (`ADR-022` §11: keys never in the Platform database); the natural long-term answer |

**Engineering note (labeled, not a recommendation):** in-database storage is the only option under which the
platform's *existing* cutover and backup guarantees continue to hold with no new machinery. It is also the
option that scales worst. Whichever is ruled, `ADR-028` is what records it.

### `OD-DOC-008` — Retention and erasure, against `BR-PLT-0003`

The platform does not physically delete (`BR-PLT-0003`, and `ADR-022` §16 — the platform does not delete
backups in V1). An employee document may be subject to a legal erasure obligation, and a soft-deleted row
still holds the bytes. Backups hold them for as long as the retention policy says.

| Option | Consequence |
|---|---|
| **Soft delete only** | Consistent with every other entity. Erasure obligations are not met, and the platform should say so plainly rather than imply otherwise |
| **Physical destruction of content, metadata retained** | The audit trail survives — who uploaded what and when — while the bytes go. Backups still hold them until they age out, which must be stated to the customer rather than papered over |
| **Full erasure including backups** | Requires backup-chain surgery that `ADR-022` explicitly does not do in V1 |

**This is legal and contractual before it is technical.**

### `OD-DOC-009` — Does V5 Document Management own this? — **RULED 2026-08-23: YES**

> **The owner ruled that V5 owns employee documents.** `REQ-HR-0005` stays deferred and traceable, FP-010 is
> closed, and no stopgap is built — so there is no migration into the V5 capability to plan, and V5 inherits
> no data shaped by a feature package rather than by its own design. That was option 2 below.
>
> **Asking this one first was the point.** It was the only question of the three that could close the package
> rather than unblock it, and answering `OD-DOC-007` before it would have produced an `ADR-028` for a
> capability nobody intended to build here.

The Product Roadmap places **Document Management at Version 5**. If employee documents ship before it, either
they are a deliberate stopgap that V5 replaces — with a migration to specify — or V5's scope shrinks to
exclude what HR already has.

| Option | Consequence |
|---|---|
| **Build now as a stopgap** | HR gets documents years earlier. A migration into the V5 capability must be planned, and V5 inherits data shaped by a feature package rather than by its own design |
| **Wait for V5** | `REQ-HR-0005` stays unimplemented and openly deferred, exactly as `DEC-EMP-0032` already deferred it once. No stopgap to migrate |
| **Build now, and V5 is scoped around it** | Requires a product commitment made now about a version far away |

**Ask this one first.** It is the only one of the three that can close this package rather than unblock it,
and answering `OD-DOC-007` before it risks producing an `ADR-028` for a capability nobody intends to build
yet.

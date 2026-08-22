---
document_id: FP-009
title: HR Employee Data Exchange — Documents, Import, Export
status: Analysis — Owner Decisions Required
version: 0.1
module: HR
milestone: Milestone 1
depends_on:
  - ADR-005
  - ADR-012
  - ADR-017
  - ADR-020
  - ADR-022
  - ADR-023
  - ADR-024
  - ADR-025
  - FP-005
  - FP-006
  - FP-007
  - FP-008
---

# Feature Package 009 — HR Employee Data Exchange

> **This is analysis, not an approved specification.** Nothing here is binding. Every topic is classified in
> [`decisions-approved.md`](decisions-approved.md) as **SETTLED-BY-PRECEDENT** (with the citation),
> **PROPOSED** (an engineering recommendation, ratifiable as drafted), or **OWNER-DECISION-REQUIRED** (a
> business or product call this package must not make on its own).
>
> **Nine owner decisions are raised.** The first one decides whether this package should exist in this shape
> at all, and it is raised before anything else because the answer changes what the other eight are attached
> to.

## What this package covers

Three source requirements, all of which appear in the requirement catalog as **titles with no body text**:

| Source | Name | Body text exists? |
|---|---|---|
| `REQ-HR-0005` | Employee Documents | **No** |
| `REQ-HR-0009` | Employee Import | **No** |
| `REQ-HR-0010` | Employee Export | **No** |

**No business rule in the product specification touches any of the three.** `BR-HR-*` says nothing about
documents, import or export; neither does `BR-PLT-*` beyond the platform-wide conventions that apply to
everything. There is no prior art in this repository to lean on and no written intent to interpret.

That absence is the defining fact of this analysis. FP-008 derived a position model from `BR-HR-0006`, which
at least stated a rule. Here there is nothing to derive *from* — only three names, a roadmap line, and one
example in an ADR. Everything below is therefore either a platform convention that applies by precedent, an
engineering proposal that says so, or a question for the owner.

## The whole of the written authority

Four statements exist. They are quoted in full because there is nothing else.

1. **The requirement catalog** — `docs/00-Master-Product-Specification/Requirement-Catalog/HR.md` lists
   `REQ-HR-0005 Employee Documents`, `REQ-HR-0009 Employee Import`, `REQ-HR-0010 Employee Export` under
   *Employee Management*. Titles only.
2. **`ADR-005` (Multi-Tenancy)** names **"Attachment Metadata"** in its list of tenant-owned entity examples.
   It is an *example* in an enumeration, not a decision about attachments — but it does establish that when
   attachment metadata exists, it is tenant-owned like everything else.
3. **`Product-Roadmap.md`** lists **"Document Management"** as a **Version 5** capability, alongside
   Manufacturing, BI, the AI Assistant and the Customer and Vendor portals.
4. **`ADR-020` (Shared→Dedicated Cutover)**, in *Object types the copy must handle*, names
   **"Large objects, and future file/document storage — out-of-row or out-of-database content that a row copy
   will not move"**, and requires the tooling to **fail fast** where an object type is not supported.

The fourth is the one that shapes this package. The platform has already written down that document storage
is a movement problem it has not solved, and has already decided what to do about it in the meantime: refuse
loudly rather than copy incompletely.

## `OD-DOC-001` — the packaging question, raised first

**Should Documents, Import and Export be one package, or should Import/Export proceed now with Documents
split out behind a storage ADR?**

This is asked before the content questions because the answer determines whether the storage ADR blocks
everything or nothing.

### Option A — one package, all three requirements

| | |
|---|---|
| **Delivery order** | Nothing ships until the binary-storage decision (`OD-DOC-007`) is made and `ADR-028` is written and approved |
| **Storage ADR** | **Blocks everything**, including import and export, which need no binary storage of any kind |
| **Roadmap overlap** | One package to reconcile with V5 Document Management, later, once |
| **Coherence** | "Employee data exchange" is a real theme: all three move employee data across the system boundary, and all three raise the same PII questions |

### Option B — Import/Export now; Documents behind `ADR-028`

| | |
|---|---|
| **Delivery order** | Import and export ship on existing precedent; documents wait for the ADR |
| **Storage ADR** | **Blocks only documents.** It is the only one of the three that needs it |
| **Roadmap overlap** | Two packages to reconcile with V5 Document Management — but the documents package can be *scheduled against* V5 rather than shipped ahead of it |
| **Coherence** | Splits a coherent theme. The PII questions get asked twice, and the second package must not quietly re-answer them differently |

### What separates them

**Import and export need nothing this platform does not already have.** They read and write the Employee
aggregate that FP-006 shipped, under the read scope FP-006 built, through the transport conventions FP-006C5
established. Every hard question they raise — atomicity, idempotency, PII — is a *business* question, not an
architectural one.

**Documents need a decision the platform has never made.** Where binary content lives determines whether
`ADR-020` cutover can move a tenant at all and whether `ADR-022` backup covers the content — and `ADR-022`
attaches backup to the **physical database**, which means content stored anywhere else is outside the backup
chain the platform promises. That is not a detail to settle in a feature package.

> **ENGINEERING RECOMMENDATION (labeled as such — this is not a ruling).** **Option B.** Three reasons, in
> order of weight:
>
> 1. **The blocking asymmetry.** Bundling makes a storage ADR that import and export have no use for into a
>    precondition for shipping them. That is a cost with no matching benefit.
> 2. **The roadmap conflict is real and is about documents only.** Document Management is a **V5** capability.
>    Building an employee-document store now risks building it twice, and the second build inherits data from
>    the first. Import and export raise no such conflict — nothing on the roadmap claims them.
> 3. **`ADR-020` already says documents are unsolved.** A package that ships binary content before that ADR
>    exists would put tenants into a state the platform's own cutover plan is written to refuse.
>
> The cost is honest and worth stating: the PII questions (`OD-DOC-005`, `OD-DOC-006`) would be answered in
> the import/export package and would need to be *applied*, unchanged, by the documents package later. This
> analysis keeps them in one place so that stays cheap.

### If the split is ruled, what the packages would be called

| Package | Directory | Covers |
|---|---|---|
| **FP-009 — HR Employee Import and Export** | `docs/17-features/FP-009-hr-employee-import-export/` | `REQ-HR-0009`, `REQ-HR-0010` |
| **FP-010 — HR Employee Documents** | `docs/17-features/FP-010-hr-employee-documents/` | `REQ-HR-0005`, gated on `ADR-028` |

This analysis is filed under `FP-009-hr-employee-data-exchange/` because it must exist before the ruling that
decides how to cut it. **If Option B is ruled, this directory is renamed to the import/export package and the
documents material moves to FP-010 unchanged** — the analysis does not need to be rewritten, only split.

## Identifier space — `DEC-DOC` / `OD-DOC`

The prefix is **`DOC`**, and it is the *package's* prefix rather than the *documents topic's* prefix: it
covers all three requirements. `DEC-DOC-0001…` and `OD-DOC-001…` start at the beginning of a fresh space and collide
with nothing — `DEC-EMP` (FP-006), `DEC-DEP` (FP-007) and `DEC-POS` (FP-008) are all closed spaces.

**The space survives the split.** If `OD-DOC-001` rules for two packages, both continue to cite these
identifiers and both allocate new ones from the *same* monotonic sequence. Decision identifiers are citations;
re-pointing them at different documents later is how a traceability matrix starts lying. Nothing is renumbered
under any outcome of `OD-DOC-001`.

Requirement-level identifiers follow the same prefix: `FR-DOC`, `SEC-DOC`, `NFR-DOC`, `BRULE-DOC`, `AC-DOC`,
`TS-DOC`.

## Owner decisions required before approval

| | Decision | Blocks |
|---|---|---|
| `OD-DOC-001` | **Packaging** — one package, or import/export now with documents behind a storage ADR | Everything below; raised first |
| `OD-DOC-002` | **Import mode** — create-only, or upsert existing employees | Import scope, authorization, idempotency |
| `OD-DOC-003` | **Import atomicity** — all-or-nothing, or partial success with a per-row report | Import contract and its failure surface |
| `OD-DOC-004` | **Classification resolution** — how `department`, `position` and `branch` resolve from file values, and whether an import may create a missing one | Import file contract |
| `OD-DOC-005` | **Import/export permissions** — separately granted sensitive permissions, or reuse of `Create`/`View` | Authorization model for both |
| `OD-DOC-006` | **Export PII surface** — whether `nationalId` may leave the system in an export | Export contract; compliance |
| `OD-DOC-007` | **Document binary storage** — in-database, filesystem, or external object store | `ADR-028`; the entire documents half |
| `OD-DOC-008` | **Document retention and erasure** — against the platform's no-physical-delete convention | Document lifecycle; compliance |
| `OD-DOC-009` | **Roadmap ownership** — whether V5 Document Management owns this, making an employee-document store a deliberate stopgap | Whether the documents half should be built at all now |

Each is stated in full, with options and consequences, in
[`decisions-approved.md`](decisions-approved.md#owner-decisions-required).

## `ADR-028` — required, and what it would own

**YES, if documents ship in any form.** Not required for import and export.

An ADR is warranted when a decision binds more than one module and cannot be revisited cheaply. Binary
content is both: the first module to store a file sets the platform's answer for every module after it, and
the choice is embedded in the cutover and backup machinery rather than in a feature.

**`ADR-028` — Binary Content Storage, Custody and Movement** would own exactly:

* **Where binary content lives** for `PlatformManaged`/`Shared`, `PlatformManaged`/`Dedicated` and
  `CustomerManaged` placements — the three are not obliged to answer the same way, and `ADR-017`'s topology
  is what makes that a real question.
* **The custody guarantee under `ADR-020`**: whether a tenant holding binary content can be cut over at all,
  what moves it, and — where it cannot move — the **fail-fast** obligation `ADR-020` already states.
* **The custody guarantee under `ADR-022`**: whether content is inside the physical database's backup chain,
  and if not, what independently backs it up and what "recovery readiness" then means. `ADR-022` §1 attaches
  readiness to the physical database; content outside it is outside that claim.
* **Integrity and addressing** — content hash, immutability of stored content, and what a metadata row
  guarantees about the bytes it points at.
* **Platform-wide ceilings**: maximum object size and the content-type allowlist as architectural constraints
  rather than per-feature configuration.
* **Encryption at rest and key custody**, consistent with `ADR-022` §11's rule that keys never live in the
  Platform database.
* **Malware scanning stance** — required, optional, or explicitly out of scope for V1.
* **Erasure** — whether binary content can be physically destroyed on request, which is the one place the
  platform's no-physical-delete convention (`BR-PLT-0003`) may have to bend, and where that bending is
  allowed to be visible.

**What `ADR-028` would NOT own**, and what stays in the feature package: the employee-document *taxonomy*,
the permissions, the routes, and which documents an employee may have. Those are HR decisions about HR data.

## Precedent stack this package inherits

| Precedent | What it settles here |
|---|---|
| `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029` | Every read is scoped, materialized, and never an omitted predicate — **including export** |
| `ADR-024`, `DEC-EMP-0007` | What is branch-owned and what merely names a branch |
| `ADR-017` | No cross-database foreign key; SQL Server is the only V1 provider |
| `ADR-020`, `DEC-DEP-0029`, `DEC-POS-0022` | Tenant-owned entities enter the E3 copy manifest **by construction**; the nine-site inventory obligation |
| `ADR-022` | Backup and recovery attach to the physical database — the fact that decides `OD-DOC-007` |
| `DEC-DEP-0024`, `DEC-DEP-0026`, `DEC-DEP-0030` | Named `POST` routes, no `DELETE` verb, per-resource problem-code namespace, `409` for state-conflict refusals |
| `DEC-POS-0018` | Scope-type-is-the-permission — the mechanism for a sensitive PII read |
| `DEC-POS-0030`/`0031` | Normalized columns for any searchable text |
| `DEC-EMP-0030` | Sensitivity precedent for the national identifier |

## What this package deliberately does not do

* It does not choose a storage location for binary content. That is `OD-DOC-007` and then `ADR-028`.
* It does not design a general document-management capability. The roadmap places that at V5, and
  `OD-DOC-009` asks whether anything should be built before it.
* It does not extend import or export beyond the **Employee** aggregate. Department, position and grade
  import are not in these requirements and are not smuggled in.
* It does not invent business rules. Where the requirement catalog is silent and precedent does not reach,
  the question is raised rather than answered.

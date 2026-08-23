---
package: FP-011
title: General Ledger — Traceability Matrix
status: APPROVED — anchored, now that Requirement-Catalog/GL.md exists (2026-08-23)
version: 1.0
date: 2026-08-23
---

# FP-011 — Traceability Matrix

> **COMPLETED 2026-08-23.** `OD-GL-0001` ratified the drafted lines into
> `Requirement-Catalog/GL.md`, so the REQ column now anchors to real catalog entries rather than to
> proposals. The "Blocked by" column below is kept as the record of what each row was waiting on and which
> ruling released it.
>
> What follows is the finding as it stood, preserved because the gap it describes is the reason this package
> exists in the form it does.
>
> **This matrix could not be completed, and the reason was the package's top-line finding.**
>
> Traceability in this product is REQ-anchored: `Requirement-Catalog/Traceability-Matrix.md` maps
> `Requirement → BR → Feature → Screen → API → Table → Permission → Test`. GL has **no requirement catalog
> entry to anchor to**. Every `REQ-GL-` identifier below is a *proposal* from [requirements.md](requirements.md),
> and until `OD-GL-0001` is answered every row here is provisional.
>
> It is published incomplete rather than withheld, because the shape of what is missing is itself the
> deliverable. FP-009 ratified the standard that applies: **an inventory with a footnote saying it is
> incomplete is not an inventory** — so the incompleteness is stated in the status line and in every affected
> column, not in a footnote.

---

# Proposed requirement → rule → criterion → scenario

| REQ (proposed) | Business rule | Acceptance | Scenarios | Blocked by |
|---|---|---|---|---|
| `REQ-GL-0001` Record a balanced journal | `BR-GL-0001`, `BR-GL-0004` | `AC-GL-0001`–`0003` | `TS-GL-0001`, `TS-GL-0004`, `TS-GL-0005` | `OD-GL-0002`, `OD-GL-0005`, `OD-GL-0007` |
| `REQ-GL-0002` Refuse unbalanced | `BR-GL-0001` | `AC-GL-0004` | `TS-GL-0002`, `TS-GL-0003` | — |
| `REQ-GL-0003` Posted journals immutable | `BR-GL-0002` | `AC-GL-0005` | `TS-GL-0006`–`TS-GL-0008` | **`OD-GL-0007`** |
| `REQ-GL-0004` Reverse a posted journal | `BR-GL-0002` (by implication) | `AC-GL-0006` | `TS-GL-0009` | `OD-GL-0006` |
| `REQ-GL-0005` Create an account | Glossary: Account | `AC-GL-0007` | — | **`OD-GL-0003`** |
| `REQ-GL-0006` Update an account | — | `AC-GL-0008` | `TS-GL-0033` | code immutability unstated |
| `REQ-GL-0007` Deactivate an account | `BR-GL-0004` | `AC-GL-0009` | `TS-GL-0012`, `TS-GL-0013` | — |
| `REQ-GL-0008` View the chart | `BR-RPT-0002` | `AC-GL-0010` | `TS-GL-0025` | `OD-GL-0003` |
| `REQ-GL-0009` Define year and periods | Glossary: Fiscal Year/Period | `AC-GL-0011` | — | **`OD-GL-0004`** |
| `REQ-GL-0010` Close a period | `BR-GL-0003` | `AC-GL-0012` | `TS-GL-0010`, `TS-GL-0011` | `OD-GL-0004`; reopening unstated |
| `REQ-GL-0011` Unique journal number | `BR-GL-0005` | `AC-GL-0013` | `TS-GL-0034` | `OD-GL-0004`; **gapless unstated** |
| `REQ-GL-0012` Search journals | `BR-RPT-0001/0002` | `AC-GL-0014` | `TS-GL-0021`, `TS-GL-0029` | `OD-GL-0005` |
| `REQ-GL-0013` Balance enquiry | `BR-RPT-0001/0002` | `AC-GL-0015` | — | `OD-GL-0008` |
| `REQ-GL-0014` Trial balance | `BR-GL-0001`, `BR-RPT-0002` | `AC-GL-0016` | `TS-GL-0031`, `TS-GL-0032` | `OD-GL-0002` |

# Business rules — coverage, and what each leaves open

| Rule | Covered by | Fully settled by existing authority? |
|---|---|---|
| `BR-GL-0001` balance | `REQ-GL-0001`, `REQ-GL-0002`, `REQ-GL-0014` | **No** — "balanced in which currency" is `OD-GL-0002` |
| `BR-GL-0002` posted journals immutable | `REQ-GL-0003`, `REQ-GL-0004` | **No** — names no correction mechanism (`OD-GL-0006`), and says *posted* (`OD-GL-0007`) |
| `BR-GL-0003` closed periods prohibit posting | `REQ-GL-0010` | **No** — whose calendar (`OD-GL-0004`); reopening unstated |
| `BR-GL-0004` inactive accounts | `REQ-GL-0007` | **Yes** |
| `BR-GL-0005` journal number unique in year | `REQ-GL-0011` | **No** — unique within whose year, and unique ≠ gapless |

**One of five is fully settled.** That ratio is the argument for raising nine owner decisions rather than
proceeding on inference.

# Cross-cutting obligations

| Obligation | Source | Criterion | Scenario |
|---|---|---|---|
| Adopt `decimal(19,4)` **by recorded decision** | `ADR-027` deferred obligations | `AC-GL-0003` | `TS-GL-0004` |
| `nvarchar` everywhere | standing constraint | `AC-GL-0019` | `TS-GL-0017`, `TS-GL-0019` |
| No cross-database FK | standing constraint | `AC-GL-0020` | `TS-GL-0018` |
| E3 manifest + ten-site inventory | `DEC-POS-0022` | `AC-GL-0021` | `TS-GL-0015`, `TS-GL-0016` |
| Cutover must not mutate append-only rows | new — no prior coverage | — | **`TS-GL-0014`** |
| Permission defined, not merely named | FP-006P | `AC-GL-0018` | `TS-GL-0024` |
| Administer grants no functional permission | `ADR-025` d8 | `AC-GL-0017` | `TS-GL-0022` |
| Scope unforgeable, empty set refuses | `ADR-025` d10, `ADR-023` d22 | `AC-GL-0014` | `TS-GL-0020`, `TS-GL-0023` |

# Identifiers reserved but not yet used

`Requirement-Numbering.md` reserves a full GL identifier space. This package proposes only `REQ-GL-*`; the
rest are listed so the next package knows they exist and does not invent parallel names.

| Reserved | Used here | Note |
|---|---|---|
| `REQ-GL-0001` | **Proposed** | Aligned to the `# Example Mapping` row's implied meaning — journal posting |
| `FR-GL-0001` | No | Feature identifiers not drafted |
| `SCR-GL-0001` | No | No screen design in scope |
| `API-GL-0001` | No | Routes sketched but not identifier-tagged |
| `TBL-GL-JournalEntry` | **Adopted as the naming pattern** | The one reservation this package follows directly |
| `RPT-GL-0001` | No | Trial balance would be the first |
| `WF-GL-0001` | No | Posting workflow depends on `OD-GL-0007` |
| `TC-GL-0001` | No | `TS-GL-*` used pending the catalog decision |

> **`TS-GL-*` versus `TC-GL-*`.** `Requirement-Numbering.md` reserves `TC-GL-0001` for test cases; the feature
> packages use `TS-` for scenarios. This package uses `TS-GL-*` to match its siblings and records the
> discrepancy rather than resolving it — reconciling the product's test-identifier namespace is not FP-011's
> to do, and doing it in passing is exactly the kind of drive-by change `ADR-027` decision 4 warns against.

# What is NOT traced, and why

| Gap | Reason |
|---|---|
| Every REQ column | `OD-GL-0001` — no `GL.md` exists, so nothing is anchored |
| Year-end close | `OD-GL-0008` — not proposed, so not traced |
| Inbound postings from other modules | `OD-GL-0009` — no contract proposed |
| Import/export | No GL requirement exists for it anywhere |
| Screens and reports beyond trial balance | Out of scope for a foundation package |

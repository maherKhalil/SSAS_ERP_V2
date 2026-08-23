---
package: FP-011
title: General Ledger Foundation
module: General Ledger
status: APPROVED — all nine owner decisions closed, all ten engineering decisions ratified (2026-08-23)
version: 1.0
date: 2026-08-23
---

# FP-011 — General Ledger Foundation

> **APPROVED FOR IMPLEMENTATION, 2026-08-23.** The analysis is complete, all nine owner decisions are
> closed and all ten engineering decisions are ratified. This package is now the authority the GL build
> works from.
>
> It still contains no ADR. GL needs one eventually and it remains not this package's to write — the same
> reasoning that made `ADR-028` V5's rather than HR's when `OD-DOC-009` closed FP-010.

## Status

| | |
|---|---|
| **Status** | **APPROVED.** All nine owner decisions closed; all ten engineering decisions ratified |
| **Owner decisions** | **9 closed** (`OD-GL-0001` … `OD-GL-0009`) — see [decisions-approved.md](decisions-approved.md) |
| **Engineering decisions** | **10 ratified** (`DEC-GL-0001` … `DEC-GL-0010`) |
| **Requirements** | `REQ-GL-0001`–`0014` ratified into `Requirement-Catalog/GL.md` |
| **Blocks** | Nothing. The schema is unblocked |
| **Does not block** | Anything shipped. HR's V1 catalog is complete and GL touches none of it |

## The nine rulings

| | Decision | Ruled |
|---|---|---|
| `OD-GL-0001` | Requirement catalog | Drafted `REQ-GL` lines ratified into `GL.md`, `HR.md`'s flat shape — **precedent over template** |
| `OD-GL-0002` | Currency | **Single currency V1.** `ADR-027` unchanged and unamended |
| `OD-GL-0003` | Chart of accounts | **Tenant-level.** `Account : ITenantOwnedEntity` only; balances never stored above company |
| `OD-GL-0004` | Fiscal calendar | **Company-level.** Journal numbers unique within *(CompanyId, FiscalYear)*; close is a company-scoped write |
| `OD-GL-0005` | Branch dimension | **None in V1.** Read scope is tenant + company |
| `OD-GL-0006` | Correction | **Reversal journal carrying `ReversesJournalId`** |
| `OD-GL-0007` | Drafts | **Two aggregates** — `JournalDraft` mutable, `JournalEntry` append-only, posting is the promotion |
| `OD-GL-0008` | Year-end close | **Out of V1.** Period close only |
| `OD-GL-0009` | Inbound postings | **None.** Manual entry only; Payroll (V2) is the first poster |

## The top-line finding: GL has no requirements

**`docs/00-Master-Product-Specification/Requirement-Catalog/` contains `Platform.md`, `HR.md`,
`Constraints.md` and `Non-Functional-Requirements.md`. There is no `GL.md`.**

This is not an oversight anyone can shrug at, because the catalog *promises* the domain:

* `Requirement-Catalog/README.md` declares the domain table row **`REQ-GL | General Ledger`**.
* `Requirement-Numbering.md` reserves **`REQ-GL-0001`**, `FR-GL-0001`, `SCR-GL-0001`, `API-GL-0001`,
  `TBL-GL-JournalEntry`, `RPT-GL-0001`, `WF-GL-0001` and `TC-GL-0001`.
* `Traceability-Matrix.md` shows a **`REQ-GL-0001`** row — under the heading **`# Example Mapping`**. It is
  an illustration, and it is reported here as one: it carries no authority, and calling it a dangling
  traceability row would overstate it. What it does do is fix the intended naming, and this package adopts
  that naming rather than inventing a second one.

So the identifier space is reserved, the domain is declared, the business rules exist — and the requirement
lines they would trace to have never been written. **Every prior feature package began by reading its
requirements. FP-011 is the first that cannot.** That is `OD-GL-0001`, and it is raised first because the
answer determines whether the requirement lines drafted in [requirements.md](requirements.md) are a
contribution or a presumption.

**What this package does about it:** [requirements.md](requirements.md) drafts **PROPOSED** `REQ-GL-` lines,
every one marked `OWNER-DECISION-REQUIRED` and every one traced to an existing `BR-GL-` rule or to the
Glossary. They are drafted rather than omitted because the alternative — a GL package that says "requirements
are missing" and stops — leaves the owner with the same blank page and none of the analysis. They are marked
rather than merged because **a feature package does not get to write the product's requirement catalog by
filing a pull request.**

## What GL inherits

GL is the first module built after the platform's foundations settled, so it inherits more than any package
before it. [decisions-approved.md](decisions-approved.md) records the architect's inheritance memo verbatim; the
mechanisms are traced to real code in [domain-model.md](domain-model.md) and [data-model.md](data-model.md).

The inheritance with the sharpest consequence:

**`ADR-027` names General Ledger by name in its deferred obligations:**

> **General Ledger** must either adopt decision 1 or amend this ADR. Matching HR by observation, without a
> recorded decision, is the outcome this ADR exists to prevent.

`DEC-GL-0001` discharges that obligation explicitly. It is the one place this package takes a position
without an owner decision, because the ADR asks for a *recorded* decision and silence is the failure mode it
names.

**`IAppendOnlyEntity` acquires its biggest client.** `BR-GL-0002` — *"Posted Journals cannot be edited"* — is
not a rule GL must invent a mechanism for. The mechanism exists, is enforced structurally at the write
boundary by `TenantDbContext.PreventAppendOnlyMutation`, and already carries the same reasoning in its own
comment: *"a correction is another transfer, never a rewrite."* For GL that sentence reads **a correction is
another journal, never a rewrite** — which is the definition of a reversal. See `DEC-GL-0002`, and
`OD-GL-0007` for the part that is genuinely undecided: whether a *draft* journal is the same aggregate.

## Package contents

| File | What it is |
|---|---|
| [requirements.md](requirements.md) | `REQ-GL-0001`–`0014`, **ratified** into `Requirement-Catalog/GL.md` |
| [business-rules.md](business-rules.md) | `BR-GL-0001`–`0005` as they exist, and what each does and does not settle |
| [domain-model.md](domain-model.md) | Aggregates, the ownership dimensions, and which platform interfaces apply |
| [data-model.md](data-model.md) | Tables, types, the `nvarchar` and no-cross-database-FK constraints, E3 |
| [authorization-model.md](authorization-model.md) | Permission grammar, the unforgeable read scope, the three dimensions |
| [lifecycle-model.md](lifecycle-model.md) | Draft → Posted → Reversed, and fiscal period states |
| [api-contracts.md](api-contracts.md) | Route surface sketch, strict transport rules inherited from HR |
| [acceptance-criteria.md](acceptance-criteria.md) | `AC-GL` lines against the proposed requirements |
| [test-scenarios.md](test-scenarios.md) | `TS-GL` scenarios, including the ones that must exist for E3 |
| [decisions-approved.md](decisions-approved.md) | **`OD-GL-0001`–`0009` closed** and **`DEC-GL-0001`–`0010` ratified** |
| [traceability-matrix.md](traceability-matrix.md) | REQ → BR → AC → TS, and every unresolved edge |

## What is deliberately absent

**No ADR.** GL will need at least one, and it is not this package's to write — the same reasoning that made
`ADR-028` V5's rather than HR's when `OD-DOC-009` closed FP-010. A package that authors the ADR binding a
module the owner has not yet scoped binds them to reasoning nobody validated.

**No schema, no migration, no code — in THIS package.** They were withheld until `OD-GL-0002` and
`OD-GL-0003` were answered, because each changes the column list of every table in the module. Both are
answered now, so the build proceeds from here rather than from guesswork.

**No estimate and no sequencing.** Those follow the owner decisions, not the other way round.

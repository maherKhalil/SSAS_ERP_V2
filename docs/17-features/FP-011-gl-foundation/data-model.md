---
package: FP-011
title: General Ledger — Data Model
status: APPROVED — table shapes settled; the migration is the build's to author
version: 1.0
date: 2026-08-23
---

# FP-011 — Data Model

> **DECISIONS CLOSED, 2026-08-23.** All nine owner decisions are ruled; conditional wording below is kept as
> the record of what was weighed, with the ruling stated where it changes the answer.
>
> | | | | |
> |---|---|---|---|
> | `0001` catalog: ratified into `GL.md` | `0002` **single currency** | `0003` **tenant-level chart** | `0004` **company calendar** |
> | `0005` **no branch dimension** | `0006` **reversal + `ReversesJournalId`** | `0007` **two aggregates** | `0008` **period close only** |
> | `0009` **manual entry only** | | | |

> **No migration is authored and no schema is final.** `OD-GL-0002` (currency) and `OD-GL-0003` (chart
> ownership) each change the column list of tables in this module, so writing DDL now would be work discarded
> — and worse, would make undecided questions look settled.
>
> What is settled, and what this document is for, are the **constraints GL inherits whatever the answers
> are.** Those are not conditional and they are not negotiable.

## The non-negotiables

| Constraint | Applies to | Why |
|---|---|---|
| **Every persisted application string is `nvarchar`** | Account codes, names, journal numbers, descriptions, references — every one | Standing product constraint. `Constraints.md` requires Arabic and English support; `varchar` cannot carry it, and a single `varchar` column is a data-loss defect that surfaces only for the users who need it most |
| **Every monetary amount is `decimal(19,4)`** | Debit, Credit, and every derived amount | `ADR-027` decision 1, adopted by `DEC-GL-0001` |
| **No foreign key crosses the Platform/Tenant database boundary** | Any GL column carrying a platform-owned identifier | Standing constraint. The identifier is carried as a **value**, validated on write; there is no FK to enforce it and there must not be |
| **All GL tables live in the tenant database** | Every table below | GL is tenant-owned business data; `ADR-005` and `ADR-017` place it there |
| Identifiers follow `ADR-013` | Every key | Not GL's to choose |

## Tables

Names follow the reservation already in `Requirement-Numbering.md` — `TBL-GL-JournalEntry` — so the module's
table naming is not a new invention.

| Table | Purpose | Ownership | Append-only? |
|---|---|---|---|
| `GlAccount` | Chart of accounts | **Tenant only** (`OD-GL-0003`) | No — `IsActive` and `Name` change |
| `GlFiscalYear` | Fiscal years | **Tenant + company** (`OD-GL-0004`) | No |
| `GlFiscalPeriod` | Periods within a year | Via its year | No — `Status` changes on close |
| `GlJournalEntry` | Journal headers | Tenant + company | **Yes** (`OD-GL-0007` ruled two aggregates) |
| `GlJournalLine` | Journal lines | Via its entry | **Yes, with its entry** |
| `GlJournalDraft` *(+ lines)* | Editable drafts | Tenant + company | No — **exists**, per `OD-GL-0007` option 3 |

## Indexes and constraints that are already implied

| Constraint | Source | Shape |
|---|---|---|
| Journal number uniqueness | `BR-GL-0005` | Unique index on **_(CompanyId, FiscalYear, JournalNumber)_** — `OD-GL-0004` |
| Account code uniqueness | `REQ-GL-0005` | Unique **within the tenant** — the chart is tenant-level (`OD-GL-0003`), so `CompanyId` is not part of the key |
| Period non-overlap | `REQ-GL-0009` | Not expressible as a simple unique index; belongs to the aggregate, with a test that goes through real SQL |
| Line-to-entry cascade | aggregate ownership | Lines are deleted with their entry — **which, for an append-only entry, is a path that must never execute**. Both halves should be asserted |
| Balance | `BR-GL-0001` | **Deliberately not a database constraint.** `DEC-GL-0008` — a CHECK cannot see sibling rows, and a trigger puts a business rule where the domain cannot test it |

## Concurrency

`DEC-GL-0007`: `RowVersion` on **mutable aggregates only** — `GlAccount`, `GlFiscalYear`, `GlFiscalPeriod`.

A posted journal does **not** carry one. There is no concurrent update to detect, because the write boundary
refuses updates to it entirely; adding a `RowVersion` there would advertise a mutation that cannot happen and
would invite someone to write the update path it implies.

## The E3 cutover manifest — an obligation, not a note

Every table above whose entity implements `ITenantOwnedEntity` **joins the tenant cutover manifest
automatically**, because `TenantCutoverCopyPlan.Build` reflects over the interface rather than reading a list.
That is the good half: GL cannot forget to be copied.

**The half GL can get wrong** is the inventory of sites that assert the manifest. `DEC-POS-0022` records
**ten** sites, and FP-009 had to correct that number from nine — its table carried nine rows plus a prose note
about a tenth, and named the wrong method for one of them. The standard that came out of it is now ratified
and applies directly here:

> **An inventory with a footnote saying it is incomplete is not an inventory.**

`DEC-GL-0010` therefore makes updating the ten-site inventory part of the same change that adds a GL entity,
not a follow-up. GL adds more `ITenantOwnedEntity` types at once than any package since the platform itself,
so this is the package most able to leave that inventory stale.

**A second E3 obligation specific to GL:** the cutover copies rows between databases, and GL rows are
append-only. A copy that inserts into an append-only table is legitimate — it is an insert, not a mutation —
but nothing currently proves that the cutover path does not attempt an update on one. That test does not exist
because no append-only table has yet been large or central enough to make anyone ask. It is listed in
[test-scenarios.md](test-scenarios.md) as `TS-GL-0014`.

## Volume, and why it is mentioned at all

GL journal lines will be the highest-row-count table in the product. Two consequences worth recording before
the schema exists rather than after:

1. **The cutover copies them.** `TenantCutoverCopySqlServerTests` already exercises 20,000 rows plus co-tenant
   noise; GL is what makes that number look small. Whether the cutover's paging holds at GL volumes is a
   question for whoever writes the schema, and it is cheaper to design for than to discover.
2. **Reads are scope-filtered, always** (`DEC-GL-0004`). A scope predicate over a materialized identifier list
   is index-friendly, but the index has to exist. `DEC-POS-0030` is the relevant prior lesson — a
   value-converted property translates in a **projection** but not in a **predicate** — and GL should not
   discover it a third time.

## What is deliberately not here

No DDL. No column types beyond the two that are fixed by product-wide rule (`nvarchar`, `decimal(19,4)`). No
migration ordering. All of it follows the owner decisions; none of it can usefully precede them.

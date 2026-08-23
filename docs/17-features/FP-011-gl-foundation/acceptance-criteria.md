---
package: FP-011
title: General Ledger — Acceptance Criteria
status: APPROVED — every requirement these trace to is ratified (2026-08-23)
version: 1.0
date: 2026-08-23
---

# FP-011 — Acceptance Criteria

> **APPROVED.** Every `AC-GL-*` traces to a ratified `REQ-GL-*`. Where a criterion was written
> conditionally on an owner decision, the decision is now recorded beside it — the conditional wording is
> kept so a reader can see what the criterion would have been under the other answer.
>
> Two are now settled rather than conditional: **`AC-GL-0005` holds at full strength** (`OD-GL-0007` chose
> two aggregates, so posted-journal immutability is structurally enforced), and **`AC-GL-0013` asserts
> uniqueness only** — gaplessness was raised and deliberately not promised.

---

## Journal entry

**`AC-GL-0001`** — Given an open fiscal period and two or more active accounts, when a caller holding
`GL.Journals.Post` submits a journal whose debit total equals its credit total, the journal is persisted with
all its lines and is assigned a journal number unique within its fiscal year. → `REQ-GL-0001`, `REQ-GL-0011`

**`AC-GL-0002`** — The persisted journal records both the accounting date supplied by the caller and the
fiscal period resolved from it. The caller does not supply the period. → `REQ-GL-0001`

**`AC-GL-0003`** — Every monetary amount is persisted as `decimal(19,4)` and round-trips without loss of
precision. → `REQ-GL-0001`, `DEC-GL-0001`

**`AC-GL-0004`** — When the debit total differs from the credit total by any amount, the request is refused
with `Gl.JournalUnbalanced` and **no row of any kind is persisted** — not the header, not a partial set of
lines, and not a record of the attempt. → `REQ-GL-0002`

> The "not a record of the attempt" clause is deliberate and is **the opposite** of FP-009's import behaviour,
> where a refusal writes a run record because `DEC-DOC-0006` makes that record the audit trail. A ledger is
> not an import pipeline: a failed journal is not an event the books should carry. If the owner wants failed
> attempts logged, that is an audit concern and belongs outside the ledger tables — worth stating because the
> two packages sit next to each other and the inconsistency would otherwise look like an oversight.

**`AC-GL-0005`** — An attempt to update or delete a posted journal or any of its lines is refused, **by
whatever path it is attempted** — repository, direct context, or a future path nobody has written yet.
→ `REQ-GL-0003`

> Under `OD-GL-0007` options 1 and 3 this is satisfied structurally by `IAppendOnlyEntity` and the criterion
> is testable against the real write boundary. **Under option 2 it cannot be stated this strongly**, because
> the guarantee becomes an aggregate-level check that a new path can bypass. The criterion is written at full
> strength so that weakening it is visibly a consequence of the decision.

**`AC-GL-0006`** — A posted journal is corrected only by posting a reversing journal whose lines mirror the
original's debits and credits. The original is unchanged. → `REQ-GL-0004`
> Whether the reversal records `ReversesJournalId` is `OD-GL-0006`.

---

## Chart of accounts

**`AC-GL-0007`** — A caller holding `GL.Accounts.Manage` creates an account with a code unique within its
owning scope; a duplicate code is refused with a named error. → `REQ-GL-0005`
> The owning scope is tenant or company per `OD-GL-0003`, and that choice also decides whether this write runs
> `AuthorizeCurrentCompanyAsync`.

**`AC-GL-0008`** — An account's name may be updated. Concurrent updates are detected by `RowVersion` and the
loser is refused rather than silently overwriting. → `REQ-GL-0006`, `DEC-GL-0007`

**`AC-GL-0009`** — A deactivated account refuses new postings with `Gl.AccountInactive`, **and** journals
posted to it before deactivation remain readable and unchanged. → `REQ-GL-0007`, `BR-GL-0004`

**`AC-GL-0010`** — A caller sees only accounts within their authorized scope; an account outside it is
reported as not found rather than as forbidden. → `REQ-GL-0008`

---

## Fiscal calendar

**`AC-GL-0011`** — A fiscal year's periods are contiguous and non-overlapping; a definition that leaves a gap
or an overlap is refused. → `REQ-GL-0009`

**`AC-GL-0012`** — Posting into a closed period is refused with `Gl.FiscalPeriodClosed`. The check runs
against the period's state **at post time**, so a journal prepared while the period was open is still refused
once it closes. → `REQ-GL-0010`, `BR-GL-0003`

**`AC-GL-0013`** — Two journals in the same fiscal year cannot share a journal number; the second is refused
with `Gl.JournalNumberConflict`. → `REQ-GL-0011`, `BR-GL-0005`
> Whether numbers must also be **gapless** is unanswered by `BR-GL-0005` and is raised with `OD-GL-0004`. This
> criterion asserts uniqueness only, and deliberately does not assert the absence of gaps — asserting it would
> commit the product to an obligation nobody has agreed.

---

## Read and reporting

**`AC-GL-0014`** — Every journal read requires a `JournalReadScope`; a caller whose authorized company set is
empty is **refused**, not served an empty page. → `REQ-GL-0012`, `DEC-GL-0004`

**`AC-GL-0015`** — An account balance enquiry returns only movements within the caller's authorized scope, and
its total equals the sum of the movements it returned. → `REQ-GL-0013`

**`AC-GL-0016`** — A trial balance's debit total equals its credit total for any period in which every journal
balanced. → `REQ-GL-0014`, `BR-GL-0001`

> This is the criterion most worth writing, because it is the one that catches a scope predicate applied to
> one side of the ledger and not the other. A trial balance that does not balance is the symptom of a filter
> bug, and it is cheap to assert.

---

## Cross-cutting

**`AC-GL-0017`** — A caller holding `Platform.Tenant.Administer` and no GL permission can neither read nor
post anything in GL. → `ADR-025` decision 8

**`AC-GL-0018`** — A permission name that no catalog contributor defines authorizes nothing, and this is
asserted rather than assumed. → FP-006P precedent, `DEC-GL-0003`

**`AC-GL-0019`** — Every persisted GL string column is `nvarchar` and round-trips Arabic text unchanged.
→ `DEC-GL-0006`, `Constraints.md`

**`AC-GL-0020`** — No foreign key exists between any GL table and any table in the Platform database.
→ `DEC-GL-0006`

**`AC-GL-0021`** — Every GL entity implementing `ITenantOwnedEntity` appears in the E3 cutover manifest, and
the site inventory in `DEC-POS-0022` is updated in the same change that adds it. → `DEC-GL-0010`

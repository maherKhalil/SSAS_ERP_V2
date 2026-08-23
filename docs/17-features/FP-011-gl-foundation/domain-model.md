---
package: FP-011
title: General Ledger — Domain Model
status: DRAFT — shapes conditional on OD-GL-0002, OD-GL-0003, OD-GL-0005, OD-GL-0007
version: 0.1
date: 2026-08-23
---

# FP-011 — Domain Model

> Four aggregates, and **not one of them has a settled shape**, because `OD-GL-0003` (chart ownership),
> `OD-GL-0005` (branch dimension) and `OD-GL-0007` (drafts) each change interfaces rather than fields.
>
> What *is* settled is which platform interfaces exist and what implementing each one commits GL to. That is
> the useful content here: the interfaces are not decoration, they change what the write boundary does.

## What implementing each interface actually costs

This table is the reason the aggregates below are drawn conditionally. In this codebase an interface on an
entity is an instruction to `TenantDbContext`, not a marker.

| Interface | What the write boundary does | Where |
|---|---|---|
| `ITenantOwnedEntity` | Stamps the trusted tenant on insert, refuses a mismatched one, and enters the row into the **E3 cutover manifest by construction** | `TenantDbContext`, `TenantCutoverCopyPlan.Build` |
| `ICompanyOwnedEntity` | Makes the save a **company-scoped write**: `AuthorizeCurrentCompanyAsync` runs before anything touches the database | `TenantDbContext.ApplyCompanyRulesAsync` |
| `IAppendOnlyEntity` | Refuses any `Modified` or `Deleted` entry for the type, **whatever path tracked it** | `TenantDbContext.PreventAppendOnlyMutation` |
| `IAuditableEntity` | Created/modified stamping | `TenantDbContext` |

The ordering is fixed and deliberate — tenant, then company, then branch, then persistence — and the reason is
recorded in `TenantDbContext` itself: a save refused on company grounds *"must never have had its branch
authorized first, or a log would record branch admission for a write that was never permitted to exist."*

---

# `JournalEntry` — the aggregate root

The document. Owns its lines; lines have no independent life.

```
JournalEntry
  Id                  ADR-013 identifier strategy
  TenantId            ITenantOwnedEntity
  CompanyId           ICompanyOwnedEntity
  JournalNumber       nvarchar — unique within fiscal year (BR-GL-0005, scope per OD-GL-0004)
  EntryDate           the accounting date; determines the fiscal period
  FiscalPeriodId      resolved at post time, not supplied by the caller
  Description         nvarchar
  Reference           nvarchar, optional
  ReversesJournalId   OD-GL-0006 — present only under option 1
  Lines               >= 2, balanced (BR-GL-0001)
```

**Interfaces:** `Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity` — and
`IAppendOnlyEntity` **if and only if** `OD-GL-0007` resolves to option 1 or 3.

**The `OD-GL-0007` consequence, stated concretely.** `IAppendOnlyEntity` is a property of the *type*. Under
option 2 (one aggregate carrying a Draft/Posted status) the entity must be `Modified` to move from draft to
posted, so the interface cannot be applied at all — and `BR-GL-0002` degrades from a structural guarantee to
an aggregate-level check that the write boundary does not enforce. That is not a small difference: the whole
argument for the interface is that *"there is no repository method for it" protects only the callers who go
through the repository.*

Under option 3 (`JournalDraft` mutable, `JournalEntry` append-only) the guarantee survives intact and the cost
is one extra type plus a promotion step. This package does not choose; it records that option 2 is the only
one of the three that gives something up, so that it is given up knowingly.

**`EntryDate` is not `CreatedUtc`.** The accounting date decides the fiscal period and therefore whether
`BR-GL-0003` refuses the posting. They are different facts and both are needed.

---

# `JournalLine` — owned by the entry

```
JournalLine
  Id
  JournalEntryId      owner; no independent lifecycle
  AccountId           the posting target (BR-GL-0004 checked at post time)
  Debit               decimal(19,4)  DEC-GL-0001
  Credit              decimal(19,4)  DEC-GL-0001
  Description         nvarchar, optional
  BranchId            OD-GL-0005 option 3 only
  LineNumber          stable ordering within the entry
```

**Debit and credit as two columns, or one signed amount?** Two columns is the accounting-native shape and
makes `BR-GL-0001` a direct sum comparison; one signed column halves the storage and makes the balance rule
`SUM(Amount) = 0`. This package does not decide, because the choice is invisible outside the module and is
properly an implementation decision for whoever writes the schema — but it is flagged here so it is made once,
deliberately, rather than differently in two places.

**Under `OD-GL-0002` option 2** each line gains a transaction currency, a transaction amount, a rate and a
base amount — and `ADR-027` decision 3 then requires the `Money` type and the `BaseCurrencyCode` promotion **in
the same change**, per that ADR's first deferred obligation.

---

# `Account` — the chart of accounts

```
Account
  Id
  TenantId            always
  CompanyId           ONLY under OD-GL-0003 option 2
  Code                nvarchar, unique within its owning scope
  Name                nvarchar
  IsActive            BR-GL-0004
  ParentAccountId     only if the chart is hierarchical — see below
  RowVersion          DEC-GL-0007: mutable, so it carries one
```

**`OD-GL-0003` is an interface decision, not a column decision.** Adding `CompanyId` also adds
`ICompanyOwnedEntity`, which makes **every account write a company-scoped write** running
`AuthorizeCurrentCompanyAsync`. A reader who sees only the column will not see the authorization change; that
is why the decision is raised as an owner decision rather than treated as schema detail.

**Hierarchy is not assumed.** Nothing in `BR-GL-*` or the Glossary says the chart is a tree. If it becomes
one, `ADR-026`'s department-hierarchy work is the precedent to read first — `DEC-DEP-0005` settled how
hierarchy is represented, and `ADR-027` decision 4 cites it as one of the cases where this codebase refused a
second source of truth. GL should reuse that reasoning rather than rediscover it.

**Deactivation, not deletion.** `BR-GL-0004` says inactive accounts cannot *receive transactions* — history
stays. This matches Company, where `PreventCompanyDeletion` enforces archive-over-delete so *"history stays
reconstructable."*

---

# `FiscalPeriod` and `FiscalYear`

```
FiscalYear                       FiscalPeriod
  Id                               Id
  TenantId                         FiscalYearId
  CompanyId    OD-GL-0004          StartDate / EndDate   contiguous, non-overlapping
  Code / Name                      Status                Open | Closed  (BR-GL-0003)
  StartDate / EndDate              RowVersion            mutable
  RowVersion
```

**`OD-GL-0004` decides ownership**, and with it whether closing a period is a company-scoped write and what
`BR-GL-0005`'s uniqueness constraint is keyed on.

**Reopening is unspecified** by every existing rule. Recorded, not invented.

---

# Aggregate boundaries and what crosses them

| From | To | Mechanism |
|---|---|---|
| `JournalEntry` | `JournalLine` | Owned. One aggregate, one transaction |
| `JournalLine` | `Account` | **Identifier only.** Separate aggregates; the active check reads the account at post time |
| `JournalEntry` | `FiscalPeriod` | **Identifier only**, resolved from `EntryDate` at post time |
| `JournalEntry` | `Company` | **Identifier only, and never a cross-database FK.** Company lives in the tenant database, but the standing rule is that no FK crosses the Platform/Tenant boundary and GL carries platform identifiers as validated values |

**Nothing in GL references an HR type.** `ADR-012` makes that compiler-enforced rather than a rule anyone can
forget, and `OD-GL-0009` is where any future coupling would have to be decided — via promotion or an event,
never an assembly reference.

---

# Invariants, and where each is enforced

| Invariant | Rule | Enforced |
|---|---|---|
| Debits equal credits | `BR-GL-0001` | Aggregate, at post time (`DEC-GL-0008`) |
| At least two lines | implied by `BR-GL-0001` | Aggregate |
| Posted journals immutable | `BR-GL-0002` | **Write boundary** via `IAppendOnlyEntity` — conditional on `OD-GL-0007` |
| No posting into a closed period | `BR-GL-0003` | Application, at post time, against live period state |
| No posting to an inactive account | `BR-GL-0004` | Application, at post time, against live account state (`DEC-GL-0009`) |
| Journal number unique in year | `BR-GL-0005` | Database unique index; scope per `OD-GL-0004` |
| Tenant isolation | `ADR-005` | Write boundary, stamped and re-verified |
| Company authorization | `ADR-025` | Write boundary, before persistence |

**The pattern worth noticing:** the two invariants that are enforced *structurally* — tenant isolation and
append-only — are the two that no feature author can forget, because they are not the feature author's code.
Everything checked "at post time" is a rule some future path could bypass, and every one of those needs a test
that goes through the real boundary rather than a handler.

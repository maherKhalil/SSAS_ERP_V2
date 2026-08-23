---
package: FP-011
title: General Ledger — Owner Decisions and Proposed Engineering Decisions
status: DRAFT — nothing here is ratified
version: 0.1
date: 2026-08-23
---

# FP-011 — Decisions

> **Nothing in this file is approved.** `OD-GL-*` are questions for the owner. `DEC-GL-*` are engineering
> decisions this package *proposes*; each is drafted so it can be ratified, amended or rejected as a unit.
>
> The file is named `decisions-open.md` rather than the usual `decisions-approved.md` deliberately: in a
> draft package nothing is approved, and FP-010 established the name for exactly this state.

---

# Part 1 — What General Ledger inherits

GL is the first module designed after the platform's foundations settled. The inheritance below is recorded
so that it is **adopted by decision rather than by observation** — which is the specific failure `ADR-027`
names. Where a source states the rule in its own words, it is quoted rather than paraphrased.

## 1.1 Money — `ADR-027`

> ## Decision 1 — A persisted monetary amount is `decimal(19,4)`
>
> Every column storing a monetary amount, in every module, uses SQL Server `decimal(19,4)`, mapped to .NET
> `decimal`.

`ADR-027`'s status section adds, in the same words: *"`decimal(19,4)` is now the product's money
representation, and General Ledger inherits it."*

**And it names this module in its deferred obligations:**

> **General Ledger** must either adopt decision 1 or amend this ADR. Matching HR by observation, without a
> recorded decision, is the outcome this ADR exists to prevent.

`DEC-GL-0001` discharges that obligation.

## 1.2 Currency — `ADR-027` decisions 2, 3, 4, 5

Decision 2 says an amount beneath a Company carries **no currency column**, because `DEC-CMP-0009` makes a
Company's `BaseCurrencyCode` required at creation and immutable, so every row beneath one Company already has
exactly one unambiguous currency. The currency is *projected on read, never accepted on write*, via
`ITenantCompanyCurrencyLookup` (`SSAS.BuildingBlocks.Tenancy/Companies/ITenantCompanyCurrencyLookup.cs`).

**Decision 3 names the conditions under which decision 2 stops being correct, and GL is where they arrive:**

> 1. A Company needs amounts in more than one currency — foreign-currency transactions, multi-currency price
>    lists, or grade ladders benchmarked in a second currency.
> 2. An amount is stored above the Company level, where no single base currency applies.
> 3. Exchange rates enter the product, at which point an amount's currency and its rate date become
>    inseparable from the amount.

Condition 1 is a foreign-currency journal. Condition 2 is a tenant-level chart of accounts. Condition 3 is
any revaluation. **All three are GL questions**, which is why `OD-GL-0002` and `OD-GL-0003` are raised before
any schema. Decision 4 (promotion, never duplication) and decision 5 (no `Money` type yet) both hinge on the
answer, and `ADR-027`'s first deferred obligation requires them to move together:

> **The first module that needs multi-currency amounts** must close decision 3: it decides the currency
> carrier, promotes the value object per decision 4, and introduces `Money` per decision 5 — as one
> deliberate change, not three incidental ones.

## 1.3 Append-only — the mechanism `BR-GL-0002` already has

`BR-GL-0002` is *"Posted Journals cannot be edited."* The enforcement exists, and it is structural rather
than conventional. `SSAS.BuildingBlocks.Domain/ICompanyOwnedEntity.cs`:

> IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, and it is enforced centrally rather than by convention.
> "There is no repository method for it" protects only the callers who go through the repository; the write
> boundary refuses a Modified or Deleted entry for any type marked here, whatever path tracked it.

and, on why the classification exists at all:

> Some records exist to say what happened, and a record of what happened that can be edited afterwards is
> not one. Employee branch history is the first: a correction is another transfer, never a rewrite.

**For GL that last sentence reads: a correction is another journal, never a rewrite** — which is a reversal,
and which is why `BR-GL-0002` needs no new machinery. `TenantDbContext.PreventAppendOnlyMutation` refuses any
`Modified` or `Deleted` entry for an `IAppendOnlyEntity` before the save reaches SQL.

Posted journals are the largest population this pattern has been asked to carry: HR's clients are assignment
and run-history rows, which are incidental to their aggregates. In GL the append-only record **is** the
aggregate, and it is the module's highest-volume table. `OD-GL-0007` is the part that is genuinely open —
whether a *draft* journal is the same aggregate, because an aggregate that is edited while draft cannot be
`IAppendOnlyEntity` from creation.

## 1.4 Authorization — `ADR-023`, `ADR-025`, and the unforgeable scope

Three independent dimensions: **tenant**, **company**, **branch**. Company and branch are **siblings, never
nested**. Functional permission and reachable scope are separate axes — `HrPermissionNames` states it:

> Holding one says which OPERATION is permitted. It says nothing about which companies or branches are
> reachable [...] Conversely `Platform.Tenant.Administer` widens those scopes and grants NONE of these: an
> administrator without ViewEmployees cannot read an employee (ADR-025 decision 8).

Reads are gated by an **unforgeable scope value**. `EmployeeReadScope` is the pattern:

> HOLDING ONE OF THESE IS PROOF THAT ALL THREE DIMENSIONS WERE CHECKED, LIVE, JUST NOW.
> [...] a read that omitted a scope predicate is not something a reviewer has to notice, because it is not
> something a caller can express.

An empty authorized set **refuses** the read rather than meaning "everything" — enforced at construction, so
`WHERE BranchId IN ()` is unrepresentable. `DEC-GL-0004` proposes `JournalReadScope` on the same terms.

Permission names follow `<Plane>.<Resource>.<Action>` — exactly three ASCII-identifier segments. Naming a
permission is **not** registering it: a catalog contributor must define it or the name authorizes nothing.

## 1.5 The remaining inherited constraints

| Constraint | Source | Consequence for GL |
|---|---|---|
| Identifiers follow the primary-key strategy | `ADR-013` | Journal, line and account identifier types are not GL's to choose freely |
| Modules never reference each other's assemblies | `ADR-012` | GL cannot reach into HR or Platform for a type; a shared type is **promoted**, per `ADR-027` decision 4 |
| **Every persisted application string is `nvarchar`** | standing constraint | Account codes, journal numbers, descriptions, references — no `varchar` anywhere |
| **No cross-database FK between Platform DB and Tenant DB** | standing constraint | A GL row may carry a Platform identifier as a value; it may never carry a foreign key to one |
| `RowVersion` for optimistic concurrency | standing convention | Applies to *mutable* aggregates. A posted journal is append-only, so it has nothing to concurrently update — see `DEC-GL-0007` |
| The E3 cutover manifest is built **by construction** | `DEC-POS-0022`, `TenantCutoverCopyPlan.Build` | Every new `ITenantOwnedEntity` enters the manifest automatically and the **ten-site** inventory must be updated with it — `DEC-GL-0010` |

---

# Part 2 — Owner decisions

Nine. Each states the question, why it cannot be answered from existing authority, the options with their
consequences, and what is blocked until it is answered.

## `OD-GL-0001` — Who authors the GL requirement catalog, and does this package's draft count?

**The gap.** `Requirement-Catalog/` has no `GL.md`, while `README.md` declares the `REQ-GL` domain and
`Requirement-Numbering.md` reserves the identifier space. Every previous package read its requirements; this
one cannot.

| | Option | Consequence |
|---|---|---|
| 1 | **Owner authors `GL.md`; FP-011's drafts are input only** | Slowest, and correct by the catalog's own authority. The catalog stays a product artifact rather than a feature-package by-product |
| 2 | Ratify FP-011's proposed lines into `GL.md` as drafted | Fast, and makes a feature package the author of product requirements — the precedent is the risk, not the content |
| 3 | Ratify a subset; owner writes the rest | Likely the practical answer, and it needs the subset named explicitly |
| 4 | Proceed with no catalog entry; trace GL to `BR-GL-*` only | **Not recommended.** `Traceability-Matrix.md` is REQ-anchored; GL would be the one module outside it |

**Blocks:** [traceability-matrix.md](traceability-matrix.md) cannot be completed — every REQ column is a
proposal until this is answered.

**Note.** The existing `HR.md` is a flat list of identifiers and titles, with none of the Priority, Category,
Business Rule Reference, Dependencies or Acceptance Criteria fields that `Requirement-Catalog/README.md`'s own
"Requirement Template" mandates. Whichever option is chosen, GL should be told which shape to match — the
template, or the precedent. They are not the same, and the difference is not GL's to settle.

## `OD-GL-0002` — Does V1 General Ledger support more than one currency?

**Why it cannot be inferred.** `ADR-027` decision 3 names foreign-currency transactions and exchange rates as
the exact triggers that end decision 2. GL is where they would arrive. Nothing in `BR-GL-0001`–`0005`, the
Glossary or the Roadmap says whether they do.

| | Option | Consequence |
|---|---|---|
| 1 | **Single currency: every journal is in its Company's base currency** | `ADR-027` decisions 2 and 5 stand unchanged. No currency column, no `Money`, no promotion. Amounts project the Company's code on read |
| 2 | Multi-currency with transaction + base amounts | Triggers `ADR-027` decision 3 in full: currency carrier decided, `BaseCurrencyCode` promoted to `SSAS.BuildingBlocks.Domain` per decision 4, `Money` introduced per decision 5 — **as one change, not three** |
| 3 | Single currency in V1, multi-currency designed for | The trap. A schema shaped for a currency it does not carry is a redundant field, and `ADR-027` decision 5 rejects exactly this: *"a redundant field is a field somebody will eventually set to something else"* |

**Blocks:** every monetary column in the module, and whether `ADR-027` needs amending at all.

## `OD-GL-0003` — Is the Chart of Accounts owned by the tenant or by the company?

**Why it matters more than it looks.** This is `ADR-027` decision 3 condition 2 — *"an amount is stored above
the Company level, where no single base currency applies"* — and it decides which platform interface the
Account aggregate implements, which in turn decides whether writing an account is a company-scoped write.

| | Option | Consequence |
|---|---|---|
| 1 | **Tenant-level chart, shared by all companies** | `Account : ITenantOwnedEntity` only. One chart to maintain; comparable reporting across companies. **But** balances above company level re-open `ADR-027` decision 3 condition 2 |
| 2 | Company-level chart | `Account : ITenantOwnedEntity, ICompanyOwnedEntity` — and per `ADR-025` that makes every account write a **company-scoped write** requiring `AuthorizeCurrentCompanyAsync`. Each company's chart may diverge |
| 3 | Tenant-level chart, company-level activation | Both mechanisms, and the most machinery. Defensible only if companies genuinely need different subsets |

**Blocks:** the Account aggregate, the authorization model for account maintenance, and the E3 manifest entry.

## `OD-GL-0004` — Who owns the fiscal calendar, and what is a Journal Number unique within?

`BR-GL-0003` prohibits posting into a closed Fiscal Period. `BR-GL-0005` requires Journal Numbers unique
within Fiscal Year. Neither says whose fiscal year.

| | Option | Consequence |
|---|---|---|
| 1 | Tenant-level calendar | One close per tenant; companies cannot close independently |
| 2 | **Company-level calendar** | Each company closes its own periods — usually what a legal entity requires. Makes the uniqueness scope *(CompanyId, FiscalYear, JournalNumber)* |
| 3 | Tenant calendar, company-level close state | Shared period definitions, independent close. More machinery, and probably what a real group needs |

**Blocks:** the uniqueness constraint on Journal Number, the period-state model in
[lifecycle-model.md](lifecycle-model.md), and whether closing is a company-scoped write.

## `OD-GL-0005` — Does a journal line carry a Branch dimension?

Company and branch are **siblings, never nested** (`ADR-023`). HR data is branch-aware; whether financial
postings are is a product question, not an inference.

| | Option | Consequence |
|---|---|---|
| 1 | **No branch dimension in V1** | Simplest. `JournalEntry` is company-owned only, and the read scope resolves two dimensions rather than three |
| 2 | Branch on the journal header | Whole-document attribution; cannot split one document across branches |
| 3 | Branch on the line | Full analytical dimension, and every GL read becomes three-dimensional like `EmployeeReadScope` |

**Blocks:** `JournalReadScope`'s shape (`DEC-GL-0004`) and the branch-authorization path on write.

## `OD-GL-0006` — What is the correction mechanism, and does a reversal link back?

`BR-GL-0002` forbids editing a posted journal. It does not say what replaces editing.

| | Option | Consequence |
|---|---|---|
| 1 | **Reversal journal carrying `ReversesJournalId`** | The correction is discoverable from either side; matches `IAppendOnlyEntity`'s own model where *a correction is another transfer* |
| 2 | Unlinked contra journal | Nothing to maintain, and no way to answer "was this reversed?" without inference |
| 3 | Reversal plus a re-posted correction, as one operation | Convenient for users; needs its own atomicity rule |

**Blocks:** the `JournalEntry` aggregate's fields and the lifecycle model.

## `OD-GL-0007` — Is a draft journal the same aggregate as a posted one?

**The sharpest engineering consequence in this package.** `IAppendOnlyEntity` refuses `Modified` for the type,
not for a state. A journal that is created as a draft, edited, then posted **cannot** be `IAppendOnlyEntity`
from creation — the write boundary would refuse the second edit.

| | Option | Consequence |
|---|---|---|
| 1 | **No drafts in V1: a journal is created posted and balanced** | `JournalEntry : IAppendOnlyEntity` cleanly, and `BR-GL-0001`/`BR-GL-0002` are enforced structurally. Least machinery, strictest UX |
| 2 | Draft and Posted are one aggregate with a status | `IAppendOnlyEntity` **cannot** be used; `BR-GL-0002` falls back to an aggregate-level guard, which is a convention the write boundary does not enforce |
| 3 | Two aggregates — `JournalDraft` (mutable) and `JournalEntry` (append-only) | Keeps the structural guarantee *and* allows drafts, at the cost of two types and a promotion step between them |

**Blocks:** whether GL is `IAppendOnlyEntity`'s biggest client or not one at all.

## `OD-GL-0008` — Is fiscal-year close in V1 scope, and does it produce opening balances?

The Glossary defines Fiscal Year and Fiscal Period and lists *Close Fiscal Year* as an operation.
`BR-GL-0003` only says closed periods prohibit posting.

| | Option | Consequence |
|---|---|---|
| 1 | Period close only; no year-end close | `BR-GL-0003` is fully satisfied. No opening-balance machinery |
| 2 | **Year-end close producing carry-forward opening balances** | Needs a retained-earnings account, a close-generated journal, and a rule for re-opening |
| 3 | Year-end close, no generated journal | Balances derived on read. Cheaper to write, and every report pays for it forever |

**Blocks:** whether an Account needs a type/classification taxonomy at all in V1.

## `OD-GL-0009` — Does anything post to GL in V1, or is it entry-only?

The Roadmap places GL in V1 alongside HR; Payroll is V2. Whether any module *feeds* GL in V1 is unstated.

| | Option | Consequence |
|---|---|---|
| 1 | **Manual journal entry only** | GL depends on no module. `ADR-012` untouched, no integration contract needed |
| 2 | HR posts to GL in V1 | Needs a cross-module contract that does **not** violate `ADR-012` — a promoted contract or an event, never an assembly reference |
| 3 | An import path, as HR has | Reuses `StrictCsvReader` and the FP-009 transport pattern; a real V1 scope increase |

**Blocks:** the module's dependency surface, and whether FP-011 is one package or two.

---

# Part 3 — Proposed engineering decisions

Ten. Each is drafted for ratification. Where one depends on an owner decision, it says so rather than
assuming an answer.

**`DEC-GL-0001` — GL adopts `ADR-027` decision 1: every persisted monetary amount is `decimal(19,4)`.**
This is the one position taken without an owner decision, because `ADR-027` explicitly requires a *recorded*
adoption and names silent matching as the failure. Independent of `OD-GL-0002`: the *precision* is settled
whether or not a currency column joins it.

**`DEC-GL-0002` — Posted journals and journal lines implement `IAppendOnlyEntity`.** Conditional on
`OD-GL-0007` option 1 or 3. Under option 2 this decision is withdrawn and `BR-GL-0002` loses its structural
enforcement — which should be stated plainly when that option is weighed.

**`DEC-GL-0003` — Permission names are `GL.<Resource>.<Action>`**, three ASCII-identifier segments, defined by
a `GlPermissionCatalogContributor` registered by the Host. Naming without registering authorizes nothing;
`HrPermissionNames` records that failure and GL will not repeat it.

**`DEC-GL-0004` — Every GL read requires an unforgeable `JournalReadScope`**, constructed only by a resolver
that has checked the functional permission and resolved the authorized company set (and branch set, if
`OD-GL-0005` adds one) against live state. Empty sets refuse the read; there is no scope meaning "everything".

**`DEC-GL-0005` — Identifiers follow `ADR-013`**, not a GL-local convention.

**`DEC-GL-0006` — Every persisted string column is `nvarchar`, and no foreign key crosses the Platform/Tenant
database boundary.** Platform identifiers are carried as values, validated on write, never as FKs.

**`DEC-GL-0007` — `RowVersion` on mutable aggregates only.** Accounts and fiscal periods carry it; an
append-only posted journal does not, because there is no concurrent update for it to detect. Adding one there
would imply a mutation the write boundary forbids.

**`DEC-GL-0008` — `BR-GL-0001` (debits equal credits) is enforced in the aggregate at post time**, not by a
database constraint. A CHECK constraint cannot see a set of lines, and a trigger would put a business rule
somewhere the domain cannot test.

**`DEC-GL-0009` — `BR-GL-0004` (inactive accounts reject transactions) is checked at post time against the
account's current state**, and the refusal names the account. Not a FK-level construct.

**`DEC-GL-0010` — Every new `ITenantOwnedEntity` extends the E3 cutover manifest by construction, and the
ten-site inventory in `DEC-POS-0022` must be updated in the same change.** The manifest is derived by
`TenantCutoverCopyPlan.Build` reflecting over the interface, so entry is automatic — but the *tests* that
assert the inventory are not, and FP-009 established that **an inventory with a footnote saying it is
incomplete is not an inventory**.

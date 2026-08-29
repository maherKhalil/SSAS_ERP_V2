---
package: FP-011
title: General Ledger — API Contracts
status: APPROVED — the decisions the sketch waited on are ruled; exact wire shapes are the build's
version: 1.0
date: 2026-08-23
---

# FP-011 — API Contracts

> **DECISIONS CLOSED, 2026-08-23.** All nine owner decisions are ruled; conditional wording below is kept as
> the record of what was weighed, with the ruling stated where it changes the answer.
>
> | | | | |
> |---|---|---|---|
> | `0001` catalog: ratified into `GL.md` | `0002` **single currency** | `0003` **tenant-level chart** | `0004` **company calendar** |
> | `0005` **no branch dimension** | `0006` **reversal + `ReversesJournalId`** | `0007` **two aggregates** | `0008` **period close only** |
> | `0009` **manual entry only** | | | |

> **Sketch, not contract.** Request and response shapes follow `OD-GL-0002` (does an amount carry a currency?)
> and `OD-GL-0005` (does a line carry a branch?), so fixing them now would fix the wrong thing. What is fixed
> is the **transport discipline**, which GL inherits whole and does not get to reinterpret.

## The transport rules GL inherits

These came out of FP-006 through FP-009 at real cost, and they are not GL's to relax.

| Rule | Consequence |
|---|---|
| **Strict reading — unknown properties are refused, not ignored** | A request carrying a property the contract does not define is a `400`, not a silently dropped field. `StrictRequestReader` opens with `HasJsonContentType()` and that line is its contract |
| **The currency is projected on read, never accepted on write** | `ADR-027` decision 2. A request that supplies a currency is rejected as an unknown property — which is the strict reader doing its job, not a special case |
| **Invalid UTF-8 is refused, never substituted** | `StrictCsvReader.StrictUtf8 = new(false, throwOnInvalidBytes: true)`. A ledger that silently replaces a byte it could not decode has corrupted a record |
| **Request size is bounded at the endpoint** | `WithMaxBodySize`. A journal with a very large number of lines needs a stated limit rather than an accidental one |
| **Errors are named, not numbered** | Every refusal carries a stable error name the client can branch on |
| **Filters are an allowlist** | An unrecognized query parameter is refused. FP-009's `TryEmployeeFilters` is the shape |

## Proposed route surface

`/api/gl/...`, consistent with `/api/hr/...`.

> **The five `journal-drafts` rows below were added in T-098, and four of them document routes that were
> already live.** `OD-GL-0007` ruled two aggregates and this document recorded that ruling in prose only —
> *"a draft surface DOES exist"* — while the table stayed at nineteen rows and never gained the family.
> The four write routes shipped from that sentence and went unspecified; the read was never built at all,
> which is how `GL.Drafts.View` came to be catalogued and required by nothing.
>
> **A route table that omits four live routes asserts something false by omission**, so they are documented
> after the fact rather than left out, and the omission is named here rather than buried in the addition.

| Method | Route | Operation | Permission |
|---|---|---|---|
| `POST` | `/api/gl/journal-drafts` | Create a draft | `GL.Drafts.Manage` |
| `PUT` | `/api/gl/journal-drafts/{id}` | Replace a draft's header and lines | `GL.Drafts.Manage` |
| `POST` | `/api/gl/journal-drafts/{id}/discard` | Discard a draft | `GL.Drafts.Manage` |
| `POST` | `/api/gl/journal-drafts/{id}/posting` | Promote a draft into a journal | `GL.Journals.Post` |
| `GET` | `/api/gl/journal-drafts` | Search drafts | `GL.Drafts.View` |
| `GET` | `/api/gl/journal-drafts/{id}` | Read one draft with its lines | `GL.Drafts.View` |
| `POST` | `/api/gl/journals/{id}/reversals` | Post a reversing journal | `GL.Journals.Reverse` |
| `GET` | `/api/gl/journals` | Search journals | `GL.Journals.View` |
| `GET` | `/api/gl/journals/{id}` | Read one journal with its lines | `GL.Journals.View` |
| `POST` | `/api/gl/accounts` | Create an account | `GL.Accounts.Create` |
| `PUT` | `/api/gl/accounts/{id}` | Update an account | `GL.Accounts.Update` |
| `POST` | `/api/gl/accounts/{id}/deactivation` | Deactivate an account | `GL.Accounts.Deactivate` |
| `POST` | `/api/gl/accounts/{id}/activation` | Reactivate an account | `GL.Accounts.Deactivate` |
| `GET` | `/api/gl/accounts` | Search the chart | `GL.Accounts.View` |
| `GET` | `/api/gl/accounts/{id}` | Read one account | `GL.Accounts.View` |
| `POST` | `/api/gl/fiscal-years` | Define a year and its periods | `GL.Periods.Manage` |
| `POST` | `/api/gl/fiscal-periods/{id}/closure` | Close a period | `GL.Periods.Close` |
| `POST` | `/api/gl/fiscal-periods/{id}/reopening` | Reopen a period | `GL.Periods.Close` |
| `GET` | `/api/gl/fiscal-periods` | Read the calendar | `GL.Periods.View` |
| `GET` | `/api/gl/reports/trial-balance` | Trial balance | see `OD-GL` note below |
| `GET` | `/api/gl/accounts/{id}/balance` | Balance enquiry | see note |

> ---- ⚠ CORRECTED 2026-08-28 (T-136). THIS TABLE HAD DISAGREED WITH THE CODE SINCE THE ROUTES SHIPPED.
>
> **Five rows were wrong and three were missing**, and none of it was detectable by anything that runs:
>
> - **`GL.Accounts.Manage` never existed.** Three rows named it. The code has `GL.Accounts.Create`,
>   `GL.Accounts.Update` and `GL.Accounts.Deactivate` — **three grants documented as one that was never
>   declared.**
> - **`POST /api/gl/journals` does not exist.** A journal is posted by posting a DRAFT, at
>   `POST /api/gl/journal-drafts/{id}/posting`, which has its own row. The phantom row is removed.
> - **Closing a period is `GL.Periods.Close`, not `GL.Periods.Manage`.** `Manage` defines a fiscal year;
>   closing carries its own grant.
> - **`GET /accounts/{id}`, `POST /accounts/{id}/activation` and `POST /fiscal-periods/{id}/reopening` were
>   live and absent here** — and the two undo routes made this document say that deactivating an account and
>   closing a period are one-way.
>
> **Reactivation carries `GL.Accounts.Deactivate` and reopening carries `GL.Periods.Close` deliberately:**
> one grant governs a state transition in both directions, which is the same shape as Attendance's
> `ClosePeriods` governing close and reopen. **A separate reactivate grant would let someone undo a decision
> they could not make.**
>
> **`GlRouteInventoryTests` pins all 21 routes and was green throughout** — it compares code to code, so it
> could never have seen any of this. **Nothing mechanical reads prose** (`DEC-L-002`).
>
> **And it is sharper than that.** That file carries a test named
> `Posting_lives_under_the_draft_because_the_journal_does_not_exist_yet`, reasoning that *"placing it
> under `/journals` would suggest a journal exists before it does"*. **The guard was arguing the very
> principle this table violated, in a test name, green, for five days.**
>
> Precisely: that test asserts no `/journals/posting` route exists, while this table claimed
> `POST /api/gl/journals`. **Different strings, same ruling** — so it would not have failed on this row
> even had it read the document. **The point is not that a guard nearly caught it. It is that the
> reasoning was written down, twice, and the specification still said otherwise.**

**Why state changes are `POST` to a sub-resource** (`/deactivation`, `/closure`, `/reversals`) rather than
`PATCH` on the parent: each is an event with its own permission and its own refusals, and for the reversal it
is *literally* the creation of a new resource. It also keeps a `PATCH` off an append-only aggregate, where it
would suggest a mutation the write boundary refuses.

**The reporting routes' permission is `OD`-dependent** — see [authorization-model.md](authorization-model.md)
on whether `GL.Reports.View` should exist at all, given FP-009's `DEC-DOC-0015` declined the analogous
additive permission for export.

## Shapes that depend on an owner decision

```
POST /api/gl/journals
{
  "entryDate": "2026-05-31",
  "description": "...",
  "reference": "...",
  "lines": [
    { "accountId": "...", "debit": 1000.0000, "credit": 0, "description": "..." }
  ]
}
```

* **`OD-GL-0002` ruled single currency**, so no line carries a currency, amount-in-currency or rate.
* **`OD-GL-0005` declined the branch dimension**, so no line carries a `branchId`.
* **`OD-GL-0007` ruled two aggregates**, so a draft surface DOES exist — a mutable
  `/api/gl/journal-drafts` family plus a posting route that promotes a draft into a `JournalEntry`. The
  journal-posting route above therefore posts *a draft*, not a body of lines. **The family is now in the
  table above; this bullet was its only specification for the whole of FP-011.**

* **The draft READ carries no journal number and no reversal fields.** A number is assigned at posting, and
  reversal is a posted-journal concept — `OD-GL-0007`'s two aggregates have different lifecycles, and a
  response carrying nulls for both would invite a client to render them.

* **`GL.Drafts.View` is required explicitly and nothing implies it.** Neither `GL.Drafts.Manage` nor
  `GL.Journals.Post` grants it: an implied permission makes the explicit one optional and its absence
  unenforceable (`AC-SS-0005`). **That is what makes the separation of duties expressible** — a reviewer can
  be given sight of a draft without the ability to edit it, and a preparer without the ability to post.

**The response never echoes a currency the request supplied**, because the request cannot supply one. It
echoes the owning Company's `BaseCurrencyCode`, read through `ITenantCompanyCurrencyLookup` — `ADR-027`
decision 2, unchanged.

## Refusals worth naming now

| Condition | Error | Rule |
|---|---|---|
| Debits do not equal credits | `Gl.JournalUnbalanced` | `BR-GL-0001` |
| Fewer than two lines | `Gl.JournalInsufficientLines` | implied by `BR-GL-0001` |
| Target period is closed | `Gl.FiscalPeriodClosed` | `BR-GL-0003` |
| No period covers the entry date | `Gl.FiscalPeriodNotFound` | `REQ-GL-0009` |
| Account is inactive | `Gl.AccountInactive` | `BR-GL-0004` |
| Account not in the caller's scope | `Gl.AccountNotFound` | `BR-RPT-0002` |
| Attempt to modify a posted journal | `Gl.JournalImmutable` `[STRUCTURAL - no route can produce it]` | `BR-GL-0002` |
| Duplicate journal number in year | `Gl.JournalNumberConflict` | `BR-GL-0005` |

**`Gl.AccountNotFound` rather than `Gl.AccountForbidden` for an out-of-scope account** is deliberate: telling
an unauthorized caller that an account exists but is not theirs leaks the chart of accounts one probe at a
time. This follows the existing scope-refusal posture rather than introducing a new one.

## What is deliberately absent

**No OpenAPI document, no examples, no versioning statement.** They describe a contract that two open owner
decisions can still change. Writing them now would produce a document that has to be rewritten and, in the
interim, would be quoted as though it were decided.

---
package: FP-011
title: General Ledger — Business Rules as They Exist
status: APPROVED — analysis of existing authority; the rulings it fed are recorded (2026-08-23)
version: 1.0
date: 2026-08-23
---

# FP-011 — Business Rules

> **DECISIONS CLOSED, 2026-08-23.** All nine owner decisions are ruled; conditional wording below is kept as
> the record of what was weighed, with the ruling stated where it changes the answer.
>
> | | | | |
> |---|---|---|---|
> | `0001` catalog: ratified into `GL.md` | `0002` **single currency** | `0003` **tenant-level chart** | `0004` **company calendar** |
> | `0005` **no branch dimension** | `0006` **reversal + `ReversesJournalId`** | `0007` **two aggregates** | `0008` **period close only** |
> | `0009` **manual entry only** | | | |

> **This document proposes no new business rules.** `Business-Rules.md` is product authority and adding to it
> is not a feature package's act. What follows is the five existing `BR-GL-*` rules, read closely for what
> each one **settles** and what it **leaves open** — because the gap between those two is where the owner
> decisions came from.

The rules are terse. That is not a criticism: five sentences that are each unambiguous are worth more than
five pages that are not. But terse rules settle less than they appear to, and the reading below is the
evidence for every `OD-GL-*` raised in [decisions-approved.md](decisions-approved.md).

---

## `BR-GL-0001` — Every Journal Entry must balance. Debit Total = Credit Total.

**Settles:** the invariant, unconditionally. There is no tolerance, no rounding allowance, and no exception
for any journal type. `DEC-GL-0008` proposes enforcing it in the aggregate at post time.

**Leaves open:** *balanced in what?* In a single-currency ledger the question does not arise. In a
multi-currency one there are two distinct rules — balanced in the transaction currency, and balanced in the
base currency after conversion — and they are not the same rule. **`OD-GL-0002` ruled single currency for
V1, so this rule is ONE obligation.**

**Why it is not a database constraint.** A CHECK constraint cannot see a set of sibling rows, and a trigger
would place a business rule where the domain layer cannot test it. The rule belongs to the aggregate that owns
the lines.

---

## `BR-GL-0002` — Posted Journals cannot be edited.

**Settles:** immutability after posting. And uniquely among the five, **the product already has the
mechanism**: `IAppendOnlyEntity`, enforced at the write boundary by
`TenantDbContext.PreventAppendOnlyMutation`, which refuses a `Modified` or `Deleted` entry for the type by
whatever path tracked it. The interface's own comment states the reasoning in terms that transfer directly:

> Some records exist to say what happened, and a record of what happened that can be edited afterwards is not
> one. Employee branch history is the first: a correction is another transfer, never a rewrite.

For GL: **a correction is another journal, never a rewrite.**

**Leaves open two things, one small and one structural:**

* It forbids editing but does not name the alternative. Reversal is the accounting answer and this package
  assumes nothing beyond raising it — `OD-GL-0006`.
* It says *posted* journals. If an unposted draft exists and is editable, then the aggregate is mutable for
  part of its life and **cannot carry `IAppendOnlyEntity` from creation**, because the interface is a property
  of the type and not of a state. That is `OD-GL-0007`, **ruled: two aggregates.** `JournalDraft` carries the
  mutable life, `JournalEntry` is append-only from creation, and `BR-GL-0002` is therefore enforced
  **structurally** by the write boundary rather than by convention.

---

## `BR-GL-0003` — Closed Fiscal Periods prohibit posting.

**Settles:** that a period has a closed state and that closure blocks posting.

**Leaves open:** whose period. The Glossary defines a Fiscal Period as *"a configurable accounting period used
by the General Ledger"* without saying whether the calendar belongs to the tenant or to each company. In a
group with several legal entities the difference is operationally large — one close for everyone, or each
company closing its own books. **`OD-GL-0004` ruled the calendar COMPANY-level**, so each company closes its
own books and closing is a company-scoped write.

It is also silent on **reopening**. A rule that says closed periods prohibit posting does not say whether a
period may be reopened, by whom, or whether reopening is itself an audited event. That silence is recorded
rather than resolved.

---

## `BR-GL-0004` — Accounts marked as inactive cannot receive transactions.

**Settles:** that accounts have an active/inactive state and that inactive ones reject new postings. Note the
precise wording — *cannot receive transactions*, not *cannot be used*. History against an inactive account
remains valid and readable, which is what makes deactivation different from deletion.

**Leaves open:** nothing material for V1. `DEC-GL-0009` proposes checking it at post time against the
account's current state, with the refusal naming the account.

**One inherited consequence worth stating:** this is a *lifecycle* rule of exactly the kind the product has
handled before — Company uses Archive rather than physical delete, and `TenantDbContext.PreventCompanyDeletion`
enforces it so *"history stays reconstructable"*. Accounts should follow that precedent rather than invent a
second pattern.

---

## `BR-GL-0005` — Journal Numbers are unique within Fiscal Year.

**Settles:** the uniqueness scope is the fiscal year rather than all time.

**Leaves open, and this is the one most likely to be missed:**

1. **Unique within *whose* fiscal year** — the same `OD-GL-0004` question. **Ruled:
   *(CompanyId, FiscalYear, JournalNumber)*.**
2. **Unique is not gapless.** Accounting and audit practice frequently require journal numbers to have **no
   gaps**, which is a materially harder obligation than uniqueness: it forbids the natural implementation
   where a failed or abandoned attempt consumes a number. `BR-GL-0005` does not ask for gapless, and this
   package does not assume it — but a ledger that ships unique-but-gapped and is later required to be gapless
   cannot be retrofitted for history that already exists. It is raised in `REQ-GL-0011` and belongs with
   `OD-GL-0004`.

---

# Rules from other domains that bind GL

| Rule | Text | Consequence |
|---|---|---|
| `BR-RPT-0001` | Reports shall only display data authorized for the current user | Every GL read is scope-gated; `DEC-GL-0004` makes that structural rather than remembered |
| `BR-RPT-0002` | Reports shall always respect Tenant and Company boundaries | The company dimension is not optional in any GL read; `OD-GL-0005` declined the branch dimension, so the scope is exactly tenant + company |

`Business-Rules.md` also lists **Budgeting** among *Future Modules*, which is the clearest available signal
that budget-versus-actual is out of V1 GL scope.

---

# Rule lifecycle

`Business-Rules.md` defines four statuses — Draft, Approved, Deprecated, Replaced — and records that
deprecated rules stay documented for historical traceability.

**None of the five `BR-GL-*` rules carries a visible status.** Neither do the HR or Platform rules, so this is
a catalog-wide characteristic rather than a GL defect, and it is noted here only because a package that traces
to a rule ought to be able to say whether that rule is Approved. It is not raised as an owner decision because
it is not GL's question to ask.

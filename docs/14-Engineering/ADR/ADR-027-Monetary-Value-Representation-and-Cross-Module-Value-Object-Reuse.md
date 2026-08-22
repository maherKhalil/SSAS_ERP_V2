---
id: ADR-027
title: Monetary Value Representation and Cross-Module Value-Object Reuse
category: Architecture Decision Record
version: 1.0
status: Proposed
date: 2026-08-21
owner: Solution Architecture Team
tags:
  - money
  - currency
  - decimal
  - value-objects
  - module-isolation
  - hr
  - general-ledger
  - architecture
depends_on:
  - ADR-002
  - ADR-003
  - ADR-008
  - ADR-012
  - ADR-014
  - ADR-026
used_by:
  - FP-008
---

# ADR-027: Monetary Value Representation and Cross-Module Value-Object Reuse

---

# Status

**Proposed**

`Proposed` is this repository's ADR status for records whose feature has not yet shipped — `ADR-024`,
`ADR-025` and `ADR-026` all stand at `Proposed` alongside delivered work — so the status says nothing about
whether these decisions are settled. They are.

**The conditional clause is resolved: this ADR is ACTIVATED.** It was drafted conditionally, because whether
FP-008 would persist a monetary amount was `OD-POS-004`, an owner decision in the FP-008 package. That
decision closed on **2026-08-21** in favour of a **money-bearing Salary Grade carrying informational bands**.

- The withdrawal branch — "if `OD-POS-004` selects no money, this ADR should be withdrawn" — **did not
  occur**, and is now moot. The clause is retained above only so a reader can see the decision was
  conditional rather than assume it was always settled.
- `OD-POS-002` retained `SalaryGrade` as a first-class aggregate, so the second withdrawal condition did not
  occur either.
- `DEC-POS-0015` and `DEC-POS-0016` in FP-008 are ratified as drafted and are **the first application** of
  decisions 1, 2 and 4 below — not their author. `decimal(19,4)` is now the product's money representation,
  and General Ledger inherits it.

Decision 4 never depended on `OD-POS-004`: it concerns cross-module value-object reuse, it is already live
under `ADR-012`, and it stands on its own. The re-homing instruction for it is moot with the rest of the
withdrawal branch.

**This ADR should be reviewed and accepted before the FP-008 schema is authored.**

---

# Context

Nothing in this product stores money.

That is a verified statement, not an assumption. Every `decimal` column in the schema is a
`decimal(25,0)` log-sequence number in the tenant backup and restore-verification tables. There is no `Money`
type, no amount column, no rounding policy, and no currency-conversion concept anywhere in
`SSAS.BuildingBlocks`, `SSAS.Platform`, or the HR module.

Currency exists in exactly one place: `Company.BaseCurrencyCode`, a `char(3)` fixed-length column under
`Latin1_General_100_BIN2` with `CK_Companies_BaseCurrencyCode` restricting it to `[A-Z][A-Z][A-Z]`, backed by
`SSAS.Platform.Domain.ValueObjects.BaseCurrencyCode` — a value object holding the static ISO-4217 alphabetic
code set. `DEC-CMP-0009` makes it **required at creation and immutable**.

Two things make the first money column an architecture decision rather than a feature detail.

**It binds every module that follows.** A precision chosen for HR salary bands is the precision General
Ledger will match, because two modules storing amounts at different precisions in one database is a defect
waiting for its first reconciliation. Changing it later means altering every populated money column in every
tenant database.

**The natural way to write it is blocked by module isolation.** `SSAS.HR.Domain` references only
`SSAS.BuildingBlocks.Domain` and `SSAS.BuildingBlocks.SharedKernel`. It cannot see
`SSAS.Platform.Domain.ValueObjects.BaseCurrencyCode`, and the compiler enforces that — the same enforcement
that stopped `DepartmentApiErrorMapper` from reaching for Platform's `Persistence.*` error type during FP-007
Phase 4. So "reuse the currency value object" is not available, and the alternatives — duplicate it, promote
it, or avoid needing it — are architecture choices with different costs.

---

# Decision

## Decision 1 — A persisted monetary amount is `decimal(19,4)`

Every column storing a monetary amount, in every module, uses SQL Server `decimal(19,4)`, mapped to .NET
`decimal`. **Never `float`, `real`, or `money`.**

Four decimal places, because `BaseCurrencyCode`'s own ISO-4217 set contains three-decimal currencies — BHD,
IQD, JOD, KWD, LYD, OMR, TND — and a product configured for one of them must not lose its minor unit. The
fourth place is a guard digit for intermediate values.

Fifteen integer digits, because high-denomination currencies produce large nominal amounts and a scaling
convention that some modules apply and others do not is worse than an oversized column.

`money` is excluded specifically: it is a fixed four-decimal type whose intermediate arithmetic rounds in ways
that surprise, and it carries no advantage over `decimal(19,4)` on modern SQL Server. `float` and `real` are
excluded because binary floating point cannot represent most decimal fractions exactly, and money that does
not add up is not money.

**Implementation status: not implemented.** Activated by the `OD-POS-004` ruling of 2026-08-21; first applied by FP-008.

## Decision 2 — Where a single owning currency is unambiguous, an amount carries no currency column

An amount stored beneath a Company is denominated in that Company's `BaseCurrencyCode` and stores no currency
of its own.

This is available because `DEC-CMP-0009` makes a company's base currency **required at creation and
immutable**. Every row beneath one company therefore already has exactly one unambiguous currency, and a
per-row copy would be a second source of truth for a fact the Company already owns — the class of defect this
codebase has refused repeatedly (`DEC-DEP-0005` on hierarchy representation, `DEC-DEP-0029` on the cutover
manifest).

**The currency is projected on read, never accepted on write.** A representation that shows an amount without
a currency is unreadable, so read models echo the owning Company's code; a request that supplies one is
rejected as an unknown property.

**Implementation status: not implemented.** Activated by the `OD-POS-004` ruling of 2026-08-21; first applied by FP-008.

## Decision 3 — The condition under which decision 2 no longer holds

Decision 2 is correct **only** while every amount beneath a Company is in that Company's base currency. It
must be revisited the moment any of these becomes true:

1. A Company needs amounts in more than one currency — foreign-currency transactions, multi-currency price
   lists, or grade ladders benchmarked in a second currency.
2. An amount is stored above the Company level, where no single base currency applies.
3. Exchange rates enter the product, at which point an amount's currency and its rate date become inseparable
   from the amount.

At that point the amount needs an explicit currency, and decision 4 governs where the currency type comes
from.

**Naming the condition is the point.** `ADR-026` decision 7 named the condition under which
`tenant.DepartmentManagers` should collapse back into a column, so the reason for an unusual shape would not
decay into folklore. The same discipline applies here in the opposite direction: the reason for a *missing*
column is recorded so that adding one later is a decision rather than a discovery.

## Decision 4 — A shared value object is **promoted**, never duplicated, and never reached across

When two modules need the same value object:

| | Approach | Verdict |
|---|---|---|
| (a) | Duplicate the type in the second module | **Prohibited.** Two ISO-4217 lists give the product two answers to "is XYZ a currency", and they will drift. The drift is silent and shows up as data one module accepts and another rejects |
| (b) | Reference the owning module's assembly | **Prohibited by `ADR-012`**, and compiler-enforced. This is not a rule anyone can forget |
| (c) | **Promote the type into `SSAS.BuildingBlocks.Domain`**, leaving one definition | **The sanctioned mechanism** |
| (d) | Avoid needing it | **Preferred where available**, as in decision 2 |

**A promotion is a deliberate, reviewed change to shared foundations — it is not a side effect of a feature
package needing a type.** `ADR-026` decision 7 refused to let an HR feature package modify Platform's cutover
engine to make its own schema fit, on exactly this reasoning. The same applies here: a feature package that
finds it needs a Platform type raises the promotion as its own decision, with its own review, rather than
performing it in passing.

Specifically for currency: `BaseCurrencyCode` stays in `SSAS.Platform.Domain` until a module genuinely needs
it, and is then promoted to `SSAS.BuildingBlocks.Domain` in a change of its own. FP-008 does not need it under
decision 2.

**Implementation status: not implemented.** Decision 4 stands independently of `OD-POS-004`.

## Decision 5 — No `Money` type is introduced yet

No `Money` value object pairing an amount with a currency is introduced by the first money-bearing package.
Amounts are `decimal` properties validated by their owning aggregate.

A `Money` type is the right model **once currency varies**. While decision 2 holds, a `Money` carrying a
currency every instance derives from the same place is a type with a redundant field, and a redundant field is
a field somebody will eventually set to something else. Introducing it early would also require the promotion
that decision 4 says must be taken deliberately, in order to solve a problem the product does not yet have.

Decision 3's conditions are the trigger for `Money` as well as for the currency column: they arrive together.

**Implementation status: not implemented.** Activated by the `OD-POS-004` ruling of 2026-08-21; first applied by FP-008.

---

# Consequences

## Positive

- The product's money representation is settled once, before any amount exists, rather than being set by
  whichever module happens to store the first one.
- General Ledger inherits a decided precision instead of matching HR by observation.
- The currency question is answered by *not needing* an answer, which is the cheapest correct answer
  available while a company has one immutable base currency.
- Cross-module type reuse gains a named mechanism, so the next module that wants a Platform type has
  somewhere to go other than copy-paste.

## Negative

- `decimal(19,4)` is wider than most amounts need, costing 9 bytes per column where 5 would do. The
  alternative is a per-module precision decision, which is how two modules end up disagreeing.
- Decision 2 means an amount is not self-describing: reading `tenant.SalaryGrades` in isolation does not tell
  you the currency. Decision 3 names when that stops being acceptable.
- Deferring `Money` (decision 5) means the eventual introduction touches every amount property written before
  it. That cost is bounded and known; introducing a half-formed type early is neither.

## Risks

| Risk | Mitigation |
|---|---|
| A module stores an amount at a different precision | An architecture guard asserting that every mapped `decimal` property in a tenant model is either `decimal(19,4)` or on a named exception list — the LSN columns being the current exceptions |
| A currency column is added quietly when multi-currency arrives | Decision 3 names the trigger conditions explicitly, and decision 4 names where the type must come from |
| `BaseCurrencyCode` is duplicated into a module under deadline pressure | Decision 4 prohibits it; an architecture guard asserting that no module defines a second ISO-4217 code set makes the prohibition executable |
| The precision proves wrong for a later module | Decision 1 is the place to amend, and decision 3 already establishes the habit of naming revisiting conditions rather than leaving them to be rediscovered |

---

# Alternatives Considered

| Alternative | Rejected because |
|---|---|
| SQL Server `money` | Fixed four-decimal type with surprising intermediate rounding, and no advantage over `decimal(19,4)` |
| `float` / `real` | Binary floating point cannot represent decimal fractions exactly |
| `decimal(18,2)` | Loses the minor unit of every three-decimal currency already in the product's own ISO-4217 set |
| Integer minor units (store cents) | Requires every module to know each currency's exponent, and the three-decimal currencies make the exponent non-uniform. Moves a data problem into every consumer |
| A currency column on every amount | A second source of truth for a fact the Company owns immutably, while decision 2's precondition holds |
| Duplicating `BaseCurrencyCode` into HR | Two ISO-4217 lists that will drift, giving the product two answers to the same question |
| Promoting `BaseCurrencyCode` to BuildingBlocks now | Correct eventually, but it is a change to shared foundations made to satisfy a package that decision 2 shows does not need it |
| Letting the first money-bearing feature package decide | The decision binds General Ledger, and a decision that binds a module outside the package making it is an ADR by definition |

---

# Deferred obligations

**The first module that needs multi-currency amounts** must close decision 3: it decides the currency
carrier, promotes the value object per decision 4, and introduces `Money` per decision 5 — as one deliberate
change, not three incidental ones.

**General Ledger** must either adopt decision 1 or amend this ADR. Matching HR by observation, without a
recorded decision, is the outcome this ADR exists to prevent.

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-21 | Solution Architecture Team | Proposes the product's monetary representation — `decimal(19,4)`, no currency column while a Company's immutable base currency is unambiguous, and named conditions for revisiting — together with the promotion rule for cross-module value objects. Five decisions. Drafted conditional on `OD-POS-004`; **activated** the same day when that decision chose a money-bearing Salary Grade, so the conditional-withdrawal clause is moot. |

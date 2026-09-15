# FP-014 — Domain model

**Written from the ruling set of 2026-08-25**, in which all seventeen `OD-SUB` carry rulings and the
scope is `E + C` (`DEC-L-006`) — enablement and the commercial record together, all 28 `REQ-SUB` in
force.

**One direction of money, stated once and then assumed.** Everywhere below, *invoice*, *price*,
*payment* and *billing* mean **the vendor billing the tenant for its subscription to this product**.
The tenant billing its own customers is General Ledger and a future receivables module, is
tenant-owned data in the Tenant ERP database, and appears nowhere in this file (`DEC-SUB-0001`, and
the boundary table in [`README.md`](README.md)).

---

## The two mechanical constraints that shape everything

### 1. Append-only means no closing column — the interval is derived by ordering

`OD-SUB-0008` ruled **append-only history, exactly one record in force at a time**. The obvious
model — a row with `EffectiveFromUtc` and `EffectiveToUtc`, where a plan change closes the old row
and opens a new one — **is wrong in this repository, and wrong for a stated reason.**

`EmployeePositionAssignment` already settled it:

> "There is no `EffectiveToUtc`, because closing an interval would mean UPDATING the previous row,
> which is precisely the history mutation this model exists to prevent; **the interval is derived by
> ordering**."

So a subscription record carries **`EffectiveFromUtc` and nothing that closes it**. "Exactly one in
force" is not a column and not a flag — it is a **derived invariant**: the record in force at instant
`T` is the one with the greatest `EffectiveFromUtc <= T`. A plan change appends; nothing is edited.

This is also what makes `REQ-SUB-0028`'s proration computable: reconstructing what a tenant was
entitled to on any past date is a query, not an audit trail reading exercise.

**Consequence, stated as an invariant because it is easy to lose:** the append is **monotonic** — a
new record's `EffectiveFromUtc` must be **strictly greater** than the current maximum for that tenant.
Without that rule an append could insert *behind* the present and silently rewrite what was in force
when a metered overage happened, which is the exact failure append-only exists to prevent.

### 2. `PreventAppendOnlyMutation` does not exist on the Platform database

**This is a build obligation, discovered by reading rather than assumed, and it must not be found
later.** The append-only guard is
`TenantDbContext.PreventAppendOnlyMutation` — `TenantDbContext.cs:484` — which refuses `Modified` or
`Deleted` for any `IAppendOnlyEntity` unconditionally. It is called from `TenantDbContext`'s
`SaveChangesAsync` and **from nowhere else**.

Every commercial record in this package lives in the **Platform** database (`DEC-SUB-0003`,
`ADR-017` § Platform database boundary (`:164`), `ADR-017` § Lookup classification, class A (`:477`)), and `PlatformDbContext` has **no equivalent guard** — its
`SaveChangesAsync` at `PlatformDbContext.cs:107` does not call one.

So `OD-SUB-0008`'s ruling rests on a mechanism that is not present on the side of the product where
this data lives. **`PlatformDbContext` must gain the same guard**, marking `IAppendOnlyEntity` the
one shared meaning rather than a second, divergent notion. Carrying the interface without the guard
would give the model the *appearance* of immutability and none of it, which is worse than not
claiming it.

---

## Aggregates

### `SubscriptionPlan` — the reusable commercial definition

Platform-global catalog data. `ADR-017` § Lookup classification, class A (`:477`) classifies subscription plans as **Class A — Platform
global**, "Stored in the Platform database. **Tenants cannot create global rows**"
(`REQ-SUB-0002`, `REQ-SUB-0003`).

- Root: `SubscriptionPlan` — `AggregateRoot<Guid>`, following `Tenant`'s shape: value objects for
  code and name, `Result<T>` factories, inline audit columns, `RowVersion`.
- `PlanCode` (value object, normalized, unique) and `PlanName` (value object).
- **Granted modules** — a set of `ModuleKey`, the unit `OD-SUB-0005` ruled (see below).
- **Limits** — `PlanLimit`, a keyed cap (`REQ-SUB-0002` × `OD-SUB-0017`). Seats are the first, and
  the model is keyed rather than a `SeatCap` column so a second cap does not require a schema change
  and a third notion of "limit".
- **Prices** — `PlanPrice`, one per supported currency, per billing period. `OD-SUB-0015` ruled
  **multi-currency**, so price is a collection, not a scalar. Money is `decimal(19,4)` **inherited
  from `ADR-027`**, which is already activated and already inherited unchanged by GL, Payroll and
  Attendance; this package does not restate it as a decision of its own (`DEC-SUB-0008`,
  `REQ-SUB-0023`, `REQ-SUB-0024`).

**Mutable, not append-only.** A plan is a catalog entry that is edited before and between uses. Its
changes are audited in the established shape, not frozen. **What must never be mutable is the
association between a tenant and a plan at a past instant** — and that lives on the subscription
record, not here.

**Choice made where the ruling set is silent:** a plan carries a lifecycle status
(`Draft` / `Active` / `Retired`) so a plan can stop being sellable without being deleted while
historical subscription records still point at it. Deleting a plan a past record references would
break `REQ-SUB-0028`'s reconstruction. Stated here rather than left to the build.

### `ModuleDefinition` — the enablement unit, and only this unit

`OD-SUB-0005` ruled that a module is **the unit carrying exactly one `IPermissionCatalogContributor`
and one `Add*Module()` registration**, each declaring a stable module key. Not the route group, not
the assembly.

Today that is **four**: HR, Finance/GL, Payroll, Attendance — verified against
`src/Host/SSAS.Host.API/Program.cs`, which registers four
`IPermissionCatalogContributor` implementations while mounting **seventeen** route groups, seven of
them HR's alone.

- Root: `ModuleDefinition` — `AggregateRoot<Guid>`, Platform-global. `ADR-017` § Lookup classification, class A (`:477`) names "module
  definitions" in the same Class A list as subscription plans, so its residency is inherited, not
  chosen.
- `ModuleKey` (value object) — stable, never reused, and the single token that both a plan grant and
  a route gate resolve against. `REQ-SUB-0015` requires routes and permissions to gate on the *same*
  unit; two notions of "module" would make that requirement unsatisfiable.
- The **Host is exempt and so is the whole Platform plane** (`REQ-SUB-0013`), so those seven route
  groups have no module key and never acquire one. That is a modelling statement, not a
  configuration default: a Platform route that could be gated is a route a lapsed tenant could be
  locked out of, taking with it the surface that would let it be re-enabled.

### `TenantSubscription` — the append-only history

The spine (`REQ-SUB-0001`, `REQ-SUB-0006`, `REQ-SUB-0017`).

- Root: `TenantSubscription` — `Entity<Guid>`, **`IAppendOnlyEntity`**, with the guard obligation
  above.
- `TenantId` — the subject of the agreement, never its owner (`DEC-SUB-0002`, `REQ-SUB-0004`). Both
  this row and `Tenant` live in the Platform database, so this is an **intra-database foreign key**
  and should be one. `DEC-SUB-0009` bars *cross-database* keys, not this.
- `SubscriptionPlanId` — the plan in force from this record's instant.
- `EffectiveFromUtc` — **the only interval column.** No `EffectiveToUtc`; see above.
- **Term** — `SubscriptionTerm`, a value object of `StartUtc` and either an `EndUtc` or an explicit
  perpetual marker. `OD-SUB-0009` ruled "an end **or an explicit perpetual marker**", and the word
  *explicit* is doing work: a nullable `EndUtc` alone cannot distinguish *perpetual* from *not yet
  set*. The value object therefore carries a closed `TermKind` (`Fixed` / `Perpetual`) alongside the
  nullable end, and refuses the two incoherent combinations at construction.
- `BillingCurrencyCode` — which of the plan's currencies this tenant is billed in
  (`OD-SUB-0015`). Held on the record rather than on the tenant, so a currency change is a history
  event like any other.
- **No `RowVersion`, no `ModifiedUtc`, no `ModifiedBy`.** `EmployeePositionAssignment` states the
  reason: "a record that is never updated has no concurrency state to protect". It carries
  `CreatedUtc`, `ChangedBy` and a reason code and text, matching the same type's shape.

**Invariants:**

1. **Monotonic append** — `EffectiveFromUtc` strictly greater than the tenant's current maximum.
2. **Exactly one in force** — derived, never stored: the record with the greatest
   `EffectiveFromUtc <= T`.
3. **A record is never edited.** A correction is another record, exactly as a position correction is
   another position change.

**Concurrency, chosen and stated.** `EmployeePositionAssignment` serializes on `Employee.RowVersion`
because a position change also writes `Employee`. Nothing on `Tenant` changes when a subscription is
appended, so there is no equivalent bump to ride on. **Two appends racing could both satisfy
"strictly greater than the current maximum" and produce two records at the same instant.** The model
therefore requires the append to take a write lock on the tenant's row inside the transaction — the
repository already has a documented lock order that includes `Tenant` (`FP-002`
`business-rules.md`) — **and** a unique constraint on `(TenantId, EffectiveFromUtc)` as the
mechanical backstop. Belt and braces, deliberately: the lock makes the race rare and the constraint
makes it impossible.

### `TenantEntitlementGrant` — additive, and additive is a shape not a rule

`OD-SUB-0011` ruled **additive grants only**: a tenant may be granted a module or a raised cap above
its plan, **never below** (`REQ-SUB-0010`).

- Root: `TenantEntitlementGrant` — `Entity<Guid>`, **`IAppendOnlyEntity`**, same shape as the
  subscription history and for the same reason: a grant that was in force last March must still be
  discoverable next March.
- `GrantKind` — a closed set, `ModuleGrant` or `LimitRaise`. A closed domain enum with a database
  `CHECK`, per `ADR-017`'s category D, not a lookup table.
- `ModuleKey` **or** (`LimitKey`, `LimitValue`) depending on kind.
- `EffectiveFromUtc`, and an optional `ExpiresUtc` — a pilot grant that ends is the ordinary case, and
  a grant expiring is not a mutation, it is a value read at resolution time.
- `GrantedBy`, reason code and text. A goodwill grant with no recorded reason is a support incident
  waiting to happen.

**The additive rule is enforced twice, on purpose.** At write time a `LimitRaise` naming a value at
or below the plan's cap is **refused** with a modelled error — loud, at the moment someone makes the
mistake. And at resolution time the cap is `max(plan, grants)`, so **even a grant that somehow named
a lower value cannot lower anything**. The invariant is a property of the resolution function's
shape rather than a rule a future author must remember; the write-time refusal exists so the mistake
is visible rather than silently absorbed.

---

## Entitlement resolution — the centre of the model

Everything in the enablement half reduces to one function, and `REQ-SUB-0007` is exactly this
question asked about one module.

```
EntitlementAt(tenantId, T):

    subscription := the TenantSubscription for tenantId with the greatest
                    EffectiveFromUtc <= T                        -- OD-SUB-0008
    if subscription is null                     -> no entitlement
    if T is outside subscription.Term           -> no entitlement -- OD-SUB-0009

    plan   := subscription.SubscriptionPlan
    grants := TenantEntitlementGrants for tenantId where
              EffectiveFromUtc <= T and (ExpiresUtc is null or ExpiresUtc > T)

    modules := plan.Modules  ∪  { g.ModuleKey : g in grants, g.Kind = ModuleGrant }
    cap(k)  := max( plan.Limit(k),
                    max{ g.LimitValue : g in grants, g.Kind = LimitRaise, g.LimitKey = k } )
```

**`T` is a parameter, and that is the whole point.** With `T = now` this answers "may this request
proceed" (`REQ-SUB-0007`, `REQ-SUB-0011`). With `T = the instant a seat was observed` it answers
"was that seat within cap" — which is the question a metered overage actually asks.

**A cap in force is a property of the subscription record live at that moment, not of the tenant.**
Judging a March overage against today's plan would let a tenant escape an overage by upgrading in
April, or acquire one by downgrading. Both are wrong, and both are what "the tenant's cap" would
produce.

### The metered-usage record, and one choice that needed making

`OD-SUB-0017` ruled **seats plus limits** — seats metered for billing, caps enforced alongside
enablement (`REQ-SUB-0027`).

- `TenantSeatUsageSample` — `Entity<Guid>`, **`IAppendOnlyEntity`**: an observation of how many seats
  a tenant held at an instant. An observation of a past fact is the textbook append-only record.

**The choice:** the sample **stamps the `TenantSubscriptionId` in force at observation time**, rather
than leaving billing to re-derive it later from `EffectiveFromUtc <= ObservedAtUtc`.

Re-derivation is one query and no extra column, so the stamp needs its justification. It is this: an
invoice line must be reproducible years later, and re-derivation makes the answer a function of the
history *as it stands when you ask*. The monotonic-append invariant is what makes those two agree
today — and a stamped identifier keeps them agreeing even if that invariant is ever relaxed, or a
record is inserted by a migration rather than by the application. **The stamp turns a derived fact
into a recorded one at the moment it is cheapest and most certain to be right.**

---

## The commercial record

`OD-SUB-0016` ruled that **the product issues invoices and captures payment itself**. This section
models the record. It stops, deliberately and visibly, where payment-capture mechanics begin.

### `SubscriptionInvoice` — vendor to tenant

- Root: `SubscriptionInvoice` — `AggregateRoot<Guid>`. Mutable while `Draft`; **`IAppendOnlyEntity`
  is not the mechanism here**, because an invoice is assembled before it is issued and immutable
  after. That is the two-type shape `DEC-ATT-0009` describes — a draft aggregate and an issued one —
  and the model follows `JournalDraft` / `JournalEntry` from FP-011 rather than inventing a third
  way. **Issuing is the promotion; a correction after issue is a credit note, never an edit.**
- `InvoiceNumber` — a value object, unique **vendor-wide** rather than per tenant. Chosen and stated:
  an issuer's invoice numbering is a single sequence in every jurisdiction that regulates it, and a
  per-tenant sequence would be a hard thing to unpick later.
- `TenantId`, `CurrencyCode`, `IssuedUtc`, the billed period, and lines.
- `SubscriptionInvoiceLine` — each line names the **`TenantSubscriptionId` it is billed against**, so
  an invoice spanning a mid-term plan change carries one line per record in force during the period.
  This is what `OD-SUB-0015`'s proration is computed over, and it is legible on the invoice rather
  than reconstructed from it (`REQ-SUB-0025`, `REQ-SUB-0028`).
- Amounts are `decimal(19,4)`, inherited from `ADR-027` (`REQ-SUB-0024`).

### `SubscriptionPaymentAttempt` — and where this model stops

`REQ-SUB-0026`. The record of an attempt to settle an invoice: which invoice, when, the outcome from
a closed set, and an **opaque provider reference**.

> ### ⚠ `T-010` OWNS PAYMENT CAPTURE. THIS MODEL DOES NOT SETTLE IT.
>
> `OD-SUB-0016` puts cardholder data in PCI-DSS scope, and the standard containment answer —
> isolating the cardholder-data environment from the application — is in tension with
> **`ADR-001` Modular Monolith**. That tension is architectural. It is queued as **`T-010`**, and a
> model document must not decide it by drawing a table.
>
> **What this model does assert, because it is a modelling statement rather than an architectural
> one: no entity in this package carries a column that could hold a primary account number, a card
> verification value, an expiry date, or any other cardholder datum.** The attempt record holds an
> opaque reference issued by whatever mechanism `T-010` rules on, and nothing else. If a future
> design needs such a column here, that is `T-010` reopening, not a schema addition.

### What is deliberately absent, and why

- **No tax model.** Multi-currency invoicing from a vendor to tenants crosses jurisdictions, and
  which regime applies to which tenant is **unauthored anywhere in this repository**. `DEC-PAY-0016`
  is the standing precedent for refusing to encode a jurisdiction the product has not named, and it
  is followed here. An invoice total is the sum of its lines; nothing computes a statutory liability.
- **No dunning, no refund, no credit-note aggregate.** The ruling set names them as consequences of
  `OD-SUB-0016` with **no requirement covering them today** — `REQ-SUB-0025` and `REQ-SUB-0026` are
  one line each. Modelling them here would invent scope. Named as absences so the gap is visible at
  acceptance rather than discovered at build.
- **No trial *concept*, and a trial that is nevertheless real.** `OD-SUB-0014` ruled a trial is a plan
  with a short term — not a state, not a flag — and `DEC-L-034` (2026-08-26) **used** that ruling: a
  tenant without a subscription holds an all-module plan on a 14-day term. **So there is still nothing
  in this model representing a trial, and that is now a stronger statement than it was**: the trial is
  an ordinary `SubscriptionPlan` and an ordinary `SubscriptionTerm`, and every mechanism already here
  handles it unchanged. *(Amended 2026-08-26 — this bullet said `REQ-SUB-0020` "falls away". It does
  not: it is unconditional and carries `AC-SUB-0033` plus `AC-SUB-0052`–`AC-SUB-0054`.)*
- **No commercial state on `Tenant`.** `OD-SUB-0010` ruled the two dimensions orthogonal. `Tenant`
  gains no field, and `TenantStatusChangeReason` gains no member — see
  [`lifecycle-model.md`](lifecycle-model.md).

---

## What is *not* an aggregate

- **The enabled-module set** (`REQ-SUB-0014`). It is `EntitlementAt(tenant, now).modules` — a
  projection, never stored. Storing it would create a second copy that can disagree with the plan,
  and `OD-SUB-0004` ruled the cache **invalidated on subscription change, never TTL-refreshed**,
  which only makes sense for something derived.
- **Entitlement itself.** Same reason. It is a function, and `REQ-SUB-0009` — a change taking effect
  without re-issuing tokens — is only true if nothing durable holds a stale copy. `FP-002`'s exact
  claim cardinality already forbids the other obvious cache, the access token (`DEC-SUB-0005`,
  `REQ-SUB-0008`).
- **The "current" subscription.** Derived by ordering. A `CurrentSubscriptionId` column on `Tenant`
  would be the `EffectiveToUtc` mistake wearing different clothes.

---

## Two requirements this slice deliberately does not reach

Twenty-six of the twenty-eight `REQ-SUB` are cited by identifier across these three documents. The
two that are not are named here so the gap reads as a boundary rather than an oversight, and so
T-008's matrix has somewhere to chain them from.

- **`REQ-SUB-0012`** — the refusal is applied by *one mechanism* covering every module uniformly.
  The model supplies what that mechanism resolves against: a single `ModuleKey`, one unit, shared by
  the plan grant and the permission catalog (`OD-SUB-0005`). **That the mechanism is one shared
  endpoint convention rather than a per-module check is `DEC-SUB-0006`, and its shape belongs to
  `authorization-model.md` — T-007.**
- **`REQ-SUB-0021`** — a tenant-plane user may read which modules its tenant has, and may **not** read
  the commercial terms. **The disclosure boundary this draws is exactly the line already drawn in
  this document**: the enabled-module set is a projection with no price, no invoice and no payment in
  it, while everything under *The commercial record* sits on the other side. **Which caller may read
  which is an authorization question — T-007.**

Neither is contradicted here, and neither is silently absorbed.

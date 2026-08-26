# FP-014 — Lifecycle model (proposed)

Written from the ruling set of 2026-08-25. Reads on from
[`domain-model.md`](domain-model.md) and [`data-model.md`](data-model.md).

---

## The subscription record has no lifecycle. The tenant's commercial state does.

This is the sentence to get right before any diagram, and it falls directly out of `OD-SUB-0008`.

A `TenantSubscription` row is **written once and never changes**. It has no status column, no state
machine and no transitions, because there is nothing about it that can transition — it is a statement
that *from this instant, this tenant is on this plan under this term*. That statement was either true
or it was not, and appending a later record does not make it false.

What *does* have a lifecycle is **the tenant's commercial state**, and it is **derived** at read time
from the history plus the clock:

| Derived state | Condition, evaluated at instant `T` |
|---|---|
| **`Unsubscribed`** | no `TenantSubscription` with `EffectiveFromUtc <= T` |
| **`InTerm`** | the record in force at `T` has `TermKind = Perpetual`, or `TermStartUtc <= T < TermEndUtc` |
| **`Expired`** | the record in force at `T` has `TermKind = Fixed` and `TermEndUtc <= T` |

**`Expired` is reached without anything being written.** No job flips a flag, no scheduled task
sweeps the table, no row is updated at midnight. The clock passes `TermEndUtc` and the same query
that returned `InTerm` yesterday returns `Expired` today.

That is not a convenience. A state that requires a write to become true is a state that is **wrong
whenever the writer has not run** — and the write that would have to run here is one per tenant per
expiry instant, which is precisely the class of scheduled job that fails quietly at 3am. Derivation
makes the answer correct by construction, and it is the same reasoning that put the interval in
ordering rather than in an `EffectiveToUtc` column.

---

## Transitions — all three are appends

There are exactly three things that can happen, and each is one new row.

### 1. First subscription — `Unsubscribed` → `InTerm`

A platform administrator assigns a plan (`OD-SUB-0013`, `REQ-SUB-0004`, `REQ-SUB-0022`). The first
`TenantSubscription` row is written with `EffectiveFromUtc` at or after now.

**A tenant with no record reaches no gated module** (`REQ-SUB-0011`). There is no default plan and
no backfill — see the sequencing obligation in [`data-model.md`](data-model.md), because shipping
the enablement gate before every existing tenant has a record would lock out the whole estate.

### 2. Plan change, renewal, currency change — `InTerm` → `InTerm`

All three are the same act: **append a record, edit nothing** (`REQ-SUB-0001`). The previous record
is untouched and remains the truth about the period it covered, which is what makes
`OD-SUB-0015`'s proration and `OD-SUB-0017`'s overage judgement reconstructable
(`REQ-SUB-0028`, `REQ-SUB-0027`).

Constrained by the **monotonic append** invariant: `EffectiveFromUtc` strictly greater than the
tenant's current maximum. A change cannot be backdated behind an instant that has already been
billed or metered against — and the reason that matters is `TenantSeatUsageSamples`, whose rows
stamp the record they were judged against.

**Renewal is not a distinct concept in this model, and that is a choice worth naming.** A renewal is
a new record with a later term; a plan change is a new record with a different plan. Modelling them
as one act means there is no state in which a subscription is "renewing", no partial write, and one
code path to test. If the product later needs to distinguish them for reporting, `ChangeReasonCode`
already carries the distinction without a second mechanism.

### 3. Expiry — `InTerm` → `Expired`, with **no write at all**

Covered above. The clock does it.

**Recovery from `Expired` is transition 2**: append a record with a new term. There is no "reactivate"
act, no `Suspended` commercial state, and nothing to un-set.

---

## `TenantStatus` is orthogonal, and stays orthogonal

`OD-SUB-0010` ruled it: subscription state and `TenantStatus` are **independent dimensions, both
checked at login** (`REQ-SUB-0019`).

**Three things follow, and all three are absences that must be actively preserved:**

1. **Expiry never writes `TenantStatus`.** An expired tenant is still `Active` as far as the tenant
   lifecycle is concerned. `Tenant.Suspend(...)` is not called, and nothing in this package holds a
   reference that could call it.
2. **`TenantStatusChangeReason` gains no commercial member.** It carries `Created`,
   `ProvisioningCompleted`, `IssueResolved`, `Administrative`, `Security`, `Compliance`,
   `Operational` — and it stays exactly that. Adding `NonPayment` would be the overload the ruling
   rejected, and it would change shipped Platform code and its guards.
3. **`Tenant` gains no column.** No plan, no expiry, no billing anchor. It carries what it carries
   today (`domain-model.md`, `DEC-SUB-0002`).

**Why the orthogonality earns its keep**, stated because a single dimension would look simpler: a
billing lapse and an administrative suspension are different events with different remedies, and the
platform must keep the ability to suspend a **paying** tenant for abuse or legal cause. Collapsing
them would make "suspended" ambiguous in exactly the situation where a support engineer most needs it
not to be.

### Login — two independent gates

`FP-002` already validates **live** tenant status on every tenant-scoped authenticated request and
permits only `Active`.

**`OD-SUB-0009` originally made expiry block login too. `DEC-L-033` (2026-08-26) amended that: expiry
gates modules and never blocks login.** The reason is recorded because it is not obvious from the
mechanism — **a lapsed customer who cannot sign in cannot reach the page that would let them
subscribe.**

So **login evaluates one condition, not two.** Commercial state is resolved per request at the
enablement gate instead:

| `TenantStatus` | Commercial state | Login | Gated modules |
|---|---|---|---|
| `Active` | `InTerm` | proceeds | reachable, per entitlement |
| `Active` | `Expired` or `Unsubscribed` | **proceeds** | **none — `403 module-not-enabled`** |
| `Suspended` / `Archived` / `Provisioning` | any | **refused — tenant lifecycle** | not reached |

The two outcomes are never collapsed into one boolean, and they now happen at different places:
a tenant-lifecycle refusal at authentication, a commercial one at the enablement gate.

> **`Authentication.md` was edited, and it needs no further edit for this amendment.** T-012 gave it
> the `Tenant-Management.md` disclaimer shape and replaced its expired-subscription rule with a
> **pointer** to `REQ-SUB-0018` rather than a restatement. Because it points rather than states, it
> stays correct through this amendment without being touched — which is precisely what that decision
> was for. Verified: the pointer resolves.

---

## Entitlement changes take effect immediately — and what that costs

`REQ-SUB-0009`: a change to a tenant's enablement takes effect **without re-issuing tokens and
without restarting the host**.

Two settled things make this non-negotiable rather than a performance preference:

- **`FP-002` forbids the obvious shortcut.** Its token model has exact claim cardinality and
  explicitly excludes "subscription or billing information" (`DEC-SUB-0005`, `REQ-SUB-0008`). An
  entitlement claim would also make every change wait out the 15-minute token lifetime.
- **`OD-SUB-0004` ruled the cache invalidated on subscription change, never TTL-refreshed**, and
  ruled that **cache invalidation is part of `REQ-SUB-0009`'s test surface, not an implementation
  detail.** A TTL is the scheduled-refresh failure wearing a different hat: it makes `REQ-SUB-0009`
  false for the length of the TTL.

**So every one of the three appends above is also an invalidation event**, and the model states it
here rather than leaving it to the build:

| Append | Invalidates |
|---|---|
| `TenantSubscriptions` row | that tenant's entitlement |
| `TenantEntitlementGrants` row | that tenant's entitlement |
| `SubscriptionPlans` / its modules / its limits | **every tenant whose in-force record names that plan** |

The third row is the one that gets missed. A plan is shared, so editing it changes the entitlement of
every tenant currently on it, and an invalidation keyed only on `TenantId` would leave all of them
stale until something else happened to them.

**Expiry has no invalidation event, and cannot have one** — nothing is written when a term lapses.
The cached value must therefore either carry the term and be evaluated against the clock on read, or
be keyed such that it cannot outlive `TermEndUtc`. **A cache that stores only "enabled: true" is
wrong at the instant of expiry and stays wrong until something unrelated evicts it.** This is the
sharpest interaction in the slice: derived expiry is what makes the model correct, and it is exactly
what a naive cache erases.

---

## The grant lifecycle

`TenantEntitlementGrant` rows are append-only on the same terms (`OD-SUB-0011`, `REQ-SUB-0010`).

- **Created** by a platform administrator, with a reason.
- **Lapses** at `ExpiresUtc` — derived, no write, same as term expiry.
- **Revoked** by appending a superseding record rather than editing or deleting. *Which* shape a
  revocation takes is a modelling detail the ruling set does not reach: a grant with `ExpiresUtc` set
  to now, or a distinct revocation row. **Chosen: append a new grant record of the same kind and key
  carrying `ExpiresUtc = now`**, because it needs no fourth `GrantKind` and reads in the same
  ordering query as everything else. Named as a choice so a reviewer can disagree with it cheaply.

**A grant can never lower anything**, in any lifecycle state. `max(plan, grants)` at resolution makes
that structural rather than procedural — see [`domain-model.md`](domain-model.md).

---

## The invoice lifecycle

`OD-SUB-0016`, `REQ-SUB-0025`.

```
Draft ──issue──▶ Issued ──settle──▶ Settled
  │                 │
  └────void─────────┴────void────▶ Void
```

- **`Draft`** — assembled, mutable, carries `RowVersion`. No `InvoiceNumber` yet.
- **Issue** — the promotion. Assigns the vendor-wide `InvoiceNumber`, stamps `IssuedUtc`, and the
  aggregate refuses every further edit. This is `JournalDraft` → `JournalEntry` from FP-011, not a
  new mechanism.
- **`Settled`** — reached when a `SubscriptionPaymentAttempt` succeeds.
- **`Void`** — a cancelled invoice. Numbers are **not** reused; a void invoice keeps its number, as
  every regulated numbering scheme requires.
- **A correction after issue is a credit note, never an edit.** Same discipline as GL's posted
  journals, where a correction is a reversal.

> **The credit-note aggregate is deliberately absent.** No requirement covers it —
> `REQ-SUB-0025` is one line — and the ruling set names refunds among the consequences of
> `OD-SUB-0016` with nothing specified. Modelling it here would invent scope. Named so the gap is
> visible now rather than at acceptance.

> ### ⚠ Settlement stops where payment capture begins — `T-010`
>
> This model says an attempt has an outcome and an invoice reaches `Settled`. **It says nothing about
> how a payment instrument is presented, tokenized, stored or charged.** `OD-SUB-0016` puts
> cardholder data in PCI-DSS scope, which is in tension with **`ADR-001` Modular Monolith**, and
> **`T-010` owns that decision.** A lifecycle document must not settle it by drawing an arrow.

---

## What has no lifecycle in this package

- **`ModuleDefinition`** — a registry entry. Added when a module ships, and its key never changes and
  is never reused (`OD-SUB-0005`).
- **The enabled-module set** (`REQ-SUB-0014`) — a projection of `EntitlementAt(tenant, now)`, never
  stored, so it has no states.
- **Disabling a module** — `OD-SUB-0012` ruled data is **retained untouched** and permissions become
  ineffective (`REQ-SUB-0016`, `REQ-SUB-0015`). So there is no teardown, no archival, no cascade and
  **no lifecycle at all** on the tenant's data when a module leaves its entitlement. Re-enabling
  restores reachability and nothing else has to happen, which is the property that makes a billing
  lapse non-destructive — the reason the ruling went that way for an ERP of record.

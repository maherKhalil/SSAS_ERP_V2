# FP-014 — API contracts

Written from the ruling set of 2026-08-25. Reads on from
[`authorization-model.md`](authorization-model.md), which carries the permission set and the plane
split, and [`domain-model.md`](domain-model.md), which carries the shapes.

---

## The rule that governs every request record

**Every request property carries `[property: JsonPropertyName]`. Every enum property additionally
carries `[property: JsonConverter(typeof(JsonStringEnumConverter))]`.**

`StrictRequestReader.ReadStrictJsonAsync` deserializes with `JsonSerializerOptions.Default`, which is
**case-sensitive** and reads enums **from numbers only**. This has shipped as a total, silent defect
twice — FP-011's missing `JsonPropertyName` made every GL write route answer `400 request.invalid`,
and FP-012's missing `JsonStringEnumConverter` made `POST /api/payroll/elements` refuse every
well-formed body.

Both faults were an **absence**, which reading the code does not reveal. This package carries enums
on nearly every write body — `TermKind`, `GrantKind`, `BillingPeriod`, invoice state — so the second
failure mode is live here in a way it was not for GL.

---

## Plane and prefix

Everything administrative is **platform-plane**, enforced with `RequirePlatformPermission`, under
`/api/platform/` beside the existing `auth`, `companies`, `localization` and `support` groups.

**The whole surface below is exempt from module enablement** (`REQ-SUB-0013`). A tenant that has
lapsed must still be reachable by the operator who would restore it, and a tenant user must still be
able to ask what it has.

### Plans — `Platform.Plans.*`

```
GET    /api/platform/plans                          Platform.Plans.View
GET    /api/platform/plans/{planId}                 Platform.Plans.View
POST   /api/platform/plans                          Platform.Plans.Administer
PUT    /api/platform/plans/{planId}                 Platform.Plans.Administer
POST   /api/platform/plans/{planId}/retire          Platform.Plans.Administer
PUT    /api/platform/plans/{planId}/modules         Platform.Plans.Administer
PUT    /api/platform/plans/{planId}/limits          Platform.Plans.Administer
PUT    /api/platform/plans/{planId}/prices          Platform.Plans.Administer
```

`retire` as a named **POST**, not `DELETE` — a plan is retired, never deleted, because historical
subscription records point at it (`REQ-SUB-0028`'s reconstruction). Nothing in this package responds
to `DELETE`, matching Payroll's surface.

`modules`, `limits` and `prices` are `PUT` of the **whole set**, not per-item add/remove. A plan's
grant list is a set, and a partial mutation API for a set invites two operators diverging it.

### Subscriptions — `Platform.Subscriptions.*`

```
GET    /api/platform/tenants/{tenantId}/subscriptions           Platform.Subscriptions.View
GET    /api/platform/tenants/{tenantId}/subscriptions/current   Platform.Subscriptions.View
POST   /api/platform/tenants/{tenantId}/subscriptions           Platform.Subscriptions.Administer
GET    /api/platform/subscriptions                              Platform.Subscriptions.View
```

**There is no `PUT` and no `DELETE`, and that is the append-only ruling showing through the
transport.** `POST` appends a record; assigning a first plan, changing plan, renewing and changing
billing currency are all the same call with different bodies (`OD-SUB-0008`, `REQ-SUB-0001`).

`/subscriptions/current` returns the record in force **now** — a convenience over the history, not a
different resource. It takes an optional `asOf` query parameter, because
`EntitlementAt(tenant, T)` takes the instant as a parameter and the operator answering *"what were
they on in March"* is the one who most needs it (`REQ-SUB-0027`, `REQ-SUB-0028`).

The unscoped `GET /api/platform/subscriptions` reads **across tenants** (`REQ-SUB-0022`) — the
platform plane is not tenant-filtered.

**`POST` is refused when `EffectiveFromUtc` is not strictly greater than the tenant's current
maximum** — the monotonic-append invariant, surfaced as a modelled error rather than a constraint
violation leaking through.

### Entitlement grants — `Platform.EntitlementGrants.Administer`

```
GET    /api/platform/tenants/{tenantId}/grants        Platform.Subscriptions.View
POST   /api/platform/tenants/{tenantId}/grants        Platform.EntitlementGrants.Administer
POST   /api/platform/tenants/{tenantId}/grants/revoke Platform.EntitlementGrants.Administer
```

`grants/revoke` as a **POST**, following HR's `manager/remove` and Attendance's `holidays/remove`:
revocation appends a superseding record and is a named act, not a deletion.

**A `POST /grants` whose `LimitValue` is at or below the plan's current cap is refused**
(`REQ-SUB-0010`, `OD-SUB-0011`). The refusal is loud at the moment the operator makes the mistake;
the resolution-time `max(plan, grants)` means it could not have lowered anything even if it had been
accepted. Both, on purpose.

### Invoices — `Platform.Invoices.*`

```
GET    /api/platform/invoices                         Platform.Invoices.View
GET    /api/platform/invoices/{invoiceId}             Platform.Invoices.View
GET    /api/platform/tenants/{tenantId}/invoices      Platform.Invoices.View
POST   /api/platform/invoices                         Platform.Invoices.Administer
PUT    /api/platform/invoices/{invoiceId}             Platform.Invoices.Administer
POST   /api/platform/invoices/{invoiceId}/issue       Platform.Invoices.Administer
POST   /api/platform/invoices/{invoiceId}/void        Platform.Invoices.Administer
GET    /api/platform/invoices/{invoiceId}/attempts    Platform.Invoices.View
```

`PUT` applies **only while `Draft`**; after `issue` the aggregate refuses it and the route answers a
modelled conflict. This is `JournalDraft` → `JournalEntry`, where posting is the promotion
(`REQ-SUB-0025`).

**Invoice numbers are vendor-wide and never reused**, including for a voided invoice.

### The tenant-facing read — `REQ-SUB-0014`

```
GET    /api/platform/modules/enabled                  authenticated tenant user, no permission
```

Returns the module keys `EntitlementAt(tenant, now)` resolves — **and nothing else**. No price, no
plan name, no term, no cap, no invoice (`REQ-SUB-0021`). The reasoning for requiring no permission is
in [`authorization-model.md`](authorization-model.md): a permission gate would render an empty
application for an unprivileged user and look identical to a tenant that has bought nothing.

**It is never gated by module enablement** — gating the endpoint that reports enablement on
enablement is a loop with no exit (`REQ-SUB-0013`).

---

## The gated response — `403 Forbidden`, and what it discloses

`OD-SUB-0006` ruled **403**, over 404, with the disclosure cost accepted knowingly.

A request to a route of a module the caller's tenant does not have (`REQ-SUB-0011`) is refused before
the handler runs, by the one shared convention `REQ-SUB-0012` requires — `RequireEnabledModule`,
applied to ten route groups across four module keys.

```
403 Forbidden
{
  "type":   "https://ssas.example/problems/module-not-enabled",
  "title":  "Module not enabled",
  "status": 403,
  "detail": "This tenant's subscription does not include the requested module.",
  "moduleKey": "Payroll"
}
```

**What 403 discloses, stated because the ruling accepted it rather than overlooked it:** the route
exists. A tenant can therefore enumerate the product's full module surface by probing, and learn that
Payroll exists even though it has not bought it. **In exchange, support can answer "why can't I reach
payroll" from the response** instead of from server logs, and an operator can tell a disabled module
apart from a bug and from a typo'd URL — which 404 makes indistinguishable.

**The refusal is uniform.** It carries the same `type` on every gated route of every module, because
`REQ-SUB-0012` is about one mechanism, and a per-module problem type would be four mechanisms wearing
one name.

### It is a different refusal from a permission refusal, and they must not merge

| Situation | Status | Problem type |
|---|---|---|
| tenant does not have the module | `403` | `module-not-enabled` |
| tenant has the module, user lacks the permission | `403` | the existing permission-denied type |
| subscription term expired | `403` | `module-not-enabled` |

Both are `403`, and they answer different questions — *the tenant has not bought this* versus *you
may not do this*. Collapsing them would make the first unanswerable from the response, which is the
whole benefit `OD-SUB-0006` bought by accepting the disclosure.

### Expiry is refused per route, not at login — amended 2026-08-26

**`DEC-L-033` amended `OD-SUB-0009`.** It previously ruled that expiry blocks login, and this section
described the refusal at the authentication surface. **It now gates modules and never blocks login**:
an expired tenant authenticates, reaches the platform plane, and is refused every gated route with the
same `403 module-not-enabled` any unentitled module produces.

**That is why the table above no longer has a third shape.** Expiry needed a special case only while
it acted somewhere else; acting through the enablement gate, it is the first row.

**A tenant-status refusal is still distinct and still at authentication** (`REQ-SUB-0019`,
`OD-SUB-0010`). The two remain separate outcomes — one commercial and resolvable by the customer,
the other administrative — and `OD-SUB-0010`'s orthogonality is untouched.

Public authentication failures remain generic in the response body, per `FP-002`'s existing rule.
The distinction is in the modelled outcome and the logs, not in what an unauthenticated caller is
told.

---

## No entitlement in the token, and the contracts must not imply one

`FP-002`'s access token has **exact claim cardinality** and excludes "subscription or billing
information" by name (`authentication-model.md:16`). So:

- **No response in this package returns a token, refreshes one, or asks for one to be re-issued after
  an entitlement change.** `REQ-SUB-0009` requires a change to take effect without it
  (`DEC-SUB-0005`, `REQ-SUB-0008`).
- **No request carries an entitlement assertion.** Entitlement is resolved server-side, per request,
  from the Platform database behind a cache invalidated on change (`OD-SUB-0004`).
- A client that has cached the enabled-module set may find it stale; `GET /api/platform/modules/enabled`
  is the refresh, and a `403 module-not-enabled` on a route the client believed enabled is the signal
  to re-read it.

---

## ⚠ No cardholder data crosses this surface — request or response

> **No request body accepted by any route in this package, and no response body returned by any route
> in this package, may carry a primary account number, a card verification value, a cardholder name,
> or an expiry date.**
>
> `SubscriptionPaymentAttempt` exposes an **opaque `providerReference`** and an outcome, and
> [`data-model.md`](data-model.md) states that no column exists that could hold a cardholder datum.
> **The transport statement is the more load-bearing of the two**, because a request body is the
> easiest place to cross the boundary by accident: a field added to a DTO does not need a migration,
> does not appear in a schema review, and reaches the application the moment it deserializes.
>
> `OD-SUB-0016` ruled that the product captures payment itself, which brings cardholder data into
> PCI-DSS scope and sits in tension with **`ADR-001` Modular Monolith**. **`T-010` owns that
> decision.** Whatever route accepts a payment instrument is `T-010`'s to specify, in `T-010`'s
> boundary — it is deliberately **not** in the list above, and its absence is the statement.
>
> **This extends to logging.** A request body logged at debug is stored cardholder data with a
> different name. Nothing in this package logs a request or response body of the payment surface.

---

## Error mapping

Modelled outcomes, not exceptions, reaching `ProblemDetails` through the established conventions.

| Outcome | Status | Notes |
|---|---|---|
| module not enabled for tenant | `403` | uniform `type` across every gated route (`REQ-SUB-0011`) |
| permission denied | `403` | existing type, unchanged |
| `EffectiveFromUtc` not after the current maximum | `409` | monotonic append refused |
| grant would not raise the cap | `422` | `REQ-SUB-0010`, refused at write |
| plan has no price in the tenant's billing currency | `422` | `REQ-SUB-0023` |
| edit of an issued invoice | `409` | promotion is one-way |
| plan retired, assignment attempted | `422` | |
| tenant has no subscription record | `404` on `/current` | **not** an empty success — an absent subscription is a real state (`REQ-SUB-0007`) |

**That last row is the one worth arguing about.** Returning `200` with a null body would make
"no subscription" indistinguishable from a serialization slip, and this is the state that decides
whether a tenant reaches anything at all. An empty list is indistinguishable from "no records" — the
same reasoning that made read scopes throw rather than return empty in `DEC-ATT-0008`.

---

## What is deliberately absent from the surface

- **No payment-instrument route.** `T-010`, above.
- **No credit-note route.** The aggregate is unmodelled — `REQ-SUB-0025` is one line and refunds are
  named in the ruling set as an uncovered consequence. Voiding an invoice is not a refund.
- **No tenant-plane write of any kind.** `REQ-SUB-0004`.
- **No usage-ingestion route.** `TenantSeatUsageSamples` are observations the product makes of
  itself, not something a tenant reports. A route accepting a seat count would let the billed party
  supply the billed quantity.
- **No bulk tenant-assignment route.** Assigning many tenants to a plan at once is exactly the shape
  of the migration hazard [`data-model.md`](data-model.md) raises — no default plan, no backfill —
  and it should not be made easy before that sequencing is ruled.

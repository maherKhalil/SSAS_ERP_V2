# FP-014 — Data model (proposed)

Written from the ruling set of 2026-08-25. Reads on from
[`domain-model.md`](domain-model.md), which carries the two mechanical constraints these tables are
shaped by.

**Every table here is vendor→tenant commerce.** Not the tenant's own receivables, which are General
Ledger's and live in the Tenant ERP database (`DEC-SUB-0001`).

---

## Settled before any table is drawn

**Residency — the Platform database, schema `platform`.** Not chosen here; inherited. `ADR-017:162`
places "Subscription/plan metadata when introduced" in the Platform-database residency list, and
`ADR-017:475` classifies subscription plans and module definitions as **Class A — Platform global**,
"Stored in the Platform database. **Tenants cannot create global rows**" (`DEC-SUB-0003`,
`REQ-SUB-0003`). `OD-SUB-0004` ruled the per-tenant **assignment** to the same database, resolved per
request behind a cache **invalidated on subscription change, never TTL-refreshed**.

**And it is load-bearing, not incidental.** `ADR-021:207` requires "account, **subscription**, and
other platform-only pages" to keep working during a customer-managed database outage (`DEC-SUB-0004`,
`REQ-SUB-0005`). Because enablement gates *every* request under `REQ-SUB-0011`, an entitlement read
that touched the Tenant ERP database would take a tenant's whole API down the moment its SQL Server
became unreachable — not degrade one page. **There is no projection of any of this into a tenant
database, and that absence is the requirement.**

**Money — `decimal(19,4)`, inherited from `ADR-027`.** Already activated by `OD-POS-004` and already
inherited unchanged by HR, GL, Payroll and Attendance. This package uses it and does not restate it
as a decision (`DEC-SUB-0008`, `REQ-SUB-0024`).

**No cross-database foreign key, in either direction** (`DEC-SUB-0009`). Every table below lives in
the Platform database alongside `Tenants`, so `TenantId` here is a **real intra-database foreign key
and should be one** — `DEC-SUB-0009` bars keys that cross the Platform/Tenant boundary, which none of
these do. Symmetrically: **nothing in any Tenant ERP database may carry a foreign key to any table
here.**

**Strings are `nvarchar`.** `ADR-018`, no exception and no per-column argument.

**Closed value sets are enums plus a `CHECK`, not lookup tables.** `ADR-017`'s category D, as used
today for tenant status and status-change reasons. `TermKind`, `GrantKind`, `InvoiceState`,
`PaymentOutcome` and `PlanStatus` are all category D.

**Ordinal collation on normalized code columns**, matching `PlatformPersistenceConstants.OrdinalCollation`
(`Latin1_General_100_BIN2`), which is how the Platform database already handles normalized natural
keys.

---

## Tables

### `SubscriptionPlans`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionPlanId` | `uniqueidentifier` | PK |
| `PlanCode` | `nvarchar(64)` | |
| `NormalizedPlanCode` | `nvarchar(64)` | ordinal collation; the uniqueness key |
| `PlanName` | `nvarchar(200)` | |
| `Status` | `int` | `Draft` / `Active` / `Retired`, `CHECK` constrained |
| audit + `RowVersion` | | `CreatedUtc`/`CreatedBy`, `ModifiedUtc`/`ModifiedBy` in the `Tenant` shape |

Unique: `(NormalizedPlanCode)` — **plans are Platform-global, so the key carries no `TenantId`.**
That is the visible difference between this table and every tenant-owned table in the product, and
it is `ADR-017:475`'s "tenants cannot create global rows" expressed as a constraint.

A plan is **never deleted**; it is `Retired`. Historical subscription records point at it and
`REQ-SUB-0028`'s reconstruction needs it to still resolve.

### `SubscriptionPlanModules`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionPlanId` | `uniqueidentifier` | FK → `SubscriptionPlans` |
| `ModuleKey` | `nvarchar(64)` | FK → `ModuleDefinitions` |

PK: `(SubscriptionPlanId, ModuleKey)`. No surrogate — the pair *is* the fact.

### `SubscriptionPlanLimits`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionPlanId` | `uniqueidentifier` | FK → `SubscriptionPlans` |
| `LimitKey` | `nvarchar(64)` | `Seats` is the first; keyed so a second cap needs no schema change |
| `LimitValue` | `bigint` | |

PK: `(SubscriptionPlanId, LimitKey)`. `bigint` rather than `int` because a limit may one day count
storage bytes or API calls, and widening a key column later is the expensive kind of change.

### `SubscriptionPlanPrices`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionPlanId` | `uniqueidentifier` | FK → `SubscriptionPlans` |
| `CurrencyCode` | `nchar(3)` | ISO 4217; `OD-SUB-0015` ruled multi-currency |
| `BillingPeriod` | `int` | closed set, `CHECK` constrained |
| `Amount` | `decimal(19,4)` | **`ADR-027`, inherited** |

PK: `(SubscriptionPlanId, CurrencyCode, BillingPeriod)`. A plan with no price row in a tenant's
billing currency cannot be assigned to that tenant — enforced in the domain, not by a constraint,
because the check spans two aggregates (`REQ-SUB-0023`).

### `ModuleDefinitions`

| Column | Type | Notes |
|---|---|---|
| `ModuleKey` | `nvarchar(64)` | **PK — the natural key is the key.** Ordinal collation |
| `DisplayName` | `nvarchar(200)` | |
| `IsGateable` | `bit` | false for the Host and Platform surface — `REQ-SUB-0013` |
| audit + `RowVersion` | | |

Four rows today: HR, Finance/GL, Payroll, Attendance — the four `IPermissionCatalogContributor`
registrations in `src/Host/SSAS.Host.API/Program.cs` (`OD-SUB-0005`).

**A `nvarchar` primary key is a deliberate departure** from the `uniqueidentifier` PK every other
table here uses, and it needs its reason stated. The module key appears in a plan's grant list, in a
tenant's grant list, in the enablement cache, in a `403` problem response, and — under `REQ-SUB-0015`
— in the permission catalog. A surrogate would mean every one of those either joins or carries the
key anyway. **If the build disagrees, a surrogate with a unique natural key is the right
disagreement**; what is not negotiable is that the key is stable and never reused.

### `TenantSubscriptions` — append-only

| Column | Type | Notes |
|---|---|---|
| `TenantSubscriptionId` | `uniqueidentifier` | PK |
| `TenantId` | `uniqueidentifier` | **FK → `Tenants`** — same database, so a real key (`DEC-SUB-0009`) |
| `SubscriptionPlanId` | `uniqueidentifier` | FK → `SubscriptionPlans` |
| `EffectiveFromUtc` | `datetimeoffset` | **the only interval column** — see below |
| `TermKind` | `int` | `Fixed` / `Perpetual`, `CHECK` constrained |
| `TermStartUtc` | `datetimeoffset` | |
| `TermEndUtc` | `datetimeoffset` **null** | `null` **iff** `TermKind = Perpetual`, `CHECK` constrained |
| `BillingCurrencyCode` | `nchar(3)` | |
| `CreatedUtc` | `datetimeoffset` | |
| `ChangedBy` | `nvarchar(256)` | |
| `ChangeReasonCode` | `nvarchar(32)` **null** | |
| `ChangeReasonText` | `nvarchar(512)` **null** | |

**No `EffectiveToUtc`. No `RowVersion`. No `ModifiedUtc`/`ModifiedBy`.** All three absences are the
same absence: the row is never updated. `EmployeePositionAssignment` states the reasoning and this
table follows it rather than inventing a second history shape — the interval is **derived by
ordering**, and a record that is never updated has no concurrency state to protect.

Unique: **`(TenantId, EffectiveFromUtc)`** — the mechanical backstop for "exactly one in force".
Index: `(TenantId, EffectiveFromUtc DESC)`, because *every* entitlement read is "the greatest
`EffectiveFromUtc <= T` for this tenant" and that index makes it a seek to the first row.

`CHECK`: `(TermKind = Perpetual AND TermEndUtc IS NULL) OR (TermKind = Fixed AND TermEndUtc IS NOT
NULL AND TermEndUtc > TermStartUtc)`. `OD-SUB-0009` asked for an end **or an explicit perpetual
marker**; this is what makes the marker explicit rather than inferred from a null.

**The monotonic-append invariant is domain-enforced, not a constraint.** "Strictly greater than the
current maximum for this tenant" cannot be expressed as a table `CHECK`. The write takes a lock on
the tenant row inside the transaction and the unique key catches the race — see
[`domain-model.md`](domain-model.md).

### `TenantEntitlementGrants` — append-only

| Column | Type | Notes |
|---|---|---|
| `TenantEntitlementGrantId` | `uniqueidentifier` | PK |
| `TenantId` | `uniqueidentifier` | FK → `Tenants` |
| `GrantKind` | `int` | `ModuleGrant` / `LimitRaise`, `CHECK` constrained |
| `ModuleKey` | `nvarchar(64)` **null** | FK → `ModuleDefinitions`; set iff `ModuleGrant` |
| `LimitKey` | `nvarchar(64)` **null** | set iff `LimitRaise` |
| `LimitValue` | `bigint` **null** | set iff `LimitRaise` |
| `EffectiveFromUtc` | `datetimeoffset` | |
| `ExpiresUtc` | `datetimeoffset` **null** | `null` = until revoked by a later grant record |
| `CreatedUtc`, `GrantedBy`, `ReasonCode`, `ReasonText` | | same shape as above |

`CHECK`: exactly one of the two shapes is populated — `(GrantKind = ModuleGrant AND ModuleKey IS NOT
NULL AND LimitKey IS NULL AND LimitValue IS NULL)` or the mirror. A row that is neither is a row the
resolution function cannot interpret.

Index: `(TenantId, EffectiveFromUtc)`. Grants are read on the same path as subscriptions and in the
same query window.

**No `CHECK` enforces "additive".** It cannot: whether a `LimitValue` raises anything depends on the
plan on the subscription record in force, which is two joins away and varies with time. `OD-SUB-0011`
is enforced at write time by the domain and made **structurally impossible to violate** at read time
by `max(plan, grants)` — see [`domain-model.md`](domain-model.md). Stated here so nobody looks for
the missing constraint and concludes it was forgotten.

### `TenantSeatUsageSamples` — append-only

| Column | Type | Notes |
|---|---|---|
| `TenantSeatUsageSampleId` | `uniqueidentifier` | PK |
| `TenantId` | `uniqueidentifier` | FK → `Tenants` |
| `TenantSubscriptionId` | `uniqueidentifier` | **FK — the record in force when observed, stamped** |
| `ObservedAtUtc` | `datetimeoffset` | |
| `SeatCount` | `int` | |
| `CreatedUtc` | `datetimeoffset` | |

Index: `(TenantId, ObservedAtUtc)`.

**The stamped `TenantSubscriptionId` is the table's whole point** (`REQ-SUB-0027`, `OD-SUB-0017`). A
metered overage is judged against the record in force **when the usage occurred**, not against
today's plan — otherwise a tenant escapes a March overage by upgrading in April. The stamp records
that judgement at the moment it is cheapest and most certain to be right, instead of leaving billing
to re-derive it from a history that must not be allowed to change underneath it.

### `SubscriptionInvoices`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionInvoiceId` | `uniqueidentifier` | PK |
| `InvoiceNumber` | `nvarchar(32)` **null** | **null while `Draft`**; assigned at issue |
| `TenantId` | `uniqueidentifier` | FK → `Tenants` |
| `CurrencyCode` | `nchar(3)` | |
| `State` | `int` | `Draft` / `Issued` / `Settled` / `Void`, `CHECK` constrained |
| `PeriodStartUtc`, `PeriodEndUtc` | `datetimeoffset` | the billed span |
| `IssuedUtc` | `datetimeoffset` **null** | |
| `TotalAmount` | `decimal(19,4)` | **`ADR-027`** |
| audit + `RowVersion` | | mutable while `Draft` |

Unique: **`(InvoiceNumber)` where `InvoiceNumber IS NOT NULL`** — a filtered unique index. Vendor-wide,
not per tenant: an issuer's numbering is a single sequence wherever it is regulated, and a per-tenant
sequence is hard to unpick later. Chosen and stated.

`RowVersion` is present here, unlike the append-only tables, because an invoice **is** mutable while
`Draft`. Immutability after issue is a state rule enforced by the aggregate — the `JournalDraft` /
`JournalEntry` shape from FP-011, not a second mechanism.

### `SubscriptionInvoiceLines`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionInvoiceLineId` | `uniqueidentifier` | PK |
| `SubscriptionInvoiceId` | `uniqueidentifier` | FK → `SubscriptionInvoices`, cascade |
| `TenantSubscriptionId` | `uniqueidentifier` | **FK — which record this line bills against** |
| `Description` | `nvarchar(512)` | |
| `Quantity` | `decimal(19,4)` | |
| `UnitAmount` | `decimal(19,4)` | **`ADR-027`** |
| `LineAmount` | `decimal(19,4)` | |

An invoice spanning a mid-term plan change carries **one line per subscription record in force during
the period**, which is what makes `OD-SUB-0015`'s proration legible on the invoice rather than
reconstructable from it (`REQ-SUB-0025`, `REQ-SUB-0028`).

### `SubscriptionPaymentAttempts`

| Column | Type | Notes |
|---|---|---|
| `SubscriptionPaymentAttemptId` | `uniqueidentifier` | PK |
| `SubscriptionInvoiceId` | `uniqueidentifier` | FK → `SubscriptionInvoices` |
| `AttemptedUtc` | `datetimeoffset` | |
| `Outcome` | `int` | closed set, `CHECK` constrained |
| `ProviderReference` | `nvarchar(256)` **null** | **opaque** |
| `CreatedUtc` | `datetimeoffset` | |

> ### ⚠ THE COLUMN LIST ABOVE IS COMPLETE, AND THAT IS THE POINT
>
> **No column in this table — or anywhere in this package — can hold a primary account number, a card
> verification value, a cardholder name, or an expiry date.** `ProviderReference` is opaque and issued
> by whatever mechanism `T-010` rules on.
>
> `OD-SUB-0016` ruled that the product captures payment itself, which puts cardholder data in PCI-DSS
> scope and sits in tension with **`ADR-001` Modular Monolith**. **`T-010` owns that decision and this
> data model does not settle it by drawing a table.** If a design later needs a cardholder column
> here, that is `T-010` being reopened, not a schema addition.

---

## Shared→Dedicated cutover — none of this participates, and that is correct

`TenantCutoverCopyPlan.Build` selects entities by reflection —
`typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType)`,
`TenantCutoverCopyPlan.cs:31` — and copies `TenantId`-scoped rows between **tenant** databases.

**No table in this package is `ITenantOwnedEntity`, none is in the tenant model, and none enters the
cutover inventory** (`DEC-SUB-0011`). They are Platform-database rows; a Shared→Dedicated migration
moves a tenant's ERP data and does not move the commercial record, because the commercial record
never left the Platform database.

**This is written down because for every module so far the same absence has been a defect.**
`DEC-ATT-0007`: "a tenant-owned type that does not carry the interface is **silently absent from
cutover** — no error, no warning, no test failure until a tenant migrates and its data does not
arrive." FP-012 shipped exactly that miss. **Here the absence is the intended outcome**, and the next
person to audit the cutover manifest against the entity list needs to find that stated rather than
file it as the same bug for a sixth time.

## Migration

Platform database, not tenant. These tables are added by a migration in
`src/Platform/SSAS.Platform.Infrastructure/Persistence/Migrations/` against `PlatformDbContext`,
schema `platform` — **not** through `tools/SSAS.Tenant.MigrationTool`, which is the tenant-database
path and has nothing to do with this data.

**Two obligations the build must not discover late:**

1. **`PlatformDbContext` must gain `PreventAppendOnlyMutation`.** It has none today; the guard exists
   only on `TenantDbContext` (`TenantDbContext.cs:484`). Three tables here are append-only and the
   ruling that made them so — `OD-SUB-0008` — is unenforced on this side of the product until that
   lands. See [`domain-model.md`](domain-model.md).
2. **No backfill, and no default plan.** A tenant with no `TenantSubscriptions` row has **no
   entitlement**, which under `REQ-SUB-0011` means it reaches no gated module. That is the correct
   reading of `CON-0001` and it is also a live operational hazard: the migration and the enablement
   gate must not ship in the same release without every existing tenant having been given a
   subscription record first. **Which plan each existing tenant gets is a commercial decision, not a
   migration default**, and inventing one would be exactly the guesswork this package exists to
   prevent. Raised here as a sequencing obligation for whoever schedules the build; it is not this
   slice's to rule.

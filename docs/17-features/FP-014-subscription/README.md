# FP-014 — Subscription and the Commercial Plane (analysis package)

**Status:** **RATIFIED — 2026-08-25.** All seventeen `OD-SUB` owner decisions are ruled, and
[`decisions-ratified.md`](decisions-ratified.md) is **this package's authority** — where it and
[`decisions-open.md`](decisions-open.md) disagree, the ratification file wins. The build prompt is
unblocked. Six rulings are the owner's, seven the architect's, four the owner's second round; each is
attributed, because reopening a commercial decision is a different act from reopening a structural one.

**Still outstanding, stated plainly rather than left to be inferred from a status word:**

- **No code and no schema.** Nothing here is implemented.
- **The master-register promotion is not done.** The `REQ-SUB` and `BR-SUB` prefixes are not yet in
  `Requirement-Numbering.md` (`OD-SUB-0002` ruled they arrive at ratification), `BR-SUB-0001`…`0021`
  are not yet promoted to the master `Business-Rules.md` under `DEC-L-012`, and the commercial plane
  still has **no `Product-Roadmap.md` entry**.
- **Two concerns are open at ratification** — the undefined seat and `REQ-SUB-0027`'s two enforcement
  semantics. Both are recorded at the foot of the ratification file. A third, this package's
  dependence on the still-`Proposed` `ADR-021`, was **closed by `DEC-L-024`** (2026-08-25): the ADR
  stays `Proposed` and every citation of it here now derives from `ADR-017`, which is `Accepted`.

`ADR-029` (tokenized payment capture) was written for this package and is `Accepted`. The tax question
`OD-SUB-0016` raised remains unauthored anywhere in the repository.

**Authority for the package existing at all:** `DEC-L-004` — the owner has ruled that `CON-0001` is
**upheld, not amended**, and that the commercial plane gets a full analysis package rather than a
narrow enablement fix.

---

# ⚠ THE BOUNDARY OF THIS FEATURE — read this before anything else

> **THIS PACKAGE IS ABOUT BILLING THE TENANT FOR THE PRODUCT. IT IS NOT ABOUT THE TENANT BILLING
> THEIR CUSTOMERS.**

Two directions of money exist in an ERP that is sold as SaaS, and they are separate systems that
happen to share vocabulary:

| | **The commercial plane — this package** | **The tenant's own receivables — NOT this package** |
|---|---|---|
| Who pays whom | the **tenant** pays the **product vendor** | the tenant's **customer** pays the **tenant** |
| What it buys | access to modules of this product | goods and services the tenant sells |
| Where it is recorded | the **Platform** database, one row per tenant | the **Tenant ERP** database, inside General Ledger |
| Who may see it | platform administration | the tenant's own finance users |
| Authority | `CON-0001`, `BR-PLT-0008`, `Glossary.md` "Subscription" | `FP-011` General Ledger, and a future receivables package |
| Tenancy | **not tenant-owned data** — the tenant is its *subject*, not its owner | tenant-owned, `ITenantOwnedEntity`, subject to cutover |

The words *invoice*, *plan*, *billing period* and *account* mean different things on each side of that
line. **A design that lets them meet is wrong**, and the specific failure it produces is not academic:
the tenant's General Ledger would acquire journal entries for the vendor's revenue, `TenantCutoverCopyPlan`
would carry the vendor's commercial records into a customer-managed database during a Shared→Dedicated
migration, and a tenant finance user with GL read permission would be able to read the vendor's pricing.

Everything below concerns the left-hand column only. Where this package says "invoice" it means an
invoice the vendor issues to the tenant, and nowhere else.

---

# The sweep — what was searched, what was found

Run 2026-08-25 against `main` at `f9b247a`.

| Swept | Result |
|---|---|
| `src/` for `Subscription`, `Billing`, `Invoice`, `Metering`, `Quota`, `Dunning`, `Seat`, `Pricing`, `Payment` (whole-word, excluding `obj/`, `bin/`) | **Zero files for every one of the nine terms** |
| `src/` for `Plan` as a whole word | **Two hits, both `TenantDatabaseVerificationFileLayout.Plan(...)`** — a method that lays out restore-verification files |
| `src/Platform/SSAS.Platform.Domain/Tenants/Tenant.cs` | code, name, status, lifecycle transitions, audit columns, `RowVersion`. **No plan, edition, seat count, entitlement, billing anchor or trial expiry** |
| `TenantStatus` enum | `Provisioning`, `Active`, `Suspended`, `Archived`. **No commercial state** |
| `docs/17-features/` | FP-001…FP-013. **No package covers the commercial model** |
| `docs/00-Master-Product-Specification/Product-Roadmap.md` | Versions 1–5. **Not one occurrence of subscription, billing, plan, metering, pricing, invoicing, licensing or signup** |
| `Requirement-Numbering.md` | ten functional prefixes — PLT, HR, GL, INV, CRM, PRJ, PAY, ATT, PRC, MFG. **No `SUB`.** Four business-rule prefixes — PLT, HR, GL, ATT. **No `BR-SUB`** |
| `CON-0001` across all of `docs/` | **exactly one occurrence — its own definition**, `Requirement-Catalog/Constraints.md:21` |
| `BR-PLT-0008` across all of `docs/` | **two occurrences** — its definition at `Business-Rules.md:161`, and a bare listing at `Tenant-Management.md:35` |
| `CON-0001` or `BR-PLT-0008` in `src/` or `tests/` | **zero** |

## The false positives, named so nobody re-derives them

Three searches return hits that look like commercial code and are not:

```
Entitlement   13 files   →  Attendance LEAVE entitlement — LeaveBalance.cs, LeaveErrors.cs and the
                            AddAttendanceFoundation migration. FP-013 vocabulary, not commercial.
Trial          5 files   →  General Ledger TRIAL BALANCE — GlEndpointRouteBuilderExtensions.cs:101-102,
                            GlReadModels.cs:83 (TrialBalanceRow). FP-011 vocabulary, not a free trial.
Plan          32 files   →  93 of the hits are the substring in "plane" (SecurityPlane, IsPlatformPlane).
                            The genuine ones are TenantCutoverTablePlan and TenantCutoverCopyPlan —
                            ADR-020 migration plans — and TenantDatabaseVerificationFileLayout.Plan.
Edition        2 files   →  SqlServerBackupCommandText EngineEdition — SQL Server Express detection.
```

**None of these is commercial-plane code.** `LeaveBalance` is an employee's leave entitlement, a
trial balance is a GL report, a cutover plan is a table-copy manifest, and an engine edition is a
SQL Server SKU. The product has no commercial concept implemented anywhere.

---

# What authority actually exists — three lines

The entire written basis for this package is three sentences, in three files:

| Source | What it says, verbatim |
|---|---|
| `Requirement-Catalog/Constraints.md:23` — **`CON-0001`** | "The application shall operate as a subscription-based Software-as-a-Service (SaaS) platform." |
| `Business-Rules.md:167`–`169` — **`BR-PLT-0008` Feature Enablement** | "Modules may be enabled or disabled per subscription plan." / "Disabled modules shall not appear in menus or APIs." |
| `Glossary.md:283` — **Subscription** | "A commercial agreement determining which modules and features are available to a Tenant." |

That is all of it. **This package says so rather than dressing it up.** `CON-0001` carries real weight
despite its brevity — its own file's preamble states that constraints "are mandatory and shall not be
violated without an approved ADR" — but weight is not detail, and nothing above tells anyone what a
plan contains, what it costs, when it expires, or what happens when it does.

## The material that looks like authority and is not

Four documents contain substantial subscription material. **Not one of them is authoritative**, and it
would be easy to mistake any of them for a specification:

**`docs/02-Functional/Platform/Tenant-Management.md`** carries an "Assign Subscription" capability
(`:63`), a "Subscription Plan" field (`:78`), an onboarding step (`:132`), a table named
`TBL-PLT-Subscription` (`:168`), a "Subscription Changed" event (`:201`), an activation criterion
"✓ Subscription is assigned" (`:217`), and "Multiple Subscription Plans" as a future enhancement
(`:231`). **Its own line 13 disclaims all of it:** the "subscription, billing … material below is
deferred and non-authoritative until covered by an approved feature package."

That disclaimer is doing exactly what it should, and it is worth stating what follows from it:
**`TBL-PLT-Subscription` is a name in a deferred draft, not a design.** This package does not inherit
its shape, its fields, or its assumption that a subscription is one table. It is evidence that the
authors intended a subscription to exist — nothing more. `Requirement-Numbering.md`'s own
*Database Tables* section lists `TBL-PLT-Tenant` and `TBL-PLT-Company` and **does not list
`TBL-PLT-Subscription`.**

**`docs/02-Functional/Platform/Authentication.md`** states "Expired subscriptions cannot login" (`:123`)
and lists "Expired Subscription" as a failure scenario (`:141`). **That document's status is `Draft`**
(`:7`–`:9`). It describes a login refusal for a state the product cannot represent, and it is the
sharpest illustration of the gap: an authored authentication rule with no data behind it. Whether it
becomes binding is `OD-SUB-0009`, not an assumption this package may make.

**`ADR-005:248`** lists "Subscription management" and "License management" among platform-administrator
capabilities. It names them; it does not model them.

**`docs/02-Functional/Platform/README.md:25`** lists "Subscription" as a Platform area. A table of
contents entry for a document that does not exist.

**The honest summary: the product's defining commercial constraint has one authored sentence, one
enforcement rule, one glossary line, and a scattering of drafts that explicitly say they do not count.**

---

# What is already decided elsewhere, and is therefore not open here

Four approved or in-force documents constrain this package before it starts. They are recorded as
`DEC-SUB` entries in [`decisions-open.md`](decisions-open.md) rather than presented as open questions,
because reopening them would be reversing a ruling rather than making one.

**`ADR-017` already places this data.** Its Platform-database residency list names
"Subscription/plan metadata when introduced" (`:162`), and its lookup classification puts
"subscription plans" in **Class A — Platform global**, "Stored in the Platform database. **Tenants
cannot create global rows**" (`:475`). The plan *catalog* is therefore settled, and only the
per-tenant *assignment* remains genuinely open — see `OD-SUB-0004`. This is a **correction to the
premise the task was issued under**, which assumed residency was wholly undecided; it is narrower
than that, and narrower is better.

**`ADR-017` requires the subscription surface to survive a tenant-database outage.** Subscription
and plan metadata is Platform-database data (`:164`), and Platform-database operation "never
depends on tenant-database routing or availability" (`:169`); an unavailable tenant database
yields a controlled unavailability result rather than a fallback (`:376`–`:378`). **Amended by
`DEC-L-024`** — formerly `ADR-021:207`, which is `Proposed` and does not yet bind. A design that reads entitlement from the tenant ERP database fails that
requirement the first time a customer's VPN drops.

**`FP-002` forbids entitlements in the access token.** The token model has **exact claim cardinality**
— the approved singleton claims plus `role` and `permission` values, with duplicates invalid — and it
explicitly excludes "subscription or billing information"
(`authentication-model.md:16`, `business-rules.md:55`). Enablement must therefore be resolved
**server-side, per request**. This is a constraint on the enforcement mechanism, and it is the one
most likely to be violated by accident, because a claim is the cheapest-looking place to put a flag.

**`ADR-027` already fixes money.** `decimal(19,4)`. If the commercial scope is ruled in, this package
inherits that representation and does not restate it as a decision of its own.

---

# The enforcement problem, stated precisely

`BR-PLT-0008` is not a data-model rule. Its second sentence is an **enforcement** clause with two
surfaces: *menus* **and** *APIs*.

`src/Host/SSAS.Host.API/Program.cs` mounts **seventeen** route groups unconditionally at lines
126–152 — one Host, six Platform, seven HR, and one each for GL, Payroll and Attendance — after five
`Add*Module()` registrations at lines 53–78. **There is no gate of any kind.** Every authenticated
caller of every tenant reaches every route of every module the product has built.

So the current state is not "the feature is not built yet". It is that **every route shipped so far
was shipped in violation of `BR-PLT-0008`**, and the retrofit grows by one module each time a module
ships.

**The seam already exists, which is the good news.** `PermissionEndpointConventions` (`:8`–`:37`)
establishes the pattern precisely: a generic convention that "expresses a requirement and nothing
more … names no permission, defines no policy and knows no module", with `RequirePermission` and
`RequirePlatformPermission` kept as **two separate methods** so that "a caller cannot choose the wrong
plane by passing a flag". Every module already composes endpoint filters this way. A module-enablement
gate is a **sibling of that convention**, not new architecture — and the two-plane split is exactly
what `REQ-SUB-0012` needs, since platform-plane routes must never be subject to tenant enablement.

**What the gate cannot be decided without** is what a "module" *is* for this purpose (`OD-SUB-0005`),
what a refused request returns (`OD-SUB-0006`), and what "menus" binds to in a repository that
contains no UI (`OD-SUB-0007`). Those are owner decisions, and the gate cannot be specified around
them.

---

# What this package does not contain

Ten of the thirteen files an FP-013-shaped package carries are **deliberately absent**:
`domain-model.md`, `data-model.md`, `api-contracts.md`, `authorization-model.md`, `lifecycle-model.md`,
`business-rules.md`, `acceptance-criteria.md`, `test-scenarios.md`, `traceability-matrix.md`,
`decisions-ratified.md`.

Each of them would state a design, and **no design has been chosen.** Whether a subscription has
states, whether pricing exists, whether a module is the unit of enablement, and whether the identifier
space is `REQ-SUB` or `REQ-PLT` are all open in [`decisions-open.md`](decisions-open.md). Writing a
data model on top of fourteen unruled questions would encode guesses as specification and give them
the authority of a checked-in document — which is precisely the failure `CON-0001` itself demonstrates,
one level up.

This package also does **not**:

- add a prefix to `Requirement-Numbering.md` — that is `OD-SUB-0002`, and FP-013 created `REQ-ATT` at
  **ratification**, not at analysis;
- add an entry to `Product-Roadmap.md` — the roadmap gains an entry when the package is ratified;
- claim an ADR number — `ADR-028` is currently the subject of a dangling reference from FP-010 and is
  not free to take;
- touch any file under `src/` or `tests/`. The enablement gate is specified here and built later.

---

# Reading order

1. This file — the boundary, the sweep, and what authority exists.
2. [`decisions-open.md`](decisions-open.md) — **`OD-SUB-0001` first.** Everything else is conditional
   on the scope ruling, and the requirements are written in conditional voice because of it.
3. [`requirements.md`](requirements.md) — the requirements each scope would put in force.

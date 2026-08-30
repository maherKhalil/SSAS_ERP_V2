---
package: FP-014
title: Subscription and the Commercial Plane
module: Platform
status: RATIFIED (2026-08-25) — and PARTLY BUILT. Measured 2026-08-30: of 54 acceptance criteria, 20 pinned by a named test, 11 implemented but unpinned, 17 not implemented, 4 blocked on an undefined subject, 2 vacuously satisfied. The entitlement half is built and tested; the billing half does not exist.
version: 1.0
date: 2026-08-25
---

# FP-014 — Subscription and the Commercial Plane

**Status:** **RATIFIED — 2026-08-25.** All seventeen `OD-SUB` owner decisions are ruled, and
[`decisions-ratified.md`](decisions-ratified.md) is **this package's authority** — where it and
[`decisions-open.md`](decisions-open.md) disagree, the ratification file wins. The build prompt is
unblocked. Six rulings are the owner's, seven the architect's, four the owner's second round; each is
attributed, because reopening a commercial decision is a different act from reopening a structural one.

**What is and is not done, stated plainly rather than left to be inferred from a status word:**

- ⚠ **THERE IS CODE AND THERE IS SCHEMA. THE ENTITLEMENT HALF OF THIS PACKAGE IS BUILT AND TESTED.**
  This bullet previously read *"No code and no schema. Nothing here is implemented"*, written **2026-08-25**
  and **falsified on 2026-08-26** by `AddSubscriptionCommercialPlane`, the first migration. It stood for
  four days. **The measurement that replaces it is below, and it is dated because it will decay the same
  way.** See [Principle 20](../../14-Engineering/Architecture-Principles.md) — an implementation-status
  claim is re-derived, never inherited, and this one was believed precisely because it was emphatic.

### Implementation status — measured 2026-08-30 against the tree and the test suite

**All 54 acceptance criteria, in four buckets:**

| bucket | count | what it means |
|---|---|---|
| **Pinned by a named test** | **20** | asserted by a test identified by name |
| **Implemented but unpinned** | **11** | the behaviour exists; nothing fails if it regresses |
| **Not implemented** | **17** | an engineer could build it today. **Three are now proven absent by test, not merely unfound** — see below |
| ⚠ **Subject undefined** | **4** | `AC-SUB-0040`, `0049`, `0050`, `0051` — **not engineering work; a decision nobody has made** |
| ⚠ **Vacuously satisfied** | **2** | `AC-SUB-0008`, `0026` — **met by the absence of the mechanism they guard against** |

**Revised later the same day, 2026-08-30, from 20 · 11 · 19 · 4.** Nothing was built or removed in between:
**two criteria moved out of *not implemented* because measuring them showed the defect was in the criterion.**
The count changed because the classification was wrong, not because the product changed — recorded here
rather than silently restated.

⚠ **A third move was proposed and REFUSED, and the refusal is the more useful record.** `AC-SUB-0045` was
reported as *"not satisfiable"* because it mentions *"this package's six"* permissions, which do not
exist. **But those six are a parenthetical, not the subject.** The criterion's actual assertion is that
**every** platform-plane permission name is used only with `RequirePlatformPermission` and every
tenant-plane name only with `RequirePermission` — over the whole 28-name set, **widened to it deliberately
by `DEC-L-010`** precisely so the ambiguity that already shipped would not go unasserted. **That subject
exists and the criterion is fully evaluable.** It stays in *not implemented*, where it was.

### ⚠ The fifth bucket: two criteria are met by the absence of what they guard

**A status column cannot express this, and green, red and absent would each mislead.**

- `AC-SUB-0008` and `AC-SUB-0026` are each met **by the absence of the mechanism they guard against.** `0008` requires that no tenant-plane subscription permission exist, and is satisfied
  because the package defines none on *either* plane. `0026` requires that losing entitlement delete no row
  — and **nothing is written when a term ends**: `HasExpiredAt` is a pure function of the term against the
  clock, no job runs, so there is no moment at which a deletion could occur and no before-and-after to
  count. ⚠ **Both guarantees are real and neither is evidence of anything, because the commit that first
  creates the mechanism is the commit that can violate them.** Whoever builds the missing half must
  re-check these two; they are notes attached to future work, not work.

### The three absences now proven rather than unfound

Item 162 built `tests/API.Tests/Infrastructure/EntitlementPermissionCouplingTests.cs` (7 tests, gate green,
PR #381) and closed `AC-SUB-0013`, `0024` and `0025` **by exercising the path instead of searching it.**
The tenant permission decision consults exactly three things — a validated tenant, `TenantStatus`, and the
caller's `permission` claims. **Entitlement is neither among them nor reachable from there.**

⚠ **The half that survives a refactor is the structural pair.** An outcome test states today's behaviour;
`No_tenant_authorization_handler_takes_an_entitlement_dependency` and its grant-path sibling **redden the
moment anyone adds an entitlement collaborator, whatever behaviour results.** Both controls were planted
and each reddened only its own test.

⚠ **`AC-SUB-0013` is closed for the DECISION path only.** Its other half — that the tenant token's claim
set is exactly FP-002's — is covered for the **platform** plane by `PlatformAccessTokenClaimsTests` and by
**nothing** for the tenant plane. **That gap is real and is not a subscription gap.**

**Scope of those tests:** the authorization decision and the grant path. A coupling composed elsewhere — in
a claims-issuing path, or a module handler doing its own entitlement check — is outside them.

⚠ **THE LINE FALLS ALMOST EXACTLY BETWEEN WHAT A TENANT MAY USE AND WHAT A TENANT IS CHARGED.** The
entitlement half — plans, grants, terms, expiry, the module gate, the cache — is built and genuinely well
tested: append-only immutability with both bypass routes covered, term invariants, cache expiry at the
boundary instant, the seed run twice, the archived tenant seeded like every other. **The commercial half
does not exist:** no declaration of `Invoice`, `PaymentAttempt`, `Overage`, `Proration` or `SeatUsage`
appears anywhere in `src/`. **So this document was not merely stale — it was wrong in the direction that
matters most, because the part it denied is the part carrying the tested guarantees.**

**Four things a criterion-by-criterion reading gets wrong, recorded so the next reader does not repeat it:**

1. ⚠ **`AC-SUB-0020`'s COUNTS ARE STALE AND ITS TEST IS STRONGER THAN ITS TEXT.** The criterion says
   *"exactly the ten gated route groups and the seven exempt ones"*; the host carries **20 `RequireModule`
   sites over four module keys** (Attendance, GL, HR, Payroll). **The test asserts neither number** — it
   asserts that every module-owned endpoint is gated and no platform-plane endpoint is, which is count-free
   and strictly stronger, and it carries its own anti-vacuity control. **Do not "fix" the test to match the
   criterion.** The criterion's numbers are what need correcting.
2. ⚠ **TWO CRITERIA ARE MET BY THE ABSENCE OF WHAT THEY GUARD — see the fifth bucket above, which is the
   only place they are classified.** The evidence, recorded once: all 28 platform permission names were
   enumerated and there is no `Platform.Subscriptions.*`, `Plans.*`, `Grants.*` or `Invoices.*`.
   ⚠ **`AC-SUB-0045` mentions *"this package's six"* and is NOT one of the two** — those six are a
   parenthetical; its subject is the whole 28-name set, and it is evaluable and unmet.
3. ⚠ **THE FOURTH BUCKET IS NOT A SMALLER VERSION OF THE THIRD.** All four rest on the **undefined seat**:
   `DEC-L-009` says *"seats"* and never defines one. `AC-SUB-0049` names `TenantUser` **because that is
   the only reading available, not because it was ruled** — flagged in T-008, again in T-013, still open,
   as is `REQ-SUB-0027`'s two enforcement semantics. **Filing these under "not implemented" would present
   a decision nobody has made as engineering work not yet done**, which is the sentence most likely to
   mislead an owner deciding whether this can be sold.
4. **The 28 requirements need no separate map.** Every one is cited by at least one acceptance criterion —
   the set difference is empty, no orphans — **so requirement status follows its criteria.** They were not
   mapped independently, and that is stated rather than implied.

**On the absence claims, which are the part of this that rots first.** Where *"not implemented"* rests on a
whole-tree symbol search, it says so. ⚠ **Where it rests on failing to find a seam, it says THAT instead:**
`AC-SUB-0013`, `0024`, `0025` and `0026` are recorded as *"no entitlement-to-permission coupling was
found"* — the coupling could be composed at a seam that was not searched, and `0026`'s *"counts before and
after"* needs an entitlement-lapse path that could not be exercised. **These are weaker claims than the
other fifteen and are not interchangeable with them.**

⚠ **And "pinned" does not mean "fully covered."** `AC-SUB-0020` is the worked example: pinned by a stronger
property while its own stated counts are wrong. **A summary reading "20 criteria are test-pinned" would be
true and would still let those numbers go on being wrong.**

**Provenance:** measured by the implementing window, reported in
[`.claude/handoff/results/item-161-fp014-implementation-split.md`](../../../.claude/handoff/results/item-161-fp014-implementation-split.md).
The named tests were **observed, not executed** as part of this measurement; the suites covering the
platform surface were run under separate items the same day. **No `src/` or `tests/` file was changed, so
no gate applies to this count.**
- **The master-register promotion is done.** `REQ-SUB-0001` and `BR-SUB-0001` are registered in
  `Requirement-Numbering.md` and `BR-SUB-0001`…`0021` are promoted into the master
  `Business-Rules.md` (T-022), which makes FP-014 **the first package to close `DEC-L-012`'s
  obligation at ratification** rather than leaving its rules stranded as `BR-PAY` and `BR-ATT` were.
  The commercial plane has its `Product-Roadmap.md` entry (T-027).
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
| `CON-0001` across all of `docs/` | **exactly one occurrence — its own definition**, `Requirement-Catalog/Constraints.md` § `CON-0001` |
| `BR-PLT-0008` across all of `docs/` | **two occurrences** — its definition at `Business-Rules.md` § `BR-PLT-0008`, and a bare listing in `Tenant-Management.md`'s Business Rules section |
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

# What authority existed before this package — three lines

**As of 2026-08-25, before ratification**, the entire written basis for the commercial plane was three
sentences, in three files. That scarcity is why this package exists, and it is recorded as it stood:
the master register now also carries `BR-SUB-0001`–`0021` (T-022).

| Source | What it says, verbatim |
|---|---|
| `Requirement-Catalog/Constraints.md` § **`CON-0001`** | "The application shall operate as a subscription-based Software-as-a-Service (SaaS) platform." |
| `Business-Rules.md` § **`BR-PLT-0008` Feature Enablement** | "Modules may be enabled or disabled per subscription plan." / "Disabled modules shall not appear in menus or APIs." |
| `Glossary.md` § **Subscription** | "A commercial agreement determining which modules and features are available to a Tenant." |

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

**`docs/02-Functional/Platform/Authentication.md`** stated "Expired subscriptions cannot login" and
listed "Expired Subscription" as a failure scenario, in a document whose status is `Draft`. It
described a login refusal for a state the product could not represent, and it was the sharpest
illustration of the gap: an authored authentication rule with no data behind it.

**Both halves are resolved.** T-012 replaced the restatement with a **pointer** to `REQ-SUB-0018`, so
the rule lives in one place; and `DEC-L-033` (2026-08-26) then changed that rule — expiry gates
modules and never blocks login. **The pointer needed no edit for the change**, which is the argument
for pointing rather than restating, demonstrated rather than asserted.

**`ADR-005`, § Platform Administration** lists "Subscription management" and "License management" among platform-administrator
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
`DEC-L-024`** — formerly `ADR-021` § 10 Outage behaviour (`:207`), which is `Proposed` and does not yet bind. A design that reads entitlement from the tenant ERP database fails that
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

**All thirteen files an FP-013-shaped package carries now exist.** Ten of them were **deliberately
absent** while this package was analysis only — `domain-model.md`, `data-model.md`, `api-contracts.md`,
`authorization-model.md`, `lifecycle-model.md`, `business-rules.md`, `acceptance-criteria.md`,
`test-scenarios.md`, `traceability-matrix.md` and `decisions-ratified.md` — and the reason is kept
here rather than deleted, because it is the argument for the order the work was done in.

Each of them states a design, and **while the owner decisions were open no design had been chosen.**
Whether a subscription has states, whether pricing exists, whether a module is the unit of enablement,
and whether the identifier space was `REQ-SUB` or `REQ-PLT` were all open questions. **Writing a data
model on top of seventeen unruled questions would have encoded guesses as specification** and given
them the authority of a checked-in document — precisely the failure `CON-0001` itself demonstrates,
one level up.

**They were written once the rulings existed, not before**, and `decisions-ratified.md` records which
ruling each rests on.

**While it was analysis only, this package also did not** — and each of these was deferred to
ratification rather than skipped:

- add a prefix to `Requirement-Numbering.md` — `OD-SUB-0002`, and FP-013 created `REQ-ATT` at
  **ratification**, not at analysis. **Done by T-022:** `REQ-SUB-0001` and `BR-SUB-0001` are registered;
- add an entry to `Product-Roadmap.md` — the roadmap gains an entry when the package is ratified.
  **Done by T-027**, in Version 1, marked specified and not yet implemented;
- claim an ADR number. **`ADR-029` was subsequently written for this package** (T-010) and is
  `Accepted`; `ADR-028` stays reserved for V5 Document Management and was never this package's to take.

**One of the four still stands, and it is the one that matters:**

- this package touches **no file under `src/` or `tests/`.** The enablement gate is specified here and
  built later. `BR-PLT-0008` remains violated by every mounted route group until it is.

---

# Reading order

1. This file — the boundary, the sweep, and what authority exists.
2. [`decisions-open.md`](decisions-open.md) — **`OD-SUB-0001` first.** Everything else is conditional
   on the scope ruling, and the requirements are written in conditional voice because of it.
3. [`requirements.md`](requirements.md) — the requirements each scope would put in force.

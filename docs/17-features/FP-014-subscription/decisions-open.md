# FP-014 — Decisions

Two registers. **`DEC-SUB-####`** are settled — by inheritance from an in-force document, or by
mechanical proof against the code — and are recorded so nobody re-derives them.
**`OD-SUB-####`** are **OWNER-DECISION-REQUIRED** and each one blocks the build prompt.

**Nothing in the `OD` register has a default.** Where this package holds an opinion it says so and
says why, but **an opinion is not a ruling**, and the build prompt must not be written until every
`OD-SUB` carries one. Where this package has no opinion worth offering, it says that too rather than
manufacturing one to fill the space.

**`OD-SUB-0001` is decided first.** Sixteen of the seventeen open decisions are conditional on it,
and [`requirements.md`](requirements.md) is written in conditional voice for that reason.

---

# Part 1 — `DEC-SUB` (settled; recorded, not reopened)

### `DEC-SUB-0001` — the two directions of money are two systems, and only one is in scope

**This package is the tenant paying the vendor. It is never the tenant billing its own customers.**

The full statement is in [`README.md`](README.md#-the-boundary-of-this-feature--read-this-before-anything-else)
and is settled here so no later document has to relitigate it.

The reason it is a `DEC` rather than an `OD` is that ruling otherwise would contradict three things
already in force: `ADR-017:475` puts subscription plans in the **Platform** database while General
Ledger is a **Tenant ERP** module; `ADR-021:207` requires the subscription surface to work while the
tenant database is *down*, which a GL-resident record cannot; and `FP-011` scopes GL to the tenant's
own books. **The separation is not a preference — it is already implied by the topology.**

**The concrete failure this prevents:** a vendor invoice recorded as a GL journal would be copied
into a customer-managed database by `TenantCutoverCopyPlan` on the next Shared→Dedicated migration,
and would be readable by any tenant user holding GL read permission.

### `DEC-SUB-0002` — the subscription is tenant-scoped data that the tenant does not own

A subscription is *about* a tenant, so it is tenant-scoped. It is not *owned* by the tenant, because
the tenant is the subject of the agreement, not a party able to amend it.

`ADR-017` already establishes this exact distinction for a different case, and names the reasoning:
tenant identity and access membership "remains Platform database data **even though it is
tenant-scoped**", called "a deliberate refinement of the naive rule 'everything `ITenantOwnedEntity`
moves to the tenant database'" (`:165`). The commercial plane is the same shape, with an
additional argument the identity case does not have — an owner who could edit its own entitlement
would make `BR-PLT-0008` unenforceable by design.

### `DEC-SUB-0003` — plan definitions live in the Platform database; `ADR-017` already ruled it

Not open, and it was not open when this package was commissioned. `ADR-017` says it twice:

> `:162` — Platform-database residency list: "**Subscription/plan metadata when introduced.**"
>
> `:475` — Lookup classification: "**A — Platform global**: `Country`, `Currency`, `Language`,
> **subscription plans**, permission catalog, module definitions, global localization catalog. Stored
> in the Platform database. **Tenants cannot create global rows.**"

**This narrows a question the task assumed was wholly open.** What remains genuinely undecided is not
*where plans live* but *where the per-tenant assignment lives and whether entitlement is projected
anywhere for read performance* — that residue is `OD-SUB-0004`.

One caveat recorded honestly: `ADR-017` carries `status: Proposed` (version 1.6). It is treated as
binding here because the repository already builds on it throughout, but it shares the
`ADR-026`/`ADR-027` status inconsistency and is not this package's to resolve.

### `DEC-SUB-0004` — the subscription surface must survive tenant-database unavailability

`ADR-021:200`–`:207`: during a customer-managed database outage, login, token refresh, platform
authority evaluation, tenant membership resolution, platform administration, and "**account,
subscription**, and other platform-only pages" all **continue to work**.

This forecloses an entire family of designs. Any entitlement read that touches the Tenant ERP
database fails the moment a customer's SQL Server is unreachable — and because enablement gates
*every* request under `REQ-SUB-0011`, that failure would take the whole API down for that tenant
rather than degrading one page. **The gate must read from Platform-side state or from cache, never
from the tenant database.**

### `DEC-SUB-0005` — entitlements cannot travel in the access token

`FP-002` is an approved package and its token model is **exact**: the listed singleton claims plus
zero or more `role` and `permission` values, with duplicate singleton claims and duplicate values
"invalid", and a hard 8192-byte ceiling that is "rejected rather than truncated"
(`authentication-model.md:14`). It then excludes the field explicitly — tokens "exclude … TenantName,
TenantStatus, CompanyId, **subscription or billing information**" (`:16`), restated in
`business-rules.md:55`.

So enablement is resolved **server-side per request** (`REQ-SUB-0008`), and a change takes effect
without re-issuing tokens (`REQ-SUB-0009`).

**This is the constraint most likely to be violated by accident**, because a claim is the cheapest
place to put a flag and the token already carries permission values that look adjacent to
entitlement. Adding an entitlement claim would amend an approved package silently, and would also
make every entitlement change wait for the 15-minute token lifetime.

### `DEC-SUB-0006` — the enforcement seam exists already; the gate is a sibling, not new architecture

`PermissionEndpointConventions` states its own design in the file (`:8`–`:13`): the generic
"this endpoint requires permission X" mechanism "expresses a requirement and nothing more. It names no
permission, defines no policy and knows no module: the CALLER supplies the permission name". Every
module composes endpoint filters on that pattern today — `GlCompanyContextEndpointFilter`,
`AttendanceCompanyContextEndpointFilter` and the HR equivalents are all the same shape.

A module-enablement gate qualifies on the same test: it names no module and knows no business
concept. **What it gates is `OD-SUB-0005`; that it is one shared convention rather than a per-module
check is settled here** (`REQ-SUB-0012`), because seventeen route groups each remembering to call a
guard is the failure mode `FP-006P` already produced once with the permission catalog.

### `DEC-SUB-0007` — attribution follows the existing audit-column convention

`Tenant` carries `CreatedUtc`/`CreatedBy`, `ModifiedUtc`/`ModifiedBy`,
`StatusChangedUtc`/`StatusChangedBy`/`StatusChangeReasonCode`, and `RowVersion`. Commercial records
follow it. Not a new decision; recorded so it is not re-argued as one.

### `DEC-SUB-0008` — money is `ADR-027`'s `decimal(19,4)`, inherited without restatement

If `OD-SUB-0015` rules pricing in, amounts use the product's money representation. `ADR-027` is
already activated by `OD-POS-004` and inherited unchanged by GL, Payroll and Attendance. **A
commercial package proposing its own precision would be re-deciding a settled cross-module rule.**

### `DEC-SUB-0009` — no cross-database foreign key, in either direction

`ADR-017` counts the existing cross-boundary foreign keys and treats removing them as a benefit of
the Platform/Tenant split (`:168`, `:450`). Commercial records live Platform-side (`DEC-SUB-0003`), so:

- a commercial row referencing `TenantId` references a **Platform** row and may use a real FK;
- **nothing in a Tenant ERP database may carry a foreign key to a commercial row**, and nothing
  commercial may FK into a tenant database. Cross-boundary references are stored as values.

### `DEC-SUB-0010` — administration is platform-plane, and the two planes stay mechanically separate

`ADR-005:248` lists "Subscription management" among platform-administrator capabilities.
`ADR-015` (status **Accepted**) establishes the dedicated platform authorization plane, and
`PermissionEndpointConventions:27`–`:29` keeps `RequirePermission` and `RequirePlatformPermission` as
**two methods** precisely so "a caller cannot choose the wrong plane by passing a flag".

Subscription administration therefore uses `RequirePlatformPermission`. A tenant-plane permission
that could edit a subscription would defeat `REQ-SUB-0004` no matter how it were named.

*Who inside the platform plane may administer, and whether a tenant gets any self-service at all, is
`OD-SUB-0013`. That it is not a tenant-plane permission is settled here.*

### `DEC-SUB-0011` — commercial records are absent from Shared→Dedicated cutover, and that is correct

`TenantCutoverCopyPlan.Build` selects entities by reflection —
`typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType)` (`TenantCutoverCopyPlan.cs:31`) — and
copies `TenantId`-scoped rows between tenant databases.

Commercial records are **not** `ITenantOwnedEntity` and are **not** in the tenant model, so they are
absent from cutover automatically. **For every module so far that absence has been a defect**
(`DEC-ATT-0007`: "a tenant-owned type that does not carry the interface is silently absent from
cutover"). Here it is the intended outcome, and it is written down because the next person to audit
the cutover manifest against the entity list will otherwise file it as the same bug.

### `DEC-SUB-0012` — the existing subscription material is not authority, and is not inherited

`Tenant-Management.md` disclaims its own subscription content at line 13 ("deferred and
non-authoritative until covered by an approved feature package"), and `Authentication.md` carries
status `Draft`.

Neither is treated as a specification. `TBL-PLT-Subscription` (`Tenant-Management.md:168`) is **a name
in a deferred draft**, and this package does not inherit its implied single-table shape;
`Requirement-Numbering.md`'s own *Database Tables* section does not list it. The one clause worth
promoting — "Expired subscriptions cannot login" — is carried as `REQ-SUB-0018`, marked
**NON-AUTHORITATIVE** and made conditional on `OD-SUB-0009`.

This is the `FP-013` move applied again: the Glossary's "Leave Request" was an *illustration* of the
word Workflow and FP-013 refused to read it as a requirement. Same discipline, larger surface.

---

# Part 2 — `OD-SUB` (owner decision required; each blocks the build)

### `OD-SUB-0001` — **SCOPE: enablement, the commercial record, or the whole plane?** ← decide this first

**Everything else is conditional on this answer.**

| | **E — enablement only** | **C — commercial only** | **E + C — the whole plane** |
|---|---|---|---|
| Answers | *which modules may this tenant reach* | *what has this tenant agreed to pay* | both |
| Satisfies `BR-PLT-0008` | **yes** | no | yes |
| Satisfies `CON-0001` | **partially** — "subscription-based" is represented as entitlement, with no commerce behind it | partially — commerce exists but is unenforced | yes |
| Needs pricing / currency / tax ruled | **no** | yes | yes |
| Needs invoicing authority ruled | **no** | yes | yes |
| Requirements in force | 18–19 | 18 | 28 |
| Touches shipped code | the Host and every route group | almost nothing | both |

**They are not three sizes of one thing.** Enablement is an *authorization* concern — it belongs
beside `ADR-015` and the permission catalog, and it is buildable today because every input it needs
already exists. Commerce is a *financial* concern needing pricing, currency, tax, invoicing authority
and payment capture, none of which anyone has ruled on, and several of which are regulated.

**This package's opinion, offered as an opinion:** **E first, C as a separate later package.** Two
reasons, both evidential rather than aesthetic. First, `BR-PLT-0008` is the only *shall* about
behaviour in the entire authority, and it is currently violated by all seventeen mounted route
groups; the violation grows by one module each release, and the retrofit is never cheaper than now.
Second, E needs no ruling this package cannot obtain — it inherits residency from `ADR-017`, the
outage constraint from `ADR-021`, the token constraint from `FP-002` and the seam from
`PermissionEndpointConventions`. C is blocked on five owner decisions with no defaults, and drafting
it now would produce exactly the kind of authoritative-looking guesswork this package exists to
correct.

**The counter-argument, stated fairly:** `CON-0001` says *subscription-based SaaS*, not *feature
flags*. Shipping E alone yields a product that gates modules but cannot sell them, and if the near-term
commercial need is real, E-then-C means touching the same tenant record twice.

**Ruling required. Every `OD` below is annotated with the scopes it applies to.**

---

### `OD-SUB-0002` — **the identifier space: a new `REQ-SUB`, or an extension of `REQ-PLT`?** *(all scopes)*

`Requirement-Numbering.md` lists **ten** functional prefixes — PLT, HR, GL, INV, CRM, PRJ, PAY, ATT,
PRC, MFG — and **`SUB` is not among them**. `REQ-SUB-####` is used throughout this package as a working
label only. It is an edit to a governing document either way, and therefore an owner decision rather
than a drafting convenience. **This package deliberately does not settle it and states both cases at
equal strength.**

**Case for a new `REQ-SUB` prefix**

- **Every existing prefix is per-module**, and the commercial plane is a distinct subsystem with its
  own data, its own authorization plane and its own lifecycle. `ATT` was created on exactly this
  reasoning nine days ago (`OD-ATT-0002`).
- **`REQ-PLT` is already large and heterogeneous** — 37 requirements running to `REQ-PLT-0067`,
  covering tenancy, identity, authentication, localization, companies and branches. Adding a
  commercial subsystem makes a crowded space harder to navigate, not easier.
- **The commercial plane may not stay one module.** If `OD-SUB-0001` rules `E + C`, billing, metering
  and entitlement are plausibly separable later; a dedicated space can split without renumbering
  Platform.
- **Traceability reads better.** `REQ-SUB-0011 → BR-PLT-0008` is legible as a cross-subsystem trace.
  A Platform-numbered requirement pointing at a Platform-numbered rule reads as an internal
  cross-reference and hides that a distinct subsystem is answering it. *(No example identifier is
  written here on purpose: minting one in the next free `REQ-PLT` slot would create a citation to a
  requirement that does not exist, which is exactly the orphan class `trace-check.py` reports.)*

**Case for extending `REQ-PLT`**

- **The authority is already Platform-numbered.** `BR-PLT-0008` *is* the rule this package implements.
  Requirements answering a `BR-PLT` rule under a non-`PLT` prefix split one concern across two spaces.
- **The data is Platform-database data** by `ADR-017:475`, administered on the Platform plane by
  `ADR-005:248`, on a Platform surface by `ADR-021:207`. By residency, authority and plane it is
  Platform.
- **`Tenant-Management.md` already places it there** — `TBL-PLT-Subscription`, in the `PLT` table space.
- **A prefix is permanent.** `Requirement-Numbering.md` states identifiers "never change" and are
  "never reused". A prefix created for a subsystem that turns out to be four Platform requirements is
  a permanent artefact of a wrong guess, and the `INV`/`CRM`/`PRJ`/`PRC`/`MFG` prefixes are already
  reserved for modules that do not exist.
- **`ATT` is a weaker precedent than it looks.** Attendance is a business module with its own
  aggregates, routes and permission namespace. The commercial plane, under scope `E`, might be one
  record and one endpoint filter.

**Two sub-questions ride with it, and need answering in the same ruling:**

1. **The business-rule prefix.** `Requirement-Numbering.md` lists four — PLT, HR, GL, ATT. Does a
   `BR-SUB` space get created, or do commercial rules join `BR-PLT` beside `BR-PLT-0008`? Answering
   this differently from the requirement prefix is *coherent* — the rule genuinely is Platform's —
   but it should be deliberate rather than incidental.
2. **The catalog file.** `Requirement-Catalog/` holds `Platform.md`, `HR.md`, `GL.md`, `PAY.md`,
   `ATT.md`. A new prefix means a new `SUB.md` and a `Traceability-Matrix.md` entry; an extension means
   a new section inside `Platform.md`.

**This package's opinion is deliberately withheld.** The two cases are close, they turn on how large
the commercial plane will be, and that is exactly what `OD-SUB-0001` decides. **Rule `OD-SUB-0001`
first; this decision is much easier afterwards.** What must not happen is the prefix being settled by
whichever label a drafter typed — which is why it is written as a decision rather than assumed by
this file's own use of `REQ-SUB`.

**Whichever way it goes, `Requirement-Numbering.md` is edited at ratification, not now** — the FP-013
precedent, where `REQ-ATT` was created by the ratification and not by the analysis.

---

### `OD-SUB-0003` — **does the enablement gate ship before the next module?** *(scope E, E+C)*

| Option | Consequence |
|---|---|
| **Gate first** | The next module is the first ever built behind the gate. Seventeen existing route groups are retrofitted once, against a known inventory. Delays whatever the next module is. |
| **Gate after the next module** | The retrofit grows by that module's route groups. Each subsequent module compounds it. `BR-PLT-0008` stays violated meanwhile. |
| **Gate never; amend `CON-0001` by ADR** | Legitimate and explicitly allowed — `Constraints.md` says constraints "shall not be violated **without an approved ADR**". Requires an owner ruling that the product is not subscription-gated, and `BR-PLT-0008` is then withdrawn rather than left unmet. |

**The third option is a real option, not a straw man.** The state that must not continue is the
current one: a mandatory constraint that is documented, unimplemented, and unacknowledged.

---

### `OD-SUB-0004` — **where does the per-tenant assignment live, and is entitlement projected?** *(all scopes)*

`DEC-SUB-0003` settles the plan **catalog**: Platform database. What `ADR-017` does not say is where
the **assignment** — this tenant, this plan, from this date, with these overrides — lives, nor whether
the resolved entitlement set is projected anywhere for read speed.

| Option | Consequence |
|---|---|
| **Assignment in the Platform database, resolved live per request** | Simplest and satisfies `DEC-SUB-0004` outright. Adds a Platform-database read to **every** gated request; needs a caching answer, and cache invalidation becomes the correctness-critical part of `REQ-SUB-0009`. |
| **Assignment in the Platform database, projected read-only into each tenant database** | Fast local reads. **Contradicts `DEC-SUB-0004`** — a projection in the tenant DB is unreadable during a customer-managed outage — and creates a second copy that can go stale. |
| **Assignment held in memory, refreshed on a schedule** | No per-request read. Makes `REQ-SUB-0009` false by construction: a disable takes effect only at the next refresh, so a revoked tenant keeps its access for that window. |

The first is the only option that does not contradict something already settled, **but its caching
strategy is a genuine decision with a correctness consequence, and this package will not choose it
silently.**

---

### `OD-SUB-0005` — **what, exactly, is a "module"?** *(scope E, E+C)*

`BR-PLT-0008` says "modules" without defining the word, and `Glossary.md` has no entry for it.
`REQ-SUB-0007`, `REQ-SUB-0011` and `REQ-SUB-0015` all depend on the answer.

| Candidate unit | What it would gate | Consequence |
|---|---|---|
| **The assembly / `Add*Module()` registration** | HR, GL, Payroll, Attendance, Platform — five | Matches how the Host composes today and is trivially enumerable. Coarse: HR is Employee + Department + Position + Import/Export, all or nothing. |
| **The route group** | seventeen `Map*Endpoints()` calls | Matches the transport surface `BR-PLT-0008` names. Splits HR into seven sellable pieces, which may be finer than anyone wants to price. |
| **The permission namespace** | the `IPermissionCatalogContributor` catalogs | Gates both surfaces (`REQ-SUB-0011` and `REQ-SUB-0015`) with one concept. Conflates *what you bought* with *what you may do* — two ideas that should stay separable. |
| **A declared product feature, mapped to routes** | an owner-chosen list | Sellable units decided commercially, not by code shape. Needs a mapping maintained by hand, and a new route with no mapping is either open by default or closed by default — itself a further decision. |

**No default is offered.** The unit determines what can be sold, which is a commercial question, not
an architectural one — and each candidate is defensible on its own terms.

---

### `OD-SUB-0006` — **what does a request to a disabled module receive?** *(scope E, E+C)*

`BR-PLT-0008` says a disabled module "shall not **appear** in … APIs". *Appear* is a disclosure word,
and it admits two readings.

| Option | Consequence |
|---|---|
| **`404 Not Found`** | Strongest reading of "shall not appear" — the route is indistinguishable from one that does not exist, so the tenant cannot enumerate unpurchased modules. Harder to support: an operator cannot tell a disabled module from a bug from a typo'd URL. |
| **`403 Forbidden` with a problem type naming the module** | Diagnosable, and honest to the caller. **Discloses the module's existence**, which is a weaker reading of the clause and lets any tenant enumerate the product's full module list. |
| **`402 Payment Required`** | Semantically precise and rare enough to be unambiguous in logs. Discloses more than `403` — it names *why* — and couples the transport response to the commercial plane, which under scope `E` does not exist yet. |

Whatever is ruled, it must apply **uniformly** (`REQ-SUB-0012`) and must reach
`ProblemDetails` through the established conventions rather than as a bare status.

---

### `OD-SUB-0007` — **what does "shall not appear in menus" bind to?** *(scope E, E+C)*

**This repository contains no UI.** There is no menu, no navigation model and no client. The clause
is half-unenforceable in the codebase it governs, and pretending otherwise would produce an
untestable requirement.

| Option | Consequence |
|---|---|
| **A capability endpoint** — the API returns the enabled module set; the client renders from it | Makes the clause testable server-side today (`REQ-SUB-0014`), and puts one authority behind both surfaces. Requires deciding the shape of that response before any client exists to consume it. |
| **Treat "menus" as out of scope for the backend; record the obligation against the future UI package** | Honest about where the surface lives. Leaves a `shall` clause with **no owner** and no test — the exact failure mode that produced this package. |
| **Both — endpoint now, UI obligation recorded as a carried requirement** | Costs a little more; nothing is dropped. |

---

### `OD-SUB-0008` — **one subscription, or a history?** *(all scopes)*

`REQ-SUB-0001` says one is in force at a time. Whether superseded subscriptions are retained is
separate.

| Option | Consequence |
|---|---|
| **Current state only — one mutable row per tenant** | Simplest. **Loses the answer to "what was this tenant entitled to on the day that happened"**, which is a question audit and disputes both ask. |
| **Append-only history, current derived** | Answers it permanently. The repository already has the mechanism — `IAppendOnlyEntity`, refused unconditionally for `Modified` or `Deleted` — and the `Employee`/`Department` dated-history precedent. Costs a second type and a resolution rule. |

Note that ruling *history* has a design consequence the `DEC-ATT-0009` precedent makes explicit: a
record that must be correctable before it is final and immutable after is **two types, not one**.

---

### `OD-SUB-0009` — **does a term exist, and is `Authentication.md`'s expiry rule made binding?** *(all scopes)*

`Authentication.md:123` — "Expired subscriptions cannot login" — is in a `Draft` document, describes a
state the product cannot represent, and is carried as `REQ-SUB-0018` **conditional on this ruling**.

| Option | Consequence |
|---|---|
| **No term — a subscription is open-ended until changed** | Nothing expires; `REQ-SUB-0018` is struck and the draft clause is deleted rather than left dangling. Cannot express a fixed-term contract or a trial (see `OD-SUB-0014`). |
| **Dated term, expiry blocks login for the whole tenant** | Matches the draft. **Blunt** — an expired tenant loses everything including its own data access, and the failure lands on every user at once with no warning path. |
| **Dated term, expiry disables modules but preserves login and read access** | Softer and probably more sellable; a tenant can still log in, see its data and renew. **No authored document says this**, so it is a new product behaviour, not an inherited one. |

If the ruling is anything other than the first, `Authentication.md` needs the corresponding edit —
a separate task, since that file is outside this package.

---

### `OD-SUB-0010` — **how does subscription state relate to `TenantStatus`?** *(all scopes)*

`TenantStatus` is `Provisioning`/`Active`/`Suspended`/`Archived`, and none of the
`TenantStatusChangeReason` values is commercial. `FP-002` already enforces "live FP-003 status …
permits only Active" on every tenant-scoped business request, so a second live check would be a
second dimension on the same path.

| Option | Consequence |
|---|---|
| **Orthogonal** — tenant lifecycle and commercial state are independent, both checked | Cleanest separation: an unpaid tenant is `Active` but unentitled; a suspended tenant is blocked regardless of payment. Two checks on every request. |
| **Commercial state drives `TenantStatus`** — non-payment suspends the tenant | One check, one concept, reuses the existing enforcement path unchanged. **Overloads a lifecycle enum with a commercial meaning**, and `TenantStatusChangeReason` would need commercial members — a change to shipped Platform code and its guards. |

---

### `OD-SUB-0011` — **may a single tenant be granted more or less than its plan?** *(scope E, E+C)*

| Option | Consequence |
|---|---|
| **No overrides — plan is the sole authority** | Entitlement is a pure function of the plan; trivially auditable. Every bespoke arrangement needs a bespoke plan, and plan proliferation is the usual result. |
| **Per-tenant overrides, additive and subtractive** | Handles pilots, goodwill grants and staged rollouts without inventing plans. Entitlement becomes plan ± overrides, which must be resolvable, auditable and displayable — and is the first place a support question will land. |

---

### `OD-SUB-0012` — **what happens to data and permissions when a module is disabled?** *(scope E, E+C)*

`REQ-SUB-0016` states the data survives. The reachable consequences still need ruling.

| Question | Options and consequence |
|---|---|
| **Existing role grants for the module's permissions** | *Left in place but inert* — re-enabling restores the previous state exactly; a role listing shows permissions that do nothing. *Revoked on disable* — the catalog stays truthful; re-enabling silently loses the grants, and there is no record of what they were. |
| **Cross-module contracts** | `Payroll` consumes `IAttendanceSummary`. **If Attendance is disabled and Payroll is not, payroll approval fails at request time.** The plan model must either forbid that combination or define the degraded behaviour — and this is not hypothetical; it is the one dependency that ships today. |
| **Background work and scheduled jobs** | Whether disabling stops them, or only the routes. |

**The cross-module row is the sharp one**, and it will multiply: every future contract adds a pair
whose enablement states can disagree.

---

### `OD-SUB-0013` — **who administers a subscription?** *(all scopes)*

`DEC-SUB-0010` settles that it is the platform plane. Within that:

| Option | Consequence |
|---|---|
| **Platform administrators only** | Matches `ADR-005:248` exactly. Every plan change is a support request. |
| **Platform administrators, plus a tenant-visible read-only view** | `REQ-SUB-0021` already proposes the read. Needs the disclosure boundary drawn: modules yes, price and payment state no. |
| **Tenant self-service upgrade** | A different product. Requires payment capture (`OD-SUB-0016`), a public plan catalog, and a tenant-plane write path that `REQ-SUB-0004` currently forbids — so it would have to amend that requirement rather than extend it. |

---

### `OD-SUB-0014` — **does a trial exist?** *(all scopes)*

The word appears nowhere in any authored document; the only "trial" in the repository is GL's trial
balance.

| Option | Consequence |
|---|---|
| **No trial** | Nothing to build. If sales later need one, it is a change to whatever `OD-SUB-0008` and `OD-SUB-0009` produce. |
| **A trial is an ordinary plan with a term** | No new concept — it falls out of `OD-SUB-0009`'s term plus a plan. Cannot express "trial" as a distinguishable state for reporting. |
| **A trial is a subscription state** | Reportable and can drive conversion behaviour. Adds a state machine to a record that may otherwise not need one. |

---

### `OD-SUB-0015` — **pricing, currency and proration** *(scope C, E+C)*

**No authored document names a price, a currency, a billing period or a tax position.** Every part of
this is unruled, and this package proposes nothing.

| Question | Consequence of the answer |
|---|---|
| **Is a price held at all?** | If no, plans are pure entitlement bundles and the commercial plane reduces to `E` plus a term. |
| **Single currency, or many?** | `ADR-027 d2`'s currency-projection rule already exists for the ERP's money; whether the commercial plane inherits it or fixes one vendor currency is separate, and `OD-GL-0002` ruled single-currency for V1 GL — a *precedent*, not a ruling here. |
| **Tax** | Jurisdictional, and `DEC-PAY-0016` is the standing precedent for refusing to encode a jurisdiction the product has not named. **The same refusal is the safe answer here** and is offered as this package's opinion. |
| **Proration on mid-term change** | Note `OD-PAY-0007` ruled calendar-day proration for *payroll*. Different subject, no precedent. |

---

### `OD-SUB-0016` — **invoicing authority and payment capture** *(scope C, E+C)*

| Option | Consequence |
|---|---|
| **Neither — the vendor invoices outside the product** | Nothing to build; the subscription record is entitlement plus a term. `CON-0001` is satisfied in substance without the product handling money. |
| **Invoices generated here, payment captured externally** | The product owns a document with legal weight — numbering, immutability, credit notes, retention. Recorded **outside** the tenant's GL (`DEC-SUB-0001`), which means a second ledger with none of GL's machinery. |
| **Both, with an external payment provider** | Card data, PCI scope, webhooks, retries, refunds, dunning. A subsystem comparable in size to a business module, and the only option that adds a regulated external dependency. |

---

### `OD-SUB-0017` — **is anything metered?** *(scope C, E+C)*

| Option | Consequence |
|---|---|
| **Nothing metered — flat per-tenant pricing by plan** | No counting, no reconciliation, no disputes about counts. Cannot price by size, so large and small tenants pay alike. |
| **Seats metered** | Needs a definition of an active seat and a counting instant. `TenantUser` exists, so the input is available. Introduces enforcement questions this package has not raised — does exceeding the count block the next login, or only bill more? |
| **Usage metered** — storage, transactions, API calls | Every counted quantity becomes a number the vendor must defend to a customer. Requires a durable, auditable meter, which is its own subsystem. |

---

## What this register deliberately does not contain

**No `OD` about the enablement gate's implementation shape.** `DEC-SUB-0006` settles that it is one
shared endpoint convention; whether it is a filter, a policy or middleware is an engineering decision
for the build task, not an owner decision.

**No `OD` about ADR numbering.** This package needs an ADR eventually, and `ADR-028` is currently the
subject of a dangling reference from FP-010. Claiming a number is not this package's to do.

**No `OD` reopening `ADR-017`, `ADR-021`, `ADR-027` or `FP-002`.** Each constrains this package and
each is recorded in Part 1. If a ruling here would contradict one of them, that is an ADR amendment
and it should be raised as such rather than smuggled in as a package decision.

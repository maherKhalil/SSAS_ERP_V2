# FP-014 — Ratified decisions

**All seventeen `OD-SUB` rulings, closed 2026-08-25 across three rounds. All twelve `DEC-SUB` ratified
as drafted.** [`decisions-open.md`](decisions-open.md) remains the record of what was asked and why;
this file is what was answered. **Where the two disagree, this file wins.**

> **AMENDED 2026-08-25 — `DEC-L-024`.** `ADR-021` is `Proposed` and, at its own `:37`, moves to
> `Accepted` only when a customer-hosted deployment is actually contracted. None is, so it does not
> bind. **Every citation of it in this package has been re-pointed to `ADR-017`, which is `Accepted`.**
> **No requirement, rule or ruling changed — only the authority under them.** Recorded here rather
> than applied quietly, because a ratified document changing its basis is an act with a record.
> Open concern 3 below is closed by this amendment.

The rulings were taken in `.claude/handoff/notes/2026-08-25-fp-014-rulings.md` and are moved here
because a ruling recorded outside the package is one the package cannot cite and `trace-check` cannot
see. That note remains the contemporaneous record; this file is the package's own.

---

## Who ruled what, and why the distinction is recorded

**Six by the owner, seven by the architect, four by the owner in a second round.** Every row below
carries its source, because **reopening an owner ruling and reopening an architect ruling are
different acts**. An owner ruling is a commercial decision about what the product sells and how it
charges; an architect ruling is a structural decision that follows from what is already built. A
future reader proposing a change needs to know which door to knock on.

The architect's rulings are marked as reversible by the owner. None was a guess and none was taken to
avoid asking.

---

## The rulings

| # | Ruled by | Ruling |
|---|---|---|
| **`OD-SUB-0001`** scope | **Owner** | **E + C — the whole plane.** Enablement and the commercial record together. All 28 `REQ-SUB` in force; no `OD-SUB` collapses. (`DEC-L-004`, `DEC-L-006`.) |
| **`OD-SUB-0002`** identifier space | Architect | **A new `REQ-SUB` prefix**, added to `Requirement-Numbering.md` **at ratification** — the `REQ-ATT` precedent from FP-013. The ruled scope is E+C, and invoicing, payment capture, metering and pricing are not "Platform" in the sense `REQ-PLT` uses it; a 28-requirement space with its own lifecycle would swamp it. |
| **`OD-SUB-0003`** sequencing | Architect | **Yes — the enablement gate ships before the next module.** The violation of `BR-PLT-0008` grows monotonically and the retrofit is never cheaper later. |
| **`OD-SUB-0004`** assignment residency | Architect | **Assignment lives in the Platform database, resolved per request, behind a cache invalidated on subscription change — never a TTL refresh.** Cache invalidation is part of `REQ-SUB-0009`'s test surface, not an implementation detail. A tenant-database projection contradicts `DEC-SUB-0004`; a scheduled refresh makes `REQ-SUB-0009` false by construction, and a TTL cache is that same failure wearing a different hat. **Authority amended by `DEC-L-024`:** the outage constraint this ruling relies on to exclude a tenant-database projection now derives from `ADR-017` § Platform database boundary and § No automatic fallback (`:169`, `:376`–`:378`) rather than `ADR-021` § 10 Outage behaviour. **The ruling itself is unchanged.** |
| **`OD-SUB-0005`** what a module is | Architect | **The unit that already carries exactly one `IPermissionCatalogContributor` and one `Add*Module()` registration** — today HR, Finance/GL, Payroll, Attendance. **Not** the route group, **not** the assembly. Each declares a stable module key. Verified against the code: four contributors, seventeen route groups, because HR alone mounts seven. |
| **`OD-SUB-0006`** disabled-module response | **Owner (round 2)** | **403 Forbidden.** The route exists; this tenant may not reach it. Chosen over 404 **with the disclosure cost accepted knowingly** — a tenant can enumerate the product surface by probing, and in exchange support can answer "why can't I reach payroll" from the response rather than from server logs. |
| **`OD-SUB-0007`** what "menus" binds to | Architect | **`REQ-SUB-0014`'s server-provided enabled-module set, and nothing client-side.** The product's obligation is to publish the set truthfully; rendering is the client's. The product cannot enforce a menu it does not draw, and a UI assertion would not make `BR-PLT-0008` testable. |
| **`OD-SUB-0008`** one subscription or a history | **Owner (round 2)** | **Append-only history, exactly one in force at a time.** A plan change closes the current record and opens the next. `REQ-SUB-0001` becomes a queryable invariant rather than a mutation, and prorated multi-currency billing can reconstruct what was in force on any date. Matches the department and position history convention `Employee` already carries. |
| **`OD-SUB-0009`** term and expiry | **Owner** | **A term exists**, with a start and an end or an explicit perpetual marker. **AMENDED 2026-08-26 by `DEC-L-033`: expiry GATES MODULES and does not block login.** An expired tenant authenticates, reaches the platform plane including the subscription surface, and reaches no gated module. **The original ruling — "expiry blocks login for the tenant" — is preserved below with the reason it changed.** |
| **`OD-SUB-0010`** relation to `TenantStatus` | **Owner (round 2)** | **Orthogonal — independent dimensions, both evaluated on every request.** Expiry never touches `TenantStatus`, so a billing lapse is never confused with an administrative suspension and the platform keeps the ability to suspend a paying tenant for abuse or legal cause. **No commercial reason is added to `TenantStatusChangeReason`.** **Mechanism corrected 2026-08-26 by `DEC-L-033`:** this row read *"both checked at login"*, which was true only while expiry blocked login. It no longer does — `TenantStatus` is checked at authentication and commercial state at the enablement gate. **The orthogonality is untouched; only where each is evaluated changed.** |
| **`OD-SUB-0011`** grants above or below plan | **Owner (round 2)** | **Additive grants only.** A tenant may be granted a module or a raised cap above its plan, **never below**. Covers pilots, negotiated deals and goodwill grants without letting an override silently remove something the customer is paying for. Entitlement resolves as **plan ∪ grants** — one direction to model, one to test. |
| **`OD-SUB-0012`** data and permissions on disable | Architect | **Data is retained untouched and permissions become ineffective.** A disabled module's permissions are neither grantable nor effective, so a stale role assignment cannot reach it. `REQ-SUB-0016` already binds retention. Deleting tenant data on a commercial event would make a billing lapse destructive and irreversible — unacceptable for an ERP of record. |
| **`OD-SUB-0013`** who administers | Architect | **Platform plane only. No tenant-plane actor may administer a subscription, whatever permissions it holds.** Not genuinely open: `ADR-005` § Platform Administration (`:248`) lists subscription management as a platform-administrator capability and `ADR-015` makes the platform plane a separate authorization plane. **Recorded rather than re-decided.** |
| **`OD-SUB-0014`** trials | **Owner** | **No trial concept.** A trial, if ever needed, is a plan with a short term — not a state, not a flag. ~~**`REQ-SUB-0020` falls away.**~~ **USED, not overturned, by `DEC-L-034` (2026-08-26):** a tenant without a subscription gets an all-module plan with a 14-day term. That is this ruling being applied — no state and no flag was introduced. **`REQ-SUB-0020` therefore does NOT fall away** — struck above rather than deleted, because the ruling that made it conditional is the same one that gave it content. It is unconditional and holds `AC-SUB-0033` plus `AC-SUB-0052`–`AC-SUB-0054`. |
| **`OD-SUB-0015`** pricing, currency, proration | **Owner** | **Multi-currency, prorated.** A plan carries a price per supported currency; a mid-term change is adjusted for the unused portion. Money is `ADR-027` `decimal(19,4)`, **inherited, not re-decided**. |
| **`OD-SUB-0016`** invoicing and payment capture | **Owner** | **The product issues invoices and captures payment itself.** Both, in-product. **The widest blast radius in the package** — see below. |
| **`OD-SUB-0017`** metering | **Owner** | **Seats plus limits.** Seats are metered for billing; the plan **additionally** sets hard caps enforced alongside module enablement. The residue — what happens when a cap is exceeded — was ruled by the architect; see below. |

---

## `OD-SUB-0016` — the ruling stands, and `ADR-029` rules *how*, not *whether*

**Both records stand and neither softens the other.** `OD-SUB-0016` is the commercial commitment:
the product issues invoices and captures payment, in-product, rather than delegating to a provider's
billing system. `ADR-029` then rules the **mechanism**: capture is **tokenized**, and no cardholder
data ever enters SSAS (`DEC-L-018`).

Recorded as a pair because the two are easy to read as being in tension and are not. The product
owns invoicing, the payment flow, its state and its reconciliation. What it does not own is the PAN,
CVV, cardholder name and expiry — those are captured by the provider's hosted fields or redirect and
never traverse the monolith. **"We capture payment" and "we never see a card number" are both true**,
and `ADR-029` exists so that a future reader does not resolve the apparent conflict by weakening
either one.

This was flagged at ruling time as reaching past FP-014, and it does. Recorded here so the model
documents carry it and the ADR is written **before** code rather than after:

- **PCI-DSS scope** — resolved by `ADR-029`. Tokenized capture keeps the cardholder-data environment
  out of the monolith, so `ADR-001 Modular Monolith` needed no exception carved into it.
- **Tax** — multi-currency invoicing from vendor to tenant crosses jurisdictions, and which regime
  applies to each tenant is **unauthored anywhere in this repository**. Not ruled. Not scoped.
- **Dunning, refunds and reconciliation** become product surfaces with no requirement covering them
  today. `REQ-SUB-0025` and `REQ-SUB-0026` are one line each.

**None of this reverses the ruling.**

---

## `OD-SUB-0017` residue — the cap is enforced at admission, never at login

**Architect ruling (`DEC-L-009`), reversible by the owner.** The question as posed — block the next
login like a licence, or bill an overage and lock nobody out — is a false pair, and both answers are
wrong for this product.

- **The enforcement point is the grant.** Creating or activating a `TenantUser` beyond the tenant's
  seat cap is **refused at that moment**, with an error naming the cap, the current count and the
  plan. The person who caused the excess is the person told, immediately, and they can act on it.
  (`AC-SUB-0049`.)
- **Login is never refused for a seat cap** — no seat check runs on the authentication path at all.
  Blocking a login enforces against an arbitrary user who did nothing, at the moment they sit down to
  work, in an ERP of record. (`AC-SUB-0050`.)
- **An excess arriving by another route is billed, never enforced retroactively.** Additive-only
  grants (`OD-SUB-0011`) mean a grant cannot lower a cap, so the remaining route is a plan change.
  A downgrade that puts a tenant over its new cap deactivates nobody. (`AC-SUB-0051`.)

**No grace period, and none is needed.** A grace period softens a lapse; nothing lapses here. The
refusal is at admission and it is immediate, specific and actionable.

**This paragraph closed by calling `OD-SUB-0009`'s expiry "the only commercial event that blocks
login" and the asymmetry deliberate. `DEC-L-033` (2026-08-26) removed the asymmetry by amending that
ruling: no commercial event blocks login now.** What survives is the distinction that carried the
argument — expiry is dated, foreseeable and whole-tenant; a seat excess is incremental and lands on
an arbitrary user. That is still why one acts at the gate and the other at the grant.

### The interaction to carry into the model rather than discover later

`OD-SUB-0011` (additive grants) and `OD-SUB-0017` (seats plus limits) meet at the cap. **A grant may
raise a cap; it may never lower one.** And `OD-SUB-0008`'s append-only history means a cap in force is
a property of **the subscription record live at that moment**, not of the tenant — so a metered
overage must be judged against the record in force when the usage occurred, not against today's.

---

## The gateable surface, now that `OD-SUB-0005` is ruled

Seventeen route groups is the wrong number for scoping the enablement retrofit. They partition as:

| | Groups | Gated? |
|---|---|---|
| Host | 1 | **exempt** — `REQ-SUB-0013` |
| Platform (auth, support auth, localization, identity/access, support authority, company) | 6 | **exempt** — `REQ-SUB-0013` |
| HR | 7 | gated, **one** module key |
| Finance/GL | 1 | gated |
| Payroll | 1 | gated |
| Attendance | 1 | gated |

**Ten route groups across four module keys.** The retrofit is materially smaller than the raw count
suggested, and the exempt seven are exactly the surface that must stay reachable so a disabled tenant
can still authenticate and be re-enabled.

---

## `Platform.` permission prefix — kept, and guarded

**Architect ruling.** Keep the prefix; make the plane mechanical with an architecture guard; and guard
the **existing** names, not only FP-014's new six.

A second naming scheme for seven permissions is worse than one ambiguous scheme, and renaming
permissions already assigned to real roles is a migration that buys nothing functional. But guarding
only `Platform.Plans.*`, `Platform.Subscriptions.*`, `Platform.EntitlementGrants.*` and
`Platform.Invoices.*` would leave the ambiguity that **already exists** unguarded — `Platform.Support.*`
is platform-plane today and nothing asserts it. **The guard covers the whole platform-plane set.**
(`DEC-L-010`.)

---

## What the rulings changed in the package

| Package statement | Ruled outcome |
|---|---|
| `OD-SUB-0001` scope columns `E` / `C` / `*` on every requirement | **All 28 `REQ-SUB` in force** — the E+C column. Nothing is struck for scope. |
| `REQ-SUB-0018` — *"CONDITIONAL ON `OD-SUB-0009`"*, authority **NON-AUTHORITATIVE** | **Unconditional and binding.** `OD-SUB-0009` made `Authentication.md`'s rule real; that document now points here rather than restating it (T-012). |
| `REQ-SUB-0020` — trial representable, *"CONDITIONAL ON `OD-SUB-0014`"* | **Ruled 2026-08-25: falls away. No trial concept exists. A trial is a plan with a short term.** **SUPERSEDED 2026-08-26 by `DEC-L-034`: unconditional and in force.** The ruling was not overturned — it was *used*. A trial is still a plan with a short term and still not a state or a flag; what changed is that one is now issued, so the requirement has content to carry. It holds `AC-SUB-0033` (the absence) and `AC-SUB-0052`–`AC-SUB-0054` (the issuance). See [the `DEC-L-034` section](#the-trial-is-a-plan-with-a-term-and-that-is-od-sub-0014-being-used-dec-l-034) below. |
| `REQ-SUB-0019` — the subscription-state / `TenantStatus` relationship | **In force, and the relationship is orthogonality.** No commercial reason joins `TenantStatusChangeReason`. |
| `REQ-SUB-0025`, `REQ-SUB-0026` — *"CONDITIONAL ON `OD-SUB-0016`"* | **Both in force.** Invoicing in-product; capture in-product **and tokenized** per `ADR-029`. |
| `REQ-SUB-0027` — *"CONDITIONAL ON `OD-SUB-0017`"* | **In force**, and it acquired a second obligation. See the open concerns below. |
| `REQ-SUB-0028` — *"CONDITIONAL ON `OD-SUB-0015`"* | **In force, prorated.** Note `OD-PAY-0007` ruled proration for *payroll* on calendar days — a different subject that sets no precedent here. |
| `DEC-SUB-0001` … `DEC-SUB-0012` | **Ratified as drafted.** Settled engineering decisions, recorded rather than reopened; `decisions-open.md` Part 1 carries their reasoning unchanged. |

---

# Open at ratification — carried deliberately, not resolved

**Three items are open at the moment of ratification.** They are recorded here rather than settled,
because none of them is this package's to settle. Ratification is not a claim that nothing is left; it
is a claim that the seventeen owner decisions are answered and the build may proceed.

## 1. `DEC-L-009` rules the seat cap without defining a seat — **architect's**

The ruling enforces a cap on seats and never says what a seat *is*. `AC-SUB-0049` reads it as a
**`TenantUser`**, because that is the only reading the repository makes available today — it is the
entity whose creation and activation the criterion can attach to.

That reading is load-bearing and it is unratified. It decides, silently, whether a deactivated user
occupies a seat, whether a user with memberships in two tenants consumes two, and whether a
platform-support principal consumes any. **Not resolved here.**

## 2. `REQ-SUB-0027` now carries two obligations with different enforcement semantics — **architect's**

As written it is one row about **metering**: *"usage that affects price — seats, tenants, storage,
transaction volume — is metered, and the product names exactly what is counted."* Metering measures.

`OD-SUB-0017` ruled **seats plus limits**, and the limits half **refuses**. The three criteria citing
`REQ-SUB-0027` are consequently not the same kind of thing: `AC-SUB-0049` is a refusal at admission,
`AC-SUB-0050` is a guarantee that no refusal happens on another path, and `AC-SUB-0051` is a billing
outcome with no refusal at all.

**Measuring and refusing are different obligations and they may want separate requirements.** Flagged
rather than split, because renumbering a requirement space at ratification is exactly the act that
makes a traceability matrix start lying.

## 3. ~~FP-014 rests on `ADR-021`, which is still `Proposed`~~ — **CLOSED by `DEC-L-024`, 2026-08-25**

> **Everything from here to the ruling below describes the position BEFORE the amendment**, kept as
> the record of what was found. `ADR-021` is still `Proposed` and still does not bind; what changed is
> that **no citation in this package rests on it any more.** The present-tense citations named below
> have all been re-pointed to `ADR-017`.

The ratification instruction asked for a `DEC-L-021` check against records still `Proposed`. It clears
for the four ADRs named — `ADR-017` and `ADR-027` moved to `Accepted` in T-011, `ADR-029` and
`ADR-030` were born `Accepted`. **It does not clear for `ADR-021`.**

`ADR-021 Customer-Managed Tenant Database Connectivity and Operations` is `Proposed`, and this package
stands in a **defined relationship** to it:

- `REQ-SUB-0005` cites `ADR-021:207` as its authority — the subscription surface must remain readable
  and administrable while the tenant's ERP database is unavailable.
- `DEC-SUB-0004` is built on the same passage, and `OD-SUB-0004`'s ruling excludes a tenant-database
  projection **because** of it.
- `decisions-open.md` states outright that `ADR-021` **constrains this package and is not reopened by
  any `OD`**.

Two further citations are incidental rather than structural: `ADR-018` (`nvarchar`, also `Proposed`)
and `ADR-020` (a diagram label, also `Proposed`).

**Ratifying this package made it authority, and an authority resting on a `Proposed` record is the
condition `DEC-L-021` names as incoherent.**

**Ruled `DEC-L-024`: `ADR-021` stays `Proposed` and the citation moves.** Moving the ADR was the
obvious repair and was never available — `ADR-021:37` states its own acceptance precondition, and
overriding a document's own terms with an argument from use is exactly what `DEC-L-020` forbids.
Every citation above now reads `ADR-017`; `REQ-SUB-0005`, `DEC-SUB-0004` and `OD-SUB-0004` are
unchanged in substance. **Every citation in this package has now been re-pointed** — the last three
sites, `data-model.md`, `README.md`'s outage paragraph and this file's own count of open concerns,
were completed on the same day. **`ADR-021` appears nowhere in FP-014 as a binding authority.**

---

## `BR-SUB-0020` — the scope was a relic of document order, and the record says so

**Amended 2026-08-25.** The rule read *"…or logged **by this package**"*. Promoted into the master
`Business-Rules.md` by T-022, that phrase had no referent: a reader who has not read FP-014 cannot tell
whether it binds the product, the module, or the sentence's own paragraph.

**What changed and what did not.** The prohibition is **identical** — it already forbade storing,
transmitting in any request or response, and logging. **Only the boundary it applies to becomes
explicit**, from "this package" to "anywhere in SSAS". The rule gained no new teeth; it stopped
pointing at nothing.

**Why the wide reading is the right one, and why this is not a guess — it is document order.**
`BR-SUB-0020` was written in T-007, commit `187445a` at 16:19. `ADR-029` was written in T-010, commit
`f03c5ff` at 20:43 — **more than four hours later** — and it rules that no cardholder datum enters
**SSAS**, not the commercial plane and not this package (`ADR-029` § Decision 1; `DEC-L-018`). The one
commit that touched this file in between, `d1b38c7` (T-013), added `BR-SUB-0021` and did not revisit
`BR-SUB-0020`.

**So nothing ever ruled the narrow scope.** The wide one was ruled after the sentence was written, and
the sentence was never looked at again. Recorded because ordering is invisible later: a future reader
seeing a package rule and an ADR disagree has no way to tell which came first, and would reasonably
assume the narrower one was deliberate.

**This amendment is `DEC-L-026`, and the number arrived after the amendment did.** It was made in
T-028 carrying no ledger identifier, because the architect had ruled it without assigning one and
minting a `DEC-L` is not the coder's act — the record said so at the time. The number was assigned
when T-029 was issued, and is recorded here rather than back-dated: **the ordering is itself the kind
of fact `DEC-L-026` was ruled on**, and a record reading as though the number had always existed would
reproduce, one level up, the exact relic this amendment corrected.

---

## `OD-SUB-0009` amended — expiry gates modules and never blocks login (`DEC-L-033`)

**Amended 2026-08-26, on the owner's ruling. The original stands below because the reason a ruling changed
is worth more than the ruling.**

### What it said

> **A term exists**, with a start and an end or an explicit perpetual marker. **Expiry blocks login for
> the tenant** — this makes `Authentication.md`'s currently-`Draft` rule binding, so `REQ-SUB-0018` ceases
> to be conditional.

### What it says now

**A term exists, unchanged. Expiry denies every gated module and never denies authentication.** An expired
tenant signs in, reaches the platform plane — its account, its users, and the subscription surface itself —
and reaches no gated module.

### Why it changed

**A lapsed customer who cannot log in cannot reach the page that would let them subscribe.** The original
ruling made expiry the one commercial event that blocked authentication, and the surface a tenant would
renew from sits behind that same authentication. The refusal foreclosed its own remedy.

### What did NOT change, stated because an amendment invites over-reading

- **The term itself.** Start, end or explicit perpetual marker, exactly as ruled.
- **`OD-SUB-0010`'s orthogonality.** Subscription state and `TenantStatus` remain independent dimensions.
  A **suspended or archived** tenant is still refused at authentication; that is administrative, not
  commercial, and the two outcomes stay distinct.
- **`REQ-SUB-0013`.** The platform plane was already ungated, which is why this amendment needs **no
  special case**: expiry acts through the same enablement gate as every other entitlement, and the
  surface an expired tenant needs is reachable because it always was.

### What it simplifies

**`DEC-L-009`'s asymmetry collapses, and the rule gets simpler rather than more complex.** That ruling
refused a seat cap at login and defended the difference by expiry being *the only commercial event that
blocks login*. **No commercial event blocks authentication at all now.** The rule is uniform: commercial
state is resolved per request at the gate, and authentication answers only to tenant lifecycle.

---

## The trial is a plan with a term, and that is `OD-SUB-0014` being used (`DEC-L-034`)

**Ruled 2026-08-26.** A tenant with no subscription record gets an **all-module plan with a 14-day term** —
seeded at cutover for the existing estate, and on tenant creation thereafter.

**No new concept was introduced, and that is the point.** `OD-SUB-0014` already ruled: *"a trial, if ever
needed, is a plan with a short term — not a state, not a flag."* This is that ruling being **used**. There
is no `Trial` status, no `IsTrial` column and no fourth lifecycle state; there is a plan, and it has a
term, and every mechanism that already handles plans and terms handles it unchanged.

**It also answers what `CON-0001` left sharp.** With no backfill and no default plan, a tenant without a
subscription row reaches no gated module — correct, and also how an entire existing estate is locked out
in one deploy. A seeded term is not a default plan smuggled back in: it is dated, it expires, and when it
does the tenant still signs in, by `DEC-L-033` above.

**Neither the seeding nor the resolver is built here.** This file records the ruling; T-040 switches the
resolver and T-041 seeds. The record changes first, deliberately (`DEC-L-027`).

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-25 | Solution Architecture Team | Ratifies FP-014. All seventeen `OD-SUB` rulings moved into the package from the 2026-08-25 ruling set, each attributed to the owner or the architect. All twelve `DEC-SUB` ratified as drafted. Three concerns carried as open at ratification: the undefined seat, `REQ-SUB-0027`'s two enforcement semantics, and this package's dependence on the still-`Proposed` `ADR-021`. |
| 1.1 | 2026-08-25 | Solution Architecture Team | **Amendment under `DEC-L-024`.** No requirement, rule or ruling changed. `ADR-021` is `Proposed` and conditions its own acceptance on a customer-hosted deployment being contracted (`:37`), so it does not bind; every citation of it in `requirements.md`, `business-rules.md` and `decisions-open.md` is re-pointed to `ADR-017:164`, `:169` and `:376`–`:378`, which carry the same conclusion in two steps rather than one. `OD-SUB-0004`'s entry records the moved authority. Open concern 3 is closed. |
| 1.2 | 2026-08-25 | Solution Architecture Team | Completes the `DEC-L-024` re-point. `data-model.md` and `README.md`'s outage paragraph now derive from `ADR-017:164`, `:169` and `:376`–`:378`; the ratification banner's count of open concerns is corrected from three to two, concern 3 having been closed. No requirement, rule or ruling changed. |
| 1.3 | 2026-08-25 | Solution Architecture Team | Amends `BR-SUB-0020`'s scope from "by this package" to "anywhere in SSAS", the boundary `ADR-029` Decision 1 actually rules. **The prohibition is unchanged**; only the boundary becomes explicit. Recorded with the commit ordering that makes the original phrasing a relic — `BR-SUB-0020` predates `ADR-029` by four hours and was never revisited. The master `Business-Rules.md` copy is updated to match, so the two do not diverge. |
| 1.4 | 2026-08-25 | Solution Architecture Team | Records that the `BR-SUB-0020` amendment is **`DEC-L-026`** — a number assigned after the amendment was made, and recorded as such rather than back-dated. Converts this file's live `ADR-017` and `ADR-029` citations to section anchors with line numbers kept beside them (`DEC-L-028`); the Revision History rows below are left as written, because they record what was cited at the time. No decision changed. |
| 1.5 | 2026-08-26 | Solution Architecture Team | **Amends `OD-SUB-0009` under `DEC-L-033`:** expiry gates modules and never blocks login; the original ruling is preserved with the reason it changed. Records `DEC-L-034` — a tenant without a subscription gets an all-module plan with a 14-day term, which is `OD-SUB-0014` being used rather than overturned. `REQ-SUB-0018`, `BR-SUB-0013`, `AC-SUB-0029`, `TS-SUB-0029` and the traceability row are rewritten; `DEC-L-009`'s asymmetry note is collapsed, because no commercial event blocks authentication any longer. No code is written from this yet — T-040 and T-041. |

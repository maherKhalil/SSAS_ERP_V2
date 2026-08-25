# FP-014 — Ratified decisions

**All seventeen `OD-SUB` rulings, closed 2026-08-25 across three rounds. All twelve `DEC-SUB` ratified
as drafted.** [`decisions-open.md`](decisions-open.md) remains the record of what was asked and why;
this file is what was answered. **Where the two disagree, this file wins.**

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
| **`OD-SUB-0004`** assignment residency | Architect | **Assignment lives in the Platform database, resolved per request, behind a cache invalidated on subscription change — never a TTL refresh.** Cache invalidation is part of `REQ-SUB-0009`'s test surface, not an implementation detail. A tenant-database projection contradicts `DEC-SUB-0004`; a scheduled refresh makes `REQ-SUB-0009` false by construction, and a TTL cache is that same failure wearing a different hat. |
| **`OD-SUB-0005`** what a module is | Architect | **The unit that already carries exactly one `IPermissionCatalogContributor` and one `Add*Module()` registration** — today HR, Finance/GL, Payroll, Attendance. **Not** the route group, **not** the assembly. Each declares a stable module key. Verified against the code: four contributors, seventeen route groups, because HR alone mounts seven. |
| **`OD-SUB-0006`** disabled-module response | **Owner (round 2)** | **403 Forbidden.** The route exists; this tenant may not reach it. Chosen over 404 **with the disclosure cost accepted knowingly** — a tenant can enumerate the product surface by probing, and in exchange support can answer "why can't I reach payroll" from the response rather than from server logs. |
| **`OD-SUB-0007`** what "menus" binds to | Architect | **`REQ-SUB-0014`'s server-provided enabled-module set, and nothing client-side.** The product's obligation is to publish the set truthfully; rendering is the client's. The product cannot enforce a menu it does not draw, and a UI assertion would not make `BR-PLT-0008` testable. |
| **`OD-SUB-0008`** one subscription or a history | **Owner (round 2)** | **Append-only history, exactly one in force at a time.** A plan change closes the current record and opens the next. `REQ-SUB-0001` becomes a queryable invariant rather than a mutation, and prorated multi-currency billing can reconstruct what was in force on any date. Matches the department and position history convention `Employee` already carries. |
| **`OD-SUB-0009`** term and expiry | **Owner** | **A term exists**, with a start and an end or an explicit perpetual marker. **Expiry blocks login for the tenant.** This makes `Authentication.md`'s then-`Draft` rule binding, so `REQ-SUB-0018` ceases to be conditional. |
| **`OD-SUB-0010`** relation to `TenantStatus` | **Owner (round 2)** | **Orthogonal — both checked at login.** Subscription state and `TenantStatus` are independent dimensions. Expiry blocks login **without touching `TenantStatus`**, so a billing lapse is never confused with an administrative suspension and the platform keeps the ability to suspend a paying tenant for abuse or legal cause. **No commercial reason is added to `TenantStatusChangeReason`.** |
| **`OD-SUB-0011`** grants above or below plan | **Owner (round 2)** | **Additive grants only.** A tenant may be granted a module or a raised cap above its plan, **never below**. Covers pilots, negotiated deals and goodwill grants without letting an override silently remove something the customer is paying for. Entitlement resolves as **plan ∪ grants** — one direction to model, one to test. |
| **`OD-SUB-0012`** data and permissions on disable | Architect | **Data is retained untouched and permissions become ineffective.** A disabled module's permissions are neither grantable nor effective, so a stale role assignment cannot reach it. `REQ-SUB-0016` already binds retention. Deleting tenant data on a commercial event would make a billing lapse destructive and irreversible — unacceptable for an ERP of record. |
| **`OD-SUB-0013`** who administers | Architect | **Platform plane only. No tenant-plane actor may administer a subscription, whatever permissions it holds.** Not genuinely open: `ADR-005:248` lists subscription management as a platform-administrator capability and `ADR-015` makes the platform plane a separate authorization plane. **Recorded rather than re-decided.** |
| **`OD-SUB-0014`** trials | **Owner** | **No trial concept.** A trial, if ever needed, is a plan with a short term — not a state, not a flag. **`REQ-SUB-0020` falls away.** |
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
refusal is at admission and it is immediate, specific and actionable. `OD-SUB-0009`'s expiry remains
the **only** commercial event that blocks login, and it blocks the whole tenant on a dated,
foreseeable term rather than an arbitrary user on an incremental one. **That asymmetry is deliberate.**

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
| `REQ-SUB-0020` — trial representable, *"CONDITIONAL ON `OD-SUB-0014`"* | **Falls away.** No trial concept exists. A trial is a plan with a short term. |
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

## 3. FP-014 rests on `ADR-021`, which is still `Proposed` — **found by the `DEC-L-021` check, architect's**

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

**Ratifying this package makes it authority, and an authority resting on a `Proposed` record is the
condition `DEC-L-021` names as incoherent.** Not resolved here — an ADR status change is its own act,
on the T-011 and T-020 precedent, and it is not in this task's scope.

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-25 | Solution Architecture Team | Ratifies FP-014. All seventeen `OD-SUB` rulings moved into the package from the 2026-08-25 ruling set, each attributed to the owner or the architect. All twelve `DEC-SUB` ratified as drafted. Three concerns carried as open at ratification: the undefined seat, `REQ-SUB-0027`'s two enforcement semantics, and this package's dependence on the still-`Proposed` `ADR-021`. |

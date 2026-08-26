# FP-014 — Proposed requirements

**Status: DRAFT — analysis only. Every identifier below is a PROPOSAL.**

**The prefix is settled.** `OD-SUB-0002` ruled a **new `REQ-SUB` space** rather than an extension of
`REQ-PLT`, and `Requirement-Numbering.md` carries `REQ-SUB` and `BR-SUB` as registered prefixes. The
identifiers below are stable; they are not working labels.

> *Amended 2026-08-26 by T-046.* This paragraph read: *"The prefix itself is unresolved …
> **`SUB` is not among them** … settled by neither this file nor its author. Every identifier here is
> renumberable, prefix included."* **True when written and false since ratification** — `SUB` is
> registered and the ruling that registered it is `OD-SUB-0002`. Dated rather than deleted
> (`DEC-L-039`), because the argument for a separate space is why the space exists.

**The `Authority` column reads honestly.** Where a requirement traces to something authored, it says
where and quotes it. Where it does not, it says **UNAUTHORED** — and it says that often, because the
entire authored basis for this package is three sentences. This follows the GL, PAY and ATT
precedent exactly: the roadmap's one-word module entry is not treated as a specification, and here
there is not even that, since `Product-Roadmap.md` never names subscription at all.

**Two sources are cited as `NON-AUTHORITATIVE` rather than as authority.** `Tenant-Management.md`'s
subscription material disclaims itself at its own line 13; `Authentication.md` carries status `Draft`.
Where a row leans on either, the column says so, and the row is conditional on an `OD-SUB` that asks
the owner whether to make it binding. Naming them is not the same as relying on them.

**Scope column.** `OD-SUB-0001` ruled **E + C — the whole plane**, so **every row below is in force**
and no scope strikes any of them. The codes are kept because they still say which half of the plane a
row belongs to, which is useful when reading it; they no longer gate anything.

| Code | Meaning |
|---|---|
| `E` | **Enablement.** Which modules a tenant may reach, and the enforcement of that. |
| `C` | **Commercial.** The agreement itself — term, price, invoicing, payment. |
| `*` | Both — the subscription record that either scope needs. |

**Nothing is struck.** The per-scope counts at the bottom were given because an `E`-only ruling would
have struck every `C` row; `OD-SUB-0001` ruled `E + C`, so they now read as a breakdown rather than as
two possible futures.

---

## The subscription record — the spine either scope needs

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0001` | Every tenant has **exactly one subscription in force at a time**, naming the plan that determines what the tenant may reach | `*` | `CON-0001`; `Glossary.md:283` — "a commercial agreement determining which modules and features are available to a Tenant" | `DEC-SUB-0001`, `OD-SUB-0008` |
| `REQ-SUB-0002` | A **plan** is a named, reusable definition naming the modules it grants, held once and referenced by many tenants — it is not a per-tenant list | `*` | `BR-PLT-0008` — "per subscription **plan**"; `ADR-017` § Lookup classification, class A (`:477`) classifies "subscription plans" as Platform-global | `DEC-SUB-0003` |
| `REQ-SUB-0003` | Plan definitions are **Platform-global data**. A tenant cannot create, amend or delete one | `*` | `ADR-017` § Lookup classification, class A (`:477`) — "Stored in the Platform database. **Tenants cannot create global rows**" | `DEC-SUB-0003` |
| `REQ-SUB-0004` | **No tenant-plane actor may alter its own subscription**, whatever permissions it holds. The tenant is the subject of the agreement, not a party able to amend it | `*` | `ADR-005` § Platform Administration (`:248`) lists "Subscription management" as a **platform administrator** capability; `ADR-015` (status **Accepted**) — the platform plane is a dedicated authorization plane | `DEC-SUB-0002`, `DEC-SUB-0010`, `OD-SUB-0013` |
| `REQ-SUB-0005` | The subscription surface remains **readable and administrable while the tenant's ERP database is unavailable** | `*` | `ADR-017` § Platform database boundary (`:164`) places subscription/plan metadata in the **Platform** database and `ADR-017` § Platform database boundary (`:169`) makes Platform-database operation independent of tenant-database availability; `:376`–`:378` require a controlled unavailability result rather than a fallback. **Amended by `DEC-L-024`** — formerly `ADR-021` § 10 Outage behaviour (`:207`), which names "subscription" explicitly but is `Proposed` and implementation-deferred | `DEC-SUB-0004` |
| `REQ-SUB-0006` | Every change to a subscription or a plan is **attributable and dated** — who, when, from what to what | `*` | **UNAUTHORED** — matches the `CreatedBy`/`ModifiedBy`/`StatusChangedBy` convention `Tenant` already carries | `DEC-SUB-0007` |

## Enablement — `BR-PLT-0008`, first sentence

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0007` | For any **(tenant, module)** pair the product answers, authoritatively, whether that module is enabled | `E` | `BR-PLT-0008` — "Modules may be enabled or disabled per subscription plan" | `OD-SUB-0005` |
| `REQ-SUB-0008` | Enablement is resolved **per request, server-side**, and is **never carried in the access token** | `E` | `FP-002 authentication-model.md:16` — tokens "exclude … subscription or billing information", under **exact claim cardinality** | `DEC-SUB-0005` |
| `REQ-SUB-0009` | A change to a tenant's enablement takes effect **without re-issuing tokens and without restarting the host** | `E` | **UNAUTHORED** — a consequence of `REQ-SUB-0008`; stated so it is tested rather than assumed | `DEC-SUB-0005` |
| `REQ-SUB-0010` | Enablement may be **overridden for one tenant ABOVE what its plan grants, and never below.** Entitlement resolves as **plan ∪ grants** | `E` | **UNAUTHORED** — no authored document states it, which the ruling did not change. **Settled by `OD-SUB-0011` (2026-08-25): additive grants only.** The row formerly asked which direction the product supported | `OD-SUB-0011` |

## Enforcement — `BR-PLT-0008`, second sentence

> "Disabled modules shall not appear in menus or APIs."

This is the clause with teeth, and it names **two** surfaces. It is carried here as requirements, not
as prose, because it is the only sentence in the authority that says *shall* about behaviour.

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0011` | A request to **any route belonging to a module not enabled for the caller's tenant is refused**, before the handler runs | `E` | `BR-PLT-0008` — "shall not appear in … **APIs**" | `DEC-SUB-0006`, `OD-SUB-0005`, `OD-SUB-0006` |
| `REQ-SUB-0012` | The refusal is applied by **one mechanism covering every module uniformly**, not by a per-module check each module remembers to add | `E` | **UNAUTHORED** — derived from `PermissionEndpointConventions`' stated design and from `ADR-012`'s Host-composition rule | `DEC-SUB-0006` |
| `REQ-SUB-0013` | **Platform-plane routes are never subject to module enablement.** Authentication, tenant selection, refresh, logout, platform support and the subscription surface itself stay reachable | `E` | `ADR-017` § Platform database boundary (`:169`), `:376`–`:378` (**amended by `DEC-L-024`**, formerly `ADR-021` § 10 Outage behaviour (`:200`–`:207`)); `ADR-015`; `PermissionEndpointConventions:27`–`:29` keeps the two planes as separate methods for exactly this class of mistake | `DEC-SUB-0004`, `DEC-SUB-0009` |
| `REQ-SUB-0014` | The product exposes, to an authenticated caller, **the set of modules enabled for its tenant**, so that a client can render navigation containing only those | `E` | `BR-PLT-0008` — "shall not appear in **menus**" | `OD-SUB-0007` |
| `REQ-SUB-0015` | A **permission belonging to a disabled module is not grantable and not effective**, so a disabled module cannot be reached through a stale role assignment | `E` | **UNAUTHORED** — the composed permission catalog (`IPermissionCatalogContributor`) is the second surface through which a module is reachable | `OD-SUB-0005`, `OD-SUB-0012` |
| `REQ-SUB-0016` | Disabling a module **does not delete, alter or hide the tenant's data** in that module; the data is unreachable, not destroyed, and returns intact on re-enablement | `E` | **UNAUTHORED** | `OD-SUB-0012` |

## Term and lifecycle

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0017` | A subscription carries a **dated term** — a start, and an end or an indication that it does not end | `*` | `Glossary.md:283` "agreement" — thin; the term is inferred from the noun, not stated | `OD-SUB-0009` |
| `REQ-SUB-0018` | An **expired** subscription denies that tenant every **gated module**, and **never denies authentication**. An expired tenant signs in, reaches the platform plane — its account, its users and the subscription surface itself — and reaches no gated module | `*` | `OD-SUB-0009` **as amended by `DEC-L-033`** (2026-08-26). No special case is needed to keep the platform plane reachable: `REQ-SUB-0013` already exempts it, so expiry acts through the same enablement gate every other entitlement does | `OD-SUB-0009`, `DEC-L-033`; `REQ-SUB-0013` |
| `REQ-SUB-0019` | **Subscription state and `TenantStatus` are ORTHOGONAL** — independent dimensions. Expiry never writes `TenantStatus`, and **no commercial reason joins `TenantStatusChangeReason`**. `TenantStatus` is evaluated at **authentication** and commercial state at the **enablement gate** | `*` | **UNAUTHORED** — no authored document states it, which the ruling did not change. `TenantStatus` today is `Provisioning`/`Active`/`Suspended`/`Archived` with no commercial reason among the change reasons. **Settled by `OD-SUB-0010` (2026-08-25): orthogonal.** The row formerly asked whether expiry suspends the tenant, and described both dimensions as *"checked at login"* — true only while expiry blocked login, and **corrected by `DEC-L-033`** | `OD-SUB-0010`, `DEC-L-033` |
| `REQ-SUB-0020` | A **trial is a plan with a short term**, and is nothing else. A tenant without a subscription holds an **all-module plan on a 14-day fixed term** — seeded by migration for tenants existing at cutover and issued on tenant creation thereafter, by **one rule** rather than two that agree. **No trial state, flag, column or enum member exists**: the trial ends because a later record takes effect, which is the mechanism every plan change already uses | `*` | `OD-SUB-0014` **as ruled by `DEC-L-034`** (2026-08-26). **Formerly UNAUTHORED** — the word "trial" appeared nowhere in the authority when this row was written, and `DEC-L-034` authored it | `OD-SUB-0014`, `DEC-L-034`; `DEC-L-009` (no grace period), `DEC-L-033` (expiry gates modules, never login) |

## Reading and disclosure

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0021` | A tenant-plane user may read **which modules its tenant has**, and may **not** read the commercial terms — price, invoice, payment state | `*` | `FP-002 business-rules.md:55` establishes the precedent that billing and subscription data are withheld from the tenant-facing token; the same reasoning applies to the read surface | `DEC-SUB-0002`, `OD-SUB-0013` |
| `REQ-SUB-0022` | Platform administration reads **across tenants**; the subscription read surface is not tenant-filtered for a platform-plane caller | `*` | `ADR-005` § Platform Administration (`:248`); `ADR-015` | `DEC-SUB-0010` |

## Commercial — in force, and none of it built

**Every row below is in force.** `OD-SUB-0001` ruled `E + C`, and the `OD-SUB` each row named has been
ruled too.

**None of them is built, and being in force does not say otherwise.** A requirement in force means the
question is settled — not that the code exists. Invoicing, payment capture, metering and proration have
no implementation in this repository, and nothing below should be read as reporting delivery.

> *Amended 2026-08-26 by T-046.* The heading read *"struck entirely if `OD-SUB-0001` rules `E`"* and
> the paragraph said every row was *"conditional on the scope ruling"* — accurate before ratification,
> and the reason the rows were written in conditional voice at all. The half that is still true, and
> was the more important half then as now, is that **none of this is proposed for building today.**

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0023` | A plan carries a **price**, in a currency, for a billing period | `C` | **UNAUTHORED** — no pricing statement exists anywhere in the repository | `OD-SUB-0015` |
| `REQ-SUB-0024` | Monetary amounts in the commercial plane use the product's money representation, **inherited unchanged** | `C` | `ADR-027` — `decimal(19,4)`. **Inherited, not re-decided** | `DEC-SUB-0008` |
| `REQ-SUB-0025` | The product issues an **invoice** from the vendor to the tenant, and that invoice is recorded outside the tenant's General Ledger | `C` | `OD-SUB-0016` **as ruled 2026-08-25** — the product issues invoices in-product. **Formerly UNAUTHORED and conditional**, which was accurate until the ruling | `DEC-SUB-0001`, `OD-SUB-0016` |
| `REQ-SUB-0026` | **Payment capture is performed by this product, and it is TOKENIZED.** The product owns the payment flow, its state and its reconciliation; **no primary account number, card verification value, cardholder name or expiry date ever enters SSAS** — those are captured by the provider's hosted fields or redirect. **Both halves are the requirement.** "We capture payment" and "we never see a card number" are both true, and reading either without the other gives the wrong system | `C` | `OD-SUB-0016` **as ruled 2026-08-25** (capture is in-product), **qualified by `ADR-029` and `DEC-L-018`** (capture is tokenized). **Formerly UNAUTHORED and conditional** | `OD-SUB-0016`, `DEC-L-018` |
| `REQ-SUB-0027` | Usage that affects price — seats, tenants, storage, transaction volume — is **metered**, and the product names exactly what is counted | `C` | `OD-SUB-0017` **as ruled 2026-08-25** — seats plus limits, with the residue ruled by `DEC-L-009`: a cap is enforced at admission, never at login. **Formerly UNAUTHORED and conditional** | `OD-SUB-0017` |
| `REQ-SUB-0028` | A mid-term plan change is **prorated** for the unused portion | `C` | `OD-SUB-0015` **as ruled 2026-08-25** — multi-currency, prorated. **Formerly UNAUTHORED and conditional.** Note `OD-PAY-0007` ruled proration for *payroll* on calendar days — that is a different subject and sets no precedent here | `OD-SUB-0015` |

---

## Counts per scope ruling

| `OD-SUB-0001` ruling | Requirements in force |
|---|---|
| **E — enablement only** | 6 spine + 4 enablement + 6 enforcement + 2 reading = **18** (the four lifecycle rows reduce to `REQ-SUB-0019` alone, since term and expiry are commercial; **19** with it) |
| **C — commercial only** | 6 spine + 4 lifecycle + 2 reading + 6 commercial = **18**, and `BR-PLT-0008` remains **unimplemented and still violated** |
| **E + C — the full plane** | **28** |

**Ruling `C` alone is the one combination this package advises against on the evidence**, and says so
here rather than hiding it in a decision: it would build billing for a product that still cannot stop
a tenant reaching a module it has not bought. That is an observation about `BR-PLT-0008` remaining
unmet, not a scope preference — the ruling is the owner's.

---

## What is deliberately absent, and why

**No requirement for a subscription *table*.** `Tenant-Management.md:168` names `TBL-PLT-Subscription`,
and it would be easy to carry that forward as though it were a design. It is a name in a document that
disclaims its own authority at line 13. Whether the commercial plane is one table or several is a data
model, and no data model is written until `OD-SUB-0001` and `OD-SUB-0004` are ruled.

**No requirement for what a "module" is.** `REQ-SUB-0007` and `REQ-SUB-0011` both depend on the unit of
enablement, and there are at least four candidates — the assembly, the route group, the permission
prefix, the roadmap's module name. Writing the requirement as though the answer were obvious would
settle `OD-SUB-0005` by phrasing.

**No signup or self-service requirement.** The word appears nowhere in the authority. A tenant is
created by a platform administrator today (`Tenant-Management.md`), and whether that ever becomes
self-service is a product question nobody has asked.

**No requirement derived from `Authentication.md`'s login rules except `REQ-SUB-0018`, which is marked
conditional.** That document is `Draft`, and it describes refusals for a state the product cannot
represent. Treating it as authority would let a draft written before any of this existed dictate the
lifecycle.

**No dunning, no credit control, no tax.** Each is a substantial subsystem and none is named in any
authored document. `OD-SUB-0015` and `OD-SUB-0016` have since been ruled — **and they scoped invoicing,
capture, pricing and proration, not these.** The absence is therefore a live exclusion rather than a
question awaiting a ruling. Naming them is deliberate: "subscription billing" means *all* of them to
many readers, and an unstated exclusion surfaces at acceptance.

**`BR-SUB` exists.** `OD-SUB-0002` ruled the new space and `Requirement-Numbering.md` carries `BR-SUB`;
[`business-rules.md`](business-rules.md) defines twenty-one rules in it. `BR-PLT-0008` still sits in the
`PLT` space, which was an argument in that decision and is now a fact about where the product's oldest
enablement rule lives.

> *Amended 2026-08-26 by T-046.* This paragraph read *"**No `BR-SUB` business rules.** …
> Adding one is part of `OD-SUB-0002`"*, and was true before ratification.

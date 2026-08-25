# FP-014 — Proposed requirements

**Status: DRAFT — analysis only. Every identifier below is a PROPOSAL.**

**The prefix itself is unresolved.** `Requirement-Numbering.md` lists ten functional prefixes — PLT,
HR, GL, INV, CRM, PRJ, PAY, ATT, PRC, MFG — and **`SUB` is not among them**. `REQ-SUB-####` is used
below as a working label so the rows can be referenced at all. Whether the commercial plane takes a
new `REQ-SUB` space or extends `REQ-PLT` (which already runs to `REQ-PLT-0067`) is **`OD-SUB-0002`**,
argued both ways there and settled by neither this file nor its author. Every identifier here is
renumberable, prefix included.

**The `Authority` column reads honestly.** Where a requirement traces to something authored, it says
where and quotes it. Where it does not, it says **UNAUTHORED** — and it says that often, because the
entire authored basis for this package is three sentences. This follows the GL, PAY and ATT
precedent exactly: the roadmap's one-word module entry is not treated as a specification, and here
there is not even that, since `Product-Roadmap.md` never names subscription at all.

**Two sources are cited as `NON-AUTHORITATIVE` rather than as authority.** `Tenant-Management.md`'s
subscription material disclaims itself at its own line 13; `Authentication.md` carries status `Draft`.
Where a row leans on either, the column says so, and the row is conditional on an `OD-SUB` that asks
the owner whether to make it binding. Naming them is not the same as relying on them.

**Scope column** — see `OD-SUB-0001`, which must be ruled first:

| Code | Meaning |
|---|---|
| `E` | **Enablement.** Which modules a tenant may reach, and the enforcement of that. |
| `C` | **Commercial.** The agreement itself — term, price, invoicing, payment. |
| `*` | Both — the subscription record that either scope needs. |

**If the owner rules `E` only, every `C` row is struck.** The counts at the bottom are given per scope
for that reason.

---

## The subscription record — the spine either scope needs

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0001` | Every tenant has **exactly one subscription in force at a time**, naming the plan that determines what the tenant may reach | `*` | `CON-0001`; `Glossary.md:283` — "a commercial agreement determining which modules and features are available to a Tenant" | `DEC-SUB-0001`, `OD-SUB-0008` |
| `REQ-SUB-0002` | A **plan** is a named, reusable definition naming the modules it grants, held once and referenced by many tenants — it is not a per-tenant list | `*` | `BR-PLT-0008` — "per subscription **plan**"; `ADR-017:475` classifies "subscription plans" as Platform-global | `DEC-SUB-0003` |
| `REQ-SUB-0003` | Plan definitions are **Platform-global data**. A tenant cannot create, amend or delete one | `*` | `ADR-017:475` — "Stored in the Platform database. **Tenants cannot create global rows**" | `DEC-SUB-0003` |
| `REQ-SUB-0004` | **No tenant-plane actor may alter its own subscription**, whatever permissions it holds. The tenant is the subject of the agreement, not a party able to amend it | `*` | `ADR-005:248` lists "Subscription management" as a **platform administrator** capability; `ADR-015` (status **Accepted**) — the platform plane is a dedicated authorization plane | `DEC-SUB-0002`, `DEC-SUB-0010`, `OD-SUB-0013` |
| `REQ-SUB-0005` | The subscription surface remains **readable and administrable while the tenant's ERP database is unavailable** | `*` | `ADR-021:207` — "account, **subscription**, and other platform-only pages" continue to work during a customer-managed outage | `DEC-SUB-0004` |
| `REQ-SUB-0006` | Every change to a subscription or a plan is **attributable and dated** — who, when, from what to what | `*` | **UNAUTHORED** — matches the `CreatedBy`/`ModifiedBy`/`StatusChangedBy` convention `Tenant` already carries | `DEC-SUB-0007` |

## Enablement — `BR-PLT-0008`, first sentence

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0007` | For any **(tenant, module)** pair the product answers, authoritatively, whether that module is enabled | `E` | `BR-PLT-0008` — "Modules may be enabled or disabled per subscription plan" | `OD-SUB-0005` |
| `REQ-SUB-0008` | Enablement is resolved **per request, server-side**, and is **never carried in the access token** | `E` | `FP-002 authentication-model.md:16` — tokens "exclude … subscription or billing information", under **exact claim cardinality** | `DEC-SUB-0005` |
| `REQ-SUB-0009` | A change to a tenant's enablement takes effect **without re-issuing tokens and without restarting the host** | `E` | **UNAUTHORED** — a consequence of `REQ-SUB-0008`; stated so it is tested rather than assumed | `DEC-SUB-0005` |
| `REQ-SUB-0010` | Enablement may be **overridden for one tenant** above or below what its plan grants, or it may not — the product supports exactly one of these and says which | `E` | **UNAUTHORED** | `OD-SUB-0011` |

## Enforcement — `BR-PLT-0008`, second sentence

> "Disabled modules shall not appear in menus or APIs."

This is the clause with teeth, and it names **two** surfaces. It is carried here as requirements, not
as prose, because it is the only sentence in the authority that says *shall* about behaviour.

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0011` | A request to **any route belonging to a module not enabled for the caller's tenant is refused**, before the handler runs | `E` | `BR-PLT-0008` — "shall not appear in … **APIs**" | `DEC-SUB-0006`, `OD-SUB-0005`, `OD-SUB-0006` |
| `REQ-SUB-0012` | The refusal is applied by **one mechanism covering every module uniformly**, not by a per-module check each module remembers to add | `E` | **UNAUTHORED** — derived from `PermissionEndpointConventions`' stated design and from `ADR-012`'s Host-composition rule | `DEC-SUB-0006` |
| `REQ-SUB-0013` | **Platform-plane routes are never subject to module enablement.** Authentication, tenant selection, refresh, logout, platform support and the subscription surface itself stay reachable | `E` | `ADR-021:200`–`:207`; `ADR-015`; `PermissionEndpointConventions:27`–`:29` keeps the two planes as separate methods for exactly this class of mistake | `DEC-SUB-0004`, `DEC-SUB-0009` |
| `REQ-SUB-0014` | The product exposes, to an authenticated caller, **the set of modules enabled for its tenant**, so that a client can render navigation containing only those | `E` | `BR-PLT-0008` — "shall not appear in **menus**" | `OD-SUB-0007` |
| `REQ-SUB-0015` | A **permission belonging to a disabled module is not grantable and not effective**, so a disabled module cannot be reached through a stale role assignment | `E` | **UNAUTHORED** — the composed permission catalog (`IPermissionCatalogContributor`) is the second surface through which a module is reachable | `OD-SUB-0005`, `OD-SUB-0012` |
| `REQ-SUB-0016` | Disabling a module **does not delete, alter or hide the tenant's data** in that module; the data is unreachable, not destroyed, and returns intact on re-enablement | `E` | **UNAUTHORED** | `OD-SUB-0012` |

## Term and lifecycle

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0017` | A subscription carries a **dated term** — a start, and an end or an indication that it does not end | `*` | `Glossary.md:283` "agreement" — thin; the term is inferred from the noun, not stated | `OD-SUB-0009` |
| `REQ-SUB-0018` | **CONDITIONAL ON `OD-SUB-0009`.** An **expired** subscription refuses login for that tenant | `*` | **NON-AUTHORITATIVE** — `Authentication.md:123` says "Expired subscriptions cannot login", but that document's status is `Draft` (`:7`–`:9`). Carried as a candidate, not as a rule | `OD-SUB-0009` |
| `REQ-SUB-0019` | The relationship between **subscription state and `TenantStatus`** is explicit: whether expiry suspends the tenant, or the two are orthogonal dimensions both checked at login | `*` | **UNAUTHORED** — `TenantStatus` today is `Provisioning`/`Active`/`Suspended`/`Archived` with no commercial reason among the change reasons | `OD-SUB-0010` |
| `REQ-SUB-0020` | **CONDITIONAL ON `OD-SUB-0014`.** A **trial** is representable, and the product states whether a trial is a plan, a subscription state, or neither | `*` | **UNAUTHORED** — the word "trial" appears nowhere in the authority; only GL's trial balance in code | `OD-SUB-0014` |

## Reading and disclosure

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0021` | A tenant-plane user may read **which modules its tenant has**, and may **not** read the commercial terms — price, invoice, payment state | `*` | `FP-002 business-rules.md:55` establishes the precedent that billing and subscription data are withheld from the tenant-facing token; the same reasoning applies to the read surface | `DEC-SUB-0002`, `OD-SUB-0013` |
| `REQ-SUB-0022` | Platform administration reads **across tenants**; the subscription read surface is not tenant-filtered for a platform-plane caller | `*` | `ADR-005:248`; `ADR-015` | `DEC-SUB-0010` |

## Commercial — struck entirely if `OD-SUB-0001` rules `E`

**Every row below is conditional on the scope ruling and on the `OD-SUB` it names. None of them is
proposed for building today**; they exist so the owner can see what ruling `C` would commit to.

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-SUB-0023` | A plan carries a **price**, in a currency, for a billing period | `C` | **UNAUTHORED** — no pricing statement exists anywhere in the repository | `OD-SUB-0015` |
| `REQ-SUB-0024` | Monetary amounts in the commercial plane use the product's money representation, **inherited unchanged** | `C` | `ADR-027` — `decimal(19,4)`. **Inherited, not re-decided** | `DEC-SUB-0008` |
| `REQ-SUB-0025` | **CONDITIONAL ON `OD-SUB-0016`.** The product issues an **invoice** from the vendor to the tenant, and that invoice is recorded outside the tenant's General Ledger | `C` | **UNAUTHORED** | `DEC-SUB-0001`, `OD-SUB-0016` |
| `REQ-SUB-0026` | **CONDITIONAL ON `OD-SUB-0016`.** **Payment capture** is either performed by this product or delegated to an external provider, and the product states which | `C` | **UNAUTHORED** | `OD-SUB-0016` |
| `REQ-SUB-0027` | **CONDITIONAL ON `OD-SUB-0017`.** Usage that affects price — seats, tenants, storage, transaction volume — is **metered**, and the product names exactly what is counted | `C` | **UNAUTHORED** | `OD-SUB-0017` |
| `REQ-SUB-0028` | **CONDITIONAL ON `OD-SUB-0015`.** A mid-term plan change is **prorated**, or it is not, and the product says which | `C` | **UNAUTHORED**. Note `OD-PAY-0007` ruled proration for *payroll* on calendar days — that is a different subject and sets no precedent here | `OD-SUB-0015` |

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

**No dunning, no credit control, no tax.** Each is a substantial subsystem, none is named in any
authored document, and `OD-SUB-0015` and `OD-SUB-0016` must be ruled before any of them can even be
scoped. Naming them as absences is deliberate: "subscription billing" means *all* of them to many
readers, and an unstated exclusion surfaces at acceptance.

**No `BR-SUB` business rules.** `Requirement-Numbering.md` has four business-rule prefixes — PLT, HR,
GL, ATT. Adding one is part of `OD-SUB-0002`, and `BR-PLT-0008` already sits in the `PLT` space, which
is itself an argument in that decision.

# FP-014 — Authorization model (proposed)

**Written from the ruling set of 2026-08-25**, in which all seventeen `OD-SUB` carry rulings and the
scope is `E + C`. Reads on from [`domain-model.md`](domain-model.md).

Three parts: **who may administer the commercial plane** (nobody inside a tenant), **who may read
what** (the disclosure line), and **the module gate** — which is not a new authorization architecture
but a sibling of one that already exists.

---

## The whole administrative surface is platform-plane. There is no tenant-plane write.

`OD-SUB-0013` ruled it and `REQ-SUB-0004` states it: **no tenant-plane actor may create, amend or
delete a subscription, a plan, a grant or an invoice, whatever permissions it holds.**

This is not a permission that is withheld — it is a permission that **does not exist**. There is no
`Subscriptions.Manage` in any tenant-plane catalog to grant by mistake, and a tenant administrator
with every permission the product defines still cannot change what their tenant is paying for.

**Why it has to be structural rather than careful.** `BR-PLT-0008` says disabled modules shall not
appear in menus or APIs. If a tenant-plane permission could amend entitlement, then the rule would be
enforced against a value the enforced party controls, and the whole of `CON-0001` would rest on
nobody granting the wrong permission. `ADR-005` § Platform Administration (`:248`) lists "Subscription management" among
**platform-administrator** capabilities, and `ADR-015` (status **Accepted**) makes the platform plane
a separate authorization plane rather than a role inside the tenant one. Both were already true;
`DEC-SUB-0010` recorded it and this document builds on it.

### The mechanism is already in the codebase, and it is two methods on purpose

`PermissionEndpointConventions` keeps `RequirePermission` (tenant plane) and
`RequirePlatformPermission` (platform plane) as **two separate methods**, and says why in the file:

> "Deliberately separate from the tenant helper — the two must not mix, and keeping them as two
> methods is what stops a caller choosing the wrong plane by passing a flag."

Every route in this package that writes uses `RequirePlatformPermission`. Not a flag, not a policy
name that happens to resolve platform-side — the other method.

---

## Permission names — proposed

The platform plane carries exactly **one** permission today: `Platform.Support.Administer`, used on
every route of `PlatformSupportAuthorityEndpointRouteBuilderExtensions`. This package proposes the
second set.

| Permission | Plane | Covers |
|---|---|---|
| `Platform.Plans.View` | platform | read the plan catalog, its modules, limits and prices |
| `Platform.Plans.Administer` | platform | create, amend and retire plans (`REQ-SUB-0002`, `REQ-SUB-0003`) |
| `Platform.Subscriptions.View` | platform | read any tenant's subscription history (`REQ-SUB-0022`) |
| `Platform.Subscriptions.Administer` | platform | append a subscription record — assign, change plan, renew (`REQ-SUB-0001`) |
| `Platform.EntitlementGrants.Administer` | platform | append and revoke additive grants (`REQ-SUB-0010`) |
| `Platform.Invoices.View` | platform | read invoices and payment attempts (`REQ-SUB-0025`) |
| `Platform.Invoices.Administer` | platform | assemble, issue and void invoices |

Coarse `View` / `Administer`, following `Platform.Support.Administer` rather than HR's per-act verbs.
The platform plane has one principal type and a small operator population; splitting `Administer`
into `Create`/`Update`/`Retire` would produce grants nobody distinguishes in practice.

**`Platform.EntitlementGrants.Administer` covers revocation as well as creation, and that is
deliberate.** Under `OD-SUB-0011` a revocation is a superseding grant record — the same append, the
same table. A separate `Revoke` permission would imply a separate act and invite a design in which
revocation edits a row.

### ⚠ A naming hazard this package inherits and must not make worse

**The `Platform.` prefix does not tell you which plane a permission belongs to.**
`Platform.Users.View` and `Platform.Roles.Create` are **tenant-plane** permissions — they live in
`PlatformPermissionNames`, are granted to tenant roles, and are enforced with `RequirePermission`.
`Platform.Support.Administer` is **platform-plane**. Same prefix, different planes, and only the
convention method at the route distinguishes them.

The names above extend the platform-plane set, so the ambiguity grows by six.

**Choice made, with the alternative named:** keep the `Platform.` prefix, because
`Platform.Support.Administer` established it and a second prefix would mean the platform plane had
two naming schemes for seven permissions. **The mitigation is mechanical rather than a convention
anyone must remember** — an architecture guard asserting that every `Platform.Plans.*`,
`Platform.Subscriptions.*`, `Platform.EntitlementGrants.*` and `Platform.Invoices.*` name is used
**only** with `RequirePlatformPermission` and never with `RequirePermission`. `REQ-SUB-0004` is
exactly the requirement that guard protects, and without it the requirement is enforced by careful
reading.

If the owner would rather have an unambiguous prefix, that is a cheap change **now** and an
expensive one after the permissions are assigned — flagged for that reason, not to reopen it.

---

## The read surface, and where disclosure stops — `REQ-SUB-0021`

This is one of the two requirements T-006 deferred here, and it draws the sharpest line in the
package.

| Caller | May read | May **not** read |
|---|---|---|
| Any authenticated tenant user | **which modules its tenant has** — the enabled-module set | price, plan cost, invoice, payment state, grant reason, another tenant's anything |
| Platform-plane operator with `*.View` | everything, **across tenants** (`REQ-SUB-0022`) | — |

**The tenant-facing read is the entitlement projection and nothing else.** It returns module keys. It
carries no price, no invoice, no term, no plan name that could imply a tier, and no seat cap — a cap
would be arguable either way, and it is excluded because a cap is a commercial term and
`REQ-SUB-0021` names commercial terms as the boundary.

**`FP-002` set this precedent and this package follows it rather than inventing a line.** The access
token excludes "subscription or billing information" by name (`authentication-model.md:16`). The read
surface applies the same reasoning to the same data: what a tenant needs is *what it can do*, not
*what it costs*.

### The enabled-module read requires authentication and no permission — chosen, and here is why

`REQ-SUB-0014`'s endpoint is available to **any authenticated tenant user**, with no permission gate.

Gating it would be the obvious instinct and it is wrong. A user without the permission would receive
an empty module set, which is **indistinguishable from a tenant that has bought nothing** — so a
missing grant would render an empty application and look like a billing problem. The set is also not
sensitive: it tells a user which parts of the product they can reach, which they discover by clicking
anyway.

**What it must not become is a per-user capability list.** It answers *what does this tenant have*,
not *what may this user do*. The second question is the permission catalog's and already has an
answer; conflating them would put entitlement and authorization in one response and make the two
impossible to reason about separately.

---

## The module gate — `REQ-SUB-0012`, the other requirement T-006 deferred

`REQ-SUB-0011` says a request to a route of a module the tenant does not have is refused.
`REQ-SUB-0012` says the refusal is applied by **one mechanism covering every module uniformly**, not
by a per-module check each module remembers to add.

`DEC-SUB-0006` settled that it is one shared endpoint convention. This document gives its shape.

### It is a sibling of `RequirePermission`, and it qualifies on the same test

`PermissionEndpointConventions` states the test its members must pass:

> "It expresses a requirement and nothing more. It names no permission, defines no policy and knows
> no module: the CALLER supplies the permission name — Platform passes Platform's, HR passes HR's —
> and the Host's policy provider materialises the requirement."

The proposed convention — `RequireEnabledModule(moduleKey)` — passes it identically: it names no
module, knows no business concept, and takes a key the caller supplies. It resolves
`EntitlementAt(tenant, now).modules` (see [`domain-model.md`](domain-model.md)) and refuses when the
key is absent.

**It lives beside `RequirePermission`, not inside it.** Entitlement and authorization are different
questions with different answers — *has this tenant bought this* versus *may this user do this* — and
a caller must be able to see both at the route.

### Where it is applied, and the number that matters

`OD-SUB-0005` ruled the module is the unit carrying one `IPermissionCatalogContributor` and one
`Add*Module()` registration. Applying the gate at the **route group** in the Host gives:

| Surface | Route groups | Gated? |
|---|---|---|
| Host | 1 | **exempt** — `REQ-SUB-0013` |
| Platform — auth, support auth, localization, identity/access, support authority, company | 6 | **exempt** — `REQ-SUB-0013` |
| HR | 7 | gated, **one** module key |
| Finance/GL | 1 | gated |
| Payroll | 1 | gated |
| Attendance | 1 | gated |

**Ten gated route groups across four module keys**, and seven exempt.

**The seven exemptions are the requirement, not an oversight.** A lapsed tenant must still be able to
authenticate, select its tenant, refresh, reach platform support and be re-enabled. Gating the
Platform plane would lock a tenant out of the only surface that could restore it — a failure with no
recovery path that does not involve a database edit.

### The permission half — `REQ-SUB-0015`

A module is reachable through two surfaces, not one: its **routes** and its **permissions**. A gate
on routes alone leaves a stale role assignment naming a disabled module's permission, which is a hole
in a gate rather than a gate.

`OD-SUB-0012` ruled that a disabled module's permissions are **neither grantable nor effective**.
Two enforcement points, both of which need the same map from permission to module key:

1. **At grant time** — assigning a permission of a module the tenant does not have is refused.
2. **At evaluation time** — such a permission does not satisfy a `RequirePermission` check even if a
   role still carries it, so a grant made while entitled stops working when entitlement lapses and
   resumes when it returns.

**The map already exists in the right shape.** `IPermissionCatalogContributor` is *per module* — that
is precisely why `OD-SUB-0005` chose it as the unit — so each contributor can declare the module key
its definitions belong to, and the composed catalog carries permission → module without a second
registry. **A separate mapping table would be a divergent notion of module**, and `REQ-SUB-0015`
requires routes and permissions to gate on the same unit.

**Data is untouched throughout** (`REQ-SUB-0016`, `OD-SUB-0012`): permissions become ineffective,
rows are not deleted, and re-enabling restores reachability with nothing else to undo.

---

## What entitlement is *not* allowed to be

**Not a claim.** `FP-002`'s token model has exact claim cardinality and excludes subscription and
billing information by name (`DEC-SUB-0005`, `REQ-SUB-0008`). Nothing in this authorization model
reads a claim to decide entitlement, and nothing adds one. A claim would also make
`REQ-SUB-0009` false for the token lifetime.

**Not a role.** An "Entitled" role or a synthetic permission per module would put a commercial fact
inside the tenant's own authorization data, where a tenant administrator could see it and — depending
on the role model — assign it.

**Not a tenant-status value.** `OD-SUB-0010` ruled the dimensions orthogonal;
`TenantStatusChangeReason` gains no commercial member. See
[`lifecycle-model.md`](lifecycle-model.md).

---

## What is deliberately absent

- **No self-service permission of any kind.** No tenant-plane actor administers a subscription, so
  there is no `ViewOwnInvoice`, no `Subscriptions.RequestUpgrade`, no upgrade flow.
  `OD-SUB-0013` ruled tenant self-service out; `REQ-SUB-0004` bars the write path it would need.
  This is a different absence from FP-013's — that one was a missing input, this one is a ruling.
- **No permission over payment capture.** `T-010` owns that surface. Naming a permission for it here
  would assert that the act happens inside this package's authorization plane, which is one of the
  things `T-010` has to decide.
- **No delegated administration.** Whether a reseller or partner could administer a subset of tenants
  is a real product question, is unauthored, and is not raised by any `REQ-SUB`. Named as an absence
  so it is not read as covered.

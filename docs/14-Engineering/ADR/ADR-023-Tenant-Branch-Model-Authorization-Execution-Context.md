---
id: ADR-023
title: Tenant Branch Model, Authorization and Execution Context
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-08-18
owner: Solution Architecture Team
tags:
  - multi-tenancy
  - branch
  - ownership
  - scoping
  - authorization
  - execution-context
  - architecture
depends_on:
  - ADR-005
  - ADR-008
  - ADR-013
  - ADR-014
  - ADR-015
  - ADR-017
  - ADR-020
used_by:
  - Platform
  - HR
  - GL
  - Sales
  - Inventory
---

# ADR-023: Tenant Branch Model, Authorization and Execution Context

---

# Status

**Accepted**

Accepted alongside the Branch foundation implementation (slices B0, B1a, B1b, B1c). It establishes the second ownership dimension beneath the tenant, the authorization model that governs it, and the execution context that every branch-owned write resolves against.

Not every decision recorded here is runtime-proven today. The *Implementation status* section below classifies each decision as implemented and verified, structurally implemented with runtime proof deferred, or a forward architectural rule. That classification is part of the decision record, not commentary on it.

---

# Context

`ADR-005` defines the platform hierarchy, and `ADR-014` established Company as a legal-entity dimension beneath the tenant. Neither answers a different question that HR, GL, Sales and Inventory all ask immediately: **which operating location produced this record**.

A tenant in this product is an organization. Real organizations operate from more than one location — a head office and its outlets, a main warehouse and its regional depots — and their users are not interchangeable across those locations. A clerk in Jeddah must not post a document into Riyadh's books because the client sent a different identifier.

Tenant ownership answers *whose data is this*. Branch ownership answers *which operating location inside that tenant produced it*. These are independent dimensions: every branch-owned entity is also tenant-owned, but most tenant-owned data is tenant-global and has no branch at all. A `Branch` cannot belong to a branch, and a `Company` is not located at one.

The implemented persistence layer already provides strong tenant isolation for any type implementing `ITenantOwnedEntity` (`ADR-014`): a global query filter on `TenantId`, server-assigned `TenantId` on insert, rejection of post-creation `TenantId` change, restricted deletes, and audit stamping. Branch scoping must reuse that machinery rather than invent a parallel one.

Two constraints from the tenant-storage work shape the model materially:

- `ADR-017` splits the estate into a **platform database** and a **tenant ERP database**. Business structure belongs to the tenant database; identity and authentication belong to the platform database. A physical foreign key between the two is impossible the moment a tenant is promoted to dedicated storage.
- `ADR-020` copies a tenant's data during shared-to-dedicated cutover from a manifest derived from the `TenantDbContext` model.

A decision is required now, before the first branch-owned business entity exists. Whether business records physically carry a `BranchId` is a one-way-door choice that would be prohibitively expensive to retrofit once HR, GL and Sales data exists.

---

# Problem Statement

Define the branch ownership, authorization and execution-context model such that:

- branch scope is a real data partition, not a UI filter;
- the branch a write lands in is decided by the server and can never be asserted by a client;
- authorization for that branch is re-evaluated at write time, because access can be revoked inside a session's lifetime;
- the model works across the platform/tenant database split without a cross-catalog foreign key;
- a tenant can be onboarded from zero branches without the write boundary making the first branch unreachable;
- no user is ever left authenticated but unable to work, and no user silently gains access to every branch.

---

# Decision

1. **Branch is Tenant-DB-owned.** `Branch` lives in the tenant ERP database (`tenant.Branches`) and implements `ITenantOwnedEntity`, inheriting all existing tenant isolation rules unchanged.
2. **Branch is not `IBranchOwnedEntity`.** A branch cannot belong to a branch. Branch is the branch *root*, exactly as `Tenant` is the tenant root and `Company` is the company root (`ADR-014`).
3. **`UserBranchAccess` is Platform-DB-owned.** It lives in the platform database (`platform.UserBranchAccess`), with the user it authorizes, because authentication must resolve branch scope before an ambient tenant context or tenant-database route exists.
4. **No cross-database foreign key exists from `UserBranchAccess.BranchId` to `Branch`.** `BranchId` is an opaque cross-database identifier (`ADR-013`). Existence, tenant ownership and active state are validated by the application against the tenant database before any assignment row is written.
5. **`Platform.Tenant.Administer` grants implicit access to all active branches of the current tenant.** Branch scope for an administrator is derived from authority, not stored.
6. **An active normal tenant user requires at least one active authorized branch.** Creating, updating, or leaving such a user active with zero effective branches is refused.
7. **Tenant Administrators receive no `UserBranchAccess` fan-out rows.** Materializing rows for them would require synchronization on every branch creation and would have to exist before the first branch does.
8. **`AuthenticationSession.ActiveBranchId` is the durable current execution context.** It is a nullable `Guid` on the existing session record; null means no branch has been selected yet.
9. **`ActiveBranchId` is not authorization proof.** It records a prior decision. It is never accepted as evidence that the decision is still valid.
10. **Branch authorization is revalidated server-side on every branch-owned write**, through `TenantDbContext` and `IBranchWriteAuthorizer`, against the durable session and the live access resolver.
11. **Exactly one authorized branch is auto-selected** at session resolution.
12. **Multiple authorized branches require explicit selection.** There is no skip: the session stays branch-less and every branch-owned write is refused until the user chooses.
13. **A Tenant Administrator with zero active branches enters `FirstBranchRequired`.** This is the onboarding path, not an error.
14. **A normal user with zero effective active branches is an integrity failure**, refused as `AccountIntegrityFailure` rather than presented as an empty branch picker.
15. **Branch switching revalidates authorization at switch time.** Selection and switching are the same operation.
16. **`IBranchOwnedEntity` is the marker for branch-owned ERP entities.** Implementing it is a deliberate classification, never a default.
17. **Tenant-global entities remain outside `IBranchOwnedEntity`** unless explicitly classified as branch-owned.
18. **`BranchId` on branch-owned writes comes from the server execution context**, never from a request DTO, header, form field, or token claim.
19. **Branch deactivation cannot strand an active normal user** with zero active branches, cannot remove a tenant's only active branch, and cannot retire the active main branch without naming a replacement in the same operation.
20. **Branch topology mutations share `BranchTopologyLock`**, a per-tenant SQL Server application lock (`sys.sp_getapplock`). Acquire, then read topology, then validate, then persist, then release.
21. **Shared-to-dedicated cutover includes `Branch`** through the tenant-owned entity manifest derived from the `TenantDbContext` model (`ADR-020`), by construction rather than by a hand-maintained list.
22. **Reports scope to the current branch or an explicitly authorized branch set**, and never by omitting the `BranchId` predicate.

---

# Ownership hierarchy

```
Tenant   (Guid TenantId)                        -- tenant root; not ITenantOwnedEntity
  ├── Company  (Guid CompanyId, Guid TenantId)  -- ITenantOwnedEntity; NOT ICompanyOwnedEntity
  └── Branch   (Guid BranchId,  Guid TenantId)  -- ITenantOwnedEntity; NOT IBranchOwnedEntity
        └── Branch-owned business entity        -- ITenantOwnedEntity + IBranchOwnedEntity  (future: HR/GL/Sales)
              (Guid TenantId, Guid BranchId, own PK)
```

Company and Branch are **sibling dimensions beneath the tenant**, not nested. A company is a legal entity; a branch is an operating location. A future business record may be scoped by either, both, or neither, and each scoping is an explicit classification.

Carrying `TenantId` alongside `BranchId` is deliberate, for the same reason `ADR-014` gives for `CompanyId`: `BranchId` implies a tenant, but storing `TenantId` too preserves the existing tenant filter and reuses the proven machinery without special cases.

---

# Database placement

| Record | Database | Schema/table | Owns |
|--------|----------|--------------|------|
| `Branch` | Tenant ERP | `tenant.Branches` | The operating location, with the business data it scopes |
| `UserBranchAccess` | Platform | `platform.UserBranchAccess` | Which branches a tenant user may enter |

**Why `Branch` is in the tenant database.** Putting it in the platform database would make every branch-scoped read a cross-database join, and would move tenant business structure onto the plane that must stay available when tenant storage is not.

**Why `UserBranchAccess` is in the platform database.** Authentication must answer "which branches may this user enter" while deciding whether a login even completes, and the platform plane is the one that stays available while a tenant database is mid-cutover or unreachable.

**Why there is no foreign key between them.** A physical constraint across catalogs is impossible once a tenant is promoted to dedicated storage (`ADR-017`) — the same reason `Company` has no FK to `Tenant`. `UserBranchAccess` does hold a foreign key to `TenantUser`, which is in the same catalog.

**Why `UserBranchAccess` is not `ITenantOwnedEntity`.** The global tenant query filter would hide these rows from the authentication path that must read them before an ambient tenant context exists — the same reason `TenantDatabaseAssignment` is not tenant-owned. `TenantId` is retained as a trusted column and every query filters on it explicitly.

## Integrity enforced by index

- Branch code is unique within a tenant, over the normalized code.
- At most one **active main** branch per tenant, enforced by a filtered unique index (`[IsMainBranch] = 1 AND [IsActive] = 1`) rather than a trigger. Expressing it as a filtered index is what allows demote-flush-promote inside one transaction when the main branch changes.
- `UserBranchAccess` is unique on `(TenantId, TenantUserId, BranchId)`.

---

# Execution context and the write boundary

## Resolution order

The tenant must be known before a branch can be, because branches live in the tenant's own database and cannot be enumerated until routing has resolved. There is therefore a legitimate authenticated state with a tenant and **no** branch.

```
authenticate  →  resolve tenant  →  route to tenant database  →  resolve branch
```

`ICurrentBranch.BranchId` is nullable. **Null is not an error at that layer**; it is the answer to "has a branch been selected yet". The write boundary is what turns it into a refusal, and only for branch-owned data. A tenant-global write remains perfectly legal with no branch selected.

## What happens on save

`TenantDbContext` runs the branch rules **before anything else touches the database**, and only when `IBranchOwnedEntity` instances are actually in the change set. Tenant-global writes — `Company`, `Branch` itself — are unaffected. This is precisely what keeps first-branch onboarding reachable: demanding an active branch context in order to create the very first branch would be unsatisfiable by construction.

When branch-owned entities are in play, `IBranchWriteAuthorizer` resolves and re-authorizes the branch in **one call**, because answering "which branch" and "may this user still write to it" separately is how the two answers drift apart:

- the **active branch** is read from the durable `AuthenticationSession`, never from a request header, form field, or token claim;
- the session's **status and expiry** are re-read, so a revoked or expired session cannot keep writing through a branch it selected while it was still usable;
- the **authorization** is re-asked through `ITenantBranchAccessResolver`, which re-reads the assignment rows, the administrator authority, and whether the branch is still active.

It **fails closed**. No branch selected, session unusable, access revoked, authority revoked, branch deactivated — each refuses the write rather than falling back to a previously valid answer. A missing authorizer or missing session context is also a refusal, never a permit.

Once authorized, the stamping rules are:

| Change | Rule |
|--------|------|
| `Added`, `BranchId` empty | Stamped with the trusted branch |
| `Added`, `BranchId` supplied | **Confirmed, never trusted** — refused if it does not match the trusted branch |
| `Modified` with `BranchId` modified | Refused — branch ownership cannot change after creation |
| `Modified` / `Deleted` in another branch | Refused — ownership must match the trusted branch context |

Relocating history by editing a column would move records between branches with no record that they moved. If a business transfer is ever needed, it must be an explicit, auditable operation, not a property assignment.

## Authorization model

| Principal | Branch scope | Source |
|-----------|--------------|--------|
| `Platform.Tenant.Administer` holder | All **active** branches of the current tenant | Derived from authority; no rows |
| Normal tenant user | `UserBranchAccess` rows ∩ active branches | Stored rows, intersected live |

`ITenantBranchAccessResolver` is the only place that decides this, so a login path and a write path cannot disagree about which rule applies. It **always intersects with active branches**: an assignment row naming a deactivated branch is not access. The row survives deactivation deliberately, so reactivating a branch restores the access that existed before it; filtering at resolution is what stops the retained row from granting entry meanwhile.

## Session outcomes

| Authorized branches | Principal | Outcome |
|---------------------|-----------|---------|
| 0 | Tenant Administrator | `FirstBranchRequired` — onboarding |
| 0 | Normal user | `AccountIntegrityFailure` — refused, fails closed |
| 1 | Any | `Active` — auto-selected |
| > 1 | Any | `BranchSelectionRequired` — session stays branch-less |

---

# Topology locking

The invariant "no active normal user is left without an active branch" spans two databases and therefore cannot be held by a transaction or a database constraint. It is held by serializing every mutation that can change branch topology onto one per-tenant resource.

Both halves must take the same lease or the guard protects nothing:

- **branch deactivation**, in Infrastructure;
- **user creation and branch-assignment editing**, in Application command handlers, through `IBranchTopologyGuard`.

The order is the whole point: **acquire, read topology, validate, persist, release**. Validating before acquiring and trusting the result afterwards is exactly the race this closes — the facts a decision rests on must not be able to change between the decision and the write.

Failure to acquire is a **retryable refusal** (`Branch.TopologyBusy`), not a failure: nothing was attempted and nothing was lost. Ownership lives and dies with the lease; a dying process drops the connection and releases it, so there is no lease to expire and no stale owner to clean up.

---

# Error semantics

Branch errors deliberately **name no database topology**. A caller told which record lives in which database learns the shape of the estate from an error message.

One generic failure (`Branch.AssignmentInvalid`, `Branch.InvalidSelection`) answers "does not exist", "belongs to another tenant" and "is inactive" identically, on purpose: distinguishing them would let an administrator of one tenant probe another tenant's branch identifiers for existence.

---

# Implementation status

This section is normative. Documentation must not claim that all twenty-two decisions are runtime-proven, because three are not.

## Implemented and verified

Decisions **1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 17, 19, 20, 21**.

Covered by real-SQL integration tests and by architecture guards, including a guard asserting that no migration introduces `principalTable: "Branches"` — decision 4 cannot regress silently.

## Structurally implemented — end-to-end runtime proof deferred

Decisions **10, 16, 18**.

For decision 10, what **is** proven with real SQL is `IBranchWriteAuthorizer` itself, across six cases:

- assignment revocation
- branch deactivation
- Tenant Administrator authority revocation
- session revocation
- session expiry
- no branch selected

What is **not** yet proven is the full chain:

```
real production IBranchOwnedEntity
  → TenantDbContext detects the branch-owned change
  → IBranchWriteAuthorizer is invoked
  → authorization succeeds or refuses
  → the write proceeds or fails closed
```

The cause is simply that **no production entity implements `IBranchOwnedEntity` yet**. The central `TenantDbContext` enforcement exists and the authorizer is proven in isolation, but the call site between them has never executed against a real business entity.

## Forward architectural rule only

Decision **22**.

It is neither implemented nor enforced. No reporting exists. `ITenantBranchAccessResolver` supplies the scope primitives — current branch, authorized set — but nothing prevents a future report from omitting the branch predicate. The executable guard must be written alongside the first branch-scoped report, in the same slice.

## Delivered scope

**Implemented:** branch persistence, `UserBranchAccess`, branch lifecycle (create, rename, deactivate, main-branch switching, first-branch onboarding), mandatory user branch assignments, topology locking, `ActiveBranchId` session foundation, branch selection and switching, write-time reauthorization.

**Deferred:** branch administration UI, the first production `IBranchOwnedEntity`, end-to-end runtime proof of decisions 10/16/18, branch-scoped reporting and the decision-22 guard, and user-management HTTP functional authorization.

---

# Deferred obligations

## LOW-1 — Employee closes the runtime proof

**Employee is the first production `IBranchOwnedEntity`** and must close five obligations with real SQL:

1. `IBranchWriteAuthorizer` **is actually invoked** on a real Employee save. This is the obligation that would otherwise hide a wiring defect: every current test passes whether or not that call site is reached.
2. An `Added` Employee is stamped with the current `BranchId`.
3. A spoofed `BranchId` is refused.
4. `BranchId` mutation is refused.
5. Cross-branch update and delete are refused.

Items 2 through 5 are the write-boundary rules; item 1 is the wiring. Proving the rules without proving the wiring leaves decision 10 unverified.

## LOW-2 — User-management functional authorization

Functional authorization for the user-management commands is deferred until those commands are exposed over HTTP. The domain invariants are enforced today; the HTTP authorization surface does not exist yet.

## Forward requirement — the reporting guard

The first branch-scoped reporting implementation must add both the executable enforcement pattern and an architecture guard proving that authorized branch predicates cannot be omitted. Until then, decision 22 is an architectural requirement, not a control.

---

# Consequences

## For HR

`Employee` implements `ITenantOwnedEntity` + `IBranchOwnedEntity` and carries `TenantId` + `BranchId`. HR is the slice that converts decisions 10, 16 and 18 from structurally implemented to runtime-proven. Employee-scoped uniqueness that is branch-specific must include `BranchId`; uniqueness that is company-wide must not.

## For GL, Sales and Inventory

Journals, invoices and stock movements are branch-owned. Each must classify every new entity explicitly as tenant-global or branch-owned; there is no default, and the failure mode of getting it wrong is silent — an entity that should have been branch-scoped and was not is readable by every branch in the tenant, and nothing about it looks wrong.

## For reporting

Reports gain a scope choice: current branch, or an explicitly authorized branch set. "All branches" must always mean *all branches currently authorized to this user*. It must never be implemented by omitting the `BranchId` predicate.

## For tenant storage and cutover

`Branch` is copied during shared-to-dedicated cutover because it is tenant-owned and the manifest is derived from the model (`ADR-020`). No manifest edit was required and none should be added. `UserBranchAccess` stays in the platform database and is not part of the tenant copy.

## For authentication

The session record gains one nullable column. No `BranchId` claim is added to any token: a claim would be a client-presentable assertion of scope, and would survive revocation until the token expired.

## For the UI

A branch indicator and switcher are required wherever branch-owned work happens. Security is **not** enforced by the switcher — APIs requiring branch context refuse when `ActiveBranchId` is missing, regardless of what the client renders.

## For future service extraction

`BranchId` as a `Guid` cross-module identifier (`ADR-013`) keeps modules referencing branches through stable identifiers and contracts, preserving the extraction path in `ADR-001`.

---

# Decision Drivers

- Correctness: branch is a real partition, not a soft filter.
- Server-side authority: scope must never be client-assertable.
- Freshness: authorization re-evaluated at write time, not captured at login.
- Reuse of the proven tenant machinery rather than a parallel one.
- Compatibility with the platform/tenant database split and with cutover.
- Onboarding reachability: a tenant with zero branches must be able to create its first.

---

# Alternatives Considered

## Option 1 – Branch as an authorization filter only, no `BranchId` column

### Advantages

- No schema dimension to carry; no migration for existing records.

### Disadvantages

- Cannot physically partition branch data; every future report and every future write depends on remembering to filter. Unsafe for GL and Inventory. Rejected.

## Option 2 – `BranchId` as a token claim

### Advantages

- Available everywhere the token is, with no database read per write.

### Disadvantages

- Makes branch scope a client-presentable assertion, and survives revocation until the token expires. Directly contradicts decision 9. Rejected.

## Option 3 – Fan out `UserBranchAccess` rows for Tenant Administrators

### Advantages

- One uniform code path for resolving scope.

### Disadvantages

- Requires synchronization on every branch creation, and would have to exist before the first branch does — unsatisfiable during onboarding. Rejected.

## Option 4 – Add `BranchId` to `ITenantOwnedEntity`

### Advantages

- One interface, one filter.

### Disadvantages

- Forces a branch dimension onto tenant-global records that have none, including `Branch` itself. Rejected.

## Option 5 – Separate `IBranchOwnedEntity`, server-resolved context, write-time reauthorization (Selected)

### Advantages

- Real partition; scope never client-assertable; revocation takes effect at the next write; tenant-global data and onboarding are unaffected.

### Disadvantages

- A platform-database read per save that touches branch-owned entities; two ownership interfaces to reason about. Accepted.

---

# Rationale

The selected model is the only one that makes Branch a true partition while keeping the decision of *which* branch entirely on the server and *whether* that branch is still permitted continuously fresh.

Treating Branch exactly as Tenant and Company are treated — root type not self-scoped, children carrying the parent identifier — keeps the mental model uniform with `ADR-014` and lets branch scoping reuse the tested tenant machinery instead of duplicating it.

The cost is one platform-database read on saves that touch branch-owned entities. That cost buys the property that matters most here: a user whose access was revoked one second ago cannot complete a write, and no cached or client-supplied value can make them able to.

---

# Implementation Guidelines

- `Branch : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity`. Do not implement `IBranchOwnedEntity` on it.
- A branch-owned business entity implements **both** `ITenantOwnedEntity` and `IBranchOwnedEntity` and carries both identifiers.
- Never accept `BranchId` from a request DTO. Let `TenantDbContext` stamp it.
- Never add a `BranchId` claim to any token.
- Resolve branch scope only through `ITenantBranchAccessResolver`. Do not re-derive it in a handler.
- Take `IBranchTopologyGuard` before reading topology in any operation that can change branch assignments or branch active state.
- Branch retirement is deactivation. There is no delete, and there will not be one: a branch identifier is referenced from the platform database and from every document produced while it was active.
- `RowVersion` is required on branch update and deactivation requests, never optional.

---

# Compliance Rules

- Every tenant entity is explicitly classified as tenant-global or branch-owned. Unclassified is a defect.
- No migration may introduce a foreign key whose principal table is `Branches` from the platform database. This is asserted by an architecture guard.
- Branch-owned writes obtain `BranchId` from the server execution context only.
- `ActiveBranchId` is never treated as authorization proof.
- Branch-scoped queries carry an explicit `BranchId` predicate over the current branch or an authorized branch set. Omitting the predicate is a defect, not an optimization.
- Branch errors never disclose database topology or the existence of another tenant's branch.
- An active normal tenant user always has at least one active authorized branch.

---

# Risks

| Risk | Mitigation |
|------|------------|
| A new branch-owned entity is not classified, and is silently readable by every branch | Explicit-classification rule plus architecture guard requiring every tenant entity to be classified |
| Wiring defect between `TenantDbContext` and `IBranchWriteAuthorizer` goes unnoticed | LOW-1 obligation: Employee must prove the authorizer is actually invoked, not merely that it works |
| A future report omits the branch predicate | Decision 22 recorded as a forward rule; executable guard mandated in the first reporting slice |
| Cross-database drift between `UserBranchAccess.BranchId` and `Branch` | Application validates assignability against the tenant database under the topology lease before writing |
| Revoked access keeps writing for the life of an access token | Authorization re-asked from authoritative state on every branch-owned save; no branch claim in tokens |
| Deactivation strands a user or leaves a tenant with no main branch | Topology lease plus refusals: only-active-branch, replacement-main-required, would-strand-users |

---

# Future Considerations

Revisit this ADR when:

- the first production `IBranchOwnedEntity` lands and decisions 10/16/18 become runtime-proven;
- branch-scoped reporting is designed and decision 22 gains an executable guard;
- cross-branch business operations (transfers, consolidations) are required;
- branch hierarchies (a branch owning sub-branches) are requested;
- the per-save platform-database read becomes a measured bottleneck;
- physical branch isolation or Row-Level Security becomes a requirement.

---

# Related Documents

- ADR-005 – Multi-Tenancy (Platform → Tenant → Business Data)
- ADR-008 – Entity Framework Core (query filters, restricted deletes)
- ADR-013 – Primary Key & Identifier Strategy (`BranchId` = `Guid`)
- ADR-014 – Company / Legal-Entity Ownership and Scoping (sibling dimension; root-type precedent)
- ADR-015 – Platform-Plane Authentication and Authorization
- ADR-017 – Tenant Storage Topology and Routing (platform/tenant split; no cross-catalog FK)
- ADR-020 – Shared-to-Dedicated Tenant Migration and Cutover (tenant-owned copy manifest)
- `docs/02-Functional/Platform/Branch-Management.md`
- `docs/14-Engineering/Architecture-Principles.md` – Principle 11
- BR-PLT-0009 … BR-PLT-0016
- REQ-PLT-0060 … REQ-PLT-0067

---

# Review Criteria

This ADR should be reviewed when:

- The first branch-owned business entity is introduced.
- Branch-scoped reporting is implemented.
- Branch administration is exposed over HTTP or in the UI.
- Cross-branch operations or branch hierarchies are requested.
- Tenant storage topology changes in a way that affects the platform/tenant split.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-18 | Solution Architecture Team | Establishes the tenant branch model, authorization and execution context. Records the twenty-two decisions with their implementation status and deferred obligations. |

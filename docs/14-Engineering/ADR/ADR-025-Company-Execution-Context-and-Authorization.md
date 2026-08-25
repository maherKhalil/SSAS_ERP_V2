---
id: ADR-025
title: Company Execution Context and Authorization
category: Architecture Decision Record
version: 1.2
status: Accepted
date: 2026-08-25
owner: Solution Architecture Team
tags:
  - multi-tenancy
  - company
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
  - ADR-018
  - ADR-020
  - ADR-023
used_by:
  - Platform
  - HR
  - GL
---

# ADR-025: Company Execution Context and Authorization

---

# Status

**Accepted** — 2026-08-25.

Proposed alongside the FP-006 Employee design review, as the companion to `ADR-024`.

This ADR **supersedes the deferred and unselected portions of `ADR-014`**: its decision 7 (company scope-resolution mechanism deferred and unchosen), its decision 8 (user↔company authorization deferred), and the company query-filter machinery sketched in its decision 6. `ADR-014` remains the authority on Company *ownership* — that Company is tenant-owned, is a true data-partition dimension, is the company root, and that `CompanyId` is a `Guid`. Those decisions are unchanged.

It was Proposed on the stated ground that **no production code implemented it yet: `ICompanyOwnedEntity` did not exist, and `Employee` would be its first consumer.** That ground has lapsed exactly as written — `ICompanyOwnedEntity` ships in `SSAS.BuildingBlocks.Domain`, and FP-006 records `Employee` as "the **first production consumer** of that interface", introducing the company-ownership machinery `ADR-014` decision 6 deferred until "the first real company-owned business record".

**Evidence — inference from use, not a recorded activation.** This ADR states no acceptance precondition, so its acceptance is inferred and is named as an inference (`DEC-L-020`). The inference is that FP-006's `decisions-approved.md` declares the architecture recorded here "binding for FP-006 implementation", and that `CompanyOwnershipArchitectureTests`, `CompanyArchitectureTests` and `DepartmentApplicationArchitectureTests` assert it executably. What is absent is any closed decision saying this ADR is accepted.

It also removes a standing inconsistency: this record supersedes deferred portions of `ADR-014`, which is itself `Accepted`, and a `Proposed` record superseding an `Accepted` one is not a coherent pairing.

---

# Context

`ADR-014` established Company as a legal-entity data-partition dimension beneath the tenant, and deliberately deferred everything needed to *use* it:

> **Decision 6** — Company-ownership machinery is deferred until the first real company-owned business record exists (expected in HR).
>
> **Decision 7** — Company status is validated live and is never trusted from a token claim. The company scope-resolution mechanism is deferred and unchosen; no specific mechanism, including any `company_id` claim, is mandated.
>
> **Decision 8** — User↔Company authorization is deferred. FP-005 Milestone 1 defines no company-membership or company-level access control.

That deferral was correct: building company-scoping infrastructure before a company-owned record existed would have produced unused machinery. FP-006 is the slice that ends it. `Employee` is company-owned — `BR-HR-0001` requires an Employee Number unique **within a company**, and the Glossary defines an Employee as *a person employed by a Company* — so an Employee cannot be created without a trusted `CompanyId`, and it cannot be read safely without knowing which companies the caller may see.

`BR-PLT-0002` has meanwhile been binding all along:

> A company user shall only access companies explicitly assigned to them.

Nothing implements it. While Company was the only company-scoped concept, that gap was inert. The moment company-owned business data exists, it becomes load-bearing.

Two implemented facts constrain the answer, and one documented statement in `ADR-014` is now stale:

- **`Company` lives in the tenant database.** It moved there through the documented Company transition migrations (`ADR-018`), is configured as `tenant.Companies`, and the current platform model snapshot contains no `Companies` table. `ADR-014`'s *For HR* section still says a future Employee would reference `platform.Companies` through a restricted foreign key; that placement changed when Company moved to the tenant catalog. The correction is recorded in `ADR-014` revision 1.1. Because `Employee` is also tenant-database-owned, Employee→Company is an **intra-catalog** restricted foreign key and is legal.
- **`ADR-023` established the branch dimension**, with a proven pattern for exactly this problem one dimension over: user-access rows in the platform database, no cross-catalog foreign key, one resolver as the single source of truth, live re-authorization, and explicit predicates rather than a global read filter.
- **`ADR-017`** makes a physical foreign key between the platform and tenant catalogs impossible once a tenant is promoted to dedicated storage.

Company and Branch are **sibling dimensions beneath the tenant**, not nested (`ADR-023`). A company is a legal entity; a branch is an operating location. A business record may be scoped by either, both, or neither.

---

# Problem Statement

Define the company execution context and authorization model such that:

- a company-owned record's `CompanyId` is trusted, not asserted by a client;
- `BR-PLT-0002` is actually enforced, rather than documented;
- company scope is resolved from live state, so revocation takes effect without waiting for a token to expire;
- the model works across the platform/tenant database split without a cross-catalog foreign key;
- authorized multi-company reads remain expressible, rather than being designed out by a global filter;
- functional permission and company scope stay independent, so neither silently implies the other;
- the smallest mechanism that satisfies the above is chosen, with no speculative organizational-hierarchy features.

---

# Decision

1. **`ICompanyOwnedEntity` is the marker for company-owned business entities.** It declares `Guid CompanyId`, follows the repository's existing ownership-interface conventions, and is implemented **in addition to** `ITenantOwnedEntity`. Implementing it is a deliberate classification, never a default. `Company` itself does not implement it (`ADR-014` decision 3). `Employee` is the first production consumer.
2. **`ICurrentCompany` is the company execution context.** It exposes a nullable `Guid? CompanyId`, mirroring `ICurrentBranch`. **Null is not an error at that layer** — it is the answer to "has a company been selected". The write and read boundaries turn it into a refusal, and only for company-owned data; tenant-global and branch-only work is unaffected. The selected company is established **per request**. A `CompanyId` supplied by a caller expresses **intent only**, never authorization.
3. **A company selection is trusted only after a live five-step validation.** In order: the trusted tenant is known; the company exists; the company belongs to that tenant; the company is `Active`; the caller is currently authorized for that company. It **fails closed** at every step. Unauthorized, inactive, wrong-tenant and nonexistent identifiers all return **one generic refusal** that discloses nothing about existence — the same reasoning as `ADR-023`'s error semantics, so that an administrator of one tenant cannot probe another tenant's company identifiers.
4. **No company authority is carried in a token.** The `company_id` JWT claim, `ICurrentUser.CompanyId`, and any header or body `CompanyId` are **never** authorization proof. No company authorization is cached into a token: a claim would be a client-presentable assertion of scope and would survive revocation until the token expired. `ADR-014` documented the existing `ICurrentUser.CompanyId` plumbing as plumbing only; this ADR makes that binding.
5. **`UserCompanyAccess` is Platform-DB-owned.** It lives in the platform database with the user it authorizes, carries `TenantId`, `TenantUserId` and `CompanyId`, and is unique on `(TenantId, TenantUserId, CompanyId)`. There is **no cross-database foreign key** to `tenant.Companies`; `CompanyId` is an opaque cross-database identifier (`ADR-013`), and existence, tenant ownership and active state are validated by the application against the tenant database before any assignment row is written. It is **not** `ITenantOwnedEntity`: the global tenant query filter would hide these rows from the paths that must read them; `TenantId` is retained as a trusted column and every query filters on it explicitly.
6. **`ITenantCompanyAccessResolver` is the single source of truth for company authorization.** It answers two questions — the permitted **active** companies for a user, and whether one specific company is authorized — resolved against live state on every request and every company-owned write. Method signatures follow the conventions of `ITenantBranchAccessResolver`. No caller re-derives company scope.
7. **`Platform.Tenant.Administer` grants implicit scope over all active companies of the current tenant**, derived from authority rather than stored rows. No `UserCompanyAccess` rows are materialized for a tenant administrator: the first administrator must be able to create the first company before any company-access row can exist, and rows would need synchronizing on every company created. It grants **no** HR or other module functional permission.
8. **Functional permission, company scope and branch scope are three independent authorization dimensions.** For a company-and-branch-owned entity such as `Employee`, an operation requires **all three**, and none substitutes for another. Holding a functional permission grants no company or branch scope. Holding company or branch scope grants no operation.
9. **The company write boundary mirrors the proven branch philosophy.** Company-owned writes stamp `CompanyId` on insert from the trusted context; a caller-supplied value is **confirmed, never trusted**, and refused if it does not match; post-creation `CompanyId` change is refused; modification and deletion of a record owned by another company are refused; and company authorization is re-asked against live state on the write.
10. **Company-scoped reads use explicit authorized-company predicates, and there is no global current-company query filter.** A global filter pinned to one company would make authorized multi-company reads unexpressible, and would defeat the reason `ADR-014` gives for carrying `TenantId` alongside `CompanyId`. "All companies" means **all companies currently authorized to the requesting user**, materialized as an explicit predicate. A read is never produced by omitting the `CompanyId` predicate. An **executable architecture guard** is required, analogous to `ADR-023` decision 22, asserting that company-scoped predicates cannot be omitted; it must be written in the same slice as the first company-scoped read.

> **Correction A (revision 1.1) — the guard required by this decision now exists.**
>
> `FP-006C4` delivered the first company-scoped reads (`GetEmployee`, `SearchEmployees`, `GetEmployeeBranchHistory`) and the guard in the same slice, as this decision requires.
>
> Company scope is carried by `AuthorizedCompanyScope` inside an `EmployeeReadScope`, which every read requires as its first, non-optional parameter and which only `EmployeeScopeResolver` can construct. `AllAuthorizedCompanies` is materialized into an identifier list before any query is composed, so the predicate is an `IN` list in both modes and omission has no representation; an empty authorized set refuses the read rather than degrading to unfiltered.
>
> The guard is `tests/Architecture.Tests/EmployeeReadScopeArchitectureTests.cs`. It asserts against the composed EF model — with the HR contributor applied — that **no** global query filter references `CompanyId` or `BranchId`, and that the tenant filter is still present; the remaining tests assert properties of the type system rather than of naming, so none can be satisfied by a rename or a comment. Real-SQL proofs `R1`–`R23` in `EmployeeBoundarySqlServerTests` prove the same behaviour against SQL Server, seeding a sibling company in the *same* branch so the company predicate is load-bearing rather than incidental.

11. **Durable company selection is deferred.** No `ActiveCompanyId` is added to the session record in V1. Per-request selection makes it unnecessary, and adding a nullable session column later is additive and changes no business schema.
12. **No company topology lock is introduced.** `BranchTopologyLock` exists because the branch invariant — that an active normal user always retains at least one active branch — spans two databases and cannot be held by a transaction. No equivalent company invariant exists: no authority requires a user to hold at least one company, so zero authorized companies is simply no company-owned access, refused at the operation. Adding a second application lock without that justification would add contention and a failure mode for nothing.

---

# Ownership and authorization placement

```
Tenant   (Guid TenantId)                              -- tenant root; not ITenantOwnedEntity
  ├── Company  (Guid CompanyId, Guid TenantId)        -- ITenantOwnedEntity; NOT ICompanyOwnedEntity
  ├── Branch   (Guid BranchId,  Guid TenantId)        -- ITenantOwnedEntity; NOT IBranchOwnedEntity
  └── Business entity                                 -- ITenantOwnedEntity [+ ICompanyOwnedEntity] [+ IBranchOwnedEntity]
```

| Record | Database | Schema/table | Owns |
|--------|----------|--------------|------|
| `Company` | Tenant ERP | `tenant.Companies` | The legal entity, with the business data it scopes |
| `UserCompanyAccess` | Platform | `platform.UserCompanyAccess` | Which companies a tenant user may act within |

**Why `UserCompanyAccess` is in the platform database.** It authorizes a *user*, and `TenantUser` is in the platform catalog, so the foreign key that matters is intra-catalog. It also keeps company scope resolvable on the plane that stays available while a tenant database is mid-cutover or unreachable — the same reasoning that placed `UserBranchAccess` there.

**Why there is no foreign key to `tenant.Companies`.** A physical constraint across catalogs is impossible once a tenant is promoted to dedicated storage (`ADR-017`), exactly as for `UserBranchAccess.BranchId`.

## Company and branch are independent

A branch is an operating location of the **tenant**, not of a company. There is therefore **no cross-validation** between the selected company and the active branch, and none must be added. A user may legitimately be authorized for companies A and B while working in branch X, and an `Employee` carries both identifiers because it is owned along both dimensions independently.

---

# The three dimensions

```
functional permission        HR.Employees.Create        -- which OPERATION
        AND
authorized company scope     ITenantCompanyAccessResolver   -- which LEGAL ENTITY
        AND
authorized branch scope      ITenantBranchAccessResolver    -- which OPERATING LOCATION
```

Each is resolved by its own mechanism and refused independently. The failure mode this structure prevents is the one `ADR-023` already names for branch: deriving scope from a functional permission, or inferring a permission from scope, fuses two dimensions that exist to stay apart.

`Platform.Tenant.Administer` widens the **company set** and the **branch set**, and nothing else. A tenant administrator without `HR.Employees.Create` cannot create an employee.

---

# Consequences

## For HR

FP-006 introduces `ICompanyOwnedEntity`, `ICurrentCompany`, `UserCompanyAccess`, `ITenantCompanyAccessResolver`, and the company write boundary — the machinery `ADR-014` decision 6 deferred to "the first company-owned business record". `Employee` carries `TenantId`, `CompanyId` and `BranchId`, and Employee-number uniqueness is scoped `(TenantId, CompanyId, NormalizedEmployeeNumber)` with no `BranchId` participation.

Employee→Company is an **intra-catalog restricted foreign key** within the tenant database, which is possible only because Company now lives there.

## For GL

The chart of accounts, fiscal calendar and journals are company-owned and inherit this model unchanged. GL does not need to design company scoping; it needs to classify its entities.

## For Platform

`BR-PLT-0002` gains an enforcement mechanism for the first time. Company-access administration reuses the existing `Platform.Companies.Manage` permission; no new administration permission is introduced in V1.

## For reporting

Reports over company-owned data scope to the selected company or to an explicitly authorized company set, and never by omitting the `CompanyId` predicate — the same rule `BR-PLT-0016` and `ADR-023` decision 22 impose for branch. `BR-RPT-0002` is satisfied by construction rather than by discipline.

## For tenant storage and cutover

`UserCompanyAccess` stays in the platform database and is **not** part of the tenant copy, exactly as `UserBranchAccess` is not. Company-owned tenant entities are copied because they are tenant-owned.

## For authentication

No token changes. No company claim is added, and no session column is added in V1.

## Negative consequences

- A platform-database read is required to resolve company scope on requests that touch company-owned data, in addition to the existing branch read.
- Three ownership interfaces and three authorization dimensions must be reasoned about together.
- Explicit predicates place the burden on every query author, which is why the architecture guard in decision 10 is mandatory rather than advisory.

---

# Decision Drivers

- Correctness: company is a real partition, and `BR-PLT-0002` must be enforced rather than documented.
- Server-side authority: company scope must never be client-assertable.
- Freshness: authorization re-evaluated per request and at write time, failing closed.
- Reuse of the proven branch machinery rather than a parallel invention.
- Expressiveness: authorized multi-company reads must remain possible.
- Minimalism: the smallest mechanism that satisfies the rules; no speculative hierarchy.
- Compatibility with the platform/tenant split and with cutover.

---

# Alternatives Considered

## Option 1 – `company_id` token claim

### Advantages

- Available everywhere the token is, with no database read per request.

### Disadvantages

- Makes company scope a client-presentable assertion and survives revocation until the token expires. Directly contradicts `ADR-014` decision 7's live-validation invariant, and repeats the mistake `ADR-023` rejected for branch. Rejected.

## Option 2 – Durable session `ActiveCompanyId`, mirroring `ActiveBranchId`

### Advantages

- Uniform with the branch model; a single stored selection per session.

### Disadvantages

- Requires a selection flow, a switching operation, and a session column before any need for them is demonstrated. Branch needed durable selection because a branch-owned write takes `BranchId` from context with no request input at all; a company-owned write already names its company. Deferred, not rejected — decision 11 keeps the path open.

## Option 3 – Global current-company EF query filter, mirroring the tenant filter

### Advantages

- One filter; company scoping cannot be forgotten by a query author.

### Disadvantages

- Pins every read to exactly one company, making authorized multi-company reads and tenant-administrator cross-company queries unexpressible — the very capability `ADR-014` cites as a reason to carry `TenantId` alongside `CompanyId`. This is the machinery `ADR-014` decision 6 sketched, and it is superseded here. Rejected.

## Option 4 – `UserCompanyAccess` in the tenant database, beside `Company`

### Advantages

- A real foreign key to `tenant.Companies` becomes possible.

### Disadvantages

- Loses the intra-catalog foreign key to `TenantUser`, which is the relationship that actually needs enforcing, and moves a user-authorization fact onto the plane that may be unavailable during cutover. Rejected.

## Option 5 – Per-request validated company context, platform-side access rows, explicit predicates (Selected)

### Advantages

- Company scope never client-assertable; revocation effective at the next request; multi-company reads expressible; reuses the proven branch pattern; no unused selection infrastructure.

### Disadvantages

- A platform-database read per company-owned request, and explicit predicates that require a guard. Accepted.

---

# Rationale

The selected model is the branch model applied one dimension over, minus the parts branch needed and company does not.

It reuses what `ADR-023` proved: user-access rows on the platform plane with an opaque cross-database identifier, one resolver that both the read path and the write path consult so they cannot disagree, live re-authorization that fails closed, and explicit predicates instead of a global filter. Those choices were made under exactly the same constraints — the platform/tenant split, cutover, and the need for an authorized *set* rather than a single value — and there is no reason for company to answer them differently.

It omits what branch needed for its own reasons: a durable session selection, a topology lock, and a minimum-one invariant. Each of those exists because a branch-owned write has no request-level input for its branch and because leaving a user with no branch is an integrity failure. Neither is true of company, so importing them would be uniformity for its own sake.

The one place this ADR diverges from `ADR-014`'s sketch — no global company query filter — is the place where `ADR-014` was written before the authorized-set requirement was understood. Recording that divergence as a decision, with the superseded option preserved as Option 3, is the point of a separate ADR rather than a silent reinterpretation.

---

# Implementation Guidelines

- `ICompanyOwnedEntity` declares `Guid CompanyId` following the repository's existing ownership-interface conventions.
- A company-owned business entity implements **both** `ITenantOwnedEntity` and `ICompanyOwnedEntity` and carries both identifiers.
- Never accept `CompanyId` as authorization. Accept it as intent, then validate the five steps in decision 3.
- Never read `ICurrentUser.CompanyId` or a `company_id` claim in a handler.
- Resolve company scope only through `ITenantCompanyAccessResolver`. Do not re-derive it.
- Validate a company against the tenant database before writing any `UserCompanyAccess` row.
- Company-scoped queries carry an explicit `CompanyId` predicate over the selected company or an authorized company set.
- Every tenant entity is explicitly classified as company-owned or company-neutral, as it already is for branch.
- Company errors disclose no database topology and no cross-tenant company existence.

---

# Compliance Rules

- Company-owned writes obtain `CompanyId` from the trusted company context only; a supplied value is confirmed, never trusted.
- `CompanyId` is immutable after creation.
- No cross-database foreign key exists between `platform.UserCompanyAccess` and `tenant.Companies`.
- No `CompanyId` claim is added to any token, and no company authorization is cached in one.
- Company-scoped reads carry an explicit `CompanyId` predicate; omitting it is a defect, not an optimization.
- "All companies" always means all companies currently authorized to the requesting user.
- Functional permission, company scope and branch scope are checked independently; none implies another.
- `Platform.Tenant.Administer` grants company and branch scope only, never functional authority.
- A user with zero authorized active companies is refused company-owned operations, never defaulted to all.

---

# Risks

| Risk | Mitigation |
|------|------------|
| A company-scoped query omits the predicate and silently returns every company's data | Explicit-predicate rule plus the mandatory executable architecture guard in decision 10 |
| A new business entity is not classified and is silently readable across companies | Explicit-classification rule mirroring the branch guard |
| Revoked company access keeps working for the life of an access token | Authorization re-asked from authoritative state per request and per write; no company claim in tokens |
| A caller probes another tenant's company identifiers for existence | One generic refusal for nonexistent, wrong-tenant, inactive and unauthorized alike |
| Cross-database drift between `UserCompanyAccess.CompanyId` and `Company` | Application validates against the tenant database before writing an assignment row |
| Company scope and functional permission are fused by a future shortcut | Decision 8 recorded as an invariant; the `Platform.Tenant.Administer` catalog entry already states the same rule for branch |
| The deferred session selection is assumed to exist | Decision 11 records it as deferred and additive |

---

# Future Considerations

Revisit this ADR when:

- durable company selection or a company switcher is required in the UI;
- a user↔company assignment administration surface is exposed over HTTP;
- company-scoped reporting is designed and the decision-10 guard is written;
- a company hierarchy, company groups, or consolidation across companies is requested;
- the per-request platform-database read becomes a measured bottleneck;
- Row-Level Security or physical company isolation becomes a requirement;
- a business entity is required to span more than one company.

---

# Related Documents

- ADR-005 – Multi-Tenancy (Platform → Tenant → Business Data)
- ADR-008 – Entity Framework Core (query filters, restricted deletes)
- ADR-013 – Primary Key & Identifier Strategy (`CompanyId` = `Guid`)
- ADR-014 – Company / Legal-Entity Ownership and Scoping (superseded in part; see revision 1.1)
- ADR-015 – Platform-Plane Authentication and Authorization
- ADR-017 – Tenant Storage Topology and Routing (platform/tenant split; no cross-catalog FK)
- ADR-018 – Tenant Schema Health and Migration Orchestration (the Company platform → tenant transition)
- ADR-020 – Shared-to-Dedicated Tenant Migration and Cutover
- ADR-023 – Tenant Branch Model, Authorization and Execution Context (sibling dimension; pattern precedent)
- ADR-024 – Employee Branch Assignment and Transfer Model
- FP-005 – Company / Legal-Entity feature package
- `docs/14-Engineering/Architecture-Principles.md` – Principle 11
- BR-PLT-0002, BR-PLT-0016, BR-RPT-0001, BR-RPT-0002, BR-HR-0001

---

# Review Criteria

This ADR should be reviewed when:

- The first company-owned business entity is implemented.
- Company-scoped reads or reporting are implemented.
- User↔company assignment is exposed over HTTP or in the UI.
- Durable company selection is introduced.
- Company hierarchies, groups, or consolidation are requested.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-18 | Solution Architecture Team | Establishes the company execution context and authorization model. Supersedes ADR-014 decisions 7 and 8 and the company query-filter machinery sketched in ADR-014 decision 6. |
| 1.1 | 2026-08-19 | Solution Architecture Team | Correction A: the executable architecture guard required by decision 10 exists as of `FP-006C4`, delivered in the same slice as the first company-scoped reads. Enforcement is structural — an unforgeable resolved scope that every read requires — and is proven by 19 architecture guards, including one asserting that no global query filter on the composed tenant model references `CompanyId` or `BranchId`, plus real-SQL proofs `R1`–`R23`. No decision text changed. |
| 1.2 | 2026-08-25 | Solution Architecture Team | Status corrected from `Proposed` to **Accepted**. No decision changed. It was Proposed on the stated ground that `ICompanyOwnedEntity` did not yet exist and `Employee` would be its first consumer; both have since happened. Acceptance is an **inference** from that use rather than a recorded activation — this ADR states no acceptance precondition (`DEC-L-020`). Also resolves a `Proposed` record superseding the `Accepted` `ADR-014`. |

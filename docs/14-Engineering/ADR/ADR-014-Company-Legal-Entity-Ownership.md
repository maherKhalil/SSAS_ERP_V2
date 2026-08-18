---
id: ADR-014
title: Company / Legal-Entity Ownership and Scoping
category: Architecture Decision Record
version: 1.1
status: Accepted
date: 2026-08-09
owner: Solution Architecture Team
tags:
  - multi-tenancy
  - company
  - legal-entity
  - ownership
  - scoping
  - architecture
depends_on:
  - ADR-005
  - ADR-008
  - ADR-010
  - ADR-013
used_by:
  - Platform
  - HR
  - GL
---

# ADR-014: Company / Legal-Entity Ownership and Scoping

---

# Status

**Accepted**

Accepted alongside the FP-005 documentation package. It establishes the ownership and scoping model that FP-005 implements and that HR and GL will consume.

## Partially superseded by ADR-025 (revision 1.1)

The **ownership** decisions in this ADR stand unchanged: Company is tenant-owned, is a true data-partition dimension, is the company root and does not implement `ICompanyOwnedEntity`, `ITenantOwnedEntity` is unmodified, and `CompanyId` is a `Guid`.

The decisions this ADR deliberately **deferred or left unchosen** have since been made by `ADR-025 — Company Execution Context and Authorization`, because FP-006 introduces `Employee` as the first company-owned business record:

| This ADR | Status |
|----------|--------|
| Decision 6 — company-ownership machinery deferred | **Discharged** by `ADR-025`. The company query-filter portion is **superseded**: see *Correction D* below |
| Decision 7 — company scope-resolution mechanism deferred and unchosen | **Superseded** by `ADR-025` decisions 2, 3 and 4 |
| Decision 8 — user↔company authorization deferred | **Superseded** by `ADR-025` decisions 5, 6, 7 and 8 |

The original text of those decisions is retained below, unedited, so the deferral and its reasoning stay visible. Where a later decision replaces one, an inline note names `ADR-025` at the point of replacement rather than rewriting the original.

Revision 1.1 also corrects one factual statement about database placement that changed after this ADR was written; see *Correction A* in the **For HR** section.

---

# Context

`ADR-005` defines the platform hierarchy:

```
Platform
  └── Tenant
        └── Company        (a legal entity owned by a tenant)
              └── Business Data
```

The Tenant layer is implemented (FP-001, FP-002, FP-003). The **Company** layer does not yet exist in code. HR and GL are the next business modules, and both require a company / legal entity to anchor their data:

- HR employees, departments, and positions belong to a company; `BR-HR-0001` requires an Employee Number unique **within a company**.
- GL chart of accounts, fiscal calendar, and journals belong to a legal entity; `REQ-PLT-0012` states each company maintains independent fiscal settings, currencies, language, and numbering.

The implemented persistence layer already provides strong tenant isolation for any type implementing `ITenantOwnedEntity`:

- a global EF Core query filter on `TenantId` (`PersistenceDbContext`);
- write-side `AssignTenant` (server-assigned `TenantId`, rejection of a mismatched trusted context);
- rejection of any post-creation `TenantId` change;
- global `DeleteBehavior.Restrict`;
- audit-metadata stamping and Platform history/deletion guards.

`ITenantOwnedEntity` currently declares only `Guid TenantId`. There is no company-scoping interface.

A decision is required now because whether Company is a real data-partition dimension, and whether future records carry a `CompanyId`, is a one-way-door choice that would be expensive to retrofit after HR and GL data exists.

---

# Problem Statement

Define the ownership and scoping model for Company and for future company-owned business records, in a way that:

- reuses the existing tenant-isolation machinery rather than inventing a parallel one;
- does not pollute tenant-wide Platform records with a company dimension they do not have;
- does not build unused generic company-filtering infrastructure before a company-owned record exists;
- keeps a clean path to future service extraction.

---

# Decision

1. **Company is always tenant-owned.** `Company` implements `ITenantOwnedEntity`, physically carries `TenantId`, and inherits all existing tenant read/write isolation rules unchanged.
2. **Company is a true data-partition dimension** beneath the tenant — not merely an authorization filter. Future company-owned business records physically carry `CompanyId` and are partitioned by it.
3. **Company does not implement `ICompanyOwnedEntity`.** Company is the company *root*; it is scoped by tenant, not by company. This mirrors how `Tenant` is the tenant root and does not implement `ITenantOwnedEntity`.
4. **`ITenantOwnedEntity` remains unchanged.** `CompanyId` is **not** added to it.
5. **`ICompanyOwnedEntity` is a separate, future, opt-in interface** declaring `Guid CompanyId`. Future company-owned business records implement **both** `ITenantOwnedEntity` and `ICompanyOwnedEntity`.
6. **Company-ownership machinery is deferred** until the first real company-owned business record exists (expected in HR). FP-005 Milestone 1 introduces no company query filter, no company write-guard, and no current-company persistence.
7. **Company status is validated live** and is never trusted from a token claim. The company scope-resolution mechanism is deferred and unchosen (see *Current company context*); no specific mechanism, including any `company_id` claim, is mandated.
8. **User↔Company authorization is deferred.** FP-005 Milestone 1 defines no company-membership or company-level access control.
9. **No Row-Level Security** is introduced for company scoping in Milestone 1.
10. **No `CompanyId` is added to existing tenant-wide Platform records.**

---

# Ownership hierarchy

```
Tenant   (Guid TenantId)                     -- tenant root; not ITenantOwnedEntity
  └── Company   (Guid CompanyId, Guid TenantId)   -- ITenantOwnedEntity; NOT ICompanyOwnedEntity
        └── Company-owned business entity          -- ITenantOwnedEntity + ICompanyOwnedEntity  (future: HR/GL)
              (Guid TenantId, Guid CompanyId, own PK)
```

- `Company` is filtered and write-guarded by `TenantId` through the existing machinery.
- A future company-owned record carries **both** `TenantId` and `CompanyId`, and is filtered and write-guarded by **both** dimensions once the company machinery is added.

Carrying both identifiers is deliberate: `CompanyId` implies a tenant, but storing `TenantId` too preserves the existing tenant filter, enables tenant-admin cross-company queries within a tenant, and reuses the proven tenant machinery without special cases.

---

# ITenantOwnedEntity remains unchanged

`ITenantOwnedEntity { Guid TenantId }` is not modified. Adding `CompanyId` to it would force a company dimension onto tenant-wide records that have none, and would couple every tenant-owned type to a concept most of them do not use.

---

# ICompanyOwnedEntity is a separate future concept

A future interface, introduced with the first company-owned record:

```csharp
public interface ICompanyOwnedEntity
{
    Guid CompanyId { get; }
}
```

When that record arrives, the persistence layer gains a parallel company query filter and a company write-guard that mirror the existing tenant filter and `AssignTenant` behavior (assign on insert from trusted current company context, reject post-creation change, require the company to belong to the current tenant). FP-005 Milestone 1 does **not** add this machinery, to avoid unused infrastructure.

> **Correction D (revision 1.1) — the query-filter half of this sketch is superseded by `ADR-025` decision 10.**
>
> The **write-guard** half is adopted as written: `ADR-025` decision 9 stamps `CompanyId` on insert from the trusted context, refuses post-creation change, and refuses cross-company modification and deletion.
>
> The **parallel global company query filter** is **not** adopted. A global filter pinned to a single current company would make authorized *multi-company* reads unexpressible, and would defeat the cross-company query capability this ADR itself cites as a reason to carry `TenantId` alongside `CompanyId`. `ADR-025` instead requires explicit authorized-company predicates plus an executable architecture guard, mirroring the choice `ADR-023` decision 22 made for branch.
>
> The original reasoning is retained because it was sound for the information available at the time: the authorized-*set* requirement only became visible when company-scoped business reads were designed in FP-006. `ADR-025` records the rejected global filter as its Option 3.

---

# Tenant-wide records that remain Company-neutral

The following implemented Platform records are tenant-wide or platform-level and must **not** receive a `CompanyId`:

- `Identity`
- `TenantUser`
- `Role`
- `RolePermissionAssignment`
- `TenantUserRoleAssignment`
- `AuthenticationAccount`
- `AuthenticationSession`
- `RefreshTokenRecord`
- `AccountActionToken`
- `TenantSelectionTransaction`
- `Tenant` (tenant root; not even tenant-owned)
- `Company` (company root; tenant-owned, but not company-owned)
- `PermissionDefinition`
- `LocalizationCatalogState`, `TenantLocalizationSettings`, `TenantLocalizationOverride`, `TenantLocalizationOverrideVersion`

Users, roles, permissions, and localization are tenant-scoped concerns. A user's access to specific companies is a future many-to-many assignment, not a column on these records.

---

# Current company context

The company scope-resolution mechanism is **deferred**. This ADR does not choose among the possible future implementations, which may include:

- a token scope claim;
- a request-selected scope validated server-side;
- a membership-backed scope;
- trusted route/context resolution.

Whichever mechanism is later chosen, these invariants are mandatory:

- company **status is validated live** and is never trusted solely from a token claim;
- the company must belong to the trusted current tenant;
- the caller must be authorized for the company;
- company lifecycle/status is never inferred from client-supplied claims.

The existing `ICurrentUser.CompanyId` and `JwtClaimTypes.CompanyId = "company_id"` are documented here only as **existing plumbing**; their presence is not a commitment to a claim-based mechanism. FP-005 Milestone 1 implements no company selection and populates no company scope.

> **Correction B (revision 1.1) — the mechanism is no longer deferred; `ADR-025` chooses it.**
>
> `ADR-025` selects **per-request, server-validated company selection**: a caller-supplied `CompanyId` expresses intent only, and the trusted context (`ICurrentCompany`) is established only after a live five-step validation — trusted tenant known, company exists, belongs to that tenant, is active, and the caller is currently authorized for it — failing closed at every step, with one generic refusal that discloses nothing about existence.
>
> Of the four mechanisms this section listed as possible, the **token scope claim** is explicitly **rejected** (`ADR-025` decision 4 and Option 1): it would make company scope client-presentable and would survive revocation until the token expired. `ICurrentUser.CompanyId` and the `company_id` claim remain plumbing and must **never** be read as authorization proof — `ADR-025` decision 4 makes that binding rather than advisory.
>
> A **durable session `ActiveCompanyId`** is deferred, not rejected (`ADR-025` decision 11, Option 2); adding it later is additive.
>
> Every invariant this section declared mandatory is preserved by `ADR-025` unchanged.

---

# Authorization

- Company lifecycle and administration are authorized by explicit Platform company permissions (FP-005) evaluated within the trusted current tenant.
- A company operation always targets a company **belonging to the current tenant**; a company identifier from another tenant must never reveal existence.
- User↔company access control (which users may act within which companies, `BR-PLT-0002`) is deferred; Milestone 1 defines no company-membership model.

> **Correction C (revision 1.1) — user↔company authorization is no longer deferred; `ADR-025` defines it.**
>
> `BR-PLT-0002` gains an enforcement mechanism with FP-006, because `Employee` is the first company-owned business record and the gap becomes load-bearing the moment such data exists.
>
> `ADR-025` introduces `UserCompanyAccess` in the **platform** database — carrying `TenantId`, `TenantUserId`, `CompanyId`, unique on `(TenantId, TenantUserId, CompanyId)`, with **no** cross-database foreign key to `tenant.Companies` (`ADR-017`, `ADR-013`) — and `ITenantCompanyAccessResolver` as the single source of truth, resolved against live state per request and per write.
>
> `Platform.Tenant.Administer` grants implicit scope over all **active** companies of the tenant with no assignment rows, mirroring `ADR-023` decision 5 for branch. It grants **no** module functional permission.
>
> Company administration continues to be authorized by the Platform company permissions this section names; `ADR-025` adds no new administration permission in V1. Functional permission, company scope and branch scope remain three independent dimensions (`ADR-025` decision 8).

---

# Consequences

## For HR

HR employee/department/position aggregates will implement `ITenantOwnedEntity` + `ICompanyOwnedEntity`, carry `TenantId` + `CompanyId`, and reference `platform.Companies(CompanyId)` via a restricted foreign key. Employee-number uniqueness is scoped `(TenantId, CompanyId, ...)`. HR is expected to introduce the company-ownership machinery.

> **Correction A (revision 1.1) — the foreign-key target above is stale.**
>
> When this ADR was written, `Company` lived in the platform catalog. It has since moved to the tenant ERP database through the documented Company transition migrations (`ADR-018`), which rename `platform.Companies` to `platform.Companies_MigratedToTenant` and create and populate `tenant.Companies`. `Company` is configured as **`tenant.Companies`**, and the current platform model snapshot contains no `Companies` table. A future Employee therefore references **`tenant.Companies(CompanyId)`**, not `platform.Companies(CompanyId)`.
>
> This is a **factual correction**, not a change of decision. It is favourable: because `Employee` also lives in the tenant database, Employee→Company is an **intra-catalog** restricted foreign key and is legal. Had Company remained in the platform catalog, the reference would have been impossible once a tenant is promoted to dedicated storage (`ADR-017`), for the same reason `UserBranchAccess` has no foreign key to `Branch` (`ADR-023` decision 4).
>
> The company-ownership machinery this section anticipates is specified by `ADR-025` and delivered by FP-006. Employee-number uniqueness remains scoped `(TenantId, CompanyId, ...)` as stated, and deliberately does not include `BranchId` (`ADR-023`, *For HR*).

## For GL

GL chart of accounts, fiscal calendar, and journals are company-owned. The company's **base currency** (`BaseCurrencyCode`, captured on `Company` as Platform-owned configuration) is the currency GL builds on; GL owns the *functional-currency accounting semantics* (rates, revaluation, restatement) without changing the Platform Company ownership boundary. GL depends on Company existing first.

## For query filters

The existing tenant filter is unchanged and applies to `Company`. A company filter is added only with the first company-owned record and composes with the tenant filter (both must match).

## For write guards

`Company` reuses `AssignTenant` and the immutable-`TenantId` guard. A company write-guard is added with the first company-owned record.

## For authorization

Platform company permissions gate company administration now; company-scoped business authorization arrives with the user↔company assignment feature.

## For future service extraction

`CompanyId` as a `Guid` cross-module identifier (`ADR-013`) keeps HR and GL referencing companies through stable identifiers and contracts, preserving the extraction path in `ADR-001`.

---

# Decision Drivers

- Reuse of proven tenant isolation instead of a parallel mechanism.
- Correctness: company is a real partition, not a soft filter.
- Minimalism: no unused company infrastructure before it is needed.
- Clean boundaries for HR, GL, and future extraction.

---

# Alternatives Considered

## Option 1 – Company as an authorization filter only (no CompanyId column)

### Advantages

- No schema dimension to carry.

### Disadvantages

- Cannot physically partition or enforce company data boundaries; unsafe for GL. Rejected.

## Option 2 – Add CompanyId to ITenantOwnedEntity

### Advantages

- One interface.

### Disadvantages

- Forces a company dimension onto tenant-wide records that have none; couples unrelated types. Rejected.

## Option 3 – Separate ICompanyOwnedEntity, machinery deferred (Selected)

### Advantages

- Reuses tenant machinery; keeps tenant-wide records clean; adds company filtering only when a company-owned record exists.

### Disadvantages

- Two interfaces and, later, two filters to reason about. Accepted.

---

# Rationale

The selected model is the only one that makes Company a true partition while reusing the existing, tested tenant machinery and avoiding premature infrastructure. Treating Company exactly as Tenant is treated one level down (root type not self-scoped; children carry the parent identifier) keeps the mental model and the code uniform.

---

# Implementation Guidelines

- `Company : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity`.
- Do not implement `ICompanyOwnedEntity` on `Company`.
- Do not add company filter/write-guard machinery in FP-005 Milestone 1.
- Introduce `ICompanyOwnedEntity`, the company filter, and the company write-guard together with the first company-owned business record. *(Revision 1.1: superseded in part — `ADR-025` decision 10 replaces "the company filter" with explicit authorized-company predicates plus an architecture guard. See Correction D.)*
- Validate company status live; never trust a company status claim.

---

# Compliance Rules

- Company carries and is isolated by `TenantId` using the existing rules.
- No `CompanyId` is added to tenant-wide Platform records.
- Future company-owned records implement both ownership interfaces and carry both identifiers.
- Company operations never expose a cross-tenant company's existence.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Premature company-filter infrastructure | Defer machinery until the first company-owned record; assert absence in architecture tests |
| Company data leaking across tenants | Company implements ITenantOwnedEntity and reuses tenant filter + write guard; SQL and architecture tests |
| Misclassifying a tenant-wide record as company-owned | Explicit company-neutral list in this ADR; review checklist |

---

# Future Considerations

Revisit when the first company-owned record is introduced (company machinery), when user↔company assignment is defined, when a company-selection/claim flow is designed, or if physical company isolation is ever required.

---

# Related Documents

- ADR-005 – Multi-Tenancy (Tenant → Company → Business Data)
- ADR-008 – Entity Framework Core (query filters, restricted deletes)
- ADR-010 – Repository Pattern
- ADR-013 – Primary Key & Identifier Strategy (CompanyId = Guid)
- ADR-017 – Tenant Storage Topology and Routing (platform/tenant split; no cross-catalog FK)
- ADR-018 – Tenant Schema Health and Migration Orchestration (the Company platform → tenant transition; see Correction A)
- ADR-023 – Tenant Branch Model, Authorization and Execution Context (sibling dimension)
- ADR-025 – Company Execution Context and Authorization (supersedes decisions 7 and 8, and the query-filter portion of decision 6)
- FP-003 – Tenant Lifecycle (root-type/not-self-scoped precedent)
- FP-005 – Company / Legal-Entity feature package
- REQ-PLT-0010, REQ-PLT-0011, REQ-PLT-0012, BR-PLT-0002

---

# Review Criteria

This ADR should be reviewed when:

- The first company-owned business record is introduced.
- User↔company assignment or company-selection is designed.
- Physical company isolation or RLS becomes a requirement.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-09 | Solution Architecture Team | Establishes Company ownership and scoping. Accepted after final approval review. |
| 1.1 | 2026-08-18 | Solution Architecture Team | Correction A: the Employee foreign-key target is `tenant.Companies`, not `platform.Companies`, following Company's move to the tenant catalog; Employee→Company is intra-catalog. Corrections B and C: decisions 7 and 8 superseded by ADR-025. Correction D: the global company query filter sketched in decision 6 superseded by ADR-025 decision 10 (explicit predicates plus architecture guard); the company write-guard half is adopted. Ownership decisions 1–5, 9 and 10 unchanged. Original text retained throughout; corrections recorded inline. |

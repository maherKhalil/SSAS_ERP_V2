---
id: ADR-015
title: Platform-Plane Authentication and Authorization
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-08-10
owner: Solution Architecture Team
tags:
  - authentication
  - authorization
  - security
  - platform-plane
  - multi-tenancy
  - identity
depends_on:
  - ADR-005
  - ADR-006
  - ADR-013
  - ADR-014
used_by:
  - Platform
  - FP-003
  - FP-001
---

# ADR-015: Platform-Plane Authentication and Authorization

---

# Status

**Accepted**

Accepted alongside the FP-003 platform-plane authorization documentation package, following the same direct-approval convention used by `ADR-014`. It resolves the platform-support authentication and authorization decision that FP-003 `DEC-TEN-0012` explicitly deferred, and establishes the security-plane model that FP-003 Tenant administration and future approved platform-support operations will implement.

---

# Context

`ADR-005` defines the platform hierarchy:

```
Platform
  └── Tenant
        └── Company
              └── Business Data
```

`ADR-006` established JWT bearer authentication with claims-based authorization. `ADR-014` established that `Tenant` is the tenant root and, like `Company` one level down, is **not** self-scoped: `Tenant` does not implement `ITenantOwnedEntity`, receives no tenant query filter, and receives no automatic `TenantId` assignment from `ICurrentTenant`.

FP-003 delivered the Tenant lifecycle backend (Domain, Application, SQL Server, tests) and operates on the **platform plane**: every Tenant command/query handler gates on `ApplicationExecutionContext.GetPlatformActor(currentUser)`, none inject `ICurrentTenant`, and the route/command `TenantId` is a *target aggregate identifier only*. FP-003 deliberately shipped **no HTTP endpoint** because the mechanism by which a platform-support principal authenticates and is authorized was undefined.

The existing token and identity model, as implemented today, is strictly tenant-bound:

- Every validated access token **must** carry a `tenant_id` critical claim (`StrictAccessTokenValidator`); `AccessTokenIssuer` rejects issuance unless `TenantId != Guid.Empty`, `TenantUserId > 0`, and `client_id == ssas-erp-web`.
- Permission claims are derived **only** from a `TenantUser`'s tenant-owned role assignments (`AccessTokenClaimsProvider`).
- `Role.AssignPermission` **rejects** any permission whose scope is not `PermissionScope.Tenant`; roles are always tenant-owned; there is no global role.
- `PermissionScope.PlatformSupport` exists as an enum value but is **reserved and unreachable** — no permission is authored with it and nothing can assign it.
- The person/login already exists as two **global, non-tenant-owned** aggregates: `Identity` and `AuthenticationAccount`. `TenantUser` is the tenant binding of an `Identity`. There is **no** existing platform-administrator / super-admin / global-role construct, and **no** MFA and **no** role/permission seeding exist today.

Related decisions: `ADR-005` (Platform administration), `ADR-006` (JWT/claims, optional future MFA), `ADR-013` (Guid identifiers), `ADR-014` (Tenant/Company root-not-self-scoped), FP-001 (platform-support authorization plane, deferred), FP-003 (`DEC-TEN-0012`, `DEC-TEN-0017`).

---

# Problem Statement

Managing the `Tenant` aggregate is a platform responsibility that has no caller-tenant scope. The tenant-plane authorization pipeline (`PermissionAuthorizationHandler`) requires a validated tenant claim, a live-eligible caller tenant, and a tenant-derived permission claim — all of which are conceptually wrong for administering Tenants, and structurally impossible for a principal that legitimately has no tenant.

A decision is required now because:

- FP-003 cannot expose any Tenant HTTP endpoint until platform-support authentication and authorization are defined;
- an incorrect choice (faking a tenant, reusing the target `TenantId` as scope, or letting tenant roles carry platform authority) would create a privilege-escalation path or permanently blur the meaning of `tenant_id`;
- the escalation-prevention invariants must be fixed before any platform permission exists.

Desired outcome: a platform authorization plane that is explicitly separate from the tenant plane, cannot be reached from tenant IAM, does not weaken the tenant token contract, and works even when the target Tenant is not Active.

---

# Decision

1. **Two explicit security planes — Tenant and Platform.** They have distinct token shapes, distinct permission scopes, and distinct authorization handlers. Tenant-plane behavior is unchanged.

2. **Tenant tokens remain tenant-bound.** `tenant_id` remains a required critical claim; current-tenant validation and eligibility remain unchanged; tenant permissions remain `PermissionScope.Tenant`, derived from tenant-owned roles. This is a hard regression boundary.

3. **Platform-support tokens are non-tenant-scoped.** A platform-support token represents a global platform-support principal and carries **no** `tenant_id` and **no** `tenant_user_id`. It must not fake a system tenant, use `Guid.Empty`, reuse the target `TenantId`, or select an arbitrary tenant to obtain access.

4. **The platform token profile is explicitly distinguishable from the tenant profile** by a server-issued, validated, non-user-editable, non-tenant-derived claim — `security_plane` (snake_case, consistent with the existing custom-claim convention). The tenant profile requires `tenant_id` present and `security_plane` absent-or-`tenant`; the platform profile requires `security_plane=platform` and `tenant_id` **absent**. A token combining `security_plane=platform` with `tenant_id` is invalid and must be rejected. The two shapes are mutually exclusive.

5. **`PlatformSupport` permissions cannot belong to tenant roles.** `Role.AssignPermission`'s rejection of any non-`Tenant`-scoped permission is a permanent security invariant. It applies to tenant custom roles, tenant system roles, and role-permission assignments alike. Tenant administrators cannot self-grant platform authority.

6. **Platform permissions come from a separate global platform authority**, never from `TenantUser`/`TenantRole` membership. Platform-token permission claims are sourced only from platform-level assignments; tenant-token permission claims are sourced only from tenant-scoped role assignments.

7. **Global `PlatformSupportPrincipal` authority is anchored to the existing global `Identity`.** The authority is a new global, non-tenant-owned, independently revocable, auditable, least-privilege assignment. A code/config-owned bootstrap allow-list may create only the genesis platform-support principal; the bootstrap is not the long-term permission-management model.

8. **A dedicated platform permission authorization handler** (`PlatformPermissionAuthorizationHandler`, invoked through `RequirePlatformPermission(...)`) enforces the platform plane. It requires (1) an authenticated principal, (2) a validated platform token profile, and (3) the exact required `PlatformSupport` permission. It must not require `ICurrentTenant`, `TenantAuthorizationContext`, caller-tenant eligibility, or target-tenant Active status. The existing tenant handler is left semantically unchanged; no single ambiguous dual-plane handler is introduced.

9. **Target `TenantId` is never caller scope.** In `/api/platform/tenants/{tenantId}` the identifier is only the target aggregate to administer. It never becomes a JWT tenant claim, `ICurrentTenant`, or business-data context. Platform authorization occurs independently of target lifecycle status; a platform principal must be able to administer `Provisioning` and `Suspended` tenants where the lifecycle operation permits (required for activation and reactivation). Target lifecycle validity remains Domain/Application-owned.

10. **Existing tenant-plane authorization remains unchanged.** FP-005 Company, Localization, and tenant-scoped IAM continue to use `RequirePermission(...)`, the existing `PermissionAuthorizationHandler`, validated current tenant, and `PermissionScope.Tenant`. No platform-support change may weaken them.

11. **Strong authentication / MFA is required before Production platform-support access.** Mandatory MFA/strong-auth is a production-readiness requirement of the platform-support token-issuance contract. It does not block implementing the platform-plane architecture in development/test. The Production issuance flow must not enable platform-support access without the approved strong-auth control. This ADR does not design the MFA subsystem.

12. **Future approved platform-support operations may reuse the Platform plane.** FP-001's proposed `/api/platform/support/tenants/{tenantId}/...` routes and any later platform operations reuse this plane rather than inventing a parallel mechanism. They are not part of FP-003 implementation.

## Defense-in-depth requirement

Even though `Role.AssignPermission` already makes a `PlatformSupport` assignment on a tenant role impossible, the tenant-token claim path (`AccessTokenClaimsProvider`) must **explicitly filter emitted permissions to `PermissionScope.Tenant`**. This protects against corrupt data, direct database changes, bad migration seeds, and future bypass code.

## Approved platform permissions (initial, FP-003)

| Permission | Scope | Routes |
|---|---|---|
| `Platform.Tenants.View` | `PermissionScope.PlatformSupport` | `GET /api/platform/tenants`, `GET /api/platform/tenants/{tenantId}` |
| `Platform.Tenants.Manage` | `PermissionScope.PlatformSupport` | `POST /api/platform/tenants` |
| `Platform.Tenants.Lifecycle` | `PermissionScope.PlatformSupport` | `POST /api/platform/tenants/{tenantId}/{activate\|suspend\|reactivate\|archive}` |

---

# Decision Drivers

- Security: prevent tenant-admin privilege escalation and keep `tenant_id` meaning precise.
- Correctness: platform administration genuinely has no caller-tenant scope.
- Least privilege: fine-grained platform permissions rather than a coarse admin bypass.
- Reuse: anchor to the existing global `Identity`; reuse signing/issuer/middleware and the reserved `PlatformSupport` scope.
- Non-regression: tenant-plane token and authorization contracts untouched.
- Extensibility: one reusable platform plane for future support operations and future external IdPs.

---

# Alternatives Considered

## Option 1 – Fake / system tenant (or `Guid.Empty`) for platform tokens

### Advantages

- Reuses the existing tenant-bound token unchanged.

### Disadvantages

- Overloads `tenant_id` with a second meaning, risks a real tenant collision, and invites a token that could establish `ICurrentTenant` for business data. Rejected.

## Option 2 – Target `TenantId` (or an arbitrarily selected tenant) as caller scope

### Advantages

- No new token shape.

### Disadvantages

- Turns a target identifier into trusted scope, breaks administration of non-Active targets, and is exactly the tenant-override anti-pattern FP-003 forbids. Rejected.

## Option 3 – Ordinary tenant roles carry `PlatformSupport` permissions / coarse SystemAdmin bypass

### Advantages

- Uses the existing role and permission plumbing.

### Disadvantages

- Directly enables tenant-admin self-escalation, contradicts the `Role.AssignPermission` invariant, and abandons least privilege. Rejected.

## Option 4 – Single ambiguous dual-plane authorization handler; platform claims from tenant role assignments

### Advantages

- One handler and one claim source.

### Disadvantages

- Concentrates both planes' security in the most sensitive class, where a future edit can leak tenant relaxations into platform checks; and sourcing platform claims from tenant roles reintroduces the escalation path. Rejected.

## Option 5 (Selected) – Dedicated platform-support authentication profile and authorization plane

### Advantages

- Explicit, non-tenant-scoped platform token; separate global authority anchored to `Identity`; dedicated handler; tenant plane untouched; escalation structurally impossible.

### Disadvantages

- New global authority, token-profile and session-representation changes, a migration, and a stronger security-test matrix. Accepted.

---

# Rationale

Option 5 is the only alternative that gives platform permissions a real, non-tenant issuance path without corrupting the tenant token contract, faking a scope, or opening an escalation route. It fits the codebase as it already is: `Identity` and `AuthenticationAccount` are already global, `PermissionScope.PlatformSupport` is already reserved, and the `Role.AssignPermission` guard is already the escalation control. Treating platform administration as its own plane — with its own token shape and handler — mirrors how the domain already separates the global `Identity` from the tenant-scoped `TenantUser`.

---

# Consequences

## Positive

- `tenant_id` retains exactly one meaning: trusted tenant business-data scope.
- Least-privilege platform authorization (View / Manage / Lifecycle) with no god-mode bypass.
- Tenant-admin escalation to platform authority is structurally impossible.
- FP-003 gains a clean, testable authorization foundation.
- A single reusable platform plane serves future approved support operations.
- FP-005 Company, Localization, and tenant IAM security are unaffected.

## Negative / Costs

- A new global platform-principal authority and platform permission assignments.
- Token-profile changes (`AccessTokenIssuer`, `StrictAccessTokenValidator`, `AccessTokenClaimsProvider`) and a platform-capable session representation.
- An EF migration and SQL verification for the new global tables.
- A stronger security-test matrix (plane confusion, escalation regression, target-status independence).
- A Production dependency on mandatory MFA/strong-auth before platform-support enablement.

---

# Implementation Guidelines

- Add a `PermissionScope`-aware overload to the permission catalog and author `Platform.Tenants.View/Manage/Lifecycle` as `PermissionScope.PlatformSupport`.
- Wire the `security_plane` claim end-to-end (claims source → issuer + guard → validator critical claims → current-user reader), mirroring fully-wired claims such as `tenant_id` and avoiding the half-wired `company_id` pattern.
- Introduce `RequirePlatformPermission(...)` and `PlatformPermissionAuthorizationHandler`; do not add permission logic to `ApplicationExecutionContext.GetPlatformActor`, which remains Application-layer defense in depth (it asserts a trusted actor identity exists; it is not authorization).
- Keep the tenant handler, the tenant token contract, and `StrictAccessTokenValidator`'s tenant profile unchanged; add the platform profile beside them.
- Add the `AccessTokenClaimsProvider` tenant-path `PermissionScope.Tenant` filter.
- Do not implement any of this in the documentation task; implementation follows the FP-003 phase plan.

# Compliance Rules

- Tenant roles may hold only `PermissionScope.Tenant` permissions; `PlatformSupport` permissions are never assignable to any tenant role or assignment.
- Platform tokens carry `security_plane=platform` and never `tenant_id`/`tenant_user_id`; a token combining them is rejected.
- Platform authorization never depends on `ICurrentTenant`, caller-tenant eligibility, or target-tenant status.
- Route `{tenantId}` on platform routes is a target identifier only and never establishes `ICurrentTenant`.
- Production platform-support token issuance requires the approved strong-auth/MFA control.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Tenant admin self-grants platform authority | `Role.AssignPermission` non-`Tenant`-scope rejection; permissions authored `PlatformSupport` |
| Corrupt/seeded platform assignment leaks into a tenant token | `AccessTokenClaimsProvider` explicit `PermissionScope.Tenant` filter (defense in depth) |
| Tenant token accepted on a platform route (or vice versa) | Mutually exclusive validated token profiles keyed on `security_plane` and `tenant_id` presence |
| Platform token gains implicit tenant business-data access | No `tenant_id` ⇒ no `ICurrentTenant`; tenant filters exclude; platform permissions grant no business read |
| Non-Active target blocks legitimate administration | Authorization is target-status-independent; source-state validity is the domain transition graph |
| Highly privileged platform access without strong auth | Mandatory MFA/strong-auth as a Production-readiness gate |
| Platform principal cannot be revoked | Assignment is independently revocable; `SecurityVersion` + session revocation invalidate live tokens |

---

# Future Considerations

Revisit when: platform-support operations beyond FP-003 are approved (reuse this plane); an external identity provider becomes the platform-support authority source; the MFA/strong-auth subsystem is designed; or platform-session refresh/re-authentication policy is finalized.

---

# Related Documents

- ADR-005 – Multi-Tenancy (Platform → Tenant → Company)
- ADR-006 – JWT Authentication and Claims-Based Authorization (MFA compatibility)
- ADR-013 – Primary Key & Identifier Strategy
- ADR-014 – Company / Legal-Entity Ownership and Scoping (root-type / not-self-scoped precedent)
- FP-003 – Tenant Lifecycle (`authorization-model.md`, `api-contracts.md`, `decisions-approved.md`, `DEC-TEN-0012`, `DEC-TEN-0017`)
- FP-001 – Identity & Access (platform-support authorization plane)

---

# Review Criteria

This ADR should be reviewed when:

- A platform-support operation beyond FP-003 Tenant administration is approved.
- An external identity provider is adopted for platform-support authentication.
- The MFA/strong-auth control or platform-session refresh policy is designed.
- Any change is proposed to the tenant-plane token contract.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-10 | Solution Architecture Team | Establishes the Tenant/Platform security-plane model and resolves FP-003 `DEC-TEN-0012`. Accepted after final approval review. |

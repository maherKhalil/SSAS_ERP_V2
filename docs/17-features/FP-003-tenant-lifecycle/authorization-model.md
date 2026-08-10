---
document_id: FP-003-AUTH
title: Tenant Lifecycle Authorization Model
status: Approved for Implementation
version: 1.2
sprint: Sprint-01
module: Platform
---

# Authorization Model

## Authorization plane

Tenant lifecycle operations are **platform-plane** operations, governed by `ADR-015` (Platform-Plane Authentication and Authorization). The system has two explicit security planes: the **tenant plane** (FP-005 Company, Localization, tenant-scoped IAM, and future tenant-owned modules) and the **platform plane** (FP-003 Tenant administration and future approved platform-support operations). Tenant-plane behavior is unchanged.

An ordinary tenant role, including a role named Administrator, does not authorize creating, activating, suspending, reactivating, archiving, listing, or administering Tenant lifecycle records.

App Owner/App Support concrete support routes, mandatory MFA subsystem, impersonation, and target-tenant support workflows remain the scope of FP-001/FP-002 features. The **authorization-plane decision** those features depend on is now resolved by `ADR-015` and is shared with FP-003; only the concrete support endpoints and the MFA subsystem remain deferred to their own packages.

## Platform security plane

The platform plane is defined by `ADR-015`:

- **Authentication.** A platform-support principal authenticates through a dedicated platform-support token profile carrying `security_plane=platform`. The platform token carries **no** `tenant_id` and **no** `tenant_user_id`; it never fakes a system tenant, uses `Guid.Empty`, reuses the target `TenantId`, or selects an arbitrary tenant. Tenant and platform token shapes are mutually exclusive, and a token combining `security_plane=platform` with `tenant_id` is rejected.
- **Principal authority.** Platform authority is a global, non-tenant-owned `PlatformSupportPrincipal` assignment anchored to the existing global `Identity`. It is independently revocable, auditable, and least-privilege. A code/config-owned bootstrap allow-list may create only the genesis platform-support principal. Tenant administrators cannot create or modify this authority through tenant IAM.
- **Permissions.** Platform permissions are `PermissionScope.PlatformSupport` and are sourced only from platform-level assignments, never from tenant-owned roles.
- **Authorization.** Platform routes use `RequirePlatformPermission(...)`, enforced by a dedicated `PlatformPermissionAuthorizationHandler` that requires (1) an authenticated principal, (2) a validated platform token profile, and (3) the exact required `PlatformSupport` permission. It does **not** require `ICurrentTenant`, `TenantAuthorizationContext`, caller-tenant eligibility, or target-tenant Active status.

## Permission escalation invariant

Tenant roles may contain only `PermissionScope.Tenant` permissions. `PlatformSupport` permissions must never be assignable to tenant custom roles, tenant system roles, or tenant role-permission assignments. The existing `Role.AssignPermission` scope rejection is a permanent part of the security boundary, so a tenant administrator cannot self-grant `Platform.Tenants.*`.

As defense in depth, the tenant token claim generation path (`AccessTokenClaimsProvider`) must explicitly filter emitted permissions to `PermissionScope.Tenant`, even though an invalid `PlatformSupport` assignment on a tenant role should already be impossible. This protects against corrupt data, direct database changes, bad migration seeds, and future bypass code.

## Platform-support bootstrap, lifecycle, and authority administration

Governed by `ADR-016` and recorded in `decisions-approved.md` as `DEC-TEN-0019`, `DEC-TEN-0020`, and `DEC-TEN-0021`. Not implemented yet; these govern the phases that make platform authority issuable into usable tokens.

### Genesis bootstrap (DEC-TEN-0019)

A configuration-owned bootstrap allow-list keyed by the immutable `AuthenticationSubject` authorizes **only** the genesis/recovery creation of platform authority. The configured subject must resolve to an existing `Identity` with an authentication-capable, active `AuthenticationAccount`; bootstrap never creates identities, is never per-request authorization, and no tenant role can invoke it.

**Usable platform authority** exists when at least one `PlatformSupportPrincipal` simultaneously (1) has `Status == Active`, (2) is anchored to a valid authentication-capable identity/account, and (3) has at least one active assignment whose permission exists in `IPermissionCatalog` with `PermissionScope.PlatformSupport`. A revoked assignment, a corrupt tenant-scoped row, an unknown permission, and a `Disabled` principal do not count.

Bootstrap is genesis/recovery-only, audited, non-tenant-editable, and inert once usable authority exists. It must not implicitly re-enable a `Disabled` principal — configuration membership is never equivalent to "always platform-authorized" or to re-enable authority. Manual SQL is not the recovery path.

"No usable platform authority exists" is evaluated **live against persisted current state** (principal `Status`, authentication-account eligibility, active persisted assignments, current `IPermissionCatalog` scope) — never from configuration, a cached flag, a bare principal row, or corrupt rows. The bootstrap allow-list may hold multiple unique `AuthenticationSubject` values, but a single evaluation establishes **exactly one** usable genesis/recovery principal: eligible subjects are sorted by ordinal comparison of the canonical `AuthenticationSubject` and the first is selected; remaining subjects stay recovery candidates with no automatic authority. Concurrent instances converge on one principal through the Phase-2 unique `IdentityId`/active-assignment constraints. Recovery creates a **new** `Active` principal only for an eligible configured subject that owns no principal; if a `Disabled` principal is the only configured subject and no other candidate is eligible, bootstrap **fails closed** with an operator diagnostic and recovery requires an additional configured subject or the explicit Re-enable operation. The genesis/recovery grant set contains only `PermissionScope.PlatformSupport` catalog permissions and includes `Platform.Support.Administer` (once authored).

### Principal lifecycle (DEC-TEN-0020)

`PlatformSupportPrincipalStatus { Active, Disabled }`, default `Active`, transitions `Active ↔ Disabled`. `Disabled` makes all platform authority unusable while retaining assignments; grant is rejected, revoke is allowed, re-enable restores still-active assignments. The status migration adds `Status` `NOT NULL` (default `'Active'`, `CHECK Active/Disabled`) and `StatusChangedUtc`/`StatusChangedBy` `NULLABLE`; principals existing before the migration backfill to `Active` with `StatusChangedUtc`/`StatusChangedBy` `NULL` (no historical transition is synthesized), and the first `Disable`/`Re-enable` populates them. Platform token issuance and refresh perform a **live** principal-status check (mirroring `ITenantAuthenticationEligibilityReadService`) and are denied for `Disabled`. Disabling does not cryptographically invalidate an already-issued short-lived JWT (see `DEC-TEN-0010`); immediate cut-off is via `SecurityVersion` + session revocation (`ADR-015`). `StrictAccessTokenValidator` validates the platform token profile structurally (`security_plane=platform` present, `tenant_id` forbidden) and performs no live DB status lookup.

### Authority administration (DEC-TEN-0021)

Platform-support principal registration, permission grant/revoke, and Disable/Re-enable require the new `Platform.Support.Administer` permission (`PermissionScope.PlatformSupport`) through the future platform-plane authorization layer. `Platform.Tenants.Manage` and `Platform.Tenants.Lifecycle` govern Tenant administration and lifecycle only and never administer `PlatformSupportPrincipal` or `PlatformPermissionAssignment`. Bootstrap is the sole exception, and only before usable platform authority exists.

## Operation classification

| Operation | Required platform permission (`PermissionScope.PlatformSupport`) |
|---|---|
| CreateTenant | `Platform.Tenants.Manage` |
| GetTenant | `Platform.Tenants.View` |
| ListTenants | `Platform.Tenants.View` |
| ActivateTenant | `Platform.Tenants.Lifecycle` |
| SuspendTenant | `Platform.Tenants.Lifecycle` |
| ReactivateTenant | `Platform.Tenants.Lifecycle` |
| ArchiveTenant | `Platform.Tenants.Lifecycle` |
| GetTenantAuthenticationEligibility | Internal trusted Platform authentication/authorization caller; not an end-user permission decision |

The permission identifiers are resolved by `ADR-015`. Each is `PermissionScope.PlatformSupport` and is enforced through `RequirePlatformPermission(...)`. HTTP endpoints remain unimplemented until the platform-plane authorization foundation described in `ADR-015` is delivered (see the FP-003 README implementation-phase note); the first backend milestone contains no HTTP endpoint.

## Authentication-eligibility contract

`ITenantAuthenticationEligibilityReadService` reports current lifecycle fact. It does not authorize the caller, issue claims, validate a tenant role, or grant access to business data.

The caller remains responsible for:

- authenticating the Identity where applicable;
- validating active membership;
- validating session and client state;
- evaluating roles and permissions;
- enforcing tenant-owned repository isolation.

## Target TenantId

Platform lifecycle commands necessarily target a Platform-owned Tenant by TenantId. This is not the same as accepting a tenant override in an ordinary tenant operation.

Requirements:

- the route or command TenantId identifies only the lifecycle aggregate to administer;
- explicit Platform authorization is checked before lifecycle data is disclosed or changed;
- the target cannot establish `ICurrentTenant` for business-data access;
- lifecycle administration does not permit querying another tenant's business tables;
- target and actor metadata are audited through trusted server-side context.

Platform authorization occurs **independently of the target Tenant's lifecycle status**. A platform principal must be able to administer `Provisioning` and `Suspended` tenants where the lifecycle operation permits — this is required for activation and reactivation. Target lifecycle validity (which source states permit which transition) remains Domain/Application-owned and is enforced after authorization, not as a caller-authorization precondition.

## GetPlatformActor is not authorization

`ApplicationExecutionContext.GetPlatformActor` is Application-layer defense in depth: it asserts only that a trusted actor identity exists (a non-empty `UserId`). It is not authorization and carries no permission logic. Transport authorization establishes that the caller is an authorized platform-support principal holding the required `PermissionScope.PlatformSupport` permission. Permission responsibility is not assigned to `ApplicationExecutionContext`.

## Ordinary tenant access

An Active Tenant is only one authorization prerequisite. It grants no membership, role, permission, company access, or Platform-support authority.

For an ineligible Tenant:

- pre-authentication workflows return generic authentication outcomes where FP-002 requires them;
- authenticated business authorization denies ordinary access according to the approved centralized status-enforcement policy;
- lifecycle status is not inferred from a JWT claim supplied by the client.

FP-002 Milestone 4 implements the centralized ordinary-tenant authorization prerequisite through `DEC-AUTH-0057`: every ordinary tenant-scoped authenticated business request performs one scoped live `ITenantAuthenticationEligibilityReadService` lookup and authorizes only current `Active` status. Tenant role and permission policies include this requirement. TenantStatus remains absent from the JWT, and logout uses a separate authenticated policy so a session belonging to a newly suspended tenant can still be revoked.

## Auditing

Lifecycle events contain domain facts. Correlation ID, request ID, trace ID, and authenticated actor metadata remain outside Domain and use the existing event-dispatch metadata boundary.

Immutable security-audit storage is not delivered by FP-003 and remains a production-release dependency.

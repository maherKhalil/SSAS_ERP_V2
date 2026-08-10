---
document_id: FP-003-DEC
title: Approved Tenant Lifecycle Decisions
status: Approved for Implementation
version: 1.1
sprint: Sprint-01
module: Platform
approved_date: 2026-08-01
---

# Approved Decisions

The decisions below are binding for FP-003 implementation. They preserve the existing `DEC-TEN-*` identifiers from the review package.

## DEC-TEN-0001 — Authoritative identifier

Use the existing server-generated, nonempty Guid `TenantId` as the immutable `platform.Tenants` primary key. Do not add a BIGINT Tenant key, maintain a second Tenant identifier, reuse a TenantId, or accept it from a client during creation.

## DEC-TEN-0002 — Status vocabulary

Use exactly `Provisioning`, `Active`, `Suspended`, and `Archived`. Only an existing `Active` Tenant is authentication-eligible; a missing Tenant is ineligible.

## DEC-TEN-0003 — Transition graph

Permit only Create to Provisioning, Provisioning to Active, Provisioning to Archived, Active to Suspended, Active to Archived, Suspended to Active, and Suspended to Archived. Archived is terminal. No transition is triggered automatically by time, inactivity, billing, payment, or subscription state.

## DEC-TEN-0004 — Tenant code

TenantCode is required, immutable, limited to 64 characters, trimmed, and stored with its display casing preserved. Its normalized value is exactly `Trim().ToUpperInvariant()`, with no culture-specific or provider-specific transformation. NormalizedTenantCode is globally unique and its column and unique index use the approved BIN2 collation.

## DEC-TEN-0005 — Tenant name

TenantName is required, limited to 200 characters, trimmed, and stored with its display casing preserved. It is not globally unique and has no NormalizedTenantName solely for uniqueness. TenantName is mutable only through an approved Tenant update operation; such an operation is not part of the first implementation milestone.

## DEC-TEN-0006 — Legal name

LegalName is deferred to a later legal or customer-profile feature and is not part of FP-003.

## DEC-TEN-0007 — Physical deletion

Physical Tenant deletion is prohibited. Provide no delete command, repository method, permission, endpoint, cascade, or routine database operation. Archive is the terminal retained lifecycle operation and supersedes the former Draft `DELETE /api/platform/tenants/{id}` contract.

## DEC-TEN-0008 — Platform aggregate and query filtering

Tenant is a Platform-level aggregate in the existing Platform persistence boundary. It does not implement `ITenantOwnedEntity`, receives no tenant query filter, and receives no automatic TenantId assignment from `ICurrentTenant`. Reading Tenant does not disable isolation filters on tenant-owned data.

## DEC-TEN-0009 — Lifecycle reason metadata

Persist `StatusChangedUtc`, `StatusChangedBy`, and `StatusChangeReasonCode`. The bounded reason codes are exactly `Created`, `ProvisioningCompleted`, `Administrative`, `Security`, `Compliance`, `Operational`, `CustomerClosure`, and `IssueResolved`. Creation records `Created`; every transition records a code, and Suspend and Archive require an explicit non-`Created` code. Safe events contain the code only and no free-form reason text, secrets, or billing detail.

## DEC-TEN-0010 — Already issued access tokens

A Tenant status change does not cryptographically invalidate an already issued short-lived JWT, which may remain valid until expiry. It cannot be refreshed when current Tenant status is ineligible. Tenant selection, session creation, refresh, and operations that validate current status use current eligibility. Before ordinary tenant APIs are production-enabled, a centralized current-status authorization policy must deny non-Active status. High-risk operations use a live current-status check. Middleware, JWT, cache, and invalidation implementation are outside the first milestone.

## DEC-TEN-0011 — Eligibility result shape

The result contains exactly `TenantId`, `Exists`, nullable `TenantStatus`, `IsAuthenticationEligible`, and `TenantAuthenticationIneligibilityReason`. Reason values are exactly `None`, `TenantNotFound`, `Provisioning`, `Suspended`, and `Archived`. A missing Tenant returns false Exists, null status, false eligibility, and TenantNotFound; Active returns true Exists, Active, true eligibility, and None. TenantName is omitted.

## DEC-TEN-0012 — Platform authorization permissions

Create, read, list, activate, suspend, reactivate, and archive are Platform-level operations. Ordinary tenant roles never authorize them, and Platform lifecycle authority grants no tenant business-data access. Exact permission identifiers and Platform-support authentication remain deferred; no Tenant HTTP endpoint is included in the first milestone. The eligibility query is an internal trusted Platform authentication or authorization contract, not an end-user permission decision.

> **Resolution (2026-08-10).** The deferred portion of this decision — the platform-support authentication model and the exact permission identifiers — is now resolved by `ADR-015` (Platform-Plane Authentication and Authorization) and recorded in `DEC-TEN-0018`. The platform-plane classification, the tenant-role prohibition, and the no-business-data-access boundary stated here remain unchanged.

## DEC-TEN-0013 — Legacy TenantId reconciliation

Reconciliation is staged and environment-specific. First inventory distinct legacy TenantIds and produce an operator-reviewed mapping of each TenantId to code, name, and status. Fail on missing or duplicate mappings. Never infer lifecycle state, create placeholder metadata, or silently mark a legacy Tenant Active. Commit no production data or environment-specific mapping to the repository.

## DEC-TEN-0014 — Tenant foreign keys

The first milestone creates the Tenant aggregate, `platform.Tenants`, and its own constraints but does not auto-backfill legacy TenantIds or retrofit all existing foreign keys. After approved reconciliation, a dedicated enforcement migration verifies complete coverage, fails on orphans, and adds restricted foreign keys while preserving composite same-tenant constraints. Existing candidates include TenantUsers, Roles, tenant-user role assignments, role-permission assignments where TenantId is present, and invitation-bound AccountActionTokens. Every new table introduced after FP-003—including session, tenant-authentication, and future module-root tables—has its Tenant foreign key from its first migration.

## DEC-TEN-0015 — Provisioning and first administrator

Creating a Tenant creates only the Tenant in Provisioning. Activation creates no company or first administrator. A later onboarding coordinator may compose those deferred capabilities explicitly. Active status alone does not authenticate a caller without every independent identity, membership, session, and authorization prerequisite.

## DEC-TEN-0016 — Subscription independence

Subscription, billing, and payment state are separate concepts and never implicitly change Tenant status. Any future coupling uses an explicit authorized lifecycle command.

## DEC-TEN-0017 — Immutable audit dependency

FP-003 emits safe audit-ready events through the existing post-commit dispatcher. Immutable audit storage is a separate production-release dependency and is not implemented in the first milestone.

## DEC-TEN-0018 — Platform-plane authentication and authorization

Resolves the platform-support authentication and permission deferral in `DEC-TEN-0012`, governed by `ADR-015`. Binding for the FP-003 Tenant HTTP transport milestone.

- **Security plane.** Tenant administration is platform-plane. The system has two explicit planes — tenant and platform — with distinct token shapes, permission scopes, and authorization handlers. Tenant-plane behavior (FP-005 Company, Localization, tenant IAM) is unchanged.
- **Token profile.** A platform-support principal authenticates through a dedicated platform-support token profile carrying `security_plane=platform`. The platform token carries **no** `tenant_id` and **no** `tenant_user_id`, and never fakes a system tenant, uses `Guid.Empty`, reuses the target `TenantId`, or selects an arbitrary tenant. Tenant and platform token shapes are mutually exclusive; a token combining `security_plane=platform` with `tenant_id` is rejected.
- **Permission identifiers.** `Platform.Tenants.View` (get, list), `Platform.Tenants.Manage` (create), and `Platform.Tenants.Lifecycle` (activate, suspend, reactivate, archive).
- **Permission scope.** All three are `PermissionScope.PlatformSupport`.
- **Separate platform authority.** Platform permissions are sourced only from a global, non-tenant-owned `PlatformSupportPrincipal` assignment anchored to the existing global `Identity`, independently revocable and auditable — never from tenant roles. A code/config-owned bootstrap allow-list may create only the genesis principal. Tenant administrators cannot create or modify this authority through tenant IAM.
- **Escalation invariant.** Tenant roles may hold only `PermissionScope.Tenant` permissions; `Role.AssignPermission`'s rejection of non-`Tenant` scope is a permanent security boundary. The tenant token claim path must additionally filter emitted permissions to `PermissionScope.Tenant` as defense in depth.
- **Dedicated handler.** Platform routes use `RequirePlatformPermission(...)`, enforced by a dedicated `PlatformPermissionAuthorizationHandler` requiring an authenticated principal, a validated platform token profile, and the exact `PlatformSupport` permission. It does not require `ICurrentTenant`, `TenantAuthorizationContext`, caller-tenant eligibility, or target-tenant Active status. The existing tenant handler is unchanged.
- **Target TenantId semantics.** In `/api/platform/tenants/{tenantId}` the identifier is the target aggregate only and never establishes `ICurrentTenant` or caller scope. Authorization is independent of target lifecycle status, so `Provisioning` and `Suspended` targets can be administered where the transition permits.
- **GetPlatformActor.** `ApplicationExecutionContext.GetPlatformActor` remains Application-layer defense in depth asserting a trusted actor identity exists; it is not authorization and receives no permission logic.
- **Production strong authentication.** Mandatory MFA/strong authentication is a production-readiness requirement for platform-support token issuance; the Production issuance flow must not enable platform-support access without it. It does not block implementing the platform-plane architecture in development/test.
- **Audit posture unchanged.** `DEC-TEN-0017` is unchanged: FP-003 emits safe audit-ready post-commit events; immutable audit storage remains a production-release dependency and is not a per-mutation runtime gate.

## Reconciled conflict register

| # | Prior conflict | Approved resolution |
|---|---|---|
| 1 | Draft Tenant Management mixed lifecycle with company, subscription, branding, localization, and notifications | FP-003 is authoritative for lifecycle and authentication eligibility; the other capabilities remain deferred and non-authoritative |
| 2 | Draft physical-delete route and permission | Superseded by terminal Archive under DEC-TEN-0007 |
| 3 | Draft TenantName uniqueness | Superseded by non-unique TenantName under DEC-TEN-0005 |
| 4 | General BIGINT guidance versus established Guid TenantId | Existing Guid TenantId remains authoritative under DEC-TEN-0001 |
| 5 | Existing tenant-owned Platform tables lack a Tenant principal foreign key | Use the staged enforcement process in DEC-TEN-0014 |
| 6 | Legacy TenantIds lack trustworthy lifecycle metadata | Use operator-reviewed, environment-specific reconciliation under DEC-TEN-0013 |
| 7 | JWTs may remain cryptographically valid after suspension | Current-state authorization requirements are fixed by DEC-TEN-0010 |
| 8 | Platform-support permissions are not defined | First milestone stayed internal under DEC-TEN-0012; the platform-support authentication model and permission identifiers are now resolved by ADR-015 and DEC-TEN-0018 |
| 9 | Broad provisioning expected a company and first administrator | Tenant creation remains Provisioning-only under DEC-TEN-0015 |
| 10 | Draft associated subscription with lifecycle | Lifecycle is independent under DEC-TEN-0016 |
| 11 | Immutable audit storage does not yet exist | Emit safe events and retain the production dependency under DEC-TEN-0017 |
| 12 | FP-001 and FP-002 required Tenant status without an implementation source | Approved FP-003 is the source consumed by those workflows |
| 13 | Draft documentation lacked an exact status vocabulary and graph | DEC-TEN-0002 and DEC-TEN-0003 define both exactly |

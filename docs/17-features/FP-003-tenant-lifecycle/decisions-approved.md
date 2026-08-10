---
document_id: FP-003-DEC
title: Approved Tenant Lifecycle Decisions
status: Approved for Implementation
version: 1.2
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

## DEC-TEN-0019 — Platform-support genesis bootstrap

Governed by `ADR-016`. Resolves how the first (and recovery) platform-support authority is trusted before any platform authorization layer exists. Not implemented yet.

- **Trust source.** A configuration-owned bootstrap allow-list keyed by the immutable `AuthenticationSubject` authorizes only the genesis/recovery creation of platform authority. Email, username, display name, `TenantId`, and `IdentityId` are rejected as configuration keys.
- **Prerequisite identity.** The configured subject must resolve to an existing `Identity` with an authentication-capable, active `AuthenticationAccount`. Bootstrap never creates identities.
- **Capability.** Bootstrap may only register/resolve the platform-support principal and establish the initial approved `PermissionScope.PlatformSupport` set. It is never per-request authorization, an implicit super-admin, a tenant-admin path, or a bypass around normal platform authorization once usable authority exists. No tenant role can invoke it.
- **Usable platform authority (definition).** Usable platform authority exists when at least one `PlatformSupportPrincipal` simultaneously (1) has `Status == Active`, (2) is anchored to a valid authentication-capable identity/account, and (3) has at least one active assignment whose permission exists in `IPermissionCatalog` with `PermissionScope.PlatformSupport`. A revoked assignment, a corrupt tenant-scoped row, an unknown permission, and a `Disabled` principal do not count.
- **Lifetime and recovery.** Bootstrap is configuration-controlled, genesis/recovery-only, audited, non-tenant-editable, and inert once usable authority exists. Recovery is eligible only when no usable authority exists; bootstrap must not implicitly re-enable a `Disabled` principal — config membership is never "always authorized" or re-enable authority.
- **Live evaluation.** "No usable platform authority exists" is evaluated live against persisted current state (principal `Status`, authentication-account eligibility, active persisted assignments, current `IPermissionCatalog` scope). It is never inferred from configuration, a cached bootstrap-success flag, the mere presence of a principal row, or corrupt assignment rows.
- **Subject cardinality and deterministic selection.** `PlatformSupportBootstrapOptions` may contain multiple unique configured `AuthenticationSubject` values. Bootstrap does not create a principal for every configured subject; a single evaluation establishes exactly one usable genesis/recovery principal. Eligible subjects (canonical, unique, resolving to an existing `Identity` with an eligible `AuthenticationAccount`, and — for a create — not already owning a principal) are sorted by ordinal comparison of the canonical `AuthenticationSubject`; the first is selected (never configuration insertion order). Remaining configured subjects stay recovery candidates and receive no authority automatically.
- **Concurrent convergence.** Concurrent instances evaluate usable authority live, deterministically select the same first eligible subject, and are bounded by the Phase-2 uniqueness (`UX_PlatformSupportPrincipals_IdentityId`, active-assignment unique index); duplicates are idempotent race outcomes. For the same configuration and persistence state, a bootstrap race establishes exactly one genesis/recovery principal, not one per instance. No distributed lock is introduced.
- **Recovery model.** Recovery never changes `Disabled → Active` and never grants to a `Disabled` principal. It may create a new `Active` recovery principal only for an eligible configured subject that owns no principal. If a `Disabled` principal is the only configured subject with no other eligible candidate, bootstrap fails closed (no re-enable, no duplicate) and emits an operator diagnostic; recovery then requires an additional approved pre-existing `AuthenticationSubject` in configuration or the separately-authorized explicit Re-enable lifecycle operation. An `Active` principal with no active catalog-valid `PlatformSupport` assignment is not usable authority; recovery may establish an eligible configured candidate without mutating the existing principal's assignments. Manual SQL is a governed break-glass activity only, not the designed recovery mechanism.
- **Audit and failure.** Bootstrap reuses the Phase-2 audit fields with a distinguishable actor representation (e.g. `platform-bootstrap:<subject>`); no immutable-audit infrastructure is invented. Missing subject / ineligible account → no authority; existing principal → idempotent; existing assignment → no duplicate; usable authority already exists → no-op; unknown or tenant-scoped permission → fail closed.

## DEC-TEN-0020 — Platform-support principal lifecycle

Governed by `ADR-016`. Adds a minimal principal status so platform authority can be suspended immediately and re-enabled without deleting history or over-broadly disabling the person's tenant access. Not implemented yet.

- **States.** `PlatformSupportPrincipalStatus { Active, Disabled }`, default `Active`; transitions `Active → Disabled` and `Disabled → Active` (both non-terminal). No `Suspended`/`Archived`/`Deleted` at this stage.
- **Semantics.** `Active` evaluates authority from active assignments. `Disabled` makes all platform authority unusable regardless of retained assignments; assignments remain persisted (not deleted, not revoked). Grant while `Disabled` is rejected; revoke while `Disabled` is allowed. Re-enable restores eligibility of still-active retained assignments (it does not recreate revoked ones).
- **Concurrency and audit.** The existing Phase-2 `RowVersion` is authoritative for lifecycle optimistic concurrency. Persist `StatusChangedUtc`/`StatusChangedBy` in addition to `ModifiedUtc/By`; no reason-code is introduced.
- **Migration and backfill.** `Status` is added `NOT NULL` (`nvarchar`, `BIN2` collation, `CHECK Status IN ('Active','Disabled')`, default `'Active'`); `StatusChangedUtc` and `StatusChangedBy` are `NULLABLE`. Any principal existing before the status migration backfills to `Status = 'Active'` with `StatusChangedUtc = NULL` and `StatusChangedBy = NULL` — a schema addition is not a lifecycle transition, so no historical transition is synthesized from `CreatedUtc`/`CreatedBy`. The first actual `Disable`/`Re-enable` populates both, and every subsequent transition overwrites them.
- **Token consequences.** Platform token issuance and refresh perform a live principal-status check (mirroring `ITenantAuthenticationEligibilityReadService`) and are denied for `Disabled`; no token-carried status is authoritative. Disabling does not cryptographically invalidate an already-issued short-lived JWT (consistent with `DEC-TEN-0010`); an existing access token may remain usable until natural expiry or until the `SecurityVersion` + session-revocation mechanism (`ADR-015`) applies. `StrictAccessTokenValidator` validates the platform profile structurally and performs no live DB status lookup. Whether the platform session reuses `AuthenticationAccount.SecurityVersion` + `AuthenticationSession` revocation or a distinct platform-session version is a Phase-3C detail; `PlatformSupportPrincipal` is not assigned its own `SecurityVersion` here.

## DEC-TEN-0021 — Platform-authority administration permission

Governed by `ADR-016`. Defines the permission that governs administering platform authority, so tenant permissions are never repurposed. Not implemented yet.

- **New permission.** `Platform.Support.Administer`, `PermissionScope.PlatformSupport`, governing platform-support principal registration, permission grant, permission revoke, and principal Disable/Re-enable. A single permission covers this small surface; no additional permissions are introduced.
- **Non-repurposing.** `Platform.Tenants.Manage` and `Platform.Tenants.Lifecycle` govern Tenant administration and lifecycle and never authorize administering `PlatformSupportPrincipal` or `PlatformPermissionAssignment`.
- **Bootstrap exception.** Bootstrap is the only exception, and only before usable platform authority exists. Once it exists, Register/Grant/Revoke/Disable/Re-enable require `Platform.Support.Administer` through the future platform-plane authorization layer. The genesis/recovery principal receives `Platform.Support.Administer` in its initial authority set (once authored) to enable the Phase-4 transition to normal self-hosting administration, and may also receive explicitly configured, approved initial `Platform.Tenants.*` permissions; the bootstrap grant set contains only code-catalog permissions with `PermissionScope.PlatformSupport`.
- **Catalog impact.** When authored, the `PlatformSupport` catalog grows from three to four entries. Being `PlatformSupport`-scoped, `Platform.Support.Administer` is excluded from tenant-facing catalog listings and tenant-token claims by the Phase-1 filters; the platform authority read path returns it only when legitimately assigned.

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
| 14 | The first platform-support authority could not be created without a trusted, non-circular, non-tenant mechanism | Genesis/recovery config bootstrap keyed by immutable AuthenticationSubject under DEC-TEN-0019 and ADR-016 |
| 15 | A platform-support principal had no way to be suspended immediately without over-broadly disabling all access | Minimal `Active`/`Disabled` principal status with live status checks under DEC-TEN-0020 and ADR-016 |
| 16 | No permission governed administering platform authority; `Platform.Tenants.Manage` risked being repurposed | New `Platform.Support.Administer` permission under DEC-TEN-0021 and ADR-016 |

---
document_id: FP-003-AC
title: Tenant Lifecycle Acceptance Criteria
status: Approved for Implementation
version: 1.2
sprint: Sprint-01
module: Platform
---

# Acceptance Criteria

### AC-TEN-0001 — Provisioning creation

Creating a valid Tenant generates a nonempty Guid TenantId, stores the normalized code and trimmed display name, and begins in `Provisioning`.

### AC-TEN-0002 — Tenant code uniqueness

Two codes with the same `Trim().ToUpperInvariant()` value cannot be created, and the displayed trimmed casing of the accepted code is preserved.

### AC-TEN-0003 — Tenant name is not unique

Two different TenantIds may use the same trimmed TenantName.

### AC-TEN-0004 — Safe reads

Get and bounded list queries return safe lifecycle projections and no tenant business data.

### AC-TEN-0005 — Activation

A current `Provisioning` Tenant can be activated once and becomes authentication-eligible after successful commit.

### AC-TEN-0006 — Invalid transitions

Every transition not listed in the approved lifecycle matrix is rejected without changing state or publishing a committed transition event.

### AC-TEN-0007 — Suspension

Suspending an `Active` Tenant makes current authentication eligibility false and blocks subsequent tenant selection, new-session, and refresh eligibility decisions.

### AC-TEN-0008 — Exact eligibility

Eligibility is true only for `Active`. A missing Tenant returns `Exists = false`, null status, false eligibility, and `TenantNotFound`; existing statuses return the matching exact reason or `None` for Active.

### AC-TEN-0009 — Reactivation

A `Suspended` Tenant can be reactivated; no other status can use the reactivation operation.

### AC-TEN-0010 — Archive is terminal

Provisioning, Active, and Suspended Tenants may be archived, after which no transition or authentication eligibility is possible.

### AC-TEN-0011 — No physical deletion

No Domain operation, command, repository method, API contract, or migration cascade physically deletes a Tenant.

### AC-TEN-0012 — Platform authorization boundary

An ordinary tenant role cannot administer Tenant lifecycle. Platform lifecycle authorization does not grant tenant business-data access.

### AC-TEN-0013 — No status override

Caller-supplied status or eligibility values cannot create, activate, suspend, reactivate, archive, or authenticate a Tenant outside the persisted lifecycle rules.

### AC-TEN-0014 — Concurrency

A stale rowversion is rejected and cannot overwrite a newer Tenant status or lifecycle metadata.

### AC-TEN-0015 — Safe events

Every successful lifecycle change raises the corresponding safe event after persistence; no event contains credentials, tokens, complete claims, billing details, or HTTP context.

### AC-TEN-0016 — Narrow eligibility contract

The authentication-eligibility contract accepts one TenantId and returns exactly TenantId, Exists, nullable TenantStatus, IsAuthenticationEligible, and TenantAuthenticationIneligibilityReason. It exposes no name, `IQueryable`, aggregate, generic repository, subscription decision, or authorization grant.

### AC-TEN-0017 — Migration reconciliation

`AddTenantLifecycle` creates the Tenant table without legacy auto-backfill or blanket foreign-key retrofit. Reconciliation uses an operator-reviewed environment-specific mapping and fails on missing or duplicate entries. A later dedicated enforcement migration verifies coverage, fails on orphans, and adds restricted foreign keys; no process invents placeholder metadata or Active status.

### AC-TEN-0018 — Persistence ownership and isolation

Tenant lifecycle uses the existing Platform context, schema, connection, migration history, and Unit of Work; Tenant itself has no tenant query filter, while existing tenant-owned entities retain their isolation filters.

### AC-TEN-0019 — Already issued access token

An already issued token does not override current non-Active status. Before ordinary tenant HTTP APIs are production-enabled, the approved centralized current-status enforcement described by `DEC-TEN-0010` is present and tested.

### AC-TEN-0020 — Focused milestone scope

The first implementation milestone introduces no subscription, company, branding, configuration, notification, authentication-session, refresh-token, JWT-issuance, tenant endpoint, Angular, or immutable-audit-store implementation.

## Platform-plane authorization (ADR-015, DEC-TEN-0018)

The following criteria apply to the future Tenant HTTP transport and its platform-plane authorization foundation. They are deferred with the endpoints; no HTTP behavior is implemented in the first backend milestone.

### AC-TEN-0021 — Tenant token rejected on platform routes

A valid tenant-plane token (carrying `tenant_id` and tenant-derived permissions) cannot invoke any `/api/platform/tenants` route; the request is denied and no lifecycle data is disclosed or changed.

### AC-TEN-0022 — Authenticated platform principal without permission

An authenticated platform-support principal lacking the required `PermissionScope.PlatformSupport` permission for a route receives 403 and no lifecycle effect occurs.

### AC-TEN-0023 — Correct platform permission authorizes

A platform-support principal holding the exact required permission (`Platform.Tenants.View`, `Platform.Tenants.Manage`, or `Platform.Tenants.Lifecycle`) is authorized for the mapped route.

### AC-TEN-0024 — Tenant role cannot obtain platform permissions

`Platform.Tenants.View`, `Platform.Tenants.Manage`, and `Platform.Tenants.Lifecycle` cannot be assigned to any tenant custom role, tenant system role, or tenant role-permission assignment; `Role.AssignPermission` rejects them by scope.

### AC-TEN-0025 — Platform token carries no tenant scope

A platform-support token carries `security_plane=platform` and no `tenant_id` or `tenant_user_id`; a token combining `security_plane=platform` with `tenant_id` is rejected as invalid.

### AC-TEN-0026 — Target TenantId is not caller scope

A route `{tenantId}` never establishes `ICurrentTenant`, never becomes a tenant claim, and never grants business-data access to the target tenant's tables.

### AC-TEN-0027 — Suspended target authorizable for reactivation

A platform principal with `Platform.Tenants.Lifecycle` is authorized to reactivate a `Suspended` target; authorization does not require the target to be Active.

### AC-TEN-0028 — Provisioning target authorizable for activation

A platform principal with `Platform.Tenants.Lifecycle` is authorized to activate a `Provisioning` target where the transition graph permits; target status does not gate caller authorization.

### AC-TEN-0029 — Tenant-plane authorization unchanged

FP-005 Company and Localization routes remain tenant-plane: they still require a validated current tenant and `PermissionScope.Tenant` permissions through the existing `RequirePermission` handler, unaffected by the platform plane.

### AC-TEN-0030 — Tenant claims provider omits platform permissions

Tenant token claim generation emits only `PermissionScope.Tenant` permissions; a `PlatformSupport` entry present through corrupt data, a bad seed, or a direct database change is not emitted into a tenant token.

## Platform-support bootstrap, lifecycle, and authority administration (ADR-016, DEC-TEN-0019/0020/0021)

Criteria for the future Phase-3 platform-support authority foundation. Deferred with the platform token/session profile; not implemented in the committed milestones.

### AC-TEN-0031 — Bootstrap requires an existing eligible identity

The configured bootstrap `AuthenticationSubject` must resolve to an existing `Identity` with an authentication-capable, active `AuthenticationAccount`; a missing or ineligible subject creates no platform authority. Bootstrap never creates identities.

### AC-TEN-0032 — Tenant users cannot bootstrap

No tenant role or tenant-IAM path can invoke bootstrap or create/modify platform-support authority; bootstrap is keyed only by immutable `AuthenticationSubject` configuration.

### AC-TEN-0033 — Bootstrap operates only without usable platform authority

Bootstrap creates/recovers authority only when no usable platform authority exists (per the ADR-016 definition) and is inert once usable authority exists.

### AC-TEN-0034 — Bootstrap is not standing request authority

Being present in bootstrap configuration never authorizes ordinary platform operations; it authorizes only the genesis/recovery operation.

### AC-TEN-0035 — Bootstrap is audited and idempotent

Genesis/recovery operations are audited with a distinguishable bootstrap actor; re-running bootstrap creates no duplicate principal or assignment and does nothing when usable authority already exists.

### AC-TEN-0036 — Bootstrap fails closed on invalid permissions

Bootstrap grants only known `PermissionScope.PlatformSupport` permissions; an unknown or tenant-scoped permission is rejected and no assignment is created.

### AC-TEN-0037 — Disabled principal is not implicitly re-enabled

Bootstrap/recovery never changes a `Disabled` principal's status; re-enable is a separate explicit lifecycle operation. Configuration membership is not re-enable authority.

### AC-TEN-0038 — Principal status default and transitions

A registered `PlatformSupportPrincipal` starts `Active`; the only transitions are `Active → Disabled` and `Disabled → Active`.

### AC-TEN-0039 — Disabled principal cannot receive a platform token

Platform token issuance performs a live principal-status check and denies issuance for a `Disabled` principal; no token-carried status is authoritative.

### AC-TEN-0040 — Disabled principal cannot refresh

Platform refresh/session continuation re-reads live status; a `Disabled` principal's refresh is denied and its platform session is revoked; no new platform token is issued.

### AC-TEN-0041 — Disabled retains assignments; grant rejected, revoke allowed

While `Disabled`, active assignment rows remain persisted (not deleted, not revoked); a grant is rejected; a revoke is allowed.

### AC-TEN-0042 — Lifecycle concurrency and existing-token accuracy

Status mutations use the principal `RowVersion` (a stale version is a conflict). Documentation states accurately that disabling does not cryptographically invalidate an already-issued short-lived JWT; immediate cut-off is via `SecurityVersion`/session revocation, and `StrictAccessTokenValidator` performs no live DB status lookup.

### AC-TEN-0043 — Administration permission is platform-scoped and un-self-grantable

`Platform.Support.Administer` is `PermissionScope.PlatformSupport` and cannot be assigned to any tenant custom role, tenant system role, or tenant role-permission assignment.

### AC-TEN-0044 — Administration permission excluded from tenant surfaces

`Platform.Support.Administer` never appears in the tenant-facing permission catalog listing and is never emitted into a tenant access-token claim.

### AC-TEN-0045 — Tenant permissions cannot administer platform authority

`Platform.Tenants.Manage` and `Platform.Tenants.Lifecycle` cannot register, grant, revoke, disable, or re-enable platform-support authority; only `Platform.Support.Administer` (or genesis bootstrap) can.

### AC-TEN-0046 — Status migration backfill

The status migration adds `Status` `NOT NULL` (default `Active`, `CHECK Active/Disabled`) and `StatusChangedUtc`/`StatusChangedBy` `NULLABLE`. Every `PlatformSupportPrincipal` existing before the migration becomes `Active` with `StatusChangedUtc` and `StatusChangedBy` `NULL`; no historical transition is synthesized from `CreatedUtc`/`CreatedBy`.

### AC-TEN-0047 — First transition populates status metadata

The first `Disable` or `Re-enable` populates `StatusChangedUtc` and `StatusChangedBy`; every subsequent transition overwrites them with the latest transition metadata, while `ModifiedUtc`/`ModifiedBy` continue under the normal audit mechanism.

### AC-TEN-0048 — Multiple subjects, deterministic single genesis

The bootstrap allow-list may contain multiple unique `AuthenticationSubject` values; a single bootstrap evaluation establishes exactly one genesis principal — the first eligible subject by ordinal comparison of the canonical subject (never configuration insertion order).

### AC-TEN-0049 — Concurrent bootstrap converges on one principal

Concurrent bootstrap evaluations that both observe no usable authority converge, via the authoritative unique `IdentityId`/active-assignment constraints, on exactly one genesis/recovery principal (the loser's duplicate is an idempotent race outcome); no distributed lock is required.

### AC-TEN-0050 — Remaining configured subjects stay unprivileged

Configured subjects other than the selected one receive no platform authority automatically; they remain recovery candidates only.

### AC-TEN-0051 — Recovery creates a new principal; disabled sole principal not re-enabled

When no usable authority exists and a `Disabled` principal exists, bootstrap never re-enables it; if another eligible configured subject owns no principal, bootstrap establishes that subject as a new `Active` recovery principal while the disabled principal remains `Disabled`. An `Active` principal that has lost all active catalog-valid `PlatformSupport` assignments is likewise not usable authority, and recovery does not mutate its assignments.

### AC-TEN-0052 — No eligible recovery candidate fails closed

If a `Disabled` principal is the only configured subject and no other eligible candidate exists, bootstrap fails closed (no implicit re-enable, no duplicate principal) and emits an operator diagnostic that no eligible recovery subject exists; recovery requires an additional approved pre-existing `AuthenticationSubject` in configuration or the separately-authorized explicit Re-enable operation.

### AC-TEN-0053 — Usable authority evaluated live

"No usable platform authority exists" is evaluated live from persisted state and the code-owned catalog — current principal `Status`, authentication-account eligibility, active persisted assignments, and `PermissionScope.PlatformSupport` — and never from configuration, a cached flag, a bare principal row, or corrupt/unknown/revoked assignment rows.

## FP-002 Milestone 4 cross-package coverage

`AC-TEN-0019` is implemented by the FP-002 Milestone 4 centralized live-Tenant authorization foundation under `DEC-AUTH-0057`, `AC-AUTH-0045`, and `TS-AUTH-0108`. FP-003 remains the authoritative lifecycle and eligibility source; FP-002 owns the Host/API authorization integration.

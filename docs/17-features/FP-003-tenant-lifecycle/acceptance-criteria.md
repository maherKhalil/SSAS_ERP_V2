---
document_id: FP-003-AC
title: Tenant Lifecycle Acceptance Criteria
status: Approved for Implementation
version: 1.4
sprint: Sprint-01
module: Platform
---

> **Version 1.4 (2026-08-12).** `AC-TEN-0078`–`AC-TEN-0093` (Phase-4 request-plane / HTTP exposure) are **Approved for Implementation** under `DEC-TEN-0023`–`DEC-TEN-0026`; none is implemented (Phase 4A delivered the authorization primitives that `AC-TEN-0084`/`AC-TEN-0091` partly anchor). Corrected after review: `AC-TEN-0082` (logout is new 4B capability with `session_id`-claim resolution), `AC-TEN-0083` (L1 serialization required), `AC-TEN-0089` (administrative-recovery semantics), and new `AC-TEN-0093` (administrative-authority loss detection and recovery). All prior criteria are unchanged.

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

> **This criterion describes FP-003's FIRST MILESTONE and is partly superseded. Which concern by which package is recorded below rather than left to be re-derived** (`DEC-L-030`, 2026-08-26; `DEC-L-027`).
>
> | Deferred concern | Superseded by |
> |---|---|
> | company | **FP-005** — `Company` ships as the tenant-owned legal-entity root |
> | authentication-session, refresh-token, JWT-issuance | **FP-002** — `AuthenticationSession`, `RefreshTokenRecord` and RS256 access tokens all ship |
> | subscription | **`DEC-L-004`, `DEC-L-006` and ratified FP-014** — the owner ruled the commercial plane `E + C`; T-035 built plans, subscriptions and entitlement grants |
> | branding, configuration, notification, tenant endpoint, Angular, immutable-audit-store | **not superseded** — still deferred, and no package claims them |
>
> **The architecture guard that enforced this criterion is retired**, not trimmed. It scanned for four declaration spellings — `TenantController`, `Subscription`, `Billing`, `CompanyProvision` — against a criterion listing eleven concerns, so it kept passing while three of them shipped: it looked for `CompanyProvision` rather than `Company`, and never named the session or refresh-token types at all. `TenantController` could not fire in a codebase that maps minimal-API endpoints and declares no controllers.
>
> **What survived it is a separate test.** The retired guard also asserted that `SSAS.Platform.API` does not reach into `SSAS.Platform.Application.Tenants` — unrelated to the four spellings, still live, and still true, since no tenant endpoint is mapped anywhere in the product. It now stands on its own as `Tenant_endpoints_remain_deferred_and_the_platform_api_does_not_reach_tenant_application`.
>
> **The `Billing` deferral is now unguarded.** It is real but was unmoored from any live authority here; if it is worth asserting it belongs with `ADR-029` and `REQ-SUB-0025`/`REQ-SUB-0026`, not with this criterion.

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

Bootstrap creates/recovers authority only when no usable platform authority exists (per the ADR-016 definition) and is inert once usable authority exists. *(Refined by the Proposed `DEC-TEN-0026` / `AC-TEN-0093`: bootstrap recovery is additionally eligible when usable platform authority exists but no usable **administrative** authority — an Active principal holding active current-catalog `Platform.Support.Administer` — remains. This forward note does not change the Phase-3B genesis-inertness behaviour verified here.)*

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

## Platform authentication session and token profile (ADR-016 Phase 3C, DEC-TEN-0022)

Deferred with the Phase-3C platform token/session profile. Every criterion below is governed by `ADR-016` and `DEC-TEN-0022` (and restates `ADR-015`/`DEC-TEN-0018` token rules where noted). None is implemented yet.

### AC-TEN-0054 — Separate platform session persistence

Platform authentication uses a separate `PlatformAuthenticationSession` aggregate and table; the tenant `AuthenticationSession` aggregate, table, foreign keys, events, and queries are unchanged.

### AC-TEN-0055 — Platform session carries no tenant identifiers

`PlatformAuthenticationSession` is not `ITenantOwnedEntity`/`ICompanyOwnedEntity`, receives no tenant query filter, and contains no `TenantId`, `TenantUserId`, or `CompanyId`.

### AC-TEN-0056 — Platform session anchors to identity and principal

`PlatformAuthenticationSession` stores both `IdentityId` and `PlatformSupportPrincipalId`; both are required and both are foreign-key-enforced.

### AC-TEN-0057 — Tenant refresh cannot mint a platform token

A tenant refresh token resolves only against tenant-session persistence; it can never continue into or mint a platform token.

### AC-TEN-0058 — Platform refresh cannot mint a tenant token

A platform refresh token resolves only against platform-session persistence; it can never continue into or mint a tenant token. No request parameter selects a plane for an existing refresh token.

### AC-TEN-0059 — Platform token with `tenant_id` is rejected

A token combining `security_plane=platform` with any `tenant_id` (or `tenant_user_id`) is rejected structurally, not ignored.

### AC-TEN-0060 — Legacy tenant token remains valid

A tenant access token without a `security_plane` claim remains valid under the tenant profile (absence ⇒ tenant); no tenant-issuer change is required in Phase 3C.

### AC-TEN-0061 — Zero PlatformSupport permissions denies issuance

A principal with zero active catalog-valid `PermissionScope.PlatformSupport` permissions is not eligible; platform token issuance is denied.

### AC-TEN-0062 — Zero PlatformSupport permissions denies refresh and revokes

On platform refresh, a principal with zero active catalog-valid `PermissionScope.PlatformSupport` permissions is denied, the current platform session is revoked, and no new token is issued.

### AC-TEN-0063 — Disabled principal denies issuance

At issuance, a live status check denies a platform token when `PlatformSupportPrincipal.Status == Disabled`; no token-carried status is authoritative.

### AC-TEN-0064 — Disabled principal denies refresh and revokes

At refresh, live principal status is re-read; a `Disabled` principal is denied, the current platform session is revoked, and no new token is issued.

### AC-TEN-0065 — Tenant sessions unaffected by platform Disable

Disabling a `PlatformSupportPrincipal` (and revoking its platform sessions) has no effect on the person's tenant `AuthenticationSession`s.

### AC-TEN-0066 — Re-enable does not resurrect revoked sessions

Re-enabling a principal (`Disabled → Active`) does not reactivate revoked `PlatformAuthenticationSession`s; a new platform session must be established.

### AC-TEN-0067 — Account SecurityVersion reused

`PlatformAuthenticationSession.SecurityVersionAtCreation` snapshots the global `AuthenticationAccount.SecurityVersion`; a live mismatch on refresh revokes and denies continuation.

### AC-TEN-0068 — No principal SecurityVersion

No `SecurityVersion` is added to `PlatformSupportPrincipal`; principal status is a separate platform-plane state.

### AC-TEN-0069 — Validator is structural and stateless

`StrictAccessTokenValidator` selects the tenant/platform profile structurally by `security_plane` and performs no database or live principal-status lookup.

### AC-TEN-0070 — Permissions re-derived live on refresh

Platform refresh re-derives permission claims live from `IPlatformSupportPermissionReadService`; no stale permission snapshot from the prior token/session is reused.

### AC-TEN-0071 — Bootstrap config does not influence issuance

Platform token issuance reads only `Identity`, `AuthenticationAccount`, `PlatformSupportPrincipal`, and `PlatformPermissionAssignment`; bootstrap subject lists/configuration never participate.

### AC-TEN-0072 — Platform session limit is independent

Platform session-limit accounting is separate from tenant accounting; a reused `MaximumActiveSessions` applies independently within platform-session persistence, and the two planes are never counted against each other.

### AC-TEN-0073 — Platform refresh reuse follows compromise semantics

Platform refresh-token reuse marks the platform session compromised/revoked under the existing session model, without affecting tenant sessions or inventing cross-plane compromise propagation.

### AC-TEN-0074 — Platform token forbidden claims

A platform access token forbids `tenant_id`, `tenant_user_id`, `role`, `company_id`, any principal-status claim, and any bootstrap/config claim; it carries `security_plane=platform` exactly once plus `identity_id`, `session_id`, `client_id`, `security_version`, and one or more active catalog-valid `PlatformSupport` permission claims.

### AC-TEN-0075 — Server-owned plane selection

The security plane is selected by a server-side typed issuer path and a distinct `PlatformAccessTokenClaimsProvider`; there is no caller-controllable `IssueToken(bool/string)` API and the tenant claims provider remains tenant-only and unchanged.

### AC-TEN-0076 — Proactive revocation on principal Disable

Explicitly disabling a `PlatformSupportPrincipal` revokes that principal's active `PlatformAuthenticationSession`s as part of the platform workflow (platform-only), blocking refresh immediately; it does not cryptographically invalidate an already-issued short-lived platform access JWT, which expires naturally.

### AC-TEN-0077 — Trusted session-creation source

Platform-session creation consumes a trusted verified-authentication result (`VerifiedIdentity`/verified-account context) and never an arbitrary caller-supplied `IdentityId`; it requires live account eligibility, an `Active` principal, and at least one catalog-valid permission before issuing a platform access/refresh pair. No HTTP route is added in Phase 3C.

## Platform request-plane authorization and HTTP exposure (Phase 4, DEC-TEN-0023–0026)

Approved for Implementation (implementation pending); governed by `ADR-016` §5. `AC-TEN-0084` and `AC-TEN-0091` are partly anchored by the committed Phase-4A primitives; the remainder gate slices 4B–4E.

### AC-TEN-0078 — Server-authorized platform login

A verified identity obtains a platform session only through a dedicated, server-owned platform login route: credentials → `VerifiedIdentity` → resolve principal by `IdentityId` → account eligible, principal `Active`, ≥1 catalog-valid `PlatformSupport` permission → platform access/refresh pair. The plane is derived from the route and persisted authority.

### AC-TEN-0079 — Caller cannot confer platform authority

Platform login/refresh reject or ignore any caller-supplied `security_plane`, `PlatformSupportPrincipalId`, permission list, `SecurityVersion`, or `plane`/`isPlatform`/`mode` field as authority; none can select a privileged plane or bypass the server-side eligibility proof.

### AC-TEN-0080 — Platform refresh route/store separation

A dedicated platform refresh route invokes `RefreshPlatformAuthenticationSessionCommandHandler` and resolves the locator only in platform-session persistence; the tenant refresh route resolves only tenant persistence. No shared locator, token-shape inference, numeric-id inference, or fallback exists.

### AC-TEN-0081 — Cross-plane refresh rejected over HTTP

A tenant refresh token presented on the platform refresh route is denied, and a platform refresh token on the tenant refresh route is denied.

### AC-TEN-0082 — Dedicated platform logout (new 4B capability; trusted session source)

A dedicated PLATFORM-ONLY platform logout route (platform-authenticated policy, not permission-gated) revokes the current platform session. The capability is **new 4B work** (no platform current-session-revoke command exists from Phase 3C). The target session is resolved from the **validated `session_id` claim** in the platform store only; a caller-supplied session/identity/principal id cannot select the target. Refresh after logout is denied; the tenant session for the same identity is unaffected; `AuthenticationAccount.SecurityVersion` is unchanged; and the already-issued access JWT remains valid until natural expiry. A tenant token cannot call platform logout and a platform token cannot cause tenant logout.

### AC-TEN-0083 — Create-vs-disable serialization required before HTTP exposure (L1)

Before platform-session creation is exposed over HTTP (4B), the create-vs-disable concurrency item is closed by **serialization** — `PlatformAuthenticationSessionCreator` serializes the principal's authority state against a concurrent Disable via a transactionally effective `FOR UPDATE`/locking read. Correctness must **not** depend on `READ_COMMITTED_SNAPSHOT` being disabled or on deployment isolation settings. Required invariant: once a `Disable` commits, no concurrent creation may commit an `Active` session for that principal (both interleavings safe); proven by a real two-connection SQL concurrency test under actual supported SQL Server settings.

### AC-TEN-0084 — Request-plane policy taxonomy

Tenant-authenticated, platform-authenticated (structural/claims-based, DB-free), and plane-neutral policies exist with the stated semantics; the platform permission policy (`PlatformPermission:`) is structurally separate from the tenant `Permission:`/`Role:` policies. *(Phase 4A delivered the platform permission policy + handler.)*

### AC-TEN-0085 — No bare authenticated policy on plane-specific routes

An architecture guard rejects a plane-specific endpoint that uses a bare `RequireAuthenticatedUser`; plane-neutral is an explicit, justified classification.

### AC-TEN-0086 — Generic-auth endpoint classification

`/api/platform/auth/logout` and `/api/platform/localization/effective`(+`/batch`) are classified TENANT-ONLY and require the tenant-authenticated policy; a platform token is rejected on them.

### AC-TEN-0087 — Authority reads require Administer

Listing/getting platform-support principals and their assignments requires `Platform.Support.Administer`; a non-`Administer` platform token and a tenant token are both denied (403).

### AC-TEN-0088 — No new read permission

No `Platform.Support.View` (or equivalent) permission is introduced in Phase 4; authority reads reuse `Platform.Support.Administer`.

### AC-TEN-0089 — Self-disable / last-admin behaviour

Self-disable, self-revoke of `Platform.Support.Administer`, and removal/disable of the last usable administrator are permitted with no preventive guard. Loss of the final usable **administrative** authority (no Active principal holds active current-catalog `Platform.Support.Administer`) activates the approved administrative-recovery path; there is no permanent administrative lockout where an eligible configured recovery subject exists. Recovery never silently re-enables a `Disabled` principal and never grants `Administer` to an arbitrary existing principal.

### AC-TEN-0093 — Administrative-authority loss detection and recovery

The recovery predicate distinguishes **usable platform authority** (an Active principal with an eligible account and ≥1 active current-catalog `PlatformSupport` permission — any of them) from **usable platform administrative authority** (an Active principal with an eligible account holding active current-catalog `Platform.Support.Administer`). When the last `Administer` is revoked while another `PlatformSupport` permission (e.g. `Platform.Tenants.View`) remains, general usable authority is `true` but usable administrative authority is `false`, and genesis/recovery bootstrap becomes eligible. Recovery establishes a **new** `Active` principal only for an eligible configured `AuthenticationSubject` that owns no principal; it does not re-enable a `Disabled` principal, does not re-grant `Administer` to the principal that retained the other permission, and — if no eligible configured recovery subject exists — remains fail-closed (governed break-glass may be required; automatic recovery is not guaranteed in every environment). Eligibility is evaluated live against persisted state, never from a still-valid access JWT.

### AC-TEN-0090 — Stateless JWT after self-disable/revoke

After self-disable or self-revoke, the already-issued short-lived platform access JWT may retain its authority until natural expiry; Disable proactively revokes platform sessions (blocking refresh) and a permission revoke is reflected at the next refresh — no immediate access-token revocation is introduced.

### AC-TEN-0091 — No new claim; no per-request DB authorization

The Phase-3C token profile is unchanged (no `PlatformSupportPrincipalId`/`principal_id` claim); `PlatformPermissionAuthorizationHandler` and the plane-authenticated policies perform no live principal-status/permission/session/`SecurityVersion` lookup per request. *(Anchored by the committed Phase-4A handler.)*

### AC-TEN-0092 — Phase-5 tenant-management boundary retained

Phase 4 exposes platform-authority administration only; `Platform.Tenants.View`/`Manage`/`Lifecycle` HTTP endpoints remain Phase 5 and are not exposed merely because the authorization primitives exist.

## FP-002 Milestone 4 cross-package coverage

`AC-TEN-0019` is implemented by the FP-002 Milestone 4 centralized live-Tenant authorization foundation under `DEC-AUTH-0057`, `AC-AUTH-0045`, and `TS-AUTH-0108`. FP-003 remains the authoritative lifecycle and eligibility source; FP-002 owns the Host/API authorization integration.

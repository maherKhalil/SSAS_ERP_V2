---
document_id: FP-003-TEST
title: Tenant Lifecycle Test Scenarios
status: Approved for Implementation
version: 1.4
sprint: Sprint-01
module: Platform
---

> **Version 1.4 (2026-08-12).** `TS-TEN-0118`–`TS-TEN-0142` (Phase-4 request-plane / HTTP exposure) are **Approved for Implementation** under `DEC-TEN-0023`–`DEC-TEN-0026`; they are planned for slices 4B–4E (the Phase-4A anti-escalation / real-JWT authorization scenarios are already implemented). Corrected after review: `TS-TEN-0136` (administrative-authority loss also triggers recovery) and new `TS-TEN-0139`–`TS-TEN-0142` (last-Administer-retain-other-permission lockout + recovery, negative no-subject case, platform-logout trusted-session resolution, create-vs-disable serialization). Prior scenarios are unchanged.

# Test Scenarios

## Domain

- **TS-TEN-0001:** Create a Tenant with a server-generated nonempty Guid TenantId and `Provisioning` status.
- **TS-TEN-0002:** Trim tenant code, preserve display casing, and derive exact `ToUpperInvariant()` normalization.
- **TS-TEN-0003:** Trim and preserve TenantName display casing without treating it as a unique identity.
- **TS-TEN-0004:** Permit every listed lifecycle transition and derive eligibility only for Active.
- **TS-TEN-0005:** Reject every unlisted or repeated transition without changing lifecycle metadata.
- **TS-TEN-0006:** Make Archived terminal and preserve the aggregate for history.
- **TS-TEN-0007:** Raise TenantCreated, TenantActivated, TenantSuspended, TenantReactivated, and TenantArchived with the exact bounded reason code and no free-form reason text.
- **TS-TEN-0008:** Preserve immutable TenantId and tenant code through all transitions.

## Application

- **TS-TEN-0010:** Reject duplicate normalized tenant code while allowing duplicate TenantName.
- **TS-TEN-0011:** Get one Tenant and list bounded safe lifecycle projections with approved filters.
- **TS-TEN-0012:** Return exact Exists, nullable status, eligibility, and reason combinations for Active, Provisioning, Suspended, Archived, and a missing Tenant.
- **TS-TEN-0013:** Coordinate create, activate, suspend, reactivate, and archive through one Platform Unit of Work each.
- **TS-TEN-0014:** Map stale rowversion to a concurrency result and commit no transition event.
- **TS-TEN-0015:** Reject lifecycle administration authorized only by an ordinary tenant role.
- **TS-TEN-0016:** Prove caller-supplied status, eligibility Boolean, or current-tenant context cannot override persisted state.
- **TS-TEN-0017:** Return an eligibility result containing exactly TenantId, Exists, nullable TenantStatus, derived Boolean, and TenantAuthenticationIneligibilityReason, with no TenantName.
- **TS-TEN-0018:** Verify cancellation tokens flow through every persistence and read boundary.
- **TS-TEN-0019:** Verify suspension/reactivation changes only Tenant lifecycle and does not mutate memberships, sessions, subscriptions, or business data.

## SQL Server

- **TS-TEN-0020:** Apply the full Platform migration chain to an empty SQL Server database.
- **TS-TEN-0021:** Apply `AddTenantLifecycle` to the Milestone 2 schema without auto-backfilling legacy TenantIds or retrofitting legacy foreign keys, then reconcile using an approved complete mapping.
- **TS-TEN-0022:** Fail reconciliation on missing or duplicate mappings; infer no state, commit no environment-specific production data, and create no placeholder or automatic Active tenant.
- **TS-TEN-0023:** Enforce global normalized TenantCode uniqueness with exact binary collation behavior.
- **TS-TEN-0024:** Enforce the four-value status check constraint and required code/name fields.
- **TS-TEN-0025:** Reject a stale lifecycle update through SQL Server rowversion.
- **TS-TEN-0026:** Enforce restricted deletes and retain an Archived Tenant and its historical references.
- **TS-TEN-0027:** Verify Tenant receives no tenant query filter and can be queried through explicitly authorized Platform reads.
- **TS-TEN-0028:** In a dedicated post-reconciliation migration, fail on orphans and enforce restricted Tenant foreign keys without breaking existing same-tenant composite constraints; require every table introduced after FP-003 to include its Tenant FK from its first migration.
- **TS-TEN-0029:** Preserve UTC creation, modification, and status-change metadata across transitions.

## Architecture and security

- **TS-TEN-0030:** Keep Tenant lifecycle Domain and Application free of EF Core, SQL Server, ASP.NET Core, HTTP, and UI dependencies.
- **TS-TEN-0031:** Define only aggregate-specific repositories; expose no generic repository, delete method, or `IQueryable` Application boundary.
- **TS-TEN-0032:** Introduce no deferred Subscription, Company, Branding, Configuration, Notification, AuthenticationSession, RefreshToken, JWT issuer, Angular, or AuditStore implementation.
- **TS-TEN-0033:** Keep Platform tenant lifecycle independent from HR and GL implementations.
- **TS-TEN-0034:** Verify the first implementation milestone exposes no Tenant HTTP endpoint.
- **TS-TEN-0035:** Scan events, commands, source, logs, and test artifacts for credentials, tokens, complete claims, billing secrets, or HTTP context.
- **TS-TEN-0036:** Preserve automatic query filters on existing `ITenantOwnedEntity` types while Tenant remains unfiltered.
- **TS-TEN-0037:** Ensure the eligibility contract cannot be used as a generic cross-tenant business-data or authorization service.
- **TS-TEN-0038:** Verify no production startup path automatically applies Platform migrations.

## Future API authorization

- **TS-TEN-0040:** Require approved Platform lifecycle permission for every future Tenant endpoint.
- **TS-TEN-0041:** Return 403 when an authenticated ordinary tenant administrator attempts a lifecycle operation without Platform authority.
- **TS-TEN-0042:** Expose no physical DELETE Tenant endpoint or tenant-delete permission.
- **TS-TEN-0043:** Return Problem Details and the project-standard conflict result for stale lifecycle requests.
- **TS-TEN-0044:** Deny ordinary tenant business access for current Provisioning, Suspended, or Archived status even when a previously issued token remains cryptographically valid.

## Platform-plane authorization (ADR-015, DEC-TEN-0018)

Scenarios for the future Tenant HTTP transport and its platform-plane authorization foundation, exercised through the real Host authorization pipeline. They are deferred with the endpoints.

### Authentication and authorization

- **TS-TEN-0045:** A request with no token to any `/api/platform/tenants` route returns 401.
- **TS-TEN-0046:** A valid tenant-plane token is denied (403) on every `/api/platform/tenants` route with no lifecycle effect.
- **TS-TEN-0047:** A platform-support token without the required `PermissionScope.PlatformSupport` permission returns 403.
- **TS-TEN-0048:** A platform-support token with the exact required permission (`Platform.Tenants.View` / `Manage` / `Lifecycle`) authorizes the mapped route.

### Plane confusion

- **TS-TEN-0049:** A token combining `security_plane=platform` with a `tenant_id` claim is rejected as invalid.
- **TS-TEN-0050:** A tenant token carrying a forged `security_plane=platform` claim is rejected (the claim is server-issued and validated, not client-editable).
- **TS-TEN-0051:** A route target `{tenantId}` does not become caller scope: it establishes no `ICurrentTenant` and grants no business-data access to the target tenant.

### Escalation

- **TS-TEN-0052:** A tenant custom role cannot be assigned a `PlatformSupport` permission; `Role.AssignPermission` rejects it by scope.
- **TS-TEN-0053:** A tenant system role cannot be assigned a `PlatformSupport` permission.
- **TS-TEN-0054:** The tenant token claim provider filters out any `PlatformSupport` entry (e.g. from corrupt data or a bad seed) and emits only `PermissionScope.Tenant` permissions.

### Target status independence

- **TS-TEN-0055:** A platform principal with `Platform.Tenants.Lifecycle` is authorized to activate a `Provisioning` target where the transition graph permits.
- **TS-TEN-0056:** A platform principal with `Platform.Tenants.Lifecycle` is authorized to reactivate a `Suspended` target.
- **TS-TEN-0057:** Target lifecycle status does not gate caller authorization; the authorization decision precedes and is independent of the domain transition check.

### Tenant-plane regression

- **TS-TEN-0058:** An FP-005 Company route still requires a validated current tenant and a `PermissionScope.Tenant` permission through the existing `RequirePermission` handler.
- **TS-TEN-0059:** A Localization tenant-plane route remains unchanged and unaffected by the platform plane.

## Platform-support bootstrap, lifecycle, and token authority (ADR-016, DEC-TEN-0019/0020/0021)

Scenarios for the future Phase-3 platform-support authority foundation. Deferred with the platform token/session profile.

### Bootstrap

- **TS-TEN-0060:** A configured bootstrap subject with no matching `Identity` creates no platform authority (logged no-op).
- **TS-TEN-0061:** A configured bootstrap subject whose `AuthenticationAccount` is not authentication-eligible creates no platform authority.
- **TS-TEN-0062:** No tenant role or tenant-IAM path can invoke bootstrap or create/modify platform authority.
- **TS-TEN-0063:** With no usable platform authority, bootstrap registers exactly one genesis principal and establishes the initial approved `PlatformSupport` permission set.
- **TS-TEN-0064:** With usable platform authority already present, bootstrap is inert and idempotent (no duplicate principal or assignment).
- **TS-TEN-0065:** Bootstrap rejects an unknown or tenant-scoped permission and creates no assignment (fail closed).
- **TS-TEN-0066:** Bootstrap/recovery never implicitly re-enables a `Disabled` principal; config membership is not re-enable authority.
- **TS-TEN-0067:** A corrupt tenant-scoped row in the platform assignment table is never counted as usable platform authority or bootstrap authority.

### Principal lifecycle (SQL)

- **TS-TEN-0068:** The status migration adds `Status` (default `Active`) with a CHECK constraint that rejects any value outside `{Active, Disabled}`.
- **TS-TEN-0069:** `Active → Disabled` and `Disabled → Active` transitions persist and are audited (`StatusChangedUtc`/`StatusChangedBy`).
- **TS-TEN-0070:** A stale principal `RowVersion` on a status mutation is a concurrency conflict.
- **TS-TEN-0071:** A `Disabled` principal retains all assignment rows (none deleted or revoked by the disable).
- **TS-TEN-0072:** A grant to a `Disabled` principal is rejected; a revoke on a `Disabled` principal is allowed.

### Platform token authority

- **TS-TEN-0073:** A platform token carries `security_plane=platform` and no `tenant_id`/`tenant_user_id`.
- **TS-TEN-0074:** A `Disabled` principal is denied a new platform token by the live status check at issuance.
- **TS-TEN-0075:** A `Disabled` principal's platform refresh is denied and its platform session is revoked; no new token is issued.
- **TS-TEN-0076:** Platform token permission claims contain only catalog-valid `PlatformSupport` permissions (authority read path re-validates against the catalog).
- **TS-TEN-0077:** A tenant token cannot become a platform token by claim injection; a malformed mixed-plane profile (`security_plane=platform` with `tenant_id`) is rejected.

### Authority administration permission

- **TS-TEN-0078:** `Platform.Support.Administer` is `PermissionScope.PlatformSupport` and cannot be assigned to any tenant custom or system role.
- **TS-TEN-0079:** `Platform.Support.Administer` is excluded from the tenant-facing permission catalog listing and from tenant access-token claims.
- **TS-TEN-0080:** `Platform.Tenants.Manage`/`Platform.Tenants.Lifecycle` cannot register, grant, revoke, disable, or re-enable platform-support authority.

### Status migration backfill (SQL)

- **TS-TEN-0081:** Applying the status migration sets every pre-existing `PlatformSupportPrincipal` to `Status = Active`.
- **TS-TEN-0082:** A pre-existing principal has `StatusChangedUtc` and `StatusChangedBy` `NULL` after the migration (no synthesized transition).
- **TS-TEN-0083:** The first `Disable` populates `StatusChangedUtc` and `StatusChangedBy`.
- **TS-TEN-0084:** A subsequent `Re-enable` overwrites `StatusChangedUtc`/`StatusChangedBy` with the latest transition metadata.

### Bootstrap subject cardinality, selection, and recovery

- **TS-TEN-0085:** With subjects `A` and `B` both eligible and no usable authority, bootstrap establishes only the deterministic first subject (ordinal) as genesis; `B` receives no authority.
- **TS-TEN-0086:** If the first subject is missing or ineligible, the next eligible subject is selected deterministically.
- **TS-TEN-0087:** Two concurrent bootstrap evaluations converge on exactly one genesis principal via the unique `IdentityId`/active-assignment constraints.
- **TS-TEN-0088:** With `A` `Disabled` and `B` eligible and owning no principal, bootstrap creates `B` as an `Active` recovery principal; `A` remains `Disabled`.
- **TS-TEN-0089:** With `A` `Disabled` as the only configured subject and no other candidate, bootstrap does not re-enable `A`, creates no duplicate, and fails closed with an operator diagnostic.
- **TS-TEN-0090:** A configured candidate that is not selected receives no platform authority.
- **TS-TEN-0091:** Once usable platform authority exists, a further bootstrap evaluation is inert.
- **TS-TEN-0092:** Usable authority is evaluated live: a corrupt tenant-scoped row, an unknown permission, a revoked assignment, and a `Disabled` principal are all excluded from the usable-authority determination.

### Platform authentication session and token profile (Phase 3C, DEC-TEN-0022)

SQL scenarios use the real SQL Server provider; validator scenarios are structural (no database).

- **TS-TEN-0093:** The `PlatformAuthenticationSessions` table has `IdentityId` and `PlatformSupportPrincipalId` columns and **no** `TenantId`/`TenantUserId`/`CompanyId`; `Status`/`RevocationReason` are `BIN2` `CHECK`-constrained; `RowVersion` is a concurrency token.
- **TS-TEN-0094:** Foreign keys `IdentityId → Identity` and `PlatformSupportPrincipalId → PlatformSupportPrincipal` exist with `OnDelete(Restrict)`; the refresh-token child is session-owned.
- **TS-TEN-0095:** Platform session creation persists an `Active` session anchored to identity + principal, snapshots the account `SecurityVersion`, and creates the initial platform refresh token.
- **TS-TEN-0096:** A platform refresh rotates the refresh token within the same family and issues a new platform access token.
- **TS-TEN-0097:** A live `AuthenticationAccount.SecurityVersion` mismatch on platform refresh revokes the platform session and denies continuation.
- **TS-TEN-0098:** A `Disabled` principal on platform refresh revokes the platform session and denies continuation (no new token).
- **TS-TEN-0099:** A principal whose active catalog-valid `PlatformSupport` assignments have all been revoked is denied on refresh, and the platform session is revoked.
- **TS-TEN-0100:** Platform refresh-token reuse (a consumed token) marks the platform session compromised/revoked without affecting tenant sessions.
- **TS-TEN-0101:** Platform session-limit accounting is independent: platform sessions are counted only against platform sessions, and tenant sessions are unaffected.
- **TS-TEN-0102:** Disabling a principal (and revoking its platform sessions) leaves the same identity's tenant `AuthenticationSession`s active.
- **TS-TEN-0103:** A tenant refresh token is not resolvable in platform-session persistence and a platform refresh token is not resolvable in tenant-session persistence (cross-plane lookup is structurally impossible).
- **TS-TEN-0104:** Re-enabling a principal does not revive a previously revoked `PlatformAuthenticationSession`; a new session is required.
- **TS-TEN-0105:** A `PlatformAuthenticationSession` (and its refresh-token history) cannot be physically deleted (retained security history), consistent with the existing session-history guard.
- **TS-TEN-0106:** Platform token issuance is denied when the principal has zero active catalog-valid `PlatformSupport` permissions.
- **TS-TEN-0107:** Platform token issuance is denied when the principal `Status == Disabled`.
- **TS-TEN-0108:** Proactively disabling a principal revokes its active platform sessions so the next refresh is denied.
- **TS-TEN-0109:** `StrictAccessTokenValidator` accepts a well-formed platform token (`security_plane=platform`, exactly-one required claims, no tenant claims).
- **TS-TEN-0110:** An unknown/empty `security_plane` value is rejected.
- **TS-TEN-0111:** A duplicated `security_plane` claim is rejected.
- **TS-TEN-0112:** A wrong-case `security_plane` (e.g. `Platform`) is rejected (exact ordinal match).
- **TS-TEN-0113:** A `security_plane=platform` token containing `tenant_id` is rejected.
- **TS-TEN-0114:** A `security_plane=platform` token containing `tenant_user_id` is rejected.
- **TS-TEN-0115:** A `security_plane=platform` token containing a `role` claim is rejected.
- **TS-TEN-0116:** A legacy tenant token without `security_plane` is accepted under the tenant profile; an explicit `security_plane=tenant` tenant token is also accepted.
- **TS-TEN-0117:** A platform token with a duplicated `permission` claim is rejected.

## Platform request-plane authorization and HTTP exposure (Phase 4, DEC-TEN-0023–0026)

Approved for Implementation (implementation pending); planned for slices 4B–4E. The Phase-4A anti-escalation and real-signed-JWT authorization proofs are already implemented (a tenant token cannot satisfy a platform policy; a platform token cannot satisfy a tenant policy; unknown/tenant-scoped/wrong-case permissions and missing/duplicate/wrong-case `security_plane` are denied; a mixed-plane token is rejected at authentication).

- **TS-TEN-0118:** An eligible platform operator obtains a platform session through the dedicated platform login route (server flow: verified identity → resolve principal → Active + ≥1 catalog-valid permission → session/token pair).
- **TS-TEN-0119:** A tenant-only identity (no platform authority) cannot obtain a platform token through the platform login route (generic authentication failure).
- **TS-TEN-0120:** A `Disabled` principal cannot obtain a platform session through platform login.
- **TS-TEN-0121:** A principal with zero catalog-valid `PlatformSupport` permissions cannot obtain a platform session.
- **TS-TEN-0122:** A caller-supplied `security_plane`/`PlatformSupportPrincipalId`/permission list/`SecurityVersion`/`mode` value is ignored or rejected and never confers platform authority.
- **TS-TEN-0123:** The platform refresh route rejects a tenant refresh token.
- **TS-TEN-0124:** The tenant refresh route rejects a platform refresh token.
- **TS-TEN-0125:** A dual-capable identity obtains each plane's session only through that plane's own route (no cross-plane auto-selection or caller switch); the dedicated platform logout revokes only the current platform session.
- **TS-TEN-0126:** Create-vs-disable concurrency (L1) closure: a Disable committed concurrently with a platform login leaves no usable new platform session, achieved by transactionally serialized creation (a `FOR UPDATE`/locking read of the principal authority state); correctness does not depend on deployment isolation settings. (See `TS-TEN-0142` for the two-connection SQL proof.)
- **TS-TEN-0127:** A TENANT-ONLY endpoint (e.g. `/api/platform/auth/logout`, localization `effective`/`batch`) rejects a platform token.
- **TS-TEN-0128:** A PLATFORM-ONLY endpoint rejects a tenant token.
- **TS-TEN-0129:** A PLANE-NEUTRAL endpoint accepts both a valid tenant and a valid platform token.
- **TS-TEN-0130:** An architecture guard fails if a plane-specific endpoint uses a bare `RequireAuthenticatedUser` policy.
- **TS-TEN-0131:** A platform token holding `Platform.Support.Administer` can list platform-support principals and assignments.
- **TS-TEN-0132:** A platform token lacking `Platform.Support.Administer` receives 403 on authority reads.
- **TS-TEN-0133:** A tenant token receives 403 on platform authority reads.
- **TS-TEN-0134:** An administrator can disable their own principal (self-disable permitted).
- **TS-TEN-0135:** An administrator can revoke their own `Platform.Support.Administer` (self-revoke permitted); removal of the last usable administrator is not blocked.
- **TS-TEN-0136:** When no usable platform authority remains (no Active principal holds any active current-catalog `PlatformSupport` permission), or when usable authority remains but no usable **administrative** authority remains, genesis/recovery bootstrap becomes eligible for an eligible configured subject that owns no principal.
- **TS-TEN-0137:** Bootstrap does not silently re-enable a `Disabled` principal; recovery creates a new `Active` principal or fails closed with an operator diagnostic.
- **TS-TEN-0138:** After self-disable or self-revoke, the already-issued platform access JWT retains authority until natural expiry (stateless), while Disable proactively revokes platform sessions and a permission revoke applies at next refresh.
- **TS-TEN-0139:** Last-Administer revoked while non-admin authority remains: seed Principal A (`Active`, `Platform.Support.Administer` + `Platform.Tenants.View`) with no other Administer-capable principal; revoke `Platform.Support.Administer`; assert A stays `Active`, `Platform.Tenants.View` stays active, usable platform authority == true, usable platform administrative authority == false, administrative recovery == eligible, an eligible configured recovery subject can establish new recovery authority, A is not silently re-granted `Administer`, and no `Disabled` principal is re-enabled.
- **TS-TEN-0140:** Administrative-recovery negative case: with no eligible configured recovery subject, loss of administrative authority cannot be recovered automatically — bootstrap fails closed with an operator diagnostic (governed break-glass required); recovery is not claimed to succeed in every environment.
- **TS-TEN-0141:** Platform logout trusted-session resolution: with a valid platform token, logout revokes the platform session identified by the validated `session_id` claim (in the platform store only); a caller-supplied `session_id`/`IdentityId`/`PlatformSupportPrincipalId` cannot select a different target session; refresh afterward is denied; the same identity's tenant session and `AuthenticationAccount.SecurityVersion` are unaffected; the already-issued access JWT remains valid until expiry; a tenant token cannot call platform logout and a platform token cannot cause tenant logout.
- **TS-TEN-0142:** Create-vs-disable serialization (L1): with two independent SQL connections/contexts, run platform-session creation concurrently with principal Disable; assert the fresh terminal DB state after the committed Disable is principal `Disabled` with zero active platform sessions for the principal and no usable refresh continuation, for both interleavings, under actual supported SQL Server settings (correctness independent of RCSI).

## First implementation milestone applicability

The first milestone implements `TS-TEN-0001` through `TS-TEN-0038` where infrastructure exists, excluding any HTTP-specific assertion other than verifying endpoint absence.

FP-002 Milestone 4 supplies the approved authorization milestone for `TS-TEN-0044` through `DEC-AUTH-0057`, `AC-AUTH-0045`, `TS-AUTH-0108`, and `TS-AUTH-0109`. `TS-TEN-0040` through `TS-TEN-0043` remain deferred with public Tenant lifecycle endpoints and Platform-support authorization. `TS-TEN-0045` through `TS-TEN-0059` are deferred with the platform-plane Tenant HTTP transport and its authorization foundation under `ADR-015` and `DEC-TEN-0018`. `TS-TEN-0060` through `TS-TEN-0092` are deferred with the Phase-3 platform-support bootstrap, principal lifecycle, and token authority under `ADR-016` and `DEC-TEN-0019`/`DEC-TEN-0020`/`DEC-TEN-0021` (`TS-TEN-0081` through `TS-TEN-0092` cover the status-migration backfill and bootstrap cardinality/selection/recovery decisions). `TS-TEN-0093` through `TS-TEN-0117` are deferred with the Phase-3C platform token/session profile under `ADR-016` and `DEC-TEN-0022` (platform session persistence and cross-plane refresh isolation, SQL schema/FK/create/refresh/mismatch/disable/zero-permission/reuse/multi-session/tenant-unaffected/cross-plane-lookup/re-enable/physical-delete, and `StrictAccessTokenValidator` platform-profile/mixed-plane/legacy scenarios). `TS-TEN-0118` through `TS-TEN-0142` are **approved** (implementation pending) with the Phase-4 request-plane / HTTP exposure work under `ADR-016` §5 and `DEC-TEN-0023`–`DEC-TEN-0026` (platform login/refresh/logout + server-owned plane selection, request-plane policy taxonomy and endpoint classification, authority-read authorization, last-admin/self-disable/recovery including administrative-authority-loss recovery and the create-vs-disable serialization proof), planned for slices 4B–4E (the administrative-recovery scenarios `TS-TEN-0136`/`TS-TEN-0139`/`TS-TEN-0140` and the create-vs-disable scenario `TS-TEN-0142` are owned by the 4D-0 / 4B prerequisite slices); the Phase-4A authorization primitives and their anti-escalation / real-signed-JWT scenarios are already implemented and committed.

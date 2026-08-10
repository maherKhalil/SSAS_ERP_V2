---
document_id: FP-003-TEST
title: Tenant Lifecycle Test Scenarios
status: Approved for Implementation
version: 1.2
sprint: Sprint-01
module: Platform
---

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

## First implementation milestone applicability

The first milestone implements `TS-TEN-0001` through `TS-TEN-0038` where infrastructure exists, excluding any HTTP-specific assertion other than verifying endpoint absence.

FP-002 Milestone 4 supplies the approved authorization milestone for `TS-TEN-0044` through `DEC-AUTH-0057`, `AC-AUTH-0045`, `TS-AUTH-0108`, and `TS-AUTH-0109`. `TS-TEN-0040` through `TS-TEN-0043` remain deferred with public Tenant lifecycle endpoints and Platform-support authorization. `TS-TEN-0045` through `TS-TEN-0059` are deferred with the platform-plane Tenant HTTP transport and its authorization foundation under `ADR-015` and `DEC-TEN-0018`. `TS-TEN-0060` through `TS-TEN-0092` are deferred with the Phase-3 platform-support bootstrap, principal lifecycle, and token authority under `ADR-016` and `DEC-TEN-0019`/`DEC-TEN-0020`/`DEC-TEN-0021` (`TS-TEN-0081` through `TS-TEN-0092` cover the status-migration backfill and bootstrap cardinality/selection/recovery decisions).

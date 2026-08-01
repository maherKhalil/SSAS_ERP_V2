---
document_id: FP-003-TEST
title: Tenant Lifecycle Test Scenarios
status: Approved for Implementation
version: 1.0
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

## First implementation milestone applicability

The first milestone implements `TS-TEN-0001` through `TS-TEN-0038` where infrastructure exists, excluding any HTTP-specific assertion other than verifying endpoint absence.

FP-002 Milestone 4 supplies the approved authorization milestone for `TS-TEN-0044` through `DEC-AUTH-0057`, `AC-AUTH-0045`, `TS-AUTH-0108`, and `TS-AUTH-0109`. `TS-TEN-0040` through `TS-TEN-0043` remain deferred with public Tenant lifecycle endpoints and Platform-support authorization.

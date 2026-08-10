---
document_id: FP-003
title: Platform Tenant Lifecycle
status: Approved for Implementation
version: 1.2
sprint: Sprint-01
module: Platform
depends_on:
  - ADR-005
  - ADR-015
  - ADR-016
  - FP-001
  - FP-002
---

# Feature Package 003 — Platform Tenant Lifecycle

> **Implementation status (informational).**
> - **Tenant backend milestone:** Implemented and merged — the Tenant aggregate, lifecycle commands and queries, the authentication-eligibility contract, and persistence.
> - **Platform-plane authorization architecture:** Approved — [`ADR-015`](../../14-Engineering/ADR/ADR-015-Platform-Plane-Authentication-Authorization.md) / `DEC-TEN-0018`.
> - **Platform-support permission scope (Phase 1):** Implemented — `Platform.Tenants.View/Manage/Lifecycle` at `PermissionScope.PlatformSupport`, tenant-role escalation invariant, tenant-token defence-in-depth filter.
> - **Platform-support authority persistence (Phase 2):** Implemented — `PlatformSupportPrincipal` + `PlatformPermissionAssignment`, catalog-validated authority read, physical-delete protection, SQL-verified.
> - **Platform-support bootstrap, lifecycle, and authority administration (Phase 3 decisions):** Approved — [`ADR-016`](../../14-Engineering/ADR/ADR-016-Platform-Support-Bootstrap-Lifecycle-Authority.md) and `DEC-TEN-0019`/`DEC-TEN-0020`/`DEC-TEN-0021`. **Not implemented.**
> - **HTTP transport:** Deferred. The tenant lifecycle HTTP endpoints described in [`api-contracts.md`](api-contracts.md) remain unimplemented.
>
> This note records implementation and approval state. No earlier FP-003 requirement, decision, or contract is otherwise changed.

## Platform-plane authorization — future implementation impact

The platform-plane transport (`ADR-015`/`ADR-016`) is documented and approved. Phase 1–2 are implemented and committed; the phases below are **not** implemented.

- **New persistent global constructs (Phase 2, implemented):** `PlatformSupportPrincipal` + `PlatformPermissionAssignment`, both global and non-tenant-owned, anchored to the existing global `Identity`.
- **Additional persistence before Phase 3 issuance (`ADR-016`, not implemented):** a principal `Status {Active,Disabled}` column + `StatusChangedUtc/By` (small additive migration), and a platform-capable session representation.
- **Tenant Domain, Tenant Application, and Tenant persistence:** unchanged.

Recommended implementation phases (documentation only; Phase 1–2 done, the rest not implemented):

1. **(Done)** Permission scope/catalog foundation + `Platform.Tenants.*` + escalation regression tests + tenant-token defence-in-depth filter.
2. **(Done)** `PlatformSupportPrincipal` authority + permission assignments + persistence/migration + physical-delete protection + SQL verification.
3. Phase 3 (`ADR-016`), in slices:
   - **3A** — principal `Active/Disabled` domain model + `StatusChangedUtc/By`, status migration, lifecycle persistence/application support, tests + SQL.
   - **3B** — bootstrap configuration + genesis/recovery gate (`DEC-TEN-0019`); author `Platform.Support.Administer` in the catalog (`DEC-TEN-0021`); tests.
   - **3C** — platform token/session profile, `security_plane=platform`, platform token issuance, live principal-status eligibility, claims sourced from `IPlatformSupportPermissionReadService`, platform refresh/session behaviour, `StrictAccessTokenValidator` platform profile.
4. `PlatformPermissionAuthorizationHandler` + `RequirePlatformPermission` + DI/Host policy; expose platform-authority administration only when authorized by `Platform.Support.Administer`.
5. FP-003 Tenant HTTP transport + Admin Transport reuse + architecture-test replacement.
6. Final security review. **Production track:** mandatory MFA / strong-auth and session hardening before platform-support Production enablement.

## Purpose

This package defines the smallest Platform-owned tenant lifecycle needed to provide one trusted source of tenant authentication eligibility.

It enables FP-001 and FP-002 workflows to determine whether a tenant may:

- participate in tenant selection;
- receive a new authentication session;
- refresh an existing authentication session;
- authorize ordinary tenant business access.

Only an `Active` tenant is authentication-eligible.

## Approval status

This package is approved for implementation. The governing decisions are recorded in [`decisions-approved.md`](decisions-approved.md); the lifecycle portions of the higher-level Draft Tenant Management document have been reconciled to FP-003.

## Scope

FP-003 covers:

- the Platform-owned `Tenant` aggregate;
- tenant code and display name;
- `Provisioning`, `Active`, `Suspended`, and `Archived` lifecycle states;
- explicit lifecycle transitions;
- a narrow tenant-authentication-eligibility read contract;
- lifecycle commands and queries;
- safe domain events;
- persistence in the existing Platform SQL Server boundary;
- concurrency, history retention, and test requirements.

## Explicit exclusions

FP-003 does not define subscription plans, billing, payment state, companies, first-administrator provisioning, branding, themes, localization, currencies, custom domains, notifications, Angular administration, support impersonation, cross-tenant business-data access, authentication sessions, refresh tokens, JWT issuance, or immutable audit storage.

Physical tenant deletion is prohibited. Archive is the terminal lifecycle operation.

## Documents

1. [`requirements.md`](requirements.md)
2. [`business-rules.md`](business-rules.md)
3. [`domain-model.md`](domain-model.md)
4. [`lifecycle-model.md`](lifecycle-model.md)
5. [`authorization-model.md`](authorization-model.md)
6. [`api-contracts.md`](api-contracts.md)
7. [`data-model.md`](data-model.md)
8. [`acceptance-criteria.md`](acceptance-criteria.md)
9. [`test-scenarios.md`](test-scenarios.md)
10. [`decisions-approved.md`](decisions-approved.md)
11. [`traceability-matrix.md`](traceability-matrix.md)

## Architecture constraints

- Platform owns the aggregate, application contracts, persistence, and future API contracts.
- `TenantId` is the existing authoritative Guid used by tenant-owned records.
- `Tenant` is a Platform-level aggregate and is not tenant query-filtered.
- Tenant-owned business records continue to use automatic `TenantId` isolation.
- Repositories are aggregate-specific; no generic repository or `IQueryable` boundary is permitted.
- Commands and queries follow CQRS.
- Domain and Application remain independent from EF Core, SQL Server, ASP.NET Core, and HTTP.
- State is persisted through the existing `PlatformDbContext` and `IPlatformUnitOfWork`.
- Domain events contain no secrets and are dispatched only after successful persistence.

## First implementation milestone

The first implementation milestone is an internal Domain/Application/SQL Server slice containing the Tenant aggregate; TenantCode and TenantName value objects; TenantStatus and TenantStatusChangeReasonCode; lifecycle transitions; the authentication-eligibility contract; an aggregate-specific repository; CreateTenantCommand, ActivateTenantCommand, SuspendTenantCommand, ReactivateTenantCommand, and ArchiveTenantCommand; GetTenantQuery, ListTenantsQuery, and GetTenantAuthenticationEligibilityQuery; persistence through the existing PlatformDbContext and IPlatformUnitOfWork; the `platform.Tenants` table; the `AddTenantLifecycle` migration; and Domain, Application, SQL Server integration, and architecture tests.

Tenant HTTP endpoints, authentication sessions, refresh tokens, and JWT work remain outside that milestone.

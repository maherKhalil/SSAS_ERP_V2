---
document_id: FP-003
title: Platform Tenant Lifecycle
status: Approved for Implementation
version: 1.1
sprint: Sprint-01
module: Platform
depends_on:
  - ADR-005
  - ADR-015
  - FP-001
  - FP-002
---

# Feature Package 003 — Platform Tenant Lifecycle

> **Implementation status (informational).**
> - **Backend milestone:** Implemented and merged — the Tenant aggregate, lifecycle commands and queries, the authentication-eligibility contract, and persistence.
> - **Platform-plane authorization architecture:** Approved — resolved by [`ADR-015`](../../14-Engineering/ADR/ADR-015-Platform-Plane-Authentication-Authorization.md) and recorded in [`decisions-approved.md`](decisions-approved.md) as `DEC-TEN-0018`.
> - **HTTP transport:** Ready for implementation after the platform-plane authorization foundation is delivered. The tenant lifecycle HTTP endpoints described in [`api-contracts.md`](api-contracts.md) remain deferred and are not yet implemented.
>
> This note records implementation and approval state. The platform-plane authorization decision is captured in `ADR-015` / `DEC-TEN-0018`; no earlier FP-003 requirement, decision, or contract is otherwise changed.

## Platform-plane authorization — future implementation impact

The platform-plane transport (`ADR-015`, `DEC-TEN-0018`) is documented and approved but not implemented. When it is built, the expected impact is:

- **New persistent global constructs:** a `PlatformSupportPrincipal` authority and a `PlatformPermissionAssignment`, both global and non-tenant-owned, anchored to the existing global `Identity`, plus a platform-capable session representation. Exact table and column names are deferred to implementation.
- **EF migration expected:** yes. **SQL verification expected:** yes.
- **Tenant Domain, Tenant Application, and Tenant persistence:** unchanged.

Recommended implementation phases (documentation only; none implemented here):

1. Permission scope/catalog foundation + `Platform.Tenants.*` definitions + escalation regression tests.
2. `PlatformSupportPrincipal` authority + permission assignments + persistence/migration + SQL verification.
3. Platform token/session profile (issuer/validator, claims sourcing) + tenant claims-provider `PermissionScope.Tenant` defense-in-depth filter.
4. `PlatformPermissionAuthorizationHandler` + `RequirePlatformPermission` + real Host security tests.
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

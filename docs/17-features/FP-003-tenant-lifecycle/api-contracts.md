---
document_id: FP-003-API
title: Tenant Lifecycle API Contracts
status: Approved for Implementation
version: 1.1
sprint: Sprint-01
module: Platform
---

# API Contracts

## Delivery boundary

These are future Platform-level HTTP contracts. The first FP-003 implementation milestone exposes no tenant lifecycle endpoint and delivers only Domain, Application, SQL Server, and tests.

The platform-support authentication model and the exact permission identifiers are now resolved by `ADR-015` (Platform-Plane Authentication and Authorization). Endpoint implementation still depends on delivering the platform-plane authorization foundation (platform-support token profile, `PlatformSupportPrincipal` authority, and `PlatformPermissionAuthorizationHandler`), immutable audit integration, and implementation of the approved centralized current-status policy where applicable.

## Security plane

Every route in this document is **platform-plane** under `ADR-015`. All seven routes require **platform-support authentication** (a validated `security_plane=platform` token) and the exact `PermissionScope.PlatformSupport` permission listed below, enforced through `RequirePlatformPermission(...)`. A tenant-plane token (carrying `tenant_id`, tenant-derived permissions) cannot call these routes. Route `TenantId` is a **target aggregate identifier only** and never establishes `ICurrentTenant` or caller scope.

| Route | Required platform permission |
|---|---|
| `GET /api/platform/tenants` | `Platform.Tenants.View` `[DEFERRED - AC-TEN-0020]` |
| `GET /api/platform/tenants/{tenantId}` | `Platform.Tenants.View` `[DEFERRED - AC-TEN-0020]` |
| `POST /api/platform/tenants` | `Platform.Tenants.Manage` `[DEFERRED - AC-TEN-0020]` |
| `POST /api/platform/tenants/{tenantId}/activate` | `Platform.Tenants.Lifecycle` `[DEFERRED - AC-TEN-0020]` |
| `POST /api/platform/tenants/{tenantId}/suspend` | `Platform.Tenants.Lifecycle` `[DEFERRED - AC-TEN-0020]` |
| `POST /api/platform/tenants/{tenantId}/reactivate` | `Platform.Tenants.Lifecycle` `[DEFERRED - AC-TEN-0020]` |
| `POST /api/platform/tenants/{tenantId}/archive` | `Platform.Tenants.Lifecycle` `[DEFERRED - AC-TEN-0020]` |

## Conventions

- Base route: `/api/platform/tenants`.
- HTTPS, versioning, bounded pagination, and Problem Details apply.
- All routes require explicit platform-plane authorization (`RequirePlatformPermission`); see the security-plane matrix above.
- Tenant administrators cannot call these routes through tenant roles.
- `TenantId` in a route path is the target aggregate identifier only; on create it is server-generated.
- Status is never accepted as a writable field in create input.
- Lifecycle commands include the project-standard expected rowversion representation.
- Stale concurrency returns 409 or the project-standard equivalent.
- No physical-delete endpoint exists.

## Create tenant

```http
POST /api/platform/tenants   [DEFERRED - AC-TEN-0020]
```

Request:

```json
{
  "tenantCode": "ACME",
  "tenantName": "Acme Trading"
}
```

The result contains safe Tenant lifecycle data and `Provisioning` status. It creates no company, administrator, subscription, branding, or notification.

## Get tenant

```http
GET /api/platform/tenants/{tenantId}   [DEFERRED - AC-TEN-0020]
```

Returns only safe Platform lifecycle data and concurrency version.

## List tenants

```http
GET /api/platform/tenants?pageNumber=1&pageSize=50&status=Active   [DEFERRED - AC-TEN-0020]
```

Only approved bounded filters are accepted. The query returns lifecycle projections, not tenant business data.

## Lifecycle commands

```http
POST /api/platform/tenants/{tenantId}/activate   [DEFERRED - AC-TEN-0020]
POST /api/platform/tenants/{tenantId}/suspend   [DEFERRED - AC-TEN-0020]
POST /api/platform/tenants/{tenantId}/reactivate   [DEFERRED - AC-TEN-0020]
POST /api/platform/tenants/{tenantId}/archive   [DEFERRED - AC-TEN-0020]
```

Every lifecycle command carries a bounded `StatusChangeReasonCode`; suspension and archive require an explicit non-`Created` value. Free-form reason text, secrets, and billing detail are not accepted.

## Authentication eligibility

`GetTenantAuthenticationEligibility` is an internal Platform Application query contract. No public eligibility endpoint is included in the first milestone.

If a future operational endpoint is approved, it must require Platform authorization and return only:

```json
{
  "tenantId": "...",
  "exists": true,
  "tenantStatus": "Active",
  "isAuthenticationEligible": true,
  "tenantAuthenticationIneligibilityReason": "None"
}
```

> **Reconciled 2026-08-29 (T-161). None of the seven lifecycle routes above is built, and the eighth is
> superseded.** Marked inline so a sweep can read the state without a human.
>
> **`[DEFERRED - AC-TEN-0020]` RECORDS a decision; it does not make one.** Tenant endpoints are deferred by
> FP-003's own first-milestone scope statement, enforced by
> `Tenant_endpoints_remain_deferred_and_the_platform_api_does_not_reach_tenant_application`. **Whether that
> deferral still stands is the owner's call and is open** — `AC-TEN-0021` through `AC-TEN-0030` specify this
> transport in full, with fifteen scenarios, and T-156 found the platform plane they depend on has already
> shipped. **This marker states where things are, not where they should go.**
>
> **`[SUPERSEDED - ...]` is a different state and must not be conflated with it.** The `DELETE` below is not
> waiting on anyone: `DEC-TEN-0007` gives the repository no delete, the migration installs
> `TR_Tenants_PreventDelete`, and archive is the only terminal operation. **Deferred means "not yet";
> superseded means "never".**
>
> ⚠ **The heading below already says this in prose, and that is exactly why the row is marked.** A sweep
> reads rows, not headings — `DEC-L-002`.

## Explicitly superseded Draft contracts

The following former Draft Tenant Management contract is superseded by approved FP-003:

```http
DELETE /api/platform/tenants/{id}   [SUPERSEDED - no delete exists, by DEC-TEN-0007]
```

Archive is the only terminal lifecycle operation. No delete permission, command, repository method, or endpoint is defined.

## Exclusions

No API contract is defined here for subscriptions, billing, companies, first-administrator provisioning, branding, localization, configuration, notification delivery, impersonation, authentication sessions, refresh tokens, JWT issuance, or Angular.

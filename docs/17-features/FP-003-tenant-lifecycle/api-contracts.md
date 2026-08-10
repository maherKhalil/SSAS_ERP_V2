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
| `GET /api/platform/tenants` | `Platform.Tenants.View` |
| `GET /api/platform/tenants/{tenantId}` | `Platform.Tenants.View` |
| `POST /api/platform/tenants` | `Platform.Tenants.Manage` |
| `POST /api/platform/tenants/{tenantId}/activate` | `Platform.Tenants.Lifecycle` |
| `POST /api/platform/tenants/{tenantId}/suspend` | `Platform.Tenants.Lifecycle` |
| `POST /api/platform/tenants/{tenantId}/reactivate` | `Platform.Tenants.Lifecycle` |
| `POST /api/platform/tenants/{tenantId}/archive` | `Platform.Tenants.Lifecycle` |

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
POST /api/platform/tenants
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
GET /api/platform/tenants/{tenantId}
```

Returns only safe Platform lifecycle data and concurrency version.

## List tenants

```http
GET /api/platform/tenants?pageNumber=1&pageSize=50&status=Active
```

Only approved bounded filters are accepted. The query returns lifecycle projections, not tenant business data.

## Lifecycle commands

```http
POST /api/platform/tenants/{tenantId}/activate
POST /api/platform/tenants/{tenantId}/suspend
POST /api/platform/tenants/{tenantId}/reactivate
POST /api/platform/tenants/{tenantId}/archive
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

## Explicitly superseded Draft contracts

The following former Draft Tenant Management contract is superseded by approved FP-003:

```http
DELETE /api/platform/tenants/{id}
```

Archive is the only terminal lifecycle operation. No delete permission, command, repository method, or endpoint is defined.

## Exclusions

No API contract is defined here for subscriptions, billing, companies, first-administrator provisioning, branding, localization, configuration, notification delivery, impersonation, authentication sessions, refresh tokens, JWT issuance, or Angular.

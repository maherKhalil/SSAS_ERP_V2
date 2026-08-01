---
document_id: FP-003-API
title: Tenant Lifecycle API Contracts
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# API Contracts

## Delivery boundary

These are future Platform-level HTTP contracts. The first FP-003 implementation milestone exposes no tenant lifecycle endpoint and delivers only Domain, Application, SQL Server, and tests.

Endpoint implementation requires Platform-support authentication, exact permission identifiers, immutable audit integration, and implementation of the approved centralized current-status policy where applicable.

## Conventions

- Base route: `/api/platform/tenants`.
- HTTPS, versioning, bounded pagination, and Problem Details apply.
- All routes require explicit Platform-level authorization.
- Tenant administrators cannot call these routes through tenant roles.
- `TenantId` is server-generated during creation.
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

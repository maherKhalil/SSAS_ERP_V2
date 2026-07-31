---
document_id: FP-001-API
title: Identity and Access API Contracts
status: Approved
version: 1.0
---

# API Contracts

## Conventions

- Base route: `/api/platform`
- Tenant identity is trusted server-side context.
- Tenant ID is not accepted as a mutable field in tenant-admin commands.
- Platform-support routes use explicit support authorization and trusted target-tenant selection.
- Lists use bounded pagination.
- Validation uses Problem Details.
- 401 is unauthenticated; 403 is unauthorized.
- Concurrency conflicts return 409 or the project-standard equivalent.
- No user deletion endpoint exists.

## Tenant users

```http
GET /api/platform/users
GET /api/platform/users/{userId}
POST /api/platform/users/invitations
PUT /api/platform/users/{userId}
POST /api/platform/users/{userId}/deactivate
POST /api/platform/users/{userId}/reactivate
POST /api/platform/users/{userId}/roles
DELETE /api/platform/users/{userId}/roles/{roleId}
```

Invitation draft:

```json
{
  "email": "user@example.com",
  "displayName": "Example User"
}
```

Under `DEC-AUTH-0025`, an invitation creates or targets a `Pending` membership and does not stage or assign roles. No role identifiers are stored in the invitation or its account-action token. Authorized administrators assign roles after membership activation.

Inviting an already active membership is rejected. A deactivated membership is restored only through the approved reactivation operation. First-tenant-administrator provisioning remains part of tenant provisioning rather than this invitation flow.

## Tenant selection

These contracts belong jointly with the authentication feature:

```http
GET /api/platform/me/tenant-memberships
POST /api/platform/me/select-tenant
```

Selection request:

```json
{
  "tenantId": "..."
}
```

The server validates that the authenticated identity has an active membership in the selected tenant.

When exactly one active membership exists, the client bypasses the selection view and the server may complete selection automatically.

## Roles

```http
GET /api/platform/roles
GET /api/platform/roles/{roleId}
POST /api/platform/roles
PUT /api/platform/roles/{roleId}
POST /api/platform/roles/{roleId}/request-retirement
POST /api/platform/roles/{roleId}/retire
POST /api/platform/roles/{roleId}/permissions
DELETE /api/platform/roles/{roleId}/permissions/{permission}
```

Retirement fails while active-user assignments exist.

## Permission catalog

```http
GET /api/platform/permissions
```

## Platform support

Proposed support routes:

```http
GET /api/platform/support/tenants/{tenantId}/users
POST /api/platform/support/tenants/{tenantId}/users/invitations
POST /api/platform/support/tenants/{tenantId}/users/{userId}/deactivate
```

Each operation requires explicit platform-support permission, validates the target tenant through trusted server-side logic, and produces an audit record.

## Explicit exclusions

- user deletion;
- direct user-permission assignment;
- password management;
- login;
- refresh-token implementation;
- logout;
- MFA;
- password reset and account recovery.

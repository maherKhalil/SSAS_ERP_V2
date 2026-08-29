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

> **Reconciled 2026-08-29 (T-160). Of the twenty-two routes below, THREE are built, one is served by a
> different route, and eighteen are not routed.** This document had never been compared to the code.
> **Every row is marked inline with its actual state**, and the states are not interchangeable.
>
> | State | Rows | Meaning |
> |---|---|---|
> | `[BUILT]` | 1 | `GET /api/platform/roles` — exists exactly as documented |
> | `[BUILT as ...]` | 3 | exists under a **different path**: the two user lifecycle routes are `/tenant-users/{id}/deactivation` and `/reactivation`, and tenant selection is `POST /api/platform/auth/select-tenant` |
> | `[SERVED BY ...]` | 1 | `GET /me/tenant-memberships` has no route; the list comes back on the **login response** as `TenantMembershipResponse` |
> | `[NOT ROUTED - handler: X]` | 17 | the handler **exists and is registered**; nothing maps a route to it |
>
> ---- ⚠ THIS IS THE "HANDLERS BUILT, TRANSPORT MISSING" SHAPE, AND IT IS THE ONLY DOCUMENT THAT IS.
>
> **Twenty-seven role and tenant-user handlers exist** — `CreateCustomRole`, `UpdateCustomRole`,
> `RetireRole`, `RequestRoleRetirement`, `AssignPermissionToRole`, `RemovePermissionFromRole`,
> `ListRoles`, `GetRoleById`, `IssueTenantUserInvitation`, `CompleteInvitation`, `AssignRoleToTenantUser`,
> `RemoveRoleFromTenantUser`, `ListTenantUsers`, `GetTenantUserById`, `UpdateTenantUserProfile` and more.
> **The live transport is one route plus four tenant-user lifecycle routes.**
>
> **FP-014 was described the same way and it was wrong there** (`T-159`: its domain and read path exist, but
> there are no write handlers, no permissions and no `Invoice` type at all). **Here the description holds.**
>
> ---- ⚠ FOUR ROWS ARE BUILT, AND MARKING THEM "ABSENT" WOULD HAVE BEEN THE WORSE ERROR.
>
> A sweep matching documented paths against live paths reports all four as missing, because the code uses
> `tenant-users` where this document says `users`, and puts tenant selection under `auth` rather than `me`.
> **They are naming divergences, not gaps** — the capability shipped. `DEC-L-085` cuts both ways: an
> unverified row inherits authority it never earned, and a row whose capability moved looks unbuilt to any
> instrument comparing spellings.
>
> **Marked inline rather than in an added column** (`DEC-L-086`, and the FP-007 lesson): a correction in a
> fifth column is invisible to a sweep reading the first.
>
> **Nothing here is deleted and nothing is a proposal to build it.**

## Tenant users

```http
GET /api/platform/users   [NOT ROUTED - handler: ListTenantUsersQueryHandler]
GET /api/platform/users/{userId}   [NOT ROUTED - handler: GetTenantUserByIdQueryHandler]
POST /api/platform/users/invitations   [NOT ROUTED - handler: IssueTenantUserInvitationCommandHandler]
PUT /api/platform/users/{userId}   [NOT ROUTED - handler: UpdateTenantUserProfileCommandHandler]
POST /api/platform/users/{userId}/deactivate   [BUILT as POST /api/platform/tenant-users/{tenantUserId}/deactivation]
POST /api/platform/users/{userId}/reactivate   [BUILT as POST /api/platform/tenant-users/{tenantUserId}/reactivation]
POST /api/platform/users/{userId}/roles   [NOT ROUTED - handler: AssignRoleToTenantUserCommandHandler]
DELETE /api/platform/users/{userId}/roles/{roleId}   [NOT ROUTED - handler: RemoveRoleFromTenantUserCommandHandler]
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
GET /api/platform/me/tenant-memberships   [SERVED BY THE LOGIN RESPONSE - TenantMembershipResponse]
POST /api/platform/me/select-tenant   [BUILT as POST /api/platform/auth/select-tenant]
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
GET /api/platform/roles   [BUILT]
GET /api/platform/roles/{roleId}   [NOT ROUTED - handler: GetRoleByIdQueryHandler]
POST /api/platform/roles   [NOT ROUTED - handler: CreateCustomRoleCommandHandler]
PUT /api/platform/roles/{roleId}   [NOT ROUTED - handler: UpdateCustomRoleCommandHandler]
POST /api/platform/roles/{roleId}/request-retirement   [NOT ROUTED - handler: RequestRoleRetirementCommandHandler]
POST /api/platform/roles/{roleId}/retire   [NOT ROUTED - handler: RetireRoleCommandHandler]
POST /api/platform/roles/{roleId}/permissions   [NOT ROUTED - handler: AssignPermissionToRoleCommandHandler]
DELETE /api/platform/roles/{roleId}/permissions/{permission}   [NOT ROUTED - handler: RemovePermissionFromRoleCommandHandler]
```

Retirement fails while active-user assignments exist.

## Permission catalog

```http
GET /api/platform/permissions   [NOT ROUTED - handler: ListPermissionCatalogQueryHandler]
```

## Platform support

Proposed support routes:

```http
GET /api/platform/support/tenants/{tenantId}/users   [NOT ROUTED - handler: ListTenantUsersQueryHandler; no support-scoped route exists]
POST /api/platform/support/tenants/{tenantId}/users/invitations   [NOT ROUTED - handler: IssueTenantUserInvitationCommandHandler; no support-scoped route]
POST /api/platform/support/tenants/{tenantId}/users/{userId}/deactivate   [NOT ROUTED - handler: DeactivateTenantUserCommandHandler; no support-scoped route]
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

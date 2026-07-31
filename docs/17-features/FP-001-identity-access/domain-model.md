---
document_id: FP-001-DOM
title: Identity and Access Domain Model
status: Approved
version: 1.0
---

# Domain Model

## Bounded context

**Platform Identity and Access Management**

## Model overview

A person may have multiple tenant memberships. Each membership is independently owned by a tenant and has tenant-specific roles and lifecycle state.

Platform-support access is modeled separately from tenant membership and requires explicit platform-level permissions.

## Proposed aggregates

### Identity aggregate

Represents the authenticated person/account boundary.

Responsibilities:

- preserve the authentication subject;
- associate the person with one or more tenant memberships;
- support tenant discovery after authentication;
- prevent duplicate active memberships for the same tenant.

Credentials and token lifecycle are implemented in the separate authentication feature package.

### TenantUser aggregate

Represents a user's membership and profile inside one tenant.

Fields:

- `TenantUserId`
- `IdentityId`
- `TenantId`
- `Email`
- `DisplayName`
- `Status`
- audit metadata
- concurrency token

Responsibilities:

- preserve immutable tenant ownership;
- enforce per-tenant email uniqueness;
- manage tenant-specific role assignments;
- enforce deactivation and reactivation;
- prevent duplicate role assignments;
- emit lifecycle and assignment events.

Child entity:

- `TenantUserRoleAssignment`

### Role aggregate

Fields:

- `RoleId`
- `TenantId`
- `Name`
- `Description`
- `RoleType` (`System` or `Custom`)
- `Status`
- audit metadata
- concurrency token

Responsibilities:

- preserve tenant ownership;
- protect system roles;
- manage permission assignments;
- prevent duplicate permission assignments;
- reject new assignments when retirement is pending;
- reject retirement while active users remain assigned;
- preserve history after retirement.

Child entity:

- `RolePermissionAssignment`

### Permission catalog

Permissions are immutable code-owned definitions for V1.

They are not tenant-created entities.

### PlatformSupportAccess

Platform-support authorization is separate from tenant roles.

It may be represented through platform-level roles/permissions or an equivalent explicit administrative authorization model, but it must not reuse tenant role assignments to grant cross-tenant access.

## Value objects

- `EmailAddress`
- `RoleName`
- `PermissionName`
- `UserDisplayName`

## Enumerations

### TenantUserStatus

- `Pending`
- `Active`
- `Deactivated`

### RoleStatus

- `Active`
- `RetirementPending`
- `Retired`

### RoleType

- `System`
- `Custom`

## Domain events

- `TenantUserInvited`
- `TenantUserActivated`
- `TenantUserDeactivated`
- `TenantUserReactivated`
- `TenantUserRoleAssigned`
- `TenantUserRoleRemoved`
- `RoleCreated`
- `RoleUpdated`
- `RoleRetirementRequested`
- `RoleRetired`
- `RolePermissionAssigned`
- `RolePermissionRemoved`
- `TenantMembershipSelected`
- `PlatformSupportTenantAccessed`

Events must not contain secrets.

## Repository contracts

Per ADR-010, repositories are aggregate-specific and module-owned:

- `IIdentityRepository`
- `ITenantUserRepository`
- `IRoleRepository`

No generic repository is permitted.

## Core invariants

1. Tenant ownership cannot change.
2. Email is unique per tenant.
3. A role assignment must remain within one tenant.
4. A tenant token contains exactly one tenant.
5. Duplicate role and permission assignments are rejected.
6. Retired or retirement-pending roles cannot receive new assignments.
7. A role with active-user assignments cannot be retired.
8. A deactivated user cannot receive usable tenant access.
9. Physical user deletion is prohibited.
10. Platform support uses explicit platform-level authorization.

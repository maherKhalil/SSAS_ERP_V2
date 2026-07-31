---
document_id: FP-001-DATA
title: Identity and Access Data Model
status: Approved
version: 1.0
---

# Data Model

## Ownership

The Platform module owns its concrete `DbContext`, schema, mappings, migrations, and aggregate-specific repositories.

## Proposed tables

### Identities

| Column | Purpose |
|---|---|
| IdentityId | Primary key |
| Subject | Authentication subject |
| CreatedUtc/By | Audit |
| ModifiedUtc/By | Audit |
| RowVersion | Concurrency |

Credentials are defined in the separate authentication package.

### TenantUsers

| Column | Purpose |
|---|---|
| TenantUserId | Primary key |
| IdentityId | Person/account reference |
| TenantId | Immutable tenant ownership |
| Email | Unique within tenant |
| DisplayName | Tenant-specific profile |
| Status | Pending, Active, Deactivated |
| CreatedUtc/By | Audit |
| ModifiedUtc/By | Audit |
| RowVersion | Concurrency |

Unique constraints:

- `(TenantId, Email)`
- `(TenantId, IdentityId)`

### Roles

| Column | Purpose |
|---|---|
| RoleId | Primary key |
| TenantId | Immutable tenant ownership |
| Name | Unique within tenant |
| Description | Optional |
| RoleType | System or Custom |
| Status | Active, RetirementPending, Retired |
| CreatedUtc/By | Audit |
| ModifiedUtc/By | Audit |
| RowVersion | Concurrency |

Unique constraint:

- `(TenantId, Name)`

### TenantUserRoleAssignments

| Column | Purpose |
|---|---|
| AssignmentId | Primary key |
| TenantId | Isolation |
| TenantUserId | Tenant user |
| RoleId | Same-tenant role |
| AssignedUtc | Audit timestamp |
| AssignedBy | Actor |

Unique constraint:

- `(TenantId, TenantUserId, RoleId)`

### RolePermissionAssignments

| Column | Purpose |
|---|---|
| AssignmentId | Primary key |
| TenantId | Isolation |
| RoleId | Role |
| PermissionName | Code-owned identifier |
| AssignedUtc | Audit timestamp |
| AssignedBy | Actor |

Unique constraint:

- `(TenantId, RoleId, PermissionName)`

### PlatformSupportAssignments

Conceptual storage for platform-level support roles/permissions.

It must be separate from tenant role assignments and must not use a tenant role to grant cross-tenant access.

## Deletion behavior

- physical deletion of users is prohibited;
- cascades are restricted;
- roles are retired, not deleted;
- audit and assignment history is preserved;
- tenant-owned relationships enforce same-tenant integrity.

## Query filters

Tenant-owned entities use context-instance tenant filters.

Platform-support workflows use explicit, separately authorized cross-tenant queries and must not disable filters casually in ordinary tenant services.

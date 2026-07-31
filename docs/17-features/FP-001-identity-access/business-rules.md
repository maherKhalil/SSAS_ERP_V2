---
document_id: FP-001-BR
title: Identity and Access Business Rules
status: Approved
version: 1.0
---

# Business Rules

### BRULE-IAM-0001 — Tenant ownership is immutable

Tenant ownership of a tenant user, role, membership, or assignment cannot be changed.

### BRULE-IAM-0002 — Tenant administrators are tenant-bound

A tenant administrator cannot access or administer any other tenant.

### BRULE-IAM-0003 — Platform support is a separate authorization plane

App Owner / App Support access is granted only through explicit platform-support permissions and is not inferred from tenant roles.

### BRULE-IAM-0004 — Same-tenant assignment

A role may be assigned only to a user membership in the same tenant.

### BRULE-IAM-0005 — Email uniqueness

Email is unique within a tenant, not globally.

### BRULE-IAM-0006 — One tenant per token

Every tenant-scoped token contains exactly one tenant identity.

### BRULE-IAM-0007 — Tenant selection

When exactly one active tenant membership exists, select it automatically. When more than one exists, require explicit post-authentication selection.

### BRULE-IAM-0008 — Exact authorization identifiers

Role and permission identifiers are matched exactly and ordinally.

### BRULE-IAM-0009 — Roles do not imply permissions by name

A role grants only explicitly assigned permissions.

### BRULE-IAM-0010 — Code-owned permission catalog

Only application-defined permission identifiers may be assigned.

### BRULE-IAM-0011 — No direct user permissions

Permissions are assigned to roles only.

### BRULE-IAM-0012 — Multiple roles allowed

A user may hold multiple active roles within the same tenant.

### BRULE-IAM-0013 — Assignment uniqueness

The same role cannot be assigned twice to the same tenant membership.

### BRULE-IAM-0014 — Role-permission uniqueness

The same permission cannot be assigned twice to the same role.

### BRULE-IAM-0015 — Disabled user access

A deactivated user cannot obtain or refresh usable tenant access.

### BRULE-IAM-0016 — No physical user deletion

Users are deactivated, never physically deleted.

### BRULE-IAM-0017 — Role retirement precondition

A role cannot be retired while assigned to any active user.

### BRULE-IAM-0018 — Role retirement restrictions

A role prepared for retirement cannot receive new assignments. On successful retirement, it no longer grants permissions and its history is preserved.

### BRULE-IAM-0019 — Audited changes

User lifecycle changes, membership changes, role changes, assignments, permission changes, and platform-support actions record audit metadata.

### BRULE-IAM-0020 — Concurrency

Stale updates fail rather than overwrite newer data.

### BRULE-IAM-0021 — Cross-tenant reads fail safely

Tenant queries never disclose another tenant's data.

### BRULE-IAM-0022 — Tenant suspension

A suspended tenant cannot receive normal application access or new tenant-scoped tokens.

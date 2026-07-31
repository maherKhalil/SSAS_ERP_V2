---
document_id: FP-001-REQ
title: Identity and Access Requirements
status: Approved
version: 1.0
---

# Requirements

## Business requirements

### BR-IAM-0001 — Tenant isolation

Tenant users, tenant administrators, roles, permissions, and assignments must be isolated by tenant.

### BR-IAM-0002 — Platform support administration

An explicitly authorized App Owner / App Support actor may access and administer multiple tenants without receiving cross-tenant access through ordinary tenant roles.

### BR-IAM-0003 — Controlled user access

Only active, authorized users may access tenant functionality.

### BR-IAM-0004 — Role-based administration

Authorized tenant administrators can manage eligible tenant roles and assignments within their own tenant.

### BR-IAM-0005 — Permission-based authorization

Access is determined by explicit application permissions evaluated against a validated, tenant-scoped identity.

### BR-IAM-0006 — Least privilege

No permission is inferred from a role name alone, and direct user-permission assignment is not supported.

### BR-IAM-0007 — Account lifecycle

Users are invited, activated, deactivated, and where allowed reactivated. Physical deletion is prohibited.

### BR-IAM-0008 — Multi-tenant membership

A person may hold separate memberships in multiple tenants, but every tenant token is scoped to exactly one selected tenant.

### BR-IAM-0009 — Traceability

Security-sensitive changes must be attributable to an authenticated actor and timestamped in UTC.

## Functional requirements

### FR-IAM-0101 — Invite tenant user

An authorized tenant administrator or platform-support actor can invite a user into an allowed tenant.

### FR-IAM-0102 — View tenant users

A tenant administrator can list and view users in the current tenant only. Platform-support access requires explicit support permission.

### FR-IAM-0103 — Update user profile

Authorized actors can update approved user profile fields without changing tenant ownership.

### FR-IAM-0104 — Deactivate and reactivate user

Authorized actors can deactivate or reactivate a user according to the approved lifecycle.

### FR-IAM-0105 — Create custom role

An authorized tenant administrator can create a custom role in the current tenant.

### FR-IAM-0106 — Update custom role

An authorized tenant administrator can update an eligible custom role.

### FR-IAM-0107 — Retire role

A role can be retired only when it has no active-user assignments.

### FR-IAM-0108 — List permission catalog

Authorized actors can view the code-owned application permission catalog.

### FR-IAM-0109 — Assign permissions to role

Authorized tenant administrators can assign catalog permissions to eligible roles in their tenant.

### FR-IAM-0110 — Assign roles to user

Authorized tenant administrators can assign one or more eligible roles to a user in the same tenant.

### FR-IAM-0111 — Remove role assignment

Authorized tenant administrators can remove an eligible role assignment.

### FR-IAM-0112 — Resolve effective permissions

Effective permissions are the distinct union of permissions from all active roles assigned to the user in the current tenant.

### FR-IAM-0113 — Support multiple tenant memberships

The system stores separate tenant memberships for a person who belongs to multiple tenants.

### FR-IAM-0114 — Tenant selection

After authentication, the system automatically selects the only active tenant membership or prompts for selection when multiple active memberships exist.

### FR-IAM-0115 — Issue tenant-scoped claims

A tenant-scoped token contains exactly one tenant claim and only the roles and permissions valid for that tenant.

### FR-IAM-0116 — Reject invalid tenant context

Authentication or authorization fails safely when tenant context is missing, duplicated, malformed, or inconsistent.

### FR-IAM-0117 — Prevent tenant override

Headers, routes, query strings, and request bodies cannot override trusted tenant context.

### FR-IAM-0118 — Prevent unauthorized administration

Every administrative operation requires explicit permission authorization.

### FR-IAM-0119 — Concurrency protection

Updates to users, roles, and assignments detect stale concurrent changes.

### FR-IAM-0120 — Pagination and filtering

User and role listings use bounded pagination and approved filters.

### FR-IAM-0121 — Audit metadata

Created and modified records store UTC timestamps and authenticated actor identifiers.

### FR-IAM-0122 — No cross-tenant assignment

Users, roles, memberships, and assignments cannot be linked across tenant boundaries.

### FR-IAM-0123 — Platform-support tenant access

Platform-support operations require explicit platform-support permissions, target-tenant selection through trusted server-side workflow, and complete audit attribution.

### FR-IAM-0124 — Deactivation instead of deletion

No user deletion endpoint or physical-delete behavior is permitted.

## Security requirements

### SEC-IAM-0201

Passwords, reset tokens, refresh tokens, and secrets are never stored or logged in plaintext.

### SEC-IAM-0202

Authentication errors do not reveal account existence unless explicitly approved.

### SEC-IAM-0203

Raw JWTs and complete claims collections are not logged.

### SEC-IAM-0204

Unauthenticated requests return 401 and authenticated but unauthorized requests return 403.

### SEC-IAM-0205

All security-sensitive commands use trusted server-side tenant and actor context.

### SEC-IAM-0206

Role and permission identifiers use exact ordinal matching unless superseded by an approved ADR.

### SEC-IAM-0207

A platform-support identity does not acquire cross-tenant access from ordinary tenant roles.

## Non-functional requirements

### NFR-IAM-0301

All persistence and network operations are asynchronous and accept cancellation tokens.

### NFR-IAM-0302

Platform does not depend on HR or GL modules.

### NFR-IAM-0303

Domain and Application remain EF Core-free.

### NFR-IAM-0304

Build, architecture, unit, and integration tests pass with zero introduced warnings.

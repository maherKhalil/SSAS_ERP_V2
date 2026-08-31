---
document_id: FP-001-AC
title: Identity and Access Acceptance Criteria
status: Approved
version: 1.0
---

# Acceptance Criteria

### AC-IAM-0001 — Tenant-isolated listing

A tenant administrator sees only users from the current tenant.

### AC-IAM-0002 — Tenant administrator cannot cross tenants

A tenant administrator cannot access or manage another tenant even when supplying another tenant ID.

### AC-IAM-0003 — Platform support can access an authorized tenant

An App Owner / App Support actor with the required platform permission can perform an approved support action in a selected tenant, and the action is audited.

### AC-IAM-0004 — Platform support is not inferred

A tenant role, including a role named Administrator, does not grant platform-support access.

### AC-IAM-0005 — Per-tenant email uniqueness

The same email cannot be added twice in one tenant but may exist in another tenant.

### AC-IAM-0006 — Automatic tenant selection

When an identity has exactly one active tenant membership, selection is completed automatically and no selection UI is required.

### AC-IAM-0007 — Multiple-tenant selection

When an identity has multiple active memberships, a tenant must be selected before a tenant-scoped token is issued.

### AC-IAM-0008 — One tenant per token

Every tenant-scoped token contains exactly one valid tenant claim.

### AC-IAM-0009 — Unauthenticated request

Protected requests without valid authentication return 401.

### AC-IAM-0010 — Unauthorized request

Authenticated requests without required permission return 403.

### AC-IAM-0011 — Role assignment

An eligible same-tenant role can be assigned exactly once to an active tenant user.

### AC-IAM-0012 — Cross-tenant assignment rejected

A role from one tenant cannot be assigned to a user in another tenant.

### AC-IAM-0013 — Only catalog permissions are assignable, and only to an eligible role

Only code-catalog permissions can be assigned to an eligible role.

### AC-IAM-0014 — Multiple roles

A user may hold multiple roles and receives the distinct union of their permissions.

### AC-IAM-0015 — No permission by role name

A role grants no permission merely because of its name.

### AC-IAM-0016 — User deactivation

A deactivated user cannot obtain or refresh usable tenant access.

### AC-IAM-0017 — No user deletion

No API or domain operation physically deletes a user.

### AC-IAM-0018 — Role retirement blocked

A role assigned to any active user cannot be retired.

### AC-IAM-0019 — Retirement assignment blocked

A role pending retirement or retired cannot receive new user assignments.

### AC-IAM-0020 — Retirement preserves history

Retiring a role preserves assignments and audit history required for traceability.

### AC-IAM-0021 — Tenant suspension

A suspended tenant cannot receive normal application access or new tenant-scoped tokens.

### AC-IAM-0022 — Concurrency

A stale update is rejected without overwriting newer data.

### AC-IAM-0023 — Auditing

Every security-sensitive change records UTC timestamp and authenticated actor.

### AC-IAM-0024 — No secrets in logs

No password, raw token, refresh token, or full claims collection appears in logs.

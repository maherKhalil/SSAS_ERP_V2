---
document_id: FP-001-DEC
title: Approved Identity and Access Decisions
status: Approved
version: 1.0
approved_date: 2026-07-31
---

# Approved Decisions

## DEC-IAM-0001 — Identity ownership

Each tenant owns its users and tenant administrators.

Tenant users and tenant administrators cannot access or administer another tenant.

A separate platform-level actor, **App Owner / App Support**, may access and administer multiple tenants and may create or manage users across tenants when explicitly authorized through platform-support permissions.

The platform-support actor is not treated as a tenant administrator and must not gain cross-tenant access through ordinary tenant roles.

## DEC-IAM-0002 — Email uniqueness

Email is unique per tenant.

The same email address may exist in more than one tenant when the person has separate tenant memberships.

## DEC-IAM-0003 — Multi-tenant membership

One authenticated person may belong to multiple tenants through separate tenant memberships.

Each issued access token is scoped to exactly one selected tenant and contains exactly one tenant claim.

Tenant-scoped permissions and roles never cross tenant boundaries.

## DEC-IAM-0004 — Tenant selection

After authentication:

- when the user has one active tenant membership, the tenant is selected automatically and the tenant-selection screen is not shown;
- when the user has more than one active tenant membership, the user must select the tenant before a tenant-scoped token is issued;
- inactive or suspended memberships are excluded;
- platform-support access is handled through explicit platform-support authorization and is not inferred from tenant membership.

## DEC-IAM-0005 — Role types

The system supports:

- protected system roles;
- tenant-defined custom roles.

Protected system roles cannot be renamed, deleted, or modified in ways prohibited by their definition.

Custom roles are tenant-owned and may be created and maintained by authorized tenant administrators.

## DEC-IAM-0006 — Permission assignment target

Permissions are assigned to roles only.

Direct user-permission assignment is not supported in V1.

## DEC-IAM-0007 — User role count

A user may hold multiple roles in the same tenant.

Effective permissions are the distinct union of permissions assigned to the user's active roles in that tenant.

## DEC-IAM-0008 — Role name uniqueness and normalization

Role names are unique per tenant.

Display casing is preserved.

Authorization uses stable role identifiers or exact claim values. No undocumented case-insensitive authorization matching is introduced.

## DEC-IAM-0009 — Permission catalog storage

Permissions are defined in a code-owned application catalog for V1.

Tenants cannot create arbitrary permission names.

## DEC-IAM-0010 — First tenant administrator

Tenant provisioning creates the first tenant membership and assigns the protected tenant-administrator role in one transaction.

## DEC-IAM-0011 — User onboarding

An authorized administrator invites a user.

The user completes account setup securely.

Administrators do not create or communicate user passwords.

## DEC-IAM-0012 — Disabled-user token behavior

Use short-lived access tokens plus refresh-token revocation or a security-version mechanism.

A disabled user cannot obtain or refresh usable tenant access.

Sensitive server-side operations may perform an additional current-status check where required.

## DEC-IAM-0013 — Tenant suspension

When a tenant is suspended:

- normal tenant application access is denied;
- new tenant-scoped tokens are not issued;
- only explicitly approved platform billing, support, or recovery operations remain available.

## DEC-IAM-0014 — Role retirement

A role cannot be retired while it is assigned to any active user.

Before retirement, active users must be deactivated or the role must be removed/replaced according to approved administrative action.

A role pending retirement:

- cannot receive new user assignments;
- retains audit history;
- cannot be physically deleted;
- stops granting permissions only after retirement succeeds.

## DEC-IAM-0015 — Authentication package boundary

Authentication and token lifecycle are defined in a separate feature package.

FP-001 covers tenant identity administration, role and permission administration, tenant memberships, authorization data, and lifecycle state.

## DEC-IAM-0016 — Password and MFA policy

Password policy, lockout, MFA, reset-token lifetime, and account recovery are deferred to the authentication feature package.

## DEC-IAM-0017 — Audit retention

FP-001 stores standard audit metadata for all identity and access changes.

An immutable security-audit history feature must be specified before production release.

## DEC-IAM-0018 — User deletion and privacy

Physical user deletion is not allowed.

A user can only be deactivated.

Historical identity, assignment, and audit references are preserved.

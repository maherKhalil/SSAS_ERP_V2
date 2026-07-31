---
document_id: FP-001-AUTH
title: Authorization Model
status: Approved
version: 1.0
---

# Authorization Model

## Tenant authorization

Tenant users and tenant administrators are scoped to one tenant per token.

Authorization succeeds only when:

1. the identity is authenticated;
2. exactly one valid tenant claim exists;
3. the claim matches trusted current tenant context;
4. the required role or permission claim exists exactly;
5. the tenant membership is active;
6. the tenant is active.

## Effective permissions

A tenant user's effective permissions are the distinct union of permissions assigned to all active roles assigned to that user's active membership in the selected tenant.

Direct user-permission assignments are not supported.

A role name alone grants nothing.

## Tenant selection

After primary authentication:

- one active tenant membership: tenant is selected automatically;
- multiple active tenant memberships: user selects one;
- no active memberships: tenant access is denied;
- token issuance occurs only after tenant selection;
- the resulting token contains exactly one tenant claim.

## Platform-support authorization

App Owner / App Support uses a separate platform authorization plane.

Requirements:

- explicit platform-support permission;
- trusted target-tenant selection;
- no cross-tenant access derived from tenant roles;
- support action auditing;
- support access limited to approved operations;
- target tenant cannot be overridden through arbitrary request input.

## Role model

- protected system roles;
- tenant-defined custom roles;
- multiple roles per user;
- role names unique per tenant;
- exact claim matching;
- roles cannot be retired while assigned to active users.

## Permission model

Permission names follow:

```text
<module>.<resource>.<action>
```

Permissions are defined in code and deployed with the application.

## Token staleness

Authentication package must implement short-lived access tokens and refresh-token revocation or security-version checks so deactivation, tenant suspension, and role/permission changes take effect within approved limits.

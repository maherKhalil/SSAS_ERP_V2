---
document_id: FP-002-API
title: Authentication API Contracts
status: Approved for Implementation
version: 1.0
---

# API Contracts

Approved V1 routes:

```http
POST /api/platform/auth/login
POST /api/platform/auth/select-tenant
POST /api/platform/auth/refresh
POST /api/platform/auth/logout
POST /api/platform/auth/password-reset/request
POST /api/platform/auth/password-reset/complete
POST /api/platform/auth/invitations/complete
POST /api/platform/auth/password/change
GET  /api/platform/auth/sessions
DELETE /api/platform/auth/sessions/{sessionId}
POST /api/platform/auth/sessions/revoke-all
```

These are package-level V1 contracts. None of these routes is implemented in Sprint-01 Milestone 2.

Login returns either:

- `Authenticated` with tenant-scoped token data when one eligible tenant exists; or
- `TenantSelectionRequired` with a short-lived selection transaction and eligible tenant summaries.

The selection endpoint validates membership before token issuance.

Reset requests always return a generic accepted response. Invalid refresh returns 401. Throttled requests return 429. Errors use Problem Details with correlation ID and no secret leakage.

Excluded: social login, external OIDC, passwordless login, mandatory tenant-user MFA, support impersonation, and HR/GL APIs.

## Sprint-01 Milestone 2 delivery boundary

Milestone 2 exposes no public authentication or action-token endpoint and performs no email delivery.

Internal invitation/reset issuing commands may return a raw token exactly once through a sensitive result that is structurally distinct from an ordinary API DTO. That result must never be serialized by an HTTP endpoint or appear in logs, telemetry, exceptions, audit records, or domain events.

Invitation completion behavior is defined before the route is implemented:

- a new or pending-setup account supplies an initial password;
- an existing verified active account supplies no password and only activates the intended pending membership;
- active memberships cannot be invited;
- deactivated memberships use the existing reactivation workflow;
- invitations contain no role identifiers and assign no roles.

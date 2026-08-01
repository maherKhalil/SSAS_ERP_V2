---
document_id: FP-002-API
title: Authentication API Contracts
status: Approved for Implementation
version: 1.0
---

# API Contracts

Approved Milestone 4 V1 routes:

```http
POST /api/platform/auth/login
POST /api/platform/auth/select-tenant
POST /api/platform/auth/refresh
POST /api/platform/auth/logout
```

These exact `/api/platform/auth/*` routes supersede every former Draft `/api/auth/*` route. `ClientId` is not present in any request and is always bound by the server to exact `ssas-erp-web`.

## Requests

Login accepts JSON only:

```json
{
  "loginEmail": "user@example.com",
  "password": "example only"
}
```

Tenant selection accepts JSON only:

```json
{
  "selectionProof": "reveal-once-value",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "tenantUserId": 123
}
```

Refresh and logout accept no JSON request body. Refresh obtains its credential only from the approved refresh cookie. Logout derives session identity only from the validated Bearer access token and accepts no SessionId.

## Responses

Login returns either:

- `Authenticated` with an access token and expiry metadata when one eligible Active Tenant exists; or
- `TenantSelectionRequired` with a reveal-once `selectionProof`, its expiry, and eligible summaries containing exactly `tenantId`, `tenantUserId`, and `tenantDisplayName`.

Authenticated login, tenant selection, and refresh return 200. Tenant-selection-required also returns 200. A refresh token is never returned in JSON. Logout clears the refresh and CSRF cookies and returns 204 with no response body.

Every authentication response contains:

```http
Cache-Control: no-store
Pragma: no-cache
Referrer-Policy: no-referrer
X-Content-Type-Options: nosniff
```

## Problem Details

| Condition | Status | Code |
|---|---:|---|
| Malformed request | 400 | `request.invalid` |
| Credential, no-membership, locked, or disabled failure | 401 | `authentication.failed` |
| Selection failure | 401 | `authentication.selection_failed` |
| Refresh failure | 401 | `authentication.refresh_failed` |
| CSRF or Origin rejection | 403 | `authentication.request_rejected` |
| Rate limit exceeded | 429 | `rate_limit.exceeded` |
| Signing or persistence temporarily unavailable | 503 | `service.unavailable` |

`NoEligibleMembership` maps to the generic authentication 401. Responses expose correlation metadata but never an internal failure cause.

## Browser security

Refresh uses host-only Secure, HttpOnly, SameSite Strict cookie `__Secure-ssas-refresh` at Path `/api/platform/auth`; refresh and logout also require JavaScript-readable signed CSRF cookie `__Secure-ssas-xsrf` and exact `X-XSRF-TOKEN` header. Login and selection require JSON-only content. All four routes require an exact approved Origin, restrictive credentialed CORS, and their endpoint-specific rate limiter.

## OpenAPI

OpenAPI exposes all four routes, HTTP Bearer JWT security, the anonymous ASP.NET authentication classification for login, selection, and refresh, Bearer authentication for logout, refresh-cookie behavior through descriptions, `X-XSRF-TOKEN` for refresh/logout, both success schemas, and every approved Problem Details response. It contains no refresh value, CSRF secret, internal command or sensitive wrapper, realistic token, or private signing information.

## Deferred HTTP surface

Password-reset and invitation delivery, authenticated password change, active-session listing, revoke-another-session, and logout-all routes are not Milestone 4 contracts. Angular authentication, MFA, external providers, native clients, Platform-support authentication, and support impersonation also remain deferred.

## Milestone 4 delivery boundary

Milestone 4 implements the four routes above, RS256 access-token issuance and validation, the refresh/CSRF cookie transport, Origin/CORS/proxy/rate-limit controls, live Tenant eligibility authorization, current-session logout, security headers, generic Problem Details, and OpenAPI. It introduces no additional public authentication route.

## Sprint-01 Milestone 2 delivery boundary

Milestone 2 exposes no public authentication or action-token endpoint and performs no email delivery.

Internal invitation/reset issuing commands may return a raw token exactly once through a sensitive result that is structurally distinct from an ordinary API DTO. That result must never be serialized by an HTTP endpoint or appear in logs, telemetry, exceptions, audit records, or domain events.

Invitation completion behavior is defined before the route is implemented:

- a new or pending-setup account supplies an initial password;
- an existing verified active account supplies no password and only activates the intended pending membership;
- active memberships cannot be invited;
- deactivated memberships use the existing reactivation workflow;
- invitations contain no role identifiers and assign no roles.

## Sprint-01 Milestone 3 delivery boundary

Milestone 3 remains an internal Domain, Application, and SQL Server slice. It exposes no login, tenant-selection, refresh, logout, session, password-reset, or invitation HTTP endpoint and introduces no controller, endpoint mapper, cookie, CSRF behavior, JWT response, access-token response, claims construction, or ASP.NET Core authentication change.

Successful credential verification returns only an internal non-serializable `VerifiedIdentity`. Tenant-selection proofs and refresh tokens are reveal-once sensitive internal results structurally separated from ordinary DTOs; they are not HTTP response contracts in this milestone.

The exact V1 ClientId `ssas-erp-web` is an internal validated binding value. Its documentation here does not authorize a caller-controlled transport field or native-client identifier.

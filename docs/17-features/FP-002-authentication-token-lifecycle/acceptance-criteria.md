---
document_id: FP-002-AC
title: Authentication Acceptance Criteria
status: Approved for Implementation
version: 1.0
---

# Acceptance Criteria

- **AC-AUTH-0001:** Login failures do not disclose the exact reason.
- **AC-AUTH-0002:** One active membership is selected automatically.
- **AC-AUTH-0003:** Multiple memberships require a short-lived selection transaction.
- **AC-AUTH-0004:** A tenant without active membership cannot be selected.
- **AC-AUTH-0005:** Every tenant access token has exactly one tenant claim.
- **AC-AUTH-0006:** Roles and permissions belong only to the selected tenant.
- **AC-AUTH-0007:** Successful refresh invalidates the submitted refresh token.
- **AC-AUTH-0008:** Reuse revokes the approved scope and requires reauthentication.
- **AC-AUTH-0009:** No plaintext refresh, reset, or invitation token is stored.
- **AC-AUTH-0010:** Current-session logout does not revoke unrelated sessions.
- **AC-AUTH-0011:** Logout-all revokes every active session for the identity.
- **AC-AUTH-0012:** Password change advances security state and applies the approved revocation policy.
- **AC-AUTH-0013:** Reset request prevents account enumeration.
- **AC-AUTH-0014:** Reset tokens are single-use and expire.
- **AC-AUTH-0015:** Invitation tokens are single-use and membership-bound.
- **AC-AUTH-0016:** Failed attempts trigger approved throttling/lockout.
- **AC-AUTH-0017:** Disabled identity or membership cannot login or refresh.
- **AC-AUTH-0018:** Suspended tenants cannot receive normal tenant tokens.
- **AC-AUTH-0019:** Invalid JWT signature, issuer, audience, expiry, or claims is rejected.
- **AC-AUTH-0020:** Secrets never appear in logs or telemetry.
- **AC-AUTH-0021:** Concurrent refresh permits at most one successful rotation.
- **AC-AUTH-0022:** Signing-key overlap supports controlled rotation.
- **AC-AUTH-0023:** Tenant resolution, selection, session creation, and refresh use FP-003 eligibility; only an existing Active Tenant is eligible.
- **AC-AUTH-0024:** Successful credential verification yields only a non-user-constructible VerifiedIdentity capability, and session creation revalidates its SecurityVersion.
- **AC-AUTH-0025:** Only exact allowlisted `ssas-erp-web` is accepted for the V1 browser; whitespace, casing differences, and arbitrary ClientId values are rejected.
- **AC-AUTH-0026:** A session is immutably bound to one Identity, membership, Tenant, ClientId, and token family and persists only Active, Revoked, or Compromised status.
- **AC-AUTH-0027:** Pre-tenant discovery returns only active memberships owned by the verified Identity whose Tenants are eligible, without exposing aggregates or another Identity's memberships.
- **AC-AUTH-0028:** A tenant-selection proof is persisted only as selector plus exact 32-byte hash, uses the canonical 76-character format, lasts five minutes, is single-use, and is consumed only with successful session creation.
- **AC-AUTH-0029:** A refresh token uses the canonical 76-character selector/secret format, persists no raw secret, and is exactly bound to its session, family, and ClientId.
- **AC-AUTH-0030:** Refresh atomically consumes one token, links exactly one replacement, updates idle expiration without extending absolute expiration, and rolls back all changes on failed persistence.
- **AC-AUTH-0031:** Verified reuse compromises only the owning session, revokes every unconsumed descendant, remains detectable from retained ancestors, and permits no grace window or second concurrent success.
- **AC-AUTH-0032:** Session creation never leaves more than ten active unexpired sessions for an Identity and revokes the oldest by CreatedUtc then AuthenticationSessionId using `SessionLimitExceeded`.
- **AC-AUTH-0033:** Successful password reset atomically advances SecurityVersion, consumes the reset token, changes the password, clears lockout, and revokes every active session using `PasswordReset`.
- **AC-AUTH-0034:** Approved locked operations serialize concurrent selection, refresh, revocation, session-limit, password-reset, membership, Tenant, and SecurityVersion races without leaking SQL details.
- **AC-AUTH-0035:** Raw refresh tokens and tenant-selection proofs are reveal-once sensitive results and never enter persistence, ordinary DTOs, command representations, logs, telemetry, exceptions, or events.

- **AC-AUTH-0036:** Exactly the four approved `/api/platform/auth/*` routes accept only their approved inputs, bind `ssas-erp-web` server-side, and return the exact status and Problem Details mappings without cause disclosure.
- **AC-AUTH-0037:** Every issued access token has the exact required claims and formats, distinct ordinally sorted roles/permissions, none of the prohibited data, a 15-minute default lifetime, and compact size no greater than 8192 bytes.
- **AC-AUTH-0038:** Production signs only with a deployment-mounted X.509 RSA private key of at least 2048 bits through an abstract provider; development and tests use only their approved non-production key sources.
- **AC-AUTH-0039:** `kid` is derived from certificate DER bytes, one active signing key is selected from an immutable snapshot, invalid key identifiers fail closed, and rollover retains old verification for at least lifetime plus 30-second skew.
- **AC-AUTH-0040:** Strict JWT validation accepts only RS256 with known enabled `kid` and valid exact issuer, audience, signature, lifetime, `nbf`, cardinality, and formats; no symmetric path remains active.
- **AC-AUTH-0041:** The exact refresh cookie is Secure, HttpOnly, SameSite Strict, host-only, scoped to `/api/platform/auth`, expiry-aligned, rotated on refresh, and cleared with identical attributes on logout or terminal refresh failure.
- **AC-AUTH-0042:** Refresh and logout require the exact Data-Protection-signed CSRF cookie/header pair bound to current session, refresh selector, and ClientId; state rotates and clears with refresh state and production key-ring startup fails closed.
- **AC-AUTH-0043:** Every endpoint enforces its exact zero-queue limits, returns generic 429 with Retry-After, protects partition inputs with deployment HMAC, and requires declared shared enforcement in Production.
- **AC-AUTH-0044:** Exact HTTPS Origin, restrictive credentialed CORS, and approved direct/trusted-proxy client-IP rules are enforced, with invalid Production origin or proxy configuration rejected at startup.
- **AC-AUTH-0045:** Every ordinary tenant-scoped authenticated business request authorizes only after one live FP-003 lookup confirms Active status, while suspended-tenant logout remains possible and TenantStatus is absent from JWTs.
- **AC-AUTH-0046:** Session creation and refresh commit only after access-token issuance succeeds; issuance failure rolls back SQL and dispatches no event, and cookies are written only after commit.
- **AC-AUTH-0047:** Logout derives the current session only from validated claims, verifies all approved bindings, revokes only that session with `UserLogout`, is outwardly idempotent, clears both cookies, returns 204, and provides no logout-all behavior.
- **AC-AUTH-0048:** Tenant-selection-required returns only the reveal-once proof and eligible summaries containing TenantId, TenantUserId, and TenantDisplayName from FP-003 TenantName, with every prohibited field absent.
- **AC-AUTH-0049:** Every authentication response has the four approved security headers and no sensitive body, Authorization header, cookie, proof, token, command, or model-state value enters logs, exceptions, compression, or examples.
- **AC-AUTH-0050:** OpenAPI exposes all four routes with exact Bearer, anonymous, cookie, CSRF, schema, status, and Problem Details documentation and no sensitive or private example/value.
- **AC-AUTH-0051:** Milestone 4 adds only the UserLogout reason constraint migration, creates no transport/key/rate-limit table, and treats post-commit cookie failure as non-atomic ambiguity without automatic refresh retry or grace window.

## Sprint-01 Milestone 2 applicability

Milestone 2 directly covers the credential and account-action portions of `AC-AUTH-0001`, `AC-AUTH-0009`, `AC-AUTH-0013`, `AC-AUTH-0014`, `AC-AUTH-0015`, `AC-AUTH-0016`, `AC-AUTH-0017`, and `AC-AUTH-0020`.

`AC-AUTH-0002` through `AC-AUTH-0008`, `AC-AUTH-0010` through `AC-AUTH-0012`, `AC-AUTH-0018`, `AC-AUTH-0019`, `AC-AUTH-0021`, and `AC-AUTH-0022` remain package acceptance criteria for later FP-002 milestones.

## Sprint-01 Milestone 3 applicability

Milestone 3 directly covers `AC-AUTH-0002` through `AC-AUTH-0004`, `AC-AUTH-0007` through `AC-AUTH-0009`, `AC-AUTH-0017`, `AC-AUTH-0018`, `AC-AUTH-0020`, `AC-AUTH-0021`, and `AC-AUTH-0023` through `AC-AUTH-0035` at the internal Domain, Application, and SQL Server boundary.

## Sprint-01 Milestone 4 applicability

Milestone 4 directly covers `AC-AUTH-0005` through `AC-AUTH-0010`, `AC-AUTH-0017` through `AC-AUTH-0023`, `AC-AUTH-0025`, `AC-AUTH-0027` through `AC-AUTH-0031`, `AC-AUTH-0034`, `AC-AUTH-0035`, and `AC-AUTH-0036` through `AC-AUTH-0051` at the Application, Host/API, authorization, SQL Server, and browser-transport boundary.

Authenticated password change, password-reset and invitation HTTP delivery, session listing, revoke-another-session, logout-all, Angular work, immutable audit storage, Platform-support authentication, MFA, external identity providers, native clients, notification delivery, and high-risk business policies beyond the reusable live-Tenant foundation remain assigned to later milestones.

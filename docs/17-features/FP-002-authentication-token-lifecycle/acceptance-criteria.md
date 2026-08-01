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

## Sprint-01 Milestone 2 applicability

Milestone 2 directly covers the credential and account-action portions of `AC-AUTH-0001`, `AC-AUTH-0009`, `AC-AUTH-0013`, `AC-AUTH-0014`, `AC-AUTH-0015`, `AC-AUTH-0016`, `AC-AUTH-0017`, and `AC-AUTH-0020`.

`AC-AUTH-0002` through `AC-AUTH-0008`, `AC-AUTH-0010` through `AC-AUTH-0012`, `AC-AUTH-0018`, `AC-AUTH-0019`, `AC-AUTH-0021`, and `AC-AUTH-0022` remain package acceptance criteria for later FP-002 milestones.

## Sprint-01 Milestone 3 applicability

Milestone 3 directly covers `AC-AUTH-0002` through `AC-AUTH-0004`, `AC-AUTH-0007` through `AC-AUTH-0009`, `AC-AUTH-0017`, `AC-AUTH-0018`, `AC-AUTH-0020`, `AC-AUTH-0021`, and `AC-AUTH-0023` through `AC-AUTH-0035` at the internal Domain, Application, and SQL Server boundary.

Access-token/JWT criteria, HTTP logout and session administration, authenticated password change, signing-key overlap, cookies/CSRF, and other transport criteria remain assigned to later milestones.

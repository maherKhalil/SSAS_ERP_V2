---
document_id: FP-002-BR
title: Authentication Business Rules
status: Approved for Implementation
version: 1.0
---

# Business Rules

- **BRULE-AUTH-0001:** Credential verification identifies a global Identity; tenant access starts only after membership resolution.
- **BRULE-AUTH-0002:** A tenant access token represents one Identity, one TenantUser, one tenant, and one session.
- **BRULE-AUTH-0003:** A pre-tenant transaction cannot access tenant business APIs.
- **BRULE-AUTH-0004:** Token issuance and refresh require active identity, membership, tenant, and session.
- **BRULE-AUTH-0005:** Roles and permissions in a token belong only to the selected tenant.
- **BRULE-AUTH-0006:** A refresh token succeeds at most once.
- **BRULE-AUTH-0007:** Reuse is treated as possible compromise.
- **BRULE-AUTH-0008:** Plain refresh, reset, and invitation tokens are never stored.
- **BRULE-AUTH-0009:** Revoking one session leaves unrelated sessions active unless an explicit wider action applies.
- **BRULE-AUTH-0010:** Password reset and approved password changes advance security state.
- **BRULE-AUTH-0011:** Invalid identifier, password, disabled state, and unavailable membership use indistinguishable public errors.
- **BRULE-AUTH-0012:** Invitation, reset, refresh, and tenant-selection tokens are purpose-bound.
- **BRULE-AUTH-0013:** An invitation activates only its intended identity and membership.
- **BRULE-AUTH-0014:** Administrators never create, view, or communicate passwords.
- **BRULE-AUTH-0015:** Private signing keys and production secrets never enter source control.

## Sprint-01 Milestone 2 interpretation

- A new password-based Identity receives a server-generated exact `local:{guid}` subject; login email is never the subject.
- Global `LoginEmail` and tenant-specific `TenantUser.Email` remain independent.
- Invitations create or target only a pending membership and never stage or assign roles.
- Invitation completion for an existing verified active account activates only the intended pending membership and does not change password or security version.
- `PendingSetup`, `Active`, and `Disabled` are the only authentication-account statuses. Temporary lockout is represented by failed-attempt state and `LockoutEndUtc`.
- Action-token lookup uses a public selector; the raw secret is verified against the exact purpose using a fixed-time hash comparison.
- Failed-login concurrency retries are bounded to three attempts and can never produce authentication success.
- Raw invitation and reset tokens may leave the issuing command only once through an explicitly sensitive internal result. They are not public API DTOs.

## Sprint-01 Milestone 3 interpretation

- Successful credential verification yields an internal `VerifiedIdentity` containing only IdentityId and SecurityVersion; callers cannot begin tenant access with a raw IdentityId.
- Eligible membership discovery always predicates on the verified Identity, includes only active memberships, and requires current FP-003 Tenant status `Active`.
- The exact V1 ClientId is `ssas-erp-web`; comparison is ordinal and case-sensitive, whitespace is invalid, and the value is immutable throughout selection, session, and refresh history.
- A tenant-selection transaction is persisted for five minutes, single-use, ClientId-bound, and consumed only with successful session creation.
- AuthenticationSession status is exactly `Active`, `Revoked`, or `Compromised`. Revoked and Compromised are terminal; idle and absolute expiration are computed separately.
- Approved revocation reasons are `SessionLimitExceeded`, `PasswordReset`, `SecurityStateChanged`, `IdentityIneligible`, `MembershipIneligible`, `TenantIneligible`, and `Administrative`.
- AuthenticationSession owns its refresh-token children, replacement chain, reuse detection, descendant revocation, refresh timestamps, and compromise transition.
- Selection proofs and refresh tokens use exact 76-character canonical selector/secret formats; only fixed 32-byte hashes are persisted and raw values cross the Application boundary once through sensitive results.
- Refresh-token expiry is `min(Session.IdleExpiresUtc, Session.AbsoluteExpiresUtc)`; refresh updates idle expiry but never absolute expiry.
- A maximum of ten active unexpired sessions per Identity is enforced transactionally by locking the AuthenticationAccount and revoking deterministic oldest sessions before new-session creation.
- Successful password reset revokes every active session for the Identity in the same transaction that advances SecurityVersion, consumes the reset token, replaces the hash, and clears lockout.
- Locked persistence operations follow AuthenticationAccount, selection transaction, membership, Tenant, session, then refresh-token lock order. Ambiguous refresh commits are not retried.

## Sprint-01 Milestone 4 interpretation

- The public surface is exactly login, tenant selection, refresh, and current-session logout under `/api/platform/auth`; ClientId is never accepted from the caller and is exact `ssas-erp-web`.
- Access tokens default to 15 minutes, use only RS256, contain the exact approved singleton and repeated claims, reject duplicate or oversized claims, and contain no identity display, tenant display/status, company, billing, subscription, password, or complete security-state data.
- Permission claims are the primary business-authorization mechanism; role claims support exact-role policies. Both use exact distinct ordinal values derived from current trusted tenant state at issuance.
- Production signing keys and Data Protection state are deployment-owned. Missing or invalid required production cryptographic configuration prevents startup; no symmetric or insecure local fallback is allowed.
- The refresh value remains only in the exact Secure, HttpOnly, SameSite Strict host cookie. Refresh and logout require exact signed session-and-refresh-bound CSRF state, and both cookies rotate or clear together as approved.
- Login, selection, refresh, and logout validate exact Origin, restrictive CORS, trusted client-IP provenance, and their exact zero-queue rate limits. Public failures remain generic.
- Every ordinary tenant-scoped authenticated business request validates live FP-003 status and permits only Active; logout is separately authorized so current suspended-tenant sessions can still be revoked.
- Access-token issuance succeeds before commit, events dispatch after commit, and cookies write after commit. Post-commit cookie failure is transport ambiguity and never creates automatic retry or a refresh grace window.
- Current-session logout trusts only validated claims, revokes only that session with `UserLogout`, is outwardly idempotent, clears refresh and CSRF cookies, and never acts as logout-all.
- Tenant-selection summaries use only TenantId, TenantUserId, and FP-003 TenantName as TenantDisplayName for an eligible membership owned by the verified Identity.
- Authentication responses are non-cacheable, non-referring, nosniff, uncompressed, and excluded from sensitive body/header/cookie/token/proof logging and realistic OpenAPI examples.
- Milestone 4 adds no persistence table. Its only schema change is the `AddUserLogoutSessionRevocationReason` constraint migration.

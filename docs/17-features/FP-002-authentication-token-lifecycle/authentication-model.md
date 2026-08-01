---
document_id: FP-002-AUTH
title: Authentication and Token Model
status: Approved for Implementation
version: 1.0
---

# Authentication Model

## Flow

```text
Verify global credentials
→ return internal VerifiedIdentity
→ discover active memberships owned by that Identity
→ validate each Tenant through FP-003
→ auto-select one or persist selection proof for many
→ revalidate account, membership, Tenant, SecurityVersion, and ClientId
→ atomically create session and first refresh token
→ later milestone issues one-tenant access token
```

Milestone 3 ends after the internal session and reveal-once refresh-token result. It does not issue an access token or expose this flow through HTTP.

## Access-token claims

Subject, IdentityId, TenantUserId, TenantId, SessionId, security version, roles, permissions, JTI, issuer, audience, issued-at, and expiration. Email/display name are included only when necessary.

## Refresh tokens

Refresh tokens use exact `<public-selector>.<secret>` form: a 32-character cryptographically random Guid selector in `N` format, one separator, and a 43-character canonical Base64Url encoding of exactly 32 random secret bytes. The total length is exactly 76 characters. Invalid structure is rejected before indexed selector lookup.

Only the SHA-256 `BINARY(32)` hash of the exact domain-separated canonical input in `DEC-AUTH-0040` is stored. Verification validates exact ClientId, session and family binding, lifecycle and expiry, and uses fixed-time hash comparison.

Tokens are session/client-bound, rotated after every successful use, and governed by idle plus absolute expiry. Each token expires at `min(Session.IdleExpiresUtc, Session.AbsoluteExpiresUtc)`. Refresh sets the new idle expiry to `min(now + configured idle lifetime, AbsoluteExpiresUtc)` and never extends absolute expiry.

Reuse of any retained consumed ancestor issues nothing, marks only its owning session Compromised, revokes unconsumed descendants, emits safe events, and requires reauthentication for that session. There is no grace window, and at most one concurrent rotation succeeds.

Raw refresh tokens are reveal-once sensitive results and are absent from persistence, ordinary DTOs, command representations, diagnostics, events, and observability.

## Verified identity and tenant eligibility

Successful credential verification returns an internal `VerifiedIdentity` capability containing only IdentityId and SecurityVersion. It cannot be created by an ordinary caller, persisted, or serialized. `BeginTenantAccessCommand` accepts this capability rather than raw IdentityId.

Membership status alone is insufficient. Automatic resolution, explicit selection, session creation, and refresh require `ITenantAuthenticationEligibilityReadService`; only an existing FP-003 Tenant in `Active` status is eligible.

## Tenant selection proof

When multiple eligible memberships exist, the server persists a five-minute, single-use TenantSelectionTransaction. Its proof uses exact `<public-selector>.<secret>` form: a 32-character cryptographically random Guid selector in `N` format, one separator, and a 43-character canonical Base64Url encoding of exactly 32 random secret bytes. Total length is exactly 76 characters, and malformed input is rejected before lookup.

Only the SHA-256 `BINARY(32)` hash of the exact domain-separated canonical input in `DEC-AUTH-0039` is stored. The transaction binds IdentityId, SecurityVersion, and exact ClientId. Failed membership or Tenant validation does not consume it; successful selection consumes it atomically with session creation.

## V1 ClientId

The only approved V1 browser ClientId is exact `ssas-erp-web`. Comparison is ordinal and case-sensitive, maximum length is 64, whitespace is rejected rather than trimmed, storage uses binary collation, and deployment allowlisting is mandatory. Selection transactions, sessions, and refresh records preserve the immutable exact value.

## Session lifecycle

Persisted statuses are exactly `Active`, `Revoked`, and `Compromised`. Active may transition to either terminal state. Expiration is computed: an Active session is usable only before both IdleExpiresUtc and AbsoluteExpiresUtc.

Default idle and absolute lifetimes are 30 and 90 days. Approved revocation reasons are `SessionLimitExceeded`, `PasswordReset`, `SecurityStateChanged`, `IdentityIneligible`, `MembershipIneligible`, `TenantIneligible`, and `Administrative`.

At most ten active unexpired sessions exist per Identity. Under one SQL Server transaction, the AuthenticationAccount is locked, sessions are ordered by CreatedUtc then AuthenticationSessionId, enough oldest sessions are revoked, and the new session and first token are created.

Locked workflows use AuthenticationAccount, selection transaction, membership, Tenant, session, then refresh-token order. Eligibility and SecurityVersion are revalidated under locks, and one captured trusted UTC value drives expiration decisions.

## Password handling

Use ASP.NET Core `PasswordHasher`, including versioned hashes and rehash after successful verification when needed. Do not use low-level PBKDF2 APIs directly for ordinary application password storage.

The Application hashing abstraction preserves failed, successful, and successful-but-rehash-required outcomes. A representation-only rehash does not change `PasswordChangedUtc` or increment the security version.

Password setup and reset use the deployment-provided versioned offline compromised/common-password dataset through `ICompromisedPasswordChecker`. No network call is required for that check.

## Approved password baseline

Minimum 12 characters, support at least 64, permit spaces/Unicode, no character-class composition rule, no routine periodic expiry, and block common or compromised passwords.

## Approved Angular storage

Keep access tokens in memory. Prefer a Secure, HttpOnly, SameSite refresh cookie with CSRF protection. Do not place refresh tokens in localStorage.

## Key management

Use external asymmetric signing keys, `kid`, and overlap during controlled rotation.

## Global password account

`AuthenticationAccount` is global and one-to-one with the FP-001 `Identity`. Its login email is independent from every tenant-specific `TenantUser.Email`.

For a new password account, the server creates an immutable exact subject using `local:{guid}` with a cryptographically random GUID in `N` format. The caller cannot supply it, and a login-email change never changes it.

Display login email is trimmed and preserves casing. Its globally unique normalized value is `Trim().ToUpperInvariant()`; no provider-specific aliases or dot rules are applied.

Approved account statuses are `PendingSetup`, `Active`, and `Disabled`. Lockout is temporary state represented by failed-attempt count and lockout end, not a status.

## Invitation and action-token core

Invitations create or target a pending membership and do not stage roles. A new or pending-setup account completes password setup and becomes active. An existing verified active account activates only the intended pending membership without requesting or changing a password or security version.

Action tokens use `<public-selector>.<secret>`. The selector is a cryptographically random GUID. The secret contains 32 random bytes and is returned once as Base64Url. Persistence contains only a purpose-bound SHA-256 hash, and verification uses fixed-time comparison.

## Milestone 2 boundary

Milestone 2 stops after credential verification and account-action processing. It issues no JWT, refresh token, session, or tenant-selection transaction and exposes no authentication HTTP endpoint.

## Milestone 3 boundary

Milestone 3 delivers the internal tenant-resolution, tenant-selection, AuthenticationSession, refresh-token, session-limit, password-reset revocation, and SQL Server concurrency core. Access-token/JWT issuance, signing keys, claims construction, endpoints, cookies, CSRF, Angular, public logout/session administration, authenticated password change, and Platform-support authentication remain deferred.

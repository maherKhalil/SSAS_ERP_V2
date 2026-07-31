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
→ resolve active memberships
→ auto-select one or require selection among many
→ validate membership
→ create session
→ issue one-tenant access token
→ issue refresh token
```

## Access-token claims

Subject, IdentityId, TenantUserId, TenantId, SessionId, security version, roles, permissions, JTI, issuer, audience, issued-at, and expiration. Email/display name are included only when necessary.

## Refresh tokens

Opaque, random, session/client-bound, stored only by hash, rotated after every use, and governed by idle plus absolute expiry.

Reuse of a consumed token issues nothing, marks the approved scope compromised, revokes descendants, emits an audit event, and requires reauthentication.

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

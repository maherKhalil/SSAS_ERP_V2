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

## Approved password baseline

Minimum 12 characters, support at least 64, permit spaces/Unicode, no character-class composition rule, no routine periodic expiry, and block common or compromised passwords.

## Approved Angular storage

Keep access tokens in memory. Prefer a Secure, HttpOnly, SameSite refresh cookie with CSRF protection. Do not place refresh tokens in localStorage.

## Key management

Use external asymmetric signing keys, `kid`, and overlap during controlled rotation.

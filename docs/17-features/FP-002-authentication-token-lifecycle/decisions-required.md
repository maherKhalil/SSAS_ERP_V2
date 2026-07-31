---
document_id: FP-002-DEC
title: Authentication Decisions Required
status: Draft
version: 0.1
---

# Decisions Required

## DEC-AUTH-0001 — Global login identifier — Blocking

FP-001 allows tenant-specific email and requires authentication before tenant selection. Choose a globally unambiguous login identifier.

Recommended: a verified globally unique login email on `AuthenticationAccount`, separate from `TenantUser.Email`.

Clarify whether the same email in different tenants always represents the same global person.

## DEC-AUTH-0002 — Credential ownership — Blocking

Recommended: separate `AuthenticationAccount` aggregate linked one-to-one with Identity.

## DEC-AUTH-0003 — Invitation and verification — Blocking

Recommended: administrator sends invitation; user verifies ownership and sets the password; administrator never creates the password.

## DEC-AUTH-0004 — Password policy — Blocking

Recommended: minimum 12, support 64+, spaces/Unicode allowed, no composition rule, no periodic expiry, block common/compromised passwords.

## DEC-AUTH-0005 — Password hashing — Blocking

Recommended: ASP.NET Core `PasswordHasher`, versioned hashes, rehash when required.

## DEC-AUTH-0006 — Failed-login policy — Blocking

Recommended initial policy: account/network rate limiting, temporary lockout after 5 failures for 15 minutes, generic public errors.

## DEC-AUTH-0007 — Access-token lifetime — Blocking

Recommended: 15 minutes, configurable.

## DEC-AUTH-0008 — Refresh/session lifetime — Blocking

Recommended: 30-day idle and 90-day absolute session lifetime, configurable.

## DEC-AUTH-0009 — Refresh reuse scope — Blocking

Recommended: mark the entire session compromised and revoke active descendants in that session.

## DEC-AUTH-0010 — Concurrent refresh — Blocking

Recommended: no grace window; exactly one request succeeds and reuse triggers compromise handling.

## DEC-AUTH-0011 — Session limit — Required

Recommended: configurable maximum 10 active sessions per identity.

## DEC-AUTH-0012 — Password-change revocation — Blocking

Recommended: authenticated change keeps current session and revokes others; reset or compromise revokes all.

## DEC-AUTH-0013 — Tenant-selection transaction — Blocking

Recommended: persisted, short-lived, single-use transaction; never accepted by business APIs.

## DEC-AUTH-0014 — Browser token storage — Blocking

Recommended: access token in memory; refresh token in Secure HttpOnly SameSite cookie; CSRF protection; no refresh token in localStorage. Confirm same-site versus cross-site deployment.

## DEC-AUTH-0015 — Mobile/desktop clients — Required

Clarify whether V1 includes them. Native clients require separate ClientId and OS secure token storage.

## DEC-AUTH-0016 — Invitation lifetime — Blocking

Recommended: 24 hours, configurable, single-use; reissue revokes older active invitations.

## DEC-AUTH-0017 — Reset lifetime — Blocking

Recommended: 30 minutes, configurable, single-use; newer request revokes older active reset tokens.

## DEC-AUTH-0018 — MFA — Sprint decision

Recommended: V1 architecture-compatible but no tenant-user MFA flow; mandatory MFA before production App Owner/App Support access.

## DEC-AUTH-0019 — Signing keys — Blocking

Recommended: external asymmetric keys, `kid`, controlled overlap, no production symmetric secret in Git.

## DEC-AUTH-0020 — Security-version checks — Blocking

Recommended: always check on refresh; use short-lived access tokens; add server-side checks only for high-risk operations.

## DEC-AUTH-0021 — Immutable security audit — Production blocker

Recommended: separate audit feature package; FP-002 emits structured security events and is not production-complete until immutable storage exists.

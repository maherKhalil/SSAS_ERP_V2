---
document_id: FP-002-REQ
title: Authentication Requirements
status: Approved for Implementation
version: 1.0
---

# Requirements

## Business requirements

- **BR-AUTH-0001:** Authenticate a person using an approved globally unambiguous login identifier.
- **BR-AUTH-0002:** Do not grant business access before resolving an active tenant membership.
- **BR-AUTH-0003:** Each login session is independently identifiable, refreshable, revocable, and auditable.
- **BR-AUTH-0004:** Access tokens are short-lived, audience-restricted, and contain only required claims.
- **BR-AUTH-0005:** Refresh tokens are confidential, rotated after use, and protected against replay.
- **BR-AUTH-0006:** Invitations and recovery use short-lived, single-use tokens.
- **BR-AUTH-0007:** Security controls cover failed login, disabled accounts, tenant suspension, password changes, and revocation.
- **BR-AUTH-0008:** Public responses do not disclose whether an account exists.
- **BR-AUTH-0009:** Authentication security events are immutably auditable.

## Functional requirements

- **FR-AUTH-0101:** Complete an invitation and set the initial password.
- **FR-AUTH-0102:** Validate the global login identifier and password.
- **FR-AUTH-0103:** Resolve active tenant memberships after credential verification.
- **FR-AUTH-0104:** Automatically select the only eligible membership.
- **FR-AUTH-0105:** Require explicit selection when multiple memberships exist.
- **FR-AUTH-0106:** Issue a signed tenant-scoped access token with exactly one tenant claim.
- **FR-AUTH-0107:** Issue a random refresh token bound to identity, tenant, session, and client.
- **FR-AUTH-0108:** Rotate refresh tokens after successful use.
- **FR-AUTH-0109:** Detect reuse and revoke the approved family/session scope.
- **FR-AUTH-0110:** Refresh only while identity, membership, tenant, session, and client remain eligible.
- **FR-AUTH-0111:** Logout current session.
- **FR-AUTH-0112:** Logout all sessions.
- **FR-AUTH-0113:** View safe active-session metadata.
- **FR-AUTH-0114:** Revoke a selected session.
- **FR-AUTH-0115:** Change password.
- **FR-AUTH-0116:** Request reset without account enumeration.
- **FR-AUTH-0117:** Complete reset with a single-use token.
- **FR-AUTH-0118:** Apply rate limiting and approved lockout.
- **FR-AUTH-0119:** Block disabled identities and deactivated memberships.
- **FR-AUTH-0120:** Block suspended tenants from normal token issuance.
- **FR-AUTH-0121:** Validate issuer, audience, signature, lifetime, and required claims.
- **FR-AUTH-0122:** Support signing-key rotation.
- **FR-AUTH-0123:** Generate claims from trusted server-side state.
- **FR-AUTH-0124:** Enforce security-version invalidation.
- **FR-AUTH-0125:** Bind sessions and refresh tokens to an approved client.

## Security requirements

Passwords use the approved ASP.NET Core password hasher. Refresh, reset, and invitation tokens are stored only as hashes. Secrets, raw JWTs, hashes, and full claims collections never appear in logs. Rotation and reuse detection are atomic. HTTPS is mandatory.

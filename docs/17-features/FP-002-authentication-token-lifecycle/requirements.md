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

- **FR-AUTH-0101:** Complete an invitation, setting the initial password only when the AuthenticationAccount is new or `PendingSetup`, and activate only the intended pending membership.
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
- **FR-AUTH-0126:** Begin tenant access only from an internal VerifiedIdentity, discover memberships using exact Identity ownership, and validate every candidate Tenant through FP-003.
- **FR-AUTH-0127:** Create one immutable identity-, membership-, tenant-, client-, and token-family-bound AuthenticationSession with its first refresh token.
- **FR-AUTH-0128:** Persist a five-minute, single-use, ClientId-bound tenant-selection transaction and consume it atomically with successful session creation.
- **FR-AUTH-0129:** Issue and rotate exact selector/secret refresh tokens, retain predecessor and replacement history, and cap token expiry by session idle and absolute expiration.
- **FR-AUTH-0130:** Detect verified reuse of any retained consumed refresh token, compromise only its owning session, and revoke its unconsumed descendants.
- **FR-AUTH-0131:** Maintain AuthenticationSession lifecycle using only Active, Revoked, and Compromised, with computed idle and absolute expiration and retained revocation history.
- **FR-AUTH-0132:** Enforce at most ten active unexpired sessions per Identity by revoking the oldest sessions before atomically creating a new one.
- **FR-AUTH-0133:** Complete password reset atomically with SecurityVersion advancement and revocation of every active session for the Identity.
- **FR-AUTH-0134:** Accept only an allowlisted exact ClientId and bind it immutably across selection transactions, sessions, and refresh records.
- **FR-AUTH-0135:** Serialize selection, session creation, refresh, revocation, password reset, and eligibility races using the approved transaction and lock order.

## Security requirements

- **SEC-AUTH-0201 — Password protection:** Passwords use the approved ASP.NET Core password hasher and are never stored in plaintext or reversible form. Successful verification preserves the framework's rehash-required outcome.
- **SEC-AUTH-0202 — Secret-token persistence:** Refresh, reset, and invitation secrets are stored only as secure hashes. Raw secrets never enter persistence.
- **SEC-AUTH-0203 — Sensitive observability:** Passwords, raw action tokens, raw refresh tokens, raw JWTs, password hashes, token hashes, and full claims collections never appear in logs, telemetry, exceptions, audit records, or domain events.
- **SEC-AUTH-0204 — Generic public failure:** Public credential and reset responses do not reveal account existence, disabled state, lockout state, or the exact authentication failure.
- **SEC-AUTH-0205 — Action-token binding:** Invitation and reset tokens are single-use, expire, and are bound to their exact purpose and intended owner.
- **SEC-AUTH-0206 — Cryptographic randomness:** Action-token selectors and secrets, refresh tokens, tenant-selection secrets, JWT identifiers, and local password-account Identity subjects use cryptographically secure randomness where generated by FP-002.
- **SEC-AUTH-0207 — Constant-time secret comparison:** Presented action-token secrets are compared to stored hash material using constant-time comparison after candidate lookup by public selector.
- **SEC-AUTH-0208 — Transport security:** Authentication and token operations require HTTPS. Refresh credentials receive the approved browser cookie and CSRF protections when their HTTP workflow is implemented.
- **SEC-AUTH-0209 — External secret ownership:** Production signing keys, private keys, dataset secrets, and deployment credentials remain outside source control and ordinary configuration files.
- **SEC-AUTH-0210 — Compromised-password enforcement:** Production password setup and reset use the approved deployment-provided offline compromised/common-password dataset and fail safely when a required dataset is missing or invalid.
- **SEC-AUTH-0211 — JWT validation:** Tenant access tokens validate signature, issuer, audience, lifetime, and required claims before establishing authenticated tenant context.
- **SEC-AUTH-0212 — Atomic refresh security:** Refresh rotation and reuse detection are atomic, and a submitted refresh token succeeds at most once.
- **SEC-AUTH-0213 — Verified authentication capability:** Ordinary callers cannot construct VerifiedIdentity from an arbitrary IdentityId or override its SecurityVersion or tenant eligibility.
- **SEC-AUTH-0214 — Selection and refresh secret boundary:** Selection proofs and refresh tokens use the approved exact 76-character canonical selector/secret formats, fixed-size hashes, pre-lookup parsing, fixed-time comparison, reveal-once results, and no ordinary serialization or observability.
- **SEC-AUTH-0215 — Exact client binding:** ClientId is allowlisted, exact, ordinal, case-sensitive, whitespace-rejecting, immutable, and revalidated for every selection and refresh.
- **SEC-AUTH-0216 — Pre-tenant isolation:** Membership discovery includes trusted IdentityId in every predicate, returns only safe eligible projections, and introduces no generic cross-tenant or ordinary-repository query-filter bypass.

## Non-functional requirements

- **NFR-AUTH-0301 — Asynchronous operations:** Persistence and external-I/O operations are asynchronous and accept cancellation tokens.
- **NFR-AUTH-0302 — Clean Architecture:** FP-002 Domain and Application code remain free of EF Core, SQL Server, ASP.NET Core, HTTP, and cryptographic framework dependencies.
- **NFR-AUTH-0303 — Module isolation:** Platform authentication does not depend on HR or GL implementation projects and does not access another module's database.
- **NFR-AUTH-0304 — SQL Server verification:** Migrations, rowversion behavior, filtered indexes, restricted deletes, and concurrent token or failed-login behavior are verified against SQL Server rather than inferred from SQLite.
- **NFR-AUTH-0305 — Configurable security policy:** Password-hashing work factor, password limits, lockout threshold and duration, access/action/refresh-token lifetimes, and session limits are deployment-configurable within validated approved bounds.
- **NFR-AUTH-0306 — Quality gates:** The full solution build, automated tests, and architecture tests pass with zero introduced warnings or errors.
- **NFR-AUTH-0307 — Audit-ready events:** FP-002 emits structured, immutable event values suitable for later security-audit persistence without containing secret material.
- **NFR-AUTH-0308 — Transactional concurrency:** SQL Server integration tests verify the canonical lock order, concurrent session-limit enforcement, selection consumption, refresh/reuse behavior, reset/refresh races, and tenant-suspension races.
- **NFR-AUTH-0309 — Retained authentication history:** Session revocation and refresh-token family history are retained, use restricted deletion, and expose no routine physical-delete behavior.
- **NFR-AUTH-0310 — Bounded indexed secret lookup:** Selection and refresh inputs are rejected at exact parser bounds before indexed selector lookup; verification never scans secret hashes.

## Sprint-01 milestone boundaries

Milestone 2 implements the credential and account-action portions of `FR-AUTH-0101`, `FR-AUTH-0102`, `FR-AUTH-0116`, `FR-AUTH-0117`, `FR-AUTH-0118`, and `FR-AUTH-0124`, together with the applicable security and non-functional requirements above.

Milestone 3 implements the internal tenant-resolution, selection-transaction, AuthenticationSession, refresh-token, session-limit, password-reset revocation, and concurrency portions of `FR-AUTH-0103` through `FR-AUTH-0110`, `FR-AUTH-0120`, `FR-AUTH-0124`, `FR-AUTH-0125`, and `FR-AUTH-0126` through `FR-AUTH-0135`, together with their applicable security and non-functional requirements. FP-003 supplies the authoritative tenant eligibility fact.

Access-token/JWT issuance, HTTP authentication endpoints, endpoint rate limiting, browser cookies and CSRF, signing-key implementation, logout and session-administration endpoints, authenticated password change, Angular authentication, immutable audit storage, and platform-support authentication remain deferred.

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

- **FR-AUTH-0136:** Expose exactly the four Milestone 4 authentication routes with the approved request shapes, response statuses, Problem Details codes, and server-bound `ssas-erp-web` ClientId.
- **FR-AUTH-0137:** Issue a default 15-minute access token containing the exact approved claim set, cardinality, formatting, ordering, exclusions, and 8192-byte compact-encoding limit.
- **FR-AUTH-0138:** Obtain production RS256 signing and rollover verification keys from the approved deployment-mounted X.509 provider without changing Application code.
- **FR-AUTH-0139:** Derive `kid` from certificate DER bytes, select one active signing key, and retain old verification keys for the approved rollover overlap.
- **FR-AUTH-0140:** Validate access tokens strictly with exact issuer, audience, RS256 algorithm, enabled `kid`, lifetime, `nbf`, 30-second skew, and exact claims before constructing trusted request context.
- **FR-AUTH-0141:** Create, rotate, and clear the exact secure host-only refresh cookie using identical approved attributes and the current refresh-token expiry.
- **FR-AUTH-0142:** Protect refresh and logout with the exact signed double-submit CSRF cookie/header contract and shared production Data Protection configuration.
- **FR-AUTH-0143:** Apply the exact endpoint-specific zero-queue rate limits and generic 429 behavior using secret-safe HMAC partitions and production shared enforcement.
- **FR-AUTH-0144:** Enforce exact HTTPS Origin and restrictive credentialed CORS rules and resolve client IP only through the approved direct or trusted-proxy mode.
- **FR-AUTH-0145:** Perform one centralized live FP-003 eligibility lookup for every ordinary tenant-scoped authenticated business request and authorize only current Active tenants.
- **FR-AUTH-0146:** Issue an access token inside the still-open Application transaction, roll back issuance failures, dispatch success events only after commit, and write cookies only after commit.
- **FR-AUTH-0147:** Revoke only the validated current session through `RevokeCurrentAuthenticationSessionCommand` using `UserLogout`, return idempotent 204, and implement no logout-all route.
- **FR-AUTH-0148:** Return tenant-selection summaries containing exactly TenantId, TenantUserId, and FP-003 TenantName as TenantDisplayName for eligible memberships owned by the verified Identity.
- **FR-AUTH-0149:** Apply the approved no-store response headers and sensitive HTTP logging, exception, compression, and model-state prohibitions to every authentication response.
- **FR-AUTH-0150:** Publish the exact four-route authentication OpenAPI contract without exposing refresh values, CSRF secrets, sensitive wrappers, real token examples, or signing information.
- **FR-AUTH-0151:** Add only the `AddUserLogoutSessionRevocationReason` constraint migration for Milestone 4 and introduce no authentication transport or key-management table.

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

- **SEC-AUTH-0217 — Generic transport failures:** Authentication endpoints expose only the approved generic status/code mappings and never disclose internal credential, eligibility, signing, or persistence causes.
- **SEC-AUTH-0218 — Exact access claims:** Access-token issuance and validation enforce exact critical-claim cardinality, exact and distinct ordinal role/permission values, approved formats and exclusions, and the 8192-byte maximum.
- **SEC-AUTH-0219 — RS256 key protection:** The active path uses RS256 with RSA keys of at least 2048 bits and contains no symmetric fallback, raw production key configuration, source-controlled private key, or generated production key.
- **SEC-AUTH-0220 — Key-selection integrity:** Missing, unknown, disabled, or duplicate `kid` and invalid or insufficient rollover metadata fail closed without unrelated-key probing.
- **SEC-AUTH-0221 — Strict JWT trust boundary:** Trusted user and tenant context is created only after signature, exact algorithm, key, issuer, audience, lifetime, `nbf`, and claim validation succeeds with inbound claim mapping disabled.
- **SEC-AUTH-0222 — Refresh-cookie protection:** The refresh credential remains host-only, Secure, HttpOnly, SameSite Strict, path-scoped, absent from JSON and JavaScript, and is cleared with attribute parity on terminal failure or logout.
- **SEC-AUTH-0223 — CSRF binding:** Refresh and logout require an unexpired Data-Protection-signed cookie/header value bound to exact session, refresh selector, and ClientId, rotated with refresh and cleared with the refresh cookie.
- **SEC-AUTH-0224 — Browser request provenance:** All four routes validate exact approved Origin; login and tenant selection are JSON-only; CORS permits credentials only for explicit exact HTTPS origins and no wildcard.
- **SEC-AUTH-0225 — Trusted client IP:** Direct mode ignores forwarded headers; trusted-proxy mode accepts forwarded client IP only from explicit known proxies or networks under the approved forward limit.
- **SEC-AUTH-0226 — Secret-safe rate limiting:** Rate-limit partitions and logs expose no raw identifier, proof, token, cookie, session secret, HMAC value, or partition key, and the partition HMAC key remains a deployment secret.
- **SEC-AUTH-0227 — Live tenant status:** Current FP-003 tenant eligibility, never a TenantStatus token claim, is required by ordinary tenant role and permission authorization; logout remains available through a separate authenticated policy.
- **SEC-AUTH-0228 — Transactional token issuance:** No access token, event, or cookie represents rolled-back session or refresh state; token issuance failure rolls back the SQL transaction.
- **SEC-AUTH-0229 — Trusted current-session logout:** Logout accepts no session identifier and verifies validated Identity, Tenant, TenantUser, ClientId, session, and SecurityVersion binding before revoking only that session.
- **SEC-AUTH-0230 — Sensitive HTTP handling:** Authentication bodies, commands, sensitive results, Authorization headers, cookie values, proofs, tokens, hashes, and model-state secrets are excluded from logs and exception details; responses are not compressed.
- **SEC-AUTH-0231 — Safe OpenAPI:** OpenAPI describes required security and cookie/header behavior without publishing refresh credentials, CSRF secrets, internal commands, sensitive wrappers, private signing data, or realistic tokens.
- **SEC-AUTH-0232 — Production cryptographic state:** Production signing and Data Protection material uses deployment-owned persistent providers and encrypted secrets, with startup failure when required material is unavailable or invalid.

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

- **NFR-AUTH-0311 — Signing-provider abstraction:** `IAccessTokenIssuer` and signing-key provider boundaries keep Application framework-neutral and permit later HSM, KMS, or Azure Key Vault replacement without Application changes.
- **NFR-AUTH-0312 — Rotation availability:** Deployment rotation supports simultaneous old/new verification across every instance for at least token lifetime plus clock skew, with startup validation of the complete provider snapshot.
- **NFR-AUTH-0313 — Data Protection availability:** Production instances share a persistent encrypted Data Protection key ring and fail startup when the configured key ring or protection certificate cannot be used.
- **NFR-AUTH-0314 — Distributed rate enforcement:** Production declares and uses an approved shared gateway or distributed rate-limit provider in addition to process-local application limits.
- **NFR-AUTH-0315 — Live-eligibility query bound:** Each tenant-scoped request performs one scoped FP-003 eligibility lookup; the implementation prevents duplicate policy lookups inside the request scope.
- **NFR-AUTH-0316 — Transport ambiguity:** SQL commit and cookie delivery are explicitly non-atomic; post-commit cookie failure is not automatically retried and creates no refresh grace window.
- **NFR-AUTH-0317 — Startup configuration validation:** Production fails startup for invalid key, Data Protection, allowed-origin, proxy, or distributed-enforcement configuration rather than running insecurely.
- **NFR-AUTH-0318 — OpenAPI completeness:** Generated OpenAPI remains consistent with exact route, security, header, schema, status, and Problem Details contracts and contains no sensitive examples.
- **NFR-AUTH-0319 — Milestone 4 persistence restraint:** No table stores signing keys, access tokens, CSRF state, rate counters, OpenAPI, CORS, or proxy configuration; only the approved reason-constraint migration changes SQL schema.

## Sprint-01 milestone boundaries

Milestone 2 implements the credential and account-action portions of `FR-AUTH-0101`, `FR-AUTH-0102`, `FR-AUTH-0116`, `FR-AUTH-0117`, `FR-AUTH-0118`, and `FR-AUTH-0124`, together with the applicable security and non-functional requirements above.

Milestone 3 implements the internal tenant-resolution, selection-transaction, AuthenticationSession, refresh-token, session-limit, password-reset revocation, and concurrency portions of `FR-AUTH-0103` through `FR-AUTH-0110`, `FR-AUTH-0120`, `FR-AUTH-0124`, `FR-AUTH-0125`, and `FR-AUTH-0126` through `FR-AUTH-0135`, together with their applicable security and non-functional requirements. FP-003 supplies the authoritative tenant eligibility fact.

Milestone 4 implements `FR-AUTH-0106` through `FR-AUTH-0111`, `FR-AUTH-0121` through `FR-AUTH-0123`, and `FR-AUTH-0136` through `FR-AUTH-0151`, together with `SEC-AUTH-0217` through `SEC-AUTH-0232` and `NFR-AUTH-0311` through `NFR-AUTH-0319`. Its public scope is limited to login, tenant selection, refresh, and current-session logout plus their access-token, authorization, and browser transport foundations.

Authenticated password change, password-reset/invitation HTTP delivery, session listing, revoke-another-session, logout-all, Angular authentication, immutable audit storage, Platform-support authentication, MFA, external providers, native/mobile clients, service authentication, API keys, impersonation, notification delivery, JWKS, and high-risk business policies beyond the reusable live-Tenant foundation remain deferred.

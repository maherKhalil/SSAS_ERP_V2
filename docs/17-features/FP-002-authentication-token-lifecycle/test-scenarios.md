---
document_id: FP-002-TEST
title: Authentication Test Scenarios
status: Approved for Implementation
version: 1.0
---

# Test Scenarios

## Credential and account-action core

- **TS-AUTH-0001:** Complete an invitation for a new password account, verify the global login email, activate the account, and activate only the intended pending membership.
- **TS-AUTH-0002:** Complete an invitation for an existing verified active account without requesting or changing its password or security version.
- **TS-AUTH-0003:** Complete an invitation for an existing `PendingSetup` account by setting its initial password.
- **TS-AUTH-0004:** Reject invitation of an already active membership and require the separate approved reactivation workflow for a deactivated membership.
- **TS-AUTH-0005:** Verify that invitation issuance and completion store no role identifiers and assign no roles.
- **TS-AUTH-0006:** Generate a new password-based Identity subject as exact `local:{guid}` in `N` format and reject caller control of the subject.
- **TS-AUTH-0007:** Preserve a stable local Identity subject when login email changes in a later approved workflow.
- **TS-AUTH-0008:** Normalize global login email with trim and `ToUpperInvariant()` while preserving trimmed display casing and applying no provider-specific transformation.
- **TS-AUTH-0009:** Verify a correct password and return only internal credential success without issuing a token.
- **TS-AUTH-0010:** Return the same generic credential failure for unknown identifier, wrong password, disabled account, and locked account.
- **TS-AUTH-0011:** Rehash a password after successful verification when the framework requests rehashing without changing password-changed time or security version.
- **TS-AUTH-0012:** Lock an account after five consecutive failed attempts for the configured fifteen-minute duration.
- **TS-AUTH-0013:** Make the account eligible after lockout expiry and reset failed-attempt state after successful verification.
- **TS-AUTH-0014:** Reread and reapply concurrent failed attempts with no more than three optimistic-concurrency retries, never returning success after a conflict.
- **TS-AUTH-0015:** Reject a password found by the approved compromised/common-password checker and fail safely when its required production dataset is unavailable.
- **TS-AUTH-0016:** Request password reset without disclosing whether an eligible account exists.
- **TS-AUTH-0017:** Complete password reset once, replace the password hash, clear lockout state, and increment security version.
- **TS-AUTH-0018:** Reject expired, consumed, revoked, malformed, or wrong-purpose invitation and reset tokens.
- **TS-AUTH-0019:** Keep the current session, revoke other sessions, and increment security version after authenticated password change.

## Tenant selection

- **TS-AUTH-0020:** Automatically select the only eligible active tenant membership.
- **TS-AUTH-0021:** Require explicit selection when multiple eligible active memberships exist.
- **TS-AUTH-0022:** Exclude pending and deactivated memberships from tenant selection.
- **TS-AUTH-0023:** Reject a tenant selection for which the authenticated Identity has no eligible membership.
- **TS-AUTH-0024:** Prevent a tenant-selection transaction from accessing tenant business APIs and reject expired, replayed, or revoked transactions.
- **TS-AUTH-0025:** Issue an access token containing exactly one tenant and only that membership's roles and permissions.

## Session and token lifecycle

- **TS-AUTH-0030:** Issue and validate an RS256 access token with approved issuer, audience, lifetime, `kid`, required claims, and exact claim values.
- **TS-AUTH-0031:** Issue a client-, session-, identity-, and tenant-bound refresh token and rotate it after one successful use.
- **TS-AUTH-0032:** Detect reuse of a consumed refresh token, compromise the session, revoke descendants, and require reauthentication for that session.
- **TS-AUTH-0033:** Enforce session idle and absolute expiry without extending absolute expiry during refresh.
- **TS-AUTH-0034:** Reject login or refresh when the account, membership, tenant, session, or client is ineligible.
- **TS-AUTH-0035:** Permit at most one successful concurrent refresh with no reuse grace window.
- **TS-AUTH-0036:** Reject refresh from a client that does not match the session and refresh-token binding.
- **TS-AUTH-0037:** Validate controlled signing-key overlap while issuing new tokens only with the current key.
- **TS-AUTH-0038:** Enforce the configured active-session limit by revoking the oldest active session and preserving history.
- **TS-AUTH-0039:** Log out the current session without revoking unrelated sessions.
- **TS-AUTH-0040:** Revoke all active sessions for an Identity during logout-all or approved security reset.
- **TS-AUTH-0041:** List only safe session metadata and revoke one selected session without exposing token material.
- **TS-AUTH-0042:** Reject refresh when the current account security version differs from the session or refresh state.

## API and security

- **TS-AUTH-0050:** Return indistinguishable public login failures without account enumeration.
- **TS-AUTH-0051:** Return the same accepted reset-request response for existing and unknown login identifiers.
- **TS-AUTH-0052:** Apply the approved endpoint rate limit by normalized login identifier and trusted network signal.
- **TS-AUTH-0053:** Enforce Secure, HttpOnly, SameSite=Strict refresh cookies and anti-CSRF protection for refresh and logout.
- **TS-AUTH-0054:** Include correlation metadata without logging passwords, raw tokens, hashes, JWTs, or full claims collections.
- **TS-AUTH-0055:** Permit anonymous access only to explicitly documented authentication endpoints.
- **TS-AUTH-0056:** Verify that a Milestone 2 sensitive raw-token result cannot be serialized as an ordinary HTTP response DTO.

## SQL Server

- **TS-AUTH-0060:** Apply the Platform migration chain to an empty SQL Server database and upgrade a database containing `InitialIdentityAccess`.
- **TS-AUTH-0061:** Enforce one AuthenticationAccount per Identity and one globally unique binary-collated normalized login email.
- **TS-AUTH-0062:** Store exact fixed 32-byte action-token hashes and no raw invitation, reset, or refresh-token column.
- **TS-AUTH-0063:** Preserve consumed and revoked action-token history.
- **TS-AUTH-0064:** Preserve concurrent failed attempts and enforce the lockout transition using SQL Server rowversion and bounded retry.
- **TS-AUTH-0065:** Permit at most one successful concurrent action-token consumption.
- **TS-AUTH-0066:** Preserve refresh-token rotation history, permit at most one successful atomic refresh, and detect subsequent reuse.
- **TS-AUTH-0067:** Enforce restricted deletes for Identity, AuthenticationAccount, TenantUser, action-token, session, and refresh-token relationships.
- **TS-AUTH-0068:** Verify that global AuthenticationAccount and AccountActionToken records receive no tenant query filter or automatic TenantId assignment.

## Architecture and scope

- **TS-AUTH-0070:** Keep Domain and Application free of EF Core, SQL Server, ASP.NET Core, HTTP, and cryptographic framework dependencies.
- **TS-AUTH-0071:** Define only aggregate-specific repositories and expose no generic repository or `IQueryable` boundary.
- **TS-AUTH-0072:** Keep Platform authentication independent from HR and GL implementations.
- **TS-AUTH-0073:** Scan source, configuration, logs, exceptions, events, and test artifacts for committed production secrets or sensitive-value logging.
- **TS-AUTH-0074:** Verify that Milestone 2 introduces no session, refresh-token, JWT-issuance, tenant-selection, HTTP endpoint, cookie, CSRF, RS256, logout/session API, Angular, immutable-audit-store, or platform-support-authentication implementation.

## Milestone 3 focused scenarios

- **TS-AUTH-0075:** Enforce a configurable maximum of ten active unexpired sessions per Identity.
- **TS-AUTH-0076:** Order active sessions by CreatedUtc then AuthenticationSessionId and atomically revoke enough oldest sessions with `SessionLimitExceeded` before creating a new session.
- **TS-AUTH-0077:** Default idle expiration to 30 days, compute current usability from trusted UTC, and reject refresh at or after `IdleExpiresUtc`.
- **TS-AUTH-0078:** Default absolute expiration to 90 days and prove that creation and refresh never extend `AbsoluteExpiresUtc`.
- **TS-AUTH-0079:** Set every refresh token's `ExpiresUtc` to the minimum of session idle and absolute expiration, including after idle-expiry renewal.
- **TS-AUTH-0080:** Persist a five-minute tenant-selection transaction using a unique Guid selector and exact `BINARY(32)` canonical hash without storing its raw proof.
- **TS-AUTH-0081:** Generate, parse, hash, and verify the exact 76-character refresh-token format and reject malformed, oversized, noncanonical, and multi-separator input before lookup.
- **TS-AUTH-0082:** Generate, parse, hash, and verify the exact 76-character tenant-selection proof format and reject malformed, oversized, noncanonical, and multi-separator input before lookup.
- **TS-AUTH-0083:** Accept exact allowlisted `ssas-erp-web`, reject whitespace and casing variants, and enforce immutable exact ClientId binding during selection and refresh.
- **TS-AUTH-0084:** Persist only Active, Revoked, and Compromised session states; permit only the approved transitions and revocation reasons; compute expiration separately; retain terminal history.
- **TS-AUTH-0085:** Serialize concurrent session creation at the ten-session limit so the committed state never exceeds the limit and deterministic oldest-session revocation occurs once.
- **TS-AUTH-0086:** Serialize password reset racing with refresh so reset atomically advances SecurityVersion and revokes all sessions or refresh completes first against the prior valid state, without partial changes.
- **TS-AUTH-0087:** Revalidate FP-003 Tenant eligibility under the approved lock order when suspension races with session creation or refresh, preventing a non-Active Tenant from committing eligible access.
- **TS-AUTH-0088:** Permit exactly one successful concurrent tenant-selection consumption and create at most one session while every losing request returns generic selection failure.
- **TS-AUTH-0089:** Permit at most one successful concurrent refresh; treat the losing verified use as reuse, compromise only the owning session, and revoke its unconsumed descendants.
- **TS-AUTH-0090:** Verify that reveal-once refresh-token and selection-proof results cannot be serialized as ordinary DTOs or exposed through commands, persistence, logs, telemetry, exceptions, events, or debugger displays.

## Milestone 4 transport-security scenarios

- **TS-AUTH-0091:** Verify exactly the four approved routes, request bodies, server-bound ClientId, successful statuses, and Problem Details code/status mappings.
- **TS-AUTH-0092:** Verify credential, no-membership, locked, disabled, selection, refresh, CSRF/Origin, rate-limit, and temporary-service failures disclose no internal cause.
- **TS-AUTH-0093:** Issue and validate the exact singleton claim set, invariant identifier formats, canonical TenantId, random N-format JTI, exact ClientId, and distinct ordinally sorted role/permission values.
- **TS-AUTH-0094:** Reject duplicate required claims, duplicate role/permission values, prohibited claim data, and an access token whose compact encoding would exceed 8192 bytes without truncation.
- **TS-AUTH-0095:** Load an active deployment-mounted X.509 RSA signing certificate and retained verification certificates, enforce 2048-bit minimum, and fail Production startup for missing, invalid, expired, or insufficient-lifetime material.
- **TS-AUTH-0096:** Verify Development uses only an announced ephemeral process-local RSA key and deterministic tests use only test-owned RSA keys, with no production generation or symmetric fallback.
- **TS-AUTH-0097:** Derive `kid` exactly from certificate DER bytes and reject missing, unknown, disabled, or duplicate identifiers without probing unrelated keys.
- **TS-AUTH-0098:** Validate one immutable issuance snapshot and old/new rollout overlap across instances for at least token lifetime plus 30-second skew, rejecting early retirement metadata.
- **TS-AUTH-0099:** Reject unsigned, HMAC, algorithm-substitution, bad-signature, wrong-issuer, wrong-audience, expired, premature, malformed, and invalid-claim JWTs with `MapInboundClaims = false` and no `Jwt:SigningKey` path.
- **TS-AUTH-0100:** Verify refresh-cookie creation, successful-refresh replacement, logout deletion, and terminal-failure deletion use exact name, path, Domain omission, Secure, HttpOnly, SameSite, and expiry behavior.
- **TS-AUTH-0101:** Accept only exact equal CSRF cookie/header state with valid signature, expiry, session, refresh-selector, and `ssas-erp-web` binding; reject tampering, replay after rotation, mismatch, and expiry.
- **TS-AUTH-0102:** Verify Production Data Protection uses the shared persistent encrypted key ring and fails startup for an unavailable directory, protection certificate, or secret configuration.
- **TS-AUTH-0103:** Require JSON-only login/selection, exact Origin on all four routes, and restrictive credentialed CORS; reject wildcard, non-HTTPS, path/query/fragment, and unapproved origins.
- **TS-AUTH-0104:** Verify direct mode ignores forwarded headers, trusted-proxy mode honors only known forwarders with the approved limit, and invalid Production proxy configuration fails startup.
- **TS-AUTH-0105:** Enforce login limits of 30/minute by trusted IP and 5/15 minutes by HMAC(normalized email + IP), zero queue, generic 429, and Retry-After.
- **TS-AUTH-0106:** Enforce selection 10/proof lifetime, refresh 10/minute, and logout 5/minute partitions with trusted IP and verified state while exposing no raw partition input or HMAC value.
- **TS-AUTH-0107:** Verify Production requires explicit shared gateway/distributed enforcement mode while Development/Test remains operable with approved process-local limiters and no database counter table.
- **TS-AUTH-0108:** Perform exactly one scoped live FP-003 lookup per ordinary tenant request; allow only Active and deny Provisioning, Suspended, Archived, and missing while allowing authenticated logout for a suspended tenant.
- **TS-AUTH-0109:** Verify TenantStatus is absent from the JWT and account, session, membership, role, and permission changes may remain stale no longer than the 15-minute access-token lifetime unless a high-risk policy checks live state.
- **TS-AUTH-0110:** Force access-token issuance failure during session creation and refresh and verify SQL rollback, no usable token, no success event dispatch, and no cookie write.
- **TS-AUTH-0111:** Verify successful issuance precedes commit, success events follow commit, cookies follow commit, and simulated post-commit cookie failure produces documented ambiguity without automatic retry or grace window.
- **TS-AUTH-0112:** Logout with validated current-session context, exact binding checks, `UserLogout`, current-session-only revocation, idempotent absent/terminal handling, cookie clearing, and 204; reject any request SessionId and expose no logout-all.
- **TS-AUTH-0113:** Apply `AddUserLogoutSessionRevocationReason` on SQL Server, accept `UserLogout`, preserve all existing reasons and data, and verify the migration changes only the reason constraint.
- **TS-AUTH-0114:** Return only TenantId, TenantUserId, and FP-003 TenantName as TenantDisplayName for eligible memberships owned by the verified Identity, with proof reveal-once in JSON and every prohibited field absent.
- **TS-AUTH-0115:** Verify all authentication responses emit no-store, no-cache, no-referrer, and nosniff headers and exclude sensitive bodies, headers, cookies, commands, results, and model-state values from logs, exceptions, compression, and examples.
- **TS-AUTH-0116:** Validate generated OpenAPI for exact four-route visibility, Bearer security, anonymous classifications, refresh-cookie description, X-XSRF-TOKEN, response schemas, statuses/codes, and absence of sensitive values.
- **TS-AUTH-0117:** Verify Milestone 4 creates no table for signing keys, access tokens, CSRF, rate limits, OpenAPI, CORS, or proxies and introduces no migration other than the approved constraint change.
- **TS-AUTH-0118:** Run API, Application, architecture, SQL Server, concurrency, security, key-rotation, configuration, and end-to-end authentication suites with only the approved Milestone 4 surface enabled.

## Milestone applicability

Milestone 2 implements `TS-AUTH-0001` through `TS-AUTH-0018`, `TS-AUTH-0054`, `TS-AUTH-0056`, `TS-AUTH-0060` through `TS-AUTH-0065`, `TS-AUTH-0067`, `TS-AUTH-0068`, and `TS-AUTH-0070` through `TS-AUTH-0074` where those scenarios concern `AuthenticationAccount` or `AccountActionToken`.

Milestone 3 implements the tenant-selection and internal session/refresh portions of `TS-AUTH-0020` through `TS-AUTH-0024`, `TS-AUTH-0031` through `TS-AUTH-0036`, `TS-AUTH-0038`, `TS-AUTH-0040`, `TS-AUTH-0042`, `TS-AUTH-0060`, `TS-AUTH-0062`, `TS-AUTH-0066` through `TS-AUTH-0068`, `TS-AUTH-0070` through `TS-AUTH-0073`, and `TS-AUTH-0075` through `TS-AUTH-0090`.

Milestone 4 implements `TS-AUTH-0025`, `TS-AUTH-0030`, `TS-AUTH-0037`, `TS-AUTH-0039`, `TS-AUTH-0050`, `TS-AUTH-0052` through `TS-AUTH-0055`, the applicable existing session/refresh/concurrency/security scenarios, and `TS-AUTH-0091` through `TS-AUTH-0118`.

Authenticated password change, password-reset and invitation HTTP delivery, session listing, revoke-another-session, logout-all, Angular authentication, immutable audit storage, Platform-support authentication, MFA, external providers, native clients, notification delivery, JWKS, and additional high-risk business policies remain later work.

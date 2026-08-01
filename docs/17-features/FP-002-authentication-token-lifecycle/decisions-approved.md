---
document_id: FP-002-DEC
title: Approved Authentication Decisions
status: Approved for Implementation
version: 1.0
approved_date: 2026-07-31
---

# Approved Decisions

## DEC-AUTH-0001 — Global login identifier

Use a **verified, globally unique login email** owned by `AuthenticationAccount`.

`TenantUser.Email` remains tenant-specific and may differ from the global login email.

The same verified global login email always represents the same global Identity across tenants. A person with memberships in several tenants authenticates once and then selects an eligible tenant.

## DEC-AUTH-0002 — Credential ownership

Create a separate `AuthenticationAccount` aggregate linked one-to-one with the FP-001 `Identity`.

Credentials, password hash, lockout state, failed-attempt state, and security version belong to `AuthenticationAccount`, not to `TenantUser`.

## DEC-AUTH-0003 — Invitation and email verification

User onboarding is invitation-based.

- An authorized administrator creates the invitation.
- The invitation is delivered to the global login email.
- The user proves ownership by consuming a single-use invitation token.
- A new or `PendingSetup` account sets the initial password. An existing verified `Active` account does not change its password during invitation completion.
- Administrators never create, view, or communicate passwords.
- Email verification is mandatory for password accounts.
- Accepting an invitation for an existing Identity links or activates only the intended tenant membership and discloses no cross-tenant membership data.

## DEC-AUTH-0004 — Password policy

Use the following V1 policy:

- minimum length: 12 characters;
- supported maximum length: at least 64 characters;
- spaces and Unicode are allowed;
- no mandatory uppercase/lowercase/digit/symbol composition rule;
- no scheduled periodic password expiration;
- common, expected, or compromised passwords are rejected;
- paste and password managers are allowed;
- password change is forced only after compromise, reset, or another approved security event.

## DEC-AUTH-0005 — Password hashing

Use ASP.NET Core `PasswordHasher`.

- Use the framework versioned password-hash format.
- Keep work-factor configuration externalized.
- Rehash after successful verification when the framework indicates rehashing is required.
- Do not use low-level PBKDF2 APIs directly for ordinary application password storage.
- Never store, log, return, or publish raw passwords.

## DEC-AUTH-0006 — Failed-login protection

Use both endpoint rate limiting and account lockout.

Initial V1 policy:

- rate limit by normalized login identifier and trusted network signal;
- lock the authentication account after 5 consecutive failed attempts;
- lockout duration: 15 minutes;
- successful authentication resets the failed-attempt counter;
- public errors remain generic and do not disclose account existence or lockout state;
- administrative unlock requires explicit permission and immutable audit history.

All thresholds are configurable.

## DEC-AUTH-0007 — Access-token lifetime

Tenant-scoped access tokens expire after **15 minutes**.

The lifetime is configurable by deployment, but the production default is 15 minutes.

## DEC-AUTH-0008 — Refresh and session lifetime

Use rotating refresh tokens with:

- session idle lifetime: 30 days;
- session absolute lifetime: 90 days;
- both values configurable;
- shorter policies permitted for privileged platform-support sessions.

A refresh never extends the absolute session expiration.

## DEC-AUTH-0009 — Refresh-token reuse response

Reuse of a consumed refresh token marks the **entire authentication session** as compromised.

The system:

- rejects the request;
- revokes all active refresh-token descendants in that session;
- emits a security event;
- requires reauthentication for that session.

Other identity sessions remain active unless a separate risk decision revokes them.

## DEC-AUTH-0010 — Concurrent refresh behavior

Use no reuse grace window.

For concurrent refresh attempts using the same token:

- at most one request succeeds;
- the successful request consumes and rotates the token atomically;
- subsequent use of the consumed token triggers the approved compromise response.

Clients must coordinate refresh requests.

## DEC-AUTH-0011 — Active-session limit

Allow a configurable maximum of **10 active sessions per Identity**.

When the limit is exceeded:

- revoke the oldest active session;
- create the new session;
- notify the user through the approved security-notification channel;
- preserve revocation history.

## DEC-AUTH-0012 — Password-change session behavior

For an authenticated password change:

- keep the current session active;
- revoke all other active sessions;
- increment the security version.

For password reset, suspected compromise, or administrative security reset:

- revoke all active sessions, including the current session;
- increment the security version;
- require reauthentication.

## DEC-AUTH-0013 — Tenant-selection transaction

Use a **persisted, short-lived, single-use tenant-selection transaction**.

- Lifetime: 5 minutes.
- It proves successful primary authentication.
- It permits only membership discovery and tenant selection.
- It is never accepted by tenant business APIs.
- Selecting a tenant consumes the transaction.
- Replay, expiration, revocation, or arbitrary tenant selection is rejected.

## DEC-AUTH-0014 — Angular browser token storage

V1 assumes the Angular application and API are deployed under the same site boundary.

Use:

- access token stored in memory only;
- refresh token in a `Secure`, `HttpOnly`, `SameSite=Strict` cookie;
- HTTPS only;
- anti-CSRF protection for refresh and logout;
- no refresh token in `localStorage` or `sessionStorage`;
- no raw token logging.

A cross-site deployment requires a separate approved security review before changing cookie policy.

## DEC-AUTH-0015 — Mobile and desktop clients

Native mobile and desktop authentication clients are deferred from V1.

The design remains compatible with future native clients through:

- separate client identifiers;
- operating-system secure storage;
- client-bound refresh sessions;
- future sender-constrained-token hardening.

FP-002 V1 implements the Angular web client and API flow only.

## DEC-AUTH-0016 — Invitation lifetime

Invitation tokens:

- expire after 24 hours;
- are configurable;
- are single-use and purpose-bound;
- are stored only as secure hashes;
- are bound to the intended Identity and tenant membership;
- are revoked when a newer invitation is issued for the same pending membership.

## DEC-AUTH-0017 — Password-reset lifetime

Password-reset tokens:

- expire after 30 minutes;
- are configurable;
- are single-use and purpose-bound;
- are stored only as secure hashes;
- are revoked when a newer reset token is issued for the same account.

## DEC-AUTH-0018 — MFA

For V1 tenant users:

- preserve architectural compatibility with MFA;
- do not implement a tenant-user MFA flow in this package.

Before App Owner/App Support access is released to production:

- MFA is mandatory;
- the concrete support-authentication flow requires a separate approved feature package.

## DEC-AUTH-0019 — JWT signing keys

Use asymmetric RSA signing with **RS256**.

- Private keys come from approved external secret or certificate management.
- Production private keys never appear in source control or ordinary configuration files.
- Every signing key has a `kid`.
- Rotation supports an overlap period where approved prior public keys remain valid.
- New tokens use only the current signing key.
- Issuer and audience are explicitly configured and validated.

## DEC-AUTH-0020 — Security-version validation

- Always validate the current security version during refresh.
- Access tokens remain short-lived.
- High-risk operations may validate current session and security version server-side.
- Normal APIs do not perform a database lookup on every request unless compliance or risk policy requires it.

## DEC-AUTH-0021 — Immutable authentication audit

Immutable security-audit persistence is a separate feature package and a production-release blocker.

FP-002:

- emits structured authentication security events;
- includes no passwords, raw tokens, token hashes, or full claims collections;
- is not considered production-complete until the immutable audit store is delivered.

## DEC-AUTH-0022 — Platform-support authentication

Concrete App Owner/App Support authentication is deferred.

Before production support access is enabled, it must have:

- separate platform-support permissions;
- mandatory MFA;
- shorter session policy;
- explicit target-tenant selection;
- no tenant-role elevation;
- immutable support-action auditing.

## DEC-AUTH-0023 — Local Identity subject

For a new password-based Identity, generate an immutable server-owned subject in this format:

```text
local:{guid}
```

The GUID is cryptographically random and consistently formatted with the `N` format.

Example:

```text
local:72e4872ef9c947e9b4339c27e9478869
```

The subject is exact and case-sensitive. Login email is never used as the Identity subject, changing login email never changes the subject, and callers cannot supply the subject. An existing `AuthenticationAccount` always reuses its existing Identity.

## DEC-AUTH-0024 — Login-email normalization

The display `LoginEmail` is trimmed and preserves its trimmed display casing.

`NormalizedLoginEmail` is calculated using:

```text
Trim().ToUpperInvariant()
```

Provider-specific transformations, including dot removal, plus-alias removal, or Gmail-specific normalization, are prohibited.

`NormalizedLoginEmail` is stored as `NVARCHAR(320)` using `Latin1_General_100_BIN2` collation and has one global unique index.

`TenantUser.Email` remains tenant-specific and independent from the global login email.

## DEC-AUTH-0025 — Invitation membership and roles

Milestone 2 invitations create or target a `Pending` `TenantUser` membership.

- Invitations do not stage or assign roles.
- No role identifiers are stored in `AccountActionToken`.
- No roles are assigned before membership activation.
- Authorized administrators assign roles after membership activation.
- Inviting an already `Active` membership is rejected.
- A `Deactivated` membership is restored only through the existing approved `TenantUser` reactivation workflow, not through invitation.
- First-tenant-administrator provisioning remains part of tenant provisioning, not this milestone.

## DEC-AUTH-0026 — Existing-account invitations

For a new `AuthenticationAccount`, invitation completion requires a password, validates the password policy, hashes the password, verifies `LoginEmail`, activates the account, and activates the intended pending membership.

For an existing verified `Active` `AuthenticationAccount`, invitation completion activates only the intended pending membership. It does not request or change a password and does not increment the security version.

For an existing `PendingSetup` account without a password, completion requires initial password setup.

The operation must not reveal to a tenant administrator whether the email already belongs to another tenant or disclose cross-tenant membership data.

## DEC-AUTH-0027 — Authentication-account status

Approved account statuses are:

- `PendingSetup`;
- `Active`;
- `Disabled`.

Temporary lockout is not an account status. It is represented by `FailedAttemptCount` and `LockoutEndUtc`. An account automatically becomes eligible again after `LockoutEndUtc` when every other eligibility rule passes.

## DEC-AUTH-0028 — Compromised-password check

Define `ICompromisedPasswordChecker` in Platform Application. Its production implementation uses a deployment-provided, versioned offline compromised/common-password dataset.

- Password setup and reset require no network call.
- Raw passwords are never written to the dataset or logs.
- Production startup validation fails when checking is enabled but the approved dataset is missing or invalid.
- Development and tests may use explicit test implementations.
- An unavailable required production dataset causes password setup or reset to fail safely.
- Package and dataset licensing must be documented.
- The check must never be silently disabled in production.

## DEC-AUTH-0029 — Action-token format

Action tokens use this format:

```text
<public-selector>.<secret>
```

The public selector is a cryptographically random GUID used only to locate the candidate record. The secret contains 32 cryptographically random bytes and is Base64Url encoded. It is returned exactly once.

The stored hash is SHA-256, persisted as a fixed 32-byte binary value, and calculated over a canonical domain-separated value containing the token purpose, public selector, and raw secret.

Verification loads by public selector, validates the exact purpose, recomputes the hash, and compares it using `CryptographicOperations.FixedTimeEquals`.

The raw token is never stored, logged, audited, or published.

## DEC-AUTH-0030 — Token-delivery boundary

Milestone 2 does not implement email delivery or public token APIs.

Issuing commands may return the raw invitation or reset token exactly once through an explicitly sensitive internal result. That result:

- must not be serialized by an HTTP endpoint;
- must not appear in logs, telemetry, exceptions, or domain events;
- is intended for a later notification-delivery adapter;
- must be structurally distinguishable from ordinary DTOs.

Actual email delivery belongs to a later notification-integration milestone.

## DEC-AUTH-0031 — Failed-login concurrency

Failed-attempt updates use optimistic concurrency with a bounded retry.

- Reread and reapply the failed attempt after a rowversion conflict.
- Attempt no more than three retries.
- Never convert a persistence conflict into authentication success.
- After retry exhaustion, return the same generic authentication failure.
- Emit only safe internal diagnostics.
- Endpoint rate limiting remains an additional later protection.

SQL Server tests must cover concurrent failed attempts and the transition into lockout.

## DEC-AUTH-0032 — Trusted tenant eligibility source

FP-003 is the authoritative source for tenant authentication eligibility. Milestone 3 consumes `ITenantAuthenticationEligibilityReadService` during automatic tenant resolution, explicit tenant selection, authentication-session creation, and refresh-token rotation.

Only `TenantStatus.Active` is authentication-eligible. `Provisioning`, `Suspended`, `Archived`, and missing tenants are ineligible. Membership status alone is insufficient, and callers cannot supply or override eligibility. Subscription, billing, company, and role state are not tenant lifecycle state. Ordinary tenant repositories do not bypass tenant filters for this purpose.

This decision resolves the previous Milestone 3 tenant-lifecycle blocker.

## DEC-AUTH-0033 — Verified Identity capability

Successful credential verification returns an internal, non-user-constructible `VerifiedIdentity` capability containing only:

- `IdentityId`;
- `SecurityVersion`.

`BeginTenantAccessCommand` accepts `VerifiedIdentity`, not a raw IdentityId. Ordinary callers cannot create the capability from an arbitrary identifier. It remains internal to the authentication Application boundary, is not persisted, and is never serialized through an HTTP endpoint. It contains no password, email, tenant, role, permission, token, or claims. The current account security version is revalidated before session creation.

`CredentialVerificationResult` is updated to expose this capability rather than a caller-reusable raw IdentityId.

## DEC-AUTH-0034 — V1 client identifier

The exact V1 browser client identifier is:

```text
ssas-erp-web
```

Comparison is exact, ordinal, and case-sensitive. Client identifiers have a maximum length of 64 characters; surrounding whitespace is invalid rather than trimmed. Values are stored with binary SQL collation and validated against a deployment-owned allowlist. The production allowlist contains `ssas-erp-web` for the V1 Angular client, and arbitrary caller-supplied values are rejected.

`ClientId` is immutable on tenant-selection transactions, sessions, and refresh-token records. Every selection and refresh validates exact equality. Native mobile and desktop identifiers remain deferred and require separate approval.

## DEC-AUTH-0035 — Session status model

Persist exactly these `AuthenticationSession` statuses:

- `Active`;
- `Revoked`;
- `Compromised`.

Permitted transitions are `Active` to `Revoked` and `Active` to `Compromised`. `Revoked` and `Compromised` are terminal.

Expiration is computed rather than persisted as another status. A session is currently usable only when its status is `Active`, current trusted UTC is earlier than `IdleExpiresUtc`, and current trusted UTC is earlier than `AbsoluteExpiresUtc`.

Approved Milestone 3 revocation reasons are exactly:

- `SessionLimitExceeded`;
- `PasswordReset`;
- `SecurityStateChanged`;
- `IdentityIneligible`;
- `MembershipIneligible`;
- `TenantIneligible`;
- `Administrative`.

Compromise uses status `Compromised`. Malformed tokens and ClientId mismatch do not revoke an otherwise valid session. Revocation history is retained and physical deletion is prohibited.

## DEC-AUTH-0036 — Authentication session binding

Every `AuthenticationSession` is immutably bound to exactly one `IdentityId`, `TenantUserId`, `TenantId`, `ClientId`, and `TokenFamilyId`.

The TenantUser must belong to both the Identity and Tenant, the membership must be `Active`, and the Tenant must be authentication-eligible through FP-003. An arbitrary client-supplied TenantId cannot grant access.

Sessions are global authentication records. They do not implement `ITenantOwnedEntity` and receive no ordinary tenant query filter. Isolation is enforced through narrow repositories, exact identity and membership predicates, composite foreign keys, and ClientId binding.

## DEC-AUTH-0037 — Membership discovery

Use the dedicated pre-tenant read contract `IIdentityTenantMembershipReadService` with operations to:

- list eligible memberships for one trusted IdentityId;
- get one eligible membership by IdentityId, TenantUserId, and TenantId.

IdentityId is mandatory in every operation and SQL predicate. Results are safe immutable tenant-selection projections and include only `TenantUserStatus.Active`; `Pending` and `Deactivated` are excluded. Tenant eligibility is validated through FP-003.

The contract exposes neither `IQueryable` nor TenantUser aggregates, never returns another Identity's membership, and is not a generic cross-tenant bypass. Ordinary tenant repositories do not use `IgnoreQueryFilters` for discovery. A narrowly scoped Infrastructure implementation may bypass the ordinary tenant filter only inside this approved service.

## DEC-AUTH-0038 — Tenant selection transaction

`TenantSelectionTransaction` is a separate persisted aggregate containing:

- `TenantSelectionTransactionId`;
- `PublicId`;
- `IdentityId`;
- `ClientId`;
- `SecurityVersionAtAuthentication`;
- `SecretHash`;
- `CreatedUtc`;
- `ExpiresUtc`;
- `ConsumedUtc`;
- `RevokedUtc`;
- audit metadata;
- `RowVersion`.

It has a five-minute lifetime, is single-use and purpose-bound to tenant selection, and is bound to exact ClientId. SecurityVersion is revalidated before selection. The proof is not accepted by tenant business APIs. Successful selection consumes it; failed membership or tenant validation does not.

Replay, expiry, revocation, concurrent consumption, malformed proof, or ClientId mismatch returns one generic selection failure. Selection consumption and session creation occur in one transaction.

## DEC-AUTH-0039 — Tenant selection proof format

Use:

```text
<public-selector>.<secret>
```

The selector is a cryptographically random Guid in `N` format: exactly 32 characters and persisted as `UNIQUEIDENTIFIER`. The secret is exactly 32 cryptographically random bytes encoded with canonical Base64Url as exactly 43 characters. The presented proof is exactly 76 characters and contains exactly one separator. Null, whitespace, malformed, oversized, noncanonical, and multi-separator values are rejected before database lookup.

The stored hash is SHA-256, exactly 32 bytes, and persisted as `BINARY(32)`. Its canonical input is:

```text
UTF8(
  "SSAS.ERP.TenantSelectionTransaction.v1" + "\0" +
  publicId:N + "\0" +
  identityId using invariant decimal + "\0" +
  securityVersion using invariant decimal + "\0" +
  exact ClientId + "\0" +
  canonical Base64Url secret
)
```

Verification loads by selector, validates exact ClientId, recomputes the hash, compares it using `CryptographicOperations.FixedTimeEquals`, and validates expiry, status, SecurityVersion, and ownership.

Raw selection proofs are reveal-once sensitive results. They never appear in persistence, logs, telemetry, exceptions, events, ordinary DTOs, or command representations.

## DEC-AUTH-0040 — Refresh token format

Use:

```text
<public-selector>.<secret>
```

The selector is a cryptographically random Guid in `N` format: exactly 32 characters and persisted as `UNIQUEIDENTIFIER`. The secret is exactly 32 cryptographically random bytes encoded with canonical Base64Url as exactly 43 characters. The presented token is exactly 76 characters and contains exactly one separator. Null, whitespace, malformed, oversized, noncanonical, and multi-separator values are rejected before database lookup.

The stored hash is SHA-256, exactly 32 bytes, and persisted as `BINARY(32)`. Its canonical input is:

```text
UTF8(
  "SSAS.ERP.RefreshToken.v1" + "\0" +
  publicId:N + "\0" +
  authenticationSessionId using invariant decimal + "\0" +
  tokenFamilyId:N + "\0" +
  exact ClientId + "\0" +
  canonical Base64Url secret
)
```

Verification uses indexed selector lookup, exact ClientId validation, fixed-time hash comparison, exact session and family binding, and lifecycle and expiry validation.

Raw refresh tokens are reveal-once sensitive results. They never appear in persistence, logs, telemetry, exceptions, domain events, ordinary DTOs, or command debugger and `ToString` representations.

## DEC-AUTH-0041 — Refresh token ownership

`AuthenticationSession` is the aggregate root. `RefreshTokenRecord` is its child entity; no `IRefreshTokenRecordRepository` exists.

The session owns token rotation, token-family history, replacement linkage, reuse detection, active-descendant revocation, refresh timestamps, and compromise transitions. `IAuthenticationSessionRepository` may load the owning session by refresh-token selector through the approved locked persistence operation.

A RefreshTokenRecord may use an independent SQL rowversion as a concurrency backstop without becoming a separate aggregate.

## DEC-AUTH-0042 — Refresh lifetime and rotation

The approved default session idle lifetime is 30 days and absolute lifetime is 90 days. Both are configurable, and refresh never extends absolute expiration.

For every issued refresh token:

```text
ExpiresUtc = min(Session.IdleExpiresUtc, Session.AbsoluteExpiresUtc)
```

Successful refresh:

1. Validates account, membership, tenant, session, ClientId, SecurityVersion, token purpose, selector, secret, and expiry.
2. Consumes the submitted token.
3. Sets `LastRefreshedUtc` to captured trusted current UTC.
4. Sets the new idle expiration to `min(now + configured idle lifetime, AbsoluteExpiresUtc)`.
5. Creates exactly one replacement refresh token.
6. Links the predecessor to its replacement.
7. Commits all changes atomically.

Failed persistence rolls back token consumption and replacement creation. An ambiguous refresh persistence result is not automatically retried.

## DEC-AUTH-0043 — Refresh reuse

Verified reuse of a consumed refresh token returns generic refresh failure, marks the affected AuthenticationSession `Compromised`, records the triggering RefreshTokenRecord, revokes every unconsumed descendant in that session, and emits safe compromise and reuse-detected events. It does not revoke unrelated sessions.

A consumed ancestor remains reuse-detectable while its session history is retained, even after that token's original expiry. There is no reuse grace window.

Concurrent use of one refresh token permits at most one successful rotation. The losing verified use follows this reuse-compromise behavior.

## DEC-AUTH-0044 — Session limit

The configurable maximum is ten active sessions per Identity.

Enforcement opens one SQL Server transaction, locks the AuthenticationAccount row for the Identity, lists currently active and unexpired sessions, orders them by `CreatedUtc` and then `AuthenticationSessionId`, revokes enough oldest sessions to make room, creates the new session and its first refresh token, and commits once.

Old sessions use revocation reason `SessionLimitExceeded` and emit a safe event for a future notification consumer. Notification delivery is not included in Milestone 3. Platform-support sessions require a separate later policy.

## DEC-AUTH-0045 — Password reset session revocation

After AuthenticationSession exists, successful password-reset completion atomically:

- increments SecurityVersion;
- revokes every active session for the Identity using `PasswordReset`;
- consumes the reset token;
- replaces the password hash;
- clears lockout state;
- commits all account, reset-token, and session changes.

No public revoke-all command is required in Milestone 3. A failed reset commit neither revokes sessions nor consumes the reset token.

## DEC-AUTH-0046 — Concurrency and lock order

Use SQL Server transactions with narrowly scoped parameterized locked reads. The canonical lock order is:

1. AuthenticationAccount.
2. TenantSelectionTransaction, when applicable.
3. TenantUser membership.
4. Tenant.
5. AuthenticationSession.
6. RefreshTokenRecord.

Concurrent refresh and selection consumption permit at most one success. Session revocation racing with refresh is serialized. Membership and Tenant eligibility are revalidated under the transaction when racing with session creation or refresh. SecurityVersion changes serialize through the AuthenticationAccount lock. Absolute expiration uses one captured trusted UTC value after required locks. Replacement insertion failure rolls back predecessor consumption.

Rowversion and unique constraints remain backstops. Ambiguous refresh commits are not retried automatically, and persistence conflicts map to generic external failures without exposing SQL details. Raw SQL is allowed only for these approved parameterized lock-acquisition operations.

## DEC-AUTH-0047 — Session and selection events

Approve these safe events:

- `TenantSelectionRequired`;
- `TenantMembershipSelected`;
- `AuthenticationSessionCreated`;
- `AuthenticationSessionRefreshed`;
- `AuthenticationSessionRevoked`;
- `AuthenticationSessionCompromised`;
- `RefreshTokenReuseDetected`;
- `SessionLimitOldestSessionRevoked`.

Events may contain safe identifiers, timestamps, exact ClientId, and a lifecycle reason or outcome category. They contain no raw refresh token, raw tenant-selection proof, secret hash, login email, password data, JWT, claims collection, cookie, or HTTP context.

Correlation, request, actor, and trace metadata remain external dispatch metadata. Immutable security-audit persistence remains deferred and is a production-release blocker.

## DEC-AUTH-0048 — Approved HTTP routes and responses

Milestone 4 exposes exactly:

```http
POST /api/platform/auth/login
POST /api/platform/auth/select-tenant
POST /api/platform/auth/refresh
POST /api/platform/auth/logout
```

These routes supersede the former Draft `/api/auth/*` routes. Login accepts only `loginEmail` and `password`. Tenant selection accepts only `selectionProof`, `tenantId`, and positive `tenantUserId`. Refresh and logout have no JSON request body. `ClientId` is never caller-supplied; the server binds exact `ssas-erp-web`.

Successful authenticated login, selection, and refresh return 200; tenant-selection-required returns 200; logout returns 204. Approved Problem Details mappings are: malformed request 400 `request.invalid`; generic credential, no-membership, lockout, or disabled failure 401 `authentication.failed`; generic selection failure 401 `authentication.selection_failed`; generic refresh failure 401 `authentication.refresh_failed`; CSRF or Origin rejection 403 `authentication.request_rejected`; rate limiting 429 `rate_limit.exceeded`; and unavailable signing or persistence 503 `service.unavailable`. `NoEligibleMembership` uses the generic authentication 401. Internal causes are never disclosed.

## DEC-AUTH-0049 — Access-token claim model

JWT access tokens have a default 15-minute lifetime and contain exactly one occurrence of `iss`, `aud`, `sub`, `jti`, `iat`, `nbf`, `exp`, `identity_id`, `tenant_id`, `tenant_user_id`, `session_id`, `client_id`, and `security_version`. They contain zero or more `role` and `permission` claims.

- `sub` is immutable `Identity.Subject`, not `IdentityId`.
- `jti` is a cryptographically random Guid in `N` format.
- Numeric identifiers use invariant positive Int64 text; `tenant_id` uses canonical nonempty Guid format.
- `client_id` is exact `ssas-erp-web`.
- Role and permission values are exact, distinct, and ordinally sorted.
- Validation rejects duplicate critical claims and duplicate role or permission values.
- Issuance rejects rather than truncates oversized claims; the maximum compact encoded JWT size is 8192 bytes.

Permission claims are the primary business-authorization mechanism. Role claims support approved exact-role policies. Tokens contain no login or tenant email, user display name, TenantName, TenantStatus, CompanyId, subscription or billing information, password data, or complete security-state object.

## DEC-AUTH-0050 — Production RS256 key source

Use RS256 only. Production uses one deployment-mounted X.509 signing certificate containing an RSA private key plus retained public verification certificates during rollover. The private-key password is supplied through deployment secret configuration; ordinary appsettings contain only nonsecret paths, identifiers, and policy metadata. RSA keys are at least 2048 bits.

There is no symmetric fallback, hard-coded or generated production key, private key in source control, or raw key material in appsettings. Production startup fails when active key material is missing or invalid. Development may use an ephemeral process-local RSA key and must clearly report that restart invalidates development access tokens. Tests use deterministic test-only RSA keys. The provider is abstracted so a future HSM, KMS, or Azure Key Vault provider does not change Application code.

## DEC-AUTH-0051 — Key identifier and rotation

The key identifier is:

```text
kid = Base64Url(SHA-256(DER-encoded certificate bytes))
```

Exactly one signing key is active, while one or more enabled verification keys may coexist. Missing, unknown, disabled, or duplicate `kid` is rejected without trying unrelated keys. One immutable provider snapshot determines both signing key and `kid` for an issuance operation.

Deployment rotation distributes the new verification certificate to every instance, verifies recognition of old and new `kid` values, activates the new signing certificate, keeps the old verification certificate enabled for at least the 15-minute access-token lifetime plus the 30-second clock skew after the final old-key issuance, and retires it only after that overlap.

Startup validation rejects duplicate `kid`, unknown active `kid`, an active key without a private RSA key, an undersized key, an inactive or expired active certificate, an active certificate expiring before `now + token lifetime + clock skew`, and verification-key retirement metadata that ends overlap too early.

## DEC-AUTH-0052 — Strict JWT validation

JWT validation sets `RequireSignedTokens = true`, `RequireExpirationTime = true`, `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true`, `ValidateLifetime = true`, and `MapInboundClaims = false`; validates exact issuer, exact audience, signature, lifetime, and `nbf`; permits only RS256; requires a known enabled `kid`; and uses 30 seconds of clock skew. Unsigned, HMAC, algorithm-substitution, malformed, expired, and invalid-claim tokens are rejected.

Exact required claim cardinality and formatting are validated before trusted request context is constructed. The active authentication path contains no `Jwt:SigningKey`, `SymmetricSecurityKey`, HMAC fallback, or symmetric test fixture.

## DEC-AUTH-0053 — Refresh cookie

The refresh cookie is exactly `__Secure-ssas-refresh` with `Secure = true`, `HttpOnly = true`, `SameSite = Strict`, omitted Domain, Path `/api/platform/auth`, and `Max-Age` plus `Expires` equal to the current refresh-token expiry.

It is host-only, never returned in JSON, never available to JavaScript, replaced after every successful refresh, and cleared after logout or a generic terminal refresh failure. Creation, replacement, and deletion use identical name, path, Domain omission, Secure, HttpOnly, and SameSite attributes. Development uses HTTPS; local HTTP support never sets `Secure = false`.

## DEC-AUTH-0054 — CSRF and Data Protection

Use a signed session-and-refresh-bound double-submit design. The cookie is exact `__Secure-ssas-xsrf`; the header is exact `X-XSRF-TOKEN`. The cookie is Secure, JavaScript-readable (`HttpOnly = false`), SameSite Strict, host-only, and scoped to `/api/platform/auth`.

An ASP.NET Core Data Protection time-limited protector protects format version, refresh-token PublicId, AuthenticationSessionId, exact ClientId, a cryptographically random nonce, and expiry. Validation requires exact cookie/header equality, a valid unexpired signature, exact session and refresh-selector binding, and exact `ssas-erp-web`. CSRF state rotates with every successful refresh-token rotation and is cleared whenever the refresh cookie is cleared.

CSRF is required for refresh and logout. Login and tenant selection instead require JSON-only content, exact accepted Origin, restrictive CORS, and rate limiting.

Production uses a deployment-mounted shared persistent Data Protection key-ring encrypted with a deployment-provided X.509 certificate. The key ring, certificate password, and other secrets do not enter source control or ordinary appsettings. Production startup fails when the shared key ring or protection certificate is unavailable.

## DEC-AUTH-0055 — Rate limiting

All authentication limiters queue zero requests and return generic 429 `rate_limit.exceeded` with `Retry-After`.

- Login: 30 requests per minute per trusted client IP, plus 5 per 15 minutes per HMAC(normalized login email + trusted IP).
- Tenant selection: 10 requests during one five-minute proof lifetime per HMAC(selection PublicId + trusted IP).
- Refresh: 10 requests per minute per verified session partition plus trusted IP.
- Logout: 5 requests per minute per validated session partition plus trusted IP.

Partition keys and logs contain no raw email, proof, selector, refresh token, cookie, session secret, or HMAC partition value. HMAC partitioning uses a deployment secret. Development and tests may use ASP.NET Core process-local rate limiting. Production also requires an approved shared API gateway or distributed provider and explicit startup configuration declaring the upstream enforcement mode; no database rate-limit table is introduced.

## DEC-AUTH-0056 — CORS, Origin, and trusted proxies

Production CORS uses explicit exact HTTPS origins. Wildcards, wildcard-with-credentials, and origins containing paths, query strings, or fragments are invalid. Credentials are allowed only for configured origins, and comparison is exact by scheme, host, and port. Login, tenant selection, refresh, and logout validate Origin. Referer is only a controlled fallback when Origin is absent and the approved browser policy permits it.

Direct network mode disables forwarded headers and uses the direct remote IP. Trusted-proxy mode enables forwarded headers only with explicit `KnownProxies` or `KnownNetworks`, uses `ForwardLimit = 1` unless separately approved, and accepts forwarded client IP only after trusted-proxy validation. Production startup fails for empty allowed origins, a non-exact-HTTPS or wildcard origin, or proxy mode without trusted proxies/networks. Environment origin and proxy values remain deployment configuration, not committed customer data.

## DEC-AUTH-0057 — Live tenant eligibility authorization

Every ordinary tenant-scoped authenticated business request performs centralized live tenant eligibility validation through the FP-003 contract. Only `Active` authorizes ordinary tenant business access; `Provisioning`, `Suspended`, `Archived`, and missing tenants are denied. There is one scoped tenant-eligibility lookup per tenant-scoped request, and tenant role and permission policies include this prerequisite.

Logout uses a separate authentication policy so a suspended-tenant session can still be revoked. Eligibility is never inferred from token claims alone and TenantStatus is not added to the JWT. Account disablement, session revocation, membership deactivation, role removal, and permission removal may remain stale for at most the 15-minute access-token lifetime unless a future high-risk policy requires a live check.

## DEC-AUTH-0058 — Access-token issuance and transaction order

Access-token issuance completes before the SQL transaction commits. Session creation and refresh open the existing Application transaction, apply Domain changes, persist or flush inside the still-open transaction when identity keys are required, build the trusted claims projection, issue the RS256 token, roll back on issuance failure, commit only after issuance succeeds, dispatch success events only after final commit, and write HTTP cookies only after database commit.

No access token represents rolled-back session or refresh state, no event is dispatched after issuance rollback, and no cookie is written before commit. Cookie delivery cannot be atomic with SQL commit; post-commit cookie failure is an accepted transport ambiguity. A retry with the old refresh token may trigger the approved reuse-compromise behavior. There is no refresh grace window and ambiguous refresh results are not automatically retried.

`IAccessTokenIssuer` is a framework-neutral Application abstraction. Its RS256/JWT implementation remains in Host authentication infrastructure.

## DEC-AUTH-0059 — User logout

Add the revocation reason `UserLogout` and implement `RevokeCurrentAuthenticationSessionCommand`. The request has no SessionId; session identity comes only from validated access-token claims and trusted current-session context.

The command locks AuthenticationAccount before AuthenticationSession, verifies Identity, Tenant, TenantUser, ClientId, session, and SecurityVersion binding, revokes only the current session with `UserLogout`, and is outwardly idempotent when the current session is absent or terminal. It clears refresh and CSRF cookies and returns 204. Logout-all is not implemented.

The approved constraint-only migration `AddUserLogoutSessionRevocationReason` changes only the AuthenticationSessions revocation-reason constraint. It adds no table.

## DEC-AUTH-0060 — Tenant selection summary

The selection summary contains exactly `TenantId`, `TenantUserId`, and `TenantDisplayName`. `TenantDisplayName` comes from FP-003 `Tenant.TenantName` and is returned only for an eligible Active membership belonging to the verified Identity.

It contains no membership display name as tenant label, TenantCode, legal name, billing or subscription details, role or permission data, or user email. The reveal-once selection proof is returned in JSON and is never stored in a cookie.

## DEC-AUTH-0061 — Sensitive HTTP responses

Every authentication response includes `Cache-Control: no-store`, `Pragma: no-cache`, `Referrer-Policy: no-referrer`, and `X-Content-Type-Options: nosniff`.

Request and response bodies, authentication request/command/sensitive-result objects, Authorization headers, cookie values, tokens, and proofs are never logged. Model-state output never echoes a password or selection proof, and exception details contain no token or proof. OpenAPI contains no realistic token-shaped example. Authentication responses are excluded from response compression if compression is introduced.

## DEC-AUTH-0062 — OpenAPI contract

OpenAPI exposes all four authentication routes and documents HTTP Bearer JWT security, refresh-cookie behavior through descriptions rather than response-schema values, `X-XSRF-TOKEN` for refresh and logout, authenticated and tenant-selection-required schemas, approved Problem Details statuses and codes, and logout as Bearer-authenticated.

Login, tenant selection, and refresh are anonymous to ASP.NET authentication; refresh still requires its cookie, Origin, CSRF, and rate-limit validation. OpenAPI exposes no refresh token, CSRF secret, internal command, sensitive Application wrapper, real token example, or private signing information.

## DEC-AUTH-0063 — Milestone 4 persistence boundary

Milestone 4 adds no table for signing keys, access tokens, CSRF, rate limiting, OpenAPI, CORS, or proxy configuration. Signing keys remain deployment/provider-owned. CSRF state is cryptographically protected and transported through the approved cookie/header design. Development/Test rate counters are process-local; production requires external shared enforcement.

The only approved migration is `AddUserLogoutSessionRevocationReason`, which changes only the existing AuthenticationSessions revocation-reason check constraint to include `UserLogout`.

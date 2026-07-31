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

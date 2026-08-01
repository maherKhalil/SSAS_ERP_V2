---
document_id: FP-002-DOM
title: Authentication Domain Model
status: Approved for Implementation
version: 1.0
---

# Domain Model

## AuthenticationAccount aggregate

Approved one-to-one aggregate for the FP-001 Identity:

- IdentityId
- global login identifier and normalized value
- password hash
- account status
- failed-attempt count and lockout end
- security version
- password-changed UTC
- audit metadata and rowversion

It owns credential and lockout behavior but not tenant memberships, roles, or permissions.

Approved status values are `PendingSetup`, `Active`, and `Disabled`. Temporary lockout is represented only by failed-attempt count and lockout end.

The account's display login email is trimmed and casing-preserving. Its globally unique normalized value is `Trim().ToUpperInvariant()`. It is independent from tenant-specific `TenantUser.Email`.

The related password-based Identity uses an immutable, server-generated, exact `local:{guid}` subject. Login email is never used as the subject.

## AuthenticationSession aggregate

Represents one independently revocable global authentication session, does not implement `ITenantOwnedEntity`, and receives no ordinary tenant query filter.

- `AuthenticationSessionId`, `IdentityId`, `TenantUserId`, `TenantId`;
- exact immutable `ClientId`;
- immutable `TokenFamilyId`;
- `SecurityVersionAtCreation`;
- status, created and last-refreshed UTC;
- idle and absolute expiry UTC;
- revoked UTC/by/reason and compromise metadata;
- audit metadata and rowversion.

Status is exactly `Active`, `Revoked`, or `Compromised`. The only transitions are Active to Revoked and Active to Compromised; both terminal states retain history. Expiration is computed from status plus idle and absolute timestamps rather than persisted as another status.

Approved revocation reasons are exactly `SessionLimitExceeded`, `PasswordReset`, `SecurityStateChanged`, `IdentityIneligible`, `MembershipIneligible`, `TenantIneligible`, `Administrative`, and `UserLogout`.

The aggregate owns `RefreshTokenRecord` children. It controls rotation, family history, predecessor/replacement linkage, verified-reuse handling, active-descendant revocation, refresh timestamps, and compromise. A child may have its own rowversion concurrency backstop without becoming an aggregate.

Every session is immutably bound to one Identity, active TenantUser membership, FP-003-eligible Tenant, exact ClientId, and token family.

## AccountActionToken aggregate

Purpose-bound single-use records for invitation and password reset:

- identity and optional TenantUser reference
- purpose
- token hash
- created, expiry, consumed, and revoked UTC
- audit metadata and rowversion

The aggregate stores a cryptographically random public selector and a fixed 32-byte SHA-256 secret hash. It never stores the raw token. Its exact purpose and ownership are immutable.

An invitation references one intended pending tenant membership but remains a global authentication record and is not tenant query-filtered.

Invitation records do not contain role identifiers. Roles are assigned only after membership activation.

## TenantSelectionTransaction

Separate aggregate proving successful primary authentication when several eligible memberships exist:

- `TenantSelectionTransactionId` and unique Guid `PublicId`;
- `IdentityId` and `SecurityVersionAtAuthentication`;
- exact immutable `ClientId`;
- fixed 32-byte `SecretHash`;
- created, five-minute expiry, consumed, and revoked UTC;
- audit metadata and rowversion.

It is persisted, purpose-bound, single-use, replay-safe, and never accepted by business APIs. Failed membership or Tenant validation does not consume it. Successful selection consumes it in the same transaction that creates the AuthenticationSession and first refresh token.

## VerifiedIdentity capability

Credential verification returns an internal, non-user-constructible `VerifiedIdentity` containing only `IdentityId` and `SecurityVersion`. It is not an entity, is not persisted or serialized, and contains no credential, email, Tenant, membership, authorization, token, or claims data.

`CredentialVerificationResult` exposes this capability, and `BeginTenantAccessCommand` accepts it instead of a raw IdentityId. The current AuthenticationAccount SecurityVersion is revalidated before session creation.

## Pre-tenant membership discovery

`IIdentityTenantMembershipReadService` lists eligible memberships for a trusted IdentityId and gets one eligible membership using IdentityId, TenantUserId, and TenantId together. It returns immutable safe projections, includes only active memberships owned by that Identity, and validates each Tenant through FP-003's `ITenantAuthenticationEligibilityReadService`.

The dedicated Infrastructure implementation may narrowly bypass the ordinary tenant filter; ordinary tenant repositories may not. The contract exposes neither TenantUser aggregates nor `IQueryable` and is not a generic cross-tenant service.

## Repository contracts

`IAuthenticationAccountRepository`, `IAuthenticationSessionRepository`, `IAccountActionTokenRepository`, and `ITenantSelectionTransactionRepository`. `RefreshTokenRecord` has no repository because it is owned by AuthenticationSession. The dedicated membership-discovery contract is a read service rather than an aggregate repository.

Repositories expose no generic repository, `IQueryable`, authorization behavior, or ordinary tenant-filter bypass. `IAuthenticationSessionRepository` exposes no physical-delete operation and may load an owning session by refresh-token selector through the approved locked persistence operation.

## Sprint-01 Milestone 2 aggregates

Milestone 2 implements only `AuthenticationAccount` and `AccountActionToken`. It reuses the existing FP-001 `Identity` and `TenantUser` aggregates, `PlatformDbContext`, and `IPlatformUnitOfWork`.

`AuthenticationSession`, refresh-token children, and `TenantSelectionTransaction` are documented FP-002 aggregates but remain deferred from Milestone 2.

## Sprint-01 Milestone 3 model

Milestone 3 implements `VerifiedIdentity`, `AuthenticationSession` with RefreshTokenRecord children, `TenantSelectionTransaction`, dedicated pre-tenant membership discovery, and FP-003 Tenant eligibility integration. It uses the existing PlatformDbContext and IPlatformUnitOfWork.

It does not implement access-token/JWT issuance, HTTP endpoints, cookies, CSRF, Angular authentication, public logout or session administration, authenticated password change, notification delivery, immutable audit persistence, Platform-support authentication, MFA, or external identity providers.

## Sprint-01 Milestone 4 model

Milestone 4 adds no aggregate and no persisted access-token, signing-key, CSRF, CORS, proxy, OpenAPI, or rate-limit entity.

Application owns framework-neutral trusted projections and `IAccessTokenIssuer`. Session creation and refresh coordinate token issuance inside the existing transaction and commit only after issuance succeeds. `RevokeCurrentAuthenticationSessionCommand` obtains session identity from trusted validated current-session context, locks AuthenticationAccount before AuthenticationSession, verifies every immutable binding, and invokes the existing aggregate revocation transition with `UserLogout`.

The Host authentication infrastructure owns X.509/RS256 key access, JWT serialization and validation, cookies, Data Protection, Origin/CORS, trusted proxies, rate limiting, security response headers, and OpenAPI. These concerns do not enter Domain entities, events, or Application HTTP contracts.

Tenant-selection summaries are safe Application projections containing exactly TenantId, TenantUserId, and TenantDisplayName sourced from FP-003 TenantName for an eligible Active membership owned by the verified Identity. The selection proof remains a separate reveal-once sensitive result.

Every ordinary tenant authorization policy composes the existing role or permission requirement with one scoped live FP-003 eligibility result. This is an authorization service boundary, not aggregate behavior, and TenantStatus is not copied into AuthenticationSession or JWT state.

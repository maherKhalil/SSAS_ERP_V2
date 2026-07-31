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

Represents one independently revocable session:

- SessionId, IdentityId, TenantUserId, TenantId
- ClientId
- status, created/last-refreshed/idle-expiry/absolute-expiry
- revoked UTC/by/reason
- security version and rowversion

Child records preserve refresh-token rotation history using token hashes only.

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

Short-lived proof of successful primary authentication. It permits only membership discovery and tenant selection and is never accepted by business APIs.

## Repository contracts

`IAuthenticationAccountRepository`, `IAuthenticationSessionRepository`, and `IAccountActionTokenRepository`. No generic repository or exposed `IQueryable`.

## Sprint-01 Milestone 2 aggregates

Milestone 2 implements only `AuthenticationAccount` and `AccountActionToken`. It reuses the existing FP-001 `Identity` and `TenantUser` aggregates, `PlatformDbContext`, and `IPlatformUnitOfWork`.

`AuthenticationSession`, refresh-token children, and `TenantSelectionTransaction` are documented FP-002 aggregates but remain deferred from Milestone 2.

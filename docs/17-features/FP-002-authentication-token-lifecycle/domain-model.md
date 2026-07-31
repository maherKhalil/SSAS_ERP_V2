---
document_id: FP-002-DOM
title: Authentication Domain Model
status: Draft
version: 0.1
---

# Domain Model

## AuthenticationAccount aggregate

Proposed one-to-one aggregate for the FP-001 Identity:

- IdentityId
- global login identifier and normalized value
- password hash
- account status
- failed-attempt count and lockout end
- security version
- password-changed UTC
- audit metadata and rowversion

It owns credential and lockout behavior but not tenant memberships, roles, or permissions.

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

## TenantSelectionTransaction

Short-lived proof of successful primary authentication. It permits only membership discovery and tenant selection and is never accepted by business APIs.

## Proposed repositories

`IAuthenticationAccountRepository`, `IAuthenticationSessionRepository`, and `IAccountActionTokenRepository`. No generic repository or exposed `IQueryable`.

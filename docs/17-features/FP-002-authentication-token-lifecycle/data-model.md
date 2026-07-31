---
document_id: FP-002-DATA
title: Authentication Data Model
status: Approved for Implementation
version: 1.0
---

# Data Model

Approved `platform` tables:

## AuthenticationAccounts

One per Identity: normalized global login identifier, password hash, status, failed attempts, lockout end, security version, password changed UTC, audit fields, rowversion.

## AuthenticationSessions

Identity, TenantUser, TenantId, ClientId, status, created/refresh/idle/absolute times, revocation metadata, security version, rowversion.

## RefreshTokenRecords

Session, token family, unique token hash, created/expiry/consumed/revoked times, replacement record, client binding.

## AccountActionTokens

Identity, optional TenantUser, purpose, unique token hash, created/expiry/consumed/revoked times, requester metadata, rowversion.

## Constraints

- one AuthenticationAccount per Identity;
- globally unique normalized login identifier;
- unique exact token hashes;
- immutable purpose and ownership;
- restricted deletes;
- retained consumed/revoked history;
- no plaintext token columns;
- atomic rotation and SQL Server rowversion concurrency.

The migration must be tested on SQL Server, not inferred from SQLite.

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

Milestone 2 SQL conventions:

- `AuthenticationAccountId`: `BIGINT IDENTITY` primary key;
- `IdentityId`: required restricted foreign key with a global unique index;
- `LoginEmail`: `NVARCHAR(320)`, trimmed display value with preserved casing;
- `NormalizedLoginEmail`: `NVARCHAR(320)`, `Latin1_General_100_BIN2`, globally unique;
- `PasswordHash`: nullable only while status is `PendingSetup`;
- `Status`: `PendingSetup`, `Active`, or `Disabled`;
- `FailedAttemptCount` and nullable `LockoutEndUtc` represent temporary lockout;
- `SecurityVersion`, `PasswordChangedUtc`, audit fields, and SQL Server rowversion.

The account is global, has no `TenantId`, and receives no tenant query filter.

## AuthenticationSessions

Identity, TenantUser, TenantId, ClientId, status, created/refresh/idle/absolute times, revocation metadata, security version, rowversion.

## RefreshTokenRecords

Session, token family, unique token hash, created/expiry/consumed/revoked times, replacement record, client binding.

## AccountActionTokens

Identity, optional TenantUser, purpose, unique token hash, created/expiry/consumed/revoked times, requester metadata, rowversion.

Milestone 2 SQL conventions:

- `AccountActionTokenId`: `BIGINT IDENTITY` primary key;
- `PublicSelector`: cryptographically random `UNIQUEIDENTIFIER` with a unique index;
- `SecretHash`: fixed `BINARY(32)` SHA-256 value with a unique index;
- required Identity/account references;
- invitation-only tenant and pending-membership references with restricted referential integrity;
- exact purpose, expiry, consumption, revocation, requester, audit, and rowversion fields.

The raw token is not a column. No role identifier is stored. The record is global and is not tenant query-filtered even when an invitation contains a trusted target-membership reference.

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

## Sprint-01 Milestone 2 persistence boundary

Milestone 2 adds only:

- `platform.AuthenticationAccounts`;
- `platform.AccountActionTokens`.

They belong to the existing `PlatformDbContext`, `platform` schema, Platform connection, `platform.__EFMigrationsHistory`, and `IPlatformUnitOfWork`.

`AuthenticationSessions` and `RefreshTokenRecords` remain approved package tables but are not created by the Milestone 2 migration. Tenant-selection persistence, immutable audit tables, and platform-support authentication tables are also deferred.

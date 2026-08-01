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

- `AuthenticationSessionId`: `BIGINT IDENTITY` primary key;
- required `IdentityId`, `TenantUserId`, and `TenantId` with restricted identity, Tenant, and composite same-tenant membership relationships;
- immutable `ClientId`: `NVARCHAR(64)` with binary collation;
- immutable `TokenFamilyId`: `UNIQUEIDENTIFIER`;
- `SecurityVersionAtCreation`;
- `Status`: exact binary-collated `Active`, `Revoked`, or `Compromised`;
- `CreatedUtc`, nullable `LastRefreshedUtc`, `IdleExpiresUtc`, and `AbsoluteExpiresUtc`;
- nullable revocation and compromise metadata with an exact approved reason;
- audit metadata and SQL Server rowversion.

AuthenticationSession is a global authentication record, not `ITenantOwnedEntity`, and receives no ordinary tenant query filter or automatic TenantId assignment. Narrow repositories, exact predicates, and composite foreign keys enforce its tenant and membership binding.

## RefreshTokenRecords

- `RefreshTokenRecordId`: `BIGINT IDENTITY` child key;
- required restricted `AuthenticationSessionId` owner;
- unique `PublicId`: cryptographically random `UNIQUEIDENTIFIER` selector;
- `SecretHash`: exact SHA-256 `BINARY(32)` with no raw-token column;
- immutable `TokenFamilyId` and binary-collated `ClientId` copied from the owning session;
- `CreatedUtc`, `ExpiresUtc`, nullable `ConsumedUtc` and `RevokedUtc`;
- nullable restricted self-reference `ReplacedByRefreshTokenRecordId`;
- optional safe reuse/compromise metadata and SQL Server rowversion.

The canonical token is exactly 76 characters: 32-character Guid `N` selector, one separator, and 43-character canonical Base64Url secret representing exactly 32 random bytes. The exact domain-separated hash input is defined only by `DEC-AUTH-0040`.

RefreshTokenRecord is owned by AuthenticationSession and has no repository of its own. History is retained and physical deletion is prohibited.

## TenantSelectionTransactions

- `TenantSelectionTransactionId`: `BIGINT IDENTITY` primary key;
- unique `PublicId`: cryptographically random `UNIQUEIDENTIFIER` selector;
- required restricted `IdentityId`;
- immutable binary-collated `ClientId`: `NVARCHAR(64)`;
- `SecurityVersionAtAuthentication`;
- `SecretHash`: exact SHA-256 `BINARY(32)` with no raw-proof column;
- `CreatedUtc`, `ExpiresUtc`, nullable `ConsumedUtc` and `RevokedUtc`;
- audit metadata and SQL Server rowversion.

The canonical proof is exactly 76 characters: 32-character Guid `N` selector, one separator, and 43-character canonical Base64Url secret representing exactly 32 random bytes. The exact domain-separated hash input is defined only by `DEC-AUTH-0039`.

The transaction is global, is not tenant query-filtered, lasts five minutes, and is consumed atomically with successful session creation.

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
- exact binary-collated ClientId with maximum length 64 and deployment allowlist validation;
- exact status and revocation-reason check constraints;
- `IdleExpiresUtc <= AbsoluteExpiresUtc` and refresh expiry capped by both;
- unique selection and refresh selectors;
- token-family and active-session indexes;
- deterministic active-session ordering by IdentityId, CreatedUtc, and AuthenticationSessionId;
- restricted session, refresh-token, replacement, membership, Tenant, Identity, and selection relationships;
- retained session and refresh-token family history with no routine physical delete.

The migration must be tested on SQL Server, not inferred from SQLite.

## Sprint-01 Milestone 2 persistence boundary

Milestone 2 adds only:

- `platform.AuthenticationAccounts`;
- `platform.AccountActionTokens`.

They belong to the existing `PlatformDbContext`, `platform` schema, Platform connection, `platform.__EFMigrationsHistory`, and `IPlatformUnitOfWork`.

`AuthenticationSessions` and `RefreshTokenRecords` remain approved package tables but are not created by the Milestone 2 migration. Tenant-selection persistence, immutable audit tables, and platform-support authentication tables are also deferred.

## Sprint-01 Milestone 3 persistence boundary

Migration `AddAuthenticationSessionsAndTenantSelection` adds only:

- `platform.AuthenticationSessions`;
- `platform.RefreshTokenRecords`;
- `platform.TenantSelectionTransactions`.

They use the existing PlatformDbContext, `platform` schema, Platform SQL Server connection, `platform.__EFMigrationsHistory`, and IPlatformUnitOfWork. The migration upgrades Milestone 2 without inventing Tenant data, weakening same-tenant constraints, or introducing JWT, cookie, notification, audit-store, or Platform-support tables.

Parameterized raw SQL is approved only for the canonical lock acquisitions: AuthenticationAccount, selection transaction when applicable, membership, Tenant, session, and refresh token. Rowversion and unique constraints are concurrency backstops; transaction failures roll back selection consumption, session creation, token consumption, and replacement insertion together.

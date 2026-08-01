---
document_id: FP-003-DATA
title: Tenant Lifecycle Data Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Data Model

## Ownership

The existing Platform module owns the Tenant persistence model:

- `PlatformDbContext`;
- `platform` schema;
- Platform SQL Server connection;
- `platform.__EFMigrationsHistory`;
- Platform EF Core migrations;
- `IPlatformUnitOfWork`;
- aggregate-specific repository and read services.

No second Platform context, connection, migration-history table, or Unit of Work is introduced.

## Table

### `platform.Tenants`

| Column | SQL definition | Purpose |
|---|---|---|
| `TenantId` | `UNIQUEIDENTIFIER` primary key | Existing authoritative tenant identifier |
| `TenantCode` | `NVARCHAR(64)`, required | Trimmed display code with casing preserved |
| `NormalizedTenantCode` | `NVARCHAR(64)` with `Latin1_General_100_BIN2`, required | Exact globally unique `Trim().ToUpperInvariant()` lookup value |
| `TenantName` | `NVARCHAR(200)`, required | Trimmed mutable display name with casing preserved |
| `Status` | bounded `NVARCHAR`, binary collation, required | Provisioning, Active, Suspended, Archived |
| `StatusChangedUtc` | `DATETIMEOFFSET`, required | Trusted last lifecycle-change time |
| `StatusChangedBy` | bounded `NVARCHAR`, nullable only where approved system bootstrap applies | Trusted lifecycle actor |
| `StatusChangeReasonCode` | bounded `NVARCHAR`, required | Safe bounded lifecycle reason code |
| `CreatedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `CreatedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |
| `ModifiedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `ModifiedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |
| `RowVersion` | SQL Server `rowversion` | Optimistic concurrency |

There is no unrelated BIGINT identity key. The existing Guid `TenantId` is the primary key and is never reused.

`TenantCode` and `NormalizedTenantCode` have maximum length 64. `TenantName` has maximum length 200. `NormalizedTenantName` is not stored because TenantName is not globally unique. `LegalName` is deferred.

## Constraints and indexes

- Primary key on `TenantId`.
- Unique index on `NormalizedTenantCode` using `Latin1_General_100_BIN2`, matching the column collation.
- Check constraint limiting `Status` to the four approved values.
- Check constraint limiting `StatusChangeReasonCode` to `Created`, `ProvisioningCompleted`, `Administrative`, `Security`, `Compliance`, `Operational`, `CustomerClosure`, and `IssueResolved`.
- Check constraints preventing empty code or name after trimming where practical.
- Coherent lifecycle metadata constraints, including a required status-change timestamp.
- Rowversion concurrency token.
- Index on `(Status, TenantName, TenantId)` for bounded Platform listing, subject to measured query validation.
- Restricted foreign-key deletion behavior.
- No soft-delete flag and no physical-delete operation; `Archived` is the terminal retained state.

## Query filters

`Tenant` is a Platform-level aggregate and does not implement `ITenantOwnedEntity`. It receives no tenant query filter and no automatic TenantId assignment from `ICurrentTenant`.

Tenant-owned business records continue to implement tenant isolation and remain filtered by trusted current context. Reading `platform.Tenants` does not disable those filters.

## Existing TenantId references

The repository already stores Guid TenantId in `TenantUsers`, `Roles`, assignments, and invitation-bound `AccountActionTokens`, but no `platform.Tenants` principal currently exists.

The first implementation milestone does not retrofit foreign keys onto all existing tables. After reconciliation, a dedicated enforcement migration adds restricted Tenant foreign keys where TenantId is present while retaining all existing composite same-tenant constraints.

Existing enforcement candidates are:

- `TenantUsers`;
- `Roles`;
- tenant-user role assignments;
- role-permission assignments where TenantId is present;
- invitation-bound `AccountActionTokens`.

Every new table introduced after FP-003, including authentication sessions, tenant-authentication records, and future module roots, references `platform.Tenants(TenantId)` with restricted deletion behavior from its first migration.

## Migration compatibility

An upgrade may contain existing distinct TenantIds with no trustworthy tenant code, name, or lifecycle status. FP-003 therefore separates schema creation from environment-specific reconciliation and enforcement.

Required staged process:

1. `AddTenantLifecycle` creates the empty `platform.Tenants` table, approved columns, constraints, and indexes; it performs no legacy auto-backfill and adds no blanket retrofit foreign keys.
2. A validation query inventories distinct legacy TenantIds across the applicable Platform tables.
3. Operators prepare and review an environment-specific one-to-one mapping of `TenantId`, code, name, and status. No production data or environment-specific mapping is committed to the repository.
4. Reconciliation fails on every missing or duplicate mapping and never infers state, generates placeholder metadata, or silently marks a Tenant Active.
5. After successful reconciliation, a dedicated enforcement migration verifies complete coverage, fails if any orphan remains, and adds the approved restricted foreign keys while preserving composite same-tenant constraints.

An empty environment still receives the empty Tenant table from `AddTenantLifecycle`; later enforcement may proceed only after its zero-legacy-reference coverage check succeeds.

The deployment validation query for the currently known Platform tables is:

```sql
SELECT legacy.TenantId
FROM
(
    SELECT TenantId FROM platform.TenantUsers
    UNION
    SELECT TenantId FROM platform.Roles
    UNION
    SELECT TenantId FROM platform.TenantUserRoleAssignments
    UNION
    SELECT TenantId FROM platform.RolePermissionAssignments
    UNION
    SELECT TenantId FROM platform.AccountActionTokens WHERE TenantId IS NOT NULL
) AS legacy
ORDER BY legacy.TenantId;
```

The release check extends this union when another pre-FP-003 table stores TenantId, compares its result with the operator-approved mapping, and stops deployment for missing or duplicate mappings.

## First milestone migration

Migration name:

```text
AddTenantLifecycle
```

It must be tested against an empty database and against the Milestone 2 schema with representative preexisting TenantIds, proving that it neither fabricates Tenant rows nor prematurely adds legacy foreign keys.

No migration runs automatically at application startup.

## Prohibited tables

FP-003 creates no Subscription, Billing, Company, Branding, Configuration, Notification, AuthenticationSession, RefreshToken, or AuditStore table.

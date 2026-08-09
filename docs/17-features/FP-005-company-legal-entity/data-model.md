---
document_id: FP-005-DATA
title: Company / Legal Entity Data Model
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Data Model

> Approved for Implementation — model reflecting the approved human decisions. This document describes the target schema and the migration to be authored during implementation; no migration is created by this documentation package.

## Ownership

The existing Platform module owns the Company persistence model:

- `PlatformDbContext`;
- `platform` schema;
- Platform SQL Server connection;
- `platform.__EFMigrationsHistory`;
- Platform EF Core migrations;
- `IPlatformUnitOfWork`;
- an aggregate-specific repository and read service.

No second Platform context, connection, migration-history table, or Unit of Work is introduced.

## Table

### `platform.Companies`

| Column | SQL definition | Purpose |
|---|---|---|
| `CompanyId` | `UNIQUEIDENTIFIER` primary key | Server-generated authoritative company identifier (`ADR-013`) |
| `TenantId` | `UNIQUEIDENTIFIER`, required | Owning tenant; `ITenantOwnedEntity` |
| `CompanyCode` | `NVARCHAR(64)`, required | Trimmed display code with casing preserved |
| `NormalizedCompanyCode` | `NVARCHAR(64)` with `Latin1_General_100_BIN2`, required | Exact `Trim().ToUpperInvariant()` value, unique per tenant |
| `CompanyName` | `NVARCHAR(200)`, required | Trimmed mutable display name with casing preserved |
| `BaseCurrencyCode` | `CHAR(3)`, required | ISO-4217 base currency, uppercase, immutable |
| `Status` | bounded `NVARCHAR`, binary collation, required | `Active`, `Inactive`, `Archived` (created `Inactive`) |
| `StatusChangedUtc` | `DATETIMEOFFSET`, required | Trusted last lifecycle-change time |
| `StatusChangedBy` | bounded `NVARCHAR`, nullable only where approved system bootstrap applies | Trusted lifecycle actor |
| `StatusChangeReasonCode` | bounded `NVARCHAR`, required | Safe bounded lifecycle reason code |
| `CreatedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `CreatedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |
| `ModifiedUtc` | `DATETIMEOFFSET`, required | Audit metadata |
| `ModifiedBy` | bounded `NVARCHAR`, nullable only for approved system operation | Audit metadata |
| `RowVersion` | SQL Server `rowversion` | Optimistic concurrency |

The Guid `CompanyId` is the primary key and is never reused. There is no unrelated BIGINT identity key on this table. Because `Company` is a low-write root, a `Guid` clustered primary key is acceptable without a sequential-`Guid` mitigation, per `ADR-013`.

## Constraints and indexes

- Primary key on `CompanyId`.
- Required `TenantId` with a restricted foreign key to `platform.Tenants(TenantId)`, present from the first migration per `DEC-TEN-0014`.
- **Per-tenant** unique index on `(TenantId, NormalizedCompanyCode)` using `Latin1_General_100_BIN2`, matching the column collation. The same normalized code may exist in different tenants; it is unique only within a tenant. This binary-collation unique index is authoritative under concurrent creation.
- Check constraint limiting `Status` to `Active`, `Inactive`, and `Archived`.
- Check constraint limiting `StatusChangeReasonCode` to `Created`, `Administrative`, `Operational`, `Compliance`, `CustomerRequest`, and `IssueResolved`.
- Check constraints preventing an empty code or name after trimming where practical.
- Check constraint requiring `BaseCurrencyCode` to be three uppercase letters; full ISO-4217 membership is enforced in Domain/Application.
- Coherent lifecycle-metadata constraints, including a required status-change timestamp.
- Rowversion concurrency token.
- Index on `(TenantId, Status, CompanyName, CompanyId)` for bounded tenant-scoped listing, subject to measured query validation.
- Restricted foreign-key deletion behavior (the global `DeleteBehavior.Restrict` convention applies).
- No soft-delete flag and no physical-delete operation; `Archived` is the terminal retained state. A persistence guard rejects any attempt to physically delete a Company row, mirroring the existing Tenant deletion guard. (The global restrict behavior alone would only block deleting a company that has dependents; the dedicated guard makes "no physical delete" absolute even for a dependent-free company.)

## Company code normalization and uniqueness

- Normalization is exactly `Trim().ToUpperInvariant()`, with **no** Unicode NFC/NFD/NFKC/NFKD normalization.
- The 64-character length limit applies to the normalized value as well as the accepted input; a value whose normalized form exceeds 64 characters is rejected before persistence.
- SQL uniqueness is enforced on the stored `NormalizedCompanyCode` under `Latin1_General_100_BIN2` (exact binary comparison of the already-uppercased value). This is exactly the documented "unique within a tenant by normalized value" semantic; the database constraint is authoritative under races, and an application pre-check is an optimization, not the authority.
- Company codes may contain Unicode characters; they are not restricted to ASCII. Display casing is preserved separately in `CompanyCode`.

## Query filters and write guards

`Company` implements `ITenantOwnedEntity`. Through the existing `PersistenceDbContext` machinery it therefore automatically receives:

- the global tenant query filter (`CurrentTenantId == entity.TenantId`);
- server-side tenant assignment on insert (`AssignTenant`), which requires a trusted current tenant and rejects a mismatched `TenantId`;
- rejection of any post-creation `TenantId` change;
- audit-metadata stamping;
- restricted delete behavior.

FP-005 introduces **no** new company filtering or write-guard machinery. `Company` does not implement `ICompanyOwnedEntity`, and no company query filter or company write guard is added in Milestone 1 (`ADR-014`, `NFR-CMP-0308`).

## Migration

Proposed migration name:

```text
AddCompanyOrganization
```

The migration creates `platform.Companies`, its columns, the per-tenant unique index, check constraints, the listing index, the rowversion token, and the restricted foreign key to `platform.Tenants(TenantId)`. It is tested against an empty database and against the current Platform schema with representative preexisting tenants. It creates exactly one table, backfills no data, and adds no HR/GL foreign keys.

No migration runs automatically at application startup.

## Prohibited in Milestone 1

`platform.Companies` is the only table introduced. FP-005 creates no company-membership table, no fiscal-calendar table, no chart-of-accounts table, no numbering-sequence table, no additional-currency table, no branding or configuration table, no audit-store table, and no outbox table. It adds no column to any existing table and changes no existing identifier.

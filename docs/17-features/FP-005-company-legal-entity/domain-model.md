---
document_id: FP-005-DOM
title: Company / Legal Entity Domain Model
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Domain Model

> Approved for Implementation — model reflecting the approved human decisions.

## Bounded context

**Platform Company**

This bounded context owns the existence, stable identity, display identity, base-currency configuration, and lifecycle state of a Company within one Tenant. It does not own company-owned business data, fiscal calendars, chart of accounts, numbering sequences, additional currencies, functional-currency accounting semantics, user↔company assignment, or company scope resolution.

## Company aggregate

`Company` is the aggregate root:

```csharp
public sealed class Company : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity
```

Fields:

- `CompanyId: Guid` (aggregate identity, `Id`);
- `TenantId: Guid` (owning tenant; `ITenantOwnedEntity`);
- `CompanyCode`;
- `NormalizedCompanyCode`;
- `CompanyName`;
- `BaseCurrencyCode`;
- `Status`;
- `StatusChangedUtc`, `StatusChangedBy`;
- `StatusChangeReasonCode`;
- `CreatedUtc`, `CreatedBy` (`IAuditableEntity`);
- `ModifiedUtc`, `ModifiedBy` (`IAuditableEntity`);
- SQL Server `RowVersion`.

`Company` implements `ITenantOwnedEntity` and therefore inherits the existing global tenant query filter, the server-side `AssignTenant` write behavior, the post-creation `TenantId` immutability guard, restricted delete behavior, and audit-metadata stamping. `Company` does **not** implement `ICompanyOwnedEntity`; per `ADR-014` it is the company root and is scoped by tenant, not by company. The `ICompanyOwnedEntity` interface is not introduced in this milestone.

## Responsibilities

The aggregate:

- creates a Company in `Inactive`;
- preserves immutable `CompanyId`, `TenantId`, company code, and base currency;
- enforces the approved transition graph;
- records safe status-change metadata;
- raises safe lifecycle events;
- updates only the mutable display name through the approved profile operation;
- rejects stale writes through persistence rowversion;
- exposes no physical-delete behavior.

Per-tenant uniqueness of the normalized company code is a database-backed invariant coordinated by the Application layer and a per-tenant unique index.

## Value objects

### CompanyCode

- required;
- trimmed display value; nonempty after trim; no control characters; Unicode permitted (not ASCII-only);
- maximum length 64 characters, applied to both the accepted input and the normalized value;
- normalized using `Trim().ToUpperInvariant()` only, with no Unicode NFC/NFD/NFKC/NFKD normalization;
- exact ordinal comparison; SQL uniqueness enforced on `NormalizedCompanyCode` under `Latin1_General_100_BIN2`;
- unique within a tenant by normalized value;
- immutable after creation.

### CompanyName

- required;
- trimmed;
- display casing preserved;
- maximum length 200 characters;
- mutable only through the approved company profile update operation;
- not unique.

### BaseCurrencyCode

- required;
- a valid ISO-4217 alphabetic three-letter code;
- stored uppercase;
- captured as the company's Platform-owned base / default currency configuration;
- immutable in Milestone 1.

Validation of ISO-4217 membership is a Domain/Application concern backed by a static ISO-4217 code set. Platform owns the base-currency *configuration value* only; functional-currency accounting semantics are a future General Ledger concern and are out of scope here.

### CompanyStatusChangeReasonCode

This bounded domain value contains exactly `Created`, `Administrative`, `Operational`, `Compliance`, `CustomerRequest`, and `IssueResolved`. Creation records `Created`; every lifecycle transition records a non-`Created` code. Domain events carry only the code and never free-form reason text.

## Enumeration

`CompanyStatus` contains exactly:

- `Active`;
- `Inactive`;
- `Archived`.

A newly created Company is `Inactive`.

## Domain events

- `CompanyCreated`;
- `CompanyActivated`;
- `CompanyDeactivated`;
- `CompanyArchived`;
- `CompanyProfileUpdated`.

Events may contain `CompanyId`, `TenantId`, previous and new status (for lifecycle events), occurrence time, and the safe bounded reason code. `CompanyProfileUpdated` carries `CompanyId`, `TenantId`, and occurrence time only. Events carry **no** company name or other display text, no credentials, tokens, complete claims, secrets, or HTTP context. Correlation, request, actor, and trace metadata remain outside Domain and are attached by the existing dispatch infrastructure.

Events are dispatched only after successful persistence, through the existing post-commit dispatcher. No integration event and no outbox are introduced, and no requirement assumes durable event delivery.

## Repository contract

Per `ADR-010`, define one aggregate-specific `ICompanyRepository` in Platform Application and one implementation in Platform Infrastructure.

It may expose only domain-focused operations such as:

- get by `CompanyId` within the current tenant;
- test normalized company-code uniqueness within the current tenant;
- add Company.

It exposes no generic CRUD, no delete method, no `IQueryable`, no authorization behavior, and no transaction management.

## Read contract

Company reads use a bounded Application read service returning safe projections (`GetCompanyById`, `ListCompanies`). The read contract exposes no aggregate, no `IQueryable`, and no cross-tenant data; all reads execute within the trusted current tenant and rely on the existing tenant query filter.

## Ownership boundary note

Company is the first tenant-owned Platform aggregate that is also a business-facing root. It reuses the tenant machinery exactly as any other `ITenantOwnedEntity`. The separate `ICompanyOwnedEntity` interface and the company query filter / write guard are **not** part of this package and arrive with the first company-owned business record (`ADR-014`, `NFR-CMP-0308`).

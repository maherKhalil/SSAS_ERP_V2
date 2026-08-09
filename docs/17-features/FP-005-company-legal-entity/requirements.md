---
document_id: FP-005-REQ
title: Company / Legal Entity Requirements
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Requirements

> Approved for Implementation — requirements reflecting the approved human decisions.

## Business requirements

### BR-CMP-0001 — Authoritative tenant-owned company

Platform must own one authoritative Company record for every company, identified by a server-generated Guid `CompanyId`, always owned by exactly one Tenant through a required `TenantId`.

### BR-CMP-0002 — Company data-partition dimension

Company is a true data-partition root beneath the Tenant. Future company-owned business records are partitioned by `CompanyId` in addition to `TenantId`.

### BR-CMP-0003 — Independent company enablement

A company may be independently activated and deactivated without affecting other companies or the owning tenant (`REQ-PLT-0011`).

### BR-CMP-0004 — Historical preservation

Company records and their references must be retained. Physical company deletion is prohibited; `Archived` is the terminal retained state.

### BR-CMP-0005 — Tenant-scoped authorization plane

Company administration requires explicit Platform company permissions evaluated within the trusted current tenant. A company owned by another tenant is never disclosed.

### BR-CMP-0006 — Stable company identity

The Guid `CompanyId` and the immutable per-tenant company code identify the company throughout its lifetime.

### BR-CMP-0007 — Tenant isolation continuity

Introducing the Company aggregate reuses the existing shared-schema tenant read/write isolation rules and must not weaken isolation of tenant-owned data.

### BR-CMP-0008 — Security traceability

Every company lifecycle transition must be attributable to trusted actor and UTC metadata and must emit safe audit-ready event data.

### BR-CMP-0009 — Base currency configuration

Each company records a **base currency** as Platform-owned currency configuration, consumed later by General Ledger. Platform owns the base-currency *configuration value*; it does not own functional-currency *accounting semantics* (rates, revaluation, restatement), which a future GL feature defines. `REQ-PLT-0012` supports company currency configuration; that the base currency is required at creation and immutable in Milestone 1 are FP-005 design decisions (see `BRULE-CMP-0010`, `DEC-CMP-0009`), not direct `REQ-PLT-0012` wording.

## Functional requirements

### FR-CMP-0101 — Create company

Create a Company with a server-generated Guid `CompanyId`, the trusted current `TenantId`, an immutable normalized company code unique within the tenant, a required display name, a required ISO-4217 base currency, and initial `Inactive` status.

### FR-CMP-0102 — Get company

Return one safe Company view by `CompanyId` within the trusted current tenant, including its concurrency version. A company not owned by the current tenant is indistinguishable from a nonexistent company.

### FR-CMP-0103 — List companies

Return a bounded, deterministically ordered, paginated list of the current tenant's companies, with an optional `status` filter.

### FR-CMP-0104 — Update company profile

Update the mutable company display name using optimistic concurrency. `CompanyCode`, `BaseCurrencyCode`, `TenantId`, and `CompanyId` are immutable and are not updatable.

### FR-CMP-0105 — Activate company

Explicitly transition a company from `Inactive` to `Active` using optimistic concurrency and an explicit non-`Created` reason code. This is the operation that makes a newly created (`Inactive`) company available, and the operation that re-enables a previously deactivated company.

### FR-CMP-0106 — Deactivate company

Explicitly transition a company from `Active` to `Inactive` using optimistic concurrency and an explicit non-`Created` reason code.

### FR-CMP-0107 — Archive company

Explicitly transition an `Active` or `Inactive` company to terminal `Archived` status using optimistic concurrency and an explicit non-`Created` reason code, retaining history.

## Security requirements

### SEC-CMP-0201 — No writable tenant

No route, header, claim, query string, or request body may supply or override `TenantId`. Tenant ownership is derived only from the trusted current tenant context.

### SEC-CMP-0202 — Trusted state

No caller-supplied status or lifecycle Boolean may override persisted company lifecycle state.

### SEC-CMP-0203 — Platform authorization and cross-tenant opacity

Company operations require explicit Platform company authorization. A `CompanyId` belonging to another tenant must never reveal the company's existence; it returns the same not-found result as an unknown identifier.

### SEC-CMP-0204 — Isolation reuse

Company implements `ITenantOwnedEntity` and participates in the existing tenant query filter and write guard. Reading companies grants no cross-tenant or business-data bypass.

### SEC-CMP-0205 — Sensitive observability

Company commands, events, logs, telemetry, exceptions, and audit-ready values contain no credentials, tokens, complete claims collections, secrets, or HTTP context. Domain events carry bounded identifiers, status, reason code, and timestamps only — never display text such as the company name.

### SEC-CMP-0206 — No physical deletion

No command, endpoint, repository method, cascade, or routine database operation may physically delete a Company.

### SEC-CMP-0207 — Concurrency protection

Stale lifecycle or profile writes must fail without overwriting newer status, name, or metadata.

### SEC-CMP-0208 — Immutable identity and currency

`CompanyId`, `TenantId`, `CompanyCode` (and its normalized value), and `BaseCurrencyCode` are immutable after creation.

## Non-functional requirements

### NFR-CMP-0301 — Asynchronous operations

Persistence and external-I/O operations are asynchronous and accept cancellation tokens.

### NFR-CMP-0302 — Clean Architecture

Company Domain and Application code remain free of EF Core, SQL Server, ASP.NET Core, HTTP, and UI dependencies.

### NFR-CMP-0303 — Module isolation

Platform company code does not depend on HR, GL, or another module's implementation or database.

### NFR-CMP-0304 — SQL Server verification

Migration compatibility, Guid keys, per-tenant uniqueness, check constraints, restricted deletes, and rowversion behavior are tested against SQL Server.

### NFR-CMP-0305 — Query boundaries

Application contracts expose neither generic repositories nor `IQueryable`. Company reads return bounded safe projections.

### NFR-CMP-0306 — Quality gates

The full build, Domain/Application tests, SQL Server integration tests, and architecture tests pass with zero introduced warnings or errors.

### NFR-CMP-0307 — Audit-ready events

Lifecycle events are immutable, contain only safe domain values, and use the existing post-commit event-dispatch boundary. Immutable audit persistence remains a separate production-release dependency and is not implemented here.

### NFR-CMP-0308 — No premature ownership infrastructure

FP-005 Milestone 1 introduces no `ICompanyOwnedEntity` interface, no company query filter, no company write guard, and no current-company / scope-resolution persistence. Company-ownership machinery is deferred to the first company-owned business record (`ADR-014`).

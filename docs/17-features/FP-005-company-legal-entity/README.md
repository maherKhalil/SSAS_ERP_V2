---
document_id: FP-005
title: Platform Company / Legal Entity
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
depends_on:
  - ADR-005
  - ADR-010
  - ADR-013
  - ADR-014
  - FP-003
---

# Feature Package 005 — Platform Company / Legal Entity

> **Approved for Implementation.** This package reflects the approved human architecture decisions (HUMAN-001 … HUMAN-011) and has passed the final approval-review gate. The decisions in [`decisions-approved.md`](decisions-approved.md) are binding, and the governing ADR-013 and ADR-014 are Accepted.

## Purpose

FP-005 establishes the Tenant-owned **Company / legal-entity root** required by HR and General Ledger.

A Company represents an independently configured legal / business entity within one Tenant. It is the data-partition root beneath the Tenant, and it anchors the company's base currency configuration, and later the fiscal calendar, chart of accounts, numbering, and employees, of the business modules.

FP-005 delivers only the smallest Company core needed to unblock the first business module. It is not a generic organizational-hierarchy feature and does not introduce a separate Organization aggregate.

## Position in the platform hierarchy

```
Platform
  └── Tenant                         (FP-003, implemented)
        └── Company                  (FP-005, this package)
              └── Company-owned business data   (future: HR, GL)
```

Company reuses the existing Tenant isolation machinery: it implements `ITenantOwnedEntity` and therefore inherits the global tenant query filter, server-side tenant assignment, post-creation `TenantId` immutability, restricted deletes, and audit-metadata stamping already provided by `PersistenceDbContext`. See `ADR-014`.

## Scope (Milestone 1)

FP-005 Milestone 1 covers:

- the Platform-owned `Company` aggregate;
- `CompanyId` (Guid) and owning `TenantId` (Guid);
- `CompanyCode`, `CompanyName`, `BaseCurrencyCode` value objects;
- `Active`, `Inactive`, and terminal `Archived` lifecycle states with an explicit transition graph, where a company is **created `Inactive`** and made `Active` by an explicit activation;
- create, update-profile, activate, deactivate, and archive commands;
- get-by-id and bounded paginated list queries;
- a code-owned Platform company permission set;
- persistence in the existing Platform SQL Server boundary (`platform.Companies`) with a per-tenant unique company code;
- optimistic concurrency, safe domain events, history retention, and test requirements;
- the future Platform HTTP surface under `/api/platform/companies`.

## Explicit exclusions

FP-005 does not define: `ICompanyOwnedEntity` filter/write-guard machinery; company-context / scope-resolution persistence; user↔company assignment or company-scoped business authorization; per-company fiscal calendar, numbering sequences, or language settings; currency **management** or functional-currency accounting semantics beyond capturing one base currency; branding/themes; HR or GL entities or foreign keys; Angular UI; subscription/entitlement coupling; Row-Level Security; an immutable audit store; and an outbox / integration-event mechanism.

Physical Company deletion is prohibited. Archive is the terminal lifecycle operation.

## Documents

1. [`requirements.md`](requirements.md)
2. [`business-rules.md`](business-rules.md)
3. [`domain-model.md`](domain-model.md)
4. [`lifecycle-model.md`](lifecycle-model.md)
5. [`authorization-model.md`](authorization-model.md)
6. [`api-contracts.md`](api-contracts.md)
7. [`data-model.md`](data-model.md)
8. [`acceptance-criteria.md`](acceptance-criteria.md)
9. [`test-scenarios.md`](test-scenarios.md)
10. [`decisions-approved.md`](decisions-approved.md)
11. [`traceability-matrix.md`](traceability-matrix.md)

## Architecture constraints

- Multi-tenant modular monolith; Clean Architecture, DDD, and CQRS.
- Platform owns the aggregate, application contracts, persistence, and API.
- `Company` implements `ITenantOwnedEntity`; it does **not** implement `ICompanyOwnedEntity` (`ADR-014`).
- `CompanyId` is a `Guid` (`ADR-013`).
- Repositories are aggregate-specific; no generic repository and no `IQueryable` boundary (`ADR-010`).
- Domain and Application remain free of EF Core, SQL Server, ASP.NET Core, and HTTP.
- State persists through the existing `PlatformDbContext` and `IPlatformUnitOfWork`.
- Domain events contain no secrets and are dispatched only after successful persistence, using the existing post-commit dispatcher; no outbox is introduced.
- The HTTP surface adopts the shared Platform admin-transport conventions (ProblemDetails, the Platform rowversion convention, security headers, strict JSON, OpenAPI), which are established separately (see [`api-contracts.md`](api-contracts.md) and `HUMAN-011`); FP-005 does not embed FP-001/FP-003 transport work.

## Source requirements

FP-005 realizes `REQ-PLT-0010` (support multiple companies) and `REQ-PLT-0011` (independent company activation/deactivation). `REQ-PLT-0012` states a company maintains independent fiscal settings, currencies, language, and numbering; FP-005 realizes only the **base-currency configuration** portion of that requirement now. That a base currency is *required at creation* and *immutable in Milestone 1* are FP-005 design decisions (`DEC-CMP-0009`), not direct wording of `REQ-PLT-0012`. Fiscal calendar, additional currencies, language, and numbering sequences are acknowledged and deferred to later milestones.

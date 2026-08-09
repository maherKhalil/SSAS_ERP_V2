---
document_id: FP-005-AC
title: Company / Legal Entity Acceptance Criteria
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Acceptance Criteria

> Approved for Implementation — criteria reflecting the approved human decisions.

### AC-CMP-0001 — Inactive creation

Creating a valid Company generates a nonempty Guid `CompanyId`, adopts the trusted current `TenantId`, stores the normalized code, trimmed name, and ISO-4217 base currency, and begins in `Inactive`. A created company is not `Active` until explicitly activated.

### AC-CMP-0002 — Company code uniqueness within a tenant

Within one tenant, two codes with the same `Trim().ToUpperInvariant()` value cannot both be created, and the accepted code's trimmed display casing is preserved.

### AC-CMP-0003 — Company code not globally unique

Two different tenants may each create a company with the same normalized company code.

### AC-CMP-0004 — Company name is mutable and not unique

Two companies in one tenant may share a trimmed company name, and the profile update operation can change a company's display name.

### AC-CMP-0005 — Base currency required and immutable

Creation requires a valid ISO-4217 base currency; no operation in Milestone 1 changes a company's base currency after creation.

### AC-CMP-0006 — Reversible enablement from Inactive default

A company is created `Inactive`; it can be activated to `Active`, and an `Active` company can be deactivated back to `Inactive`. Activation is rejected for a company that is not `Inactive`, and deactivation is rejected for a company that is not `Active`.

### AC-CMP-0007 — Invalid transitions

Every transition not listed in the approved lifecycle matrix is rejected without changing state or publishing a committed transition event.

### AC-CMP-0008 — Archive is terminal

`Active` and `Inactive` companies may be archived, after which no further transition is possible.

### AC-CMP-0009 — No physical deletion

No Domain operation, command, repository method, API contract, or migration cascade physically deletes a Company.

### AC-CMP-0010 — Tenant read isolation

Get and bounded list queries return only the current tenant's companies. A `CompanyId` owned by another tenant returns the same not-found result as an unknown identifier and never reveals existence.

### AC-CMP-0011 — Tenant ownership immutability and write rejection

`TenantId` is assigned from the trusted current tenant at creation and cannot be changed afterward; an attempt to persist a company with a mismatched tenant context is rejected.

### AC-CMP-0012 — Platform authorization boundary

Company operations require the relevant `Platform.Companies.*` permission. Company read/manage/lifecycle authority is separable, and no operation grants access to another tenant.

### AC-CMP-0013 — No writable tenant

No route, body, header, claim, or query value can set or override `TenantId`.

### AC-CMP-0014 — Concurrency

A stale rowversion is rejected and cannot overwrite a newer company status, name, or lifecycle metadata.

### AC-CMP-0015 — Safe events

Every successful create, lifecycle change, and profile update raises the corresponding safe event after persistence; no event contains the company name or other display text, credentials, tokens, complete claims, secrets, or HTTP context.

### AC-CMP-0016 — No premature company-ownership machinery

Milestone 1 introduces no `ICompanyOwnedEntity` interface, no company query filter, no company write guard, and no current-company / scope-resolution persistence.

### AC-CMP-0017 — Immutable identity and currency

`CompanyId`, `TenantId`, `CompanyCode` (and its normalized value), and `BaseCurrencyCode` cannot be changed after creation through any command or endpoint.

### AC-CMP-0018 — Persistence ownership and isolation reuse

Company uses the existing Platform context, schema, connection, migration history, and Unit of Work. As an `ITenantOwnedEntity`, it inherits the existing tenant query filter, `AssignTenant` write behavior, post-creation `TenantId` immutability, and restricted deletes, without introducing new isolation machinery.

### AC-CMP-0019 — Focused milestone scope

Milestone 1 introduces no user↔company assignment, company scope resolution, fiscal calendar, additional currency, numbering, language settings, branding, HR/GL entity, Angular, RLS, immutable-audit store, or outbox implementation.

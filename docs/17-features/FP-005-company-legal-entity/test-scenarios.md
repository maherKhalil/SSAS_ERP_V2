---
document_id: FP-005-TEST
title: Company / Legal Entity Test Scenarios
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Test Scenarios

> Approved for Implementation — scenarios reflecting the approved human decisions.

## Domain

- **TS-CMP-0001:** Create a Company with a server-generated nonempty Guid `CompanyId` and `Inactive` status; a created company is never `Active` until activated.
- **TS-CMP-0002:** Trim the company code, preserve display casing, and derive exact `ToUpperInvariant()` normalization; reject empty or control-character codes; reject a value whose normalized form exceeds 64 characters.
- **TS-CMP-0003:** Trim and preserve `CompanyName` display casing without treating it as a unique identity.
- **TS-CMP-0004:** Accept a valid ISO-4217 base currency, store it uppercase, and reject an unknown or malformed currency code.
- **TS-CMP-0005:** Permit every listed lifecycle transition (Create→Inactive, Inactive→Active, Active→Inactive, Active→Archived, Inactive→Archived) and reject every unlisted or repeated transition without changing metadata.
- **TS-CMP-0006:** Make `Archived` terminal and preserve the aggregate for history.
- **TS-CMP-0007:** Reject any post-creation change to `CompanyId`, `TenantId`, `CompanyCode`, or `BaseCurrencyCode`.
- **TS-CMP-0008:** Raise `CompanyCreated`, `CompanyActivated`, `CompanyDeactivated`, `CompanyArchived`, and `CompanyProfileUpdated` with the exact bounded reason code, no free-form reason text, and no company name in the payload.
- **TS-CMP-0009:** Require an explicit non-`Created` reason code for activate, deactivate, and archive; record `Created` only at creation.

## Application

- **TS-CMP-0020:** Reject a duplicate normalized company code within one tenant while allowing the same normalized code in a different tenant.
- **TS-CMP-0021:** Update only the company display name; reject attempts to change code, currency, tenant, or status through the profile operation.
- **TS-CMP-0022:** Get one Company and list bounded, deterministically ordered safe projections with the optional status filter, all scoped to the current tenant.
- **TS-CMP-0023:** Coordinate create, update, activate, deactivate, and archive through one Platform Unit of Work each.
- **TS-CMP-0024:** Map a stale rowversion to a concurrency result and commit no transition or update event.
- **TS-CMP-0025:** Enforce `Platform.Companies.View`, `Platform.Companies.Manage`, and `Platform.Companies.Lifecycle` on the corresponding operations; deny operations lacking the permission.
- **TS-CMP-0026:** Prove caller-supplied status, tenant, or lifecycle Boolean cannot override persisted state or tenant ownership.
- **TS-CMP-0027:** Verify cancellation tokens flow through every persistence and read boundary.
- **TS-CMP-0028:** Two different raw company code inputs within one tenant that normalize (via `Trim().ToUpperInvariant()`) to the same `NormalizedCompanyCode` cannot both be created; under concurrent creation the SQL per-tenant unique index is authoritative and exactly one create succeeds while the other returns a deterministic conflict.

## SQL Server

- **TS-CMP-0040:** Apply the full Platform migration chain including `AddCompanyOrganization` to an empty SQL Server database.
- **TS-CMP-0041:** Enforce the `(TenantId, NormalizedCompanyCode)` per-tenant unique index with exact binary-collation behavior.
- **TS-CMP-0042:** Allow the same normalized company code in two different tenants.
- **TS-CMP-0043:** Enforce the three-value `Status` check constraint, the reason-code check constraint, the base-currency-shape check, and required code/name fields.
- **TS-CMP-0044:** Query companies only within the current tenant through the inherited tenant query filter; a company from another tenant is not returned.
- **TS-CMP-0045:** Reject a persisted company whose `TenantId` does not match the trusted current tenant context (`AssignTenant`), and reject a post-creation `TenantId` change.
- **TS-CMP-0046:** Reject a stale company update through SQL Server rowversion.
- **TS-CMP-0047:** Enforce restricted deletes and the deletion guard; retain an `Archived` company and its references; enforce the restricted foreign key to `platform.Tenants(TenantId)`.
- **TS-CMP-0048:** Preserve UTC creation, modification, and status-change metadata across transitions.

## API

- **TS-CMP-0060:** Require authentication and the correct `Platform.Companies.*` permission for every route; return 401 unauthenticated and 403 for a missing permission.
- **TS-CMP-0061:** Reject unknown fields and a writable `TenantId` (and writable `companyCode`/`baseCurrencyCode` on update) with `400 request.invalid`.
- **TS-CMP-0062:** Return `404 company.not_found` for a `CompanyId` owned by another tenant, identical to an unknown identifier.
- **TS-CMP-0063:** Enforce canonical padded RFC 4648 Base64 rowversion; map malformed to `400 platform.rowversion_invalid`, a valid stale value to `409 concurrency.conflict`, and a missing required value to `400 request.invalid`.
- **TS-CMP-0064:** Return `409 company.code_conflict` for a duplicate normalized code within the tenant, with exactly one success under concurrent creates.
- **TS-CMP-0065:** Return `409 company.transition_invalid` for a transition not permitted from the current status (for example activating an already-`Active` company); confirm a created company is `Inactive` and becomes `Active` only via the activate route.
- **TS-CMP-0066:** Confirm there is no `DELETE` route and no `reactivate` route; confirm activate, deactivate, and archive exist.
- **TS-CMP-0067:** Enforce list paging defaults and maxima and deterministic ordering; reject out-of-range paging with `400 request.invalid`.
- **TS-CMP-0068:** Confirm OpenAPI describes every schema, permission, success, and error response and matches runtime output.

## Architecture

- **TS-CMP-0080:** Keep Company Domain and Application free of EF Core, SQL Server, ASP.NET Core, HTTP, and UI dependencies.
- **TS-CMP-0081:** Define only aggregate-specific repositories; expose no generic repository, delete method, or `IQueryable` Application boundary.
- **TS-CMP-0082:** Expose no physical-delete command, repository method, or endpoint for Company.
- **TS-CMP-0083:** Scan Company events, commands, source, and logs for the company name, credentials, tokens, complete claims, secrets, or HTTP context and find none.
- **TS-CMP-0084:** Verify Company command and query handlers are asynchronous and accept cancellation tokens.
- **TS-CMP-0085:** Verify `Company` implements `ITenantOwnedEntity`.
- **TS-CMP-0086:** Verify `Company` does **not** implement `ICompanyOwnedEntity`, and that no `ICompanyOwnedEntity` interface, company query filter, or company write guard is introduced in this milestone.
- **TS-CMP-0087:** Verify the tenant-ownership classification: `Company` is listed among tenant-owned types and none of the existing tenant-wide records (Identity, TenantUser, Role, authentication, localization, `Tenant`) gains a `CompanyId`. This targeted classification check enumerates the approved tenant-owned and company-neutral sets rather than asserting a blanket "all aggregate roots are tenant-owned" rule, because `Tenant` and other platform-level roots are legitimate exceptions.
- **TS-CMP-0088:** Keep Platform company code independent from HR and GL implementations; verify the Platform Company Domain references no HR or GL Domain type (including any future archive-eligibility prerequisite).
- **TS-CMP-0089:** Verify no production startup path automatically applies Platform migrations.

## Milestone applicability

Milestone 1 implements `TS-CMP-0001` through `TS-CMP-0089` where the corresponding infrastructure exists. All API scenarios (`TS-CMP-0060` through `TS-CMP-0068`) are part of Milestone 1 because Company delivers its HTTP transport in this package. Archive-eligibility prerequisite checks for dependent modules (`BRULE-CMP-0018`, `DEC-CMP-0027`) are forward-looking and are not implemented or tested in Milestone 1 beyond confirming the Platform Company Domain references no HR/GL Domain type (`TS-CMP-0088`).

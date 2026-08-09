---
document_id: FP-005-API
title: Company / Legal Entity API Contracts
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# API Contracts

> Approved for Implementation — contracts reflecting the approved human decisions.

## Boundary and conventions

- Base route: `/api/platform/companies`.
- HTTPS, API versioning, and Problem Details apply.
- Every route requires authentication, a trusted current tenant, and the required `Platform.Companies.*` permission.
- No route, body, query, or header accepts a writable `TenantId`; the owning tenant is derived from the trusted current tenant context.
- Status is never accepted as a writable field on create or update; a company is created `Inactive` and made `Active` only through the activate route.
- Requests use strict JSON binding; unknown fields are rejected with `400 request.invalid`.
- Reads are bounded; the list uses page-based pagination with documented positive limits.
- Every mutating command carries the Platform-standard expected rowversion (see **Rowversion transport**).
- No physical `DELETE` route exists. No colon-style action route exists.
- Because enablement is a single reversible pair (`BRULE-CMP-0005`), there is an `activate` and a `deactivate` route and **no** separate `reactivate` route.
- The Company HTTP surface adopts the shared Platform admin-transport conventions — ProblemDetails, the Platform rowversion convention, response security headers, strict JSON, and OpenAPI. Those conventions are established/extracted separately (see `HUMAN-011`); FP-005 does not define a parallel convention and embeds no FP-001/FP-003 transport work.

## Approved routes

| Route | Method | Permission | Success | Numbered contract |
|---|---|---|---|---|
| `/api/platform/companies` | POST | `Platform.Companies.Manage` | 201 created | AC-CMP-0001 / TS-CMP-0060, TS-CMP-0064 |
| `/api/platform/companies` | GET | `Platform.Companies.View` | 200 bounded list | AC-CMP-0010 / TS-CMP-0067 |
| `/api/platform/companies/{companyId}` | GET | `Platform.Companies.View` | 200 company | AC-CMP-0010 / TS-CMP-0062 |
| `/api/platform/companies/{companyId}` | PUT | `Platform.Companies.Manage` | 200 updated | AC-CMP-0004 / TS-CMP-0061 |
| `/api/platform/companies/{companyId}/activate` | POST | `Platform.Companies.Lifecycle` | 200 | AC-CMP-0006 / TS-CMP-0065 |
| `/api/platform/companies/{companyId}/deactivate` | POST | `Platform.Companies.Lifecycle` | 200 | AC-CMP-0006 / TS-CMP-0065 |
| `/api/platform/companies/{companyId}/archive` | POST | `Platform.Companies.Lifecycle` | 200 | AC-CMP-0008 / TS-CMP-0066 |

Each numbered AC/TS verifies method/path, authentication, permission, trusted tenant derivation, exact schema and unknown-field rejection, success status/projection, error codes/statuses, concurrency where relevant, paging limits, cross-tenant not-found opacity, and OpenAPI conformance.

## Create company

```http
POST /api/platform/companies
```

Request:

```json
{
  "companyCode": "ACME-EG",
  "companyName": "Acme Egypt",
  "baseCurrencyCode": "EGP"
}
```

The result contains safe Company data and `Inactive` status, including `companyId` and the concurrency version. `TenantId` is server-assigned and is not part of the request. A normalized code already used within the tenant returns `409 company.code_conflict`. A newly created company is `Inactive` and must be activated through the activate route before it is available.

## Get company

```http
GET /api/platform/companies/{companyId}
```

Returns safe Company data and the concurrency version for a company owned by the current tenant. A `companyId` unknown to the current tenant returns `404 company.not_found`, indistinguishable from a company owned by another tenant.

## List companies

```http
GET /api/platform/companies?pageNumber=1&pageSize=50&status=Active
```

Returns the current tenant's companies as bounded safe projections with deterministic ordering (company name ascending, then `companyId`). Only the optional `status` filter is accepted in Milestone 1. Code/name search is not included. `pageSize` defaults to 50, minimum 1, maximum 200; `pageNumber` defaults to 1. Out-of-range paging values are `400 request.invalid`.

## Update company profile

```http
PUT /api/platform/companies/{companyId}
```

Request:

```json
{
  "companyName": "Acme Egypt LLC",
  "expectedRowVersion": "AAAAAAAAB9E="
}
```

Updates only the display name. `companyCode`, `baseCurrencyCode`, and any tenant field are not accepted and, if present, are rejected as unknown fields (`400 request.invalid`).

## Lifecycle commands

```http
POST /api/platform/companies/{companyId}/activate
POST /api/platform/companies/{companyId}/deactivate
POST /api/platform/companies/{companyId}/archive
```

Request body for each:

```json
{
  "reasonCode": "Administrative",
  "expectedRowVersion": "AAAAAAAAB9E="
}
```

`reasonCode` must be a non-`Created` value from `CompanyStatusChangeReason` (`Administrative`, `Operational`, `Compliance`, `CustomerRequest`, `IssueResolved`). Free-form reason text, secrets, and billing detail are not accepted. `activate` requires the company to be `Inactive`; `deactivate` requires it to be `Active`; `archive` requires it to be `Active` or `Inactive`. A transition not permitted from the company's current status returns `409 company.transition_invalid`.

## Rowversion transport

Company uses the Platform-wide rowversion transport convention documented in the API standards (`docs/08-Development/Development-Standards.md`, "Optimistic Concurrency (RowVersion) Transport"). It is not a Company-specific codec:

- Every `expectedRowVersion` and every exposed `rowVersion` uses canonical padded RFC 4648 Base64, compatible with .NET `System.Text.Json` byte-array serialization. Base64Url and hexadecimal are prohibited.
- A supplied value must be nonblank, have no surrounding whitespace, decode to exactly 8 bytes (the SQL rowversion length), and re-encode byte-for-byte to the submitted representation. Server output is always canonical.
- A malformed, blank, wrong-length, Base64Url, hexadecimal, or noncanonical expected rowversion returns `400 platform.rowversion_invalid`.
- A missing expected rowversion where one is required returns `400 request.invalid`.
- Only a valid stale rowversion maps to `409 concurrency.conflict`.

Implementation prerequisite: the shared Platform rowversion codec must be extracted into a neutral shared Platform/Host transport component before the Company API is implemented; Company must not depend on the localization-owned `LocalizationRowVersionCodec` (`HUMAN-005`, `DEC-CMP-0020`).

## ProblemDetails and status codes

All errors contain authoritative `code`, `status`, `type`, and `correlationId`. Mapping:

| Condition | Status | Code |
|---|---|---|
| Malformed / unknown fields, bad paging, unsupported status filter | 400 | `request.invalid` |
| Malformed rowversion | 400 | `platform.rowversion_invalid` |
| Unauthenticated | 401 | (authentication challenge) |
| Missing permission or tenant-context policy denial | 403 | `authorization.forbidden` |
| Unknown or cross-tenant company | 404 | `company.not_found` |
| Duplicate normalized company code within tenant | 409 | `company.code_conflict` |
| Transition not permitted from current status | 409 | `company.transition_invalid` |
| Stale rowversion | 409 | `concurrency.conflict` |

`Persistence.ConcurrencyConflict` remains internal and maps at this HTTP boundary to `409 concurrency.conflict`. Company code uniqueness is ultimately enforced by the SQL per-tenant unique index; concurrent creates of the same normalized code within a tenant yield exactly one success and one deterministic `409 company.code_conflict`.

## Security headers

Responses set `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`, and the Platform-standard security headers already applied by the existing Platform transports. No response body includes secrets, tokens, or cross-tenant data.

## OpenAPI

The OpenAPI document for the Company HTTP surface, delivered with these routes in this milestone, specifies all schemas, the strict unknown-field policy, enums, length limits, rowversion encoding, paging maxima, examples, permission/security requirements, and every success and error response and code. Contract tests compare runtime output to the document.

## Exclusions

No API contract is defined here for user↔company assignment, company scope resolution, fiscal calendar, additional currencies, numbering sequences, language settings, branding, HR, GL, or Angular. No `DELETE` and no `reactivate` route exists.

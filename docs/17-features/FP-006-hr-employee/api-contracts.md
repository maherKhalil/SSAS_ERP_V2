---
document_id: FP-006-API
title: HR Employee API Contracts
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# API Contracts

> Approved for Implementation — contracts reflecting the settled FP-006A decisions.

## Boundary and conventions

- Base route: `/api/hr/employees`.
- HTTPS, API versioning, and Problem Details apply.
- Every route requires authentication, a trusted current tenant, a trusted company context, a trusted branch execution context where the operation is branch-owned, and the required `HR.Employees.*` permission.
- No route, body, query, or header accepts a writable `TenantId`.
- **No route, body, or header accepts a writable `BranchId`** except the transfer route's destination, which is a business argument authorized server-side (`SEC-EMP-0203`, `SEC-EMP-0212`).
- `CompanyId` is accepted only as the **company context selection** described below, never as a writable ownership field on an entity.
- `Status` is never accepted as a writable field on create or update; lifecycle changes go through their own routes.
- Requests use strict JSON binding; unknown fields are rejected with `400 request.invalid`.
- Reads are bounded; search uses page-based pagination with documented positive limits.
- Every mutating command carries the Platform-standard expected rowversion (see **Rowversion transport**).
- No physical `DELETE` route exists. No colon-style action route exists.
- The Employee HTTP surface adopts the shared Platform admin-transport conventions — ProblemDetails, the Platform rowversion convention, response security headers, strict JSON, and OpenAPI. FP-006 defines no parallel convention.

## Company context selection

Every Employee route requires a selected company. The selection is transmitted as the request header:

```http
X-Company-Id: 6f1b7f4e-6f0e-4a6f-9a5d-0b8f2f0a1c33
```

It expresses **intent only** and is authorized server-side against live state before it is trusted, through the five-step validation in [`authorization-model.md`](authorization-model.md). It is never authorization proof, and it is never written onto an entity from the request (`SEC-EMP-0202`).

A header is used rather than a route segment because company is an ambient execution dimension shared by every HR route, exactly as tenant and branch are — not a resource in the Employee path. Routes therefore stay `/api/hr/employees/...` and never `/api/hr/companies/{companyId}/employees/...`.

A missing header returns `400 request.invalid`. A company that fails any validation step returns `403 company.scope_denied`, identical for nonexistent, wrong-tenant, inactive, and unauthorized identifiers, so existence is never disclosed.

Branch context is **not** transmitted by the client at all. It comes from the durable authenticated session (`ADR-023` decision 8). A request whose session has no selected branch, where the operation is branch-owned, returns `409 branch.selection_required`.

## Approved routes

| Route | Method | Permission | Success | Numbered contract |
|---|---|---|---|---|
| `/api/hr/employees` | POST | `HR.Employees.Create` | 201 created | AC-EMP-0001 / TS-EMP-0090 |
| `/api/hr/employees` | GET | `HR.Employees.View` | 200 bounded list | AC-EMP-0027 / TS-EMP-0096 |
| `/api/hr/employees/{employeeId}` | GET | `HR.Employees.View` | 200 employee | AC-EMP-0028 / TS-EMP-0092 |
| `/api/hr/employees/{employeeId}` | PUT | `HR.Employees.Update` | 200 updated | AC-EMP-0007 / TS-EMP-0091 |
| `/api/hr/employees/{employeeId}/activate` | POST | `HR.Employees.Update` | 200 | AC-EMP-0013 / TS-EMP-0094 |
| `/api/hr/employees/{employeeId}/deactivate` | POST | `HR.Employees.Update` | 200 | AC-EMP-0013 / TS-EMP-0094 |
| `/api/hr/employees/{employeeId}/terminate` | POST | `HR.Employees.Terminate` | 200 | AC-EMP-0014 / TS-EMP-0095 |
| `/api/hr/employees/{employeeId}/transfer` | POST | `HR.Employees.Transfer` | 200 | AC-EMP-0031 / TS-EMP-0093 |
| `/api/hr/employees/{employeeId}/branch-history` | GET | `HR.Employees.View` | 200 ordered history | AC-EMP-0035 / TS-EMP-0097 |

Each numbered AC/TS verifies method/path, authentication, permission, trusted tenant/company/branch derivation, exact schema and unknown-field rejection, success status/projection, error codes/statuses, concurrency where relevant, paging limits, cross-boundary not-found opacity, and OpenAPI conformance.

## Create employee

```http
POST /api/hr/employees
X-Company-Id: 6f1b7f4e-6f0e-4a6f-9a5d-0b8f2f0a1c33
```

Request:

```json
{
  "employeeNumber": "EMP-00147",
  "fullName": "Layla Haddad",
  "employmentDate": "2026-03-01T00:00:00+00:00",
  "nationalId": "2990112345678"
}
```

The result contains safe Employee data and `Active` status, including `employeeId`, the server-stamped `branchId`, the resolved `companyId`, the `department` sub-object described in [FP-007's contracts](../FP-007-hr-department/api-contracts.md) (`{ departmentId, code, name }`, shipped 2026-08-22 and present on the detail and on every search row), and the concurrency version.

`tenantId`, `companyId`, `branchId`, and `status` are **not** part of the request and are rejected as unknown fields if present (`400 request.invalid`). `nationalId` is optional.

> **AS-BUILT CORRECTION (2026-08-22, HR as-built cleanup).** The create contract shown above is **no longer
> complete**: later packages added two REQUIRED fields to this same request, and a caller sending exactly the
> four fields sampled here is refused today. `departmentId` became required in FP-007 (`BR-HR-0005`, and
> FP-007's own `api-contracts.md` records the change), and `positionId` in FP-008 (`BR-HR-0006`,
> `DEC-POS-0026`). Both are business arguments authorized server-side, not ownership fields, so the rule this
> section states — no writable `tenantId`, `companyId`, `branchId` or `status` — is unchanged. The sample is
> annotated rather than rewritten so the FP-006 contract and the two changes to it both stay readable.

A normalized employee number already used within the company returns `409 employee.number_conflict`. A normalized national ID already used within the company returns `409 employee.national_id_conflict`.

## Get employee

```http
GET /api/hr/employees/{employeeId}
X-Company-Id: ...
```

Returns safe Employee data and the concurrency version for an Employee within the caller's authorized company and branch scope. An `employeeId` outside that scope — unknown, another tenant's, another company's, or in an unauthorized branch — returns `404 employee.not_found`, indistinguishable in every case.

## Search employees

```http
GET /api/hr/employees?pageNumber=1&pageSize=50&status=Active&branchScope=CurrentBranch
X-Company-Id: ...
```

Returns bounded safe projections with deterministic ordering (full name ascending, then `employeeId`).

Query parameters:

| Parameter | Values | Default |
|---|---|---|
| `pageNumber` | ≥ 1 | 1 |
| `pageSize` | 1 … 200 | 50 |
| `status` | `Active`, `Inactive`, `Terminated` | omitted → `Active` and `Inactive` only |
| `branchScope` | `CurrentBranch`, `SelectedAuthorizedBranches`, `AllAuthorizedBranches` | `CurrentBranch` |
| `branchIds` | required when `branchScope=SelectedAuthorizedBranches`, otherwise rejected | — |
| `companyScope` | `CurrentCompany`, `AllAuthorizedCompanies` | `CurrentCompany` |
| `employeeNumber` | exact normalized match | omitted |

Search **defaults to excluding `Terminated`** employees; including them requires the explicit `status=Terminated` filter, so ordinary operational reads are not silently widened while audit reads remain possible.

`branchIds` values that are not a subset of the caller's authorized branch set return `403 branch.scope_denied`, identical for unauthorized, inactive, and nonexistent identifiers. An empty authorized branch set returns `403 branch.scope_denied` rather than an unfiltered result. The same applies to company scope with `403 company.scope_denied`.

`AllAuthorizedBranches` and `AllAuthorizedCompanies` **materialize** the authorized identifier sets into explicit predicates. Neither is ever implemented by omitting a predicate (`BRULE-EMP-0025`).

Out-of-range paging values are `400 request.invalid`.

## Update employee profile

```http
PUT /api/hr/employees/{employeeId}
X-Company-Id: ...
```

Request:

```json
{
  "fullName": "Layla Haddad-Nasr",
  "nationalId": "2990112345678",
  "expectedRowVersion": "AAAAAAAAB9E="
}
```

Updates only the mutable profile fields.

**`tenantId`, `companyId`, `branchId`, `employeeNumber`, and `status` are not accepted** and, if present, are rejected as unknown fields (`400 request.invalid`). `branchId` is absent from this contract by construction, not merely validated away — an ordinary update can never express a transfer (`BRULE-EMP-0015`).

Updating a `Terminated` Employee returns `409 employee.transition_invalid`.

## Lifecycle commands

```http
POST /api/hr/employees/{employeeId}/activate
POST /api/hr/employees/{employeeId}/deactivate
POST /api/hr/employees/{employeeId}/terminate
```

Request body for activate and deactivate:

```json
{
  "reasonCode": "Administrative",
  "expectedRowVersion": "AAAAAAAAB9E="
}
```

Request body for terminate:

```json
{
  "terminationDate": "2027-01-31T00:00:00+00:00",
  "reasonCode": "Resignation",
  "expectedRowVersion": "AAAAAAAAB9E="
}
```

`reasonCode` must be a non-`Created` value from `EmployeeStatusChangeReasonCode` (`Administrative`, `Operational`, `Compliance`, `Resignation`, `Dismissal`, `EndOfContract`). *(As built: the type is named `EmployeeStatusChangeReason`, without the `Code` suffix; the member set is exactly as listed.)*

`activate` requires the Employee to be `Inactive`; `deactivate` requires `Active`; `terminate` requires `Active` or `Inactive`. A transition not permitted from the current status returns `409 employee.transition_invalid`.

A `terminationDate` earlier than the Employee's `employmentDate` returns `400 request.invalid`.

## Transfer employee

```http
POST /api/hr/employees/{employeeId}/transfer
X-Company-Id: ...
```

Request:

```json
{
  "destinationBranchId": "b21c9f0a-4c3d-4d1e-9d2b-7a5e1f6c8d40",
  "reasonCode": "Reorganisation",
  "reasonText": "Consolidating the northern outlets",
  "expectedRowVersion": "AAAAAAAAB9E="
}
```

Transfer has its **own DTO**, distinct from update, and is the only contract in the package carrying a branch identifier.

`destinationBranchId` is a **business argument, not trusted execution context**. It is authorized server-side through `ITenantBranchAccessResolver` against live state, intersected with active branches, inside the transaction. It is never treated as an assertion of the caller's own scope, and it never becomes the caller's execution branch.

The **source** branch is not part of the request. It is the Employee's current `BranchId`, which must equal the caller's trusted branch execution context, except under the inactive-source recovery rule (`BRULE-EMP-0021`).

`reasonCode` must be a non-`InitialAssignment` value from `EmployeeBranchTransferReasonCode` (`Reorganisation`, `OperationalNeed`, `EmployeeRequest`, `BranchClosure`, `Correction`). *(As built: the type is named `EmployeeBranchTransferReason`, without the `Code` suffix; the member set is exactly as listed.)* `reasonText` is optional, limited to 512 characters, persisted for audit only, and never emitted in a domain event.

Responses:

| Condition | Status | Code |
|---|---|---|
| Destination equals source | 400 | `request.invalid` |
| Destination unauthorized, inactive, wrong tenant, or unknown | 403 | `branch.scope_denied` |
| Employee is `Terminated` | 409 | `employee.transition_invalid` |
| Stale rowversion | 409 | `concurrency.conflict` |
| Success | 200 | new `branchId` and concurrency version |

A successful transfer atomically updates `Employee.BranchId` and appends exactly one `EmployeeBranchAssignment` record.

## Get employee branch history

```http
GET /api/hr/employees/{employeeId}/branch-history
X-Company-Id: ...
```

Returns the Employee's immutable branch-assignment records ordered by `effectiveFromUtc` ascending, then by `id`, each carrying `sourceBranchId` (null on the initial record), `destinationBranchId`, `effectiveFromUtc`, `transferredBy`, and `reasonCode`. `reasonText` is included only for callers holding `HR.Employees.View`, which every caller of this route already holds.

The route is authorized by the Employee's **current** branch, not by the branches named inside the history (see [`authorization-model.md`](authorization-model.md)). The result is bounded by the same paging convention as search.

## Rowversion transport

Employee uses the Platform-wide rowversion transport convention documented in the API standards (`docs/08-Development/Development-Standards.md`, "Optimistic Concurrency (RowVersion) Transport"). It is not an Employee-specific codec:

- Every `expectedRowVersion` and every exposed `rowVersion` uses canonical padded RFC 4648 Base64. Base64Url and hexadecimal are prohibited.
- A supplied value must be nonblank, have no surrounding whitespace, decode to exactly 8 bytes, and re-encode byte-for-byte to the submitted representation. Server output is always canonical.
- A malformed, blank, wrong-length, Base64Url, hexadecimal, or noncanonical expected rowversion returns `400 platform.rowversion_invalid`.
- A missing expected rowversion where one is required returns `400 request.invalid`.
- Only a valid stale rowversion maps to `409 concurrency.conflict`.

`EmployeeBranchAssignment` carries no rowversion and is never updated, so no concurrency token is transported for it.

## ProblemDetails and status codes

All errors contain authoritative `code`, `status`, `type`, and `correlationId`. Mapping:

| Condition | Status | Code |
|---|---|---|
| Malformed / unknown fields, bad paging, invalid dates, missing company header, destination equals source | 400 | `request.invalid` |
| Malformed rowversion | 400 | `platform.rowversion_invalid` |
| Unauthenticated | 401 | (authentication challenge) |
| Missing functional permission | 403 | `authorization.forbidden` |
| Company unauthorized, inactive, wrong tenant, or unknown | 403 | `company.scope_denied` |
| Branch unauthorized, inactive, wrong tenant, or unknown; empty authorized set | 403 | `branch.scope_denied` |
| Unknown, cross-tenant, cross-company, or unauthorized-branch employee | 404 | `employee.not_found` |
| No branch selected in session for a branch-owned operation | 409 | `branch.selection_required` |
| Duplicate normalized employee number within the company | 409 | `employee.number_conflict` |
| Duplicate normalized national ID within the company | 409 | `employee.national_id_conflict` |
| Transition not permitted from current status | 409 | `employee.transition_invalid` |
| Stale rowversion | 409 | `concurrency.conflict` |

Internal persistence refusals from the branch and company write boundaries — a spoofed `BranchId`, an unsanctioned `BranchId` modification, a cross-branch or cross-company write — are **never** surfaced as their internal messages. They map to `403 branch.scope_denied` or `403 company.scope_denied`, disclosing no database topology, no table names, and no cross-tenant existence.

Employee number and national-ID uniqueness are ultimately enforced by the SQL per-company unique indexes; concurrent creates of the same normalized value within a company yield exactly one success and one deterministic `409`.

## Security headers

Responses set `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`, and the Platform-standard security headers already applied by the existing Platform transports. No response body includes secrets, tokens, or data outside the caller's authorized tenant, company, and branch scope.

## OpenAPI

The OpenAPI document for the Employee HTTP surface, delivered with these routes in this milestone, specifies all schemas, the strict unknown-field policy, enums, length limits, rowversion encoding, paging maxima, the company-context header, scope-mode parameters, examples, permission/security requirements, and every success and error response and code. Contract tests compare runtime output to the document.

> **WHAT IS ACTUALLY GENERATED TODAY (corrected 2026-08-22, HR as-built cleanup). The paragraph above
> describes a target, not the current state.** As built:
>
> * The Host **does** generate an OpenAPI document — `AddSwaggerGen`, served at `/swagger/v1/swagger.json` —
>   and every HR route appears in it through the framework's own route metadata, with its path, method and
>   inferred request shape.
> * The HR endpoints declare only `WithTags` and `WithName`. There is **no** `Produces<T>`, no
>   `ProducesProblem`, no examples and no declared security requirement, so the document does **not**
>   describe the response schemas, the error responses, the problem codes, the enums, the length limits, the
>   rowversion encoding, the paging maxima or the permission requirements this section claims for it. The
>   Platform authentication surface does declare them, which is what makes the difference visible as an
>   omission rather than a convention.
> * **No contract test compares HR runtime output to the document.** The only OpenAPI contract test in the
>   repository is `LocalizationOpenApiContractTests`, covering Platform localization.
>
> **Enriching the HR surface — the `Produces`/`ProducesProblem`/security metadata and a contract-test suite
> to hold it true — is registered as its own backlog task and was deliberately NOT started here.** It is
> feature work rather than as-built reconciliation: the other four gaps this cleanup found were values that
> existed and failed to reach the wire, while this one is a body of work nobody has written. Ruled
> 2026-08-22.

## Exclusions

No API contract is defined here for rehire, employee documents (`REQ-HR-0005`), import (`REQ-HR-0009`), export (`REQ-HR-0010`), department, position, manager assignment, employee-number generation, user↔company assignment administration, or Angular. No `DELETE` route exists, and no route accepts a writable `TenantId`, `CompanyId`, or `BranchId` on an entity.

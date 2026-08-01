---
document_id: FP-004-API
title: Localization API Contracts
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# API Contracts

## Boundary and conventions

HTTP/OpenAPI are Milestone 2. Prefix is `/api/platform/localization`; all routes require authentication, trusted current Tenant, live Active status, and strict request binding unless the public system-default group is separately exposed. No route/body/query/header accepts writable TenantId. Unknown fields are rejected. Reads are bounded; list/history use stable cursor or page pagination with implementation-approved positive limits documented in OpenAPI. No physical DELETE and no colon-style action route exist.

Effective text includes `resourceKey`, `value`, `requestedCulture`, `resolvedCulture`, `direction`, `resolutionSource`, `catalogVersion`, `resourceVersion`, and applicable `tenantLocalizationVersion`. Management projections add format/classification/editability, compatibility, current version, eligible Undo target, and rowversion as applicable.

## Approved routes

| Route | Permission | Success | Numbered contract |
|---|---|---|---|
| `GET /api/platform/localization/resources` | View | 200 bounded list | AC-LOC-0042 / TS-LOC-0071 |
| `GET /api/platform/localization/resources/{resourceKey}` | View | 200 resource | AC-LOC-0043 / TS-LOC-0072 |
| `PUT /api/platform/localization/resources/{resourceKey}/overrides/{culture}` | Manage | 200 update or 201 create | AC-LOC-0044 / TS-LOC-0073 |
| `POST /api/platform/localization/resources/{resourceKey}/overrides/{culture}/undo` | Manage | 200 | AC-LOC-0045 / TS-LOC-0074 |
| `POST /api/platform/localization/resources/{resourceKey}/overrides/{culture}/restore-default` | Manage | 200 | AC-LOC-0046 / TS-LOC-0075 |
| `GET /api/platform/localization/resources/{resourceKey}/history` | ViewHistory | 200 bounded history | AC-LOC-0047 / TS-LOC-0076 |
| `POST /api/platform/localization/preview` | Manage | 200 | AC-LOC-0048 / TS-LOC-0077 |
| `GET /api/platform/localization/effective` | View | 200 bounded group/list | AC-LOC-0049 / TS-LOC-0078 |
| `POST /api/platform/localization/effective/batch` | View | 200 bounded batch | AC-LOC-0050 / TS-LOC-0079 |

Each numbered AC/TS verifies method/path, auth, permission, trusted Tenant derivation, live eligibility, exact schema and unknown-field rejection, success status/projection, codes/statuses, concurrency where relevant, limits/paging, cross-Tenant denial, and OpenAPI.

## Mutation schemas

PUT body: `{ "value": string, "expectedRowVersion": string|null }`. `null` is create-only: absent aggregate creates; existing (including restored inactive) returns 409 `localization.override_already_exists`. Non-null is update-only: missing returns 409 `localization.override_missing`; stale returns 409 `concurrency.conflict`; match updates. Concurrent creates yield exactly one success and one deterministic 409. Culture is route `en|ar`; ResourceKey is route identity.

Undo body: `{ "targetVersion": integer, "expectedRowVersion": string }`. Target must equal the server-advertised eligible lineage predecessor. Stale: 409 `concurrency.conflict`; no predecessor: 409 `localization.undo_not_available`; wrong target: 422 `localization.undo_target_invalid`; incompatible target: 422 `localization.undo_target_incompatible`.

Restore body: `{ "expectedRowVersion": string }`. It retains the aggregate inactive and appends history; missing/stale use `localization.override_missing`/`concurrency.conflict` at 409.

Preview body: `{ "resourceKey": string, "culture": "en"|"ar", "value": string }`. It returns encoded text-only preview plus validation metadata and performs no write/version/event/shared-cache insertion/logged text. A non-overridable resource is rejected as uneditable.

Effective batch body: `{ "resourceKeys": string[], "requestedCulture": "en"|"ar", "formattingContext"?: object }`; keys are unique and count-bounded. Single effective query uses bounded resource/group selectors and requestedCulture. Formatting fields are typed, independent, and cannot be inferred from culture.

## Read/query contracts

List supports bounded filters for culture, lifecycle, compatibility, and search; comparisons are explicitly documented (ResourceKey ordinal; display-search culture-aware only when implemented). Detail contains both defaults, Tenant current state, compatibility, rowversion, and eligible Undo target. History is newest-first with VersionNumber stable ordering and protected values only for ViewHistory. Retired keys are absent from ordinary effective output but visible to authorized history/diagnostics.

## ProblemDetails

All errors contain authoritative `code`, `status`, `type`, `correlationId`, and stable `resourceKey`; `title`/`detail` are optional safe localized aids. RequestedCulture/ResolvedCulture appear where localized display text is returned/useful. Localization changes no outcome or status. Standard 400 handles malformed/unknown fields, unsupported culture, length/control/parser errors, and batch/limit errors; 401 unauthenticated; 403 permission/live-Tenant/audit-gate denial (using repository convention); 404 safe invisible resource/aggregate; exact 409/422 mappings are above. Sensitive authentication responses preserve FP-002 generic codes/semantics and never reveal internal cause.

## OpenAPI

Milestone 2 specifies all schemas, strict unknown-field policy, enums, UTF-16 limits, rowversion encoding, paging/batch maxima, examples, permission/security requirements, every success/error response and code, and RequestedCulture/ResolvedCulture. Contract tests compare runtime output to the document.

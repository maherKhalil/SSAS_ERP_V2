---
document_id: FP-009-API
title: HR Employee Data Exchange — API Contracts
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — API Contracts

> Routes are justified by scope, not generated from handlers, and the 1:1 route-to-handler property
> `DEC-DEP-0023` established holds here too. **Nine routes** if all three requirements ship; **five** if
> `OD-DOC-001` splits documents out.
>
> This surface breaks one convention the module has held since FP-006 — it accepts and returns something
> other than JSON — and that break is argued rather than assumed, below.

## Routes

| Method | Route | Permission | Requirement |
|---|---|---|---|
| `POST` | `/api/hr/employees/import/validate` | `HR.Employees.Import` | `FR-DOC-0101` |
| `POST` | `/api/hr/employees/import` | `HR.Employees.Import` | `FR-DOC-0102` |
| `GET` | `/api/hr/employees/import-runs` | `HR.Employees.View` | `FR-DOC-0103` |
| `GET` | `/api/hr/employees/export` | `HR.Employees.Export` | `FR-DOC-0201` |
| `GET` | `/api/hr/employees/export-runs` | `HR.Employees.View` | `FR-DOC-0202` |
| `POST` | `/api/hr/employees/{employeeId}/documents` | `HR.EmployeeDocuments.Upload` | `FR-DOC-0301` |
| `GET` | `/api/hr/employees/{employeeId}/documents` | `HR.EmployeeDocuments.View` | `FR-DOC-0302` |
| `GET` | `/api/hr/employee-documents/{documentId}/content` | `HR.EmployeeDocuments.Download` | `FR-DOC-0303` |
| `POST` | `/api/hr/employee-documents/{documentId}/withdraw` | `HR.EmployeeDocuments.Withdraw` | `FR-DOC-0304` |

**No `DELETE` verb anywhere** (`DEC-DEP-0024`), and `withdraw` is a named `POST` for the reason
[`lifecycle-model.md`](lifecycle-model.md) gives: the route name must not assert an answer `OD-DOC-008` has
not given.

**Why documents have two prefixes.** Upload and list hang off the employee, because that is the only way to
address "this person's documents". Content and withdrawal hang off `/api/hr/employee-documents/{documentId}`,
because a document identifier is already unique and repeating the employee in the path would create a second
way to get it wrong — a mismatched pair to validate and a mismatch to answer. The scope proof is identical
either way: the read resolves the document's employee and proves *that employee* is in scope
([`authorization-model.md`](authorization-model.md)).

**Route inventory obligation.** The HR surface is **41 routes** today, asserted exactly by
`HrRouteInventoryTests` beside the full list. This package takes it to **46** (import/export only) or **50**
(with documents). The count and the list are both updated, or the guard fails — which is the point of it.

## The convention this surface breaks

Every HR route to date accepts and returns `application/json` with strict binding. These do not:

| Route | Request | Response |
|---|---|---|
| `POST .../import`, `.../import/validate` | `multipart/form-data` — one file part plus an `importKey` field | `application/json` |
| `GET .../export` | — | `text/csv` |
| `POST .../{employeeId}/documents` | `multipart/form-data` | `application/json` |
| `GET .../content` | — | The document's stored content type |

**The break is unavoidable and is contained.** A file cannot be a JSON field without base64, which inflates
it by a third and forces the whole payload into memory before parsing. What is preserved is the part that
matters: **the metadata half stays strict.** The form's non-file fields are validated against a declared set
and an unrecognized field is `400 request.invalid`, exactly as an undeclared JSON property is. The strictness
was never about JSON; it was about never silently ignoring input.

**No `Content-Disposition` filename echo from caller input on the export.** The export file name is
server-generated. Reflecting a caller-supplied name into a response header is a header-injection surface for
no benefit.

## Import

```http
POST /api/hr/employees/import
X-Company-Id: …
Content-Type: multipart/form-data; boundary=…
```

| Part | Type | Required |
|---|---|---|
| `file` | CSV, UTF-8, ≤ 10 MB, ≤ 5,000 data rows | Yes |
| `importKey` | text, ≤ 128 chars | Yes (`DEC-DOC-0004`) |

### Column contract

Matched case-insensitively, order-independent; a missing required column or an unrecognized column is
refused before any row is read (`DEC-DOC-0002`).

| Column | Required | Notes |
|---|---|---|
| `employeeNumber` | Yes | Unique per company, normalized |
| `fullName` | Yes | |
| `employmentDate` | Yes | ISO-8601 |
| `departmentCode` | Yes | Resolved per `OD-DOC-004`; never created (`BRULE-DOC-0601`) |
| `positionCode` | Yes | Same |
| `nationalId` | No | Optional in FP-006; `OD-DOC-006` may make it export-absent, and the round-trip property (`DEC-DOC-0008`) needs it optional here |

**No `companyId`, `branchId`, `tenantId` or `status` column exists.** They are not validated away — they are
absent from the contract, so a file carrying one is refused by the unknown-column rule. This is `FP-006`'s
"absent by construction, not merely validated" applied to a header row.

### Response — the per-row report

```jsonc
{
  "importRunId": "…",
  "outcome": "Applied",              // Validated | Applied | Refused
  "rowCount": 1000,
  "acceptedCount": 998,
  "rejectedCount": 2,
  "errors": [
    { "rowNumber": 14,  "column": "employeeNumber", "code": "employee.number_conflict",
      "message": "…" },
    { "rowNumber": 902, "column": "departmentCode", "code": "department.not_found",
      "message": "…" }
  ]
}
```

`rowNumber` is the **1-based line number in the submitted file, header included** — the number the operator's
editor shows them. `code` comes from the module's existing problem-code namespaces, because a row failing
uniqueness fails it for exactly the reason a single create would, and inventing import-specific codes for the
same conditions would give one failure two names.

**`outcome` under `OD-DOC-003`:** all-or-nothing makes `acceptedCount` either `rowCount` or `0`; partial
success makes the two counts independent. The shape does not change, which is why it can be specified before
the ruling.

## Export

```http
GET /api/hr/employees/export?status=Terminated&branchScope=AllAuthorizedBranches
X-Company-Id: …
```

Accepts **the same query parameters as employee search**, with the same strict allowlist, the same defaults
and the same refusals. An export is a search that leaves the system, so it must not grow a second, subtly
different filter vocabulary — and the FP-009 audit-of-the-audit lesson applies: a parameter implemented below
the transport and unreachable above it is a capability nobody can use.

Response: `text/csv`, UTF-8 with BOM (Excel opens UTF-8 without a BOM as mojibake, and this file exists to be
opened in Excel), columns per `DEC-DOC-0008` minus whatever `OD-DOC-006` removes, ordered by full name then
identifier — the same total order search uses, so paging and export agree.

## Documents

```http
POST /api/hr/employees/{employeeId}/documents
Content-Type: multipart/form-data
```

| Part | Notes |
|---|---|
| `file` | ≤ 10 MB; content type on the allowlist **and** matching magic bytes (`SEC-DOC-0406`) |
| `documentType` | From the closed enum (`DEC-DOC-0012`) |

Response `201` with the metadata representation:

```jsonc
{
  "documentId": "…",
  "employeeId": "…",
  "documentType": "Contract",
  "fileName": "contract-2026.pdf",
  "contentType": "application/pdf",
  "byteCount": 184213,
  "contentHash": "sha256:…",
  "status": "Active",
  "uploadedUtc": "…",
  "uploadedBy": "…",
  "rowVersion": "AAAAAAAAB9E="
}
```

**No content URL in the representation.** A field holding a link to the bytes would be a second
authorization surface — one that outlives the response, travels in logs, and is checked by whatever code
happens to serve it. Content is fetched from the content route, which resolves the content scope every time.

## Problem codes

Own namespaces (`DEC-DEP-0026`), reusing existing codes where the condition is genuinely the existing one.

| Condition | Status | Code |
|---|---|---|
| Malformed file, bad header, unknown column, cap exceeded, unparsable row | `400` | `request.invalid` |
| Unsupported file format | `400` | `employee_import.format_unsupported` |
| Import key already used | `200` | — the original run's result (`DEC-DOC-0004`) |
| Row-level uniqueness, transition and reference failures | *(in the report)* | `employee.number_conflict`, `employee.national_id_conflict`, `department.not_found`, `position.not_found` |
| Document exceeds the size ceiling | `400` | `employee_document.too_large` |
| Content type not allowlisted, or bytes disagree with it | `400` | `employee_document.content_type_rejected` |
| Document unknown or out of scope | `404` | `employee_document.not_found` |
| Withdrawing an already-withdrawn document | `409` | `employee_document.transition_invalid` |
| Missing functional permission | `403` | `authorization.forbidden` |
| Company / branch scope refusals | `403` | `company.scope_denied`, `branch.scope_denied` |

**`409` for the state conflict, per `DEC-DEP-0030`** — ratified 2026-08-22, and the first package to inherit
it rather than rediscover it.

## Rowversion

`FR-DOC-0304` carries `expectedRowVersion` in its body under the platform convention. Import and export carry
none: an import creates, and an export changes nothing. The run records are append-only and have no
concurrency token at all — the `EmployeeBranchAssignment` precedent.

---
document_id: FP-007-API
title: HR Department — API Contracts
status: Approved for Implementation
version: 1.0
---

# FP-007 — API Contracts

Routes are justified by scope, not generated from domain methods. Every mutation carries a `RowVersion` using
the transport convention already established in `Development-Standards.md` and FP-006 (base64 string in the
request body, echoed on every read). Requests are strict DTOs: unknown properties are rejected, and no request
carries `TenantId` or `CompanyId` — both are server-stamped and accepting them would create a spoofing surface
the platform has already closed elsewhere.

## Department

| Method | Route | Permission | Notes |
|---|---|---|---|
| `POST` | `/api/hr/departments` | `HR.Departments.Create` | `FR-DEP-0101` |
| `GET` | `/api/hr/departments/{departmentId}` | `HR.Departments.View` | `FR-DEP-0102` |
| `GET` | `/api/hr/departments` | `HR.Departments.View` | `FR-DEP-0103`; filters `status`, `parentDepartmentId`, `search`; paged |
| `GET` | `/api/hr/departments/{departmentId}/hierarchy` | `HR.Departments.View` | `FR-DEP-0105`; returns ancestors and descendants |
| `PUT` | `/api/hr/departments/{departmentId}` | `HR.Departments.Update` | `FR-DEP-0104`; **name and code only** |
| `POST` | `/api/hr/departments/{departmentId}/parent` | `HR.Departments.Update` | `FR-DEP-0106` |
| `PUT` | `/api/hr/departments/{departmentId}/manager` | `HR.Departments.Update` | `FR-DEP-0107`; assign |
| `DELETE` | `/api/hr/departments/{departmentId}/manager` | `HR.Departments.Update` | `FR-DEP-0107`; clear |
| `POST` | `/api/hr/departments/{departmentId}/deactivate` | `HR.Departments.Deactivate` | `FR-DEP-0108` |
| `POST` | `/api/hr/departments/{departmentId}/reactivate` | `HR.Departments.Deactivate` | `FR-DEP-0108` |

**There is no `DELETE /api/hr/departments/{id}`.** Departments are not deleted (`BRULE-DEP-0016`).

**Why parent, manager and status are separate routes rather than fields on `PUT`.** Each has its own
authorization, its own refusal set, and its own audit meaning. Folding them into the general update would make
a re-parent — which moves a whole subtree and must pass the acyclicity invariant — reachable by sending one
extra field to a route whose name says "update". FP-006 made the same call for Employee transfer, and the
reasoning is identical.

`DELETE .../manager` rather than `PUT` with a null: a null in a strict DTO is ambiguous between "clear it" and
"I did not send this field", and the two must not be confused when the field controls who runs a department.

## Employee — changes to existing contracts

| Contract | Change |
|---|---|
| `POST /api/hr/employees` | Request gains **required** `departmentId` (subject to `OD-DEP-001`) |
| `PUT /api/hr/employees/{id}` | **Unchanged.** `departmentId` is *not* added here (`BRULE-DEP-0018`) |
| `POST /api/hr/employees/{id}/department` | **New.** `HR.Employees.Update`. `FR-DEP-0110`. Body: `departmentId`, `rowVersion` |
| `GET /api/hr/employees/{id}` | Response gains `department` — `{ departmentId, code, name }` |
| `GET /api/hr/employees` | Response items gain the same; request gains optional `departmentId` filter (`FR-DEP-0111`) |
| `GET /api/hr/employees/{id}/branch-history` | **Unchanged** |

## Representations

```jsonc
// Department
{
  "departmentId": "…",
  "code": "SALES",
  "name": "Sales",
  "parentDepartmentId": "…",          // null at root
  "manager": {                        // null when unassigned
    "employeeId": "…",
    "employeeNumber": "E-0001",
    "fullName": "…",
    "isTerminated": false             // DEC-DEP-0013 — surfaced, never auto-cleared
  },
  "status": "Active",
  "employeeCount": 12,                // within the caller's employee read scope; see below
  "rowVersion": "AAAAAAAAB9E="
}
```

> **SHIPPED 2026-08-22** (HR as-built cleanup, the task that closed this gap). The field was specified here,
> **never built**, and FP-007's own as-built pass nonetheless marked this document matched — the divergence
> was found by the FP-008 Phase 4 reconciliation and recorded as NOT SHIPPED on 2026-08-21. It is now
> implemented rather than deleted from the spec.
>
> **As built:** `IEmployeeReadService.CountEmployeesByDepartmentAsync` — the sibling of the position
> counter, requiring an `EmployeeReadScope` — composed onto the wire by
> `DepartmentCompositionServices.CountEmployeesAsync`, which every department representation goes through,
> reads and write-backs alike.
>
> **Semantics are `DEC-POS-0034`'s, unchanged:** a NUMBER within the caller's own employee scope, `0` when
> the caller can read employees but sees none of this department's members, and **`null`** — present, not
> absent — when the caller holds `HR.Departments.View` without `HR.Employees.View`. Zero and null are
> different answers.
>
> **Proven by** `D33`–`D37` in `DepartmentEndpointTests` (number, zero-is-not-null, null-when-unscoped,
> counted under the caller's own scope, and present on a write-back) and three scope-containment tests in
> `DepartmentApplicationSqlServerTests`, which run the shipped composer against real rows because a stub
> cannot demonstrate that a predicate filters.
>
> **On the detail representation only.** The list row does not carry it: this document specifies it here and
> nowhere else, and a per-row count would be one extra scoped aggregate per result.

**`employeeCount` is computed within the caller's employee read scope, and the field name says so in the API
documentation.** *(As built: the count is scope-visible, matching the shipped position counter exactly — and
like it, the count is not filtered by employment status, so a terminated employee still counts toward the
department they were in.)* Two users can legitimately see different counts for the same department, because they are
authorized for different branches. The alternative — a company-wide count — would leak the size of branches
the caller cannot read. This is the one place where `OD-DEP-005`'s "department is company-visible" and
"employees are branch-scoped" meet, and it must be resolved in favour of the tighter scope.

## Error mapping

Department errors map through the module's own `IApiErrorMapper` in `SSAS.HR.API`, using the
`SSAS.BuildingBlocks.Api` transport primitives introduced in FP-006C5. No new mapping mechanism is added.

| Domain error | HTTP |
|---|---|
| `DepartmentNotFound`, or found outside company scope | `404` |
| `ParentIsSelf`, `ParentIsDescendant`, `ParentInDifferentCompany`, `ParentInactive` | `422` |
| `CodeAlreadyExists` | `409` |
| `ManagerInDifferentCompany`, `ManagerTerminated`, `ManagerIsDepartmentMember` | `422` |
| `DepartmentInactive` (receiving an employee) | `422` |
| `HasActiveChildren` (deactivating) | `422` |
| Stale `RowVersion` | `409` |
| Permission denied | `403` |
| Company scope empty or company inactive | `403` |

`404` for out-of-scope is deliberate and matches the Employee surface: a `403` would confirm the department
exists in a company the caller may not see.

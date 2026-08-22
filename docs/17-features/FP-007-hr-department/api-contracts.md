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

| Method | Route | Permission | Notes | As built |
|---|---|---|---|---|
| `POST` | `/api/hr/departments` | `HR.Departments.Create` | `FR-DEP-0101` | as specified |
| `GET` | `/api/hr/departments/{departmentId}` | `HR.Departments.View` | `FR-DEP-0102` | as specified |
| `GET` | `/api/hr/departments` | `HR.Departments.View` | `FR-DEP-0103`; filters `status`, `parentDepartmentId`, `search`; paged | as specified |
| `GET` | `/api/hr/departments/{departmentId}/hierarchy` | `HR.Departments.View` | `FR-DEP-0105`; returns ancestors and descendants | **`GET /{departmentId}/children`, direct children only** (`DEC-DEP-0024`) |
| `PUT` | `/api/hr/departments/{departmentId}` | `HR.Departments.Update` | `FR-DEP-0104`; **name and code only** | as specified |
| `POST` | `/api/hr/departments/{departmentId}/parent` | `HR.Departments.Update` | `FR-DEP-0106` | **two routes: `/move` and `/move-to-root`** (`DEC-DEP-0023`) |
| `PUT` | `/api/hr/departments/{departmentId}/manager` | `HR.Departments.Update` | `FR-DEP-0107`; assign | **`POST /{departmentId}/manager`** (`DEC-DEP-0024`) |
| `DELETE` | `/api/hr/departments/{departmentId}/manager` | `HR.Departments.Update` | `FR-DEP-0107`; clear | **`POST /{departmentId}/manager/remove`** (`DEC-DEP-0024`) |
| `POST` | `/api/hr/departments/{departmentId}/deactivate` | `HR.Departments.Deactivate` | `FR-DEP-0108` | as specified |
| `POST` | `/api/hr/departments/{departmentId}/reactivate` | `HR.Departments.Deactivate` | `FR-DEP-0108` | **`POST /{departmentId}/activate`** (`DEC-DEP-0025`) |

> **AS-BUILT CORRECTION (2026-08-22, HR as-built cleanup).** Five of the ten rows above name a route the
> surface does not expose. Each was superseded by a **ratified** Phase 4 decision — `DEC-DEP-0023` split the
> parent change into two routes, `DEC-DEP-0024` removed the `DELETE` verb from the module entirely and cut
> the hierarchy read down to direct children, `DEC-DEP-0025` named the lifecycle pair — and this table was
> never brought forward. The rows are corrected in place rather than rewritten, so the original contract and
> the decision that changed it both stay readable.
>
> **The shipped surface is twelve routes**, not the thirteen `DEC-DEP-0023` states: eleven on the
> `/api/hr/departments` prefix plus `POST /api/hr/employees/{employeeId}/change-department`. The arithmetic
> is independently fixed by the route inventory — FP-006's nine plus these twelve is the twenty-one that
> FP-008 took to forty-one — and `HrRouteInventoryTests` asserts the exact list.
>
> `GET /{departmentId}/children` returning only one level means **`AC-DEP-0016` and `TS-DEP-0027`, which
> specify an ancestors-and-descendants read, describe a route that does not exist.** `DEC-DEP-0024` ruled
> that deliberately; the criteria were never annotated, and are annotated now where they appear.

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
| `POST /api/hr/employees/{id}/department` | **New.** `HR.Employees.Update`. `FR-DEP-0110`. Body: `departmentId`, `rowVersion`. *(As built 2026-08-22: shipped as `POST /api/hr/employees/{id}/change-department`, per `DEC-DEP-0024`'s named-POST convention.)* |
| `GET /api/hr/employees/{id}` | Response gains `department` — `{ departmentId, code, name }`. **NOT SHIPPED** — see the note below |
| `GET /api/hr/employees` | Response items gain the same; request gains optional `departmentId` filter (`FR-DEP-0111`). **NEITHER SHIPPED** — see the note below |
| `GET /api/hr/employees/{id}/branch-history` | **Unchanged** |

> **NOT SHIPPED (recorded 2026-08-22, HR as-built cleanup — awaiting an architect ruling).** Two of the six
> employee-contract changes above are absent from the implementation, and no decision records dropping them:
>
> 1. **The `department` object on the employee representation.** `EmployeeResponse` and
>    `EmployeeSummaryResponse` carry no department at all — not the nested object, not even a bare
>    `departmentId`. The read models beneath them DO carry `Employee.DepartmentId` (`EmployeeDetail`,
>    `EmployeeSummary`), so the value reaches the application layer and stops at the wire.
> 2. **The `departmentId` search filter (`FR-DEP-0111`).** The filter is fully implemented BELOW the
>    transport — `EmployeeSearchCriteria.DepartmentId` and its SQL predicate both exist and are exercised
>    — but `departmentId` is absent from the employee search query allowlist, so no caller can reach it. A
>    request naming it is rejected as an undeclared parameter. The capability is shipped and unreachable.
>
> Both are recorded here rather than implemented: this cleanup's scope was the `employeeCount` gap, and what
> to do about these is an architect decision, not a coder's.

## Representations

```jsonc
// Department
{
  "departmentId": "…",
  "code": "SALES",
  "name": "Sales",
  "parentDepartmentId": "…",          // null at root
  "manager": {                        // null when there is no manager at all
    "isAssigned": true,               // AS BUILT — see the correction below
    "employeeId": "…",                 // null when the caller may not see who
    "employeeNumber": "E-0001",
    "fullName": "…",
    "isActive": true                  // AS BUILT: shipped as isActive, NOT isTerminated
  },
  "companyId": "…",                    // AS BUILT: reported, and absent from the original sample
  "status": "Active",
  "employeeCount": 12,                // within the caller's employee read scope; see below
  "rowVersion": "AAAAAAAAB9E="
}
```

> **AS-BUILT CORRECTION — the manager sub-object (2026-08-22).** The original sample describes two states,
> present and null. The surface ships **three**, and the third is the one that matters: a department is
> company-visible while employees are branch-scoped, so a caller may legitimately learn THAT a department has
> a manager without being allowed to know who. `isAssigned` carries that distinction, and collapsing it into
> `null` would tell such a caller the department has no manager — which is false. `D32` in
> `DepartmentEndpointTests` pins it.
>
> The terminated flag shipped **inverted and renamed**: `isActive`, not `isTerminated`. `DEC-DEP-0013`'s rule
> is unchanged — a terminated manager is surfaced and never auto-cleared — but a client written against
> this document's field name would read the wrong sense of the right fact, which is why the correction is
> recorded rather than left to the reader.

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

| Domain error | HTTP | As built |
|---|---|---|
| `DepartmentNotFound`, or found outside company scope | `404` | `404 department.not_found` |
| `ParentIsSelf`, `ParentIsDescendant`, `ParentInDifferentCompany`, `ParentInactive` | `422` | **`409 department.hierarchy_invalid`** |
| `CodeAlreadyExists` | `409` | `409 department.code_conflict` |
| `ManagerInDifferentCompany`, `ManagerTerminated`, `ManagerIsDepartmentMember` | `422` | **`409 department.manager_invalid`** |
| `DepartmentInactive` (receiving an employee) | `422` | **`409 department.transition_invalid`** |
| `HasActiveChildren` (deactivating) | `422` | **`409 department.transition_invalid`** |
| Stale `RowVersion` | `409` | `409 concurrency.conflict` |
| Permission denied | `403` | `403 authorization.forbidden` |
| Company scope empty or company inactive | `403` | `403 company.scope_denied` |

> **STATUS-CODE DIVERGENCE (recorded 2026-08-22, HR as-built cleanup — awaiting an architect ruling).** The
> four rows specifying **`422`** ship as **`409`**. `DepartmentApiErrorMapper` answers
> `department.hierarchy_invalid`, `department.manager_invalid` and `department.transition_invalid` at 409, and
> **no decision in this package records the change** — unlike the route naming, which `DEC-DEP-0023`–`0025`
> ratified.
>
> This is recorded rather than corrected in either direction. It is observable client behaviour on a **merged
> surface**: rewriting the document would ratify a change nobody ruled, and changing the code would alter a
> shipped contract on a coder's judgement. The problem CODES are distinct either way, so a client that
> branches on `code` — the authoritative field by this package's own convention — is unaffected; a client
> branching on the status class is not.
>
> Worth noting for whoever rules: the mapper's own reasoning is explicit and defensible — it keeps every
> refusal that names a *state conflict* at 409, and the employee surface uses 409 for the same shape
> (`employee.transition_invalid`). The divergence may well be the document being stale rather than the code
> being wrong.

`404` for out-of-scope is deliberate and matches the Employee surface: a `403` would confirm the department
exists in a company the caller may not see.

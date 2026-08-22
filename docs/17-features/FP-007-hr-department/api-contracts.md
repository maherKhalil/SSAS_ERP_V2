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
| `GET /api/hr/employees/{id}` | Response gains `department` — `{ departmentId, code, name }`. **SHIPPED 2026-08-22** |
| `GET /api/hr/employees` | Response items gain the same; request gains optional `departmentId` filter (`FR-DEP-0111`). **BOTH SHIPPED 2026-08-22** |
| `GET /api/hr/employees/{id}/branch-history` | **Unchanged** |

> **SHIPPED 2026-08-22** (HR as-built cleanup, ruled). Two of the six employee-contract changes above were
> found absent by the audit on 2026-08-21 and are now built. They had failed in opposite ways, which is worth
> keeping:
>
> 1. **The `department` object on the employee representation** was never built at all. `EmployeeResponse`
>    and `EmployeeSummaryResponse` carried no department — not the nested object, not even a bare
>    `departmentId` — while the read models beneath them did carry `Employee.DepartmentId`. The value
>    reached the application layer and stopped at the wire.
>
>    **As built:** `EmployeeDepartmentSummary` on `EmployeeDetail` and `EmployeeSummary`, replacing the bare
>    identifier so there is one source for it rather than two that can drift, surfaced as
>    `EmployeeDepartmentResponse` on both wire shapes. The code and name are resolved by an **INNER JOIN to
>    `tenant.Departments` inside the existing employee query** — one join on `Employee.DepartmentId`, which
>    is NOT NULL behind a real foreign key, so it can neither add nor remove a row. A per-row service call
>    was rejected: it would be N round trips for a label. The search total is still counted on the unjoined
>    query, so the count and the page cannot disagree even if the join were ever changed.
>
>    **No extra permission gate, and the distinction from `employeeCount` is the reason.** That field reads
>    ACROSS an aggregate the caller may have no authority over — employees are branch-scoped, departments are
>    not — so it needs a scope of its own. This one LABELS a field the employee record already carries, in
>    the employee's own company, which the caller's scope has already admitted; resolving it to a code and a
>    name discloses nothing a caller holding `HR.Departments.View` could not read directly.
>
> 2. **The `departmentId` search filter (`FR-DEP-0111`)** was fully implemented BELOW the transport —
>    `EmployeeSearchCriteria.DepartmentId`, the SQL conjunct, and `D15` proving it narrows rather than
>    widens — and unreachable, because the name was missing from the employee search query allowlist. A
>    request naming it was rejected as an undeclared parameter. **The fix is that one allowlist entry**, plus
>    parsing that refuses a malformed identifier rather than treating it as "no filter".
>
> **Proven by** `A46`–`A51` in `EmployeeEndpointTests` — the sub-object on the detail and on a list row, the
> filter reaching the criteria, the filter combining with the others rather than replacing them, a malformed
> identifier refused, and three near-miss parameter names still refused so the allowlist is shown to be
> still closed — and by `D16` in `EmployeeBoundarySqlServerTests`, which reads two employees in two
> different departments through the real join and checks each row carries its own.

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
| `ParentIsSelf`, `ParentIsDescendant`, `ParentInDifferentCompany`, `ParentInactive` | `409` | `409 department.hierarchy_invalid` |
| `CodeAlreadyExists` | `409` | `409 department.code_conflict` |
| `ManagerInDifferentCompany`, `ManagerTerminated`, `ManagerIsDepartmentMember` | `409` | `409 department.manager_invalid` |
| `DepartmentInactive` (receiving an employee) | `409` | `409 department.transition_invalid` |
| `HasActiveChildren` (deactivating) | `409` | `409 department.transition_invalid` |
| Stale `RowVersion` | `409` | `409 concurrency.conflict` |
| Permission denied | `403` | `403 authorization.forbidden` |
| Company scope empty or company inactive | `403` | `403 company.scope_denied` |

> **RULED 2026-08-22 — the four rows above were drafted as `422` and ship as `409`; the SHIPPED BEHAVIOUR
> IS RATIFIED and the rows are corrected to match it (`DEC-DEP-0030`).** The audit found the divergence and
> did not resolve it, because re-litigating status codes on live routes to match stale prose is backwards.
>
> The reasoning the mapper already carried is the reasoning that was ratified: every refusal naming a *state
> conflict* answers 409, and the employee surface answers 409 for the identical shape
> (`employee.transition_invalid`). The problem CODES were distinct throughout — the authoritative field by
> this package's own convention — so no client branching on `code` was ever affected.
>
> The distinction from the route rows above matters: those were superseded by decisions that existed and
> were simply never carried into this table. This one had no decision at all until now, which is why it gets
> a numbered one rather than an annotation.

`404` for out-of-scope is deliberate and matches the Employee surface: a `403` would confirm the department
exists in a company the caller may not see.

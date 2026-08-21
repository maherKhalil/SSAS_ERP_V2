---
document_id: FP-008-API
title: HR Position — API Contracts
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — API Contracts

Routes are justified by scope, not generated from domain methods, and stand **1:1 with handlers** — a handler
without a route is dead application code, and a route without a handler is a promise nothing keeps
(`DEC-DEP-0023`). Every mutation carries a `RowVersion` using the transport convention in
`Development-Standards.md` (base64 string in the request body, echoed on every read). Requests are strict
DTOs: unknown properties are rejected, and no request carries `TenantId` or `CompanyId` — both are
server-stamped, and accepting them would create a spoofing surface the platform has already closed.

## Position

| Method | Route | Permission | Notes |
|---|---|---|---|
| `POST` | `/api/hr/positions` | `HR.Positions.Create` | `FR-POS-0201` |
| `GET` | `/api/hr/positions/{positionId}` | `HR.Positions.View` | `FR-POS-0202` |
| `GET` | `/api/hr/positions` | `HR.Positions.View` | `FR-POS-0203`; filters `status`, `jobGradeId`, `search`; paged |
| `PUT` | `/api/hr/positions/{positionId}` | `HR.Positions.Update` | `FR-POS-0204`; title, code and grade reference |
| `POST` | `/api/hr/positions/{positionId}/deactivate` | `HR.Positions.Deactivate` | `FR-POS-0205` |
| `POST` | `/api/hr/positions/{positionId}/activate` | `HR.Positions.Deactivate` | `FR-POS-0205`; the same permission guards both directions (`DEC-DEP-0025`) |

**Six routes for Position**, plus four per retained grade entity on the same shape, plus the two employee
routes below.

## Grades **(OD-POS-002)**

| Method | Route | Permission |
|---|---|---|
| `POST` | `/api/hr/job-grades` | `HR.JobGrades.Create` |
| `GET` | `/api/hr/job-grades/{jobGradeId}` | `HR.JobGrades.View` |
| `GET` | `/api/hr/job-grades` | `HR.JobGrades.View` |
| `PUT` | `/api/hr/job-grades/{jobGradeId}` | `HR.JobGrades.Update` |
| `POST` | `/api/hr/job-grades/{jobGradeId}/deactivate` | `HR.JobGrades.Deactivate` |
| `POST` | `/api/hr/job-grades/{jobGradeId}/activate` | `HR.JobGrades.Deactivate` |

`/api/hr/salary-grades` mirrors this exactly under `HR.SalaryGrades.*`, and exists only under `OD-POS-002`
option (i). Under option (ii) the single family is `/api/hr/grades`.

## Employee — changes to existing contracts

| Contract | Change |
|---|---|
| `POST /api/hr/employees` | Request gains **required** `positionId` (subject to `OD-POS-001`) |
| `PUT /api/hr/employees/{id}` | **Unchanged.** `positionId` is *not* added here (`BRULE-POS-0017`) |
| `POST /api/hr/employees/{employeeId}/change-position` | **New.** `HR.Employees.Update`. `FR-POS-0211`. Body: `positionId`, `reasonCode?`, `reasonText?`, `rowVersion` |
| `GET /api/hr/employees/{employeeId}/position-history` | **New.** `HR.Employees.View`. `FR-POS-0212` |
| `GET /api/hr/employees/{id}` | Response gains `position` — `{ positionId, code, title }` |
| `GET /api/hr/employees` | Response items gain the same; request gains an optional `positionId` filter (`FR-POS-0213`) |
| `GET /api/hr/employees/{id}/branch-history`, `.../department-history` | **Unchanged** |

**The change route lives on the EMPLOYEE prefix**, under `HR.Employees.Update`, matching
`POST /api/hr/employees/{employeeId}/change-department` exactly. `DEC-POS-0019` records why, and records the
one question that precedent does not answer: whether a promotion deserves its own permission.

## No `DELETE` verb anywhere

Every state change is a **named `POST`**. The HR surface exposes no `MapDelete` at all — a convention
established by the employee surface, extended by the department surface, and enforced by the
`The_hr_surface_exposes_no_delete_verb` token guard (`DEC-DEP-0024`). FP-008 inherits it rather than
relitigating it, and the guard's route inventory must be extended so that inheritance is checked rather than
assumed.

## Representations

```jsonc
// Position
{
  "positionId": "…",
  "code": "ACC-SR",
  "title": "Senior Accountant",
  "jobGrade": {                      // null when unassigned; absent entirely under OD-POS-002 (iv)
    "jobGradeId": "…",
    "code": "G7",
    "name": "Grade 7",
    "rankOrder": 70
  },
  "status": "Active",
  "employeeCount": 12,               // within the caller's employee read scope; see below
  "rowVersion": "AAAAAAAAB9E="
}
```

```jsonc
// SalaryGrade — only under OD-POS-002 (i) and OD-POS-004 (ii)/(iii)
{
  "salaryGradeId": "…",
  "code": "S7",
  "name": "Band 7",
  "rankOrder": 70,
  "minimumAmount": 12000.0000,       // null permitted; see data-model.md
  "midpointAmount": 15000.0000,
  "maximumAmount": 18000.0000,
  "currencyCode": "SAR",             // ECHOED FROM THE COMPANY, NOT STORED — see below
  "status": "Active",
  "rowVersion": "…"
}
```

**`currencyCode` is a read-side projection of the owning Company's `BaseCurrencyCode`, not a stored column**
(`DEC-POS-0015`). It appears in the representation because an amount without a currency is unreadable, and it
is **not** accepted on write — sending it is an unknown property and is rejected. If it were writable it would
be a second source of truth for a fact the Company already owns.

**`employeeCount` is computed within the caller's employee read scope, and the field name says so in the API
documentation.** Two users can legitimately see different counts for the same position, because they are
authorized for different branches. A company-wide count would leak the size of branches the caller cannot
read. This is the same resolution `FP-007` reached for `Department.employeeCount`, in favour of the tighter
scope.

## Error mapping

Position errors map through **their own mapper and their own problem-code namespace** (`position.*`,
`job_grade.*`, `salary_grade.*`), not through `EmployeeApiErrorMapper` or `DepartmentApiErrorMapper`.

`DEC-DEP-0026` records why this is a defect rather than a shortcut, and records that it produced one: a
department manager conflict answered `employee.number_conflict`, because that mapper's only unique-constraint
arm had been written for the employee-number pre-check. A shared table cannot stay honest once two resources
disagree about what a shared persistence code means.

**`HR.API` references no Platform assembly** (`ADR-012`), so the mapper uses HR's own error types throughout.
The compiler enforced this in FP-007 Phase 4 when a first draft reached for Platform's `Persistence.*` type,
and it will enforce it here.

| Domain error | HTTP | Problem code |
|---|---|---|
| `PositionNotFound`, or found outside company scope | `404` | `position.not_found` |
| `CodeAlreadyExists` | `409` | `position.code_conflict` |
| `GradeInDifferentCompany`, `GradeInactive` | `422` | `position.grade_invalid` |
| `PositionInactive` (receiving an employee) | `422` | `position.inactive` |
| `PositionHasIncumbents` (deactivating, **`OD-POS-005`** reading (ii) only) | `422` | `position.has_incumbents` |
| `RankOrderAlreadyExists` | `409` | `job_grade.rank_conflict` |
| `AmountsOutOfOrder` | `422` | `salary_grade.amounts_invalid` |
| `PositionUnchanged` (change to the current position) | `422` | `position.unchanged` |
| Stale `RowVersion` | `409` | `concurrency.conflict` |
| Permission denied | `403` | |
| Company scope empty or company inactive | `403` | |

`404` for out-of-scope is deliberate and matches the Employee and Department surfaces: a `403` would confirm
the position exists in a company the caller may not see.

### `Persistence.UniqueConstraint` is context-dependent

`DEC-DEP-0027` established that the persistence layer's generic unique-constraint failure is resolved **by the
caller who knows the operation**, not by a switch that does not. FP-008 inherits the rule and has two
contexts:

- on **create and update of a position**, the only unique index is `NormalizedCode`, so it answers
  `position.code_conflict` — the same answer the pre-check gives, making a race and a sequential duplicate
  indistinguishable;
- on **create and update of a grade**, two unique indexes exist — `NormalizedCode` and `RankOrder` — and they
  are **not** interchangeable. The route must resolve which one fired, or answer with the one the operation
  was checking. **This is a new case the Department precedent does not cover**, because Department had exactly
  one unique index per operation. It is recorded here so it is designed rather than discovered.

**The wire equivalence is the contract, not the error identity.** Where two errors map to the same problem
code, both arms must move together if either is renamed, or the translation silently starts disclosing the
difference it exists to hide.

## Route inventory

The exact route count follows from `OD-POS-002`:

| `OD-POS-002` | Position routes | Grade routes | Employee routes | Total new |
|---|---|---|---|---|
| (i) three entities | 6 | 12 | 2 | **20** |
| (ii) one ladder | 6 | 6 | 2 | **14** |
| (iii) money deferred | 6 | 6 | 2 | **14** |
| (iv) position only | 6 | 0 | 2 | **8** |

`HrRouteInventoryTests` pins the HR surface as an **exact** inventory read from **both** the module harness
and the Host composition. FP-007 shipped an unreachable thirteenth route because the harness did not mirror
the Host and the route-absence test that should have caught it was passing vacuously. That inventory must be
extended by exactly the number above, in both harnesses, or FP-008 will repeat it.

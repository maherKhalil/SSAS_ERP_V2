---
document_id: FP-007-REQ
title: HR Department — Requirements
status: Draft — Owner Decision Required
version: 0.1
---

# FP-007 — Requirements

> Draft. Requirements marked **(OD)** depend on an unresolved owner decision and are provisional.

## Source requirements

| Source | Name | Coverage in FP-007 |
|---|---|---|
| `REQ-HR-0100` | Department CRUD | `FR-DEP-0101`, `FR-DEP-0102`, `FR-DEP-0103`, `FR-DEP-0104` |
| `REQ-HR-0101` | Department Hierarchy | `FR-DEP-0105`, `FR-DEP-0106` |
| `REQ-HR-0102` | Department Manager | `FR-DEP-0107` |
| `REQ-HR-0006` | Employee History | **Not extended.** Department history remains deferred (`DEC-DEP-0016`, `OD-DEP-004`) |
| `REQ-HR-0200` | Position Management | **Out of scope** (`DEC-DEP-0020`) |

## Functional requirements

### FR-DEP-0101 — Create a Department

A user holding `HR.Departments.Create`, acting within an authorized and active company, creates a Department
with a `Code`, a `Name`, an optional `ParentDepartmentId`, and an optional `ManagerEmployeeId`. The department
is created `Active`. `TenantId` and `CompanyId` are stamped from trusted server context and are never accepted
from the caller.

### FR-DEP-0102 — Read a Department

A user holding `HR.Departments.View` retrieves a single Department by identifier within their authorized
company scope. A department outside that scope is indistinguishable from one that does not exist.

### FR-DEP-0103 — List and search Departments

A user holding `HR.Departments.View` lists Departments within their authorized company scope, filtered
optionally by status, by parent, and by a name or code fragment. Results are ordered deterministically.

### FR-DEP-0104 — Update a Department's descriptive fields

A user holding `HR.Departments.Update` changes `Name`, and `Code` subject to `BRULE-DEP-0004`. Ownership
(`TenantId`, `CompanyId`), lifecycle status, parent, and manager are **not** changed through this operation —
each has its own (`FR-DEP-0106`, `FR-DEP-0107`, `FR-DEP-0108`).

### FR-DEP-0105 — Read the Department hierarchy

A user holding `HR.Departments.View` retrieves a Department's ancestors and its descendants within their
authorized company scope.

### FR-DEP-0106 — Change a Department's parent

A user holding `HR.Departments.Update` moves a Department, and its whole subtree with it, to a new parent or
to the root. The operation refuses any move that would create a cycle (`BRULE-DEP-0009`) and any move whose
new parent belongs to a different company (`BRULE-DEP-0008`).

### FR-DEP-0107 — Assign or clear a Department's manager

A user holding `HR.Departments.Update` sets `ManagerEmployeeId` to an Employee in the same tenant and the same
company, or clears it. Refusals are `BRULE-DEP-0010` through `BRULE-DEP-0013`.

### FR-DEP-0108 — Deactivate and reactivate a Department

A user holding `HR.Departments.Deactivate` moves a Department between `Active` and `Inactive`. Deactivation
does not remove existing employees (`BRULE-DEP-0015`); it refuses new arrivals (`BRULE-DEP-0014`,
`BR-HR-0009`).

### FR-DEP-0109 — Assign an Employee to a Department **(OD)**

Employee creation requires a `DepartmentId` in the same tenant, the same company, and `Active` status. The
treatment of Employees that already exist is `OD-DEP-001` and is not settled by this document.

### FR-DEP-0110 — Change an Employee's Department **(OD)**

An Employee's Department changes only through an explicit `ChangeDepartment` operation holding
`HR.Employees.Update` — never as a field on the ordinary profile update (`DEC-DEP-0015`). Whether the change
is recorded as history is `OD-DEP-004`.

### FR-DEP-0111 — Filter Employee search by Department

Employee search accepts an optional `departmentId` filter. This filters within the caller's existing employee
read scope; it does **not** become a fourth authorization dimension (`DEC-DEP-0019`).

## Non-functional requirements

### NFR-DEP-0301 — Scoped reads are indexed

Every Department read is served by an index whose leading keys match the mandatory predicate order — tenant,
then company. No scoped read may be served by a scan that ignores a scope column.

### NFR-DEP-0302 — Hierarchy queries are bounded

Ancestor and descendant queries execute in a single round trip via a recursive common table expression, with
an explicit recursion limit so a corrupted hierarchy fails loudly rather than exhausting the server
(`DEC-DEP-0006`).

### NFR-DEP-0303 — Optimistic concurrency

Every Department mutation carries a `RowVersion` and refuses a stale token, using the transport convention
already established in `Development-Standards.md` and FP-006.

### NFR-DEP-0304 — Authorization resolves live

Company scope, company status, and functional permission are resolved per operation and never cached from
login, matching `EmployeeScopeResolver`.

### NFR-DEP-0305 — Cutover coverage is derived, not declared

Department and any Employee foreign key to it are covered by the Shared→Dedicated copy manifest through the
existing model-derived mechanism, with dependency order produced by the foreign-key graph
(`TenantCutoverCopyPlan`). No hand-maintained ordering is introduced (`DEC-DEP-0018`).

### NFR-DEP-0306 — The acyclicity invariant holds under concurrency

Two simultaneous re-parent operations cannot combine to produce a cycle. The serialization mechanism is
specified in [`domain-model.md`](domain-model.md) (`DEC-DEP-0006`) and proven by `TS-DEP-0031`.

## Explicitly not required

FP-007 introduces no `PositionId`, no `Employee.ManagerId`, no department-scoped read of any other aggregate,
no cost-centre or GL mapping, no automatic code generation, and no placeholder column for any of them.

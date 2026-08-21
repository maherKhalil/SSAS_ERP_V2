---
document_id: FP-008-REQ
title: HR Position — Requirements
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — Requirements

> Draft. Requirements marked **(OD)** depend on an unresolved owner decision and are provisional.
>
> **The source requirements have no body text.** `REQ-HR-0200`, `REQ-HR-0201` and `REQ-HR-0202` appear in the
> requirement catalog as titles only. Everything below is therefore *derived* — from `BR-HR-0006`, from the
> patterns FP-006 and FP-007 established, and from the owner decisions in [`README.md`](README.md). Where a
> requirement rests on a reading rather than on a written statement, it says so.

## Source requirements

| Source | Name | Body text exists? | Coverage in FP-008 |
|---|---|---|---|
| `REQ-HR-0200` | Position Management | **No** | `FR-POS-0201`–`FR-POS-0205`, `FR-POS-0210` |
| `REQ-HR-0201` | Job Grades | **No** | `FR-POS-0206`–`FR-POS-0208` **(OD)** — existence depends on `OD-POS-002` |
| `REQ-HR-0202` | Salary Grade | **No** | `FR-POS-0206`–`FR-POS-0209` **(OD)** — existence depends on `OD-POS-002`; money content depends on `OD-POS-004` |
| `REQ-HR-0006` | Employee History | — | **Extended.** Position-change history is realized (`DEC-POS-0008`), on the terms `DEC-DEP-0016` was reversed to |
| `REQ-HR-0100`–`REQ-HR-0102` | Department | — | **Unchanged.** FP-008 adds no department behaviour and removes none, subject to `OD-POS-003` |

## Functional requirements

### FR-POS-0201 — Create a Position

A user holding `HR.Positions.Create`, acting within an authorized and active company, creates a Position with
a `Code`, a `Title`, and — subject to `OD-POS-002` — a grade reference. The position is created `Active`.
`TenantId` and `CompanyId` are stamped from trusted server context and are never accepted from the caller.

### FR-POS-0202 — Read a Position

A user holding `HR.Positions.View` retrieves a single Position by identifier within their authorized company
scope. A position outside that scope is indistinguishable from one that does not exist.

### FR-POS-0203 — List and search Positions

A user holding `HR.Positions.View` lists Positions within their authorized company scope, filtered optionally
by status, by grade, and by a title or code fragment. Results are ordered deterministically and paged.

### FR-POS-0204 — Update a Position's descriptive fields

A user holding `HR.Positions.Update` changes `Title`, `Code` subject to `BRULE-POS-0004`, and the grade
reference. Ownership (`TenantId`, `CompanyId`) and lifecycle status are **not** changed through this
operation; status has its own (`FR-POS-0205`).

### FR-POS-0205 — Deactivate and reactivate a Position

A user holding `HR.Positions.Deactivate` moves a Position between `Active` and `Inactive`. **Whether
deactivation is permitted while incumbents exist is `OD-POS-005`** and is not settled by this document.
Deactivation refuses new arrivals (`BRULE-POS-0013`).

### FR-POS-0206 — Create and read a Grade **(OD)**

A user holding the grade `Create` or `View` permission creates and reads grades within their authorized
company scope, with a `Code`, a `Name`, and a `RankOrder` (`DEC-POS-0006`). **Which grade entities exist is
`OD-POS-002`.**

### FR-POS-0207 — Update a Grade **(OD)**

A user holding the grade `Update` permission changes a grade's `Name`, its `Code` subject to
`BRULE-POS-0004`, and its `RankOrder` subject to `BRULE-POS-0007`.

### FR-POS-0208 — Deactivate and reactivate a Grade **(OD)**

A user holding the grade `Deactivate` permission moves a grade between `Active` and `Inactive`. Deactivation
is refused while `Active` dependents reference it and does **not** cascade (`BRULE-POS-0015`,
`DEC-POS-0013`).

### FR-POS-0209 — Maintain a Salary Grade's amounts **(OD)**

**Exists only if `OD-POS-004` selects a money-bearing Salary Grade.** A user holding
`HR.SalaryGrades.Update` sets a minimum, midpoint and maximum amount, ordered and non-negative
(`BRULE-POS-0008`), denominated in the owning Company's base currency (`DEC-POS-0015`). The amounts are
**informational**: FP-008 contains no value they constrain (`DEC-POS-0023`).

### FR-POS-0210 — Assign an Employee to a Position **(OD)**

Employee creation requires a `PositionId` in the same tenant, the same company, and `Active` status. **The
treatment of Employees that already exist is `OD-POS-001` and is not settled by this document.**

### FR-POS-0211 — Change an Employee's Position **(OD)**

An Employee's Position changes only through an explicit `ChangePosition` operation holding
`HR.Employees.Update` — never as a field on the ordinary profile update (`DEC-POS-0010`). Each change appends
one immutable `EmployeePositionAssignment` record atomically with the column change (`DEC-POS-0008`).
**Whether a separate promotion permission is required is named in `DEC-POS-0019`.**

### FR-POS-0212 — Read an Employee's position history

A user holding `HR.Employees.View` retrieves an Employee's position assignments in effective order, within
their existing employee read scope. The read adds no new authorization dimension.

### FR-POS-0213 — Filter Employee search by Position

Employee search accepts an optional `positionId` filter. This filters within the caller's existing employee
read scope; it does **not** become a fourth authorization dimension (`DEC-POS-0020`).

## Non-functional requirements

### NFR-POS-0301 — Scoped reads are indexed

Every Position and grade read is served by an index whose leading keys match the mandatory predicate order —
tenant, then company. No scoped read may be served by a scan that ignores a scope column.

### NFR-POS-0302 — Optimistic concurrency

Every Position and grade mutation carries a `RowVersion` and refuses a stale token, using the transport
convention in `Development-Standards.md`. The append-only assignment record carries none, because it is never
updated (`DEC-POS-0021`).

### NFR-POS-0303 — Authorization resolves live

Company scope, company status, and functional permission are resolved per operation and never cached from
login, matching `EmployeeScopeResolver` and `DepartmentScopeResolver`.

### NFR-POS-0304 — Cutover coverage is derived, not declared

Every new tenant-owned table is covered by the Shared→Dedicated copy manifest through the existing
model-derived mechanism, with dependency order produced by the foreign-key graph. No hand-maintained ordering
is introduced. The three sites that pin the current set **by name** are updated in one deliberate act
(`DEC-POS-0022`).

### NFR-POS-0305 — No foreign-key cycle is introduced

No table in this package may reference `tenant.Employees` while `tenant.Employees` references it.
`TenantCutoverCopyPlan.Build` fails with `CutoverCopyOrderUndecidable` on a cycle, which would break
Shared→Dedicated cutover for every tenant (`DEC-POS-0002`, `ADR-026` decision 7).

### NFR-POS-0306 — `BR-HR-0006`'s cardinality is unrepresentable to violate

The model admits no state in which one employee holds two current positions — not because a check rejects it,
but because a single column cannot express it (`DEC-POS-0021`).

## Explicitly not required

FP-008 introduces no employee salary, wage, or compensation column; no `Employee.ManagerId`; no
`Position.ReportsToPositionId` unless `OD-POS-006` says otherwise; no headcount, establishment or vacancy
concept; no position-scoped read of any aggregate; no cost-centre or GL mapping; no automatic code
generation; and no placeholder column for any of them.

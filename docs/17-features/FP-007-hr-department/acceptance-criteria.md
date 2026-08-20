---
document_id: FP-007-AC
title: HR Department — Acceptance Criteria
status: Draft — Owner Decision Required
version: 0.1
---

# FP-007 — Acceptance Criteria

Criteria marked **(OD)** are provisional and depend on an unresolved owner decision.

## Create and identity

- **AC-DEP-0001** — Creating a department with a code, a name and no parent produces an `Active` root
  department whose `TenantId` and `CompanyId` match the caller's trusted context.
- **AC-DEP-0002** — `TenantId` and `CompanyId` supplied in the request body are ignored, not honoured. A
  request naming another tenant's identifiers produces a department in the caller's own tenant.
- **AC-DEP-0003** — A second department with a code that normalizes to an existing code in the same company is
  refused with `409`, and the refusal comes from the unique index under concurrent creation, not only from a
  prior read.
- **AC-DEP-0004** — The same code is free in a different company.
- **AC-DEP-0005** — A blank or whitespace-only name or code is refused.

## Company isolation

- **AC-DEP-0006** — Reading a department belonging to a company outside the caller's authorized scope returns
  `404`, not `403`.
- **AC-DEP-0007** — A caller whose authorized company set resolves empty is refused; no read returns
  unfiltered results.
- **AC-DEP-0008** — Revoking the caller's company access mid-session refuses the next department read without
  requiring a new token.
- **AC-DEP-0009** — Deactivating the company mid-session refuses the next department write.

## Hierarchy

- **AC-DEP-0010** — A department created with a parent in the same company is placed beneath it.
- **AC-DEP-0011** — A department whose proposed parent belongs to another company is refused.
- **AC-DEP-0012** — Setting a department as its own parent is refused, and the database check constraint
  `CK_Departments_ParentIsNotSelf` refuses it as well when attempted directly in SQL.
- **AC-DEP-0013** — Given `A → B → C`, moving `A` beneath `C` is refused.
- **AC-DEP-0014** — Given `A → B → C`, moving `B` beneath a new root `D` carries `C` with it; `C`'s parent is
  unchanged and `C` is a descendant of `D`.
- **AC-DEP-0015** — Moving a department beneath an `Inactive` parent is refused.
- **AC-DEP-0016** — The hierarchy read returns ancestors in root-to-parent order and descendants in a
  deterministic order.
- **AC-DEP-0017** — Two concurrent re-parent operations that would together form a cycle cannot both succeed;
  the second is refused, and the resulting hierarchy is acyclic.

## Manager

- **AC-DEP-0018** — Assigning an employee of the same company as manager succeeds and is readable on the
  department.
- **AC-DEP-0019** — Assigning an employee from a different company is refused.
- **AC-DEP-0020** — Assigning a terminated employee is refused.
- **AC-DEP-0021** — Terminating an employee who is a manager does **not** clear the assignment; the department
  reads back with `manager.isTerminated = true`.
- **AC-DEP-0022** — Clearing a manager removes the assignment and the department reads back with a null
  manager.
- **AC-DEP-0023 (OD)** — Under `OD-DEP-003` reading (i): assigning an employee as manager of the department
  they belong to is refused, and moving an employee into the department they manage is refused.
- **AC-DEP-0024** — A department has at most one manager, enforced by the primary key of
  `tenant.DepartmentManagers` rather than by a handler check.

## Lifecycle

- **AC-DEP-0025** — A new department is `Active`.
- **AC-DEP-0026** — Deactivating a department with assigned employees succeeds; those employees keep their
  department and remain readable.
- **AC-DEP-0027** — Deactivating a department with `Active` children is refused.
- **AC-DEP-0028** — Creating an employee into an `Inactive` department is refused (`BR-HR-0009`).
- **AC-DEP-0029** — Changing an employee into an `Inactive` department is refused; changing an employee *out
  of* one succeeds.
- **AC-DEP-0030** — An `Inactive` department is still readable and still appears in lists, marked inactive.
- **AC-DEP-0031** — Reactivating restores the ability to receive employees.
- **AC-DEP-0032** — No API route, command, handler or repository method physically deletes a department, and
  an architecture guard asserts the absence rather than a reviewer noticing it.

## Employee membership

- **AC-DEP-0033 (OD)** — (`FR-DEP-0109`) Creating an employee without a department is refused; creating one
  with a department from another company is refused; creating one with an `Active` department in the same
  company succeeds.
- **AC-DEP-0035** — An employee's department cannot be changed through `PUT /api/hr/employees/{id}`; the field
  is not accepted there, and a request containing it is rejected rather than silently ignored.
- **AC-DEP-0036** — `POST /api/hr/employees/{id}/department` changes the department and refuses a stale
  `RowVersion` with `409`.
- **AC-DEP-0037** — Transferring an employee between branches leaves their department unchanged; changing an
  employee's department leaves their branch unchanged.
- **AC-DEP-0038** — Terminating an employee leaves their department intact.
- **AC-DEP-0039 (OD)** — The `OD-DEP-001` strategy actually chosen is implemented, and its terminal state is
  asserted: under A, no employee has a null department after migration; under B or C, the follow-up migration
  exists and is named; under D, the migration fails loudly if any null remains.

## Authorization

- **AC-DEP-0040** — Each of the four permissions is required by exactly the operations listed in
  [`authorization-model.md`](authorization-model.md), and by no others.
- **AC-DEP-0041** — `Platform.Tenant.Administer` alone does not authorize any department operation.
- **AC-DEP-0042** — Changing an employee's department requires `HR.Employees.Update`, **not**
  `HR.Departments.Update`.
- **AC-DEP-0043** — Every permission this package introduces is present in the composed `IPermissionCatalog`,
  so a role can actually be granted it. (FP-006P's failure — constants defined but never contributed, leaving
  every endpoint refusing every caller — must not recur.)

## Reads

- **AC-DEP-0044** — Every department query composes an explicit tenant and company predicate; none relies on a
  global filter alone, and an architecture guard asserts it.
- **AC-DEP-0045** — `DepartmentReadScope` cannot be constructed outside its resolver, and no query overload
  accepts a request without one.
- **AC-DEP-0046 (OD)** — Under `OD-DEP-005`: a caller authorized for one branch sees departments whose members
  are all in another branch.
- **AC-DEP-0047** — `employeeCount` reflects only employees within the caller's employee read scope.

## Concurrency and cutover

- **AC-DEP-0048** — Every department mutation refuses a stale `RowVersion` with `409`.
- **AC-DEP-0034** — The Shared→Dedicated copy plan **builds successfully** with Department and
  `DepartmentManagers` present, and orders Department after Company, Employee after Department, and
  `DepartmentManagers` after Employee. A model in which `Department` holds a direct manager foreign key is
  asserted to make the plan fail with `CutoverCopyOrderUndecidable`, so the reason for `DEC-DEP-0022` is
  recorded executably and cannot be undone by accident.
- **AC-DEP-0049** — A real cutover carries departments, department managers, employees and branch history, and
  source and destination counts agree for every one of them.
- **AC-DEP-0050** — Department's `RowVersion` is excluded from the copy projection.

## Ownership classification

- **AC-DEP-0051** — `Department` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity` and **does not
  implement `IBranchOwnedEntity`**, asserted by an architecture guard so the absence reads as a decision.
- **AC-DEP-0052** — `Department` has no `BranchId` column, asserted from the composed EF model rather than
  from a migration file.

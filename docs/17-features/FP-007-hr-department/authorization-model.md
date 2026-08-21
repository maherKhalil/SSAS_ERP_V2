---
document_id: FP-007-AUTH
title: HR Department — Authorization Model
status: Approved for Implementation
version: 1.0
---

# FP-007 — Authorization Model

## The three dimensions, unchanged

`ADR-025` decision 8 established that functional permission, company scope and branch scope are independent
questions with independent answers. FP-007 changes nothing about that and **adds no fourth dimension**.
Department is a filterable attribute, not an authorization boundary (`DEC-DEP-0019`).

| Dimension | Department operations |
|---|---|
| **Functional permission** | The four permissions below |
| **Company scope** | Resolved live by `ITenantCompanyAccessResolver`, exactly as for Employee |
| **Branch scope** | **Not applicable.** Department is not branch-owned (`DEC-DEP-0001`) |

`Platform.Tenant.Administer` widens company scope and grants **none** of the permissions below — an
administrator without `HR.Departments.View` cannot read a department, preserving the independence `ADR-025`
decision 8 exists to protect.

## D-permissions

**Decision `DEC-DEP-0017`.** Four permissions, contributed through the existing
`IPermissionCatalogContributor` seam by extending `HrPermissionCatalogContributor` and `HrPermissionNames`.
Naming follows the platform grammar of exactly three ASCII-identifier segments, `<Plane>.<Resource>.<Action>`.

| Permission | Description as a tenant administrator reads it |
|---|---|
| `HR.Departments.View` | View departments and the department hierarchy within the caller's authorized company scope |
| `HR.Departments.Create` | Create departments |
| `HR.Departments.Update` | Update department name and code, move departments within the hierarchy, and assign or clear department managers |
| `HR.Departments.Deactivate` | Deactivate and reactivate departments |

### What each permission covers, explicitly

| Operation | Permission | Why |
|---|---|---|
| Read one, list, read hierarchy | `View` | |
| Create | `Create` | |
| Rename, change code | `Update` | |
| **Move in the hierarchy** (`FR-DEP-0106`) | `Update` | A move is a structural edit of the department itself. It is *not* separated out, because a role able to rename a department but not move it is a distinction no requirement asks for |
| **Assign / clear manager** (`FR-DEP-0107`) | `Update` | Same reasoning. Flagged as a deliberate grouping rather than an omission, exactly as FP-006 flagged activate/deactivate living under `HR.Employees.Update` (`DEC-EMP-0030`) |
| **Deactivate and reactivate** | `Deactivate` | Separated from `Update` because it changes whether the department can receive employees — a materially different authority from editing its label |
| **Change an Employee's department** (`FR-DEP-0110`) | `HR.Employees.Update` | This writes to the **Employee**, not the Department. It is Employee authority, and giving it to `HR.Departments.Update` would let someone who may only edit org structure reassign people |
| **Filter employee search by department** (`FR-DEP-0111`) | `HR.Employees.View` | A filter within an existing scope, requiring no new authority |

**No `HR.Departments.Delete`.** Deletion does not exist (`BRULE-DEP-0016`), so the permission would authorize
nothing. **No `HR.Departments.Manage`** catch-all: a permission whose description cannot say what it lets
someone *do* is a permission nobody can grant responsibly.

## D-read scope — `OD-DEP-005`

Department reads resolve **tenant + company + functional permission**, and nothing else.

The recommended answer to `OD-DEP-005` is **company-scoped visibility**: a user authorized for Branch A only,
in Company X, sees every department in Company X — including departments whose current members are all in
Branch B, and including departments with no members at all.

**Why branch-derived visibility was rejected as the recommendation.** It would make a department's existence a
function of who is asking. Three concrete consequences: the Employee read DTO would name a department the
caller could not then fetch; an empty department would be invisible to every user simultaneously, including
the person who just created it; and the hierarchy would fracture, since a parent could be invisible while its
child was not. Each is a bug that would be reported as a bug.

**This remains an owner decision** because the question underneath — whether a department's name and structure
are sensitive across branches — is a business one, and a "no" changes the answer.

### The scope object

Department reads use a `DepartmentReadScope` following the `EmployeeReadScope` pattern exactly: a private
constructor, an `internal` factory called from one resolver, a materialized non-empty `AuthorizedCompanyScope`,
and no overload that omits it. The reasoning transfers unchanged — *a read that omitted a scope predicate must
not be something a reviewer has to notice, because it must not be something a caller can express*.

It carries **no branch scope**, and an architecture guard asserts that absence, so the next reader knows it was
decided rather than forgotten.

## Cross-company refusals

| Attempt | Result |
|---|---|
| Read a department in an unauthorized company | Not found — indistinguishable from nonexistent |
| Parent a department under one from another company | Refused, `BRULE-DEP-0008` |
| Assign a manager from another company | Refused, `BRULE-DEP-0011` |
| Assign an employee to a department in another company | Refused, `BRULE-DEP-0017` |

The first is *not found* rather than *forbidden* deliberately: a distinct "forbidden" response confirms the
department exists, which is itself a cross-company disclosure (`BR-PLT-0002`).

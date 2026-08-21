---
document_id: FP-008-AUTH
title: HR Position — Authorization Model
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — Authorization Model

## The three dimensions, unchanged

`ADR-025` decision 8 established that functional permission, company scope and branch scope are independent
questions with independent answers, and `ADR-026` decision 8 established that organizational structure is not
a fourth. FP-008 changes neither. Position is a filterable attribute, not an authorization boundary
(`DEC-POS-0020`).

| Dimension | Position operations |
|---|---|
| **Functional permission** | The permissions below |
| **Company scope** | Resolved live by `ITenantCompanyAccessResolver`, exactly as for Employee and Department |
| **Branch scope** | **Not applicable.** Position is not branch-owned (`DEC-POS-0001`) |

`Platform.Tenant.Administer` widens company scope and grants **none** of the permissions below — an
administrator without `HR.Positions.View` cannot read a position, preserving the independence `ADR-025`
decision 8 exists to protect.

## The permission set

**Decision `DEC-POS-0018`.** Four per entity, contributed through the existing `IPermissionCatalogContributor`
seam by extending `HrPermissionCatalogContributor` and `HrPermissionNames`. Naming follows the platform
grammar of exactly three ASCII-identifier segments, `<Plane>.<Resource>.<Action>`.

**Naming them is not registering them.** `HrPermissionNames` is the single source of the names and is not a
catalog; a constant added there without a definition in `HrPermissionCatalogContributor` produces a
permission that authorizes nothing and an endpoint that refuses every caller. That regression has happened
once in this codebase (FP-006P) and `TS-POS-0046` exists so it cannot happen silently again.

### Position

| Permission | Description as a tenant administrator reads it |
|---|---|
| `HR.Positions.View` | View positions within the caller's authorized company scope |
| `HR.Positions.Create` | Create positions |
| `HR.Positions.Update` | Update a position's title, code, and grade assignment |
| `HR.Positions.Deactivate` | Deactivate and reactivate positions |

### Grades **(OD-POS-002)**

The families that exist follow from the entity set, and so does the count:

| `OD-POS-002` | Families | Total new HR permissions |
|---|---|---|
| (i) three entities | `HR.Positions.*`, `HR.JobGrades.*`, `HR.SalaryGrades.*` | **12** |
| (ii) one ladder | `HR.Positions.*`, `HR.Grades.*` | **8** |
| (iii) money deferred | `HR.Positions.*`, `HR.JobGrades.*` | **8** |
| (iv) position only | `HR.Positions.*` | **4** |

### What each permission covers, explicitly

| Operation | Permission | Why |
|---|---|---|
| Read one, list, search | `HR.Positions.View` | |
| Create | `HR.Positions.Create` | |
| Retitle, change code, **change the grade reference** | `HR.Positions.Update` | Re-grading a position is a structural edit of the position. It is not separated out, because a role able to retitle a position but not re-grade it is a distinction no requirement asks for — flagged as a deliberate grouping rather than an omission, exactly as `DEC-DEP-0017` flagged hierarchy moves |
| **Deactivate and reactivate** | `HR.Positions.Deactivate` | Separated from `Update` because it changes whether the position can receive employees — a materially different authority from editing its label. **Both directions**, following `DEC-DEP-0025`: granting reactivation under ordinary `Update` would let a caller who may only retitle undo a closure someone holding the sensitive permission deliberately made |
| **Change an Employee's position** (`FR-POS-0211`) | `HR.Employees.Update` | This writes to the **Employee**, not the Position. See `DEC-POS-0019` and the flagged question below |
| **Read an Employee's position history** (`FR-POS-0212`) | `HR.Employees.View` | A read of the employee's own record within their existing scope |
| **Filter employee search by position** (`FR-POS-0213`) | `HR.Employees.View` | A filter within an existing scope, requiring no new authority |
| **Maintain salary amounts** (`FR-POS-0209`) | `HR.SalaryGrades.Update` | See the sensitivity note below |

**No `Delete` for anything.** Deletion does not exist (`BRULE-POS-0012`), so the permission would authorize
nothing. **No `Manage` catch-all**: a permission whose description cannot say what it lets someone *do* is one
nobody can grant responsibly. Both carried unchanged from `DEC-DEP-0017`.

## One deliberate departure from the minimal set, flagged rather than slipped in

If `OD-POS-004` puts money on Salary Grade, **`HR.SalaryGrades.View` is not merged into
`HR.Positions.View`.**

Pay bands are more sensitive than job titles. A single org-structure `View` would mean that everyone who may
read the organization chart may also read the pay structure — a disclosure decision taken by accident rather
than on purpose. This codebase already treats sensitivity, not resource identity, as sufficient grounds for a
separate permission: `DEC-EMP-0030` separated `HR.Employees.Terminate` and `HR.Employees.Transfer` from
`Update` on exactly that basis.

**It is recorded as a departure** because the FP-007 discipline is "four, and deliberately not more", and a
package that quietly grew the set while citing that discipline would be citing it dishonestly. Under
`OD-POS-004` option (i) the departure disappears with the money.

## The question `DEC-POS-0019` names rather than answers

Changing an Employee's position uses `HR.Employees.Update`, on the employee route prefix, and **not**
`HR.Positions.Update` and **not** `HR.Employees.Transfer`.

**What precedent settles.** `DEC-DEP-0018` and its 2026-08-21 amendment: a branch transfer moves a record
across an authorization boundary, so it has its own permission; a department change moves nothing across any
boundary, so it lives under ordinary update authority on the employee prefix. Position is a classification in
exactly the same sense — `PositionId` is not a security partition. Giving it to `HR.Positions.Update` would
let someone who may only edit the job catalog reassign people.

**What precedent does not settle.** A position change is frequently a **promotion**, and many organizations
gate promotions more tightly than an ordinary profile edit. Since this codebase already accepts sensitivity as
a reason to split a permission, the precedent covers the *classification* question and leaves the
*sensitivity* question open. If the owner wants promotions gated separately, the answer is a fifth employee
permission — `HR.Employees.ChangePosition` — and **it should be decided before roles are granted**, because
splitting a permission after the fact requires re-granting every role that held the broader one.

## Read scope

Position reads resolve **tenant + company + functional permission**, and nothing else.

`PositionReadScope` follows `DepartmentReadScope` exactly: a private constructor, an `internal` factory called
from one resolver, a materialized non-empty `AuthorizedCompanyScope`, and no overload that omits it. The
reasoning transfers unchanged — *a read that omitted a scope predicate must not be something a reviewer has to
notice, because it must not be something a caller can express.*

It carries **no branch scope**, and an architecture guard asserts that absence, so the next reader knows it
was decided rather than forgotten.

**This is SETTLED-BY-PRECEDENT given `DEC-POS-0001`, and the dependency is real.** `DEC-DEP-0019` settled the
visibility question for a company-owned org-structure record: branch scope filters *employee membership*, not
record existence, because making a record's existence a function of who is asking breaks the Employee read
DTO (it names something the caller cannot then fetch) and makes lists incoherent. All of that transfers.
**If `DEC-POS-0001` were rejected and Position given different ownership, this decision reopens with it.**

## Cross-company refusals

| Attempt | Result |
|---|---|
| Read a position in an unauthorized company | Not found — indistinguishable from nonexistent |
| Reference a grade from another company | Refused, `BRULE-POS-0011` |
| Assign an employee to a position in another company | Refused, `BRULE-POS-0016` |
| Read an employee's position history outside employee scope | Not found |

The first is *not found* rather than *forbidden* deliberately: a distinct "forbidden" response confirms the
position exists, which is itself a cross-company disclosure (`BR-PLT-0002`).

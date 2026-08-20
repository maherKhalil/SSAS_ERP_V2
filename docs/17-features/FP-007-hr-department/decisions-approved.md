---
document_id: FP-007-DEC
title: HR Department — Decisions
status: Draft — Owner Decision Required
version: 0.1
---

# FP-007 — Decisions

> **Not approved.** This document records decisions *proposed* by the FP-007A analysis. Decisions become
> binding only when the package is approved, and the package cannot be approved while `OD-DEP-001` …
> `OD-DEP-005` are open.

## Classification summary

| Decision | Subject | Classification |
|---|---|---|
| `DEC-DEP-0001` | Department ownership | **ENGINEERING-RECOMMENDATION** → `OD-DEP-002` |
| `DEC-DEP-0005` | Hierarchy representation | **ENGINEERING-SETTLED** |
| `DEC-DEP-0006` | Acyclicity invariant | **ENGINEERING-SETTLED** |
| `DEC-DEP-0009` | `BR-HR-0005` enforcement | **OWNER-DECISION-REQUIRED** → `OD-DEP-001` |
| `DEC-DEP-0014` | `BR-HR-0007` scope | **OWNER-DECISION-REQUIRED** → `OD-DEP-003` |
| `DEC-DEP-0016` | Department history | **OWNER-DECISION-REQUIRED** → `OD-DEP-004` |
| `DEC-DEP-0019` | Department read visibility | **OWNER-DECISION-REQUIRED** → `OD-DEP-005` |
| `DEC-DEP-0022` | Manager association shape | **ENGINEERING-RECOMMENDATION** |
| all others below | | **ENGINEERING-SETTLED** |

---

## Ownership and structure

**DEC-DEP-0001** — Department is **Tenant + Company owned, and not Branch owned**. It implements
`ITenantOwnedEntity` and `ICompanyOwnedEntity`; the absence of `IBranchOwnedEntity` is asserted by a guard.
*Rationale:* `BR-PLT-0013` scopes branch ownership to transactions, and a Department is a master organizational
record. Decisively, `ADR-024` makes branch-owned departments incoherent — every branch transfer would strand
the employee's department, and `ADR-024` provides for no such thing. **ENGINEERING-RECOMMENDATION**, pending
`OD-DEP-002`.

**DEC-DEP-0002** — Branch and Department are independent dimensions. A branch transfer never changes a
department and a department change never changes a branch.

**DEC-DEP-0003** — `TenantId` and `CompanyId` are stamped by the persistence boundary from trusted context and
are immutable thereafter, exactly as for Employee.

**DEC-DEP-0004** — A Company may have multiple root departments. Requiring a single root would force an
artificial "Company" node into every hierarchy.

## Hierarchy

**DEC-DEP-0005** — **Adjacency list** (`ParentDepartmentId`). Closure table and materialized path both
maintain derived state that can drift from its source; at department scale the read benefit does not pay for
that risk. A closure table may be added later as a pure read optimization derived from the adjacency list.
Full comparison in [`domain-model.md`](domain-model.md). **ENGINEERING-SETTLED.**

**DEC-DEP-0006** — The acyclicity invariant is: self-parent refused in the aggregate; cross-company parent
refused; inactive parent refused; **descendant-as-parent refused by walking upward from the proposed new
parent**. The ancestry is passed to the aggregate as evidence only the repository can produce, so a handler
that skipped the check cannot call the method. Re-parent operations serialize per `(TenantId, CompanyId)` for
the duration of the read-and-update, because two concurrent legal moves can jointly form a cycle.
`SERIALIZABLE` isolation was rejected: it would make an invisible transaction property responsible for a named
business rule. **ENGINEERING-SETTLED.**

**DEC-DEP-0007** — `Code` is user-entered and never generated, matching FP-006's treatment of
`EmployeeNumber` (`DEC-EMP-0011`). No numbering service is introduced.

**DEC-DEP-0008** — Moving a department moves its subtree. Descendants are never detached or re-parented by
the move.

## `BR-HR-0005`

**DEC-DEP-0009** — **OWNER-DECISION-REQUIRED (`OD-DEP-001`).** The treatment of Employees that already exist
without a Department is not decided by this package. The recommendation is: establish first whether any
production tenant holds Employee rows; if not, Option D; if so, Option A. Option C is not recommended under
any circumstances, because a binding rule enforced nowhere becomes folklore.

**DEC-DEP-0010** — For Employees created *after* FP-007, `DepartmentId` is required, same-company, and
`Active` at assignment. This part is not owner-dependent.

## Lifecycle

**DEC-DEP-0011** — Two states, `Active` and `Inactive`, with `Inactive` reversible. Employee's `Terminated` is
genuinely terminal; a department's off state is not, and modelling them alike would be wrong.

**DEC-DEP-0012** — Deactivation does not cascade to children and does not evict employees. Cascading would
destroy the information needed to reverse it; eviction would break `BR-HR-0005` for every affected employee.

**DEC-DEP-0013** — Terminating a manager does **not** clear the manager assignment. It is surfaced instead.
An automatic clear destroys the record that there had been a manager and makes an HR-structure write a side
effect of an unrelated operation.

## Manager

**DEC-DEP-0014** — **OWNER-DECISION-REQUIRED (`OD-DEP-003`).** `BR-HR-0007` has no field to constrain: **no
employee→manager reporting line exists in any repository authority**, and FP-006 deferred `ManagerId`
entirely. Department manager is not the same relationship. FP-007 proposes reading (iii) — enforce the
departmental reading now, transfer the personal reporting line — but **does not invent a reporting-line
model**, and records that the requirement catalog currently contains no requirement for one.

**DEC-DEP-0022** — The manager association lives in **`tenant.DepartmentManagers`**, not as a column on
`Department`. *Rationale:* a direct `Department.ManagerEmployeeId → Employee` foreign key, combined with
`Employee.DepartmentId → Department`, forms a cycle in the table-level FK graph, and `TenantCutoverCopyPlan`
fails hard with `CutoverCopyOrderUndecidable` on a cycle — verified in source. Shared→Dedicated cutover would
break for every tenant. A separate association table is a dependent of both and a principal of neither, so the
graph stays acyclic with full referential integrity and no change to Platform's cutover engine. See
[`data-model.md`](data-model.md) `RISK-DEP-001`. **ENGINEERING-RECOMMENDATION.**

## Employee changes

**DEC-DEP-0015** — An Employee's department changes only through an explicit `ChangeDepartment` operation.
`DepartmentId` gets no public setter and is not a field on the ordinary profile update. This mirrors
`BranchId`, and for the same reason: a mutable ownership-adjacent field is one that will eventually be mutated
by an ordinary update.

**DEC-DEP-0016** — **OWNER-DECISION-REQUIRED (`OD-DEP-004`).** Department-change history remains deferred as
FP-006 stated. What is lost is stated plainly: for the deferral period, *who moved between departments, when,
and why* is unrecoverable — only the current value and the audit stamps survive. Unlike most deferrals this
one is not free, because the missing period cannot be reconstructed later. `EmployeeDepartmentChanged` is
raised now so the seam exists if the owner chooses to introduce history.

## Reads and authorization

**DEC-DEP-0017** — Four permissions: `View`, `Create`, `Update`, `Deactivate`. No `Delete` (deletion does not
exist), no `Manage` catch-all. Hierarchy moves and manager assignment live under `Update`, flagged as a
deliberate grouping rather than an omission.

**DEC-DEP-0018** — Changing an Employee's department requires `HR.Employees.Update`, not
`HR.Departments.Update`. It writes to the Employee.

**DEC-DEP-0019** — **OWNER-DECISION-REQUIRED (`OD-DEP-005`).** Department is **not** an authorization
dimension — no read is scoped by department, and department is a filterable attribute only. Recommended
visibility is company-scoped: branch scope filters employee membership, not department existence.

## Exclusions

**DEC-DEP-0020** — Position is out of scope. `BR-HR-0006` remains binding and transfers to the package
introducing Position, on exactly the terms FP-006 used for Department. No `PositionId` placeholder is
introduced.

**DEC-DEP-0021** — Department carries no financial semantics: no cost centre, no GL mapping, no budget.

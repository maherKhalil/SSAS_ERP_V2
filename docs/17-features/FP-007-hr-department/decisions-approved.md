---
document_id: FP-007-DEC
title: HR Department — Decisions
status: Approved for Implementation
version: 1.0
---

# FP-007 — Decisions

> **Approved for Implementation, and built.** This document recorded decisions *proposed* by the FP-007A
> analysis. All five owner decisions (`OD-DEP-001` … `OD-DEP-005`) were closed between 2026-08-20 and
> 2026-08-21, and FP-007 shipped in four phases. The decisions below are binding.
>
> **The original decision text is preserved verbatim.** Where a ruling changed or closed a decision, an
> amendment is appended beneath it rather than rewritten over it — a decision record whose history is
> edited away cannot show why an answer changed, which is the one thing it exists to show. Amendments are
> dated and cite the ruling that produced them.

## Classification summary

| Decision | Subject | Classification |
|---|---|---|
| `DEC-DEP-0001` | Department ownership | **CLOSED** (`OD-DEP-002`, 2026-08-20) — recommendation adopted |
| `DEC-DEP-0005` | Hierarchy representation | **ENGINEERING-SETTLED** |
| `DEC-DEP-0006` | Acyclicity invariant | **ENGINEERING-SETTLED** |
| `DEC-DEP-0009` | `BR-HR-0005` enforcement | **CLOSED** (`OD-DEP-001`, 2026-08-20) — Option A, fail-loud |
| `DEC-DEP-0014` | `BR-HR-0007` scope | **CLOSED** (`OD-DEP-003`, 2026-08-20) — reading (iii) adopted |
| `DEC-DEP-0016` | Department history | **CLOSED** (`OD-DEP-004`, 2026-08-20) — deferral reversed |
| `DEC-DEP-0019` | Department read visibility | **CLOSED** (`OD-DEP-005`, 2026-08-20) — recommendation adopted |
| `DEC-DEP-0022` | Manager association shape | **ADOPTED** — recommendation implemented as written |
| `DEC-DEP-0023` … `DEC-DEP-0028` | Phase 4 transport surface | **RULED** (2026-08-20/21) |
| all others below | | **ENGINEERING-SETTLED** |

> `DEC-DEP-0016` is the one decision whose ANSWER REVERSED. The draft deferred department-change history and
> stated plainly what that would cost. The owner reversed the deferral, and FP-007 Phase 1 shipped
> `EmployeeDepartmentAssignment` as append-only history from the outset — so the loss the draft warned about
> never occurred. See the amendment beneath it.

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

> **Amendment 2026-08-20 (`OD-DEP-001` closed).** Option A adopted, with the collision case ruled
> explicitly. For every company holding legacy Employees, the migration creates exactly one Department with
> `Code`/`NormalizedCode` = `UNASSIGNED`, `Name` = `Unassigned`, `Status` = `Active`, assigns those
> Employees to it, and writes one initial history row each. **No system-origin discriminator is added** —
> no `IsSystem`, `Origin`, `OriginKind` or `IsBuiltIn` — and `UNASSIGNED` is not reserved globally.
>
> If a company ALREADY holds a Department whose `NormalizedCode` is `UNASSIGNED`, the migration **fails
> loudly and transactionally**. It does not reuse, rename, modify, delete or suffix it, and does not choose
> another code: each of those would silently attach real employees to a Department a customer created for
> their own purposes, and none could be undone by a later migration that cannot know what they meant.
>
> The collision check is a **separate pass over every affected company before any write**, so the common
> failure never writes at all rather than relying on rollback. The error names the offending companies and
> the one remedy. Shipped in `20260820140653_AddEmployeeDepartment`; proven by
> `EmployeeDepartmentMigrationSqlServerTests` (12 real-SQL scenarios).

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

> **Amendment 2026-08-20 (`OD-DEP-004` closed — DEFERRAL REVERSED).** The owner chose to build the history
> rather than defer it, and FP-007 Phase 1 shipped `EmployeeDepartmentAssignment` from the outset. The
> unrecoverable gap this decision warned about therefore never opened: *who moved between departments, when,
> and why* is recorded from the first Department onward.
>
> The record is append-only, tenant- and company-owned, and **not branch-owned** — a department change says
> nothing about a branch. There is no `EffectiveToUtc`: closing an interval would mean UPDATING the previous
> row, which is precisely the history mutation the model exists to prevent, so the interval is derived by
> ordering (`EffectiveFromUtc`, then the identifier as a tie-break). A null `SourceDepartmentId` marks the
> initial record and nothing else identifies it.
>
> The aggregate produces the record: `Employee.StampInitialAssignment` writes the first one in the same unit
> of work as the Employee, and `Employee.ChangeDepartment` appends each subsequent one atomically with the
> column change. The factories are `internal`, so nothing outside the domain assembly can fabricate a row.

## Reads and authorization

**DEC-DEP-0017** — Four permissions: `View`, `Create`, `Update`, `Deactivate`. No `Delete` (deletion does not
exist), no `Manage` catch-all. Hierarchy moves and manager assignment live under `Update`, flagged as a
deliberate grouping rather than an omission.

**DEC-DEP-0018** — Changing an Employee's department requires `HR.Employees.Update`, not
`HR.Departments.Update`. It writes to the Employee.

> **Amendment 2026-08-21.** Implemented as written, and reinforced at the transport layer: the route lives
> on the EMPLOYEE prefix (`POST /api/hr/employees/{employeeId}/change-department`) under
> `HR.Employees.Update`, and **not** under `HR.Employees.Transfer`. `DepartmentId` is a classification, not
> a security partition (`ADR-024` boundary): a branch transfer moves a record across an authorization
> boundary, a department change moves nothing across any. Permission bleed is proven in both directions —
> department permissions do not authorize this route, and employee permissions do not authorize the
> department routes.
>
> **Citation defect, recorded rather than silently corrected.** `EmployeeErrors.cs` cites `DEC-DEP-0031` for
> this rule. No such decision exists; the rule is this one, `DEC-DEP-0018`. The source comment is wrong and
> needs a one-line correction, which is out of scope for a documentation-only task.

**DEC-DEP-0019** — **OWNER-DECISION-REQUIRED (`OD-DEP-005`).** Department is **not** an authorization
dimension — no read is scoped by department, and department is a filterable attribute only. Recommended
visibility is company-scoped: branch scope filters employee membership, not department existence.

## Exclusions

**DEC-DEP-0020** — Position is out of scope. `BR-HR-0006` remains binding and transfers to the package
introducing Position, on exactly the terms FP-006 used for Department. No `PositionId` placeholder is
introduced.

**DEC-DEP-0021** — Department carries no financial semantics: no cost centre, no GL mapping, no budget.

---

## Transport surface (Phase 4, ruled 2026-08-20/21)

These decisions did not exist in the FP-007A analysis, which stopped at the application boundary. They were
ruled during Phase 4 and are numbered from the next free identifiers in this package's own sequence.

**DEC-DEP-0023** — The Department HTTP surface is **thirteen routes, one per handler**. Route and handler
stand in a 1:1 relationship, checked before any code was written; a handler without a route would be dead
application code, and a route without a handler would be a promise nothing keeps.

> **COUNT CORRECTED (2026-08-22, HR as-built cleanup): it is TWELVE, not thirteen.** Eleven routes on the
> `/api/hr/departments` prefix plus `POST /api/hr/employees/{employeeId}/change-department`, matching twelve
> handlers — so the 1:1 property the decision turns on holds exactly, and only the number was wrong. The
> arithmetic is fixed independently by the route inventory: FP-006's nine plus these twelve is the twenty-one
> that FP-008 took to forty-one, and `HrRouteInventoryTests` asserts the exact list at both counts. The
> decision's substance is unaffected; a wrong number in a ratified decision is corrected here rather than
> reproduced by whoever counts next.

Hierarchy movement is **two routes**, `POST /{id}/move` and `POST /{id}/move-to-root`, because Phase 2
shipped two commands with different validation — a parent change walks the ancestry, a root move has no
destination to check. A single route with a nullable `parentDepartmentId` was **rejected**: it would put the
choice of command in transport, and it would make the most destructive reading of the field the quiet one.

**DEC-DEP-0024** — Every state change is a **named `POST`**, and the surface exposes **no `DELETE` verb**.
Manager removal is `POST /{id}/manager/remove`, not `DELETE /{id}/manager`: ending an association is not
deleting a resource — the employee is untouched and only the association ends. This follows the employee
surface, which has no `MapDelete` at all. Enforced by
`TenantCutoverCopyArchitectureTests`-style token guard `The_hr_surface_exposes_no_delete_verb`, so the next
module inherits the convention rather than relitigating it.

`GET /{id}/children` returns **direct children only**. `REQ-HR-0101` specifies the adjacency model and no
full-tree contract; a caller wanting a whole tree walks it, which puts the cost of the depth in front of
whoever is paying it.

**DEC-DEP-0025** — Permission mapping. `View` guards the four reads; `Create` guards creation; `Update`
guards the ordinary edit, both hierarchy moves, and manager assign and remove. **`Deactivate` guards BOTH
directions of the lifecycle** — activate as well as deactivate. That permission governs whether a department
may receive employees, and both directions change that answer; granting reactivation under ordinary `Update`
authority would let a caller who may only rename a department undo a closure that someone holding the
sensitive permission deliberately made.

Functional permission and company scope remain **independent dimensions** (`ADR-025` decision 8 pattern):
holding `Platform.Tenant.Administer` widens scope and grants none of these.

**DEC-DEP-0026** — Department errors have their **own mapper and their own problem-code namespace**
(`department.*`). Routing department results through `EmployeeApiErrorMapper` is not a shortcut but a
defect, and it produced one: a department manager conflict answered `employee.number_conflict`, because that
mapper's only unique-constraint arm was written for the employee-number pre-check. A shared table cannot
stay honest once two resources disagree about what a shared persistence code means.

`HR.API` references **no Platform assembly** (`ADR-012`), so the mapper uses HR's own errors throughout. The
compiler enforced this during Phase 4 when a first draft reached for Platform's `Persistence.*` type.

**DEC-DEP-0027** — `Persistence.UniqueConstraint` is **context-dependent**, and the context is resolved by
the caller who knows the operation rather than by a switch that does not:

- on **create and update** it is the unique index on `NormalizedCode`, so it answers
  `department.code_conflict` — the same answer the pre-check gives, making a race and a sequential duplicate
  indistinguishable;
- on **assign-manager** the only unique constraint is `PK_DepartmentManagers`, so the route pre-translates
  to `concurrency.conflict` before mapping. A PK-race loser and a rowversion loser must be
  indistinguishable: both mean *somebody got there first, reload and retry*, and telling them apart would
  leak which internal check fired.

**The wire equivalence is the contract, not the error identity.** The two errors map to the same problem
code; if either is renamed or remapped, both arms must move together or the translation silently starts
disclosing the difference it exists to hide. Scoped absence answers `404 department.not_found` for unknown,
cross-tenant, cross-company and out-of-scope alike.

**DEC-DEP-0028** — Two concurrent `AssignManager` calls from the same read state have **two sanctioned
outcomes**, and both are correct:

- **both succeed** — assignment is an upsert, and assigning a manager does not touch the `Departments` row,
  so the second caller's rowversion token is still fresh, it sees the committed association and replaces it;
- **one succeeds and the loser fails gracefully**, with `ConcurrencyConflict` or `UniqueConstraintViolation`
  depending on which check fired first.

What must always hold is that **no exception escapes and no second row appears**. Asserting "exactly one
loser" would be wrong — it fails on correct behaviour. The primary key on `DepartmentId` is what makes a
second row unrepresentable, and that is the design rather than a backstop.

## Shared→Dedicated cutover

**DEC-DEP-0029** — `Department`, `DepartmentManager` and `EmployeeDepartmentAssignment` enter the E3
tenant-owned copy manifest **by construction, not by registration**: `TenantCutoverCopyPlan.Build` reflects
over the composed model and includes every non-owned `ITenantOwnedEntity` with a table name. There is no
hand-maintained list to forget.

Three guards keep that honest: `C6_1`/`C6_2` assert the **exact** seven-entity manifest by name, so a new
tenant-owned entity fails loudly rather than being silently absent; `C6_14` proves a contributor-free model
silently loses every HR table, so the composition is load-bearing; and `C6_15` asserts the **topological
order**, including Departments before Employees — the edge `Employee.DepartmentId` created.

That ordering is the practical consequence of `DEC-DEP-0022`. Had the manager been a column on `Department`,
`Department` would reference `Employee` while `Employee` references `Department`, the copy plan's sort would
find a cycle, and `TenantCutoverCopyPlan.Build` would fail with `CutoverCopyOrderUndecidable` — breaking
Shared→Dedicated cutover for every tenant.

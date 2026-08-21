---
document_id: FP-008-BR
title: HR Position — Business Rules
status: Approved for Implementation
version: 1.0
---

# FP-008 — Business Rules

## Inherited rules and where they land

| Source rule | Statement | Disposition in FP-008 |
|---|---|---|
| `BR-HR-0006` | Every employee must have one active position | **Realized.** `BRULE-POS-0016` binds every Employee — `OD-POS-001` ruled `PositionId` `NOT NULL` from day one and `OD-POS-005` ruled *active* to qualify the assignment, so there is no unbound cohort and no ambiguity left |
| `BR-HR-0007` | An employee cannot directly manage themselves | **Transferred unchanged.** `OD-POS-006` deferred the position hierarchy, so `DEC-DEP-0014` reading (iii) remains in force: the departmental half is enforced in FP-007, and the personal reporting line still has no field anywhere in the product |
| `BR-HR-0005` | Every employee belongs to exactly one department | **Untouched.** `OD-POS-003` ruled Position independent of Department, so `Employee.DepartmentId` remains the single authority and nothing in FP-008 moves it |
| `BR-PLT-0002` | Company isolation | **Realized.** `BRULE-POS-0002`, `BRULE-POS-0011`, `BRULE-POS-0017` |
| `BR-PLT-0003` | Soft delete | **Realized.** `BRULE-POS-0012` — no physical delete |
| `BR-PLT-0004` | Audit trail | **Realized.** Every Position and grade carries the `IAuditableEntity` stamps |
| `BR-PLT-0006` | Numbering sequences | **Deferred, as before.** `Code` is user-entered (`DEC-POS-0024`) |
| `BR-PLT-0013` | Branch owns transactions | **Respected by exclusion.** A Position is not a transaction and is not branch-owned (`DEC-POS-0001`) |

## Rules

### Identity and ownership

**BRULE-POS-0001** — A Position's `TenantId` and `CompanyId` are stamped by the persistence boundary from
trusted server context on insert. Neither is accepted from a caller, and neither may change after creation.
The same holds for `JobGrade` and `SalaryGrade`.

**BRULE-POS-0002** — A Position belongs to exactly one Company. Every relationship it participates in — its
grade and its holders — must resolve within that same Company. It has no department relationship at all
(`OD-POS-003`).

**BRULE-POS-0003** — A Position has no `BranchId`. It is a company-wide job definition and may be held by
employees in any branch of its company (`DEC-POS-0001`).

### Code and title

**BRULE-POS-0004** — `Code` is unique within a Company, compared on a normalized, binary-collated value so
the uniqueness index is authoritative under concurrent creation rather than advisory. Codes that normalize
alike collide. **The scope is per company** (`OD-POS-003` ruled Position independent of Department, so there is
department if positions become department-owned.**

**BRULE-POS-0005** — `Title` is required and must not be blank after trimming. It is **not** unique: two
positions in different parts of the organization may legitimately share a title, and forcing uniqueness would
make "Accountant" available to exactly one part of the company.

**BRULE-POS-0006** — `Code` may be changed by an authorized user, subject to `BRULE-POS-0004`. It is not
regenerated and is not derived from the title.

### Grades

> `OD-POS-002` ruled three aggregates, so every rule in this section applies to both `JobGrade` and
> `SalaryGrade` unless it names one.

**BRULE-POS-0007** — A grade's `RankOrder` is a positive integer, unique within its ladder and within its
Company. It is authoritative data, not derived from the code (`DEC-POS-0006`).

**BRULE-POS-0008** — A Salary Grade's amounts, where present, are
non-negative and satisfy `Minimum ≤ Midpoint ≤ Maximum`, enforced by a check constraint. They are denominated
in the owning Company's base currency and carry no currency of their own (`DEC-POS-0015`).

**BRULE-POS-0009** — A Position references at most one grade of each retained kind, in the same Tenant and
the same Company, with status `Active` at the moment of assignment.

**BRULE-POS-0010** — A Job Grade references at most one Salary
Grade, in the same Tenant and the same Company. **The reference points from Job Grade to Salary Grade and
never the reverse**, so the foreign-key graph stays a tree and the cutover order stays decidable
(`DEC-POS-0002`, `NFR-POS-0305`).

**BRULE-POS-0011** — A grade belongs to exactly one Company, and a Position may not reference a grade from
another Company.

### Lifecycle

**BRULE-POS-0012** — A Position is never physically deleted. `Inactive` is its terminal state, and it remains
readable and referenceable so historical assignment records keep their meaning (`BR-PLT-0003`,
`DEC-POS-0011`). The same holds for every grade entity.

**BRULE-POS-0013** — An `Inactive` Position may not receive a new Employee. This refuses both Employee
creation into it and an Employee position change into it.

**BRULE-POS-0014** — *(`OD-POS-005`, assignment reading)* Deactivating a Position does **not** remove,
reassign, or invalidate the Employees already holding it. Those Employees remain incumbents, and `BR-HR-0006`
remains satisfied for them — "one active position" qualifies the *assignment*, not the position's lifecycle
status. This mirrors `BRULE-DEP-0015` exactly.

The alternative reading was considered and rejected by the owner: had *active* qualified the position's
status, deactivation would have broken `BR-HR-0006` for every incumbent at that instant, and would have had to
be refused — using one rule to break another, which FP-007 declined to do.

**BRULE-POS-0015** — A grade with `Active` dependents may not be deactivated until those dependents are
deactivated or re-pointed. Deactivation does not cascade (`DEC-POS-0013`).

### Employee membership

**BRULE-POS-0016** — *(`BR-HR-0006`)* Every Employee created after FP-008 references exactly one Position, in
the same Tenant and the same Company as the Employee, with status `Active` at the moment of assignment.

**BRULE-POS-0017** — An Employee's Position changes only through the explicit `ChangePosition` operation. It
is not a writable field on the ordinary profile update (`DEC-POS-0010`).

**BRULE-POS-0018** — Every position change appends exactly one immutable `EmployeePositionAssignment` record,
atomically with the column change. The record is never updated and never deleted; a correction is another
position change, never a rewrite (`DEC-POS-0008`).

**BRULE-POS-0019** — An Employee's branch transfer does not change their Position, an Employee's department
change does not change their Position, and a Position change changes neither their Branch nor their
Department. The three dimensions are independent (`ADR-024`, `DEC-DEP-0002`).

**BRULE-POS-0020** — A terminated Employee retains their Position. Termination does not clear it, because a
historical employment record without a job is unreadable — the same reasoning as `BRULE-DEP-0020`.

**BRULE-POS-0021** — An Employee holds **at most one current Position**, and the model admits no state in
which they hold two. This is enforced by the shape of `Employee.PositionId` rather than by a check
(`DEC-POS-0021`).

**BRULE-POS-0022** — *(`OD-POS-001`)* **There are no Employees that existed before FP-008.** The migration
asserts it: it counts `tenant.Employees` before any DDL and fails loudly and transactionally if the count is
not zero (`DEC-POS-0026`). `BRULE-POS-0016` therefore binds **every** Employee in the product without
exception, and no cohort is governed by a different rule.

This is the one place FP-008 diverges materially from FP-007. `BRULE-DEP-0021` had to carve out a
pre-existing cohort and `DEC-DEP-0009` seeded a permanent `UNASSIGNED` Department for them. Nothing here does,
because the operational fact was established before the migration was authored rather than assumed after.

### Rules this package deliberately does not state

**No rule limits how many Employees may hold one Position.** A Position is a job definition, not a budgeted
seat (`DEC-POS-0025`). If the owner's meaning of "Position" is a single seat, this omission is wrong and the
model needs revisiting at its root — which is why the assumption is recorded rather than left implicit.

**No rule constrains an employee's pay against a Salary Grade's range**, because no employee pay value exists
in this package to constrain (`DEC-POS-0023`). Stating such a rule would mark it as enforced when nothing in
the product can violate it, which is the failure `ADR-026` decision 10 names.

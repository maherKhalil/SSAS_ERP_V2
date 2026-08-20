---
document_id: FP-007-BR
title: HR Department — Business Rules
status: Draft — Owner Decision Required
version: 0.1
---

# FP-007 — Business Rules

## Inherited rules and where they land

| Source rule | Statement | Disposition in FP-007 |
|---|---|---|
| `BR-HR-0005` | Every employee belongs to exactly one department | **Realized subject to `OD-DEP-001`.** `BRULE-DEP-0017` for new employees; existing rows depend on the owner decision |
| `BR-HR-0007` | An employee cannot directly manage themselves | **Partially realized subject to `OD-DEP-003`.** `BRULE-DEP-0012` under reading (i); the personal reporting line is transferred onward |
| `BR-HR-0008` | Circular department hierarchies are prohibited | **Realized.** `BRULE-DEP-0009` |
| `BR-HR-0009` | Inactive departments cannot receive new employees | **Realized.** `BRULE-DEP-0014` |
| `BR-PLT-0002` | Company isolation | **Realized.** `BRULE-DEP-0002`, `BRULE-DEP-0008`, `BRULE-DEP-0011` |
| `BR-PLT-0003` | Soft delete | **Realized.** `BRULE-DEP-0016` — no physical delete |
| `BR-PLT-0004` | Audit trail | **Realized.** Every Department carries the `IAuditableEntity` stamps |
| `BR-PLT-0013` | Branch owns transactions | **Respected by exclusion.** Department is not a transaction and is not branch-owned (`DEC-DEP-0001`) |
| `BR-HR-0006` | Every employee must have one active position | **Transferred** to the package introducing Position (`DEC-DEP-0020`) |

## Rules

### Identity and ownership

**BRULE-DEP-0001** — A Department's `TenantId` and `CompanyId` are stamped by the persistence boundary from
trusted server context on insert. Neither is accepted from a caller, and neither may change after creation.

**BRULE-DEP-0002** — A Department belongs to exactly one Company. Every relationship it participates in —
parent, manager, employee membership — must resolve within that same Company.

**BRULE-DEP-0003** — A Department has no `BranchId`. It is a company-wide organizational unit and may contain
employees from any branch of its company (`DEC-DEP-0001`, subject to `OD-DEP-002`).

### Code and name

**BRULE-DEP-0004** — `Code` is unique within a Company, compared on a normalized, binary-collated value so the
uniqueness index is authoritative under concurrent creation rather than advisory. Codes that normalize alike
collide.

**BRULE-DEP-0005** — `Name` is required and must not be blank after trimming. It is not unique: two
departments in different parts of the hierarchy may legitimately share a name.

**BRULE-DEP-0006** — `Code` may be changed by an authorized user, subject to `BRULE-DEP-0004`. It is not
regenerated, and it is not automatically derived from anything.

### Hierarchy

**BRULE-DEP-0007** — A Department has at most one parent. A Department with no parent is a root; a Company may
have more than one root.

**BRULE-DEP-0008** — A parent Department must belong to the same Tenant and the same Company as its child. A
cross-company parent is refused.

**BRULE-DEP-0009** — *(`BR-HR-0008`)* A Department may not be its own ancestor. Specifically: a Department may
not be its own parent, and a Department's new parent may not be any of its own descendants. Enforcement is
specified in [`domain-model.md`](domain-model.md) and is transactional, not advisory.

**BRULE-DEP-0010** — Moving a Department moves its entire subtree. Descendants are not re-parented, orphaned,
or detached by the move.

### Manager

**BRULE-DEP-0011** — `ManagerEmployeeId`, when present, must reference an Employee in the same Tenant and the
same Company as the Department.

**BRULE-DEP-0012** — *(`BR-HR-0007`, reading (i), subject to `OD-DEP-003`)* An Employee may not be the manager
of the Department they themselves belong to.

**BRULE-DEP-0013** — A terminated Employee may not be assigned as a Department's manager. An Employee who is
already a manager and is subsequently terminated does **not** have the reference cleared automatically; the
Department is reported as having a terminated manager and an authorized user must reassign it
(`DEC-DEP-0013`).

### Lifecycle

**BRULE-DEP-0014** — *(`BR-HR-0009`)* An `Inactive` Department may not receive a new Employee. This refuses
both Employee creation into it and an Employee department change into it.

**BRULE-DEP-0015** — Deactivating a Department does **not** remove, reassign, or invalidate the Employees
already assigned to it. Those Employees remain members, and `BR-HR-0005` remains satisfied for them.

**BRULE-DEP-0016** — A Department is never physically deleted. `Inactive` is its terminal state, and it
remains readable and referenceable so historical Employee records keep their meaning (`BR-PLT-0003`).

**BRULE-DEP-0022** — A Department with `Active` child departments may not be deactivated until those children
are deactivated or moved. Deactivating a parent does not cascade (`DEC-DEP-0012`).

### Employee membership

**BRULE-DEP-0017** — *(`BR-HR-0005`)* Every Employee created after FP-007 references exactly one Department,
in the same Tenant and the same Company as the Employee, with status `Active` at the moment of assignment.

**BRULE-DEP-0018** — An Employee's Department changes only through the explicit `ChangeDepartment` operation.
It is not a writable field on the ordinary profile update (`DEC-DEP-0015`).

**BRULE-DEP-0019** — An Employee's branch transfer does not change their Department, and a Department change
does not change their Branch. The two dimensions are independent (`ADR-024`, `DEC-DEP-0002`).

**BRULE-DEP-0020** — A terminated Employee retains their Department. Termination does not clear membership,
because a historical employment record without a department is unreadable.

**BRULE-DEP-0021** — Employees that existed before FP-007 are governed by `OD-DEP-001`. Until that decision is
recorded, this package states no rule for them, and it does not claim `BR-HR-0005` is satisfied.

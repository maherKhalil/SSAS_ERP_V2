---
document_id: FP-006-BR
title: HR Employee Business Rules
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Business Rules

> Approved for Implementation — rules reflecting the settled FP-006A decisions.

### BRULE-EMP-0001 — Initial state

Every newly created Employee begins in `Active`. An employee is hired into employment, so no separate activation step is required and none is defined. This deliberately differs from `Company` (`BRULE-CMP-0001`), whose `Inactive`-on-create rule exists to express readiness prerequisites that do not apply to a person.

### BRULE-EMP-0002 — Approved transitions

Only these transitions are permitted:

- `None` to `Active` (Create);
- `Active` to `Inactive` (Deactivate);
- `Inactive` to `Active` (Activate);
- `Active` to `Terminated` (Terminate);
- `Inactive` to `Terminated` (Terminate).

All unlisted transitions are rejected, including activating an already-`Active` employee, deactivating an already-`Inactive` employee, and any transition out of `Terminated`.

### BRULE-EMP-0003 — Terminated is terminal

A `Terminated` Employee shall not transition again. No rehire operation exists in Milestone 1.

### BRULE-EMP-0004 — Post-termination restrictions

A `Terminated` Employee shall not be assigned new business transactions (`BR-HR-0004`), shall not be transferred, and shall not have its profile updated. It remains retrievable and searchable for audit and reporting.

### BRULE-EMP-0005 — No physical deletion

Physical Employee deletion is prohibited. No delete command, repository method, permission, endpoint, cascade, or routine database operation exists, and a persistence guard rejects physical deletion. The same prohibition applies to `EmployeeBranchAssignment` (`BR-PLT-0003`).

### BRULE-EMP-0006 — EmployeeId authority

`EmployeeId` is a server-generated, nonempty, immutable, never-reused `Guid`, assigned at creation and never accepted from a caller (`ADR-013`).

### BRULE-EMP-0007 — Employee number

`EmployeeNumber` is:

- required at creation;
- **user-entered** in Milestone 1;
- **immutable** after creation;
- trimmed, nonempty after trim, and free of control characters;
- limited to 64 characters, applied to both the accepted input and the normalized value;
- normalized as exactly `Trim().ToUpperInvariant()` with ordinal comparison and no Unicode NFC/NFD/NFKC/NFKD normalization;
- stored with display casing preserved alongside the normalized value;
- Unicode-permitted; it is not ASCII-only.

The 64-character limit and the normalization rule follow the established `CompanyCode` convention (`DEC-CMP-0006`, `DEC-CMP-0007`).

### BRULE-EMP-0008 — Employee number uniqueness scope

`EmployeeNumber` is unique **within a company**, by normalized value, enforced by a `(TenantId, CompanyId, NormalizedEmployeeNumber)` unique index using `Latin1_General_100_BIN2` (`BR-HR-0001`). The database constraint is authoritative under concurrent creation; an application pre-check is an optimization, not the authority. The same normalized number may exist in a different company or a different tenant.

### BRULE-EMP-0009 — Branch does not participate in employee number uniqueness

`BranchId` shall **not** appear in the employee-number unique index. Uniqueness that is company-wide must not include the branch dimension (`ADR-023`, *For HR*). Consequently two employees in different branches of the same company cannot share an employee number, which is the intended reading of `BR-HR-0001`.

### BRULE-EMP-0010 — National identity

Where recorded, `NationalId` is unique **within a company**, by normalized value, on the same scope and with the same normalization and collation as the employee number (`BR-HR-0002`). `NationalId` is optional in Milestone 1; the uniqueness constraint applies only to rows where a value is present.

### BRULE-EMP-0011 — Employment and termination dates

`EmploymentDate` is required at creation. `TerminationDate` is null until termination and required at termination. `EmploymentDate` shall never be later than `TerminationDate` (`BR-HR-0003`). Both are stored as UTC (`BR-PLT-0005`).

### BRULE-EMP-0012 — Immutable tenant ownership

The owning `TenantId` is derived only from the trusted current tenant context, is assigned server-side at creation, and is immutable thereafter.

### BRULE-EMP-0013 — Immutable company ownership

The owning `CompanyId` is adopted only from the trusted company execution context (`ADR-025`), is confirmed rather than trusted when supplied, and is **immutable** after creation. An Employee never moves between companies. A person employed by a different legal entity is a different employment relationship, and Milestone 1 represents that as a separate Employee record.

### BRULE-EMP-0014 — Branch ownership is immutable through ordinary update

`BranchId` is assigned by the server at creation from the trusted branch execution context and shall never change through ordinary entity or property mutation. The existing branch write boundary refuses any modified `BranchId` that is not covered by an exact sanctioned transfer declaration (`ADR-023` decision 18, `ADR-024` decisions 2 and 3).

### BRULE-EMP-0015 — Transfer is an explicit operation

An Employee's branch shall change only through the dedicated `TransferEmployee` operation, which carries its own command, route, permission, and audit record. `UpdateEmployee` shall not accept `BranchId` in its contract at all (`ADR-024` decisions 3 and 10).

### BRULE-EMP-0016 — Transfer timing

A transfer takes effect immediately upon successful commit. Milestone 1 defines no future-dated transfer, no scheduled transfer, and no cancellation operation. A mistaken transfer is corrected by another explicit authorized transfer, which appends a further history record (`ADR-024` decision 9).

### BRULE-EMP-0017 — Transfer prohibited for a terminated employee

A `Terminated` Employee shall not be transferred (`ADR-024` decision 10).

### BRULE-EMP-0018 — Branch assignment history is append-only

`EmployeeBranchAssignment` records are immutable. No record shall be updated or physically deleted, including to close an effective interval. Each branch change appends exactly one record, written in the same transaction as the Employee's branch change (`ADR-024` decisions 5 and 7).

### BRULE-EMP-0019 — Initial branch assignment record

Employee creation shall write an initial assignment record with `SourceBranchId = null` and `DestinationBranchId` equal to the stamped `Employee.BranchId`, in the same transaction. An Employee with no branch-assignment history is a defect (`ADR-024` decision 5).

### BRULE-EMP-0020 — Dual branch authorization for transfer

Transfer shall authorize the source branch and the destination branch independently. The source is the trusted branch execution context; the destination is authorized separately through the live branch access resolver, which intersects with active branches. Both shall be revalidated inside the operation (`ADR-024` decision 6).

### BRULE-EMP-0021 — Inactive source branch recovery

An Employee whose current branch has been deactivated may be transferred **out** of that inactive source branch only when the actor holds `Platform.Tenant.Administer`, the destination branch is active and belongs to the same tenant, the operation is the explicit `TransferEmployee` operation, the transfer is audited, and normal destination authorization succeeds. The exception is one-directional, grants no ordinary read or write authority over the inactive branch, and permits only the transfer needed to relocate the Employee (`ADR-024` decision 12).

### BRULE-EMP-0022 — Historical branch attribution

Current-state questions shall read `Employee.BranchId`. Point-in-time questions shall read `EmployeeBranchAssignment`, selecting the record with the greatest `EffectiveFromUtc` less than or equal to the instant in question. Mixing current-branch attribution into a historical report is a defect (`ADR-024` decision 8).

### BRULE-EMP-0023 — Optimistic concurrency

Every mutating Employee operation supplies an expected rowversion. A stale rowversion returns a conflict, commits nothing, and raises no event. `Employee.RowVersion` is the serialization point for transfer, ensuring the branch value and the assignment history cannot diverge (`ADR-024` decision 7).

### BRULE-EMP-0024 — Three independent authorization dimensions

An Employee operation requires a functional permission **and** an authorized company scope **and** an authorized branch scope. None implies another. `Platform.Tenant.Administer` widens company and branch scope only and grants no HR functional authority (`ADR-025` decision 8).

### BRULE-EMP-0025 — Explicit scope predicates on reads

Every Employee read carries an explicit `CompanyId` predicate and an explicit `BranchId` predicate. "All authorized branches" and "all authorized companies" mean the materialized set of identifiers currently authorized to the caller, never the omission of a predicate (`BR-PLT-0016`, `ADR-023` decision 22, `ADR-025` decision 10).

### BRULE-EMP-0026 — Department and Position deferral

`BR-HR-0005` (every employee belongs to exactly one department), `BR-HR-0006` (every employee must have one active position), and `BR-HR-0007` (an employee cannot directly manage themselves) are **retained as binding business rules** and are **not** discarded, weakened, or reinterpreted. Their enforcement is deferred until the Department (`REQ-HR-0100`) and Position (`REQ-HR-0200`) aggregates exist.

Milestone 1 Employee creation does not require, accept, or persist a `DepartmentId`, `PositionId`, or `ManagerId`, and introduces **no placeholder entity, table, column, or foreign key** for them. The obligation transfers to the package that introduces Department and Position, which must add the associations and enforce all three rules at that time, including for employees created under Milestone 1.

### BRULE-EMP-0027 — Automatic employee numbering deferral

`BR-PLT-0006` lists Employee Number among the numbering sequences configurable per company. That automatic generation is **deferred**, not discarded: Milestone 1 introduces no numbering-sequence table, service, or configuration, because no such mechanism exists in the platform and FP-005 explicitly excluded building one.

`EmployeeNumber` is designed as a required input to the create command rather than a client-owned identity, so a future generator can supply the value server-side before the aggregate is constructed, with no change to the column, index, constraint, or resource shape. The only contract change a generator would require is making the request field optional where a sequence is configured, which is additive.

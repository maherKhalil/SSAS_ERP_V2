---
document_id: FP-006-DOM
title: HR Employee Domain Model
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Domain Model

> Approved for Implementation — model reflecting the settled FP-006A decisions.

## Bounded context

**HR Employee**

This bounded context owns the existence, stable identity, employment identity, lifecycle state, current branch assignment, and branch-assignment history of an Employee within one Tenant and one Company. It does **not** own departments, positions, reporting lines, employee documents, payroll, attendance, company lifecycle, branch lifecycle, or the platform ownership machinery it consumes.

## Employee aggregate

`Employee` is the aggregate root:

```csharp
public sealed class Employee
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IBranchOwnedEntity
```

Fields:

- `EmployeeId: Guid` (aggregate identity, `Id`);
- `TenantId: Guid` (owning tenant; `ITenantOwnedEntity`);
- `CompanyId: Guid` (owning company; `ICompanyOwnedEntity`);
- `BranchId: Guid` (current operating branch; `IBranchOwnedEntity`);
- `EmployeeNumber`;
- `NormalizedEmployeeNumber`;
- `NationalId` (optional);
- `NormalizedNationalId` (optional);
- `FullName`;
- `EmploymentDate`;
- `TerminationDate` (null until termination);
- `Status`;
- `StatusChangedUtc`, `StatusChangedBy`;
- `StatusChangeReasonCode`;
- `CreatedUtc`, `CreatedBy` (`IAuditableEntity`);
- `ModifiedUtc`, `ModifiedBy` (`IAuditableEntity`);
- SQL Server `RowVersion`.

No `DepartmentId`, `PositionId`, or `ManagerId` field exists in Milestone 1 (`BRULE-EMP-0026`). No speculative HR attribute — salary, grade, contract type, nationality, address, contact detail — is introduced; each belongs to a later package with its own requirements.

### Ownership consequences

`Employee` is the first entity in the product to carry all three ownership dimensions.

Through `ITenantOwnedEntity` it inherits the existing global tenant query filter, server-side tenant assignment on insert, post-creation `TenantId` immutability, restricted delete behavior, and audit-metadata stamping.

Through `IBranchOwnedEntity` it enters `TenantDbContext.ApplyBranchRulesAsync`, which stamps `BranchId` on insert from the trusted execution context, confirms rather than trusts a supplied value, refuses any modified `BranchId` outside a sanctioned transfer declaration, and refuses cross-branch modification and deletion. Employee is the first production entity to reach that path, which is why the `ADR-023` LOW-1 proofs are an obligation of this package.

Through `ICompanyOwnedEntity` it enters the company write boundary introduced by `ADR-025` decision 9, which stamps `CompanyId` on insert from the trusted company context, confirms rather than trusts a supplied value, refuses post-creation change, and refuses cross-company modification and deletion.

`CompanyId` and `BranchId` are **independent sibling dimensions** beneath `TenantId`. Neither is derived from the other, and no invariant relates the selected company to the active branch (`ADR-023`, `ADR-025`).

## Responsibilities

The aggregate:

- creates an Employee in `Active`;
- preserves immutable `EmployeeId`, `TenantId`, `CompanyId`, and employee number;
- treats `BranchId` as immutable except through the sanctioned transfer operation;
- enforces the approved transition graph;
- enforces that `EmploymentDate` is never later than `TerminationDate`;
- refuses transfer and profile update for a `Terminated` employee;
- records safe status-change metadata;
- raises safe lifecycle and transfer events;
- updates only mutable profile fields through the approved update operation;
- rejects stale writes through persistence rowversion;
- exposes no physical-delete behavior.

Per-company uniqueness of the normalized employee number and normalized national ID is a database-backed invariant coordinated by the Application layer and per-company unique indexes.

## Value objects

### EmployeeNumber

- required;
- trimmed display value; nonempty after trim; no control characters; Unicode permitted (not ASCII-only);
- maximum length 64 characters, applied to both the accepted input and the normalized value;
- normalized using `Trim().ToUpperInvariant()` only, with no Unicode NFC/NFD/NFKC/NFKD normalization;
- exact ordinal comparison; SQL uniqueness enforced on `NormalizedEmployeeNumber` under `Latin1_General_100_BIN2`;
- unique within a company by normalized value;
- immutable after creation;
- user-entered in Milestone 1.

### NationalId

- optional in Milestone 1;
- when present: trimmed, nonempty after trim, no control characters, maximum length 64 characters on both accepted input and normalized value;
- normalized identically to `EmployeeNumber`;
- unique within a company by normalized value, over rows where a value is present;
- mutable through the profile update operation, because a recorded national identity may be corrected.

### FullName

- required;
- trimmed;
- display casing preserved;
- maximum length 200 characters;
- mutable through the approved profile update operation;
- not unique.

A single `FullName` is deliberate for Milestone 1. Decomposed name parts, transliteration, and localized name forms are real HR requirements but have no source requirement in `HR.md`, and adding them speculatively would fix a structure that later requirements may contradict.

### EmployeeStatusChangeReasonCode

This bounded domain value contains exactly `Created`, `Administrative`, `Operational`, `Compliance`, `Resignation`, `Dismissal`, and `EndOfContract`. Creation records `Created`; every later lifecycle transition records a non-`Created` code. Domain events carry only the code and never free-form reason text. The vocabulary follows the `CompanyStatusChangeReasonCode` precedent (`DEC-CMP-0026`) with employment-specific termination reasons added.

### EmployeeBranchTransferReasonCode

This bounded domain value contains exactly `InitialAssignment`, `Reorganisation`, `OperationalNeed`, `EmployeeRequest`, `BranchClosure`, and `Correction`. `InitialAssignment` is recorded only by the initial assignment record written at creation and is invalid on a transfer; every transfer records one of the remaining codes. `BranchClosure` is the expected code for an inactive-source recovery transfer (`BRULE-EMP-0021`), and `Correction` is the expected code when a mistaken transfer is reversed by another transfer (`BRULE-EMP-0016`).

## Enumeration

`EmployeeStatus` contains exactly:

- `Active`;
- `Inactive`;
- `Terminated`.

A newly created Employee is `Active`.

## EmployeeBranchAssignment

`EmployeeBranchAssignment` is an immutable, append-only history record:

```csharp
public sealed class EmployeeBranchAssignment
  : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
```

Fields:

- `Id: Guid` (server-generated);
- `TenantId: Guid` (`ITenantOwnedEntity`);
- `CompanyId: Guid` (`ICompanyOwnedEntity`);
- `EmployeeId: Guid` (the Employee the record describes);
- `SourceBranchId: Guid?` — **null only on the initial assignment record**, otherwise the branch left;
- `DestinationBranchId: Guid` — never null; the branch entered;
- `EffectiveFromUtc: DateTimeOffset` — the commit instant; never a future value;
- `TransferredBy: string` — the acting principal;
- `ReasonCode: EmployeeBranchTransferReasonCode`;
- `ReasonText: string?`;
- `CreatedUtc`, `CreatedBy` (`IAuditableEntity`).

It carries **no `RowVersion`**: an append-only record that is never updated has no concurrency state to protect. Serialization of concurrent transfers is provided by `Employee.RowVersion` (`BRULE-EMP-0023`).

It carries **no `ModifiedUtc` / `ModifiedBy`**, because it is never modified.

### It is deliberately NOT branch-owned

`EmployeeBranchAssignment` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity` and **does not implement `IBranchOwnedEntity`** (`ADR-024` decision 4).

A transfer record spans a branch boundary: it names a source and a destination and belongs to neither. Stamping it with a single `BranchId` would either hide a departure from the branch that received the employee or hide an arrival from the branch that released them, and it would collide with the write boundary, whose trusted context during a transfer is the source while the record's subject is the destination.

This is an **explicit classification** under Architecture Principle 11, recorded so that it can never read as an omission. No modelling or foreign-key decision may accidentally make this type branch-owned; an architecture test asserts the classification (`TS-EMP-0113`).

### ReasonCode and ReasonText shape

The smallest repository-consistent option is adopted: a **required bounded `ReasonCode`** plus an **optional free-text `ReasonText`** limited to 512 characters (`DEC-EMP-0024`).

The bounded code follows the established `CompanyStatusChangeReasonCode` precedent and is the only reason value carried in domain events, so events stay free of free-form text (`NFR-EMP-0307`). The optional text is persisted for the audit record only — it is never used in a decision, never compared, never indexed, and never emitted in an event.

## Domain operations

| Operation | Effect |
|---|---|
| `Create` | Constructs the Employee in `Active`, stamps identity and employment fields, records reason code `Created`, and produces the initial branch-assignment record with reason `InitialAssignment` |
| `UpdateProfile` | Changes only `FullName` and `NationalId`. Never `TenantId`, `CompanyId`, `BranchId`, `EmployeeId`, `EmployeeNumber`, or `Status` |
| `Deactivate` / `Activate` | Moves between `Active` and `Inactive` with a non-`Created` reason code |
| `Terminate` | Moves to terminal `Terminated`, sets `TerminationDate`, enforces `EmploymentDate <= TerminationDate`, records a non-`Created` reason code |
| `Transfer` | Changes `BranchId` to the authorized destination and produces one appended assignment record. Refused for a `Terminated` employee and when destination equals source |

`Transfer` is a **dedicated domain and application operation**. It is never expressible as a property assignment on the update path, and the update contract does not carry `BranchId` at all (`BRULE-EMP-0015`).

## Domain events

- `EmployeeCreated`;
- `EmployeeProfileUpdated`;
- `EmployeeActivated`;
- `EmployeeDeactivated`;
- `EmployeeTerminated`;
- `EmployeeTransferred`.

Events may contain `EmployeeId`, `TenantId`, `CompanyId`, previous and new status (for lifecycle events), previous and new `BranchId` (for `EmployeeTransferred`), occurrence time, and the safe bounded reason code. Events carry **no** employee name, national ID, employee number, free-form reason text, credentials, tokens, complete claims, secrets, or HTTP context. Correlation, request, actor, and trace metadata remain outside Domain and are attached by the existing dispatch infrastructure.

Events are dispatched only after successful persistence, through the existing post-commit dispatcher. No integration event and no outbox is introduced.

## Repository contract

Per `ADR-010`, define one aggregate-specific `IEmployeeRepository` in HR Application and one implementation in HR Infrastructure.

It may expose only domain-focused operations such as:

- get by `EmployeeId` within the trusted tenant, authorized company scope, and authorized branch scope;
- test normalized employee-number uniqueness within the current company;
- test normalized national-ID uniqueness within the current company;
- add Employee together with its initial branch-assignment record;
- append a branch-assignment record.

It exposes no generic CRUD, no delete method, no `IQueryable`, no authorization behavior, and no transaction management.

## Read contract

Employee reads use a bounded Application read service returning safe projections (`GetEmployeeById`, `SearchEmployees`, `GetEmployeeBranchHistory`). The read contract exposes no aggregate and no `IQueryable`.

Every read is executed within the trusted current tenant and carries an **explicit `CompanyId` predicate and an explicit `BranchId` predicate** over the selected or authorized scope. Unlike `Company`, Employee does **not** rely on a global filter for its company or branch dimension: `ADR-025` decision 10 rejects a global company query filter, and `ADR-023` has never defined a global branch filter. Only the tenant dimension is filtered globally. See [`authorization-model.md`](authorization-model.md).

## Ownership boundary note

`Employee` is the first entity to consume `ICompanyOwnedEntity` and the first to consume `IBranchOwnedEntity` in production. Both interfaces and their write boundaries are **general platform infrastructure** introduced by this package, not HR-specific code (`NFR-EMP-0309`). The sanctioned branch-transfer channel is likewise general and must be reusable by any future transferable branch-owned entity (`ADR-024` decision 11).

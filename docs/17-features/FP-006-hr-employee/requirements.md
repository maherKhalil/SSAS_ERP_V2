---
document_id: FP-006-REQ
title: HR Employee Requirements
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Requirements

> Approved for Implementation — requirements reflecting the settled FP-006A decisions.

## Business requirements

### BR-EMP-0001 — Authoritative tenant-owned employee

HR must own one authoritative Employee record for every employee, identified by a server-generated Guid `EmployeeId`, always owned by exactly one Tenant through a required `TenantId` (`BR-PLT-0001`).

### BR-EMP-0002 — Company-owned employee

Every Employee belongs to exactly one Company, the legal entity that employs them. `CompanyId` is required and immutable. Employee is partitioned by company in addition to tenant (`ADR-014`, `ADR-025`).

### BR-EMP-0003 — Branch-owned employee

Every Employee belongs to exactly one active Branch, the operating location at which they work. `BranchId` is required, assigned by the server, and changes only through an explicit transfer (`BR-PLT-0013`, `ADR-023`, `ADR-024`).

### BR-EMP-0004 — Employee number identity

Every Employee carries an Employee Number that is unique within its company (`BR-HR-0001`). The number is required, user-entered in V1, and immutable after creation.

### BR-EMP-0005 — National identity uniqueness

Where a National ID is recorded, it is unique within its company (`BR-HR-0002`).

### BR-EMP-0006 — Employment lifecycle

An Employee is created active, may be deactivated and reactivated, and may be terminated. Employment Date cannot be later than Termination Date (`BR-HR-0003`). A terminated employee cannot be assigned new business transactions (`BR-HR-0004`).

### BR-EMP-0007 — Historical preservation

Employee records and their references must be retained. Physical employee deletion is prohibited; `Terminated` is the terminal retained state (`BR-PLT-0003`).

### BR-EMP-0008 — Explicit branch transfer

An Employee may move between branches only through an explicit, authorized, audited transfer operation. Ordinary employee update must never relocate an employee (`REQ-HR-0004`, `ADR-024`).

### BR-EMP-0009 — Branch assignment history

Every branch an Employee has occupied, and every move between branches, must be retained as immutable history sufficient to attribute the employee to the correct branch at any past point in time (`REQ-HR-0006`, `ADR-024`).

### BR-EMP-0010 — Independent authorization dimensions

Access to Employee data requires a functional permission, an authorized company scope, and an authorized branch scope, evaluated independently. None substitutes for another (`BR-PLT-0002`, `BR-PLT-0016`, `ADR-025`).

### BR-EMP-0011 — Employee retrieval and search

Authorized callers must be able to retrieve one Employee and to search employees within their authorized company and branch scope, with bounded results (`REQ-HR-0008`).

### BR-EMP-0012 — Security traceability

Every Employee mutation must be attributable to an actor and a UTC instant, and must record what changed (`BR-PLT-0004`, `BR-PLT-0005`).

---

## Functional requirements

### FR-EMP-0101 — Create employee

Create an Employee within the trusted tenant, the trusted company context, and the trusted branch execution context, with a required unique employee number and required identity and employment fields. The Employee is created `Active`. An initial branch-assignment record is written in the same transaction.

### FR-EMP-0102 — Get employee

Retrieve one Employee by `EmployeeId` within the caller's authorized company and branch scope, returning a safe projection and the concurrency version.

### FR-EMP-0103 — Search employees

Return a bounded, deterministically ordered, paginated list of safe Employee projections within an explicit company scope and an explicit branch scope, with optional status and employee-number filters.

### FR-EMP-0104 — Update employee profile

Update only the mutable profile fields of an Employee using optimistic concurrency. `TenantId`, `CompanyId`, `BranchId`, `EmployeeId`, `EmployeeNumber`, and `Status` are not updatable through this operation.

### FR-EMP-0105 — Terminate employee

Terminate an Employee with a required termination date not earlier than the employment date and a required bounded reason code, using optimistic concurrency. Termination is terminal.

### FR-EMP-0106 — Transfer employee

Move an Employee from its current branch to an authorized active destination branch through a dedicated operation, appending one branch-assignment record and updating the Employee's current branch in one transaction, using optimistic concurrency and a required bounded reason code.

### FR-EMP-0107 — Get employee branch history

Return the Employee's ordered, immutable branch-assignment records, sufficient to determine the branch effective at any past instant.

---

## Security requirements

### SEC-EMP-0201 — No writable tenant

The owning `TenantId` is derived only from the trusted current tenant context. It is never accepted from the route, body, header, claim, or query string, and it is immutable after creation.

### SEC-EMP-0202 — Trusted company context

`CompanyId` is adopted only from the trusted company execution context established by `ADR-025`. A caller-supplied company identifier expresses intent only and is authorized live before it is trusted; it is never authorization proof. A supplied `CompanyId` on an entity is confirmed against the trusted context, never trusted, and is immutable after creation.

### SEC-EMP-0203 — Trusted branch context

`BranchId` is assigned by the server from the authenticated branch execution context and is never accepted from a request DTO, header, form field, or token claim. A supplied value is confirmed against the trusted context and refused when it differs (`ADR-023` decision 18).

### SEC-EMP-0204 — Independent authorization dimensions

Functional permission, authorized company scope, and authorized branch scope are evaluated independently for every Employee operation. Holding `Platform.Tenant.Administer` widens company and branch scope only and grants no HR functional permission. Holding an `HR.Employees.*` permission widens no company or branch scope.

### SEC-EMP-0205 — Fail-closed authorization

Every authorization failure refuses the operation. No branch selected, no company selected, unusable session, revoked access, revoked authority, deactivated branch, inactive company, missing authorizer, or missing context each refuse rather than fall back to a previously valid or wider answer.

### SEC-EMP-0206 — Explicit scope predicates

Every Employee read carries an explicit `CompanyId` predicate and an explicit `BranchId` predicate over the selected scope or an authorized scope set. "All authorized branches" and "all authorized companies" are materialized as explicit predicates. Omitting a predicate is a defect, never an optimization, and never means "all".

### SEC-EMP-0207 — No physical deletion

No delete command, repository method, permission, endpoint, cascade, or routine database operation exists for Employee or for `EmployeeBranchAssignment`. A persistence guard rejects physical deletion.

### SEC-EMP-0208 — Concurrency protection

Every mutating Employee operation supplies an expected rowversion and is rejected when stale.

### SEC-EMP-0209 — Immutable identity

`EmployeeId`, `TenantId`, `CompanyId`, and `EmployeeNumber` are immutable after creation. `BranchId` is immutable through ordinary update and changes only through the sanctioned transfer channel.

### SEC-EMP-0210 — Cross-boundary opacity

An `EmployeeId` belonging to another tenant, another company outside the caller's authorized set, or a branch outside the caller's authorized set yields the same not-found result as an unknown identifier. Existence is never disclosed across a boundary. Branch and company refusals disclose no database topology.

### SEC-EMP-0211 — No token-carried scope

No `BranchId` or `CompanyId` claim is added to any token, and no branch or company authorization is cached in one. Scope is resolved from live state on every request and every write.

### SEC-EMP-0212 — Transfer authorization

Transfer authorizes the source branch and the destination branch independently, against live state, inside the operation. The destination is a business argument that is authorized, never an assertion of the caller's execution scope.

---

## Non-functional requirements

### NFR-EMP-0301 — Asynchronous operations

Every Employee command and query handler is asynchronous and accepts a cancellation token that flows through every persistence and read boundary.

### NFR-EMP-0302 — Clean Architecture

HR Domain and Application remain free of EF Core, SQL Server, ASP.NET Core, HTTP, and UI dependencies.

### NFR-EMP-0303 — Module isolation

HR references Platform only through approved contracts and identifiers. Platform Domain references no HR type.

### NFR-EMP-0304 — SQL Server verification

Ownership, uniqueness, constraints, concurrency, the branch write boundary, and the company write boundary are verified against real SQL Server, not only in memory.

### NFR-EMP-0305 — Query boundaries

Employee reads are bounded. Search uses page-based pagination with documented positive limits and deterministic ordering. No `IQueryable` crosses the Application boundary.

### NFR-EMP-0306 — Quality gates

The package's acceptance criteria and test scenarios are implemented where the corresponding infrastructure exists, and the two architecture guards ship with the first Employee read.

### NFR-EMP-0307 — Audit-ready events

Employee domain events carry domain facts only — identifiers, status, bounded reason codes, occurrence time — and no personal display text, credentials, tokens, complete claims, secrets, or HTTP context. Events are dispatched only after successful persistence.

### NFR-EMP-0308 — Deterministic normalization

Employee number and national-ID normalization is exactly `Trim().ToUpperInvariant()` with ordinal comparison and no Unicode NFC/NFD/NFKC/NFKD normalization, matching the established repository convention.

### NFR-EMP-0309 — Ownership machinery introduction

FP-006 introduces the company-ownership machinery deferred by `ADR-014` decision 6 and specified by `ADR-025`, and the sanctioned branch-transfer channel specified by `ADR-024`. Both are general platform infrastructure, not Employee-specific code.

### NFR-EMP-0310 — Cutover manifest integrity

`Employee` and `EmployeeBranchAssignment` are tenant-owned and are therefore carried by shared-to-dedicated cutover. The declared tenant-owned inventory is extended deliberately, and copy ordering respects foreign-key dependency.

---

## Exclusions

FP-006 Milestone 1 defines no rehire operation and no transition out of `Terminated`; no Employee document storage (`REQ-HR-0005`); no employee import (`REQ-HR-0009`); no employee export (`REQ-HR-0010`); no Department aggregate, table, column, or foreign key (`REQ-HR-0100`); no Position aggregate, table, column, or foreign key (`REQ-HR-0200`); no manager or reporting-line field; no automatic employee-number sequence generation or numbering-sequence infrastructure (`BR-PLT-0006`); no durable session company selection; no payroll, attendance, recruitment, performance, training, or self-service concept; no Angular UI; no Row-Level Security; no immutable audit store; and no outbox or integration-event mechanism.

`BR-HR-0005` (every employee belongs to exactly one department), `BR-HR-0006` (every employee must have one active position), and `BR-HR-0007` (an employee cannot directly manage themselves) are **retained as binding business rules**. They are not discarded, weakened, or reinterpreted. Their enforcement is deferred until the Department and Position aggregates exist, and the obligation transfers to the package that introduces them, including for employees created under V1 (`BRULE-EMP-0026`, `DEC-EMP-0017`, `DEC-EMP-0018`, `DEC-EMP-0031`).

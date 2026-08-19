---
document_id: FP-006-DEC
title: HR Employee Decisions
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
approval: Approved for implementation (FP-006A authority review and owner decisions closed)
---

# Decisions

> **Approved for Implementation.** The decisions below reflect the settled FP-006A authority review, the five owner decisions that closed it, and the architecture recorded in `ADR-024` and `ADR-025`. They are binding for FP-006 implementation. Identifiers are stable: where a decision's answer changes, its content is updated in place rather than renumbered.
>
> No decision here overrides an ADR. Where a decision restates an ADR, the ADR is authoritative and is cited.

## Ownership

## DEC-EMP-0001 — Employee is tenant-owned

`Employee` implements `ITenantOwnedEntity`, physically carries `TenantId`, and reuses the existing tenant read/write isolation rules unchanged: the global tenant query filter, server-side tenant assignment on insert, post-creation `TenantId` immutability, restricted deletes, and audit stamping. (`ADR-023` *For HR*, `BR-EMP-0001`, `BRULE-EMP-0012`.)

## DEC-EMP-0002 — Employee is company-owned

`Employee` implements `ICompanyOwnedEntity` and physically carries `CompanyId`. Employee is the **first production consumer** of that interface, and FP-006 therefore introduces the company-ownership machinery that `ADR-014` decision 6 deferred until "the first real company-owned business record". (`ADR-014` revision 1.1, `ADR-025` decision 1, `BR-EMP-0002`.)

## DEC-EMP-0003 — Employee is branch-owned

`Employee` implements `IBranchOwnedEntity` and physically carries `BranchId`. Employee is the **first production `IBranchOwnedEntity`**, and its implementation converts `ADR-023` decisions 10, 16 and 18 from structurally implemented to runtime-proven. (`ADR-023` *For HR* and LOW-1, `ADR-024` decision 1, `BR-EMP-0003`.)

## DEC-EMP-0004 — EmployeeId is a Guid

`EmployeeId` is a server-generated, nonempty, immutable, never-reused `Guid`. It qualifies as a `Guid` because it is a cross-module business identifier that future HR, payroll, and transactional modules will reference. Because Employee is a higher-write root than Company, the implementation applies the sequential-`Guid` guidance for the clustered key or justifies an alternative clustering choice against measured insert behavior. (`ADR-013`, `BRULE-EMP-0006`.)

## DEC-EMP-0005 — Employee stores the authoritative current BranchId

`Employee.BranchId` is the authoritative current operating branch. A purely temporal or derived representation, in which the current branch is computed from assignment history and no `BranchId` column exists, is **rejected**: it contradicts `ADR-023`, and it would leave `IBranchOwnedEntity` unimplementable, taking Employee outside the branch write boundary entirely. (`ADR-024` decision 1.)

## DEC-EMP-0006 — EmployeeBranchAssignment is append-only history

Employee branch history is an immutable, append-only record. Employee creation writes an initial record with `SourceBranchId = null`; each transfer appends one further record. **No record is ever updated or physically deleted**, including to close an effective interval — writing an `EffectiveToUtc` onto a prior row would be exactly the history mutation the model exists to prevent. Because V1 forbids future-dating, `EffectiveFromUtc` is monotonic per employee and the effective interval is derived by ordering. (`ADR-024` decisions 5 and 9, `BRULE-EMP-0018`, `BRULE-EMP-0019`.)

## DEC-EMP-0007 — EmployeeBranchAssignment is NOT branch-owned

`EmployeeBranchAssignment` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity` and **does not implement `IBranchOwnedEntity`**.

A transfer record spans a branch boundary: it names a source and a destination and belongs to neither. Stamping it with a single `BranchId` would either hide a departure from the branch that received the employee or hide an arrival from the branch that released them, and it would collide with the write boundary, whose trusted context during a transfer is the source while the record's subject is the destination.

This is an **explicit classification** under Architecture Principle 11, not an omission. Neither `SourceBranchId` nor `DestinationBranchId` may be mapped to `IBranchOwnedEntity.BranchId`, and neither column may be named `BranchId`, so that no convention or shadow property can silently reclassify the table. (`ADR-024` decision 4, `TS-EMP-0113`.)

## DEC-EMP-0008 — Company relationship is required and immutable

Every Employee belongs to exactly one Company. `CompanyId` is required at creation, adopted only from the trusted company execution context, confirmed rather than trusted when supplied, and **immutable** thereafter.

An Employee never moves between companies. A person employed by a different legal entity has a different employment relationship, and Milestone 1 represents that as a separate Employee record. (`ADR-014`, `ADR-025` decision 9, Glossary "Employee", `BRULE-EMP-0013`.)

## Identifier

## DEC-EMP-0009 — EmployeeNumber is user-entered in V1

`EmployeeNumber` is required at creation and supplied by the caller. `BR-PLT-0006` lists Employee Number among per-company configurable numbering sequences, but no numbering mechanism exists in the platform and FP-005 explicitly excluded building one, so V1 accepts a user-entered value validated and enforced unique by index. (`BR-HR-0001`, `BR-PLT-0006`, `BRULE-EMP-0007`.)

## DEC-EMP-0010 — EmployeeNumber immutability, normalization, and length

`EmployeeNumber` is immutable after creation, trimmed, nonempty after trim, free of control characters, limited to 64 characters on both accepted input and normalized value, and stored with display casing preserved. Normalization is exactly `Trim().ToUpperInvariant()` with ordinal comparison and no Unicode NFC/NFD/NFKC/NFKD normalization. Unicode is permitted; the number is not ASCII-only. The 64-character limit and normalization rule follow the established `CompanyCode` convention. (`DEC-CMP-0006`, `DEC-CMP-0007`, `BRULE-EMP-0007`, `NFR-EMP-0308`.)

## DEC-EMP-0011 — Automatic employee numbering is deferred

Automatic per-company employee-number generation is **deferred, not discarded**. FP-006 introduces no numbering-sequence table, service, or configuration.

`EmployeeNumber` is designed as a required *input* to the create command rather than a client-owned identity, so a future generator can supply the value server-side before the aggregate is constructed, with no change to the column, index, constraint, or resource shape. The only contract change a generator would require is making the request field optional where a sequence is configured, which is additive. The obligation to satisfy `BR-PLT-0006` transfers to the package that introduces numbering sequences. (`BRULE-EMP-0027`, `AC-EMP-0046`, `TS-EMP-0118`.)

## DEC-EMP-0012 — Employee number uniqueness scope

`EmployeeNumber` is unique **within a company**, by normalized value, enforced by a `(TenantId, CompanyId, NormalizedEmployeeNumber)` unique index using `Latin1_General_100_BIN2`.

**`BranchId` does not participate.** `BR-HR-0001` scopes the rule to the company, and `ADR-023` states that Employee uniqueness which is company-wide must not include `BranchId`. The consequence is intended: two employees in different branches of one company cannot share a number. The database constraint is authoritative under concurrent creation; an application pre-check is an optimization, not the authority. (`BR-HR-0001`, `ADR-014`, `ADR-023` *For HR*, `BRULE-EMP-0008`, `BRULE-EMP-0009`.)

## DEC-EMP-0013 — National ID is optional and company-scoped

`NationalId` is optional in Milestone 1. Where present it is unique within a company by normalized value, enforced by a filtered unique index over rows where a value exists, using the same normalization and collation as the employee number. It is mutable through the profile update operation, because a recorded national identity may be corrected. (`BR-HR-0002`, `BRULE-EMP-0010`.)

## Lifecycle

## DEC-EMP-0014 — Lifecycle states and initial state

`EmployeeStatus` contains exactly `Active`, `Inactive`, and `Terminated`. A newly created Employee is **`Active`**.

This deliberately differs from `Company`, which is created `Inactive` (`DEC-CMP-0011`). That rule exists because a company may exist before its configuration prerequisites are ready; an Employee has no such prerequisites, and an employee who must be separately activated after hiring would be a state with no business meaning. `Inactive` is a reversible not-in-service state, not termination. (`BRULE-EMP-0001`, `BRULE-EMP-0002`.)

## DEC-EMP-0015 — No physical deletion

Physical deletion of an Employee or an `EmployeeBranchAssignment` is prohibited. No delete command, repository method, permission, endpoint, cascade, or routine database operation exists, and a persistence guard rejects physical deletion of either, mirroring the existing Company deletion guard. `Terminated` is the terminal retained state. (`BR-PLT-0003`, `DEC-CMP-0013`, `BRULE-EMP-0005`, `SEC-EMP-0207`.)

## DEC-EMP-0016 — Rehire is deferred

No transition out of `Terminated` exists in Milestone 1, and no rehire operation is defined. No source requirement establishes one.

A future package introducing rehire must decide whether it reuses the existing Employee identity or creates a new Employee record, and must state the consequences for employee-number uniqueness, employment dates, and branch history. Neither choice is foreclosed: the append-only branch history and the retained terminal record support either. (`BRULE-EMP-0003`, `AC-EMP-0014`.)

## DEC-EMP-0017 — Department is deferred

`BR-HR-0005` ("Every employee belongs to exactly one department") is **retained as a binding business rule**. It is not discarded, weakened, or reinterpreted.

Its *enforcement* is deferred until the Department aggregate (`REQ-HR-0100`) exists. Milestone 1 Employee creation does not require, accept, or persist a `DepartmentId`, and FP-006 introduces **no placeholder Department entity, table, column, or foreign key**.

This is a deliberate scope decision taken to keep the first production `IBranchOwnedEntity` slice minimal. The obligation transfers to the package that introduces Department, which must add the association and enforce `BR-HR-0005` at that time, including for employees created under Milestone 1. (`BRULE-EMP-0026`, `AC-EMP-0045`, `TS-EMP-0117`.)

## DEC-EMP-0018 — Position is deferred

`BR-HR-0006` ("Every employee must have one active position") is **retained as a binding business rule** on exactly the same terms as `DEC-EMP-0017`, deferred until the Position aggregate (`REQ-HR-0200`) exists. No `PositionId` column and no placeholder Position entity, table, or foreign key is introduced. (`BRULE-EMP-0026`, `AC-EMP-0045`, `TS-EMP-0117`.)

## Transfer

## DEC-EMP-0019 — Transfer is a separate operation

An Employee's branch changes only through the dedicated `TransferEmployee` operation, with its own command, route, DTO, functional permission, and audit record.

`UpdateEmployee` does **not** accept `BranchId` in its contract at all — omission at the contract level, not validation — and the branch write boundary refuses an unsanctioned modification anyway, as defence in depth. Ordinary Employee CRUD can never become arbitrary branch reassignment. (`REQ-HR-0004`, `ADR-023` decision 18, `ADR-024` decisions 2, 3 and 10, `BRULE-EMP-0014`, `BRULE-EMP-0015`.)

## DEC-EMP-0020 — Transfer is immediate only

A transfer takes effect immediately upon successful commit. Milestone 1 defines no future-dated transfer, no scheduled transfer, and no cancellation operation. A mistaken transfer is corrected by another explicit authorized transfer, which appends a further history record with reason `Correction`.

Forbidding future-dating is what keeps `EffectiveFromUtc` monotonic per employee, which is what makes the point-in-time query unambiguous without stored end dates. (`ADR-024` decision 9, `BRULE-EMP-0016`.)

## DEC-EMP-0021 — Transfer atomicity and concurrency

The `Employee.BranchId` change and the appended assignment record commit in **one transaction**. `Employee.RowVersion` is the optimistic-concurrency serialization point, which is what guarantees the assignment log cannot fork.

Concurrent transfers, a transfer racing an ordinary update, and a transfer racing a termination all resolve through that rowversion with exactly one winner; the loser is refused and retries against re-read state. A stale rowversion, a destination deactivated before commit, and source or destination authorization revoked before commit are each refused.

**No new application lock is introduced.** `BranchTopologyLock` exists because the branch-assignment invariant spans two databases and can strand a user; a transfer changes no branch topology and touches one catalog. (`ADR-024` decision 7, `BRULE-EMP-0023`, `AC-EMP-0033`.)

## DEC-EMP-0022 — Dual branch authorization

Transfer authorizes the source branch and the destination branch independently. The source is the trusted branch execution context; the destination is authorized separately through `ITenantBranchAccessResolver`, which intersects with active branches. Both are revalidated **inside** the transaction, not captured at request start.

The destination is a **business argument that is authorized**, never an assertion of the caller's own execution scope. This does not weaken `ADR-023` decision 18: the caller's execution scope still comes only from the server. (`ADR-024` decision 6, `BRULE-EMP-0020`, `SEC-EMP-0212`.)

## DEC-EMP-0023 — Inactive source branch recovery

A Tenant Administrator may transfer an Employee **out of** an inactive source branch when they hold `Platform.Tenant.Administer` and `HR.Employees.Transfer`, the destination is active and belongs to the same tenant, the operation is the explicit `TransferEmployee` operation, the transfer is audited, and normal destination authorization succeeds.

This is a narrow, explicit exception to `ADR-023` decision 5, under which an administrator's scope is all *active* branches and an inactive branch is unreachable. Without it, an Employee in a deactivated branch would be permanently unwritable and untransferable.

The alternative — refusing branch deactivation while employees remain — was rejected because it would require the Platform module to inspect HR employees, a Platform → HR dependency the modular monolith forbids.

The exception is one-directional: it authorizes moving an Employee out and nothing else. It grants no ordinary read or write authority over the inactive branch, does not widen the administrator's branch scope for any other operation, and does not permit a transfer *into* an inactive branch. (`ADR-024` decision 12, `BRULE-EMP-0021`, `AC-EMP-0036`, `TS-EMP-0051`.)

## DEC-EMP-0024 — Transfer reason code and text shape

Each assignment record carries a **required bounded `ReasonCode`** and an **optional free-text `ReasonText`** limited to 512 characters.

`EmployeeBranchTransferReasonCode` contains exactly `InitialAssignment`, `Reorganisation`, `OperationalNeed`, `EmployeeRequest`, `BranchClosure`, and `Correction`. `InitialAssignment` is valid only on the initial record and invalid on a transfer, enforced by a check constraint paired with the nullability of `SourceBranchId`.

The bounded code follows the established `CompanyStatusChangeReasonCode` precedent and is the only reason value carried in domain events, keeping events free of free-form text. `ReasonText` is persisted for the audit record only: it is never used in a decision, compared, indexed, or emitted in an event. This is the smallest repository-consistent option and is recorded here because the choice is additive rather than derived from an authority. (`DEC-CMP-0025`, `DEC-CMP-0026`, `NFR-EMP-0307`.)

## DEC-EMP-0025 — Historical branch attribution

Current-state questions read `Employee.BranchId`. Point-in-time questions read `EmployeeBranchAssignment`, selecting the record with the greatest `EffectiveFromUtc` less than or equal to the instant in question.

**Mixing current-branch attribution into a historical report is a defect.** A report about a past period must attribute an employee to the branch they were actually in at that time, not the branch they are in now. (`ADR-024` decision 8, `BRULE-EMP-0022`, `AC-EMP-0035`.)

## Company context and authorization

## DEC-EMP-0026 — Company execution context

FP-006 introduces `ICurrentCompany`, exposing a nullable `Guid? CompanyId` established **per request**. A caller-supplied `CompanyId` expresses **intent only** and is trusted only after live validation, in order and failing closed at every step: the trusted tenant is known; the company exists; it belongs to that tenant; it is `Active`; the caller is currently authorized for it.

Unauthorized, inactive, wrong-tenant, and nonexistent identifiers return one generic refusal that discloses nothing about existence.

The `company_id` JWT claim, `ICurrentUser.CompanyId`, and any header, body, route, or query `CompanyId` are **never** authorization proof, and no company authorization is cached into a token. A durable session `ActiveCompanyId` is deferred and remains additive. (`ADR-025` decisions 2, 3, 4 and 11, `SEC-EMP-0202`, `SEC-EMP-0211`.)

## DEC-EMP-0027 — User↔Company authorization

FP-006 implements company authorization rather than deferring it, because Employee is the first company-owned business record and `BR-PLT-0002` becomes load-bearing the moment such data exists.

`UserCompanyAccess` lives in the **platform** database, carries `TenantId`, `TenantUserId` and `CompanyId`, and is unique on `(TenantId, TenantUserId, CompanyId)`. It holds **no cross-database foreign key** to `tenant.Companies`; `CompanyId` is an opaque cross-database identifier, validated by the application against the tenant database before any row is written. It is not `ITenantOwnedEntity`, for the same reason `UserBranchAccess` is not.

`ITenantCompanyAccessResolver` is the single source of truth, resolved against live state per request and per write. No handler re-derives company scope. A caller with zero authorized active companies is refused company-owned operations and never defaulted to all. (`BR-PLT-0002`, `ADR-025` decisions 5 and 6, `ADR-014` revision 1.1 Correction C.)

## DEC-EMP-0028 — Three independent authorization dimensions

Every Employee operation requires a **functional permission** and an **authorized company scope** and an **authorized branch scope**, evaluated independently. None substitutes for another.

`Platform.Tenant.Administer` widens company scope to all active companies and branch scope to all active branches, derived from authority with no stored rows, and grants **no** HR functional permission. Conversely, holding an `HR.Employees.*` permission widens no company or branch scope.

Company scope and branch scope are themselves independent sibling dimensions: a branch is an operating location of the tenant, not of a company, so no invariant relates the selected company to the active branch and none is added. (`ADR-023` decision 5, `ADR-025` decisions 7 and 8, `BRULE-EMP-0024`, `AC-EMP-0040`, `AC-EMP-0041`.)

## DEC-EMP-0029 — Explicit scope predicates, no global company or branch filter

Every Employee read carries an explicit `CompanyId` predicate and an explicit `BranchId` predicate. Only the tenant dimension is filtered globally.

**No global current-company or current-branch EF query filter is introduced.** A global filter pinned to one company or branch would make authorized multi-company and multi-branch reads unexpressible. This is a deliberate divergence from the machinery sketched in `ADR-014` decision 6, superseded by `ADR-025` decision 10 and recorded in `ADR-014` revision 1.1, Correction D.

"All authorized branches" and "all authorized companies" **materialize** the caller's authorized identifiers into the predicate. An omitted predicate never means "all", and an empty authorized set refuses the read rather than returning unfiltered results.

Two **executable architecture guards** enforce this and must ship in the same slice as the first Employee read: the `ADR-023` decision 22 guard for branch predicates, and the `ADR-025` decision 10 guard for company predicates. Neither may be satisfied by a global query filter. (`BR-PLT-0016`, `ADR-023` decision 22, `ADR-025` decision 10, `BRULE-EMP-0025`, `AC-EMP-0029`, `AC-EMP-0030`, `TS-EMP-0110`, `TS-EMP-0111`.)

## DEC-EMP-0030 — Functional permissions

Employee uses a five-permission, code-owned set at `PermissionScope.Tenant`: `HR.Employees.View`, `HR.Employees.Create`, `HR.Employees.Update`, `HR.Employees.Transfer`, and `HR.Employees.Terminate`. All satisfy the platform permission-name grammar of exactly three ASCII-identifier segments, so no framework change is required.

`Transfer` is separated from `Update` because a transfer moves a record across a security partition and is the one operation permitted to change `BranchId`. `Terminate` is separated because it is terminal and sensitive under `BR-PLT-0103`. Activate and deactivate are grouped under `Update` rather than given a separate `Lifecycle` permission, because unlike Company lifecycle they are reversible, non-terminal, and carry no cross-partition consequence. (`BR-PLT-0101`, `BR-PLT-0103`, `AC-EMP-0040`.)

## Deferrals and obligations

## DEC-EMP-0031 — Manager and reporting line deferred

No `ManagerId` or reporting-line field exists in Milestone 1. `BR-HR-0007` ("An employee cannot directly manage themselves") is **retained as a binding business rule** and is deferred with Department, because it has no field to constrain until a reporting line exists. The obligation transfers to the package that introduces reporting lines. (`BRULE-EMP-0026`, `TS-EMP-0117`.)

## DEC-EMP-0032 — Documents, import, and export deferred

`REQ-HR-0005` (Employee Documents), `REQ-HR-0009` (Employee Import), and `REQ-HR-0010` (Employee Export) are deferred whole and are outside the Employee core slice. No entity, table, column, route, command, or storage binding is introduced for any of them.

Each carries a forward obligation. A future import must satisfy every rule in this package, including per-record company and branch authorization; bulk insert must not bypass the branch or company write boundary. A future export is a company- and branch-scoped read and inherits both architecture guards; it must never be implemented by omitting a scope predicate. (`AC-EMP-0047`.)

## DEC-EMP-0033 — Shared→Dedicated copy obligations

`Employee` and `EmployeeBranchAssignment` are tenant-owned, so the model-derived copy plan includes them **by construction** and no engine change is required.

The **declared tenant-owned inventory** asserted separately in the architecture tests must be extended deliberately from `["Branch", "Company"]` to include both. That assertion is designed to fail on any new tenant-owned entity precisely so copy order, identity keys, and computed columns are decided rather than assumed.

Expected dependency order, principals before dependents:

```
Company
Branch
Employee
EmployeeBranchAssignment
```

`RowVersion` remains excluded from the copy mapping; `EmployeeBranchAssignment` has no rowversion to exclude. This is an implementation obligation of FP-006; no production code or test is modified by this documentation package. (`ADR-020`, `ADR-023` decision 21, `NFR-EMP-0310`, `AC-EMP-0037` … `AC-EMP-0039`, `TS-EMP-0130` … `TS-EMP-0133`.)

## DEC-EMP-0034 — ADR-023 LOW-1 runtime proofs are acceptance criteria

Employee is the first production `IBranchOwnedEntity`, and FP-006 must close the four deferred real-SQL proofs through the **existing** `TenantDbContext.ApplyBranchRulesAsync` path, not through new or Employee-specific branch infrastructure:

- **V** — a real Employee create genuinely reaches `IBranchWriteAuthorizer` and is stamped with the current `BranchId`;
- **W** — a spoofed `BranchId` on create is refused, not silently rewritten;
- **X** — ordinary update cannot mutate `BranchId`;
- **Y** — cross-branch update and delete are refused.

Additionally, revoked branch assignment, revoked `Platform.Tenant.Administer` authority, and revoked company authorization must each refuse the next Employee operation.

**V is the proof that matters most**: every branch test written before FP-006 passes whether or not the authorizer's call site is reached, because no production entity implemented the interface. A test asserting only stamping leaves `ADR-023` decision 10 unverified. These are first-class acceptance criteria for the implementation slice, not background. (`ADR-023` LOW-1, `AC-EMP-0020` … `AC-EMP-0026`, `TS-EMP-0060` … `TS-EMP-0066`.)

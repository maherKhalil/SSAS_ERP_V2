---
document_id: FP-006-AC
title: HR Employee Acceptance Criteria
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Acceptance Criteria

> Approved for Implementation — criteria reflecting the settled FP-006A decisions.

## Ownership and creation

### AC-EMP-0001 — Active creation with trusted ownership

Creating a valid Employee generates a nonempty Guid `EmployeeId`, adopts the trusted current `TenantId`, adopts the trusted `CompanyId` from the validated company context, receives a server-stamped `BranchId` from the trusted branch execution context, stores the normalized employee number and trimmed name, and begins in `Active`.

⚠ **THIS CRITERION IS A ROLL-UP, NOT AN INDEPENDENT CLAIM (noted 2026-08-31, architect).** Every clause in
it — the identifier, the trusted tenant, the trusted company, the server-stamped branch, the normalized
number, the initial state — **is specified again, on its own, in a criterion below.** It exists to state the
creation outcome as one sentence, and it asserts nothing that those criteria do not.

**So it is deliberately left uncited by the citation sweep, and that is the correct disposal rather than a
gap.** Citing it on any one test would present a summary as a single assertion; citing it on all of them
would repeat what the clause-level criteria already say. ⚠ **A criterion that indexes other criteria is
verified by verifying them.** **The clause-by-clause mapping as at 2026-08-31 is in the B18 pass-12 result
file; it is not repeated here, because a list of test names in a specification goes stale the first time
somebody renames one.**

### AC-EMP-0002 — Trusted tenant only

`TenantId` is never accepted from the route, body, header, claim, or query string. A persisted Employee whose `TenantId` does not match the trusted current tenant is rejected, and a post-creation `TenantId` change is rejected.

### AC-EMP-0003 — Trusted company only

`CompanyId` is adopted only from the validated company context. A caller-supplied company identifier that fails any of the five validation steps refuses the operation. An entity arriving with a `CompanyId` that differs from the trusted context is refused rather than silently rewritten, and a post-creation `CompanyId` change is rejected.

### AC-EMP-0004 — Trusted branch only

`BranchId` is never accepted from a request DTO, header, form field, or token claim. An Employee created with an empty `BranchId` is stamped with the trusted branch.

### AC-EMP-0005 — Initial branch-assignment record

Creating an Employee writes exactly one `EmployeeBranchAssignment` in the same transaction, with `SourceBranchId` null, `DestinationBranchId` equal to the stamped `Employee.BranchId`, reason code `InitialAssignment`, and the trusted actor and UTC instant. An Employee with no branch-assignment history cannot exist.

### AC-EMP-0006 — Employee number identity

Within one company, two employee numbers whose `Trim().ToUpperInvariant()` values are equal cannot both be created; the accepted number's trimmed display casing is preserved; and a value whose normalized form exceeds 64 characters is rejected. The same normalized number may be created in a different company or a different tenant.

### AC-EMP-0007 — Profile update scope

The update operation changes only `FullName` and `NationalId`. `tenantId`, `companyId`, `branchId`, `employeeNumber`, and `status` are absent from the contract and are rejected as unknown fields if supplied. Updating a `Terminated` Employee is refused.

### AC-EMP-0008 — Employee number immutability

No operation, route, or repository method can change `EmployeeNumber` after creation.

### AC-EMP-0009 — National ID uniqueness and optionality

An Employee may be created with no national ID, and many employees in one company may have none. Where a national ID is recorded, two employees in the same company cannot hold the same normalized value.

### AC-EMP-0010 — Employment and termination dates

An Employee cannot be terminated with a `TerminationDate` earlier than its `EmploymentDate`. Both are stored as UTC. A non-terminated Employee has a null `TerminationDate`, and a `Terminated` Employee has a non-null one.

### AC-EMP-0011 — Nvarchar persistence

Every persisted Employee and `EmployeeBranchAssignment` application string column is SQL Server `nvarchar`. No `varchar`, `char`, or `text` column exists.

## Lifecycle

### AC-EMP-0012 — Approved transitions only

Every listed transition (Create→Active, Active→Inactive, Inactive→Active, Active→Terminated, Inactive→Terminated) is permitted, and every unlisted or repeated transition is rejected without changing metadata.

### AC-EMP-0013 — Reversible enablement pair

Deactivate is accepted only from `Active` and activate only from `Inactive`. Neither changes company, branch, or any identity field, and neither writes a branch-assignment record. No separate reactivate route exists.

### AC-EMP-0014 — Terminated is terminal

`Terminated` is terminal and preserves the aggregate for history. No transition out of `Terminated` exists, and no rehire operation is defined.

### AC-EMP-0015 — Post-termination behavior

A `Terminated` Employee cannot be updated, activated, deactivated, transferred, or deleted, and remains retrievable by id and returnable by search subject to the ordinary scope predicates. Its employee number and national ID remain reserved within the company.

### AC-EMP-0016 — Search excludes terminated by default

Search without a status filter returns only `Active` and `Inactive` employees. `Terminated` employees are returned only when explicitly requested.

### AC-EMP-0017 — No physical deletion

No delete command, repository method, permission, endpoint, or cascade exists for Employee or `EmployeeBranchAssignment`, and a persistence guard rejects physical deletion of either.

### AC-EMP-0018 — Reason codes

Creation records `Created`. Activate, deactivate, and terminate each require an explicit non-`Created` reason code. Transfers require a non-`InitialAssignment` reason code, and the initial assignment record carries `InitialAssignment`.

### AC-EMP-0019 — Optimistic concurrency

Every mutating operation supplies an expected rowversion, a stale value returns a conflict, and no state change or event is committed.

## B1 closure — ADR-023 LOW-1 real-SQL proofs

These four criteria discharge the `ADR-023` LOW-1 obligation. They must be satisfied through the existing `TenantDbContext.ApplyBranchRulesAsync` path against real SQL Server, not through Employee-specific branch infrastructure and not through mocks.

### AC-EMP-0020 — V: the branch write authorizer is genuinely invoked and the branch is stamped

Creating a real Employee through the real `TenantDbContext` reaches `IBranchWriteAuthorizer`, and the resulting row carries the current `BranchId`.

**This is the criterion that matters most.** Every branch test written before FP-006 passes whether or not the authorizer's call site is actually reached, so only a real Employee save proves the wiring. A test that asserts stamping without proving invocation does not satisfy this criterion.

### AC-EMP-0021 — W: a spoofed BranchId on create is refused

An Employee submitted for insert carrying a `BranchId` that is not the trusted current branch is **refused**, not silently rewritten to the trusted value. Silently correcting it would hide the attempt.

### AC-EMP-0022 — X: ordinary update cannot mutate BranchId

An ordinary Employee update whose `BranchId` property is modified is refused by the write boundary, independently of the API contract's omission of the field. Both defences are verified: the field is absent from the contract, and the boundary refuses it anyway.

### AC-EMP-0023 — Y: cross-branch update and delete are refused

Modifying or deleting an Employee whose `BranchId` differs from the trusted current branch is refused.

### AC-EMP-0024 — Revoked branch assignment refuses the next write

After a user's branch assignment is revoked mid-session, the next Employee write in that branch fails closed, even though the session still records the branch as its active context.

### AC-EMP-0025 — Revoked Tenant Administrator authority removes implicit branch scope

After `Platform.Tenant.Administer` is revoked mid-session, the holder no longer has implicit access to branches they never held assignment rows for, and the next Employee write in such a branch fails closed.

### AC-EMP-0026 — Revoked company authorization refuses the next operation

After a user's company authorization is revoked mid-session, the next Employee operation in that company fails closed, for both reads and writes.

## Reads and scope

### AC-EMP-0027 — Bounded scoped search

Search returns bounded, deterministically ordered safe projections with documented paging defaults and maxima, and rejects out-of-range paging.

### AC-EMP-0028 — Cross-boundary opacity

An `employeeId` that is unknown, belongs to another tenant, belongs to a company outside the caller's authorized set, or sits in a branch outside the caller's authorized set returns an identical not-found result in every case.

### AC-EMP-0029 — Every read carries explicit company and branch predicates

Every Employee read path emits an explicit `CompanyId` predicate and an explicit `BranchId` predicate. This is verified by executable architecture guards, not by inspection.

Specifically:

- `AllAuthorizedBranches` **materializes** the caller's authorized branch identifiers into the predicate; it is never implemented by omitting the predicate.
- `AllAuthorizedCompanies` **materializes** the caller's authorized company identifiers into the predicate on the same terms.
- `SelectedAuthorizedBranches` values that are not a subset of the authorized set are refused, identically for unauthorized, inactive, and nonexistent identifiers.
- A company selection outside the authorized set is refused on the same terms.
- An empty authorized branch set or an empty authorized company set **refuses** the read rather than returning unfiltered results.
- No read mode, parameter combination, or default produces a widening by predicate omission.

### AC-EMP-0030 — No global company or branch query filter

No global current-company or current-branch EF query filter is introduced. Only the tenant dimension is filtered globally, and the two architecture guards assert that scope is carried by explicit predicates instead.

## Transfer

### AC-EMP-0031 — Successful transfer is atomic

A successful transfer updates `Employee.BranchId` to the destination and appends exactly one `EmployeeBranchAssignment` recording the source, destination, actor, UTC instant, and bounded reason code — both in one transaction. Neither is committed without the other.

### AC-EMP-0032 — Dual branch authorization

Transfer authorizes the source branch as the trusted execution context and the destination branch separately through the live branch access resolver, intersected with active branches, and revalidates both inside the transaction. A destination that is unauthorized, inactive, or belongs to another tenant is refused identically.

### AC-EMP-0033 — Transfer concurrency

Two simultaneous transfers of one Employee produce exactly one success and one deterministic concurrency conflict; the assignment log cannot fork. A transfer racing an ordinary update or a termination resolves the same way. A stale rowversion is refused. A destination deactivated before commit is refused. Branch or company authorization revoked before commit is refused.

### AC-EMP-0034 — Transfer refusals

Transfer is refused when the Employee is `Terminated`, and when the destination equals the source.

### AC-EMP-0035 — Branch history and point-in-time attribution

An Employee's branch history is returned in effective order and is sufficient to determine the branch effective at any past instant, as the record with the greatest `EffectiveFromUtc` less than or equal to that instant. No history record is ever updated or deleted. Current-state reads use `Employee.BranchId` and never the log; point-in-time reads use the log and never `Employee.BranchId`.

### AC-EMP-0036 — Inactive-source recovery

A Tenant Administrator holding `HR.Employees.Transfer` can transfer an Employee out of a deactivated source branch into an authorized active destination, and the transfer is audited. The same operation is refused for a caller without `Platform.Tenant.Administer`. The exception grants no other read or write access to the inactive branch, and does not permit a transfer *into* an inactive branch.

## Shared→Dedicated cutover

### AC-EMP-0037 — Employee is deliberately recognized in the copy inventory

`Employee` appears in the declared tenant-owned inventory asserted by the architecture tests, and the model-derived copy plan covers it. The declared inventory and the model agree exactly.

### AC-EMP-0038 — EmployeeBranchAssignment is deliberately recognized

`EmployeeBranchAssignment` appears in the declared tenant-owned inventory on the same terms.

### AC-EMP-0039 — Copy ordering is valid

The derived copy plan orders `Company` and `Branch` before `Employee`, and `Employee` before `EmployeeBranchAssignment`, so inserts never violate referential integrity and constraints stay enabled throughout. `RowVersion` is excluded from the copy mapping.

## Authorization dimensions

### AC-EMP-0040 — Three independent dimensions

Every Employee operation requires its functional permission **and** an authorized company scope **and** an authorized branch scope. Each is independently sufficient to refuse:

- a caller with company and branch scope but without the `HR.Employees.*` permission is refused;
- a caller with the permission and branch scope but without company scope is refused;
- a caller with the permission and company scope but without branch scope is refused.

### AC-EMP-0041 — Tenant Administrator grants scope, never operations

`Platform.Tenant.Administer` widens company scope to all active companies and branch scope to all active branches, and grants **no** HR functional permission. A tenant administrator without `HR.Employees.Create` cannot create an employee, and without `HR.Employees.View` cannot read one.

### AC-EMP-0042 — No token-carried scope

No `BranchId` or `CompanyId` claim is added to any token, no company or branch authorization is cached in one, and no Employee code path reads `ICurrentUser.CompanyId` or the `company_id` claim as authorization.

### AC-EMP-0043 — Sanctioned transfer channel cannot be opened externally

The branch-transfer channel cannot be activated from a request DTO, request header, form field, token claim, or an arbitrary repository caller. It is opened only by the transfer handler, authorizes one exact entity/source/destination triple, and is scoped to the single save that performs the transition. No general "allow BranchId modification" flag exists.

### AC-EMP-0044 — Internal boundary refusals do not leak topology

Refusals originating in the branch or company write boundaries are surfaced as generic scope denials. No response discloses table names, database placement, or the existence of another tenant's company or branch.

## Deferrals

### AC-EMP-0045 — Department and Position deferral

An Employee can be created, retrieved, updated, transferred, and terminated with no department, position, or manager association. `tenant.Employees` contains no `DepartmentId`, `PositionId`, or `ManagerId` column, and FP-006 introduces no Department or Position entity, table, or foreign key. `BR-HR-0005`, `BR-HR-0006`, and `BR-HR-0007` remain recorded as binding and deferred, not removed.

### AC-EMP-0046 — Employee number generation deferral

`EmployeeNumber` is supplied by the caller at creation, and FP-006 introduces no numbering-sequence table, service, or configuration. The create contract accepts the value as a required input rather than deriving it, so a future generator can supply it server-side without changing the column, index, constraint, or resource shape.

### AC-EMP-0047 — Excluded operations are absent

FP-006 introduces no route, command, handler, permission, or table for rehire, employee documents, import, or export.

**Scope corrected 2026-08-31 (architect), from `DEC-EMP-0032`, which this criterion implements.** The sentence previously read *"No route, command, handler, permission, or table **exists**"* — an unqualified product-wide absence, where its own governing decision defers the three requirements **"whole and outside the Employee core slice"** and goes on to describe the obligations **a future import and export must satisfy**. ⚠ **An unscoped ban would have been falsified rather than violated the day FP-009 shipped employee import/export**, and `AC-EMP-0045` and `AC-EMP-0046` — the two criteria immediately above, deferring the same way — both already say *"FP-006 introduces no…"*. The criterion was the odd one out in its own section and stricter than the decision it cites; the scope is restored, not narrowed.

**What asserts it: `No_rehire_operation_exists` (`EmployeeArchitectureTests`) covers the REHIRE clause only.** The documents, import and export clauses are unasserted here **by design** — documents belong to the closed FP-010 and import/export to FP-009, so the guard that would catch a violation belongs with whichever package builds the subject, not with this one.

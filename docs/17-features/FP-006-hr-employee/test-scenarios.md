---
document_id: FP-006-TEST
title: HR Employee Test Scenarios
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Test Scenarios

> Approved for Implementation — scenarios reflecting the settled FP-006A decisions.

## Domain

- **TS-EMP-0001:** Create an Employee with a server-generated nonempty Guid `EmployeeId` and `Active` status; a created Employee is never `Inactive` or `Terminated`.
- **TS-EMP-0002:** Trim the employee number, preserve display casing, and derive exact `ToUpperInvariant()` normalization; reject empty or control-character numbers; reject a value whose normalized form exceeds 64 characters.
- **TS-EMP-0003:** Trim and preserve `FullName` display casing without treating it as a unique identity.
- **TS-EMP-0004:** Accept an absent national ID; when present, trim, normalize, and length-check it identically to the employee number.
- **TS-EMP-0005:** Permit every listed lifecycle transition (Create→Active, Active→Inactive, Inactive→Active, Active→Terminated, Inactive→Terminated) and reject every unlisted or repeated transition without changing metadata.
- **TS-EMP-0006:** Make `Terminated` terminal and preserve the aggregate for history; expose no rehire operation.
- **TS-EMP-0007:** Reject any post-creation change to `EmployeeId`, `TenantId`, `CompanyId`, or `EmployeeNumber`.
- **TS-EMP-0008:** Reject a termination whose `TerminationDate` is earlier than `EmploymentDate`; require a null `TerminationDate` while not terminated and a non-null one once terminated.
- **TS-EMP-0009:** Refuse profile update, activation, deactivation, and transfer for a `Terminated` Employee.
- **TS-EMP-0010:** Produce the initial branch-assignment record at creation with `SourceBranchId` null, `DestinationBranchId` equal to the Employee's branch, and reason `InitialAssignment`; refuse to construct an Employee without one.
- **TS-EMP-0011:** Refuse a transfer whose destination equals the source; require a non-`InitialAssignment` reason code on transfer and reject `InitialAssignment`.
- **TS-EMP-0012:** Raise `EmployeeCreated`, `EmployeeProfileUpdated`, `EmployeeActivated`, `EmployeeDeactivated`, `EmployeeTerminated`, and `EmployeeTransferred` with bounded reason codes only, and with no employee name, national ID, employee number, or free-form reason text in any payload.

## Application

- **TS-EMP-0020:** Reject a duplicate normalized employee number within one company while allowing the same normalized number in a different company and in a different tenant.
- **TS-EMP-0021:** Reject a duplicate normalized national ID within one company while allowing many employees with no national ID.
- **TS-EMP-0022:** Update only `FullName` and `NationalId`; reject attempts to change tenant, company, branch, employee number, or status through the profile operation.
- **TS-EMP-0023:** Get one Employee and search bounded, deterministically ordered safe projections with the optional status, employee-number, company-scope, and branch-scope filters.
- **TS-EMP-0024:** Default search to `Active` and `Inactive` only; return `Terminated` employees solely when explicitly requested.
- **TS-EMP-0025:** Coordinate create, update, activate, deactivate, terminate, and transfer through one tenant Unit of Work each.
- **TS-EMP-0026:** Map a stale rowversion to a concurrency result and commit no transition, update, or transfer event.
- **TS-EMP-0027:** Prove a caller-supplied status, tenant, company, or branch value cannot override persisted state or ownership.
- **TS-EMP-0028:** Two different raw employee-number inputs within one company that normalize to the same value cannot both be created; under concurrent creation the SQL per-company unique index is authoritative and exactly one create succeeds while the other returns a deterministic conflict.
- **TS-EMP-0029:** Commit the Employee branch change and its appended assignment record in one transaction; prove neither commits without the other by forcing a failure after the branch change and observing no history row.
- **TS-EMP-0030:** Return branch history in effective order and resolve the branch effective at a supplied past instant as the record with the greatest `EffectiveFromUtc` less than or equal to it.
- **TS-EMP-0031:** Verify cancellation tokens flow through every Employee persistence and read boundary.

## Authorization

- **TS-EMP-0040:** Enforce `HR.Employees.View`, `Create`, `Update`, `Transfer`, and `Terminate` on the corresponding operations; deny an operation whose permission is absent.
- **TS-EMP-0041:** Refuse an operation for a caller holding company and branch scope but lacking the functional permission.
- **TS-EMP-0042:** Refuse an operation for a caller holding the functional permission and branch scope but lacking company scope.
- **TS-EMP-0043:** Refuse an operation for a caller holding the functional permission and company scope but lacking branch scope.
- **TS-EMP-0044:** Confirm `Platform.Tenant.Administer` widens company scope to all active companies and branch scope to all active branches, and grants no HR functional permission: an administrator without `HR.Employees.Create` cannot create and without `HR.Employees.View` cannot read.
- **TS-EMP-0045:** Establish the company context only after the full five-step live validation; refuse a company that is nonexistent, in another tenant, inactive, or outside the authorized set, with one indistinguishable refusal in every case.
- **TS-EMP-0046:** Refuse company-owned operations for a caller with zero authorized active companies; never default to all.
- **TS-EMP-0047:** Refuse a read whose `SelectedAuthorizedBranches` values are not a subset of the authorized set, identically for unauthorized, inactive, and nonexistent identifiers.
- **TS-EMP-0048:** Materialize `AllAuthorizedBranches` and `AllAuthorizedCompanies` into explicit identifier predicates; refuse the read when either authorized set is empty rather than returning unfiltered results.
- **TS-EMP-0049:** Prove no Employee code path reads `ICurrentUser.CompanyId` or the `company_id` claim as authorization, and that no `BranchId` or `CompanyId` claim is added to any token.
- **TS-EMP-0050:** Authorize a transfer's destination through the live branch access resolver rather than the execution context, and refuse a destination that is unauthorized, inactive, or in another tenant.
- **TS-EMP-0051:** Permit an inactive-source recovery transfer only for a caller holding both `Platform.Tenant.Administer` and `HR.Employees.Transfer`; refuse the identical operation without the administration authority; refuse any transfer *into* an inactive branch; and confirm the exception grants no other read or write access to the inactive source branch.

## SQL Server

These scenarios execute against real SQL Server through the real `TenantDbContext`. **They must not be replaced by mocks that merely exercise handlers.**

### B1 closure — ADR-023 LOW-1

- **TS-EMP-0060 (V):** Create a real Employee through the real `TenantDbContext` and prove `IBranchWriteAuthorizer` is **genuinely reached** — not merely that the resulting row carries a branch. The authorizer must be observed being invoked on the real save path, and the persisted row must carry the current `BranchId`.

  This is the scenario that matters most and the one that must not be weakened. Every branch test written before FP-006 passes whether or not the authorizer's call site is reached, because no production entity implemented `IBranchOwnedEntity`. A test that asserts only stamping leaves `ADR-023` decision 10 unverified and does not satisfy this scenario.

- **TS-EMP-0061 (W):** Submit an Employee for insert carrying a `BranchId` that is not the trusted current branch and confirm the save is **refused**, not silently rewritten to the trusted value.
- **TS-EMP-0062 (X):** Load an Employee, modify its `BranchId` through an ordinary update, and confirm the write boundary refuses it independently of the API contract omitting the field.
- **TS-EMP-0063 (Y):** Confirm that modifying and deleting an Employee whose `BranchId` differs from the trusted current branch are both refused.

### Revocation

- **TS-EMP-0064:** Revoke a user's branch assignment after the session has selected that branch, and confirm the next Employee write fails closed even though the session still records the branch as its active context.
- **TS-EMP-0065:** Revoke `Platform.Tenant.Administer` mid-session and confirm the holder loses implicit access to branches for which they hold no assignment rows, so the next Employee write in such a branch fails closed.
- **TS-EMP-0066:** Revoke a user's company authorization mid-session and confirm the next Employee write and the next Employee read in that company each fail closed.

### Schema and ownership

- **TS-EMP-0067:** Apply the full tenant migration chain including `AddHrEmployee` to an empty SQL Server database, and to the current tenant schema with representative preexisting companies and branches.
- **TS-EMP-0068:** Enforce the `(TenantId, CompanyId, NormalizedEmployeeNumber)` per-company unique index with exact binary-collation behavior, and confirm `BranchId` does not participate: two employees in different branches of one company cannot share a number.
- **TS-EMP-0069:** Allow the same normalized employee number in two different companies and in two different tenants.
- **TS-EMP-0070:** Enforce the filtered `(TenantId, CompanyId, NormalizedNationalId)` unique index while permitting many rows with a null national ID.
- **TS-EMP-0071:** Enforce the `Status` check constraint, the reason-code check constraints, the employment/termination date constraint, and the status-versus-termination-date coherence constraints.
- **TS-EMP-0072:** Enforce the `EmployeeBranchAssignments` check constraints: source differs from destination, and `InitialAssignment` occurs if and only if `SourceBranchId` is null.
- **TS-EMP-0073:** Query Employees only within the current tenant through the inherited global tenant query filter; confirm an Employee from another tenant is not returned.
- **TS-EMP-0074:** Reject a persisted Employee whose `TenantId` does not match the trusted current tenant, and reject a post-creation `TenantId` change.
- **TS-EMP-0075:** Reject a persisted Employee whose `CompanyId` does not match the trusted company context, reject a post-creation `CompanyId` change, and refuse cross-company modification and deletion.
- **TS-EMP-0076:** Reject a stale Employee update through SQL Server rowversion.
- **TS-EMP-0077:** Enforce restricted deletes and the deletion guards for both tables; confirm the restricted foreign keys to `tenant.Companies(CompanyId)`, `tenant.Branches(BranchId)`, and `tenant.Employees(EmployeeId)`; and confirm a `Terminated` Employee and its history are retained.
- **TS-EMP-0078:** Preserve UTC creation, modification, status-change, and transfer metadata across every operation.

### Transfer against real SQL

- **TS-EMP-0079:** Perform a successful transfer and confirm `Employee.BranchId` changes to the destination and exactly one assignment record is appended, in one transaction.
- **TS-EMP-0080:** Run two simultaneous transfers of one Employee and confirm exactly one succeeds, the other returns a deterministic concurrency conflict, and the assignment log does not fork.
- **TS-EMP-0081:** Run a transfer concurrently with an ordinary update, and a transfer concurrently with a termination, and confirm each resolves through `Employee.RowVersion` with one winner.
- **TS-EMP-0082:** Refuse a transfer supplying a stale rowversion.
- **TS-EMP-0083:** Deactivate the destination branch after the request begins but before commit, and confirm the transfer is refused because the resolver is re-asked inside the transaction.
- **TS-EMP-0084:** Revoke source-branch authorization before commit and confirm the transfer is refused; revoke destination-branch authorization before commit and confirm the same.
- **TS-EMP-0085:** Confirm no assignment record can be updated or physically deleted.

## API

- **TS-EMP-0090:** Create an Employee: require authentication, `HR.Employees.Create`, a valid company header, and a selected branch; return 201 with the server-stamped branch and resolved company; reject `tenantId`, `companyId`, `branchId`, and `status` as unknown fields.
- **TS-EMP-0091:** Update an Employee profile: accept only `fullName`, `nationalId`, and `expectedRowVersion`; reject every other field including `branchId` with `400 request.invalid`.
- **TS-EMP-0092:** Return `404 employee.not_found` identically for an unknown `employeeId`, one owned by another tenant, one in a company outside the authorized set, and one in a branch outside the authorized set.
- **TS-EMP-0093:** Transfer an Employee through its own DTO: authorize the destination server-side, return the new branch and concurrency version, and map destination refusals to `403 branch.scope_denied`, a terminated Employee to `409 employee.transition_invalid`, an equal destination to `400 request.invalid`, and a stale rowversion to `409 concurrency.conflict`.
- **TS-EMP-0094:** Activate and deactivate an Employee; return `409 employee.transition_invalid` for a transition not permitted from the current status; confirm no `reactivate` route exists.
- **TS-EMP-0095:** Terminate an Employee; reject a `terminationDate` earlier than the employment date with `400 request.invalid`; confirm termination is terminal.
- **TS-EMP-0096:** Enforce search paging defaults and maxima, deterministic ordering, the default exclusion of `Terminated`, and the scope-mode parameters; reject out-of-range paging and a `branchIds` value supplied without `branchScope=SelectedAuthorizedBranches`.
- **TS-EMP-0097:** Return branch history in effective order, authorized by the Employee's current branch rather than by the historical branches named inside it.
- **TS-EMP-0098:** Enforce canonical padded RFC 4648 Base64 rowversion; map malformed to `400 platform.rowversion_invalid`, a valid stale value to `409 concurrency.conflict`, and a missing required value to `400 request.invalid`.
- **TS-EMP-0099:** Return `409 employee.number_conflict` for a duplicate normalized number within the company and `409 employee.national_id_conflict` for a duplicate national ID, with exactly one success under concurrent creates.
- **TS-EMP-0100:** Require the company header on every route, returning `400 request.invalid` when absent and `403 company.scope_denied` when it fails validation; return `409 branch.selection_required` when the session has no selected branch for a branch-owned operation.
- **TS-EMP-0101:** Confirm internal branch and company write-boundary refusals surface as generic `403` scope denials that disclose no table name, database placement, or cross-tenant existence; and confirm there is no `DELETE` route.
- **TS-EMP-0102:** Confirm OpenAPI describes every schema, the company header, scope-mode parameters, permission, success, and error response, and matches runtime output.

## Architecture

- **TS-EMP-0110 — ADR-023 decision 22 guard:** An executable guard asserting that **every** query path reaching an `IBranchOwnedEntity` applies an explicit `BranchId` predicate, and that no code constructs an "all branches" read by omitting it. This guard discharges `ADR-023` decision 22, which that ADR records as a forward architectural rule with no enforcement, and it **must ship in the same slice as the first Employee read**. It must not be satisfiable by a global query filter.
- **TS-EMP-0111 — ADR-025 decision 10 guard:** An executable guard asserting that **every** query path reaching an `ICompanyOwnedEntity` applies an explicit `CompanyId` predicate, and that no global current-company EF query filter is introduced. It **must ship in the same slice as the first Employee read** and must not be satisfiable by a global query filter.
- **TS-EMP-0112:** Verify `Employee` implements `ITenantOwnedEntity`, `ICompanyOwnedEntity`, and `IBranchOwnedEntity`.
- **TS-EMP-0113:** Verify `EmployeeBranchAssignment` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity` and **does not implement `IBranchOwnedEntity`**; and verify neither `SourceBranchId` nor `DestinationBranchId` is mapped to a property or column named `BranchId`, so no convention or shadow property can silently reclassify the table as branch-owned.
- **TS-EMP-0114:** Verify the sanctioned transfer channel cannot be activated from a request DTO, header, form field, token claim, or arbitrary repository caller; that it authorizes one exact entity/source/destination triple scoped to a single save; and that no general "allow `BranchId` modification" flag exists.
- **TS-EMP-0115:** Verify no migration in either stream introduces a foreign key whose principal table lives in the other database, including `principalTable: "Branches"` and `principalTable: "Companies"` from the platform stream.
- **TS-EMP-0116:** Verify every persisted Employee and assignment application string column is `nvarchar`; find no `varchar`, `char`, or `text` column.
- **TS-EMP-0117:** Verify `tenant.Employees` contains no `DepartmentId`, `PositionId`, or `ManagerId` column, and that FP-006 introduces no Department or Position entity, table, or foreign key.
- **TS-EMP-0118:** Verify FP-006 introduces no numbering-sequence table, service, or configuration, and no server-side employee-number generation path.
- **TS-EMP-0119:** Keep HR Domain and Application free of EF Core, SQL Server, ASP.NET Core, HTTP, and UI dependencies; define only aggregate-specific repositories and expose no generic repository, delete method, or `IQueryable` Application boundary.
- **TS-EMP-0120:** Expose no physical-delete command, repository method, or endpoint for Employee or `EmployeeBranchAssignment`.
- **TS-EMP-0121:** Scan Employee events, commands, source, and logs for employee names, national IDs, employee numbers, free-form reason text, credentials, tokens, complete claims, secrets, or HTTP context and find none; verify handlers are asynchronous and accept cancellation tokens.
- **TS-EMP-0122:** Verify Platform Domain references no HR type, and that the ownership machinery Employee consumes is general platform infrastructure rather than HR-specific code.

## Shared→Dedicated copy

- **TS-EMP-0130:** Verify the declared tenant-owned inventory asserted in the architecture tests has been extended to exactly `Branch`, `Company`, `Employee`, `EmployeeBranchAssignment`, and that it agrees exactly with the tenant model. This assertion is designed to fail on a new tenant-owned entity so ordering, identity, and column decisions are made deliberately.
- **TS-EMP-0131:** Verify the model-derived copy plan covers `Employee` and `EmployeeBranchAssignment` without an engine change.
- **TS-EMP-0132:** Verify the derived copy order places `Company` and `Branch` before `Employee`, and `Employee` before `EmployeeBranchAssignment`, so inserts never violate referential integrity and constraints stay enabled throughout.
- **TS-EMP-0133:** Verify `RowVersion` is excluded from the `Employee` copy mapping, and that `EmployeeBranchAssignment` has no rowversion column to exclude; perform a real Shared→Dedicated cutover carrying employees and their branch history and confirm both arrive complete.

## Milestone applicability

Milestone 1 implements `TS-EMP-0001` through `TS-EMP-0133` where the corresponding infrastructure exists.

All API scenarios (`TS-EMP-0090` … `TS-EMP-0102`) are part of Milestone 1 because Employee delivers its HTTP transport in this package.

The B1 closure scenarios (`TS-EMP-0060` … `TS-EMP-0063`), the revocation scenarios (`TS-EMP-0064` … `TS-EMP-0066`), the two architecture guards (`TS-EMP-0110`, `TS-EMP-0111`), and the copy scenarios (`TS-EMP-0130` … `TS-EMP-0133`) are **carried obligations from earlier slices** and are not optional. They discharge commitments recorded in `ADR-023` (LOW-1 and decision 22), `ADR-025` (decision 10), and `ADR-020`, none of which any existing code or test records.

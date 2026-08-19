---
document_id: FP-006-AUTH
title: HR Employee Authorization Model
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Authorization Model

> Approved for Implementation — model reflecting the settled FP-006A decisions.

## Three independent dimensions

Every Employee operation requires **all three** of the following, evaluated independently:

```
functional permission        HR.Employees.<Action>            -- which OPERATION
        AND
authorized company scope     ITenantCompanyAccessResolver     -- which LEGAL ENTITY
        AND
authorized branch scope      ITenantBranchAccessResolver      -- which OPERATING LOCATION
```

**None substitutes for another.** Holding a functional permission grants no company or branch scope. Holding company or branch scope grants no operation. This is the invariant the whole model exists to protect, and fusing any two of the dimensions is the failure mode `ADR-023` and `ADR-025` were written to prevent (`BRULE-EMP-0024`, `SEC-EMP-0204`).

Company scope and branch scope are themselves **independent sibling dimensions**. A branch is an operating location of the *tenant*, not of a company (`ADR-023`), so no invariant relates the selected company to the active branch and none must be added.

## Authorization plane

Employee administration is an HR capability performed **within** a tenant by an authenticated caller that holds the required HR permission, operates in a trusted current-tenant context, holds a trusted company context, and holds a trusted branch execution context.

All permissions are `PermissionScope.Tenant`. None is platform-plane, and none is ever assignable to a platform-support principal.

## Functional permissions

Employee uses a five-permission, code-owned set following the established `<Plane>.<Resource>.<Action>` convention. All five satisfy the platform permission-name grammar of exactly three ASCII-identifier segments.

| Permission | Grants |
|---|---|
| `HR.Employees.View` | Read employees within the caller's authorized scope: get by id, search, and branch history. |
| `HR.Employees.Create` | Create an employee. |
| `HR.Employees.Update` | Update an employee profile, and activate or deactivate an employee. |
| `HR.Employees.Transfer` | Transfer an employee between branches. |
| `HR.Employees.Terminate` | Terminate an employee. |

Rationale: reading, ordinary profile maintenance, cross-branch relocation, and ending employment are meaningfully distinct responsibilities. `Transfer` is separated from `Update` because a transfer moves a record across a security partition and is the one operation permitted to change `BranchId` (`BRULE-EMP-0015`). `Terminate` is separated because termination is terminal and is a sensitive operation under `BR-PLT-0103`.

Activate and deactivate are grouped under `Update` rather than given a separate `Lifecycle` permission, because unlike Company lifecycle they are reversible, non-terminal, and carry no cross-partition or irreversible consequence.

These permissions are defined in a code-owned HR permission catalog. They are not tenant-defined. No implicit permission inheritance is assumed beyond what the existing authorization framework already provides.

## Operation classification

| Operation | Functional permission | Company scope | Branch scope |
|---|---|---|---|
| `CreateEmployee` | `HR.Employees.Create` | Selected company must be authorized | Current branch (write context) |
| `GetEmployee` | `HR.Employees.View` | Employee's company must be in authorized set | Employee's branch must be in authorized set |
| `SearchEmployees` | `HR.Employees.View` | Explicit authorized company predicate | Explicit authorized branch predicate |
| `GetEmployeeBranchHistory` | `HR.Employees.View` | Employee's company must be in authorized set | Employee's **current** branch must be in authorized set |
| `UpdateEmployeeProfile` | `HR.Employees.Update` | Employee's company must be the selected authorized company | Employee's branch must equal current branch |
| `ActivateEmployee` / `DeactivateEmployee` | `HR.Employees.Update` | As above | As above |
| `TerminateEmployee` | `HR.Employees.Terminate` | As above | As above |
| `TransferEmployee` | `HR.Employees.Transfer` | As above | **Source** = current branch, **destination** authorized separately |

Write operations require the Employee's branch to equal the current branch because the branch write boundary refuses cross-branch modification and deletion (`ADR-023`). Read operations accept any authorized branch, which is what makes multi-branch visibility possible without granting multi-branch write.

`GetEmployeeBranchHistory` is authorized by the Employee's **current** branch, not by the branches named inside the history. Requiring authorization over every historical branch would make an employee's own history unreadable after any transfer out of a branch the caller cannot reach, which defeats the purpose of retaining it. The history is a property of the Employee, and the Employee is reachable through its current branch.

## Platform.Tenant.Administer

`Platform.Tenant.Administer` is the authority that makes a user a tenant administrator. For Employee it:

- **may widen company scope** to all **active** companies of the current tenant, derived from authority with no `UserCompanyAccess` rows (`ADR-025` decision 7);
- **may widen branch scope** to all **active** branches of the current tenant, derived from authority with no `UserBranchAccess` rows (`ADR-023` decision 5);
- **does NOT grant any HR functional permission.**

A tenant administrator without `HR.Employees.Create` cannot create an employee. A tenant administrator without `HR.Employees.View` cannot read one. Holding it says which **companies and branches** are reachable; it says nothing about which **operations** are permitted.

Its one Employee-specific effect beyond scope width is the inactive-source recovery transfer (`BRULE-EMP-0021`), which additionally requires `HR.Employees.Transfer`.

## Company scope — the ADR-025 contract

### Establishing the trusted company context

`ICurrentCompany` exposes a nullable `Guid? CompanyId`. Null is not an error at that layer; it is the answer to "has a company been selected". The write and read boundaries turn it into a refusal, and only for company-owned data.

A caller-supplied `CompanyId` expresses **intent only**. The context is trusted only after this live validation, in order, failing closed at every step:

1. the trusted tenant is known;
2. the company exists;
3. the company belongs to that tenant;
4. the company is `Active`;
5. the caller is currently authorized for that company.

Steps 2 through 5 are resolved against **live state on every request**, never from a set captured at login.

### Sources of company scope

| Principal | Company scope | Source |
|---|---|---|
| `Platform.Tenant.Administer` holder | All **active** companies of the current tenant | Derived from authority; no rows |
| Normal tenant user | `UserCompanyAccess` rows ∩ active companies | Stored rows, intersected live |

`UserCompanyAccess` lives in the **platform** database, carries `TenantId`, `TenantUserId`, and `CompanyId`, and is unique on `(TenantId, TenantUserId, CompanyId)`. There is **no cross-database foreign key** to `tenant.Companies`; `CompanyId` is an opaque cross-database identifier, and existence, tenant ownership and active state are validated by the application against the tenant database before any assignment row is written (`ADR-025` decision 5).

`ITenantCompanyAccessResolver` is the **single source of truth**. No handler re-derives company scope.

### Prohibited as company authorization

The `company_id` JWT claim, `ICurrentUser.CompanyId`, and any header, body, route, or query `CompanyId` are **never** authorization proof (`ADR-025` decision 4, `SEC-EMP-0211`). No company authorization is cached into a token. FP-006 code must not read `ICurrentUser.CompanyId`.

### Zero authorized companies

A caller with zero authorized active companies is **refused** company-owned operations. It is never defaulted to "all", and it is not an account-integrity failure — there is no company equivalent of `BR-PLT-0010`.

## Branch scope — the ADR-023 contract

### Scope modes

Employee reads support exactly three branch scope modes, all producing an explicit predicate:

| Mode | Predicate | Notes |
|---|---|---|
| `CurrentBranch` | `BranchId = @activeBranch` | The default |
| `SelectedAuthorizedBranches` | `BranchId IN (@ids)` | `@ids` must be a **subset** of the authorized set; a non-subset is refused |
| `AllAuthorizedBranches` | `BranchId IN (@allAuthorizedIds)` | The authorized set is **materialized** into the predicate |

`AllAuthorizedBranches` is **never** implemented by omitting the `BranchId` predicate. "All branches" means all branches currently authorized to the requesting user (`BR-PLT-0016`, `ADR-023` decision 22).

An empty authorized branch set **refuses** the read. It never degrades to unfiltered.

A `BranchScope` value is modelled as a closed discriminated choice so that "no branch specified" is unrepresentable in the read contract.

### Sources of branch scope

| Principal | Branch scope | Source |
|---|---|---|
| `Platform.Tenant.Administer` holder | All **active** branches of the current tenant | Derived from authority; no rows |
| Normal tenant user | `UserBranchAccess` rows ∩ active branches | Stored rows, intersected live |

`ITenantBranchAccessResolver` is the single source of truth and always intersects with active branches. A retained assignment row naming a deactivated branch is not access.

### Write-side branch authorization

Branch authorization is re-evaluated **on every branch-owned write**, inside `TenantDbContext.ApplyBranchRulesAsync` through `IBranchWriteAuthorizer`, against the durable session and the live access resolver. It fails closed: no branch selected, unusable or expired session, revoked access, revoked authority, deactivated branch, missing authorizer, or missing session context each refuse the write.

`BranchId` is never accepted from a request DTO, header, form field, or token claim. A supplied value is confirmed against the trusted context and refused when it differs.

### Company scope also applies to reads by an equivalent set semantics

Employee reads carry an explicit `CompanyId` predicate over the selected company or, where a read exposes multi-company results, over the materialized authorized company set. The same rule applies: an omitted predicate never means "all companies", and an empty authorized set refuses the read (`ADR-025` decision 10).

Milestone 1 exposes `CurrentCompany` for all reads and additionally `AllAuthorizedCompanies` for `SearchEmployees`. No read mode omits the predicate.

## Transfer authorization

`TransferEmployee` is the only operation that authorizes two branches. The sequence is:

```
1. functional permission        HR.Employees.Transfer
2. company scope                the Employee's CompanyId is the selected authorized company
3. source branch                the trusted branch execution context (or BRULE-EMP-0021 recovery)
4. destination branch           ITenantBranchAccessResolver — live, active-only
5. domain rules                 not Terminated; destination differs from source
6. open the transfer channel    entity + source + destination, exactly
7. one transaction              set Employee.BranchId, append the assignment record
8. commit                       Employee.RowVersion serializes
```

Steps 3 and 4 are both **re-asked inside the transaction**. An authorization answer obtained before step 6 is not carried across the commit.

The destination `BranchId` is a **business argument that is authorized**, never an assertion of the caller's execution scope (`SEC-EMP-0212`). This does not weaken `ADR-023` decision 18: the caller's own execution scope still comes only from the server, and the destination is admitted only after an explicit live authorization check.

### Sanctioned transfer channel

The branch write boundary permits a `BranchId` modification only when an open server-controlled declaration matches the exact *(entity, source branch, destination branch)* triple. The channel:

- is opened only inside the `TransferEmployee` handler, after steps 1 through 5;
- is scoped to the single save that performs the transition;
- is **not activatable** from a request DTO, request header, form field, JWT or token claim, or an arbitrary repository caller;
- is auditable.

Every non-matching `BranchId` modification is refused exactly as today (`ADR-024` decisions 2 and 3).

### Inactive source branch recovery

Because branch authorization intersects with active branches, an Employee in a deactivated branch is otherwise unreachable by every principal, including a tenant administrator whose scope is all *active* branches.

A transfer **out of** an inactive source branch is permitted only when all of the following hold: the actor holds `Platform.Tenant.Administer`; the actor holds `HR.Employees.Transfer`; the destination branch is active and belongs to the same tenant; the operation is the explicit `TransferEmployee` operation; the transfer is audited; and normal destination authorization succeeds.

The exception is one-directional. It grants no ordinary read or write authority over the inactive branch, does not widen the administrator's branch scope for any other operation or for `ITenantBranchAccessResolver` generally, and permits only the transfer needed to relocate the Employee (`ADR-024` decision 12, `BRULE-EMP-0021`).

## Cross-boundary opacity

An `EmployeeId` belonging to another tenant, to a company outside the caller's authorized set, or to a branch outside the caller's authorized set yields the same `404` not-found result as an unknown identifier. Existence is never disclosed across a boundary.

Company and branch refusals name no database topology and do not distinguish "does not exist", "belongs to another tenant", and "is inactive" — distinguishing them would let an administrator of one tenant probe another tenant's identifiers for existence (`ADR-023` error semantics, `ADR-025` decision 3).

## Architecture guards — FP-006 implementation obligations

Both guards are **executable architecture tests** and both **must ship in the same slice as the first Employee read**. Until they exist, the corresponding ADR decision is an architectural requirement rather than a control.

### ADR-023 decision 22 guard — branch-scoped reads

An executable guard asserting that every query path reaching an `IBranchOwnedEntity` applies a `BranchId` predicate, and that no code constructs an "all branches" read by omitting it.

`ADR-023` records decision 22 as a *forward architectural rule only* — neither implemented nor enforced, because no reporting existed. Employee search is the first branch-scoped read in the product, so FP-006 owns discharging it. See `TS-EMP-0110`.

### ADR-025 decision 10 guard — company-scoped reads

An executable guard asserting that every query path reaching an `ICompanyOwnedEntity` applies a `CompanyId` predicate, and that no global current-company EF query filter is introduced.

`ADR-025` decision 10 deliberately rejects a global company query filter, because a filter pinned to one company would make authorized multi-company reads unexpressible. The guard is what makes explicit predicates safe. See `TS-EMP-0111`.

Neither guard may be satisfied by a global query filter. Both assert the presence of an explicit predicate.

## Auditing

Employee events contain domain facts only. Correlation ID, request ID, trace ID, and authenticated actor metadata remain outside Domain and use the existing event-dispatch metadata boundary.

Every transfer is independently audited by its immutable `EmployeeBranchAssignment` record, which retains the actor, the instant, both branches, and the bounded reason code regardless of event delivery. That record is the authoritative transfer audit; the domain event is a notification, not the audit trail.

Immutable security-audit storage is not delivered by FP-006 and remains a production-release dependency, on the same terms recorded for Company (`DEC-CMP-0018`).

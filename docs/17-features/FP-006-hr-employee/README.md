---
document_id: FP-006
title: HR Employee
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
depends_on:
  - ADR-013
  - ADR-014
  - ADR-017
  - ADR-020
  - ADR-023
  - ADR-024
  - ADR-025
  - FP-005
---

# Feature Package 006 — HR Employee

> **Approved for Implementation.** This package reflects the settled FP-006A design decisions and the architecture recorded in `ADR-024` and `ADR-025`. The decisions in [`decisions-approved.md`](decisions-approved.md) are binding.

## Purpose

FP-006 establishes the **Employee** aggregate: the first business record in the product owned along all three ownership dimensions — tenant, company, and branch.

An Employee is a person employed by a Company, working at a Branch, within one Tenant. FP-006 delivers the smallest Employee core that satisfies the V1 HR requirements and closes the ownership obligations that earlier slices deferred. It is not a general HR module and it does not introduce organizational structure.

## Position in the platform hierarchy

```
Platform
  └── Tenant                                   (FP-003, implemented)
        ├── Company                            (FP-005, implemented)
        ├── Branch                             (Branch foundation B0/B1, implemented)
        └── Employee                           (FP-006, this package)
              └── EmployeeBranchAssignment     (FP-006, append-only branch history)
```

Company and Branch are **sibling dimensions beneath the tenant**, not nested (`ADR-023`). Employee is scoped by both, independently.

## Architecture significance

FP-006 is the slice in which four deferred architectural commitments become real:

- **First production `IBranchOwnedEntity`.** `Employee` is the first business entity to reach `TenantDbContext.ApplyBranchRulesAsync` against real data. It converts `ADR-023` decisions 10, 16 and 18 from *structurally implemented* to *runtime-proven*, and discharges the `ADR-023` LOW-1 obligation.
- **First production `ICompanyOwnedEntity`.** `Employee` triggers the company-ownership machinery that `ADR-014` decision 6 deferred until "the first real company-owned business record", now specified by `ADR-025`.
- **First branch-scoped read model.** Employee search is expected to be the first branch-scoped read in the product, and therefore owns discharging `ADR-023` decision 22 with an executable guard, in this slice.
- **First company-scoped read/write model.** Employee likewise owns discharging `ADR-025` decision 10 with its own executable guard, in this slice.

The failure mode of getting any of these wrong is silent: an entity that should have been scoped and was not is readable across every branch or company in the tenant, and nothing about it looks wrong.

## Authoritative inputs

| Authority | Contribution |
|---|---|
| `HR.md` (`REQ-HR-0001` … `REQ-HR-0010`) | The Employee requirement set |
| `Business-Rules.md` (`BR-HR-0001` … `BR-HR-0009`) | Employee number and national-ID uniqueness, employment dates, termination, department, position |
| `Business-Rules.md` (`BR-PLT-0003`, `BR-PLT-0004`, `BR-PLT-0006`, `BR-PLT-0013`, `BR-PLT-0016`, `BR-PLT-0002`) | Soft delete, audit trail, numbering, branch transaction ownership, reporting scope, company isolation |
| `ADR-013` | `Guid` identifier strategy |
| `ADR-014` revision 1.1 | Company ownership; `tenant.Companies` placement correction |
| `ADR-020`, `ADR-023` decision 21 | Shared→Dedicated copy manifest |
| `ADR-023` | Branch ownership, write boundary, authorization, decision 22 |
| `ADR-024` | Employee branch assignment and transfer model |
| `ADR-025` | Company execution context and authorization |
| `Architecture-Principles.md` Principle 11 | Explicit ownership classification |
| `Development-Standards.md` | Rowversion transport, transport conventions |
| FP-005 | Package structure and documentation conventions **only** |

FP-005 defines conventions. Where FP-005's own architecture has been superseded — notably the Company table's database placement and the sketched global company query filter — this package follows `ADR-014` revision 1.1, `ADR-024` and `ADR-025`. Employee references `tenant.Companies`; see [`data-model.md`](data-model.md).

## Documents

1. [`requirements.md`](requirements.md)
2. [`business-rules.md`](business-rules.md)
3. [`domain-model.md`](domain-model.md)
4. [`lifecycle-model.md`](lifecycle-model.md)
5. [`authorization-model.md`](authorization-model.md)
6. [`api-contracts.md`](api-contracts.md)
7. [`data-model.md`](data-model.md)
8. [`acceptance-criteria.md`](acceptance-criteria.md)
9. [`test-scenarios.md`](test-scenarios.md)
10. [`decisions-approved.md`](decisions-approved.md)
11. [`traceability-matrix.md`](traceability-matrix.md)

## Scope (Milestone 1)

- the `Employee` aggregate, owned by tenant, company and branch;
- the `EmployeeBranchAssignment` append-only branch-history record;
- `EmployeeNumber` and `NationalId` value objects with company-wide uniqueness;
- `Active`, `Inactive`, and terminal `Terminated` lifecycle states with an explicit transition graph, where an employee is **created `Active`**;
- create, update-profile, terminate, and **transfer** commands;
- get-by-id, bounded paginated search, and branch-history queries;
- a code-owned HR employee permission set (`HR.Employees.*`);
- the `ICompanyOwnedEntity` interface, the company write boundary, `ICurrentCompany`, `UserCompanyAccess`, and `ITenantCompanyAccessResolver` (`ADR-025`);
- the sanctioned branch-transfer channel (`ADR-024`);
- explicit company and branch read predicates with two executable architecture guards;
- persistence in the existing tenant SQL Server boundary (`tenant.Employees`, `tenant.EmployeeBranchAssignments`);
- optimistic concurrency, safe domain events, history retention, and test requirements;
- the HTTP surface under `/api/hr/employees`.

## Architecture constraints

- Multi-tenant modular monolith; Clean Architecture, DDD, and CQRS.
- HR owns the Employee aggregate, application contracts, persistence configuration, and API. Platform owns the ownership machinery Employee consumes.
- `Employee` implements `ITenantOwnedEntity`, `ICompanyOwnedEntity`, and `IBranchOwnedEntity`.
- `EmployeeBranchAssignment` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity`, and **not** `IBranchOwnedEntity` (`ADR-024`).
- `EmployeeId` is a `Guid` (`ADR-013`).
- Repositories are aggregate-specific; no generic repository and no `IQueryable` boundary (`ADR-010`).
- Domain and Application remain free of EF Core, SQL Server, ASP.NET Core, and HTTP.
- All persisted application strings are SQL Server `nvarchar`.
- No foreign key crosses the platform/tenant database boundary.
- State persists through the existing `TenantDbContext` and tenant Unit of Work; no second context is introduced.
- Domain events contain no secrets and are dispatched only after successful persistence; no outbox is introduced.
- The HTTP surface adopts the shared Platform admin-transport conventions (ProblemDetails, the Platform rowversion convention, security headers, strict JSON, OpenAPI).

## Source requirements

FP-006 realizes `REQ-HR-0001` (Create Employee), `REQ-HR-0002` (Update Employee), `REQ-HR-0003` (Terminate Employee), `REQ-HR-0004` (Transfer Employee), `REQ-HR-0007` (Employee Status), and `REQ-HR-0008` (Employee Search).

It realizes the **branch-assignment portion** of `REQ-HR-0006` (Employee History) through `EmployeeBranchAssignment`. Broader employee history — profile field history, department history, position history — is acknowledged and deferred.

## Explicit exclusions

Each exclusion below names the requirement it defers and the obligation that carries it forward. None is discarded.

| Excluded from V1 | Source | Deferred obligation |
|---|---|---|
| **Rehire** | No source requires it | No transition out of `Terminated` exists in V1. A future package introducing rehire must decide whether it reuses the Employee identity or creates a new one, and must state the effect on `EmployeeNumber` uniqueness and branch history (`DEC-EMP-0016`) |
| **Employee Documents** | `REQ-HR-0005` | Deferred whole. No document entity, table, column, or storage binding is introduced (`DEC-EMP-0032`) |
| **Employee Import** | `REQ-HR-0009` | Deferred whole. A future import must satisfy every rule in this package, including per-record branch and company authorization; bulk insert must not bypass the write boundary (`DEC-EMP-0032`) |
| **Employee Export** | `REQ-HR-0010` | Deferred whole and outside the Employee core. A future export is a branch- and company-scoped read and inherits both guards; it must never be implemented by omitting a scope predicate (`DEC-EMP-0032`) |
| **Department** | `REQ-HR-0100`; `BR-HR-0005` | `BR-HR-0005` is retained as binding and its enforcement is deferred until the Department aggregate exists. V1 Employee creation neither requires nor persists a `DepartmentId`, and **no placeholder entity, table, column, or foreign key is introduced** (`DEC-EMP-0017`) |
| **Position** | `REQ-HR-0200`; `BR-HR-0006` | `BR-HR-0006` is retained as binding on the same terms as `BR-HR-0005` (`DEC-EMP-0018`) |
| **Automatic `EmployeeNumber` generation** | `BR-PLT-0006` | `EmployeeNumber` is user-entered in V1. No numbering-sequence table, service, or configuration is introduced. The schema and API remain forward-compatible so a future generator can supply the value server-side without schema redesign (`DEC-EMP-0011`) |
| **Durable company selection** | `ADR-025` decision 11 | No session `ActiveCompanyId` column. Company context is per-request; adding durable selection later is additive |
| **Manager / reporting line** | `BR-HR-0007` | No `ManagerId` in V1. `BR-HR-0007` (an employee cannot directly manage themselves) has no field to constrain until a reporting line exists, and is deferred with Department (`DEC-EMP-0031`) |

Physical Employee deletion is prohibited. `Terminated` is the terminal lifecycle state.

## Implementation obligations

These are carried commitments that inspection of the codebase will not reveal, because each is a deferral rather than an artefact. They are acceptance criteria for the implementation slice, not background.

1. **`ADR-023` LOW-1 real-SQL proofs (B1 V/W/X/Y).** Employee must prove, through the existing `TenantDbContext.ApplyBranchRulesAsync` path and not through new infrastructure, that: the branch write authorizer is genuinely invoked on a real Employee save (**V**); a spoofed `BranchId` is refused (**W**); ordinary update cannot mutate `BranchId` (**X**); cross-branch update and delete are refused (**Y**). **V matters most**: every branch test written before FP-006 passes whether or not the authorizer's call site is reached, so only a real Employee save proves the wiring. See `AC-EMP-0020` … `AC-EMP-0023`.
2. **Revocation proofs.** Branch assignment revoked mid-session, Tenant Administrator authority revoked mid-session, and company authorization revoked mid-session must each refuse the next Employee operation. See `AC-EMP-0024` … `AC-EMP-0026`.
3. **`ADR-023` decision 22 guard.** An executable architecture guard proving no branch-owned Employee read path can omit its `BranchId` predicate, shipped with the first Employee read.
4. **`ADR-025` decision 10 guard.** An executable architecture guard proving no company-owned Employee read path can omit its `CompanyId` predicate, shipped with the first Employee read.
5. **Shared→Dedicated copy inventory.** The declared tenant-owned inventory asserted in the architecture tests must be extended from `["Branch", "Company"]` to include `Employee` and `EmployeeBranchAssignment`, with dependency order `Company` → `Branch` → `Employee` → `EmployeeBranchAssignment`. That assertion is designed to fail on a new tenant-owned entity precisely so ordering, identity and column decisions are made deliberately. See [`data-model.md`](data-model.md) and `AC-EMP-0037` … `AC-EMP-0039`.

> **All five obligations are discharged as of `FP-006C6`.** 1 and 2 by the `B1` and revocation proofs in `FP-006C3`; 3 and 4 by the executable guards in `FP-006C4`; 5 by `FP-006C6`.
>
> Obligation 5 turned out to require more than extending the declared list. The copy plan derived its manifest from a tenant model built with **no module contributors**, so `Employee` could not appear in it however the inventory was written — a promotion would have copied Platform's tables, validated every row it copied, reported success, and left every employee behind. The manifest is now derived from the contributor-composed model resolved from the same registration the runtime uses, and the contributor-free static it previously read was removed rather than corrected, so no future caller can reach for one by accident.

> **`DEC-EMP-0030` / `AC-EMP-0040` are implemented as of `FP-006P`.** The five permissions were declared as
> code-owned constants in `FP-006C5`, and the release review found they were defined in no permission
> catalog — so no tenant role could be granted one and every Employee endpoint refused every caller, while
> the whole suite passed because tests supply permissions directly and never travel the assignment path.
> `FP-006P` adds the module permission-contribution seam (`ADR-012` r1.2): HR owns the definitions, the Host
> registers the contributor explicitly, and Platform composes them into the one catalog. The design decided
> here is unchanged — five permissions, tenant scope, activate and deactivate under `Update` — and the proof
> that closes the gap is a real role assignment through `AssignPermissionToRoleCommandHandler` followed by a
> real Employee read authorized by nothing but the resulting access-token claims.

No migration is created by this documentation package.

---
document_id: FP-008-AC
title: HR Position — Acceptance Criteria
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — Acceptance Criteria

Criteria marked **(OD)** are provisional and depend on an unresolved owner decision. Criteria marked
**(OD-POS-002)** exist only if the owner retains the grade entities.

## Create and identity

- **AC-POS-0001** — Creating a position with a code and a title produces an `Active` position whose
  `TenantId` and `CompanyId` match the caller's trusted context.
- **AC-POS-0002** — `TenantId` and `CompanyId` supplied in the request body are ignored, not honoured. A
  request naming another tenant's identifiers produces a position in the caller's own tenant.
- **AC-POS-0003** — A second position with a code that normalizes to an existing code in the same company is
  refused with `409`, and the refusal comes from the unique index under concurrent creation, not only from a
  prior read.
- **AC-POS-0004** — The same code is free in a different company.
- **AC-POS-0005** — A blank or whitespace-only title or code is refused.
- **AC-POS-0006** — A created position has no employee reference of any kind, and `tenant.Positions` has no
  column referencing `tenant.Employees`.

## Company isolation

- **AC-POS-0007** — Reading a position belonging to a company outside the caller's authorized scope returns
  `404`, not `403`.
- **AC-POS-0008** — A caller whose authorized company set resolves empty is refused; no read returns
  unfiltered results.
- **AC-POS-0009** — Revoking the caller's company access mid-session refuses the next position read without
  requiring a new token.
- **AC-POS-0010** — Deactivating the company mid-session refuses the next position write.
- **AC-POS-0011** — A position may not reference a grade belonging to another company.

## Grades **(OD-POS-002)**

- **AC-POS-0012** — A grade is created with a code, a name and a `RankOrder`, and reads back with all three.
- **AC-POS-0013** — Two grades in the same company and ladder may not share a `RankOrder`; the refusal comes
  from the unique index.
- **AC-POS-0014** — `RankOrder` is authoritative: ordering grades by rank produces an order that a lexical
  sort of the codes does not, proven with `G9` and `G10`.
- **AC-POS-0015** — A position may reference a grade in the same company, and reads back with it.
- **AC-POS-0016** — A position may not reference an `Inactive` grade.
- **AC-POS-0017 (OD-POS-002 (i))** — A Job Grade may reference a Salary Grade. **No Salary Grade may
  reference a Job Grade**, and the composed model contains no such foreign key.

## Salary amounts **(OD-POS-004)**

- **AC-POS-0018 (OD)** — Amounts are stored at `decimal(19,4)` and round-trip a three-decimal currency value
  without loss.
- **AC-POS-0019 (OD)** — Amounts out of order (`Minimum > Midpoint`, or `Midpoint > Maximum`) are refused,
  and the database check constraint refuses them as well when written directly in SQL.
- **AC-POS-0020 (OD)** — A negative amount is refused.
- **AC-POS-0021 (OD)** — A salary grade may be created with no amounts at all, and reads back with nulls.
  **This is what makes `OD-POS-001` Option A possible without inventing money.**
- **AC-POS-0022 (OD)** — `tenant.SalaryGrades` has **no currency column**. The representation's
  `currencyCode` is the owning Company's `BaseCurrencyCode`, and sending it on a write is rejected as an
  unknown property.

## Lifecycle

- **AC-POS-0023** — A newly created position is `Active`.
- **AC-POS-0024** — Deactivating and reactivating a position records `StatusChangedUtc` and
  `StatusChangedBy` on each transition, and both directions require `HR.Positions.Deactivate`.
- **AC-POS-0025** — An `Inactive` position refuses a new employee, on creation and on position change alike.
- **AC-POS-0026** — An `Inactive` position remains readable and appears in lists, marked inactive.
- **AC-POS-0027** — No route, handler, or repository method deletes a position or a grade; the composed HTTP
  surface exposes no `DELETE` verb.
- **AC-POS-0028 (OD-POS-005)** — **Under reading (i):** deactivating a position with incumbents succeeds, and
  every incumbent retains it. **Under reading (ii)/(iii):** the deactivation is refused with
  `position.has_incumbents`, and succeeds once the last incumbent has moved. *One of these two criteria is
  correct and the other is wrong; which, is the owner decision.*
- **AC-POS-0029 (OD-POS-002)** — A grade with `Active` positions referencing it may not be deactivated, and
  deactivation does not cascade to them.

## Employee assignment

- **AC-POS-0030 (OD)** — An employee created after FP-008 requires a `positionId` in the same tenant, the
  same company, and `Active` status.
- **AC-POS-0031** — `positionId` is not accepted on the ordinary employee profile update; sending it is
  rejected as an unknown property.
- **AC-POS-0032** — Changing an employee's position updates `PositionId` and appends exactly one
  `EmployeePositionAssignment` record, in one transaction. Neither happens without the other.
- **AC-POS-0033** — The initial assignment record has a null `SourcePositionId`, and no other record does.
- **AC-POS-0034** — A change to the position the employee already holds is refused with `position.unchanged`,
  and no history record is written.
- **AC-POS-0035** — A branch transfer leaves `PositionId` untouched, a department change leaves it untouched,
  and a position change leaves `BranchId` and `DepartmentId` untouched.
- **AC-POS-0036** — Terminating an employee leaves their `PositionId` and their full assignment history
  intact.
- **AC-POS-0037** — No update or delete path exists for `EmployeePositionAssignment`; the entity implements
  `IAppendOnlyEntity` and the guard that asserts append-only entities carries no `RowVersion` covers it.
- **AC-POS-0038 (OD-POS-001)** — Employees that existed before FP-008 are handled exactly as `OD-POS-001`
  directs. **Until that decision is recorded, this criterion has no testable content and the package does not
  claim `BR-HR-0006` is satisfied.**
- **AC-POS-0039 (OD-POS-001, Options A/B/D)** — The migration's follow-up obligation exists and is named: for
  A, the collision pass; for B and D, the later `NOT NULL` migration. A nullable column with no named
  follow-up fails this criterion.
- **AC-POS-0040 (OD-POS-001, Option A)** — If a company already holds a position or grade whose normalized
  code collides with the seeded one, the migration fails loudly and transactionally, names the offending
  companies and codes, and **writes nothing** — the collision pass runs over every affected company before any
  write.

## Authorization

- **AC-POS-0041** — Each of the position permissions authorizes exactly its own operations and no other; a
  caller holding `HR.Positions.View` cannot create, update, or deactivate.
- **AC-POS-0042** — `HR.Positions.*` does not authorize the employee change-position route, and
  `HR.Employees.*` does not authorize any position route. Permission bleed is proven in **both** directions.
- **AC-POS-0043** — Every permission this package names is **defined in the composed catalog** and can be
  granted to a role. A name present in `HrPermissionNames` but absent from the catalog fails this criterion.
- **AC-POS-0044** — `Platform.Tenant.Administer` widens company scope and grants none of these permissions.
- **AC-POS-0045 (OD-POS-004)** — `HR.SalaryGrades.View` is a distinct permission; holding
  `HR.Positions.View` alone does not read salary amounts.
- **AC-POS-0046** — `PositionReadScope` cannot be constructed outside its resolver, carries no branch scope,
  and no read method omits it.

## Persistence and concurrency

- **AC-POS-0047** — Every position and grade mutation refuses a stale `RowVersion` with `409`.
- **AC-POS-0048** — `EmployeePositionAssignment` has no `RowVersion` column, and two concurrent position
  changes for one employee serialize on `Employee.RowVersion` with exactly one winner.
- **AC-POS-0049** — The model admits no state in which one employee has two current positions.
- **AC-POS-0050** — Assigning an employee to a position that is concurrently deactivated either refuses or
  succeeds against the pre-deactivation state; it never produces an employee holding an inactive position.
- **AC-POS-0051** — Every scoped read is served by an index whose leading keys are tenant then company; no
  scoped read is served by a scan that ignores a scope column.

## Cutover and ownership classification

- **AC-POS-0052** — Every new table is present in the derived E3 copy manifest **without any hand-maintained
  registration**, and the exact-list assertion names all of them.
- **AC-POS-0053** — The derived copy order places every grade before Position, Position before Employee, and
  Employee before every assignment table.
- **AC-POS-0054** — The restore-verification `DROP TABLE` list is exactly the copy order read backwards, and
  the drop succeeds against a real database carrying the new foreign keys.
- **AC-POS-0055** — **A constructed model containing a direct `Position → Employee` foreign key fails with
  `CutoverCopyOrderUndecidable`.** The failure mode is asserted executably, not described in prose.
- **AC-POS-0056** — `Position` does not implement `IBranchOwnedEntity`, and the composed EF model contains no
  `BranchId` column on any table this package introduces. The assertion reads the **composed model**, not
  migration files.
- **AC-POS-0057** — `EmployeePositionAssignment` is tenant- and company-owned and **not** branch-owned.

## Transport

- **AC-POS-0058** — Routes and handlers stand 1:1; the exact route inventory matches in **both** the module
  harness and the Host composition. A route reachable in one and not the other fails this criterion.
- **AC-POS-0059** — Position errors answer in the `position.*` / `job_grade.*` / `salary_grade.*` namespaces
  and never in `employee.*` or `department.*`.
- **AC-POS-0060** — `HR.API` references no Platform assembly.
- **AC-POS-0061 (OD-POS-002)** — A grade unique-constraint violation is distinguished correctly between the
  code index and the rank-order index; a rank collision does not answer `job_grade.code_conflict`.

## Scope boundaries

- **AC-POS-0062** — `tenant.Employees` gains no salary, wage, rate, or compensation column, and no such value
  is stored anywhere in this package.
- **AC-POS-0063** — No `Employee.ManagerId` is introduced, and — unless `OD-POS-006` says otherwise — no
  `Position.ReportsToPositionId`.
- **AC-POS-0064** — No headcount, establishment, or vacancy column exists, and any number of employees may
  hold one position.

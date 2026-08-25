---
id: ADR-026
title: HR Organizational Structure and Department Hierarchy
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-08-25
owner: Solution Architecture Team
tags:
  - hr
  - department
  - hierarchy
  - ownership
  - company
  - cutover
  - architecture
depends_on:
  - ADR-013
  - ADR-014
  - ADR-017
  - ADR-020
  - ADR-023
  - ADR-024
  - ADR-025
used_by:
  - FP-007
---

# ADR-026: HR Organizational Structure and Department Hierarchy

---

# Status

**Accepted** — 2026-08-25.

This ADR named its own acceptance test: `decision 4`, `decision 9` and `decision 10` depended on owner
input recorded as `OD-DEP-001`, `OD-DEP-003` and `OD-DEP-005`, and it stated it should not be accepted
until those were answered. **They were answered, and the test passes.**

**Evidence:** all five FP-007 owner decisions closed on 2026-08-20 — `OD-DEP-001` → `DEC-DEP-0009`
(fail-loud `BR-HR-0005` enforcement), `OD-DEP-003` → `DEC-DEP-0014`, `OD-DEP-005` → `DEC-DEP-0019`.
`Department` shipped in PR #45, and `tests/Architecture.Tests/DepartmentApplicationArchitectureTests.cs`
carries executable guards tagged `[Trait("Decision", "ADR-026")]`.

---

# Context

FP-006 delivered Employee and deliberately introduced no organizational structure. Four business rules were
retained as binding with their enforcement deferred to "the package that introduces them": `BR-HR-0005`
(one department per employee), `BR-HR-0007` (no self-management), `BR-HR-0008` (no circular hierarchies) and
`BR-HR-0009` (inactive departments receive nobody).

Department is the first hierarchical aggregate in the product, the first record that takes one ownership
dimension while deliberately refusing another, and the first business rule to apply retroactively to rows that
already exist. It is also the first change to a shipped aggregate: Employee gains a department.

None of that is Department-specific. Position will follow immediately behind it with the same ownership
question and the same retroactive-rule problem, and any future org structure — cost centres, teams, job
families — will inherit whatever pattern is set here. That is why these decisions belong in an ADR rather than
inside one feature package.

---

# Decision

## Decision 1 — Department is Tenant + Company owned, and not Branch owned

`Department` implements `ITenantOwnedEntity` and `ICompanyOwnedEntity`. It does **not** implement
`IBranchOwnedEntity` and carries no `BranchId`.

`BR-PLT-0013` scopes branch ownership to *transactions*; a Department is a master organizational record.
Decisively, `ADR-024` already settles the consequence: employee branch transfer is a sanctioned, branch-only
operation with its own history and dual-branch authorization. If departments were branch-owned, every transfer
would strand the employee in a branch where their department does not exist, breaking `BR-HR-0005` on every
transfer — and `ADR-024` provides for nothing of the kind. A branch-owned Department would require amending an
accepted ADR.

**Implementation status: not implemented.** Depends on `OD-DEP-002` (owner confirmation of the business
reading).

## Decision 2 — The absence of branch ownership is asserted, not assumed

An architecture guard asserts that `Department` does not implement `IBranchOwnedEntity` and that no `BranchId`
column exists in the composed EF model. Employee implements all three dimensions; Department sitting beside it
with two reads as an oversight until something says otherwise.

The assertion reads the **composed model**, not migration files. TEST-001 established why: a guard that
enumerates files can be green and blind.

**Implementation status: not implemented.**

## Decision 3 — Hierarchy is an adjacency list

A nullable self-referencing `ParentDepartmentId`. Closure tables and materialized paths buy read performance
by maintaining derived state, and derived state can drift from its source — a trade this codebase has
consistently refused (`TenantCutoverCopyPlan` derives its manifest rather than declaring it). Department
hierarchies are small, and recursive CTEs are adequate at that size.

A closure table may be added later as a pure read optimization **derived from** the adjacency list. The
reverse migration is not available, which is a further reason to start here.

**Implementation status: not implemented.**

## Decision 4 — The acyclicity invariant is transactional, evidence-based, and serialized per company

`BR-HR-0008` is enforced as: self-parent refused in the aggregate; cross-company parent refused; inactive
parent refused; descendant-as-parent refused by walking **upward from the proposed new parent**.

Two properties make this more than a validation call:

1. **The aggregate cannot be called without the evidence.** `ChangeParent` accepts an ancestry value only the
   repository can produce, so a handler that skipped the check does not compile. This is the same shape as
   `EmployeeReadScope` in FP-006, and for the same reason: a rule enforced by remembering to call a validator
   is a rule that will eventually not be called.
2. **Re-parent operations serialize on `(TenantId, CompanyId)`.** Two concurrent moves can each read a
   consistent ancestry and jointly form a cycle — move A under B while moving B under A. Each is individually
   legal. `SERIALIZABLE` isolation was rejected: it would make an invisible property of the transaction
   responsible for a named business rule.

Only the self-parent case is expressible as a SQL Server constraint, and it is one
(`CK_Departments_ParentIsNotSelf`). **The asymmetry is stated rather than hidden**: one branch of `BR-HR-0008`
has a database guarantee; the rest has a transactional one, proven against real SQL.

**Implementation status: not implemented.**

## Decision 5 — A company may have multiple root departments

Requiring a single root would force an artificial "Company" node into every hierarchy.

**Implementation status: not implemented.**

## Decision 6 — Ownership-adjacent fields change only through sanctioned channels

`Employee.DepartmentId` gets no public setter and is not a field on the ordinary profile update. It changes
only through an explicit `ChangeDepartment` operation. This extends the rule `ADR-024` established for
`BranchId` to the second ownership-adjacent field, making it the pattern rather than a one-off.

**Implementation status: not implemented.**

## Decision 7 — A hierarchical entity's manager association is a separate table

The manager of a Department is recorded in `tenant.DepartmentManagers` (primary key `DepartmentId`, foreign
keys to Department and to Employee), **not** as a `ManagerEmployeeId` column on `Department`.

This is not stylistic. `Department.ManagerEmployeeId → Employee` together with
`Employee.DepartmentId → Department` forms a cycle in the table-level foreign-key graph.
`TenantCutoverCopyPlan.Order` places tables principals-before-dependents and returns
`CutoverCopyOrderUndecidable` when no table is ready — verified in source. **A direct manager foreign key would
break Shared→Dedicated cutover for every tenant.** The association table is a dependent of both and a principal
of neither, so the graph stays acyclic, referential integrity is fully preserved, and Platform's cutover engine
is untouched.

**Condition under which this should be revisited:** if the cutover engine ever gains cycle-aware copying — a
two-pass insert that lands rows with nullable references first and fills them after their principals arrive —
then the direct column becomes available and is the better model. That is an ADR-level change to a
Platform-owned component with its own proven guards, and an HR feature package must not make it as a side
effect of needing a column.

**Implementation status: not implemented.**

## Decision 8 — Organizational structure is not an authorization dimension

Department is a filterable attribute. No read is *scoped by* department, and no `DepartmentReadScope`-style
fourth dimension joins tenant, company and branch. `ADR-025` decision 8's three independent dimensions remain
three.

**Implementation status: not implemented.**

## Decision 9 — Retroactive business rules require an explicit enforcement strategy, recorded before migration

`BR-HR-0005` binds employees created before the column existed. A rule that applies to existing rows cannot be
enforced merely by making a column non-nullable, and the choice between backfilling a synthetic default,
accepting a nullable interim, or blocking until the data is corrected is a **business** decision with different
data, different rollout risk, and a different meaning for what the rule means in the interim.

**The strategy must be recorded before the migration is authored.** The migration's steps differ materially
between options, and a nullable column shipped "for now" with no committed remediation is how a binding rule
becomes advisory.

This decision generalizes: Position will face the identical problem with `BR-HR-0006`, and should follow
whatever is chosen here.

**Implementation status: not implemented.** Depends on `OD-DEP-001`.

## Decision 10 — A rule with no field to constrain is recorded as unenforceable, not quietly satisfied

`BR-HR-0007` ("an employee cannot directly manage themselves") presumes an employee→manager reporting line.
**No repository authority defines one.** FP-006 deferred `ManagerId` entirely, and the requirement catalog
contains no requirement for a reporting line.

A Department manager is a different relationship. Treating it as if it satisfied `BR-HR-0007` would mark a
binding rule as covered when it is not — precisely the failure the traceability discipline exists to catch.

The rule is therefore recorded as **partially enforceable at best**, with the interpretation left to the owner
and the remainder transferred explicitly to a package that may never arrive. **Where a rule cannot be
enforced, the honest record is that it is open.**

**Implementation status: not implemented.** Depends on `OD-DEP-003`.

---

# Consequences

## Positive

- Ownership classification for org structure is settled once, and Position inherits it.
- The acyclicity invariant is unforgettable by construction rather than by review.
- The cutover break is caught in design, before a migration exists, rather than by a red nightly after merge.
- Retroactive rule enforcement gains a named, reusable process instead of being decided per package.

## Negative

- `tenant.DepartmentManagers` is an extra table for one logical field, and it exists to route around a
  limitation of the copy engine rather than because the domain asked for it. Decision 7 names the condition
  for removing it, so the reason does not decay into folklore.
- Per-company serialization of re-parent operations is a lock that most systems would not need. Departments
  move rarely, so the cost is negligible and the alternative is a correctness hole.
- Recursive CTEs will eventually be the wrong answer if hierarchies grow far beyond expectation. The migration
  path to a closure table is open and one-directional in our favour.

## Risks

| Risk | Mitigation |
|---|---|
| A future entity adds a mutual foreign key and breaks cutover again | `TS-DEP-0044` asserts the failure mode executably: a constructed model with the direct manager FK must fail with `CutoverCopyOrderUndecidable` |
| `OD-DEP-001` chooses a nullable interim and nobody remediates | `AC-DEP-0039` requires the follow-up migration to exist and be named; option C is recommended against explicitly |
| Department ownership is later found to need branch scope | Would require amending this ADR and `ADR-024`. The guard in decision 2 ensures the change is deliberate rather than accidental |

---

# Alternatives Considered

| Alternative | Rejected because |
|---|---|
| Branch-owned Department | Breaks `BR-HR-0005` on every `ADR-024` branch transfer, and duplicates hierarchies per branch |
| Closure table for hierarchy | Derived state that can drift, for read performance not needed at this scale |
| Materialized path | Same drift concern, plus a depth cap encoded in a string length |
| `SERIALIZABLE` isolation for the acyclicity check | Makes an invisible transaction property responsible for a named business rule |
| Trigger-based cycle enforcement | Hides business logic from every reader of the domain, and the tenant schema's trigger inventory is itself asserted by a guard |
| No foreign key on the manager column | Preserves the natural model at the cost of referential integrity on a column pointing at real people |
| Two-pass cutover copy | The correct long-term answer, but it is a Platform-engine change and must not be made as a side effect of an HR feature |
| Deferring the manager feature | `REQ-HR-0102` is in scope and explicitly requested |

---

# Deferred obligations

**Position is the next org-structure aggregate** and must close two things this ADR opens:

1. `BR-HR-0006` on the terms `BR-HR-0005` is settled under (decision 9).
2. Whether Position is company-owned like Department, or has different ownership semantics — decided
   explicitly, not by copying.

**An employee reporting line, if one is ever required**, must close the remainder of `BR-HR-0007`
(decision 10) and state whether the departmental reading remains in force alongside it.

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-20 | Solution Architecture Team | Proposes the HR organizational-structure model: Department ownership, hierarchy representation and invariant, the manager association shape and its cutover cause, and the process for retroactive and unenforceable business rules. Ten decisions, three of them dependent on owner input. |

---
document_id: FP-008-DEC
title: HR Position — Decisions
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — Decisions

> **Draft.** These decisions are *proposed* by the FP-008A analysis. Six are marked
> **OWNER-DECISION-REQUIRED** and are recorded as **OPEN** — they are not decided here, and nothing in this
> package should be implemented until they are answered.
>
> Every decision carries one of three classifications. The distinction is the point of this document:
>
> | Classification | Meaning |
> |---|---|
> | **SETTLED-BY-PRECEDENT** | An existing ADR or approved package already decides this. The citation is given, and the decision is stated only so that the next reader does not have to rediscover it |
> | **PROPOSED** | An engineering recommendation offered for architect ratification. Reasoned, but not authoritative until ratified |
> | **OWNER-DECISION-REQUIRED** | Turns on business semantics no repository authority settles. Stated as an `OD-POS-###` with options and consequences, and **never** as a recommendation dressed as a conclusion |
>
> `ADR-026`'s deferred obligations require Position's ownership to be "decided explicitly, not by copying".
> `DEC-POS-0001` is therefore PROPOSED rather than SETTLED, even though the precedent is strong and the
> reasoning is the same. Resemblance is not authority.

## Classification summary

| Decision | Subject | Classification |
|---|---|---|
| `DEC-POS-0001` | Position ownership (tenant + company, not branch) | **PROPOSED** — `ADR-026` forbids inheriting it |
| `DEC-POS-0002` | Position carries no employee reference | **SETTLED-BY-PRECEDENT** — `ADR-026` d.7, `DEC-DEP-0022` |
| `DEC-POS-0003` | Ownership columns stamped and immutable | **SETTLED-BY-PRECEDENT** — `DEC-DEP-0003` |
| `DEC-POS-0004` | Position ↔ Department relationship | **OWNER-DECISION-REQUIRED** (`OD-POS-003`) |
| `DEC-POS-0005` | Entity set — one, two or three aggregates | **OWNER-DECISION-REQUIRED** (`OD-POS-002`) |
| `DEC-POS-0006` | Grade rank order is authoritative data | **PROPOSED** |
| `DEC-POS-0007` | Code is user-entered, normalized, unique per company | **SETTLED-BY-PRECEDENT** — `DEC-DEP-0004`, `DEC-DEP-0007`; scope conditional on `OD-POS-003` |
| `DEC-POS-0008` | Employee gains `PositionId` plus append-only history | **PROPOSED** — the *existence* of history is settled by `DEC-DEP-0016` as amended |
| `DEC-POS-0009` | `BR-HR-0006` enforcement for existing employees | **OWNER-DECISION-REQUIRED** (`OD-POS-001`) |
| `DEC-POS-0010` | `PositionId` changes only through a sanctioned channel | **SETTLED-BY-PRECEDENT** — `ADR-026` d.6, `DEC-DEP-0015` |
| `DEC-POS-0011` | Two lifecycle states, reversible, no physical delete | **SETTLED-BY-PRECEDENT** — `DEC-DEP-0011`, `BR-PLT-0003` |
| `DEC-POS-0012` | Meaning of "active"; deactivation with incumbents | **OWNER-DECISION-REQUIRED** (`OD-POS-005`) |
| `DEC-POS-0013` | Grade deactivation refused while active dependents exist | **PROPOSED** — mirrors `BRULE-DEP-0022` |
| `DEC-POS-0014` | Whether SalaryGrade carries money | **OWNER-DECISION-REQUIRED** (`OD-POS-004`) |
| `DEC-POS-0015` | Currency carrier, if money is carried | **PROPOSED**, conditional on `DEC-POS-0014` |
| `DEC-POS-0016` | Decimal precision, if money is carried | **PROPOSED**, conditional; **escalated to `ADR-027`** |
| `DEC-POS-0017` | Position hierarchy and the `BR-HR-0007` remainder | **OWNER-DECISION-REQUIRED** (`OD-POS-006`) |
| `DEC-POS-0018` | The permission set | **PROPOSED**; count conditional on `OD-POS-002` and `OD-POS-004` |
| `DEC-POS-0019` | Change-position authority | **PROPOSED** — precedent covers classification, not sensitivity |
| `DEC-POS-0020` | Read scope | **SETTLED-BY-PRECEDENT** given `DEC-POS-0001` — `ADR-026` d.8, `DEC-DEP-0019` |
| `DEC-POS-0021` | Concurrency and the `BR-HR-0006` cardinality race | **PROPOSED** |
| `DEC-POS-0022` | E3 manifest membership and copy order | **SETTLED-BY-PRECEDENT** — `DEC-DEP-0029`, `ADR-020` |
| `DEC-POS-0023` | No employee compensation field | **PROPOSED** (scope boundary) |
| `DEC-POS-0024` | Codes are never generated | **SETTLED-BY-PRECEDENT** — `DEC-DEP-0007`, `DEC-EMP-0011` |
| `DEC-POS-0025` | No headcount or establishment control | **PROPOSED** (scope boundary) |

**Six owner decisions, twenty-five decisions total.** Nine are settled by precedent and are stated here only
so nobody re-derives them; ten are engineering proposals awaiting ratification.

---

## Ownership and structure

**DEC-POS-0001** — Position is **Tenant + Company owned, and not Branch owned**. It implements
`ITenantOwnedEntity` and `ICompanyOwnedEntity`; the absence of `IBranchOwnedEntity` is asserted by an
architecture guard, exactly as Department's is.

*Reasoning, stated independently rather than inherited.* `BR-PLT-0013` scopes branch ownership to
transactions, and a Position is a master organizational record. Decisively, `ADR-024` constrains it the same
way it constrained Department: employee branch transfer is a sanctioned branch-only operation with its own
history and dual-branch authorization. If positions were branch-owned, every transfer would strand the
employee in a branch where their position does not exist, breaking `BR-HR-0006` on every transfer — and
`ADR-024` provides for nothing of the kind.

**Classification: PROPOSED, deliberately not SETTLED.** `ADR-026`'s deferred obligations require this to be
"decided explicitly, not by copying". The reasoning above is the explicit decision; ratification is the
architect's.

**DEC-POS-0002** — **Position carries no reference to any Employee.** There is no incumbent column, no
`CurrentHolderEmployeeId`, and no position-holder association table pointing from Position to Employee. Who
holds a position is answered by querying Employees by `PositionId`.

*Rationale, and it is not stylistic.* `Employee.PositionId → Position` plus any `Position.* → Employee`
foreign key is a **cycle in the table-level foreign-key graph**. `TenantCutoverCopyPlan.Order` places tables
principals-before-dependents and returns `CutoverCopyOrderUndecidable` when no table is ready — the exact
failure `RISK-DEP-001` verified in source for the naive Department manager column. Shared→Dedicated cutover
would stop working for every tenant.

**Classification: SETTLED-BY-PRECEDENT** — `ADR-026` decision 7 and `DEC-DEP-0022`. The trap is already
named; this decision exists so FP-008 does not walk into it a second time. `ADR-026` decision 7's condition
for revisiting applies here identically: if the cutover engine ever gains cycle-aware two-pass copying, a
direct reference becomes available.

**DEC-POS-0003** — `TenantId` and `CompanyId` are stamped by the persistence boundary from trusted server
context on insert, are never accepted from a caller, and never change. **SETTLED-BY-PRECEDENT**
(`DEC-DEP-0003`, `DEC-CMP-*`, `ADR-025`).

**DEC-POS-0004** — **OWNER-DECISION-REQUIRED (`OD-POS-003`).** Whether a Position is owned by a Department,
optionally linked to one, or independent of departments is not decided by this package. The engineering
recommendation is **independent**, because every other option either creates a second source of truth for an
employee's department or reverses a shipped FP-007 decision. The business question underneath — whether jobs
are defined centrally or departmentally — is the owner's.

## Entity set

**DEC-POS-0005** — **OWNER-DECISION-REQUIRED (`OD-POS-002`).** Whether `REQ-HR-0200`, `REQ-HR-0201` and
`REQ-HR-0202` describe three aggregates, two, or one is not decided by this package. **Three requirement
lines exist and are not collapsed here.** Only `BR-HR-0006` forces an aggregate, and what it forces is
Position; no business rule in the catalog mentions grades at all.

**DEC-POS-0006** — If grades exist in any form, a grade's **rank order is explicit, authoritative data** — an
integer `RankOrder`, unique within its ladder — and is **not** derived from the code string.

*Rationale.* Deriving order from a code is a derived-state-that-can-drift trade this codebase has refused
twice (`DEC-DEP-0005`, `DEC-DEP-0029`), and it is wrong on its own terms: `"G10"` sorts before `"G9"` under
every string collation the product uses. Sparse values (10, 20, 30) leave insertion room without renumbering
a live ladder.

*The honest counter-argument.* **No current requirement compares two grades.** Nothing promotes, nothing
ranks, nothing asks "is this grade higher". So `RankOrder` could be deferred. It is proposed now because
adding order later to a populated ladder means **inventing the order retroactively** for rows whose intended
sequence nobody recorded — the same class of not-free deferral `DEC-DEP-0016` described, and the reason that
deferral was reversed. **PROPOSED.**

## Identity

**DEC-POS-0007** — `Code` is user-entered, required, and unique within its scope, compared on a trimmed,
upper-invariant `NormalizedCode` stored under the `OrdinalCollation` binary collation, so the uniqueness
index is authoritative under concurrent insert rather than advisory. Codes that normalize alike collide.

**Classification: SETTLED-BY-PRECEDENT** (`DEC-DEP-0004`, `DEC-DEP-0007`, and `NormalizedEmployeeNumber`
before them) — **except for the scope**, which is **conditional on `OD-POS-003`**: uniqueness is per company
if positions are independent, and per department if they are department-owned. Under the recommended reading
of `OD-POS-003` it is per company.

**DEC-POS-0024** — Codes are never generated, derived, or sequenced. No numbering service is introduced.
**SETTLED-BY-PRECEDENT** (`DEC-DEP-0007`, `DEC-EMP-0011`, `BR-PLT-0006` deferred).

## Employee ↔ Position

**DEC-POS-0008** — Employee gains a `PositionId` column, **and** an append-only
`EmployeePositionAssignment` history table is introduced from the outset. The history mirrors
`EmployeeDepartmentAssignment` exactly rather than inventing a third history shape: tenant- and company-owned,
**not** branch-owned, no `RowVersion`, no `ModifiedUtc`/`ModifiedBy`, no `EffectiveToUtc`, a nullable
`SourcePositionId` marking the initial record and nothing else identifying it, `internal` factories so
nothing outside the domain assembly can fabricate a row, and the interval derived by ordering.

*The existence of history is not an open question.* `DEC-DEP-0016` was raised as `OD-DEP-004`, the owner
**reversed the deferral**, and Phase 1 shipped the department history from the outset. The stated reason —
that the deferral period is unrecoverable — applies to position history word for word. Re-raising it as an
owner decision would ask the owner to re-decide something they have already decided against, in a case where
the argument is identical.

*What remains PROPOSED* is the pairing of a column with a history table rather than the history table alone.
The column is what makes `BR-HR-0006`'s "one" unrepresentable-to-violate (see `DEC-POS-0021`); the history is
what makes the record complete. Department carries both, and a reader who knows one aggregate should
recognize the other. **PROPOSED.**

**DEC-POS-0009** — **OWNER-DECISION-REQUIRED (`OD-POS-001`).** The treatment of Employees that already exist
without a Position is not decided by this package, and **no recommendation is offered**. `ADR-026` decision 9
settles the process (the strategy is recorded before the migration is authored) and points at the
`DEC-DEP-0009` precedent, but it does not settle the answer. Three facts distinguish this case: the deciding
operational fact — whether any production tenant holds Employee rows — appears never to have been
established; the synthetic row may be a chain of two or three rows rather than one; and if `OD-POS-004`
makes money mandatory, one link in that chain would have to be invented with no honest value.

**DEC-POS-0010** — `Employee.PositionId` gets **no public setter** and is **not** a field on the ordinary
profile update. It changes only through an explicit `ChangePosition` operation.

**Classification: SETTLED-BY-PRECEDENT** — `ADR-026` decision 6 states this as the pattern rather than a
one-off, having extended it from `BranchId` to `DepartmentId`. `PositionId` is the third ownership-adjacent
field and inherits it without further argument.

## Lifecycle

**DEC-POS-0011** — Two states, `Active` and `Inactive`, with `Inactive` **reversible**; no physical delete,
ever; the record stays readable and referenceable so historical assignment rows keep their meaning.
**SETTLED-BY-PRECEDENT** (`DEC-DEP-0011`, `BRULE-DEP-0016`, `BR-PLT-0003`). This applies to Position and to
every grade entity that `OD-POS-002` retains.

**DEC-POS-0012** — **OWNER-DECISION-REQUIRED (`OD-POS-005`).** Whether "active" in `BR-HR-0006` qualifies the
assignment or the position's lifecycle status is not decided here, and it determines whether deactivating a
position with incumbents is allowed (the Department precedent) or refused. The engineering recommendation is
the assignment reading, because it is the only one under which the rule and the Department lifecycle
precedent are simultaneously satisfiable without inventing a bulk-reassignment operation — but the sentence
genuinely supports both readings and the choice is a business one.

**DEC-POS-0013** — A grade with `Active` dependents — positions referencing it, or a lower ladder referencing
it — may not be deactivated until those dependents are deactivated or re-pointed. **Deactivation does not
cascade.**

*Rationale, carried from `DEC-DEP-0012`:* a cascade would deactivate an arbitrary amount of structure from
one action, and the operator would have no record of what was already inactive beforehand, so reactivation
could not restore the prior state. Refusing is more work and is the only version that is reversible.
**PROPOSED** (mirrors `BRULE-DEP-0022`).

## Money

**DEC-POS-0014** — **OWNER-DECISION-REQUIRED (`OD-POS-004`).** Whether SalaryGrade carries monetary amounts
is not decided here. The "validation" reading is recorded as **unavailable in FP-008**: no employee
compensation field exists for a range to constrain, and marking a constraint as enforced when no write can
violate it is the failure `ADR-026` decision 10 names. Reachable only if `OD-POS-002` keeps SalaryGrade in
this package.

**DEC-POS-0015** — *If* money is carried: **no currency column.** Amounts are denominated in the owning
Company's `BaseCurrencyCode`.

*Rationale.* `SSAS.HR.Domain` references only `SSAS.BuildingBlocks.Domain` and
`SSAS.BuildingBlocks.SharedKernel` — verified in the project files — so `SSAS.Platform.Domain.ValueObjects.BaseCurrencyCode`
is **unreachable from HR**, compiler-enforced under `ADR-012` exactly as it was for `DepartmentApiErrorMapper`
in FP-007 Phase 4. Three options follow, and the third is proposed:

| | Approach | Cost |
|---|---|---|
| (a) | Duplicate an ISO-4217 value object inside `SSAS.HR.Domain` | Two 180-entry currency lists that will drift. The product would have two answers to "is XYZ a currency" |
| (b) | Promote `BaseCurrencyCode` into `SSAS.BuildingBlocks.Domain` | Correct long-term, but it moves a Platform-owned type from an HR feature package. `ADR-026` decision 7 refused the analogous reach into Platform's cutover engine for exactly this reason |
| (c) | **No currency column; amounts are in the Company's base currency** | Free, and truthful today: `DEC-CMP-0009` makes a company's base currency **required at creation and immutable**, so every row under one company has exactly one unambiguous currency |

**Condition for revisiting**, named so the reason does not decay into folklore: if a company ever needs grade
ladders in more than one currency, (c) is no longer sufficient and (b) becomes the right answer — as an
ADR-level change, deliberately, not as a side effect of needing a column. **PROPOSED**, conditional.

**DEC-POS-0016** — *If* money is carried: monetary columns are `decimal(19,4)`, non-negative, with
`Minimum ≤ Midpoint ≤ Maximum` enforced by a check constraint.

*This has no precedent in the product.* The only `decimal` columns anywhere are the `decimal(25,0)`
log-sequence numbers in the tenant backup tables, which are not money. `19,4` is proposed because four
decimal places accommodate the three-decimal currencies already in `BaseCurrencyCode`'s ISO-4217 set (BHD,
IQD, JOD, KWD, LYD, OMR, TND) with a guard digit, and fifteen integer digits accommodate high-denomination
currencies without scaling tricks.

**This decision sets the product's money representation, and General Ledger will inherit it.** A
feature-package decision that binds a future module is an ADR-level decision by definition, which is why it
is **escalated to `ADR-027`** rather than settled here. **PROPOSED**, conditional.

## Reporting line

**DEC-POS-0017** — **OWNER-DECISION-REQUIRED (`OD-POS-006`).** Whether Position carries
`ReportsToPositionId` is not decided here. The engineering recommendation is to **defer**: no requirement
asks for it, it would make FP-008 the second hierarchical aggregate with the full `DEC-DEP-0006` apparatus,
and inventing a reporting line so `BR-HR-0007` has something to constrain is the "quietly satisfied" failure
`ADR-026` decision 10 exists to name. If deferred, `BR-HR-0007`'s remainder transfers onward unchanged from
`DEC-DEP-0014` reading (iii).

## Authorization

**DEC-POS-0018** — The permission set is **four per entity**, following the `DEC-DEP-0017` discipline —
`View`, `Create`, `Update`, `Deactivate`; **no `Delete`** (deletion does not exist, so the permission would
authorize nothing) and **no `Manage`** catch-all (a permission whose description cannot say what it lets
someone do is one nobody can grant responsibly). Names satisfy the platform grammar of exactly three
ASCII-identifier segments, `<Plane>.<Resource>.<Action>`.

The **count** follows from `OD-POS-002`:

| `OD-POS-002` | Permission families | Total new HR permissions |
|---|---|---|
| (i) three entities | `HR.Positions.*`, `HR.JobGrades.*`, `HR.SalaryGrades.*` | 12 |
| (ii) one ladder | `HR.Positions.*`, `HR.Grades.*` | 8 |
| (iii) money deferred | `HR.Positions.*`, `HR.JobGrades.*` | 8 |
| (iv) position only | `HR.Positions.*` | 4 |

**One deliberate departure from the minimal-set discipline, flagged rather than slipped in.** If SalaryGrade
carries money, its `View` is **not** merged into `HR.Positions.View`. Pay bands are more sensitive than job
titles, and a single `View` would mean everyone who may read the org chart may also read the pay structure.
This is the same reasoning that separated `HR.Employees.Terminate` and `HR.Employees.Transfer` from `Update`
in `DEC-EMP-0030`: sensitivity, not resource identity, is what justifies a separate permission in this
codebase. **PROPOSED.**

**DEC-POS-0019** — Changing an Employee's position requires **`HR.Employees.Update`**, on the employee route
prefix, and **not** `HR.Positions.Update` and **not** `HR.Employees.Transfer`.

*What precedent settles:* `DEC-DEP-0018` and its 2026-08-21 amendment establish that a classification change
is not a partition change. A branch transfer moves a record across an authorization boundary; a department
change moves nothing across any, so it lives under ordinary update authority on the employee prefix. A
position change is a classification in exactly the same sense.

*What precedent does not settle, and why this is PROPOSED rather than SETTLED:* a position change is often a
**promotion**, which many organizations gate more tightly than an ordinary profile edit. `DEC-EMP-0030`
already shows that sensitivity — not partition-crossing — is a sufficient reason to split a permission in
this codebase. So the technical classification is settled and the sensitivity question is not. If the owner
wants promotions gated separately, the answer is a fifth employee permission, and it should be decided now
rather than after roles are granted. **PROPOSED**, with that question named.

**DEC-POS-0020** — Position reads resolve **tenant + company + functional permission**, and nothing else.
`PositionReadScope` follows `DepartmentReadScope` exactly — private constructor, `internal` factory called
from one resolver, a materialized non-empty `AuthorizedCompanyScope`, no overload that omits it — and carries
**no branch scope**, with an architecture guard asserting that absence.

Position is **not** an authorization dimension: no read is *scoped by* position, and employee search may
filter *by* position without that filter becoming a fourth dimension.

**Classification: SETTLED-BY-PRECEDENT given `DEC-POS-0001`** — `ADR-026` decision 8 and `ADR-025` decision 8
keep the three dimensions three, and `DEC-DEP-0019` settled the visibility question for a company-owned
org-structure record. The dependency is real and worth stating: **if `DEC-POS-0001` were rejected and Position
were given different ownership, this decision reopens with it.**

## Concurrency

**DEC-POS-0021** — Every Position and grade mutation carries a `RowVersion` and refuses a stale token, using
the transport convention in `Development-Standards.md`. `EmployeePositionAssignment` has **no** `RowVersion`,
because a record that is never updated has no concurrency state to protect; concurrent changes serialize on
`Employee.RowVersion`, exactly as transfers and department changes do.

**`BR-HR-0006`'s cardinality needs no arbitration mechanism, because the column shape makes a second current
position unrepresentable.** `Employee.PositionId` is one column holding one value: two concurrent assignments
race on `Employee.RowVersion` and one loses. This is the `DEC-DEP-0028` lesson restated — there, the primary
key on `DepartmentId` was "what makes a second row unrepresentable, and that is the design rather than a
backstop".

*The alternative, and why it is second-best.* If the assignment table were the sole record, with an
`IsCurrent` flag and no column on Employee, then two concurrent assignments could each insert a current row
and only a **unique filtered index** (`WHERE IsCurrent = 1`, keyed on `EmployeeId`) could arbitrate. That
index would work. It would also be arbitrating a race that the column shape never creates. **PROPOSED**, and
it is a consequence of `DEC-POS-0008` rather than an independent choice.

*One race that is real and needs the FP-007 treatment:* assigning an employee to a position that is being
deactivated concurrently. This is the same shape as `BRULE-DEP-0014` and takes the same answer — the
destination's status is validated inside the transaction that writes the assignment, not before it.

## Shared→Dedicated cutover

**DEC-POS-0022** — Every new tenant-owned table enters the E3 copy manifest **by construction, not by
registration**: `TenantCutoverCopyPlan.Build` reflects over the composed model and includes every non-owned
`ITenantOwnedEntity` with a table name, ordering them by a topological sort of the foreign-key graph. There is
no hand-maintained list to forget.

**But three assertion sites pin the current set by name and must be updated deliberately** — they exist
precisely so a new tenant-owned entity fails loudly rather than being silently absent:

| Site | What it pins | Today |
|---|---|---|
| `TenantCutoverCopySqlServerTests.C6_1_C6_2_The_cutover_manifest_covers_every_contributed_tenant_owned_entity` | The **exact** entity list | 7 entities |
| `TenantCutoverCopySqlServerTests.C6_15_The_copy_order_places_every_principal_before_its_dependents` | The topological order | Departments before Employees |
| `TenantRestoreVerificationProviderSqlServerTests` | The `DROP TABLE` list, **the reverse of the copy order** | 6 tables |

Under `OD-POS-002` option (i) the manifest goes from **7 entities to 11** (`Position`, `JobGrade`,
`SalaryGrade`, `EmployeePositionAssignment`); under (ii) or (iii) to 10; under (iv) to 9. The derived copy
order becomes, with the recommended readings of `OD-POS-003` and `DEC-POS-0002`:

```
Company → Branch → Department → SalaryGrade → JobGrade → Position → Employee → { EmployeeBranchAssignment,
                                                                                 EmployeeDepartmentAssignment,
                                                                                 EmployeePositionAssignment }
```

and the restore drop list is that sequence read backwards. **SETTLED-BY-PRECEDENT** (`DEC-DEP-0029`,
`ADR-020`); recorded here so the three sites are edited as one deliberate act rather than discovered one red
test at a time.

## Exclusions

**DEC-POS-0023** — FP-008 introduces **no employee compensation field**. No salary, wage, rate, or pay column
is added to `Employee`, and no such value is stored anywhere in this package. A Salary Grade is a band
attached to a job; what an individual is paid is Payroll. **PROPOSED** (scope boundary).

*This is load-bearing rather than decorative:* it is the fact that makes the "validation" reading of
`OD-POS-004` empty, and the fact that makes `HR.SalaryGrades.View` a structure-disclosure question rather
than a personal-data one.

**DEC-POS-0025** — FP-008 introduces **no headcount, establishment control, or vacancy management**. A
Position is a job definition; how many people may hold it, and whether a seat is budgeted or vacant, are
establishment-control concepts no requirement asks for. Any number of employees may hold the same position.
**PROPOSED** (scope boundary).

*Worth stating explicitly* because "Position" in some HR products means a single budgeted seat held by at
most one person. If that is the owner's meaning, `BR-HR-0006` reads differently, a uniqueness constraint
appears on the assignment, and this package's model is wrong at its root — so the assumption is recorded here
rather than left implicit.

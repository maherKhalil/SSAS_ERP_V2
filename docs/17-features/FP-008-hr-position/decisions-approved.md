---
document_id: FP-008-DEC
title: HR Position — Decisions
status: Approved for Implementation
version: 1.0
---

# FP-008 — Decisions

> **Approved for Implementation.** This document recorded decisions *proposed* by the FP-008A analysis. All
> six owner decisions (`OD-POS-001` … `OD-POS-006`) were closed on 2026-08-21, and all eleven engineering
> proposals were ratified as drafted on the same date. The decisions below are binding.
>
> **The original decision text is preserved verbatim.** Where a ruling closed or changed a decision, an
> amendment is appended beneath it rather than written over it — a decision record whose history is edited
> away cannot show why an answer changed, which is the one thing it exists to show. Amendments are dated and
> cite the ruling that produced them.
>
> Every decision carries one of three original classifications. The distinction is the point of this
> document:
>
> | Classification | Meaning |
> |---|---|
> | **SETTLED-BY-PRECEDENT** | An existing ADR or approved package already decides this. The citation is given, and the decision is stated only so that the next reader does not have to rediscover it |
> | **PROPOSED** | An engineering recommendation offered for architect ratification. Reasoned, but not authoritative until ratified |
> | **OWNER-DECISION-REQUIRED** | Turns on business semantics no repository authority settles. Stated as an `OD-POS-###` with options and consequences, and **never** as a recommendation dressed as a conclusion |
>
> `ADR-026`'s deferred obligations require Position's ownership to be "decided explicitly, not by copying".
> `DEC-POS-0001` was therefore PROPOSED rather than SETTLED, even though the precedent is strong and the
> reasoning is the same. Resemblance is not authority.

## The six rulings, in the owner's terms

| Owner decision | Ruling (2026-08-21) |
|---|---|
| `OD-POS-001` | **No production employees exist.** `BR-HR-0006` is enforced from day one; `Employee.PositionId` ships `NOT NULL`; **no synthetic backfill row or chain is created.** The operational fact is **asserted by the migration, never assumed** — see `DEC-POS-0026` |
| `OD-POS-002` | **Three aggregates** — `Position`, `JobGrade`, `SalaryGrade`. Twelve permissions. The E3 manifest goes from 7 entities to 11 |
| `OD-POS-003` | **Independent of Department.** The engineering recommendation, option (a), is adopted |
| `OD-POS-004` | **SalaryGrade carries money as informational bands.** `DEC-POS-0015` and `DEC-POS-0016` activate, and `ADR-027` with them |
| `OD-POS-005` | **The assignment reading of "active".** Deactivating a Position with incumbents is **allowed**; an `Inactive` position cannot receive **new** assignments |
| `OD-POS-006` | **`ReportsToPositionId` deferred.** The `BR-HR-0007` remainder transfers onward unchanged |

**No ruling reversed an engineering recommendation.** Where this package offered one — `OD-POS-003`,
`OD-POS-005`, `OD-POS-006`, and `DEC-POS-0019`'s default — it was adopted. `OD-POS-001` and `OD-POS-002`
carried no recommendation, and `OD-POS-001` was answered by establishing the operational fact this package
said was unestablished, which is the outcome it asked for rather than a choice among its options.

## Classification summary

| Decision | Subject | Original | Final status |
|---|---|---|---|
| `DEC-POS-0001` | Position ownership (tenant + company, not branch) | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0002` | Position carries no employee reference | SETTLED-BY-PRECEDENT | **BINDING** — `ADR-026` d.7, `DEC-DEP-0022` |
| `DEC-POS-0003` | Ownership columns stamped and immutable | SETTLED-BY-PRECEDENT | **BINDING** — `DEC-DEP-0003` |
| `DEC-POS-0004` | Position ↔ Department relationship | OWNER-REQUIRED | **CLOSED** (`OD-POS-003`, 2026-08-21) — independent; recommendation adopted |
| `DEC-POS-0005` | Entity set | OWNER-REQUIRED | **CLOSED** (`OD-POS-002`, 2026-08-21) — three aggregates |
| `DEC-POS-0006` | Grade rank order is authoritative data | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0007` | Code user-entered, normalized, unique | SETTLED-BY-PRECEDENT | **BINDING**; scope **resolved to per-company** by `OD-POS-003` |
| `DEC-POS-0008` | `PositionId` column plus append-only history | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0009` | `BR-HR-0006` enforcement for existing employees | OWNER-REQUIRED | **CLOSED** (`OD-POS-001`, 2026-08-21) — no backfill; `NOT NULL` from day one; fail-loud guard in `DEC-POS-0026` |
| `DEC-POS-0010` | `PositionId` changes only through a sanctioned channel | SETTLED-BY-PRECEDENT | **BINDING** — `ADR-026` d.6 |
| `DEC-POS-0011` | Two lifecycle states, reversible, no delete | SETTLED-BY-PRECEDENT | **BINDING** — `DEC-DEP-0011`, `BR-PLT-0003` |
| `DEC-POS-0012` | Meaning of "active"; deactivation with incumbents | OWNER-REQUIRED | **CLOSED** (`OD-POS-005`, 2026-08-21) — assignment reading; recommendation adopted |
| `DEC-POS-0013` | Grade deactivation refused while dependents active | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0014` | Whether SalaryGrade carries money | OWNER-REQUIRED | **CLOSED** (`OD-POS-004`, 2026-08-21) — informational bands |
| `DEC-POS-0015` | Currency carrier | PROPOSED, conditional | **APPROVED** 2026-08-21 — **activated** and ratified as drafted |
| `DEC-POS-0016` | Decimal precision | PROPOSED, conditional | **APPROVED** 2026-08-21 — **activated**; `ADR-027` activates with it |
| `DEC-POS-0017` | Position hierarchy and the `BR-HR-0007` remainder | OWNER-REQUIRED | **CLOSED** (`OD-POS-006`, 2026-08-21) — deferred; recommendation adopted |
| `DEC-POS-0018` | The permission set | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted, **including the `HR.SalaryGrades.View` separation**. Twelve permissions |
| `DEC-POS-0019` | Change-position authority | PROPOSED | **APPROVED** 2026-08-21 — the default: `HR.Employees.Update`, no fifth permission |
| `DEC-POS-0020` | Read scope | SETTLED-BY-PRECEDENT | **BINDING** — and its dependency on `DEC-POS-0001` is now discharged |
| `DEC-POS-0021` | Concurrency and the `BR-HR-0006` cardinality race | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0022` | E3 manifest membership and copy order | SETTLED-BY-PRECEDENT | **BINDING** — manifest **7 → 11** |
| `DEC-POS-0023` | No employee compensation field | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0024` | Codes are never generated | SETTLED-BY-PRECEDENT | **BINDING** — `DEC-DEP-0007`, `DEC-EMP-0011` |
| `DEC-POS-0025` | No headcount or establishment control | PROPOSED | **APPROVED** 2026-08-21 — ratified as drafted |
| `DEC-POS-0026` | **The migration asserts the operational fact** | — | **NEW**, ruled 2026-08-21 with `OD-POS-001` |
| `DEC-POS-0027` | **The salary band is atomic** | — | **NEW**, ruled 2026-08-21 during Phase 1 |

**Twenty-seven decisions.** Six owner decisions closed, eleven engineering proposals ratified as drafted,
eight binding by precedent, and two new decisions — one created by the `OD-POS-001` ruling, one ruled during
Phase 1 implementation.

> **`OD-POS-001` is the ruling to read first**, because it is the one this package could not offer a
> recommendation for. The answer was not chosen from the tabled options — it was produced by **establishing
> the operational fact** the package said had never been established. That fact then made Option D free, and
> `DEC-POS-0026` exists so the fact is checked rather than trusted.

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

> **Amendment 2026-08-21 — RATIFIED as drafted.** The architect ratified the reasoning above as an explicit
> decision, which discharges the first of the two obligations `ADR-026` handed this package. `ADR-026`
> revision 1.1 should record that discharge, since the obligation lives in that ADR rather than here.

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

> **Amendment 2026-08-21 (`OD-POS-003` closed — recommendation adopted).** **Position is independent of
> Department.** `tenant.Positions` carries no `DepartmentId`, and no invariant relates an employee's position
> to their department. Jobs are defined centrally, as a company-wide catalog.
>
> The consequence the recommendation named is accepted knowingly: **the org chart cannot list a department's
> jobs directly.** It can only reach them through the employees who hold both, so a department with no
> employees has no visible job structure. If that reporting view is later wanted, it is a read model derived
> from employees — **not** a `Position.DepartmentId` column, which would reintroduce the second source of
> truth this decision exists to prevent.
>
> Two consequences follow immediately. `DEC-POS-0007`'s uniqueness scope, which this decision gated, resolves
> to **per company**. And `BR-HR-0005` is untouched: `Employee.DepartmentId` remains the single authority on
> an employee's department, exactly as FP-007 shipped it.

## Entity set

**DEC-POS-0005** — **OWNER-DECISION-REQUIRED (`OD-POS-002`).** Whether `REQ-HR-0200`, `REQ-HR-0201` and
`REQ-HR-0202` describe three aggregates, two, or one is not decided by this package. **Three requirement
lines exist and are not collapsed here.** Only `BR-HR-0006` forces an aggregate, and what it forces is
Position; no business rule in the catalog mentions grades at all.

> **Amendment 2026-08-21 (`OD-POS-002` closed).** **Three aggregates:** `Position`, `JobGrade` and
> `SalaryGrade`, one per requirement line. Job evaluation and pay banding are maintained separately, and the
> reference runs `Position → JobGrade → SalaryGrade`.
>
> **The reference is one-directional and must stay so.** A `SalaryGrade → JobGrade` foreign key would
> reintroduce the cycle `DEC-POS-0002` exists to prevent, in a place nobody would think to look for it.
> `AC-POS-0017` and `TS-POS-0019` assert its absence in the composed model.
>
> Determined by this ruling: **four** new tenant-owned tables (`Positions`, `JobGrades`, `SalaryGrades`,
> `EmployeePositionAssignments`); **twelve** new HR permissions; the E3 manifest **7 → 11**; and **twenty**
> new HTTP routes. Every count that this package previously expressed as a table of options is now a single
> number.

## Rank order

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

> **Amendment 2026-08-21 — RATIFIED as drafted.** `RankOrder` is `int`, `NOT NULL`, unique within
> `(TenantId, CompanyId)` per ladder, and applies to **both** `JobGrades` and `SalaryGrades` now that
> `OD-POS-002` has retained both. The counter-argument stands as recorded: nothing in V1 compares two grades,
> and the field is carried because retrofitting order onto a populated ladder cannot be done honestly.

## Identity

**DEC-POS-0007** — `Code` is user-entered, required, and unique within its scope, compared on a trimmed,
upper-invariant `NormalizedCode` stored under the `OrdinalCollation` binary collation, so the uniqueness
index is authoritative under concurrent insert rather than advisory. Codes that normalize alike collide.

**Classification: SETTLED-BY-PRECEDENT** (`DEC-DEP-0004`, `DEC-DEP-0007`, and `NormalizedEmployeeNumber`
before them) — **except for the scope**, which is **conditional on `OD-POS-003`**: uniqueness is per company
if positions are independent, and per department if they are department-owned. Under the recommended reading
of `OD-POS-003` it is per company.

> **Amendment 2026-08-21 (scope resolved).** `OD-POS-003` selected the independent reading, so uniqueness is
> **per company** for all three entities: `UX_Positions_TenantId_CompanyId_NormalizedCode`, and the same
> shape on `JobGrades` and `SalaryGrades`. The conditional clause is discharged; there is no per-department
> variant to build.

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

> **Amendment 2026-08-21 — RATIFIED as drafted.** The column-plus-history pairing is adopted. With
> `OD-POS-001` ruling that no employees exist at upgrade time, **every** employee's history begins with a
> genuine initial record written by `StampInitialAssignment` at creation — there is no backfilled cohort
> whose first record describes a migration rather than a hiring decision. The department history does carry
> such a cohort, from `20260820140653_AddEmployeeDepartment`; the position history will not.

**DEC-POS-0009** — **OWNER-DECISION-REQUIRED (`OD-POS-001`).** The treatment of Employees that already exist
without a Position is not decided by this package, and **no recommendation is offered**. `ADR-026` decision 9
settles the process (the strategy is recorded before the migration is authored) and points at the
`DEC-DEP-0009` precedent, but it does not settle the answer. Three facts distinguish this case: the deciding
operational fact — whether any production tenant holds Employee rows — appears never to have been
established; the synthetic row may be a chain of two or three rows rather than one; and if `OD-POS-004`
makes money mandatory, one link in that chain would have to be invented with no honest value.

> **Amendment 2026-08-21 (`OD-POS-001` closed — the operational fact was established).**
>
> **No production tenant holds Employee rows.** The fact this package said had never been established was
> established, and it makes the free option available: **`BR-HR-0006` is enforced from day one**,
> `Employee.PositionId` ships **`NOT NULL`**, and **no synthetic backfill row or chain is created**.
>
> There is no `UNASSIGNED` Position, no `UNASSIGNED` JobGrade, and no `UNASSIGNED` SalaryGrade. The permanent
> synthetic residue that `DEC-DEP-0009` Option A accepted for Department — a row nothing forces to empty,
> indistinguishable from real data by design — **is not created here at all.** The two aggregates therefore
> differ in their migration history, and that difference is a consequence of when the operational fact was
> established rather than of a different judgement about what is desirable.
>
> All three concerns this decision raised are discharged rather than answered: there is no chain, so nothing
> has to invent money, and no system-origin discriminator question arises.
>
> **The fact is asserted, not assumed.** See `DEC-POS-0026`, which the owner ruled as a mandatory safeguard
> alongside this decision. A migration that is correct only because of an operational claim must verify the
> claim, because the claim can be wrong and the migration cannot be undone.

**DEC-POS-0010** — `Employee.PositionId` gets **no public setter** and is **not** a field on the ordinary
profile update. It changes only through an explicit `ChangePosition` operation.

**Classification: SETTLED-BY-PRECEDENT** — `ADR-026` decision 6 states this as the pattern rather than a
one-off, having extended it from `BranchId` to `DepartmentId`. `PositionId` is the third ownership-adjacent
field and inherits it without further argument.

**DEC-POS-0026** — **The migration asserts the operational fact that licenses it.** Before any DDL, the
FP-008 migration counts rows in `tenant.Employees`. **If the count is not zero, it fails loudly and
transactionally, and writes nothing.**

*Ruled 2026-08-21 by the owner as a mandatory safeguard alongside `OD-POS-001`.*

The `OD-POS-001` ruling — `NOT NULL` from day one, no backfill — is correct **only** while no Employee rows
exist. That is an operational fact about a moment in time, and it is asserted about **every tenant database
the migration runs against**, not about one. A tenant provisioned between the ruling and the upgrade, a
restored database, a development or demo catalog, or a customer-managed database under `ADR-021` could each
hold rows the ruling did not contemplate.

What the migration must not do, in any of those cases, is what a naive `ALTER COLUMN … NOT NULL` would do:
fail on a constraint violation with an engine message, or — worse, had a default been supplied — succeed
silently and stamp every existing employee with a position nobody chose.

| Property | Requirement |
|---|---|
| **When** | A separate pass **before any write**, so the common failure never writes at all rather than relying on rollback. This is the `DEC-DEP-0009` collision-pass shape exactly |
| **Scope** | Per tenant database, each time the migration runs — not once at design time |
| **Failure** | `THROW` with a message naming the database, the row count found, and **the recorded decision** (`FP-008 DEC-POS-0009 / OD-POS-001`), so the operator reads the reasoning rather than guessing at the constraint |
| **Remedy in the message** | The one remedy: this tenant predates the ruling's premise, and the backfill strategy that `OD-POS-001` declined must be reconsidered for it — **the migration must not be edited to force it through** |
| **What it must not do** | Supply a default, delete rows, skip the column, or degrade to nullable |

**This is the same fail-loud family as `DEC-DEP-0009`**, and for the same reason recorded there: an
automatic accommodation would silently attach real employees to a record nobody chose, and no later migration
could know what they meant. The difference is what is being asserted — `DEC-DEP-0009` asserts that a code is
free, `DEC-POS-0026` asserts that a table is empty — and the discipline is identical.

Proven by `TS-POS-0043`.

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

> **Amendment 2026-08-21 (`OD-POS-005` closed — recommendation adopted).** **The assignment reading.** "One
> active position" means the employee has one *current* assignment; it does not require the position itself
> to be `Active`.
>
> Two consequences, and they are the Department shape exactly:
>
> - **Deactivating a Position with incumbents is ALLOWED.** The incumbents keep it, `BR-HR-0006` remains
>   satisfied for each of them, and no bulk-reassignment operation is needed. This mirrors `BRULE-DEP-0015`,
>   and it means a retired job may still have holders — an oddity the ruling accepts knowingly, in exchange
>   for never using one rule to break another.
> - **An `Inactive` Position may not receive a NEW assignment**, on employee creation and on position change
>   alike. This is `BRULE-POS-0013`, and the owner named its shape explicitly: the parallel of `BR-HR-0009`
>   as realized by `BRULE-DEP-0014`.
>
> `BRULE-POS-0014`'s conditional clause is discharged, and `position.has_incumbents` is **not** an error this
> package defines — there is no operation that raises it.

**DEC-POS-0013** — A grade with `Active` dependents — positions referencing it, or a lower ladder referencing
it — may not be deactivated until those dependents are deactivated or re-pointed. **Deactivation does not
cascade.**

*Rationale, carried from `DEC-DEP-0012`:* a cascade would deactivate an arbitrary amount of structure from
one action, and the operator would have no record of what was already inactive beforehand, so reactivation
could not restore the prior state. Refusing is more work and is the only version that is reversible.
**PROPOSED** (mirrors `BRULE-DEP-0022`).

> **Amendment 2026-08-21 — RATIFIED as drafted.** With three aggregates retained, this applies in two places:
> a `JobGrade` with `Active` positions referencing it, and a `SalaryGrade` with `Active` job grades
> referencing it.
>
> **The asymmetry with `DEC-POS-0012` is deliberate and worth stating**, because a reader will notice it. A
> Position with incumbents *may* be deactivated; a grade with active dependents *may not*. The difference is
> that an employee's assignment is a fact about a person that survives the position's retirement, while a
> grade reference is a structural pointer that would leave an `Active` position pointing at an `Inactive`
> grade — an incoherent tree, refused for the same reason `DEC-DEP-0006` step 3 refuses an active child under
> an inactive parent.

## Money

**DEC-POS-0014** — **OWNER-DECISION-REQUIRED (`OD-POS-004`).** Whether SalaryGrade carries monetary amounts
is not decided here. The "validation" reading is recorded as **unavailable in FP-008**: no employee
compensation field exists for a range to constrain, and marking a constraint as enforced when no write can
violate it is the failure `ADR-026` decision 10 names. Reachable only if `OD-POS-002` keeps SalaryGrade in
this package.

> **Amendment 2026-08-21 (`OD-POS-004` closed).** **SalaryGrade carries money as INFORMATIONAL bands.**
> `MinimumAmount`, `MidpointAmount` and `MaximumAmount` are stored and are internally ordered; they constrain
> nothing outside their own row, because there is nothing in FP-008 for them to constrain.
>
> The "validation" reading remains recorded as unavailable rather than rejected — the distinction matters.
> Salary-range **enforcement** transfers to Payroll as a named obligation in
> [`traceability-matrix.md`](traceability-matrix.md); it is not discarded, and FP-008 does not claim it.
>
> `DEC-POS-0015` and `DEC-POS-0016` activate. `ADR-027` activates with them, and its conditional-withdrawal
> clause is now moot.

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

> **Amendment 2026-08-21 — ACTIVATED and RATIFIED as drafted.** Option (c) is adopted.
> `tenant.SalaryGrades` has **no currency column**; `currencyCode` appears in the read representation as a
> projection of the owning Company's `BaseCurrencyCode` and is **rejected as an unknown property on write**.
> The revisiting condition stands as recorded, and `ADR-027` decision 3 now owns it at the architecture
> level.

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

> **Amendment 2026-08-21 — ACTIVATED and RATIFIED as drafted.** `decimal(19,4)`, non-negative,
> `Minimum ≤ Midpoint ≤ Maximum` by check constraint. **`ADR-027` activates**, and its conditional-withdrawal
> clause is moot; `ADR-027` decision 1 now owns the precision for the whole product, and this decision is its
> first application rather than its author.
>
> **One residual choice is flagged rather than left implicit.** The amount columns are **nullable**, and the
> reason originally recorded for that — that `OD-POS-001`'s seeded grade would otherwise have to invent
> money — **is discharged**, because the ruling creates no seeded grade. Nullability now rests on a different
> and weaker ground: a job ladder may legitimately be defined before it is priced, and a grade awaiting
> benchmarking has no honest amounts. That is a real case, but it is a smaller one than the reason it
> replaces, and the architect may wish to make the amounts mandatory now that nothing forces them to be
> optional. **Recorded as an open question for `ADR-026`/`ADR-027` review, not decided here**;
> `CK_SalaryGrades_Amounts_Ordered` is written as "when all three are present" either way.

**DEC-POS-0027** — **The salary band is ATOMIC.** `MinimumAmount`, `MidpointAmount` and `MaximumAmount` are
**all null or all present**. No partially specified band is representable, at any layer.

*Ruled 2026-08-21 during Phase 1 implementation, citing `DEC-POS-0016`.*

A grade with a minimum and no maximum is not a half-answer — it is a row nobody can act on, and it forces
every reader downstream to invent a meaning for the missing ceiling. Three independently nullable amounts
are eight possible states, six of which mean nothing; atomicity reduces that to the two that do.

The rule is stated in three places, and each is load-bearing:

| Layer | How |
|---|---|
| **Domain** | `SalaryBand` holds three NON-nullable amounts, and the VALUE is what may be absent. `SalaryBand.Create` returns `Result<SalaryBand?>` — all-null is a successful **absence**, not an error — so a caller can tell "unpriced" from "invalid" without guessing |
| **Model** | `SalaryGrade.Band` is an **optional owned type**: EF materializes it as null when the columns are null and writes all three together when it is not. The mapping cannot express a partial band |
| **Database** | `CK_SalaryGrades_Band_Atomic`, alongside the ordering and non-negativity constraints, states the same rule to SQL Server for writes that bypass the application entirely |

**Nullability's justification is define-before-price.** A job ladder is commonly laid out before it is
benchmarked, and a grade awaiting benchmarking has no honest amounts. The original justification recorded in
`DEC-POS-0016` — that `OD-POS-001`'s seeded grade would otherwise have to invent money — **died with that
ruling**, which seeds nothing. The weaker reason is recorded as the reason rather than left implied by the
stronger one it replaced.

Ordering is **non-strict**: a band whose three amounts are equal is a fixed-rate grade, which is a real
structure, and refusing it would be a rule no requirement asks for. Completeness is checked before
non-negativity, and non-negativity before ordering, so each refusal names the defect the caller must fix
first rather than whichever check happened to run earliest — a partial band has no ordering to be wrong
about.

Proven by `GradeDomainTests`, across **all six** partial combinations rather than a representative one: an
atomicity rule that held for five of six would be no rule at all.

## Reporting line

**DEC-POS-0017** — **OWNER-DECISION-REQUIRED (`OD-POS-006`).** Whether Position carries
`ReportsToPositionId` is not decided here. The engineering recommendation is to **defer**: no requirement
asks for it, it would make FP-008 the second hierarchical aggregate with the full `DEC-DEP-0006` apparatus,
and inventing a reporting line so `BR-HR-0007` has something to constrain is the "quietly satisfied" failure
`ADR-026` decision 10 exists to name. If deferred, `BR-HR-0007`'s remainder transfers onward unchanged from
`DEC-DEP-0014` reading (iii).

> **Amendment 2026-08-21 (`OD-POS-006` closed — recommendation adopted).** **`ReportsToPositionId` is
> deferred.** Position is flat: no self-reference, no acyclicity invariant, no per-company serialization
> lock, and no ancestry evidence type. FP-008 introduces **no** hierarchical aggregate.
>
> `BR-HR-0007`'s remainder **transfers onward unchanged** from `DEC-DEP-0014` reading (iii), to the package
> introducing an employee reporting line — which no current requirement asks for, and which may therefore
> never arrive. It is recorded as **OPEN** in [`traceability-matrix.md`](traceability-matrix.md), not as
> covered.
>
> The cost this decision named is accepted: position-assignment history written between FP-008 and any future
> hierarchy package will carry no reporting context, so *who reported to whom, when* is unrecoverable for that
> period. It is the smaller loss the decision described — the reporting structure's current state is
> recoverable the moment a hierarchy exists, and only its history is not.

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

> **Amendment 2026-08-21 — RATIFIED as drafted, INCLUDING the separation.** `OD-POS-002` selected three
> entities, so the set is **twelve**: `HR.Positions.{View,Create,Update,Deactivate}`,
> `HR.JobGrades.{…}`, `HR.SalaryGrades.{…}`.
>
> The architect ratified the `HR.SalaryGrades.View` separation explicitly. **It is a departure from "four,
> and deliberately not more", and it is recorded as one** — a package that grew the set while citing that
> discipline without saying so would be citing it dishonestly. The grounds are the `DEC-EMP-0030` grounds:
> sensitivity, not resource identity. Holding `HR.Positions.View` does not read pay bands (`AC-POS-0045`,
> `TS-POS-0054`).

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

> **Amendment 2026-08-21 — RATIFIED with the default.** `HR.Employees.Update`, on the employee prefix, at
> `POST /api/hr/employees/{employeeId}/change-position`. **No fifth employee permission.**
>
> **The promotion-sensitivity question was considered and declined for V1**, and that is recorded rather than
> left to look like an oversight: the owner saw the question this decision raised, weighed a separate
> `HR.Employees.ChangePosition`, and chose not to introduce one. FP-008 therefore ships **five** employee
> permissions, not six.
>
> The consequence the decision named still holds and is now a known, accepted cost: **splitting this
> permission later requires re-granting every role that held `HR.Employees.Update`.** If promotion approval
> becomes a requirement, that is the price, and it was accepted knowingly rather than discovered.
>
> Permission bleed is proven in both directions (`AC-POS-0042`, `TS-POS-0051`): position permissions do not
> authorize this route, and employee permissions do not authorize the position routes.

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

> **Amendment 2026-08-21 — dependency discharged.** `DEC-POS-0001` was ratified as drafted, so the condition
> holds and this decision is binding without qualification. `PositionReadScope`, `JobGradeReadScope` and
> `SalaryGradeReadScope` all follow the pattern; the three authorization dimensions remain three.

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

> **Amendment 2026-08-21 — RATIFIED as drafted.** No unique filtered index is introduced, because the column
> shape creates no race for one to arbitrate. The deactivation race remains real and is proven by
> `TS-POS-0058`; the `OD-POS-005` ruling does not remove it, because an `Inactive` position still refuses a
> **new** assignment.

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

> **Amendment 2026-08-21 — counts resolved.** `OD-POS-002` selected three entities, so the manifest goes
> from **7 to 11**: `Branch`, `Company`, `Department`, `DepartmentManager`, `Employee`,
> `EmployeeBranchAssignment`, `EmployeeDepartmentAssignment`, **`EmployeePositionAssignment`**, **`JobGrade`**,
> **`Position`**, **`SalaryGrade`** — the exact assertion is ordinal by type name.
>
> `OD-POS-003` selected the independent reading, so **Position has no edge to Department** and the two are
> unordered siblings with respect to each other. The copy order above stands as the derived answer; the drop
> list is it read backwards, and grows from **6 tables to 10**.

## Exclusions

**DEC-POS-0023** — FP-008 introduces **no employee compensation field**. No salary, wage, rate, or pay column
is added to `Employee`, and no such value is stored anywhere in this package. A Salary Grade is a band
attached to a job; what an individual is paid is Payroll. **PROPOSED** (scope boundary).

*This is load-bearing rather than decorative:* it is the fact that makes the "validation" reading of
`OD-POS-004` empty, and the fact that makes `HR.SalaryGrades.View` a structure-disclosure question rather
than a personal-data one.

> **Amendment 2026-08-21 — RATIFIED as drafted.** It remains load-bearing after the `OD-POS-004` ruling:
> the bands are informational **because** there is nothing to validate them against, and
> `HR.SalaryGrades.View` guards the pay *structure*, never an individual's pay — which the product still does
> not store.

**DEC-POS-0025** — FP-008 introduces **no headcount, establishment control, or vacancy management**. A
Position is a job definition; how many people may hold it, and whether a seat is budgeted or vacant, are
establishment-control concepts no requirement asks for. Any number of employees may hold the same position.
**PROPOSED** (scope boundary).

*Worth stating explicitly* because "Position" in some HR products means a single budgeted seat held by at
most one person. If that is the owner's meaning, `BR-HR-0006` reads differently, a uniqueness constraint
appears on the assignment, and this package's model is wrong at its root — so the assumption is recorded here
rather than left implicit.

> **Amendment 2026-08-21 — RATIFIED as drafted.** The assumption this decision recorded rather than left
> implicit is confirmed: a Position is a **job definition**, and any number of employees may hold one
> (`AC-POS-0064`, `TS-POS-0066`). The single-budgeted-seat reading is not the owner's meaning, so the model
> stands as drafted.

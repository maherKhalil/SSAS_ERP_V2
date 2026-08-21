---
document_id: FP-008
title: HR Position
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
depends_on:
  - ADR-012
  - ADR-013
  - ADR-014
  - ADR-020
  - ADR-023
  - ADR-024
  - ADR-025
  - ADR-026
  - FP-005
  - FP-006
  - FP-007
---

# Feature Package 008 — HR Position

> **Approved for Implementation.** This package began as analysis with six open owner decisions
> ([`OD-POS-001` … `OD-POS-006`](#owner-decisions-required-before-approval)) and eleven engineering proposals.
> **All six were closed and all eleven ratified on 2026-08-21.** The decisions in
> [`decisions-approved.md`](decisions-approved.md) are binding.
>
> The original analysis text is preserved throughout; each owner decision below carries the ruling that closed
> it, appended rather than written over.
>
> This package **settles what precedent settles** and **raised what precedent did not**. Every topic is
> classified in [`decisions-approved.md`](decisions-approved.md) as SETTLED-BY-PRECEDENT (with the citation),
> PROPOSED, or OWNER-DECISION-REQUIRED. Nothing was settled by resemblance to FP-007.

## The rulings

| Owner decision | Ruling (2026-08-21) |
|---|---|
| `OD-POS-001` | **No production employees exist.** `BR-HR-0006` enforced from day one; `Employee.PositionId` ships `NOT NULL`; **no synthetic backfill row or chain.** The fact is **asserted by the migration**, never assumed — `DEC-POS-0026` |
| `OD-POS-002` | **Three aggregates** — `Position`, `JobGrade`, `SalaryGrade`. Twelve permissions, four new tables, twenty new routes, E3 manifest 7 → 11 |
| `OD-POS-003` | **Independent of Department.** Recommendation adopted |
| `OD-POS-004` | **Money as informational bands.** `DEC-POS-0015` and `DEC-POS-0016` activate, and `ADR-027` with them |
| `OD-POS-005` | **The assignment reading of "active".** Deactivating a Position with incumbents is allowed; an `Inactive` position refuses **new** assignments |
| `OD-POS-006` | **`ReportsToPositionId` deferred.** The `BR-HR-0007` remainder transfers onward unchanged |

**The one to read first is `OD-POS-001`**, because it is the only decision this package refused to offer a
recommendation for. The answer was not chosen from the tabled options: it was produced by **establishing the
operational fact** the package said had never been established. That fact made the free option available, and
`DEC-POS-0026` exists so the fact is verified at every upgrade rather than trusted once.

**One consequence worth naming.** FP-007 shipped a permanent `UNASSIGNED` Department in every company holding
legacy employees, and `DEC-DEP-0009` accepted that residue knowingly. FP-008 creates none. The two aggregates
differ because the operational fact was established this time — not because the judgement about what is
desirable changed.

### One residual question, flagged rather than absorbed

`DEC-POS-0016`'s salary amount columns are **nullable**, and the reason originally recorded for that — that
`OD-POS-001`'s seeded grade would otherwise have to invent money — **is discharged**, because the ruling
creates no seeded grade. Nullability now rests on a weaker ground: a ladder may be defined before it is
priced. That is a real case but a smaller one, and the architect may wish to make the amounts mandatory now
that nothing forces them to be optional. It is recorded as open in `DEC-POS-0016` rather than decided here.

## Purpose

FP-008 establishes **Position**, the last organizational aggregate FP-006 and FP-007 deferred, and the one
that closes `BR-HR-0006` — *"every employee must have one active position"* — the only rule in the HR set
still recorded as transferred rather than realized.

`ADR-026` named Position explicitly as the aggregate that must close two obligations it opened, and attached
a constraint to one of them:

> **Position is the next org-structure aggregate** and must close two things this ADR opens:
> 1. `BR-HR-0006` on the terms `BR-HR-0005` is settled under (decision 9).
> 2. Whether Position is company-owned like Department, or has different ownership semantics — **decided
>    explicitly, not by copying**.

That second clause is a constraint on this document. Department's ownership is a strong precedent, and this
package proposes following it — but as a decision with its own reasoning (`DEC-POS-0001`), because `ADR-026`
forbids inheriting it silently.

## Position in the platform hierarchy

```
Platform
  └── Tenant                                   (FP-003, implemented)
        ├── Company                            (FP-005, implemented)
        │     ├── Department                   (FP-007, implemented — hierarchical)
        │     ├── SalaryGrade                  (FP-008, this package)
        │     ├── JobGrade                     (FP-008 — references one SalaryGrade, never the reverse)
        │     └── Position                     (FP-008 — flat; references one JobGrade)
        ├── Branch                             (Branch foundation B0/B1, implemented)
        └── Employee                           (FP-006, implemented)
              ├── EmployeeBranchAssignment     (FP-006, append-only branch history)
              ├── DepartmentId                 (FP-007)
              ├── EmployeeDepartmentAssignment (FP-007, append-only department history)
              ├── PositionId                   (FP-008 — NOT NULL from day one)
              └── EmployeePositionAssignment   (FP-008, append-only position history)
```

Position sits **beneath Company, beside Department rather than beneath it** — `OD-POS-003` ruled it
independent, so no `Position.DepartmentId` exists and an employee's department has exactly one authority,
`Employee.DepartmentId`, unchanged from FP-007.

Position is **flat**: `OD-POS-006` deferred `ReportsToPositionId`, so FP-008 introduces no second hierarchical
aggregate and none of the acyclicity apparatus FP-007 built.

## The authority base is thin, and that is the point

`REQ-HR-0200` (Position Management), `REQ-HR-0201` (Job Grades) and `REQ-HR-0202` (Salary Grade) exist in
`docs/00-Master-Product-Specification/Requirement-Catalog/HR.md` as **titles with no body text**. There is no
requirement statement to read, no field list, and no acceptance language. `BR-HR-0006` is one sentence.
`BR-HR-0007` is one sentence and, as `ADR-026` decision 10 records, has no field anywhere in the product to
constrain.

That thinness is why this package raises six owner decisions rather than two. Where FP-007 could lean on
`ADR-024` to answer a business question about branches, FP-008 has three requirement **titles** and must ask
what they mean.

**Verified greenfield.** No Position, Grade, or FP-008 artifact exists anywhere in the repository — no
entity, table, column, foreign key, permission, route, or document. `DEC-EMP-0018` and `DEC-DEP-0020` each
state that no `PositionId` placeholder was introduced, and the code matches: `tenant.Employees` has no
position column, and the E3 cutover manifest's exact seven-entity assertion contains nothing position-shaped.

## Architecture significance

FP-008 is the slice where three patterns established once get tested as patterns:

- **The second application of the retroactive-rule process.** `ADR-026` decision 9 established that a rule
  binding pre-existing rows needs an enforcement strategy recorded *before* the migration is authored, and it
  named Position as the next case. `BR-HR-0006` is that case, and it arrived with a complication FP-007 did
  not have: the synthetic row this backfill would need could be a **chain** of synthetic rows, and one link
  in that chain would have had to invent money. **The `OD-POS-001` ruling dissolved the complication rather
  than solving it** — no rows exist, so there is no chain and nothing to invent. What the process produced
  here was not a backfill strategy but the discovery that none was needed, plus `DEC-POS-0026` to keep that
  true.
- **The first entity that carries money.** Nothing else in the product stores a monetary amount. The only
  other `decimal` columns are `decimal(25,0)` log-sequence numbers in the backup tables. Currency lives in
  exactly one place — `Company.BaseCurrencyCode`, a `char(3)` column backed by a Platform value object that
  **HR cannot reference** (`ADR-012`, compiler-enforced). `OD-POS-004` put money in FP-008, so this package
  sets the product's money representation and General Ledger inherits it. That is an ADR-level consequence,
  and it is why **`ADR-027` is active**.
- **The second change to a shipped aggregate.** Employee gains `PositionId`, one day after gaining
  `DepartmentId`. The FP-007 migration, its collision guard and its twelve real-SQL proofs are a working
  template — which makes the *mechanics* cheap and puts all of the weight on the business decision the
  mechanics serve.

The failure mode `ADR-026` decision 9 was written against — shipping a nullable `PositionId` "for now" with no
committed remediation, so that `BR-HR-0006` quietly joins `BR-HR-0007` in the set of rules that are binding on
paper and enforced nowhere — **is not taken.** The column is `NOT NULL` from the first migration, and
`DEC-POS-0026` makes the condition that licenses it a checked precondition rather than an assumption.

## Authoritative inputs

| Authority | Contribution |
|---|---|
| `HR.md` (`REQ-HR-0200`, `REQ-HR-0201`, `REQ-HR-0202`) | The Position requirement set — **titles only, no body text** |
| `Business-Rules.md` (`BR-HR-0006`) | Every employee must have one active position — the central rule and the central collision |
| `Business-Rules.md` (`BR-HR-0007`) | No self-management — relevant only if positions carry reporting lines (`OD-POS-006`) |
| `Business-Rules.md` (`BR-PLT-0002`, `BR-PLT-0003`, `BR-PLT-0004`, `BR-PLT-0013`) | Company isolation, soft delete, audit trail, branch transaction ownership |
| `ADR-012` | Runtime module composition — **constrains the currency decision**, see `DEC-POS-0015` |
| `ADR-013` | `Guid` identifier strategy |
| `ADR-014` revision 1.1 | Company ownership; `tenant.Companies` placement |
| `ADR-020` | Shared→Dedicated cutover; the copy manifest and its ordering |
| `ADR-023`, `ADR-025` | Branch and company execution context; decision 8's independent dimensions |
| `ADR-024` | Employee branch transfer — **constrains `DEC-POS-0001`** exactly as it constrained `DEC-DEP-0001` |
| `ADR-026` | Org-structure ownership, the retroactive-rule process (d.9), the unenforceable-rule process (d.10), the association-table shape (d.7), and the two obligations it hands this package |
| `ADR-027` (drafted here) | Money representation and cross-module value-object reuse — **activated** by the `OD-POS-004` ruling |
| FP-006 | The Employee aggregate this package modifies |
| FP-007 | The closest structural precedent. Every pattern proposed here is cited to a `DEC-DEP` decision or declined explicitly |

## Documents

1. [`requirements.md`](requirements.md)
2. [`business-rules.md`](business-rules.md)
3. [`domain-model.md`](domain-model.md)
4. [`data-model.md`](data-model.md)
5. [`lifecycle-model.md`](lifecycle-model.md)
6. [`authorization-model.md`](authorization-model.md)
7. [`api-contracts.md`](api-contracts.md)
8. [`acceptance-criteria.md`](acceptance-criteria.md)
9. [`test-scenarios.md`](test-scenarios.md)
10. [`decisions-approved.md`](decisions-approved.md) — **provisional**; see the owner decisions below
11. [`traceability-matrix.md`](traceability-matrix.md)

## Explicit exclusions

FP-008 defers the following. Each names where the obligation goes; none is discarded.

| Excluded from FP-008 | Source | Deferred obligation |
|---|---|---|
| **An individual employee's salary or compensation** | Roadmap V1 Payroll | A Salary *Grade* is a band attached to a job. An employee's actual pay is a Payroll concept. **No salary, wage or compensation column is added to `Employee`** (`DEC-POS-0023`). This is what makes the "validation" reading of `OD-POS-004` unavailable: there is nothing in FP-008 for a range to validate |
| **Employee reporting line (`ManagerId`)** | `BR-HR-0007` | Unchanged from `DEC-DEP-0014` reading (iii) and `ADR-026` decision 10. `OD-POS-006` deferred the position hierarchy, so the remainder transfers onward untouched |
| **Position hierarchy (`ReportsToPositionId`)** | — | No authority defines one. **Deferred by the `OD-POS-006` ruling**, with the cost stated in `DEC-POS-0017`: reporting *history* for the deferral period is unrecoverable, though the current structure will not be |
| **Headcount, establishment control, vacancy management** | — | "How many people may hold this position" is an establishment-control concept no requirement asks for. A Position here is a job definition, not a budgeted seat (`DEC-POS-0025`) |
| **Cost centres, GL mapping, budgets** | Roadmap V1 General Ledger | Carried forward unchanged from `DEC-DEP-0021` |
| **Position codes generated automatically** | `BR-PLT-0006` | `Code` is user-entered, exactly as `EmployeeNumber` and `DepartmentCode` are (`DEC-POS-0024`) |
| **Position-scoped reads of any aggregate** | — | Nothing gains a position filter as an authorization dimension. Employee search may filter *by* position; no read is *scoped by* position (`DEC-POS-0020`, `ADR-026` d.8) |

## Owner decisions required before approval

---

### OD-POS-001 — What happens to Employees that already exist without a Position?

**This is the one-way door.** `BR-HR-0006` admits no null: every employee must have one active position.
Every Employee row in existence today has none, and there is no column to hold one. The moment Position
ships, the rule is either satisfied by a migration, suspended by a decision, or violated silently.

**`ADR-026` decision 9 already settles the *process*: the strategy must be recorded before the migration is
authored.** It does not settle the *answer*, and it says so — it says Position "should follow whatever is
chosen here", which is a pointer to a precedent, not a ruling. The precedent is `DEC-DEP-0009`: the owner
chose Option A, a seeded `UNASSIGNED` department per company, with a fail-loud collision check.

**Three facts make this decision different from `OD-DEP-001`, and they should be established before
choosing.**

1. **The deciding operational fact is still unestablished.** `OD-DEP-001` asked whether any production tenant
   holds Employee rows, and observed that if none does, the free option is available and every other option
   is unnecessary complexity. The FP-007 record does not show that this fact was ever established — Option A
   was chosen, which is the answer for the case where production data exists. **If it does not, the same free
   option is available here**, and choosing A again would create a second permanent synthetic row for no
   reason.
2. **The synthetic row may be a chain.** FP-007 needed one `UNASSIGNED` Department per company. Under the
   three-entity reading of `OD-POS-002`, a synthetic Position may require a synthetic JobGrade, which may
   require a synthetic SalaryGrade — three permanent rows per company instead of one, each of which will look
   like ordinary data to every future reader.
3. **One link in that chain may have to invent money.** If `OD-POS-004` puts ranges on SalaryGrade and makes
   them mandatory, the migration must write a minimum and a maximum for a grade nobody designed. There is no
   honest number. This is a hard constraint on `OD-POS-004`, not a footnote: **a mandatory money range and a
   seeded-default backfill are not compatible** unless the range is nullable or the seeded grade is exempt —
   and an exemption means a discriminator column this codebase already declined to add once (`DEC-DEP-0009`
   amendment: no `IsSystem`, `Origin`, `OriginKind` or `IsBuiltIn`).

| Option | Business meaning | Migration mechanics | Satisfies `BR-HR-0006` immediately? | Effect on the E3 copy | Effect on API validation | Future cleanup |
|---|---|---|---|---|---|---|
| **A — Seeded default position per company, backfill all** (the `DEC-DEP-0009` precedent) | "Unassigned" is a real job these employees hold until HR classifies them | Create tables → add `Employees.PositionId` nullable → insert the synthetic chain per affected company → `UPDATE` all employees → write one initial history row each → `ALTER COLUMN … NOT NULL` → add FK and index. Needs the same **separate collision pass before any write** that `20260820140653_AddEmployeeDepartment` uses | **Yes**, formally | None. The synthetic rows are ordinary tenant-owned rows and copy by construction | `positionId` required on create from day one | Perpetual. Nothing forces the position to empty, and it is indistinguishable from real data by design |
| **B — Nullable for migrated rows, required for new creates, remediation later** | Existing employees are explicitly *unclassified*; new ones cannot be | Nullable column + FK + index; a **named** later migration alters to `NOT NULL` | **No** — every legacy row violates the rule until remediated | None | `positionId` required on create; every read must tolerate null | A second migration plus a remediation project that is easy to never schedule |
| **C — Nullable for everyone, enforcement milestone later** | The rule is advisory until a stated date | Nullable column; enforce nothing | **No** | None | Optional everywhere | Highest. This is how a binding rule becomes folklore — `OD-DEP-001` recommended against it in every case |
| **D — Block deployment until every existing employee is assigned** | The rule is real from the first moment | Nullable column → an assignment tool or script → a later migration to `NOT NULL` that **fails loudly** if any null remains. **If no production tenant holds Employee rows this collapses into one migration, with the column `NOT NULL` immediately** | **Yes**, genuinely | None | Required from day one | None |
| **E — Amend `BR-HR-0006`** | The rule binds employees created after Position exists and is silent about earlier ones | As B, but the nullable state is *correct* rather than transitional and nothing is owed later | Vacuously | None | Required on create; null is a legal persisted state forever | None — but the rule text in `Business-Rules.md` must change, which is a Master Product Specification edit, not a package decision |

**No recommendation is offered.** The choice is the owner's, it is not reversible once data exists, and the
precedent (Option A) was set for Department under an operational fact that was never established and may not
hold. Option E is listed because "amend the rule" is a legitimate answer that `OD-DEP-001` did not offer, and
one the owner may prefer to a permanent synthetic row — but it changes a Master Product Specification
document and must be taken as such.

**What FP-008 will not do:** choose silently, ship a nullable column with no recorded strategy, or add a
system-origin discriminator to make the synthetic row distinguishable. The last was already declined once
(`DEC-DEP-0009` amendment), and reversing it for Position would leave the two aggregates inconsistent.

> **RULING 2026-08-21 — the operational fact was established: no production tenant holds Employee rows.**
>
> `BR-HR-0006` is enforced from day one. `Employee.PositionId` ships **`NOT NULL`**. **No synthetic backfill
> row or chain is created** — no `UNASSIGNED` Position, JobGrade, or SalaryGrade exists.
>
> **This did not select one of the five options; it answered the question the options were a fallback for.**
> This decision said the deciding fact had never been established and should be before choosing. It was
> established, and it made the free path available. All three complications the decision raised are therefore
> **discharged rather than solved**: no chain, nothing to invent, no discriminator question.
>
> **Mandatory safeguard, ruled alongside it: `DEC-POS-0026`.** The migration counts `tenant.Employees` before
> any DDL and **fails loudly and transactionally if the count is not zero**, naming the database, the count,
> and this decision. A migration that is correct only because of an operational claim must verify the claim —
> tenants provisioned after the ruling, restored databases, and `ADR-021` customer-managed catalogs are each a
> way for it to be false in one database while true in another. Same fail-loud family as `DEC-DEP-0009`.
>
> See `DEC-POS-0009` and `DEC-POS-0026`.

---

### OD-POS-002 — Are Job Grade and Salary Grade one ladder or two?

**Question.** The requirement catalog lists three lines — `REQ-HR-0200` Position Management, `REQ-HR-0201`
Job Grades, `REQ-HR-0202` Salary Grade — with no body text for any of them. In HR practice these are
sometimes the same ladder under two names and sometimes two deliberately separate structures: a **job grade**
classifies the work (its level, scope, evaluation points), and a **salary grade** classifies the pay band.
Organizations that run job evaluation separately from pay benchmarking keep them apart; organizations that
run one ladder do not.

**This package will not collapse three requirement lines into fewer entities without the owner saying so.**
Three catalog entries exist; how many aggregates they describe is a business fact, not a modelling
preference.

| Option | Entity set | What it means | Consequences |
|---|---|---|---|
| **(i) Three entities** | `Position` → `JobGrade` → `SalaryGrade` | The job's level and the pay band are separately maintained and separately mapped | Four new tenant-owned tables including the assignment history; **12** HR permissions; a three-link synthetic chain for `OD-POS-001`; the E3 manifest goes from 7 entities to 11 |
| **(ii) Two entities — one ladder** | `Position` → `Grade` | One grade ladder carries both the job level and the pay band; `REQ-HR-0201` and `REQ-HR-0202` are two views of one structure | Three new tables including history; **8** permissions; a two-link synthetic chain. **Splitting later is a data migration**, because one ladder's rows must be divided in two and re-mapped |
| **(iii) Two entities — money deferred** | `Position` → `JobGrade`; `SalaryGrade` deferred to Payroll | Job classification ships now; the pay structure ships with the module that pays people | Three new tables including history; **8** permissions; **no money anywhere in FP-008**, which makes `OD-POS-004` moot and `ADR-027` unnecessary. `REQ-HR-0202` is recorded as transferred, on the terms FP-006 used for Department |
| **(iv) One entity** | `Position` only; no grades | Grades are an attribute of a position (a `Level` field), not entities | Two new tables including history; **4** permissions. **Not recommended**: `REQ-HR-0201` and `REQ-HR-0202` are named requirement lines, and answering two requirements with a string column is the collapse this decision exists to prevent. It is listed because the owner may know the requirement lines were aspirational |

**Engineering observation, offered as information rather than a recommendation.** Only `BR-HR-0006` forces
anything, and what it forces is **Position**. No business rule in the catalog mentions grades at all. That
makes option (iii) the smallest answer that leaves no requirement mis-stated: it delivers the rule-bearing
aggregate and transfers the money-bearing one on terms this repository has used twice before. But whether pay
bands belong to HR or to Payroll is a business-architecture question, and it is the owner's.

**This decision gates almost everything else.** The permission count, the table count, the E3 manifest, the
backfill chain length, `OD-POS-004`, and whether `ADR-027` is needed at all all follow from it.

> **RULING 2026-08-21 — option (i): three aggregates.** `Position`, `JobGrade` and `SalaryGrade`, one per
> requirement line. Job evaluation and pay banding are maintained separately, and the reference runs
> `Position → JobGrade → SalaryGrade` — **one-directional, and it must stay so**, or the cycle `DEC-POS-0002`
> prevents returns where nobody would look for it.
>
> Every count this package expressed as a table of options is now a single number:
>
> | | |
> |---|---|
> | New tenant-owned tables | **4** — `Positions`, `JobGrades`, `SalaryGrades`, `EmployeePositionAssignments` |
> | New HR permissions | **12** |
> | New HTTP routes | **20** |
> | E3 cutover manifest | **7 → 11** entities; restore drop list **6 → 10** tables |
>
> See `DEC-POS-0005`.

---

### OD-POS-003 — Is a Position owned by a Department, linked to one, or independent?

**Question.** Is "Senior Accountant" a job that exists once in the company and is held by people in various
departments, or is it "Senior Accountant, Finance" — a position that belongs to the Finance department?

**Why this is not a modelling preference.** Employee already carries `DepartmentId`, shipped in FP-007 and
`NOT NULL`. If Position also carries a required `DepartmentId`, then **an employee's department is recorded
twice** — once directly, once through their position — and the two can disagree. Two sources of truth for one
fact is the class of defect this codebase has consistently refused: `DEC-DEP-0005` rejected closure tables for
exactly this reason, and `DEC-DEP-0029` derives the cutover manifest rather than declaring it.

| Option | Model | What resolves the two-sources problem | Consequences |
|---|---|---|---|
| **(a) Independent** | `Position` has no department reference. An employee has a department and a position, separately | The problem does not arise | Simplest. Position codes are unique per company. A departmental reorganization does not touch positions. **The org chart cannot say which jobs exist in which department** except by going through employees, so an empty department has no visible job structure |
| **(b) Linked, optional** | `Position.DepartmentId` nullable, advisory | Nothing — the two may disagree, and the model says the direct value wins | Cheap, and dishonest under load: a nullable advisory copy of a fact is a field somebody will eventually read as authoritative |
| **(c) Owned, required, and Employee's department becomes derived** | `Position.DepartmentId` required; `Employee.DepartmentId` is **removed** and derived through the position | One source of truth, in the position | **Reverses FP-007.** `DEC-DEP-0009`, `DEC-DEP-0010`, `DEC-DEP-0015` and `ADR-026` decision 6 all rest on `Employee.DepartmentId` being a real, required, sanctioned-channel-only column. Dropping it means dropping a shipped `NOT NULL` column, its index, its change operation, and the meaning of its history table. A department change would become a position change |
| **(d) Owned, required, and the two must agree** | `Position.DepartmentId` required; `Employee.DepartmentId` stays; an invariant requires them equal | An enforced invariant | Every department change must also change the position, or be refused; every position change must also change the department. Two append-only histories must stay in step. **The invariant cannot be expressed as a database constraint** across two tables without a trigger, and `ADR-026` decision 4 rejected triggers because the tenant schema's trigger inventory is itself asserted by a guard |

**Engineering recommendation: (a), independent.** It is the only option that creates no second source of
truth, and the only one that neither reverses an FP-007 decision nor requires an invariant the database
cannot hold. Its real cost — the org chart cannot list a department's jobs directly — is a reporting feature
no requirement asks for, and it can be added later as a read model without changing ownership.

**The business question underneath**, which is not an engineering call: does this organization define jobs
centrally (a company-wide job catalog) or departmentally (each department owns its own job titles)? If the
answer is departmental, option (a) is wrong for the business however clean it is, and the owner should say
so — in which case (d) is the honest form and its cost must be accepted knowingly.

> **RULING 2026-08-21 — option (a): independent.** Jobs are defined centrally, as a company-wide catalog.
> `tenant.Positions` carries **no `DepartmentId`**, and no invariant relates an employee's position to their
> department.
>
> The two-sources-of-truth problem never arises: `Employee.DepartmentId` remains the single authority on an
> employee's department, exactly as FP-007 shipped it, and `BR-HR-0005` is untouched.
>
> **The cost is accepted knowingly:** the org chart cannot list a department's jobs directly — only through
> the employees holding both — so a department with no employees has no visible job structure. If that view is
> wanted later it is a **read model derived from employees**, never a `Position.DepartmentId` column.
>
> `DEC-POS-0007`'s uniqueness scope, which this decision gated, resolves to **per company**.

---

### OD-POS-004 — Does Salary Grade carry money, and if so, is it validation or information?

**Only reachable if `OD-POS-002` keeps SalaryGrade in FP-008.** Under option (iii) or (iv) this decision does
not arise and `ADR-027` should be withdrawn.

**A fact that removes one of the three usual answers.** FP-008 introduces **no employee compensation field**
— an individual's pay is Payroll (`DEC-POS-0023`). So a salary range in FP-008 has **nothing to validate**.
The "ranges are validation" reading is not a stricter choice than "ranges are information"; it is an empty
one, in exactly the way `ADR-026` decision 10 describes: *a rule with no field to constrain is recorded as
unenforceable, not quietly satisfied.* Choosing "validation" would mark a constraint as enforced when no
write in the product can violate it.

| Option | What SalaryGrade holds | Consequences |
|---|---|---|
| **(i) No money** | A band identifier and a name only; amounts arrive with Payroll | No currency decision, no precision decision, **no `ADR-027`**. `OD-POS-001`'s synthetic chain needs no invented numbers. `REQ-HR-0202` is realized as far as the band structure and the remainder is transferred with an explicit statement of what is not delivered |
| **(ii) Money as information** | `MinimumAmount`, `MidpointAmount`, `MaximumAmount` — stored, never enforced against anything | Requires the currency carrier (`DEC-POS-0015`) and the precision (`DEC-POS-0016`), both **unprecedented in this product**, and therefore `ADR-027`. If the amounts are mandatory, `OD-POS-001` Option A must invent them for the seeded grade |
| **(iii) Money as validation** | As (ii), plus a rule that an employee's pay must fall in band | **Not available in FP-008.** There is no employee pay field to check. Selecting this means selecting (ii) now and transferring the enforcement to Payroll — a legitimate answer, but it must be recorded as transferred rather than as realized |

**Two consequences the owner should see before choosing (ii) or (iii).**

- **HR cannot reuse the product's currency type.** `BaseCurrencyCode` lives in `SSAS.Platform.Domain`.
  `SSAS.HR.Domain` references only `SSAS.BuildingBlocks.Domain` and `SSAS.BuildingBlocks.SharedKernel` —
  verified in the project files and enforced by the compiler, exactly as `ADR-012` enforced it against
  `DepartmentApiErrorMapper` in FP-007 Phase 4. The options are to duplicate a 180-entry ISO-4217 list inside
  HR, to promote the value object into BuildingBlocks (a Platform-touching change), or to carry no currency
  column at all and read amounts as the owning Company's base currency. `DEC-POS-0015` proposes the third and
  names the condition for revisiting it.
- **Pay bands are more sensitive than job titles.** If SalaryGrade carries money, giving it the same `View`
  permission as the org chart means everyone who may see the structure may also see the pay structure.
  `DEC-POS-0018` therefore proposes a **separate** `HR.SalaryGrades.View`, which is a departure from the
  "deliberately minimal" permission discipline and is flagged as one.

> **RULING 2026-08-21 — option (ii): money as informational bands.** `MinimumAmount`, `MidpointAmount` and
> `MaximumAmount` are stored and internally ordered. They constrain nothing outside their own row, because
> FP-008 stores no value for them to constrain.
>
> **The "validation" reading stays recorded as *unavailable*, not as rejected** — the distinction is the
> `ADR-026` decision 10 discipline. Salary-range enforcement transfers to Payroll as a named obligation in
> [`traceability-matrix.md`](traceability-matrix.md); it is not discarded, and FP-008 does not claim it.
>
> Both consequences this decision named are accepted as drafted: **no currency column** (`DEC-POS-0015` —
> amounts are in the owning Company's immutable base currency, projected on read and rejected on write), and
> **a separate `HR.SalaryGrades.View`** (`DEC-POS-0018`, ratified including the separation).
>
> **`ADR-027` activates.** Its conditional-withdrawal clause is moot, and `decimal(19,4)` is now the product's
> money representation rather than a proposal — General Ledger inherits it.

---

### OD-POS-005 — What does "active" mean in `BR-HR-0006`, and what happens when a position with incumbents is deactivated?

**Question.** `BR-HR-0006` reads *"Every employee must have one active position."* The word **active** can
attach to either noun, and the two readings produce different systems.

| Reading | Meaning | Consequence when a position is deactivated |
|---|---|---|
| **(i) The assignment is current** | The employee has one *current* position assignment. "Active" distinguishes the present assignment from historical ones | Deactivating a position with incumbents is **harmless**. The employees keep a current assignment; the position simply stops accepting new ones. Mirrors `BRULE-DEP-0015` exactly |
| **(ii) The position's lifecycle status is `Active`** | The position an employee holds must itself be in the `Active` state | Deactivating a position with incumbents **breaks `BR-HR-0006` for every one of them at that instant**. Deactivation must therefore be refused while incumbents exist — a real divergence from the Department precedent, where deactivation is always allowed and employees stay |
| **(iii) Both** | The assignment is current **and** the position is `Active` | As (ii), with the same refusal |

**Why this cannot be left to implementation.** FP-007's lifecycle model refused to let one rule break
another: it declined to evict employees from a deactivated department because doing so would violate
`BR-HR-0005` for each of them, and named that "using one rule to break another". Reading (ii) creates the
identical situation for positions, and the only way to avoid it is to **refuse the deactivation**. That is a
materially different operator experience from Department — an HR administrator retiring a job must first move
every holder off it, and no bulk reassignment operation is in scope.

**Engineering recommendation: reading (i).** It is the only reading under which `BR-HR-0006` and the
Department lifecycle precedent are simultaneously satisfiable without inventing a bulk-move operation, and it
is the reading that makes the rule about the *employee's* record rather than about someone else's edit to a
shared row. **But this is a business reading of a business rule**, the sentence genuinely supports (ii), and
if the owner means (ii) then `BRULE-POS-0014` must refuse deactivation and the acceptance criteria change.

> **RULING 2026-08-21 — reading (i): the assignment.** "One active position" means the employee has one
> *current* assignment. It does not require the position itself to be `Active`.
>
> - **Deactivating a Position with incumbents is ALLOWED.** They keep it, `BR-HR-0006` stays satisfied for
>   each of them, and no bulk-reassignment operation is needed. This is `BRULE-DEP-0015`'s shape exactly. A
>   retired job may still have holders — an oddity accepted knowingly, in exchange for never using one rule to
>   break another.
> - **An `Inactive` Position refuses a NEW assignment**, on employee creation and on position change alike.
>   The owner named the shape: the parallel of `BR-HR-0009` as realized by `BRULE-DEP-0014`.
>
> `BRULE-POS-0014`'s conditional clause is discharged, and **`position.has_incumbents` is not an error this
> package defines** — no operation raises it.

---

### OD-POS-006 — Does a reporting line enter FP-008 at all?

**Question.** In many HR models the position structure *is* the reporting structure: a position reports to a
position, and an employee's manager is derived from it. If FP-008 introduces `Position.ReportsToPositionId`,
it becomes the package that introduces a reporting line — and inherits the open remainder of `BR-HR-0007`.

**What is currently true, verified.** No repository authority defines an employee→manager reporting line.
FP-006 deferred `ManagerId` entirely (`DEC-EMP-0031`). FP-007 adopted reading (iii) of `OD-DEP-003`
(`DEC-DEP-0014`): the departmental half is enforced, and the personal reporting line transfers to "the
package introducing an employee reporting line — **which no current requirement asks for**". `REQ-HR-0200` is
a title with no body, so it neither asks for a position hierarchy nor rules one out.

| Option | Scope | Consequences |
|---|---|---|
| **Defer (recommended)** | Position is flat. No `ReportsToPositionId` | `BR-HR-0007`'s remainder stays open and honestly recorded, per `ADR-026` decision 10. FP-008 stays a single non-hierarchical aggregate |
| **Include** | `Position.ReportsToPositionId`, self-referencing | FP-008 becomes the **second hierarchical aggregate**, inheriting the whole `DEC-DEP-0006` apparatus: repository-produced ancestry evidence, a `CK_Positions_ReportsToIsNotSelf` constraint, per-`(TenantId, CompanyId)` application-lock serialization, and its own concurrency proofs. It also acquires the derived employee→manager line — which finally gives `BR-HR-0007` a field, and with it the obligation to enforce it |

**Engineering recommendation: defer.** Three reasons. No requirement asks for it. Including it roughly
doubles the package, and the acyclicity machinery is the most expensive thing FP-007 built. And inventing a
reporting line so that `BR-HR-0007` has something to constrain is precisely the "quietly satisfied" failure
`ADR-026` decision 10 exists to name.

**The honest cost of deferring**, stated so it is not discovered later: adding a nullable
`ReportsToPositionId` to a populated table is cheap, but the position-assignment history written between
FP-008 and that later package will carry no reporting context — so *who reported to whom, when* is
unrecoverable for that period, in the way `DEC-DEP-0016` described before it was reversed. This is a smaller
loss than the department case, because the reporting structure's *current* state is recoverable the moment
the hierarchy exists and only its history is lost. It is stated rather than assumed.

> **RULING 2026-08-21 — defer.** `ReportsToPositionId` is not introduced. **Position is flat**: no
> self-reference, no acyclicity invariant, no per-company serialization lock, no ancestry evidence type.
> FP-008 introduces no hierarchical aggregate, and none of the machinery FP-007 built for one.
>
> `BR-HR-0007`'s remainder **transfers onward unchanged** from `DEC-DEP-0014` reading (iii), to a package no
> current requirement asks for and which may therefore never arrive. It is recorded as **OPEN**, not covered.
>
> **The cost is accepted as stated:** position-assignment history written between FP-008 and any future
> hierarchy package carries no reporting context, so *who reported to whom, when* is unrecoverable for that
> period — the smaller loss this decision described, since the current structure will be recoverable and only
> its history will not.

---

## What this package does not claim

It is approved. Three things it still does not claim are worth stating, because an approved package is read as
settled in every respect unless it says otherwise.

**It does not claim `BR-HR-0006` is satisfied yet.** The design satisfies it; nothing is built. The rule
becomes true when the migration runs against a real database and `Employee.PositionId` is `NOT NULL` there —
not before, and not because this document was approved.

**It does not claim `BR-HR-0007` is discharged.** `OD-POS-006` deferred the position hierarchy, so its
remainder transfers onward unchanged, to a package no current requirement asks for and which may never arrive.
It is recorded in [`traceability-matrix.md`](traceability-matrix.md) as **OPEN**, not as covered — the
`ADR-026` decision 10 discipline: *where a rule cannot be enforced, the honest record is that it is open.*

**It does not claim salary-range enforcement.** `OD-POS-004` chose informational bands, and FP-008 stores no
value for them to constrain. The obligation transfers to Payroll and is recorded as transferred, not as
realized.

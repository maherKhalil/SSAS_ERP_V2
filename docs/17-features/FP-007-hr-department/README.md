---
document_id: FP-007
title: HR Department
status: Draft — Owner Decision Required
version: 0.1
module: HR
milestone: Milestone 1
depends_on:
  - ADR-013
  - ADR-014
  - ADR-023
  - ADR-024
  - ADR-025
  - ADR-026
  - FP-005
  - FP-006
---

# Feature Package 007 — HR Department

> **Draft — Owner Decision Required.** This package is analysis and design only. Five decisions
> ([`OD-DEP-001` … `OD-DEP-005`](#owner-decisions-required-before-approval)) turn on business semantics that no
> repository authority settles, and they are presented for approval rather than guessed. **This package is not
> approved for implementation and no code may be written against it until those decisions are recorded.**

## Purpose

FP-007 establishes the **Department** aggregate: the organizational structure that FP-006 deliberately left
out, and the home of four business rules FP-006 retained as binding while deferring their enforcement.

A Department is an organizational unit within a Company, arranged in a hierarchy, optionally headed by an
Employee. FP-007 delivers the smallest Department core that satisfies `REQ-HR-0100`, `REQ-HR-0101` and
`REQ-HR-0102` and discharges the obligations FP-006 transferred to "the package that introduces them". It is
not a general org-structure module and it does not introduce Position.

## Position in the platform hierarchy

```
Platform
  └── Tenant                                   (FP-003, implemented)
        ├── Company                            (FP-005, implemented)
        │     └── Department                   (FP-007, this package — hierarchical)
        ├── Branch                             (Branch foundation B0/B1, implemented)
        └── Employee                           (FP-006, implemented)
              ├── EmployeeBranchAssignment     (FP-006, append-only branch history)
              └── DepartmentId                 (FP-007, this package)
```

Department sits **beneath Company only**. It is not a branch dimension. See `DEC-DEP-0001` and `ADR-026`.

## Architecture significance

FP-007 is the slice in which the organizational structure deferred by FP-006 becomes real:

- **First hierarchical aggregate in the product.** Nothing in the platform is self-referencing yet. The
  representation, the acyclicity invariant, and where that invariant is enforced are architecture decisions,
  not feature details, because Position and any future org structure will follow the same pattern.
- **First entity that is company-owned but deliberately NOT branch-owned.** Employee proved all three
  dimensions together; Department is the first record that takes one and refuses another. That refusal has to
  be explicit and guarded, or the next reader will assume the omission was an oversight.
- **First retroactive business rule.** `BR-HR-0005` binds employees "including for employees created under
  V1" (`BRULE-EMP-0026`). Every other rule in the product has applied from the moment its aggregate existed.
  This one applies to rows that already exist without the column it constrains.
- **First change to a shipped aggregate.** Employee gains `DepartmentId`. FP-006's contracts, read models and
  cutover manifest all move.

The failure mode of getting the third one wrong is the dangerous one: a nullable `DepartmentId` that nobody
ever remediates looks exactly like a working system, and `BR-HR-0005` quietly becomes advisory.

## Authoritative inputs

| Authority | Contribution |
|---|---|
| `HR.md` (`REQ-HR-0100`, `REQ-HR-0101`, `REQ-HR-0102`) | The Department requirement set |
| `Business-Rules.md` (`BR-HR-0005`, `BR-HR-0007`, `BR-HR-0008`, `BR-HR-0009`) | Membership, self-management, acyclicity, inactive departments |
| `Business-Rules.md` (`BR-PLT-0002`, `BR-PLT-0003`, `BR-PLT-0004`, `BR-PLT-0013`, `BR-PLT-0016`) | Company isolation, soft delete, audit trail, branch transaction ownership, reporting scope |
| `ADR-013` | `Guid` identifier strategy |
| `ADR-014` revision 1.1 | Company ownership; `tenant.Companies` placement |
| `ADR-023` | Branch ownership, decision 22 branch-scoped reads |
| `ADR-024` | Employee branch transfer — **constrains `DEC-DEP-0001`** |
| `ADR-025` | Company execution context, authorization, decision 8 and decision 10 |
| `ADR-026` (this package) | Department ownership, hierarchy representation, acyclicity invariant, `BR-HR-0005` enforcement strategy |
| `Architecture-Principles.md` Principle 11 | Explicit ownership classification |
| `Development-Standards.md` | Rowversion transport, transport conventions |
| FP-006 | The Employee aggregate this package modifies, and the obligations it transferred here |
| FP-005 | Package structure and documentation conventions **only** |

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

FP-007 defers the following. Each names where the obligation goes; none is discarded.

| Excluded from FP-007 | Source | Deferred obligation |
|---|---|---|
| **Position** | `REQ-HR-0200`, `REQ-HR-0201`, `REQ-HR-0202`; `BR-HR-0006` | `BR-HR-0006` ("every employee must have one active position") remains binding and its enforcement transfers to the package introducing Position, on exactly the terms FP-006 used for Department. **No `PositionId`, table, column, or foreign key is introduced here** (`DEC-DEP-0020`) |
| **Employee reporting line (`ManagerId`)** | `BR-HR-0007` | Department has a manager; an *employee* does not have a manager. No authority in the repository defines an employee→manager reporting line. `BR-HR-0007` is therefore only partially enforceable here — see `OD-DEP-003`. No reporting-line model is invented (`DEC-DEP-0014`) |
| **Employee department history** | `REQ-HR-0006` | FP-006 deferred profile, department and position history. FP-007 records the current department and its audit stamps only. What is lost is stated in `DEC-DEP-0016`; see `OD-DEP-004` before accepting it |
| **Department-scoped reads of other aggregates** | — | Nothing gains a department filter. Employee search may filter *by* department; no read is *scoped by* department (`DEC-DEP-0019`) |
| **Cost centres, GL mapping, budgets** | Roadmap V1 General Ledger | Department is an HR organizational unit here and carries no financial semantics (`DEC-DEP-0021`) |
| **Department codes generated automatically** | `BR-PLT-0006` | `Code` is user-entered, exactly as `EmployeeNumber` is in FP-006 (`DEC-DEP-0007`) |

## Owner decisions required before approval

Five decisions turn on business semantics that no ADR, Feature Package, or Master Product Specification
document settles. Each is stated with options, an engineering recommendation, and the consequence of each
choice. **`OD-DEP-001` is the blocking one.**

---

### OD-DEP-001 — What happens to Employees that already exist without a Department?

**Question.** `BR-HR-0005` binds every employee to exactly one department, and `BRULE-EMP-0026` extends it to
employees created under V1. Those rows exist today with no department and no column to hold one. What is the
enforcement strategy?

**A fact that changes the answer, which the repository cannot tell me:** whether any production tenant
database currently holds Employee rows. FP-006 merged recently and the product is pre-release. **If no
production tenant has employees, Option D is free and every other option is unnecessary complexity.** That is
an operational fact the owner holds, and it should be established before choosing.

| Option | Business meaning | Migration mechanics | Satisfies `BR-HR-0005` immediately? | Rollout risk | Future cleanup |
|---|---|---|---|---|---|
| **A — System default department per company, backfill all** | "Unassigned" is a real organizational unit these employees sit in until HR classifies them | Add `DepartmentId` nullable → insert one `Unassigned` department per company → `UPDATE` all employees → alter column to `NOT NULL` | **Yes**, formally | Medium: creates a department HR never asked for, visible in every list and hierarchy | Perpetual: nothing forces the department to ever empty, and it will look like normal data |
| **B — Nullable for migrated rows, required for new creates, later remediation** | Existing employees are explicitly *unclassified*; new ones cannot be | Add nullable column; API requires it on create; a named remediation milestone makes it `NOT NULL` | **No** — a null row violates the rule until remediated | Low at deploy | Requires a second migration and a deliberate remediation project that is easy to never schedule |
| **C — Nullable for everyone, enforcement milestone later** | The rule is advisory until a stated date | Add nullable column, enforce nothing | **No** | Lowest | Highest: the rule is unenforced in code, which is how a binding rule becomes folklore |
| **D — Block deployment until every existing employee is assigned** | The rule is real from the first moment | Add nullable column, ship an assignment tool or script, refuse the `NOT NULL` migration until zero nulls remain | **Yes**, genuinely | Highest if production data exists; **zero if it does not** | None |

**Engineering recommendation: establish the production fact first.**
- If **no production tenant holds Employee rows** → **Option D**. It is free, it is honest, and it leaves no
  residue. This is the likely case.
- If **production employees exist** → **Option A**, because it is the only remaining option that makes
  `BR-HR-0005` true on the day it ships, and an explicitly-named `Unassigned` department is a visible,
  queryable statement that work remains — which a `NULL` is not. Its cost, a department HR did not ask for,
  is real and should be accepted knowingly.

**Option C is not recommended in any case.** A binding business rule enforced nowhere is the failure mode
this whole package exists to avoid.

---

### OD-DEP-002 — Can one Department contain employees from more than one Branch?

**Question.** Is a Department a company-wide organizational unit that spans branches ("Sales" exists once and
has people in Riyadh and Jeddah), or is it per-branch ("Sales — Riyadh" and "Sales — Jeddah" are different
departments)?

**This is a business question, but `ADR-024` has largely already answered it.** Employee branch transfer is a
sanctioned, branch-only operation with its own history and its own dual-branch authorization. If Department
were branch-owned, *every* transfer would silently break `BR-HR-0005` — the employee would arrive in a branch
where their department does not exist — and `ADR-024` makes no provision for that. Branch-owned departments
would require transfer to become a combined branch-and-department operation, which would contradict an
accepted ADR.

| Option | Consequence |
|---|---|
| **Spanning (recommended)** | Department is Tenant + Company owned, not branch-owned. Transfer stays a pure branch operation. "Sales" exists once per company |
| **Per-branch** | Department gains `BranchId`. Every branch transfer becomes a department change too, requiring `ADR-024` to be amended. Departments duplicate per branch, and the hierarchy fragments |

**Engineering recommendation: spanning.** Confirmation is sought rather than a decision, because the
architecture already constrains it and the owner should know that is the reading being locked in.

---

### OD-DEP-003 — What does `BR-HR-0007` actually constrain?

**Question.** `BR-HR-0007` says "an employee cannot directly manage themselves." **There is no employee→manager
reporting line anywhere in the repository's authorities.** FP-006 deferred `ManagerId` entirely
(`DEC-EMP-0031`) and no requirement defines one. Department has a *manager*; an employee does not.

So the rule has no field to constrain unless one of these readings is adopted:

| Reading | What it means | Enforceable in FP-007? |
|---|---|---|
| **(i) Departmental** | An employee may not be the manager of the department they themselves belong to | **Yes**, fully |
| **(ii) Personal reporting line** | A future `Employee.ManagerId` may not point at the same employee | No — the field does not exist and no requirement asks for it |
| **(iii) Both** | (i) now, (ii) when a reporting line is introduced | (i) now, (ii) transferred |

**Engineering recommendation: (iii).** Enforce (i) in FP-007 as `BRULE-DEP-0012`, and transfer (ii) explicitly
to whichever package introduces an employee reporting line — recording that the requirement catalog does not
currently contain one, so it may never arrive.

**A note worth the owner's attention:** reading (i) has a real operational cost. It means a department head
cannot be a member of the department they head, which many organizations would find backwards. If the owner's
intent is (ii) only, say so — then `BR-HR-0007` is entirely deferred and FP-007 enforces nothing for it, which
is a legitimate answer but must be recorded rather than assumed.

---

### OD-DEP-004 — Is department-change history needed now, or deferred with the rest of Employee history?

**Question.** FP-006 deferred "profile, department, and position history" and realized only branch-assignment
history. FP-007 introduces the department relationship. Does it also introduce
`EmployeeDepartmentAssignment`?

**Why this cannot simply be deferred quietly.** Branch history is append-only from the first branch
assignment. If department history is added later, everything between FP-007 shipping and that later package is
unrecoverable — there is no record to reconstruct. Deferral here is not free the way most deferrals are.

| Option | Cost now | What is lost |
|---|---|---|
| **Defer (matches FP-006's stated deferral)** | None | Who moved between departments, when, and why — permanently, for the deferral period. Only the current value and `ModifiedBy`/`ModifiedUtc` survive |
| **Introduce `EmployeeDepartmentAssignment` now** | One more append-only table, mirroring `EmployeeBranchAssignment`, which already exists as a proven pattern | Nothing |

**Engineering recommendation: defer, as FP-006 states** — but with the owner explicitly acknowledging the
irrecoverable gap. FP-006's deferral is the standing authority and this package should not overrule it
silently. If HR expects to answer "when did this person move to Finance?", the answer must be **introduce it
now**, and that is cheap because `EmployeeBranchAssignment` is a working template.

---

### OD-DEP-005 — Should a branch-scoped user see a company-wide Department?

**Question.** Department is company-owned and not branch-owned (subject to `OD-DEP-002`). A user authorized
for Branch A only reads employees in Branch A. Those employees belong to departments that also contain Branch
B employees. Can that user see the department?

| Option | Consequence |
|---|---|
| **Company-scoped visibility (recommended)** | The user sees every department in their authorized companies. Branch scope filters *employee membership*, not department existence. Department lists show units that may currently contain no employee the user can see |
| **Branch-derived visibility** | The user sees only departments containing at least one employee they may read. Departments appear and disappear as employees move; an empty department is invisible to everyone; and the Employee read DTO can reference a department the caller cannot fetch |

**Engineering recommendation: company-scoped visibility.** Branch-derived visibility makes a department's
existence a function of who is asking, which breaks the Employee read DTO (it names a department the caller
cannot then retrieve) and makes the hierarchy incoherent — a parent could be invisible while its child is not.

**The business question underneath:** whether a department's *name and structure* is sensitive across
branches. If it is, the answer changes, and that is not an engineering call.

---

## What this package does not claim

It does not claim to be approved. It does not claim `BR-HR-0005` is satisfied — that depends on
`OD-DEP-001`. It does not claim `BR-HR-0007` is fully discharged — that depends on `OD-DEP-003`, and under
every reading part of it transfers onward. Those are stated in
[`traceability-matrix.md`](traceability-matrix.md) as open, not as covered.

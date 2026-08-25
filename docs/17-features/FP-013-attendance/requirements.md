# FP-013 — Proposed requirements

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

**Every identifier below is a PROPOSAL in a namespace that does not yet exist.**
`Requirement-Numbering.md` has no `ATT` prefix (`OD-ATT-0002`), so these are provisional and renumberable —
including their prefix.

**Authority column reads honestly.** Where a requirement traces to something written down, it says where.
Where it does not, it says **UNAUTHORED** rather than citing the roadmap's one-word "Attendance" line as if
that were a specification. The GL/PAY packages set this precedent and it is followed exactly.

**Scope column** is `A` (attendance), `B` (leave), `*` (both) — see `OD-ATT-0001`. **If the owner rules A,
every `B` row is struck; if B, every `A` row is struck.** The count at the bottom is given per scope for
that reason.

---

## The calendar — the shared foundation of every scope

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-ATT-0001` | A company maintains a **working calendar** defining which dates are working days, with the weekend pattern held as **data** (Fri/Sat, Sat/Sun and Thu/Fri all occur in the product's target regions) rather than as a constant | `*` | **UNAUTHORED** — derived from `OD-PAY-0007`'s stated blocker | `OD-ATT-0004`, `OD-ATT-0011` |
| `REQ-ATT-0002` | A working calendar carries a dated, named **holiday list** that may be maintained by an administrator | `*` | **UNAUTHORED** | `OD-ATT-0004` |
| `REQ-ATT-0003` | Given any date range, the calendar answers **how many working days it contains** — the single query every other requirement here depends on | `*` | **UNAUTHORED** | `OD-ATT-0004` |

## Attendance capture

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-ATT-0004` | An authorized user records, **for one employee on one date**, the quantity of time worked | `A` | **UNAUTHORED** | `OD-ATT-0003`, `OD-ATT-0011` |
| `REQ-ATT-0005` | Attendance is recorded only for employees the recorder is authorized to see, resolved **live** at query time | `A` | `ADR-025`; `DEC-PAY-0017` precedent | `DEC-ATT-0008` |
| `REQ-ATT-0006` | Attendance may not be recorded for a date **after** an employee's termination date, nor **before** their employment date | `A` | `BR-HR-0004` as read by `OD-PAY-0010` | `DEC-ATT-0013` |
| `REQ-ATT-0007` | An authorized user records **overtime** as a quantity against an employee and date, carrying a tier label | `A` | `DEC-PAY-0002` (the boundary this lifts) | `OD-ATT-0008` |
| `REQ-ATT-0008` | An authorized user records an **absence**, distinguishing paid from unpaid — unpaid absence being the quantity Payroll must deduct | `A` | `DEC-PAY-0002` | `OD-ATT-0008` |
| `REQ-ATT-0009` | Attendance quantities are recorded **without monetary value**; no rate, amount or currency appears | `*` | `ADR-027 d1`/`d2` boundary | `DEC-ATT-0004` |

## Leave

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-ATT-0010` | An administrator maintains a **leave type catalog** per company; each type names a closed **behaviour** the code understands | `B` | **UNAUTHORED** — shaped on the `PayElement` precedent | `OD-ATT-0005` |
| `REQ-ATT-0011` | An employee has a **balance** per leave type, expressed in the unit the type declares | `B` | **UNAUTHORED** | `OD-ATT-0006` |
| `REQ-ATT-0012` | An authorized user **submits a leave request** naming a type and a date range | `B` | `Glossary.md` — "Leave Request" as a Workflow *example* only; **not a definition** | `OD-ATT-0007` |
| `REQ-ATT-0013` | A request consumes **working days**, counted by `REQ-ATT-0003` — a weekend or holiday inside the range is not consumed | `B` | **UNAUTHORED** | `OD-ATT-0004` |
| `REQ-ATT-0014` | A request is **approved or rejected** by an authorized approver who is **not the requester** | `B` | `OD-PAY-0009`'s separation-of-duty reading | `OD-ATT-0007` |
| `REQ-ATT-0015` | An approved request **decrements** the balance; a rejected or cancelled one does not | `B` | **UNAUTHORED** | `OD-ATT-0006` |
| `REQ-ATT-0016` | A request may be **cancelled**, and the rules differ before and after its dates have passed | `B` | **UNAUTHORED** | `OD-ATT-0012` |
| `REQ-ATT-0017` | Leave requests obey the same employment-window bar as `REQ-ATT-0006` | `B` | `BR-HR-0004` per `OD-PAY-0010` | `DEC-ATT-0013` |

## The period, and the boundary with Payroll

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-ATT-0018` | Attendance is organised into **periods** that a permitted user closes, after which the period's records do not change | `*` | `ADR-014` closed-period precedent | `OD-ATT-0010`, `OD-ATT-0012` |
| `REQ-ATT-0019` | A correction discovered after close is recorded as a **dated adjustment**, never as a mutation of a closed record | `*` | `DEC-ATT-0009` (mechanical) | `OD-ATT-0012` |
| `REQ-ATT-0020` | Attendance publishes a **contract** returning, for one employee and one period, the quantities Payroll needs — **totals, never punch-level data** | `*` | `ADR-012`; the triple precedent | `DEC-ATT-0002`, `OD-ATT-0009` |
| `REQ-ATT-0021` | The contract lets a caller **inspect** a period's open/closed state and returns a **modelled outcome**, not an exception, when the period is unavailable | `*` | `InspectPostingWindowAsync` precedent | `OD-ATT-0010` |
| `REQ-ATT-0022` | **PAYROLL-SIDE FOLLOW-UP (specified here, not built here).** Once the contract exists, Payroll gains the `PayElementBehaviour` members `DEC-PAY-0002` refused, and `PayrollCalculator` consumes them. **`PayrollCalculatorTests.No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt` is REPLACED, not deleted.** | `*` | `DEC-PAY-0002` | `DEC-ATT-0001`, `DEC-ATT-0012` |

## Reading

| ID | Requirement | Scope | Authority | Decisions |
|---|---|---|---|---|
| `REQ-ATT-0023` | An employee may read **their own** attendance and leave records. **BLOCKED — NOT IMPLEMENTABLE TODAY:** it needs a mapping from the authenticated identity to an employee record, and **no such mapping exists** (verified; `Employee` carries no user identifier). `OD-PAY-0016` deferred payroll self-service for exactly this reason. **This is `DEC-PAY-0002`'s shape — a missing input, not a scoping preference** | `*` | **UNAUTHORED** | `OD-ATT-0013` |
| `REQ-ATT-0024` | An authorized user reads records for the employees within their authority, bounded by company and — **if `OD-ATT-0011` so rules** — branch | `*` | `ADR-025` | `OD-ATT-0011`, `OD-ATT-0013` |
| `REQ-ATT-0025` | Leave **type** is separable from leave **occurrence** in the permission model, because a type may disclose health information | `B` | Permission grammar's sensitivity convention | `OD-ATT-0013` |

---

## Counts — RULED: scope is BOTH, so all 25 are in force

The table below is kept as the record of what each scope would have cost.

| `OD-ATT-0001` ruling | Requirements in force |
|---|---|
| **A** — attendance only | 3 calendar + 6 capture + 5 period/contract + 2 reading = **16** |
| **B** — leave only | 3 calendar + 1 (`REQ-ATT-0009`) + 8 leave + 5 period/contract + 3 reading = **20** |
| **C** — both | **25** |

## What is deliberately absent, and why

**No accrual requirement.** `OD-ATT-0006` may add several; until it is ruled, writing them would state a
design the owner has not chosen.

**No device or biometric capture requirement.** `OD-ATT-0016` proposes excluding it explicitly. It is named
here as an absence because "attendance system" means "clock terminals" to many readers, and an unstated
exclusion surfaces at acceptance.

**No overtime *rate* requirement.** Multipliers are money and live in Payroll (`DEC-ATT-0004`). Attendance
supplies the tiered quantity; Payroll supplies the rate.

**No direct manager-approval requirement.** Checked rather than assumed: **`Employee` has no `ManagerId`.**
What exists is a **`DepartmentManager`** — one seat per department, keyed on `DepartmentId` — over
**nesting departments** (`ParentDepartmentId`). So a reporting line is derivable, but it is indirect and
department-mediated, and it has reachable holes (`ManagerNotAssigned` and `ManagerTerminated` are both
modelled errors). `OD-ATT-0007` carries the three sub-questions those holes raise. Writing an approval
requirement before they are ruled would state a chain the code cannot currently complete.

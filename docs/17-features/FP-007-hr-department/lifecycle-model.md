---
document_id: FP-007-LIFE
title: HR Department — Lifecycle Model
status: Approved for Implementation
version: 1.0
---

# FP-007 — Lifecycle Model

## D6 — States

**Decision `DEC-DEP-0011`: two states, `Active` and `Inactive`.** Classification: **ENGINEERING-SETTLED.**

```
        create
          │
          ▼
     ┌─────────┐   Deactivate    ┌───────────┐
     │ Active  │ ──────────────▶ │ Inactive  │
     │         │ ◀────────────── │           │
     └─────────┘   Reactivate    └───────────┘
                                       │
                                       ×  no physical delete, ever
```

| | |
|---|---|
| **Initial state** | `Active`. A department created inactive would be a department nobody can use and nobody asked for |
| **Deactivate** | `HR.Departments.Deactivate`. Records `StatusChangedUtc` and `StatusChangedBy` |
| **Reactivate** | `HR.Departments.Deactivate` — the same permission covers both directions (`DEC-DEP-0017`) |
| **Physical delete** | **Never** (`BRULE-DEP-0016`, `BR-PLT-0003`) |
| **Terminal state** | `Inactive`, but reversibly so |

`Inactive` is *reversible* here, unlike Employee's `Terminated`, which is genuinely terminal. Deactivating a
department is an organizational decision that organizations reverse; terminating a person is not. The two
should not be modelled alike merely because they are both "the off state".

## Deactivation rules

| Situation | Behaviour | Rule |
|---|---|---|
| Department has assigned Employees | **Allowed.** Employees stay. They remain members and `BR-HR-0005` remains satisfied for them | `BRULE-DEP-0015` |
| Department has `Active` children | **Refused** until the children are deactivated or moved. Deactivation does not cascade | `BRULE-DEP-0022` |
| Department has `Inactive` children | Allowed | `BRULE-DEP-0022` |
| Department is a manager's own department | Irrelevant — no interaction | — |
| New Employee created into it | **Refused** | `BRULE-DEP-0014`, `BR-HR-0009` |
| Employee changed into it | **Refused** | `BRULE-DEP-0014`, `BR-HR-0009` |
| Employee changed *out of* it | **Allowed** — this is how a department is emptied | `DEC-DEP-0012` |
| Department moved beneath it | **Refused** — an active child under an inactive parent is an incoherent tree | `DEC-DEP-0006` step 3 |
| Reads | **Still visible.** An inactive department is readable and appears in lists, marked inactive | `BRULE-DEP-0016` |

**Why deactivation does not cascade.** A cascade would deactivate an arbitrary amount of structure from one
click, and the operator would have no record of what was already inactive beforehand — so reactivation could
not restore the prior state. Refusing until children are handled is more work for the operator and is the only
version that is reversible.

**Why existing employees are not evicted.** `BR-HR-0009` says inactive departments cannot receive *new*
employees. It does not say existing ones are expelled, and expelling them would violate `BR-HR-0005` for every
one of them the instant a department was deactivated — using one rule to break another.

## Manager and lifecycle interaction

| Event | Effect on `DepartmentManagers` |
|---|---|
| Manager Employee is **terminated** | **Nothing automatic.** The assignment stands and the department is reported as having a terminated manager (`BRULE-DEP-0013`, `DEC-DEP-0013`) |
| Manager Employee **transfers branch** | Nothing. Manager is a company-level relationship; branch is orthogonal (`BRULE-DEP-0019`) |
| Manager Employee **changes department** | Nothing automatic — but under `OD-DEP-003` reading (i), moving *into* the department they manage is refused by `BRULE-DEP-0012` |
| Department is **deactivated** | Nothing. The record of who headed it is part of what makes the inactive department readable |

**Why termination does not silently clear the manager.** An automatic clear destroys information — the
department would show "no manager" and nothing would say there had been one — and it makes a write to HR
structure a side effect of an unrelated HR operation. Surfacing it instead means somebody decides who takes
over, which is what actually needs to happen. The cost is that a department can point at a terminated
employee; that is why the filtered index on `ManagerEmployeeId` exists, so the report of such departments is a
cheap query rather than a scan.

---
document_id: FP-008-LIFE
title: HR Position — Lifecycle Model
status: Approved for Implementation
version: 1.0
---

# FP-008 — Lifecycle Model

## States

**Decision `DEC-POS-0011`: two states, `Active` and `Inactive`.** Classification:
**SETTLED-BY-PRECEDENT** (`DEC-DEP-0011`, `BR-PLT-0003`).

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
| **Initial state** | `Active`. A position created inactive would be a job nobody can fill and nobody asked for |
| **Deactivate** | `HR.Positions.Deactivate`. Records `StatusChangedUtc` and `StatusChangedBy` |
| **Reactivate** | `HR.Positions.Deactivate` — the same permission covers both directions (`DEC-POS-0018`, following `DEC-DEP-0025`) |
| **Physical delete** | **Never** (`BRULE-POS-0012`) |
| **Terminal state** | `Inactive`, but reversibly so |

`Inactive` is reversible here, as it is for Department and unlike Employee's `Terminated`. Retiring a job is
an organizational decision organizations reverse.

## The collision this package has to name — `OD-POS-005`

Department's lifecycle has a clean answer for deactivation with members: **they stay**. `BRULE-DEP-0015`
records why — `BR-HR-0009` says an inactive department may not receive *new* employees and does not say
existing ones are expelled, and expelling them "would violate `BR-HR-0005` for every one of them the instant
a department was deactivated — using one rule to break another."

`BR-HR-0006` is worded differently, and the difference matters: *"Every employee must have one **active**
position."* If **active** qualifies the position's lifecycle status, then leaving incumbents on a deactivated
position breaks the rule for all of them at that instant — the Department answer is unavailable, and
deactivation must be refused instead.

| `OD-POS-005` reading | Deactivating a Position with incumbents | Consistency |
|---|---|---|
| **(i) The assignment is current** | **Allowed.** Incumbents stay; the position stops accepting new arrivals | Matches Department exactly. A retired job may still have holders, which some readers will find odd |
| **(ii) The position's status is `Active`** | **Refused** until every incumbent is moved | `BR-HR-0006` is true at every instant. Diverges from Department, and retiring a job becomes a multi-step operation with no bulk-move tool in scope |
| **(iii) Both** | **Refused**, as (ii) | as (ii) |

> **RULING 2026-08-21 — reading (i), the assignment.** "One active position" means the employee has one
> *current* assignment; it does not require the position itself to be `Active`.
>
> **Deactivating a Position with incumbents is ALLOWED.** The Department answer is available after all, and
> the collision this section named does not occur. A retired job may still have holders — the oddity reading
> (i) was said to carry — and that is accepted in exchange for never using one rule to break another.
>
> **An `Inactive` Position still refuses a NEW assignment**, which is the half of `BR-HR-0009`'s shape that
> does apply. `position.has_incumbents` is **not** an error this package defines: no operation raises it.

The paragraphs above are preserved as the analysis that produced the question. The rest of this document is
written to the ruling.

## Deactivation rules

| Situation | Behaviour | Rule |
|---|---|---|
| Position has incumbents | **Allowed.** They stay, and `BR-HR-0006` remains satisfied for each of them | `BRULE-POS-0014` |
| New Employee created into an inactive Position | **Refused** | `BRULE-POS-0013` |
| Employee changed into an inactive Position | **Refused** | `BRULE-POS-0013` |
| Employee changed *out of* an inactive Position | **Allowed** — this is how a position is emptied | `BRULE-POS-0014` |
| Position referencing an `Active` grade | Unaffected | — |
| Grade with `Active` positions referencing it | **Refused** until those positions are deactivated or re-pointed. No cascade | `BRULE-POS-0015`, `DEC-POS-0013` |
| Grade with `Inactive` dependents only | Allowed | `BRULE-POS-0015` |
| Reads of an inactive Position or grade | **Still visible**, marked inactive | `BRULE-POS-0012` |

**Why grade deactivation does not cascade.** Carried unchanged from `DEC-DEP-0012`: a cascade would
deactivate an arbitrary amount of structure from one action, and the operator would have no record of what
was already inactive beforehand — so reactivation could not restore the prior state. Refusing until
dependents are handled is more work for the operator and is the only version that is reversible.

## Employee lifecycle interaction

| Event | Effect on the Position relationship |
|---|---|
| Employee is **terminated** | **Nothing.** The position is retained, because a historical employment record without a job is unreadable (`BRULE-POS-0020`) |
| Employee **transfers branch** | Nothing. Position is a company-level relationship; branch is orthogonal (`BRULE-POS-0019`) |
| Employee **changes department** | **Nothing.** `OD-POS-003` ruled Position independent of Department, so the two are unrelated (`BRULE-POS-0019`) |
| Employee **changes position** | `PositionId` is updated and one immutable history record is appended, atomically (`BRULE-POS-0018`) |
| Position is **deactivated** | **Nothing.** The employee keeps it (`OD-POS-005`, reading (i)) |

**Why termination does not clear the position.** The same reasoning `BRULE-DEP-0020` records for department:
an automatic clear destroys information — the record would show no job and nothing would say there had been
one — and it makes a write to org structure a side effect of an unrelated HR operation. A terminated
employee's last position is part of what makes their record readable.

## The one asymmetry a reader will notice

A Position with incumbents **may** be deactivated. A grade with active dependents **may not**. That looks
inconsistent until the difference is named, so it is named here rather than left to be rediscovered.

An employee's assignment is a **fact about a person** that survives the position's retirement — the employee
still holds that job title today, whether or not the organization is still hiring for it. A grade reference is
a **structural pointer**, and deactivating its target would leave an `Active` position pointing at an
`Inactive` grade: an incoherent tree, refused for the same reason `DEC-DEP-0006` step 3 refuses an active
child under an inactive parent.

`DEC-POS-0012` and `DEC-POS-0013` record both halves.

## What this document settled by ruling rather than by design

The central question here — what happens when a position with incumbents is deactivated — was `OD-POS-005`,
and it was **not** answered by this document. It was raised as an owner decision precisely because a lifecycle
model that quietly picked one reading would settle a business rule's meaning by implementation, which is what
`ADR-026` decisions 9 and 10 were written to prevent. The owner ruled reading (i) on 2026-08-21, and the
tables above are written to that ruling.

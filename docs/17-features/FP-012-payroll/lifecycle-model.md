# FP-012 — Lifecycle Model

The run state machine, and the two questions it cannot answer alone.

---

## States

Proposed per `OD-PAY-0009` option 2.

```
  Draft ──calculate──▶ Calculated ──approve──▶ Approved ──post──▶ Posted
    ▲                       │                                        │
    └──────recalculate──────┘                                        │
                                                                     ▼
                                                          (correction: reverse
                                                           in GL + new run)
```

| State | Means | Mutable |
|---|---|---|
| **Draft** | The run exists, its population and period are fixed, no amounts | yes |
| **Calculated** | Lines exist for every included employee | yes — recalculation replaces the line set |
| **Approved** | Someone with the elevated permission has authorized these amounts | no |
| **Posted** | A balanced journal exists in GL and the run names it | never |

**No state is skipped.** A run cannot be approved without being calculated, and cannot be posted without
being approved (`REQ-PAY-0010`).

---

## Why approval is its own state and its own permission

`BR-PLT-0103` names **Payroll Processing** a sensitive operation requiring elevated permissions. The
authored rule does not say *which* act is the sensitive one, so this package places it at **approval** and
says why.

Calculation is a computation — it can be run, inspected, found wrong and run again, and it commits nothing.
Approval is the assertion *these are the amounts these people will be paid*, and it is the gate the GL
posting passes through.

This mirrors GL exactly: `GL.Drafts.Manage` and `GL.Journals.Post` were deliberately separated so that
preparing work and authorizing it can be different people. Payroll has the same shape and a stronger reason
for it.

---

## Recalculation

Free before approval, impossible after posting (`REQ-PAY-0012`).

**A recalculation replaces the entire line set** rather than adjusting lines. Lines are append-only within
a *calculation*, not across calculations — the alternative, mutating lines in place, would make a payslip
read differently before and after an event nobody recorded.

*Open:* whether superseded line sets are retained or discarded. Retaining them makes "why did the number
change between Tuesday and Thursday" answerable; discarding them keeps the table proportional to the
current truth. Not raised as its own `OD-PAY` because it falls out of `OD-PAY-0011` — but the ruling should
state it explicitly, because the two are easy to decide inconsistently.

---

## Correction after posting

There is no edit path. A posted run is immutable because its journal is immutable, and posted journals are
append-only (`DEC-PAY-0012`, `BR-PAY-0008`).

Correcting therefore means:

1. **Reverse the journal in GL** — a reversing entry, which is GL's own correction mechanism and requires
   `GL.Journals.Reverse`.
2. **Create a new payroll run** for the same period with corrected inputs.
3. Both runs remain; the reversal makes the ledger truthful.

**This is a chain of two sensitive operations across two modules**, and `OD-PAY-0011` should say whether
Payroll orchestrates it or merely permits it. Orchestrating means Payroll can trigger a GL reversal, which
is a meaningful widening of what the posting contract does — and therefore of `OD-PAY-0013`.

---

## The closed-period interaction

`BR-GL-0003` prohibits posting into a closed fiscal period. The check belongs at **approval**
(`OD-PAY-0014` option 1), not at posting.

The reason is a state with no exit: if the check sits at posting, a run can be Approved — authorized,
sensitive, believed final — and then be unpostable, with no legitimate transition available. It cannot go
back to Draft (approval already happened) and it cannot go forward.

**Period closure is never reopened automatically.** Option 3 of `OD-PAY-0014` — auto-reopen, post,
re-close — is recorded as considered and rejected: it turns `GL.Periods.Close` into a suggestion a
subordinate module can overrule.

---

## Inclusion, and the termination question

A run's population is fixed when it is created: **every employee employed for at least one day of the
period** (`REQ-PAY-0008`, `BR-PAY-0003`).

`BR-HR-0004` says *a terminated employee cannot be assigned new business transactions.* Read literally,
final pay is barred — and people do not get paid for work they have done.

**This package reads it as barring new obligations, not the settlement of existing ones**, and raises it as
`OD-PAY-0010` rather than deciding it, because it is an *interpretation of an authored rule* rather than a
gap in one. Interpreting somebody else's rule silently is the thing the decision register exists to
prevent.

---

## What has no lifecycle here

**Payment.** There is no Paid state, because the product has no banking or payment integration and nothing
could ever set it (`OD-PAY-0009` option 3).

**Approval workflow beyond one step.** Multi-level approval, delegation and escalation are not proposed;
one elevated approval is what `BR-PLT-0103` supports.

**Period-end or year-end close for payroll.** GL closes fiscal periods; Payroll has no equivalent ceremony
proposed, and none is implied by anything above.

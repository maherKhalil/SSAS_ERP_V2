# FP-013 — Lifecycle model (proposed)

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

Two lifecycles, and the discipline that connects this module to Payroll.

---

## The attendance period

```
        Open ──── close ────▶ Closed
         ▲                       │
         └──── reopen ───────────┘
       (RULED IN: safe, because append-only records mean reopen permits appending, never editing)
```

**The reopen arrow exists or it does not, and `OD-ATT-0012` decides.** It is drawn dashed here because the
consequences run far past this diagram: under `DEC-ATT-0009`, records in a period that can reopen **cannot
be `IAppendOnlyEntity` at all**, since `PreventAppendOnlyMutation` refuses `Modified` unconditionally and
does not consult period status.

So the three rulings give three different shapes:

| Ruling | Period states | Record shape |
|---|---|---|
| **(a)** corrections are next-period adjustments | Open → Closed, one way | append-only from creation |
| **(b)** reopen with permission | Open ⇄ Closed | **not** append-only; immutability rests on a status check |
| **(c)** mutable until payroll approval | Open → Closed, one way | **two types**, draft and final, per the FP-012 split |

**Transitions are named-action POSTs with their own permissions**, never a status field on a request body.
`OD-PAY-0009`'s reasoning: the most consequential act in a module should not arrive through the same door as
an ordinary edit.

### Closing is a checked act, not a flag flip

A close should refuse rather than proceed when the period is not in a state to close. What counts as such a
state depends on rulings not yet made — whether every active employee needs a record, whether pending leave
requests block, whether a zero-record period is legitimate. **Each of those is a proper acceptance-criterion
question and none is answerable before `OD-ATT-0001`.**

Named here so the build does not implement close as `Status = "Closed"` and discover the questions later.

---

## The leave request — scope B and C

```
   Submitted ──▶ Approved ──▶ (dates pass) ──▶ Taken
       │                            │
       ├──▶ Rejected                └──▶ Cancelled  ◀── rules differ
       │                                                after the dates
       └──▶ Cancelled (by requester, before decision)
```

**Balance moves on approval only** (`REQ-ATT-0015`). Submission reserves nothing — which is a decision, and
it has a visible consequence: two requests can be submitted against a balance that only covers one, and the
second approval is what fails.

**The alternative — reserving on submission — is defensible** and produces a friendlier failure (the
requester learns at submission rather than the approver at decision). It also introduces a reservation that
must be released on rejection, on cancellation, and on expiry, which is three more paths to get wrong.
`OD-ATT-0006` should rule it, and this package's opinion is to move on approval, accepting the later
failure.

**Cancellation after the dates have passed is different in kind**, because by then the absence is a fact
that happened. Cancelling it is a correction, so it lands under `OD-ATT-0012`'s ruling rather than under
ordinary cancellation.

---

## The close discipline with Payroll — `OD-ATT-0010`

**This is the join between the two modules, and getting it wrong is expensive rather than annoying.**

A payroll run calculated from a still-open attendance period is a snapshot of a moving target. Payroll runs
get approved and **posted to GL**, so a wrong snapshot becomes a posted journal entry — and reversing a
posted entry is a business event, not a fix.

```
   Attendance period Open
            │
            │   Payroll asks: InspectAttendancePeriodAsync(companyId, date)
            │        ──▶ PeriodOpen        ← a MODELLED outcome, not an exception
            ▼
   Attendance period Closed
            │
            │   Payroll asks again ──▶ Available
            ▼
   Payroll calculates, approves, posts
```

The precedent is one module away and already proven: `IJournalPoster.InspectPostingWindowAsync` let Payroll
ask GL whether a period was open **before** attempting to post, so the caller received a clear refusal
instead of a late failure. The closed enum `JournalPostingStatus` made every outcome a value the caller had
to handle.

**`OD-ATT-0010` should rule (a) with an inspect method** — this package's opinion — giving a closed enum
along the lines of:

```
AttendanceSummaryStatus: Available | PeriodOpen | PeriodNotFound | EmployeeNotInScope
```

Options (b) — read an open period but report its state — and (c) — no close concept — remain open. **(c) is
the one to be wary of**, because it makes the failure silent: payroll numbers that are simply wrong, with
nothing anywhere reporting that anything happened.

---

## What the calculation may and may not do

`OD-PAY-0009` barred the calculator from approving its own run: the actor who computes a figure must not be
the actor who blesses it. The analogue here is `OD-ATT-0007`'s **self-approval bar** — and under the
department-manager finding it is not hypothetical, because a department manager submitting leave would
otherwise resolve to themselves as approver.

**The bar belongs in the domain, not the endpoint.** A permission check answers "may this person approve
requests"; it cannot answer "may this person approve *this* request". Only the aggregate knows both the
requester and the approver.

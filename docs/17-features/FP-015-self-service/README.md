---
package: FP-015
title: Employee Self Service
module: Platform + HR + Payroll + Attendance (consumers)
status: DRAFT — owner decisions unruled; specification only, no code
version: 0.1
date: 2026-08-27
---

## ⚠ Provenance of the documents added 2026-08-30

Five design documents reached this package on 2026-08-30 from a branch opened **2026-08-27**, unmerged for
**384 commits**. They are landed rather than discarded because their measurements held up — **but they were
not all re-verified, and which is which matters.**

| document | state |
|---|---|
| `authorization-model.md` | **Re-verified against the tree.** §§1, 3, 4, 6 confirmed line by line. **§§2, 5, 7, 8 were overtaken by work done since — see the amendment at the head of that file, and read it before its findings.** |
| `api-contracts.md` | **Re-verified 2026-08-30.** §§1, 2, 3 hold. **§§4 and 5 name absences since closed; one citation corrected in place** — see the amendment at the head of that file. |
| `data-model.md` | **Re-verified 2026-08-30 — NO discrepancies, the first such.** The table it specifies is implemented column for column, both unique index names match, and its one named gap (`TenantModelResidencyTests`) is closed. |
| `domain-model.md` | **Not re-verified.** As written 2026-08-27. |
| `lifecycle-model.md` | **Not re-verified.** As written 2026-08-27. |

**Treat the four unverified documents as a draft of that date, not as a current description of the tree.**
The one document that was checked had **four of its eight sections overtaken in three days** — so the base
rate here is high, and silence about the others is not evidence that they held.

**What the check found is worth carrying: every cited line number still resolved 384 commits later.** The
drift was not decay of citations; it was that things named as ABSENT had since been built. **If these four
are re-verified later, expect the same shape — the descriptive parts sound, the claims about what is
missing stale.**

# FP-015 — Employee Self Service

**Status: DRAFT.** No `OD-SS` decision is ruled except one, recorded below. **No code, no schema,
nothing implemented.** This package does not become buildable until its owner decisions are ruled and
[`decisions-ratified.md`](decisions-ratified.md) exists, exactly as FP-014 required.

---

# WHY THIS PACKAGE EXISTS — IT IS NOT A ROADMAP ITEM

Self-service is on the roadmap as a bare line. **That is not its authority.** Its authority is that
**two ratified packages deferred a requirement each, both naming the same missing input, and that
input now exists.**

```
OD-PAY-0016   payroll self-service DEFERRED — no identity→employee mapping
OD-ATT-0013   attendance self-service DEFERRED — "no identity→employee assumption anywhere.
              Third time deferred; NOW A RECORDED FUTURE PACKAGE."
ADR-030       Identity-to-Employee Mapping — status: ACCEPTED, 2026-08-25
```

**FP-015 is that recorded future package, and its stated precondition has fired.**

FP-012 put it most strongly: the mapping *"was flagged as unverified, and **it must not be built
on**."* That prohibition is lifted by `ADR-030` being `Accepted` — **not by anyone deciding the
feature is now wanted.** The deferrals were never scoping preferences. `FP-013` recorded the
distinction explicitly: *"This is `DEC-PAY-0002`'s shape — a missing input, not a scoping
preference."*

## The two requirements this package unblocks, by name

| Requirement | Package | State today |
|---|---|---|
| **`REQ-ATT-0023`** — an employee may read **their own** attendance and leave records | FP-013 | **BLOCKED, UNAUTHORED.** *"no `ViewOwn` permission exists, asserted"* |
| **`OD-PAY-0016`** — payroll self-service | FP-012 | **DEFERRED**, same missing input |

**`REQ-ATT-0023` is `UNAUTHORED`** — it has no acceptance criteria and no scenarios, deliberately,
because a requirement that cannot be built should not carry criteria that cannot be tested. **This
package authors them.**

---

# THE ONE DECISION ALREADY RULED

**`OD-SS-0001` — how is "view my own X" authorised?**

**RULED BY THE OWNER, 2026-08-27: a DISTINCT PERMISSION, not a scope on the existing one.**

`payroll.payslip.view` reads any employee's payslip. `payroll.payslip.view.self` reads only the
caller's own, resolving the employee from `ADR-030`'s mapping. **The self-service endpoint has no
parameter for another person's record and therefore no other mode.**

**The reasoning is structural and is the reason it was chosen over the cheaper option.** A scope
applied at the handler requires every handler to remember to apply it, and **a handler that forgets
serves everyone's payslips while looking correct.** The ninety-five architecture guards in this
repository assert *permissions*, not scopes — so nothing would catch the omission. A distinct
permission makes the omission unrepresentable rather than unlikely.

**This ruling is what creates the `ViewOwn` permission `REQ-ATT-0023` asserts does not exist.**

---

# ⚠ THE BOUNDARY OF THIS PACKAGE — read this before anything else

**In scope:** an authenticated identity reading **its own** records, across the modules that deferred
it. Nothing else.

**Explicitly out of scope, and each for a stated reason rather than for brevity:**

- **Writing.** No self-service submission of anything — no leave request, no timesheet, no profile
  edit. **Reading is the deferred requirement; writing was never deferred because it was never
  proposed.** A write surface is a different package with different invariants.
- **A self-service UI.** This package specifies authorisation, contracts and criteria. Presentation
  is not specified here.
- **Manager self-service.** *"My team's records"* is a delegation model, not a self model, and it
  needs an org-hierarchy decision this package does not make.
- **Creating the identity→employee mapping.** `ADR-030` already ruled it: a Platform-plane mapping
  keyed by tenant, **optional on both sides, no foreign key in either direction and none possible**,
  because the two live in different databases. **This package consumes it and must not redesign it.**

---

# WHAT IS NOT YET DECIDED

Every `OD-SS` beyond `OD-SS-0001` is open. The substantive ones, stated as questions rather than
pre-answered:

- **Which modules are in the first cut?** Attendance and payroll deferred explicitly. HR profile is
  implied by neither.
- **What happens when the mapping is absent?** `ADR-030` makes it optional on both sides — most
  employees have no user, and a user may be an external accountant with no employee record. **A
  self-service call from an unmapped identity is a case, not an error to be discovered later.**
- **Does a terminated employee retain self-service access to their own history?** A payslip is a
  legal record; the answer is not obviously "no" and is not this package's to assume.
- **Does self-access survive module entitlement being switched off?** `BR-PLT-0008` gates every
  mounted route group. **A self-service route is a module route.**

---

# AUTHORITY AND DEPENDENCIES

- **`ADR-030`** (`Accepted`) — the identity→employee mapping. **This package's precondition.**
- **`OD-ATT-0013`**, **`OD-PAY-0016`** — the deferrals this package closes.
- **`REQ-ATT-0023`** — blocked and unauthored; this package authors its criteria.
- **`BR-PLT-0008`** — every mounted route group is gated. Applies here unchanged.
- **`DEC-L-058`** — this package is developed on `ClaudeBranch`.

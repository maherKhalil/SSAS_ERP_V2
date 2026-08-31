---
package: FP-015
title: Employee Self Service
module: Platform + HR + Payroll + Attendance (consumers)
status: DRAFT — owner decisions unruled. ⚠ NOT specification-only: the self-service routes shipped 2026-08-28, and TWELVE of fourteen acceptance criteria are pinned by named tests — the other two implemented and guaranteed by construction, none unbuilt (measured T-206, completed T-207, 2026-08-31)
version: 0.1
date: 2026-08-27
---

## ⚠⚠ CORRECTED 2026-08-31 (T-205, T-206) — THE *NO CODE* CLAIM WAS TRUE WHEN WRITTEN AND FALSE THE NEXT DAY

**This front matter read `specification only, no code`, dated 2026-08-27.** ⚠ **`GET /me/records`,
`GET /me/leave-requests` and `GET /me/payslips` are live, and `git blame` dates all three to
2026-08-28.** **The scope this package defines — *an authenticated identity reading its own records,
across the modules that deferred it* — shipped the day after the claim was written.**

**This is the second package in this repository whose implementation-status claim was falsified within
twenty-four hours**, after FP-014's *No code and no schema*. **Both were written deliberately and
precisely, which is exactly why nobody re-read them.** See **Principle 20**: an implementation-status
claim is re-derived, never inherited.

⚠ **AND THE CLAIM UNDERSTATED THE PRODUCT TWICE.** Not merely that code exists — **nine of fourteen
acceptance criteria are pinned by named tests, two of them STRUCTURALLY: the route binds no subject, so
another employee's records are unreachable rather than forbidden.** **And `AC-SS-0007` is carried by a
shared `SelfServiceContractRule`, generalised across modules in T-089 — *a self-service route may bind
FILTERS, never a SUBJECT.*** ⚠ **A package described as specification-only holds a live architectural
invariant that has already survived one revision**, strengthened when a second self route made
`bound.Count == 0` untenable.

**COMPLETED T-207: ALL FIVE REMAINING CRITERIA ARE IMPLEMENTED. NOT ONE WAS UNBUILT.** ⚠ **Nine becomes
TWELVE PINNED of fourteen** — `AC-SS-0010` and `0011` by `The_link_is_untouched_by_a_refusal`, which is
**exactly the failure mode `AC-SS-0011` names for itself**; `AC-SS-0012` by `An_ended_employment_does_not_resolve`,
**whose source names the criterion by id** (T-090, `DEC-L-073`).

⚠ **`AC-SS-0013` and `0014` ARE CALLED NEITHER PINNED NOR UNBUILT, DELIBERATELY.** They are guaranteed by
construction — the self routes sit inside the group carrying `RequireModule`, and both modules say so in
source — **but what is NOT asserted is the COMPOSITION: no test says *the SELF route specifically is
refused when the module is off*.** The gate is proven on a probe route, membership is structural and
commented, **and their conjunction is inferred.** **What would settle it: one test per module hitting
`/me/payslips` and `/me/records` on an unentitled tenant — the gate suites already have the harness.**

⚠⚠ **AND THE CANDIDATE FILE LIST THAT T-206 OFFERED WAS WRONG, BY A HOMONYM.**
`AttendanceRouteInventoryTests`' *Entitlement* is **LEAVE entitlement — days of leave** — **not module
entitlement.** **The coverage lives in files nobody had named:** `ModuleEnablementGateTests`,
`ExpiredTenantGateTests`, `UserEmployeeResolverSeamTests`, `EmployeeTerminationAccountClosureTests`.
**A candidate list built by matching names is itself a name match, and inherits every homonym in it.**
**The owner-decision half of the status is unchanged and still true: the decisions are unruled.**

---
## Provenance of the documents added 2026-08-30 — all five re-verified

Five design documents reached this package on 2026-08-30 from a branch opened **2026-08-27** and unmerged for
**384 commits**. **All five have now been checked against the tree, one at a time.**

| document | result |
|---|---|
| `authorization-model.md` | **4 discrepancies.** §§1, 3, 4, 6 hold. **§§2, 5, 7, 8 were overtaken** — see the amendment at the head of that file. |
| `api-contracts.md` | **3.** §§1, 2, 3 hold. §§4 and 5 name absences since closed; one citation corrected in place. |
| `data-model.md` | **0.** The table it specifies is implemented column for column. |
| `domain-model.md` | **0.** Type, base class, members, and the physical-removal rule all match; the mistake it names as most likely was not made. |
| `lifecycle-model.md` | **1.** States and transitions hold. One "what has no lifecycle here" claim expired — see its amendment. |

**⚠ An earlier version of this section warned that "the base rate here is high", extrapolated from the one
document that had been checked at the time. That was wrong, and it is corrected here rather than quietly
removed.** Eight discrepancies fell across five documents, **and four of them were in a single file**. Two
documents were perfect.

**What actually predicts staleness is not age. It is whether a section describes what IS or what IS NOT.**
All eight discrepancies are absence claims — *"no guard asserts…"*, *"appears unimplemented"*, *"none"*,
*"does not exist in the product"* — and **not one descriptive section in five documents was wrong.** The two
documents that match the tree perfectly are the two describing a table and a type that **did not exist when
they were written**, which is the opposite of what "unverified for 384 commits" would suggest.

**And one expired sentence was half rule.** *"Employment type does not exist in the product, and belongs to
FP-006"* stated a fact that lasted two days and an instruction that was obeyed — the type was built in HR,
not here. **When re-reading a specification, separate the prediction from the rule before discarding either.**

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

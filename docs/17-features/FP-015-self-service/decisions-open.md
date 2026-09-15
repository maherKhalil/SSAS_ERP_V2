---
package: FP-015
title: Self Service — Owner Decisions
status: DRAFT — four ruled 2026-08-27, remainder open
version: 0.1
date: 2026-08-27
---

# Owner decisions — FP-015

## `OD-SS-0001` — how is "view my own X" authorised?

**RULED, owner, 2026-08-27: a DISTINCT PERMISSION, not a scope.**

`payroll.payslip.view` reads any employee's payslip. `payroll.payslip.view.self` reads only the
caller's own. **The self-service endpoint has no parameter for another person's record and therefore
no other mode.** A scope applied at the handler would rely on every handler remembering, and the
ninety-five architecture guards assert *permissions*, not scopes — nothing would catch an omission.

**This is what creates the `ViewOwn` permission `REQ-ATT-0023` asserts does not exist.**

---

## `OD-SS-0002` — which modules are in the first cut?

**RULED, owner, 2026-08-27: proceed. Attendance and payroll.**

Those two deferred self-service by name (`OD-ATT-0013`, `OD-PAY-0016`) and are the requirements this
package closes. **HR profile self-service is recorded as an open extension, not scoped out** — it was
never deferred because it was never proposed.

---

## `OD-SS-0003` — what does a self-service call from an identity with no employee record do?

**RULED, owner, 2026-08-27, and the ruling CORRECTED THIS PACKAGE'S PREMISE.**

The README's original example was an external accountant — *"a user may be an external accountant
with no employee record."* **The owner rejected the example:** an external accountant who is paid
**is an employee**, with an employment type of part-time or freelance.

**The unmapped case is therefore narrower than this package first described.** It remains real —
`ADR-030` makes the mapping optional on both sides — but its population is platform support staff and
users created before their employee record exists, **not paid external parties.**

**Behaviour:** an unmapped identity is an **ordinary refusal, not an exception.** `ADR-030` makes
absence a normal state; a self-service call from an unmapped identity answers "no employee record for
this identity" the way a closed period answers a posting attempt. It is not a fault and must not be
discovered as one.

### ⚠ THIS RULING SURFACED A PRODUCT GAP THAT IS NOT THIS PACKAGE'S TO FILL

**Employment type does not exist in this product.** Verified 2026-08-27:

```
Employee      no employment-type field   (TenantId … Status, StatusChangeReason, …)
Position      no employment-type field
src/          no EmploymentType / EmployeeType / ContractType / WorkType enum anywhere
FP-006 spec   part-time / full-time / freelance / contractor not mentioned
```

**The ruling presumes a concept the product does not have.** It is not blocking for FP-015 — nothing
here reads employment type — but it is a real gap, and it is **cross-cutting rather than an HR field**:

- **Payroll proration.** `OD-PAY-0015` records proration as **calendar days, unchanged, "the lever is
  recorded as untaken."** Part-time employment is exactly that lever.
- **Attendance.** A part-timer's expected hours differ from a full-timer's; the working calendar
  assumes one shape.
- **Leave entitlement.** Ordinarily pro-rated by employment type.

**Recorded here because this is where it was found. It belongs to FP-006 and must not be built inside
FP-015.**

---

## `OD-SS-0004` — does a terminated employee retain self-service access?

**RULED, owner, 2026-08-27: history is KEPT; login and self-service are DISABLED.**

The two halves are separate and both are load-bearing:

- **The records survive.** A payslip is a legal record and termination does not delete it. The
  identity→employee mapping is **not** severed on termination — severing it would make the history
  unattributable.
- **The access does not.** A terminated employee cannot authenticate and cannot reach self-service.

**So the guard is on the identity, not on the mapping** — which matters, because the naive
implementation (drop the mapping at termination) satisfies the second half and destroys the first.

---

## `OD-SS-0005` — does self-service survive the tenant's subscription expiring?

**RULED, owner, 2026-08-27: no employee login and no self-service during expiry; history kept.**

**⚠ ONE READING ASSUMED, STATED SO IT CAN BE CORRECTED CHEAPLY.**

`DEC-L-033` ruled that **expiry gates modules and does not block login** — a lapsed customer who
cannot log in cannot reach the page that would let them subscribe.

The owner's wording is *"no employee login in system or self service"*. Two readings:

- **(A) — TAKEN.** Employees may still authenticate, and reach **nothing**: self-service is a module
  route, `BR-PLT-0008` gates every mounted route group, so an expired subscription closes it along
  with every other module surface. **Fully consistent with `DEC-L-033`; no amendment required.**
- **(B) — not taken.** Employees cannot authenticate at all. This would **amend `DEC-L-033`** and
  require the authentication layer to distinguish employee from subscription-payer — a distinction
  `DEC-L-033` was written to avoid, and one that breaks its own rationale when the payer *is* an
  employee.

**(A) achieves the stated intent — employees get nothing during expiry — without a new distinction in
the auth layer.** If the intent was (B), it is one sentence to say so and the difference is real.

**History is kept under either reading.** Expiry gates access; it does not delete records.

---

## Still open

- **HR profile self-service** — in the first cut or not (`OD-SS-0002` left it open).
- **Write surfaces** — out of scope by the README's boundary; the boundary is not a ruling.
- **Presentation** — not specified by this package.

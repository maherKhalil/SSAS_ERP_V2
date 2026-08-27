---
package: FP-015
title: Self Service — Acceptance Criteria
status: DRAFT
version: 0.1
date: 2026-08-27
---

# Acceptance Criteria — FP-015

**Every criterion below is paired with a scenario in `test-scenarios.md` and a row in
`traceability-matrix.md`.** Nothing here is carried by prose.

---

## `REQ-SS-0001` — read own attendance and leave

### AC-SS-0001 — A mapped identity holding the self permission reads its own records
An authenticated identity with an `ADR-030` mapping and `attendance.record.view.self` receives the
attendance and leave records of the employee it maps to.

### AC-SS-0002 — Another employee's records are unreachable, not forbidden
The endpoint accepts no employee identifier. **There is no request that names another employee**, so
the refusal is structural rather than a check that could be omitted.

---

## `REQ-SS-0002` — read own payslip

### AC-SS-0003 — A mapped identity holding the self permission reads its own payslips
As `AC-SS-0001`, for `payroll.payslip.view.self`.

### AC-SS-0004 — A payslip belonging to another employee is unreachable
As `AC-SS-0002`.

---

## `REQ-SS-0003` — distinct permission, not a scope

### AC-SS-0005 — The administrative permission alone does not grant self access
An identity holding `payroll.payslip.view` and **not** `payroll.payslip.view.self` is refused at the
self-service endpoint. **The two permissions are independent; neither implies the other.**

### AC-SS-0006 — The self permission alone does not grant administrative access
An identity holding `payroll.payslip.view.self` and **not** `payroll.payslip.view` is refused at the
administrative endpoint. **This is the direction that matters commercially:** granting every employee
self-service must not grant them the payroll register.

---

## `REQ-SS-0004` — no parameter identifying whose record

### AC-SS-0007 — The route carries no employee identifier
The self-service route's contract has **no path, query, header or body member naming an employee.**
Asserted against the transport contract, not against a handler's behaviour.

---

## `REQ-SS-0005` — unmapped identity

### AC-SS-0008 — An unmapped identity receives an ordinary refusal
An authenticated identity with no `ADR-030` mapping receives a refusal naming the condition — *no
employee record for this identity*. **The same shape as a closed fiscal period refusing a posting.**

### AC-SS-0009 — It is not an exception
The unmapped case produces **no thrown exception, no 5xx, and no entry in an error log.** `ADR-030`
makes absence a normal state; a normal state that is logged as a fault trains its readers to ignore
the log.

---

## `REQ-SS-0006` — retention

### AC-SS-0010 — The mapping survives termination
An employee's `ADR-030` mapping is **unchanged** by termination. Asserted on the mapping itself, not
on what can be read through it.

### AC-SS-0011 — Terminated employees' records remain attributable
A payslip belonging to a terminated employee still resolves to that employee. **This is the criterion
that fails if anyone implements `AC-SS-0012` by severing the mapping.**

---

## `REQ-SS-0007` — terminated access

### AC-SS-0012 — A terminated employee cannot reach self-service
An identity mapped to an employee whose status is terminated is refused at every self-service
endpoint. **The guard is on the identity's authorisation, not on the presence of the mapping** —
see `AC-SS-0011`.

---

## `REQ-SS-0008` — module gating

### AC-SS-0013 — Self-service routes are gated by module entitlement
A tenant without the payroll module entitlement cannot reach `payroll.payslip.view.self`, exactly as
it cannot reach any other payroll route. **`BR-PLT-0008` applies unchanged; no special case.**

### AC-SS-0014 — An expired subscription closes self-service
A tenant whose subscription has expired reaches no self-service surface. **Authentication is
unaffected** (`DEC-L-033`) — the identity may still log in and reach the platform plane.

---

## Declared gaps

**None.** Every criterion above is owned by a requirement and carries a scenario.

---
package: FP-015
title: Self Service — Test Scenarios
status: DRAFT
version: 0.1
date: 2026-08-27
---

# Test Scenarios — FP-015

**Allocation: SEQUENTIAL.** `TS-SS-0001` onward with no blocks and no reserved tails — the modern
convention, so `trace-check.py` asserts contiguity here rather than reporting it.

---

## Reading own records

### TS-SS-0001 — Mapped identity reads own attendance
Given an identity mapped to employee E holding `attendance.record.view.self`, when the self-service
attendance endpoint is called, then E's records are returned. — `AC-SS-0001`

### TS-SS-0002 — Mapped identity reads own payslips
As above for `payroll.payslip.view.self`. — `AC-SS-0003`

### TS-SS-0003 — The route exposes no employee identifier
Given the self-service transport contract, when its members are enumerated, then **none names an
employee**. Asserted on the contract, so it cannot be satisfied by a handler that checks. —
`AC-SS-0002`, `AC-SS-0004`, `AC-SS-0007`

---

## The two permissions are independent

### TS-SS-0004 — Administrative permission alone is refused at the self endpoint
Given an identity holding `payroll.payslip.view` only, when the self-service endpoint is called,
then the call is refused. — `AC-SS-0005`

### TS-SS-0005 — Self permission alone is refused at the administrative endpoint
Given an identity holding `payroll.payslip.view.self` only, when the administrative payslip endpoint
is called, then the call is refused. **The direction that matters commercially.** — `AC-SS-0006`

---

## Unmapped identity

### TS-SS-0006 — Unmapped identity receives an ordinary refusal naming the condition
Given an authenticated identity with no `ADR-030` mapping, when a self-service endpoint is called,
then the result is a refusal naming *no employee record for this identity*. — `AC-SS-0008`

### TS-SS-0007 — The unmapped case throws nothing and logs no fault
Given the same call, then **no exception is raised, no 5xx is produced, and nothing is written to the
error log.** — `AC-SS-0009`

---

## Termination

### TS-SS-0008 — The mapping is unchanged by termination
Given employee E with a mapping, when E is terminated, then the mapping still resolves E. Asserted on
the mapping, not through a read. — `AC-SS-0010`

### TS-SS-0009 — A terminated employee's payslip remains attributable
Given a payslip belonging to terminated employee E, when it is resolved, then it still names E.
**This scenario fails if `TS-SS-0010` is implemented by severing the mapping.** — `AC-SS-0011`

### TS-SS-0010 — A terminated employee cannot reach self-service
Given an identity mapped to terminated employee E, when any self-service endpoint is called, then the
call is refused. — `AC-SS-0012`

---

## Module gating

### TS-SS-0011 — Self-service is closed without the module entitlement
Given a tenant without the payroll entitlement, when `payroll.payslip.view.self` is called, then the
route is gated exactly as every other payroll route. — `AC-SS-0013`

### TS-SS-0012 — An expired subscription closes self-service and leaves login working
Given a tenant whose subscription has expired, when an employee authenticates, then authentication
succeeds and every self-service surface is closed. — `AC-SS-0014`

---

## Declared gaps

**None.** Every scenario owns at least one criterion; every criterion is owned.

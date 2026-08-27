---
package: FP-015
title: Self Service — Requirements
status: DRAFT — derived from OD-SS-0001..0005, ruled 2026-08-27
version: 0.1
date: 2026-08-27
---

# Requirements — FP-015

**`REQ-SS-` is NOT yet registered in `Requirement-Numbering.md`.** FP-014 promoted `REQ-SUB-0001`
**at ratification**, discharging `DEC-L-012`'s obligation there rather than at drafting. **FP-015 owes
the same promotion at its own ratification and not before** — a prefix registered against a package
that never ratifies is a reserved number nobody can reuse.

---

| ID | Requirement | Ruling | State |
|---|---|---|---|
| `REQ-SS-0001` | An authenticated identity mapped to an employee may read **their own** attendance and leave records. | `OD-SS-0002` | **Closes `REQ-ATT-0023`**, which is `BLOCKED` and `UNAUTHORED` |
| `REQ-SS-0002` | An authenticated identity mapped to an employee may read **their own** payslips. | `OD-SS-0002` | Closes `OD-PAY-0016`'s deferral |
| `REQ-SS-0003` | Self-service read is authorised by a **distinct permission** per surface, not by a scope on the equivalent administrative permission. | `OD-SS-0001` | **Owner-ruled** |
| `REQ-SS-0004` | A self-service endpoint takes **no parameter identifying whose record is read.** The employee is resolved from the caller's identity via `ADR-030`. | `OD-SS-0001` | Structural consequence |
| `REQ-SS-0005` | A self-service call from an identity with **no employee record** is answered as an ordinary refusal, not an exception. | `OD-SS-0003` | `ADR-030` makes absence normal |
| `REQ-SS-0006` | A **terminated** employee's records are **retained**, and the identity→employee mapping is **not severed** on termination. | `OD-SS-0004` | Legal record |
| `REQ-SS-0007` | A **terminated** employee cannot authenticate and cannot reach self-service. | `OD-SS-0004` | Guard on the **identity** |
| `REQ-SS-0008` | Self-service routes are **module routes** and are gated by module entitlement exactly as every other module route is. | `OD-SS-0005`, `BR-PLT-0008` | No special case |

---

## `REQ-SS-0003` and `REQ-SS-0004` are one requirement stated twice, deliberately

`Payroll.Payslips.View` reads any employee's payslip. `Payroll.Payslips.ViewOwn` reads only the
caller's own.

**`REQ-SS-0004` is what makes `REQ-SS-0003` more than a naming convention.** A distinct permission on
an endpoint that still accepts an employee identifier is a scope with extra steps — the handler must
still remember to compare it to the caller. **An endpoint with no such parameter has no other mode**,
and forgetting becomes unrepresentable rather than unlikely.

**Stated separately because they can be implemented separately, and implementing only the first is
the failure this package exists to prevent.** The ninety-five architecture guards assert permissions,
not scopes; nothing in this repository would catch the handler that forgot.

## `REQ-SS-0006` and `REQ-SS-0007` pull in opposite directions and both are required

The obvious implementation of `REQ-SS-0007` is to sever the identity→employee mapping at termination.
**It satisfies `REQ-SS-0007` and destroys `REQ-SS-0006`** — the retained payslips become
unattributable, which is a worse outcome than the access it was closing.

**So the guard is on the identity, never on the mapping.** The mapping is a historical fact; the
authentication is a live capability. **Termination ends the second and must not touch the first.**

## `REQ-SS-0005` is a case, not an error path

`ADR-030` makes the mapping **optional on both sides**: most employees have no user, and a user may
be platform support staff with no employee record.

**An unmapped identity is therefore a normal state of the system**, and a self-service call from one
answers *"no employee record for this identity"* the way a closed fiscal period answers a posting
attempt — **an ordinary answer the caller acts on, not a fault discovered in a log.**

**`OD-SS-0003` narrowed this from the package's first draft**, which cited an external accountant as
the example. **The owner rejected it: an accountant who is paid is an employee.** The unmapped
population is platform support and users created before their employee record exists.

### ⚠ The MECHANISM in this requirement was wrong. The behaviour it asks for was not.

**As drafted, `REQ-SS-0005` reads as though the absence is a null identity.** T-076 measured it:
`CurrentAuthenticationSessionAccessor.cs:16-36` returns null only when there is **no tenant session at
all** — unauthenticated, platform plane, or a background composition. **A tenant-authenticated caller
always has a `TenantUserId`.**

**So there are two refusals with two causes, and only the second is this package's:**

| Cause | Answered where | Whose |
|---|---|---|
| no tenant session | never reaches a handler | the authentication layer's |
| a tenant user with **no linked employee** | the handler, as an ordinary result | **FP-015's** |

**The unmapped case is a lookup miss in `UserEmployeeLink`, not a null accessor.** The requirement's
behaviour is unchanged and correct; only the mechanism behind it was wrong. **Corrected here rather
than left for whoever implements it to discover** — see `api-contracts.md` §3.

## `REQ-SS-0008` — and the consequence stated plainly

Under `DEC-L-033` an expired subscription **gates modules and does not block login.** A self-service
route is a module route, so `BR-PLT-0008` closes it along with every other module surface.

**The consequence, which is a commercial fact rather than a technical one:** a tenant whose
subscription lapses cuts its employees off from their own payslips, while those employees can still
authenticate and reach a subscription page they have no authority to act on.

**The owner ruled this outcome directly** (`OD-SS-0005`). It is recorded here because it is the kind
of thing that reads as an oversight later, and it was not one.

---

## Not requirements — the boundary from `README.md`, restated so this file stands alone

**No writing.** No leave request, no timesheet, no profile edit. Reading is what was deferred.
**No UI.** **No manager self-service** — *"my team's records"* is delegation, not self.
**No redesign of `ADR-030`.** This package consumes the mapping.

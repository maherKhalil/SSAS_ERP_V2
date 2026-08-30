---
package: FP-015
title: Self Service — Lifecycle Model
status: DRAFT
version: 0.1
date: 2026-08-27
---

## ⚠ AMENDMENT 2026-08-30 — ONE CLAIM IN "WHAT HAS NO LIFECYCLE HERE" HAS EXPIRED

**The states and transitions are verified and hold exactly.** There is no `Status`, `IsActive` or soft-delete
on the type, and both indexes are unfiltered — `HasFilter` count is **zero** — so *"live is exists"* is
literally true of the schema.

**The expired sentence:** *"**Employment type.** Does not exist in the product (`OD-SS-0003`'s finding) and
belongs to FP-006."* **It exists and is fully shipped** — `enum EmploymentType` in `SSAS.HR.Contracts`, a
**required constructor parameter** on `Employee`, persisted, read back by `EmployeePlacementDirectoryService`,
with migration `20260829175328_AddEmployeeEmploymentType` dated **two days after this document was written.**

⚠ **But only half that sentence expired, and the half that held is the more important one.** It says two
things: *"does not exist"* — a **fact**, now false — and *"must not be built here; it belongs to FP-006"* — a
**rule**, which was obeyed: `EmploymentType` was built in HR by T-153, not in this package.

**A sentence of the form "X does not exist, and if it arrives it belongs elsewhere" is half prediction and
half instruction, and the two age at completely different rates.** The prediction had a shelf life of two
days. The instruction is still correct and still binding. **When re-reading a specification, separate those
before discarding the sentence** — deleting it for being stale would have thrown away the rule along with
the fact.


# Lifecycle Model — FP-015

**One object has a lifecycle in this package: the `UserEmployeeLink`.** Self-service itself is a read
surface and has no state. **This file exists because the link's lifecycle is where `REQ-SS-0006` and
`REQ-SS-0007` pull against each other**, and that tension is the package's single most likely
implementation error.

---

## States

**There are two, and neither is a status column.**

| State | Meaning |
|---|---|
| **linked** | a row exists for `(TenantId, TenantUserId)` |
| **unlinked** | no row exists |

**No `Status`, no `IsActive`, no soft delete.** `ADR-030` Decision 3 says *at most one **live** link
each way*, and `data-model.md` enforces that with two unfiltered unique indexes. **A filtered index
would require every uniqueness check and every read to remember the filter** — the reason
`UserBranchAccessConfiguration` gives for physical removal, adopted here.

**"Live" is therefore "exists".** There is no dead link to distinguish from a live one.

---

## Transitions

| Event | Effect on the link |
|---|---|
| tenant user created | **none** — `ADR-030` optional on both sides |
| employee created | **none** — same |
| link established | row inserted; both unique indexes enforce the cardinality |
| link corrected | old row removed, new row inserted |
| **employee terminated** | **NONE. See below.** |
| **subscription expires** | **NONE.** Access is gated; the link is untouched |
| tenant user deactivated | **none** — the guard is on authentication |

**Only two events write to this table**, and both are deliberate administrative acts.

---

## ⚠ Termination does not touch the link, and this is the error the package exists to prevent

**`REQ-SS-0007` says a terminated employee cannot reach self-service. `REQ-SS-0006` says their records
are retained and the link is not severed.**

**The obvious implementation of the first destroys the second.** Severing the link at termination
satisfies "cannot reach self-service" completely — and makes the retained payslips **unattributable**,
which `OD-SS-0004` ruled is the worse outcome and the reason the two halves were ruled separately.

**So:**

> **The link is a historical fact. Authentication is a live capability. Termination ends the second
> and must not touch the first.**

**The guard is on the identity's ability to authenticate — never on the presence of the link.**

**Recorded in five places on purpose** (`REQ-SS-0006`, `AC-SS-0011`, `TS-SS-0009`, `domain-model.md`,
here). `TS-SS-0009` is the scenario that fails if anyone implements `AC-SS-0012` by severing, and it
is written to fail for that reason rather than incidentally.

---

## The unmapped case is a state, not an error — and its mechanism was corrected

**A tenant-authenticated caller always has a `TenantUserId`** (`CurrentAuthenticationSessionAccessor.cs:16-36`,
established by T-076). **So "unmapped" is a lookup miss in `UserEmployeeLink`, not a null identity.**

**The two refusals have different causes and only one is this package's:**

- **no tenant session** — answered by the authentication layer; never reaches a handler.
- **a tenant user with no link** — answered by the handler as an ordinary result. **This one.**

`ADR-030` Decision 5: *"A support administrator opening a self-service page is not a fault condition;
it is Tuesday."*

**The population is platform support staff and users created before their employee record exists** —
`OD-SS-0003` narrowed it from this package's first draft, which cited an external accountant. **The
owner rejected that example: an accountant who is paid is an employee.**

---

## Subscription expiry — gated, not severed

`DEC-L-033`: expiry gates modules and does not block login. Self-service is a module route, so
`BR-PLT-0008` closes it with every other module surface (`OD-SS-0005`, reading A).

**Nothing about the link changes.** When the subscription is restored, self-service resumes with no
repair step, **because there was no state to repair.**

**The commercial consequence was ruled directly and is not an oversight:** a tenant whose subscription
lapses cuts its employees off from their own payslips, while those employees can still authenticate
and reach a subscription page they have no authority to act on.

---

## What has no lifecycle here

- **The permissions.** `Payroll.Payslips.ViewOwn` and the two Attendance ones are granted and revoked
  by ordinary role assignment; nothing in this package changes that path.
- **The read surfaces.** Stateless.
- **Employment type.** Does not exist in the product (`OD-SS-0003`'s finding) and belongs to FP-006.
  **Not modelled here and must not be built here.**

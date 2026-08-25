# FP-013 — Domain model (proposed)

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

**Conditional throughout.** `OD-ATT-0001` (scope) decides which aggregates exist at all, and `OD-ATT-0011`
(branch) decides the ownership interfaces every one of them carries. This document says what follows from
each ruling rather than quietly picking one.

---

## The one mechanical constraint that shapes everything

`TenantDbContext.PreventAppendOnlyMutation` refuses `EntityState.Modified` **or** `EntityState.Deleted` for
any `IAppendOnlyEntity` — **unconditionally**. No status check, no flag, no escape.

FP-012 discovered what that means the hard way. A payroll run needs mutable lines while it is a draft and
immutable lines once approved, and **no single type can be both**, because the refusal does not consult
status. The answer was three types: a mutable `PayrollRun`, a mutable `PayrollRunDraftLine`, and an
append-only `PayrollRunLine` with an `internal` constructor.

**The same question decides this module's shape**, and `OD-ATT-0012` is the ruling that decides it:

| `OD-ATT-0012` ruling | Consequence for the aggregate |
|---|---|
| **(a)** closed is closed; corrections are next-period adjustments | Records are append-only **from creation**; an "adjustment" is another append-only row. Simplest, and the GL model. |
| **(b)** reopen with permission | Records **cannot** be append-only at all, or reopening is unimplementable. Immutability then rests on a status check somebody must remember — the shape FP-012 rejected. |
| **(c)** mutable until the payroll run is approved | **Two types**, exactly as FP-012: a mutable draft record and an append-only final record, with the close transition converting one into the other. |

**This is not a detail to settle during implementation.** Rulings (a) and (b) produce different tables, and
discovering the difference after the migration exists is expensive.

---

## Aggregates

### `WorkingCalendar` — required by every scope

The foundation. Owns the weekend pattern and the holiday list, and answers `WorkingDaysBetween(from, to)`,
which both `REQ-ATT-0003` and `REQ-ATT-0013` depend on.

- Root: `WorkingCalendar` — `ITenantOwnedEntity`, `ICompanyOwnedEntity`, `IAuditableEntity`
- Child: `CalendarHoliday` — dated, named, maintainable
- **Weekend pattern is data.** A `DayOfWeek` set, not a constant. Fri/Sat, Sat/Sun and Thu/Fri all occur in
  this product's target regions, and `ADR-024`'s locale handling already contemplates that spread.
- **Branch ownership open** (`OD-ATT-0011`). If branch-owned, one calendar per branch and the company-level
  one becomes a default; if not, one per company.

**Not append-only.** A holiday list is maintained — public holidays get moved by decree more often than
anyone would like. Its changes are audited, not frozen.

### `AttendanceRecord` — scope A and C

The atom, whose grain is `OD-ATT-0003`. Under this package's opinion (daily records) it is one employee,
one date, the quantities observed.

- `ITenantOwnedEntity`, `ICompanyOwnedEntity`, `IAuditableEntity`, **`IBranchOwnedEntity`** — RULED present
- Append-only-ness per the table above
- Quantities only: worked, overtime by tier (per `OD-ATT-0008`), absence paid and unpaid. **No money**
  (`DEC-ATT-0004`)
- `EmployeeId` held as a value; the employment-window check (`REQ-ATT-0006`) runs against `IEmployeeRoster`
  **at write time**, not as a constraint

**Why the employment-window check cannot be a database constraint:** the roster is an HR-owned contract and
the dates live in HR's tables. Reaching them directly would be the module-assembly coupling `ADR-012`
forbids — the shared Tenant DB makes it *possible*, which is exactly why the rule has to be deliberate.

### `AttendancePeriod` — every scope

Holds the open/closed state `REQ-ATT-0018` requires and that `OD-ATT-0010` may make a precondition of
payroll calculation.

- Mutable whole life — status transitions, audit, `ClosedUtc`, `ClosedBy` — exactly like `PayrollRun`
- Whether it aligns 1:1 with the fiscal period (the `OD-PAY-0002` shape) or stands independent sits inside
  `OD-ATT-0009`'s per-period-versus-date-range question
- The cost is real either way: **aligned** means Attendance inherits GL's period concept wholesale;
  **independent** means a payroll run can straddle two attendance periods, which is the straddle
  `OD-PAY-0002` refused to let a caller express

### `LeaveType` — scope B and C

The `PayElement` precedent applied exactly: **a configurable catalog with a closed behaviour enum.**
Administrators define the types; the code defines the small set of things a type can *do*.

- `ITenantOwnedEntity`, `ICompanyOwnedEntity`, `IAuditableEntity`; **not** branch-owned — a catalog is
  company policy, following `Department`'s asserted classification
- Immutable code from creation, following `Account` and `PayElement`
- **`LeaveBehaviour` membership is `OD-ATT-0005` plus `OD-ATT-0006`.** `DEC-PAY-0002`'s discipline applies
  without exception: **a behaviour whose input does not exist must not be declared.** If accrual is deferred,
  `Accruing` is not a member on day one — declaring it would be `PayElementBehaviour`'s `OvertimeMultiple`
  mistake in a fresh costume.

### `LeaveBalance` — scope B and C

Whether this is an aggregate at all is `OD-ATT-0006`.

- **Administered** (this package's opinion): a small entity — employee, type, entitlement, consumed —
  maintained by an administrator. Mutable, audited.
- **Accrued**: a dated ledger of accrual events plus a projection, with carry-over caps, expiry and
  seniority tiers. `PayrollCalculator`-weight work, and a second calculation engine in the product.

**An administered balance is a strict subset of an accrued one**, so accrual can be added later without
invalidating stored data. The reverse is not true — which is what makes the deferral safe and the
commitment not.

### `LeaveRequest` — scope B and C

The Workflow the Glossary uses as its example.

- Mutable through its lifecycle (submitted → approved/rejected → cancelled), then settled
- Consumes **working days** from `WorkingCalendar` (`REQ-ATT-0013`), never calendar days
- Approver per `OD-ATT-0007` — and under the department-manager finding that path is
  `Employee.DepartmentId` → `DepartmentManager`, **indirect and with reachable holes**
- Decrements `LeaveBalance` on approval only (`REQ-ATT-0015`)

**Approval is a named-action POST with its own permission**, following `Payroll.Run.Approve`. No
`PUT {status: "approved"}` — `OD-PAY-0009`'s reasoning was that the most sensitive act in a module must not
arrive through the same door as an ordinary edit, and approving leave is that act here.

---

## What is deliberately *not* an aggregate

**No `Shift` or `Roster`.** Scheduling — who is *supposed* to work when — is a distinct problem from
recording what happened, and nothing in the authority base asks for it. Named as an absence because
attendance and shift scheduling are sold together often enough that the exclusion should be visible rather
than assumed.

**No `Punch` or `ClockEvent`**, under this package's `OD-ATT-0003` opinion. If clock events are ruled in,
they become the atom and `AttendanceRecord` becomes a derivation — a **different model, not an addition**,
which is why `OD-ATT-0003` must be ruled before any table is written.

**No `Device`.** `OD-ATT-0016` proposes excluding hardware integration explicitly.

---

## The contract Payroll consumes

`IAttendanceSummary` (name provisional) in an `SSAS.Attendance.Contracts` assembly, following
`SSAS.HR.Contracts` and `SSAS.GL.Contracts` exactly. Its shape is `OD-ATT-0009`; that it is a **summary and
not a feed** is settled (`DEC-ATT-0002`).

It carries an **inspect** method returning a modelled outcome in a closed enum — the
`InspectPostingWindowAsync` precedent — so "that period is not closed yet" reaches the caller as a value it
must handle rather than an exception it may not catch.

**`SSAS.GL.Contracts` had to be recreated by its consumer** because it did not exist when Payroll needed it.
Attendance creates its own contract assembly, in the same commit that records the follow-up obligation, so
that does not happen a second time.

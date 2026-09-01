# FP-013 — Business rules

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

**`Business-Rules.md` contains no `BR-ATT` rule.** "Attendance" appears in it exactly once, under
*"Future Modules — Business Rules for these modules will be added in future releases."*

So every rule below is a **proposal for that file**, not a citation from it. Adding them is the same
governing-document edit as `OD-ATT-0002`'s `REQ-ATT` prefix, and it is the same owner decision.

Scope: `A` attendance, `B` leave, `*` both.

---

## Proposed rules

| ID | Rule | Scope | Basis |
|---|---|---|---|
| `BR-ATT-0001` | A working calendar's weekend pattern is **configuration, not a constant**. No code path may assume Saturday and Sunday | `*` | `ADR-024`'s locale spread; the product's target regions include Fri/Sat and Thu/Fri weekends |
| `BR-ATT-0002` | Leave and absence consume **working days** as defined by the calendar in force, never calendar days | `B` | proposed |
| `BR-ATT-0003` | The number of days a leave request consumed is **fixed at the moment of decision** and does not change if the calendar is later amended | `B` | derived from `PayrollRunLine`'s append-only reasoning — a settled figure stays settled |
| `BR-ATT-0004` | Attendance may not be recorded for a date outside an employee's employment window | `A` | `BR-HR-0004`, read per `OD-PAY-0010` |
| `BR-ATT-0005` | Records already made remain readable and reportable after termination; what is barred is **new** recording | `*` | `BR-HR-0004` as read by `OD-PAY-0010` — the rule bars new obligations, not the settlement of existing ones |
| `BR-ATT-0006` | A closed attendance period does not change. A correction is a new, dated record | `*` | `ADR-014`; **RULED** — adjustments, never edits |
| `BR-ATT-0007` | An employee may not approve their own leave request | `B` | `OD-PAY-0009`'s separation-of-duty reading, applied |
| `BR-ATT-0008` | Attendance records **quantities only**. No rate, amount or currency is recorded, stored or returned | `*` | `ADR-027 d1`/`d2` module boundary; `DEC-ATT-0004` |
| `BR-ATT-0009` | A leave type is **deactivated, never deleted**. Requests referencing a deactivated type remain intact | `B` | the `PayElement` and `Account` precedent |
| `BR-ATT-0010` | A leave type's code is immutable from creation | `B` | the `Account` and `PayElement` precedent |
| `BR-ATT-0011` | Payroll consumes attendance for a period **only through the published contract**, never by reading Attendance tables | `*` | `ADR-012`; `DEC-ATT-0002` |
| `BR-ATT-0012` | Attendance never writes to HR. Employee facts are read through `IEmployeeRoster` and flow one way | `*` | `DEC-PAY-0014` symmetry; `DEC-ATT-0003` |

---

## Rules that would be needed, and cannot be written yet

**Accrual rules** (`OD-ATT-0006`) — entitlement per period of service, pro-ration on hire and termination,
carry-over caps, expiry, negative balances, treatment on termination. That is six or more rules, and every
one of them is jurisdictional. Writing them before the ruling would state policy the owner has not set.

**Approval routing rules** (`OD-ATT-0007`) — who approves when a department has no manager, who approves the
department manager, what happens when the manager is terminated. `ManagerNotAssigned` and `ManagerTerminated`
are **both modelled errors in HR today**, so these are reachable states rather than theoretical ones.

**Overtime threshold rules** (`OD-ATT-0008`) — only if overtime is computed rather than recorded. Daily
threshold, weekly threshold, holiday treatment, and the tier boundaries.

**Branch rules** (`OD-ATT-0011`) — whether a supervisor may record attendance for an employee at another
branch, and what happens to a period's records when an employee transfers branch mid-period.
`IBranchTransferScope` exists because that transfer is a real modelled event, so the question is live.

---

## The interaction that needs stating plainly

**`BR-ATT-0003` and `BR-ATT-0006` are the same instinct** — a figure that has been settled stays settled,
even when the inputs that produced it later change. It is `PayrollRunLine`'s append-only reasoning applied to
two different facts.

**`BR-ATT-0006` is now RULED.** Corrections are new adjustment records, never edits, so the rule stands as written. The paragraph below is kept as the reasoning that produced it. Had the owner ruled
that periods reopen, `BR-ATT-0006` is not the rule — and, per `DEC-ATT-0009`, the records cannot be
append-only either, so the whole immutability story changes shape.

**`BR-ATT-0008` deserves a note on enforcement.** It is stated as a rule, but it is best enforced
mechanically: `TS-ATT-0027` asserts that **no column in any Attendance table is `decimal(19,4)`**. A rule
that a test can check is a rule; a rule only a reviewer can check is a hope.

⚠⚠⚠ **CORRECTED 2026-09-01, AND THIS PARAGRAPH IS THE SHARPEST INSTANCE IN THE SWEEP: `TS-ATT-0027` RETURNS
ZERO FILES IN `tests/`, IN A SENTENCE WHOSE NEXT CLAUSE SAYS A RULE ONLY A REVIEWER CAN CHECK IS A HOPE.**
⚠ **AND THE RULE IS NOT A HOPE — THE TEST EXISTS UNDER ANOTHER NAME:**
`AttendanceSchemaSqlServerTests.No_attendance_column_uses_the_money_type_and_every_quantity_is_decimal_9_2`
(`tests/Integration.Tests/AttendanceSchemaSqlServerTests.cs:62`), which counts `sys.columns` for
`precision = 19 AND scale = 4` under `tenant.Attendance%` and asserts zero. **Its comment carries this
paragraph's closing sentence verbatim** — the test was written from this text, and only the identifier was
never carried across.

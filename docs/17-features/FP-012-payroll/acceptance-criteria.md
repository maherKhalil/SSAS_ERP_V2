# FP-012 — Acceptance Criteria

`AC-PAY-####`, traced to the requirement each verifies. Criteria marked **conditional** exist only under a
particular ruling and are labelled with it, so that a ruling which removes them removes them cleanly rather
than leaving orphans.

---

## Compensation

| ID | Criterion | Verifies |
|---|---|---|
| `AC-PAY-0001` | Creating a compensation record for an employee stores it with its effective date and does not alter any prior record. | `REQ-PAY-0001` |
| `AC-PAY-0002` | The compensation in force on a date is the record with the greatest effective date not after it. | `REQ-PAY-0001` |
| `AC-PAY-0003` | No **employee** compensation value is readable through any HR endpoint or stored on any HR table. **The salary band is the ruled exception — see the note below.** | `REQ-PAY-0002` |
| `AC-PAY-0004` | An amount outside the employee's salary grade band is accepted and recorded, and the out-of-band condition is surfaced to the caller. | `REQ-PAY-0006` — **`OD-PAY-0004` RULED opt. 1** — informational |
| `AC-PAY-0005` | Compensation granted in one company is not readable from another. | `REQ-PAY-0002`, `OD-PAY-0005` |

## Pay elements

| ID | Criterion | Verifies |
|---|---|---|
| `AC-PAY-0006` | A pay element is created with a caller-supplied code; no code is generated. | `REQ-PAY-0003`, `DEC-PAY-0011` |
| `AC-PAY-0007` | An element's code cannot be changed after creation, and a request supplying one is refused. | `REQ-PAY-0003` |
| `AC-PAY-0008` | Two elements in the same company cannot share a code; the same code is free in another company. | `REQ-PAY-0003` |
| `AC-PAY-0009` | Elements are evaluated in ascending calculation order, and a line records the order used. | `REQ-PAY-0004` |

## Runs

| ID | Criterion | Verifies |
|---|---|---|
| `AC-PAY-0010` | A run is created for one company and one period, and includes every employee employed for at least one day of it. | `REQ-PAY-0007`, `REQ-PAY-0008` |
| `AC-PAY-0011` | An employee terminated during the period is included. | `REQ-PAY-0008` — **`OD-PAY-0010` RULED opt. 1** — include |
| `AC-PAY-0012` | An employee terminated before the period begins is not included. | `REQ-PAY-0008` |
| `AC-PAY-0013` | Calculating produces one line per applicable element per included employee, and a net amount. | `REQ-PAY-0009` |
| `AC-PAY-0014` | Recalculating replaces the previous line set entirely. | `REQ-PAY-0012` |
| `AC-PAY-0015` | A run cannot be approved without being calculated, nor posted without being approved. | `REQ-PAY-0010` |
| `AC-PAY-0016` | Approval is refused to a caller holding every other payroll permission but not `Payroll.Runs.Approve`. | `REQ-PAY-0011` |
| `AC-PAY-0017` | A posted run cannot be recalculated, edited, or re-approved. | `REQ-PAY-0012`, `REQ-PAY-0013` |
| `AC-PAY-0018` | Every run retains who calculated, approved and posted it, and when; none of it is subsequently altered. | `REQ-PAY-0014` |

## Posting

| ID | Criterion | Verifies |
|---|---|---|
| `AC-PAY-0019` | Posting an approved run creates exactly one journal in GL for the company, in the fiscal period containing the pay date. | `REQ-PAY-0015` |
| `AC-PAY-0020` | The created journal balances: total debits equal total credits. | `REQ-PAY-0015`, `BR-PAY-0006` |
| `AC-PAY-0021` | A run containing an element with no account mapping cannot be approved, and the response names the element. | `REQ-PAY-0005` |
| `AC-PAY-0022` | A run whose pay date falls in a closed fiscal period cannot be approved, and the response names the period. | `REQ-PAY-0016` — **`OD-PAY-0014` RULED opt. 1** — refuse at approval |
| `AC-PAY-0023` | Closing a fiscal period is not reversed by any payroll operation. | `REQ-PAY-0016`, `OD-PAY-0014` |
| `AC-PAY-0024` | Correcting a posted run produces a reversing journal and a second run; the original run and journal are unchanged. | `REQ-PAY-0013` |
| `AC-PAY-0025` | No Payroll assembly references a GL assembly, and no GL assembly references a Payroll assembly. | `REQ-PAY-0017`, `ADR-012` |

## Reading

| ID | Criterion | Verifies |
|---|---|---|
| `AC-PAY-0026` | A payslip returns the stored lines for one employee in one run, and the lines sum exactly to the stated total. | `REQ-PAY-0018`, `OD-PAY-0008` |
| `AC-PAY-0027` | A caller with every HR permission and no payroll permission can read no compensation and no payslip. | `REQ-PAY-0018`, `BR-PAY-0010` |
| `AC-PAY-0028` | A read scope cannot be supplied by the caller; a request attempting to widen its own scope is refused. | `DEC-PAY-0006` |

## Persistence and cutover

| ID | Criterion | Verifies |
|---|---|---|
| `AC-PAY-0029` | **Every tenant-owned payroll table** appears in the E3 cutover manifest, and a tenant cutover carries payroll data. **See the note below on the count this criterion used to carry.** | `DEC-PAY-0010` |
| `AC-PAY-0030` | Every persisted payroll string column is `nvarchar`, and every monetary column is `decimal(19,4)`. | `DEC-PAY-0004`, `DEC-PAY-0007` |
| `AC-PAY-0031` | No foreign key crosses from a payroll table to a Platform-database table. | `DEC-PAY-0008` |

---

## What is deliberately unverifiable here

**No criterion asserts a tax or statutory amount** — none is authored, and inventing one would create a
test that encodes a guess as a requirement.

**No criterion asserts an attendance-derived value** (`DEC-PAY-0002`).

**`OD-PAY-0008` is RULED (option 1),** so `AC-PAY-0026` — the lines sum to the total — is now a criterion
that must pass rather than one whose passability depended on a ruling.

---

## ⚠ `AC-PAY-0029` carried a count, and the count had gone stale (corrected 2026-08-31, architect)

**It read *“All **five** payroll tables appear in the E3 cutover manifest”*. The manifest's expected list
carries **seven** — `EmployeeCompensation`, `PayElement`, `PayElementAssignment`, `PayrollPeriod`,
`PayrollRun`, `PayrollRunDraftLine`, `PayrollRunLine` — and `CutoverManifestArchitectureTests` says so in
its own comment: *“SEVEN from Payroll (FP-012)”*.**

⚠ **The property held and the number did not**, which is the bad failure mode: a reader auditing the
criterion against the manifest finds a mismatch **and cannot tell which side is wrong**. Nothing was
violated; the package simply grew two tables past a number written when it had five.

⚠⚠ **And the fix is not to write *seven*, because seven rots the same way.** `DEC-PAY-0010` — the decision
this criterion implements — is stated as a **property**: *“Every tenant-owned entity joins the E3
manifest.”* **The criterion was a COUNTED form of a UNIVERSAL ruling, and the count is the only part that
could go out of date.** The property form is restored, so the criterion now survives the eighth table.
**The guard was always count-free and is unaffected: it asserts the composed count minus the excluded set,
so a new table that forgets the test moves the left side and the assertion fails.**

**Same correction as `AC-EMP-0047` the same day, in the other shape: that one was an absence written wider
than its ruling, this one a count written narrower than its ruling. Both were settleable from the decision
the criterion itself cites.**

**`TS-PAY-0028` carried the same number and is corrected with it — the count was in two places, so it was
already going stale in one.**

---

## ⚠ `AC-PAY-0003` clause 2 said more than it meant (corrected 2026-08-31, architect)

**It read *"No compensation value is … stored on any HR table"*, and as literally worded it is FALSE.**

**Measured over the mechanism rather than by name: the entire HR domain declares `decimal` in exactly one
file. `SalaryBand.MinimumAmount`, `.MidpointAmount` and `.MaximumAmount` are the only monetary properties
in HR, owned by `SalaryGrade` through its band. No `Amount`, `Salary`, `Pay`, `Rate` or `Wage` property
exists on any other HR type.**

⚠⚠ **So HR does store amounts, and it is supposed to. `DEC-POS-0023` draws the line the criterion meant:
a band is a STRUCTURAL definition of what a JOB pays, not a record of what a PERSON is paid.** The clause
is corrected to say *employee* compensation, which is what it has always been enforcing.

**Why this matters beyond the wording: a criterion that is false as read invites two bad outcomes** — a
test written to its letter fails against correct code and gets "fixed" by deleting the salary band, or the
clause is quietly ignored and stops guarding anything at all.

⚠ **And the guard behind it was narrower still.** `No_position_command_carries_a_compensation_value_or_headcount`
inspects POSITION MUTATION COMMANDS only: a future `Employee.BaseSalary` property would pass it — wrong
type, wrong package, not a command. **The mechanism-shaped guard, walking the HR domain's declared
properties and allowing the band by name with `DEC-POS-0023` cited, is the one that catches what this
criterion exists to prevent.**

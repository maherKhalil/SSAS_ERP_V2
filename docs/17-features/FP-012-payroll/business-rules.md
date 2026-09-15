# FP-012 — Business Rules

**No `BR-PAY-####` rule exists anywhere in the product today.** `Business-Rules.md` lists Payroll under
*"Future Modules — Business Rules for the following modules will be added in future releases."*

So every rule below is **proposed for ratification into the master rule set**, exactly as `REQ-PAY-####` is
proposed into the catalog. Each is marked with what it rests on.

The one authored rule that already binds Payroll is not restated as a `BR-PAY`, because it already exists:

> **`BR-PLT-0103`** — Sensitive operations require elevated permissions. Examples: Delete, Reverse Journal,
> Close Fiscal Year, **Payroll Processing**.

---

## Proposed rules

### `BR-PAY-0001` — A payroll run belongs to one company and one period
No run spans companies or periods. **Rests on:** `Employee`, journals and fiscal calendars are all
company-owned; a run that spanned companies could not produce a single balanced journal.

### `BR-PAY-0002` — Compensation is recorded with an effective date and is never overwritten
The amount in force on a given date is derived from the record effective on that date. **Rests on:**
`REQ-PAY-0001`; **`OD-PAY-0003` RULED for dated history**, so this rule stands.

### `BR-PAY-0003` — An employee employed for any part of a period is included in that period's run
Including an employee terminated during the period. **Rests on:** an interpretation of `BR-HR-0004` —
final pay discharges an obligation already incurred rather than assigning a new transaction. **`OD-PAY-0010` RULED: include them.** The reading is recorded on the ruling — `BR-HR-0004` bars new
obligations, not the settlement of obligations already incurred.

### `BR-PAY-0004` — Pay elements are evaluated in a defined order
An element computed from another is evaluated after it. **Rests on:** `REQ-PAY-0004`; `OD-PAY-0007`.

### `BR-PAY-0005` — Every pay element in a run must map to an account before the run can be approved
**Rests on:** a journal cannot be composed from an unmapped element. Enforced at approval rather than
posting so the failure surfaces before anyone treats the run as final (`OD-PAY-0012`).

### `BR-PAY-0006` — A payroll journal balances
Total debits equal total credits. **Rests on:** `BR-GL-0001` — inherited, not new. Stated here because it
constrains the *mapping*: the element set must be mappable to a balanced pair of sides.

### `BR-PAY-0007` — A run whose target fiscal period is closed cannot be approved
**Rests on:** `BR-GL-0003`. The check sits at approval; period closure is never reopened automatically
(`OD-PAY-0014`).

### `BR-PAY-0008` — A posted run is immutable
Correction is by reversal and a new run. **Rests on:** posted journals are append-only — inherited from
GL, not a payroll invention (`DEC-PAY-0012`).

### `BR-PAY-0009` — Approving a run requires a permission distinct from calculating it
**Rests on:** `BR-PLT-0103`, and the `GL.Drafts.Manage` / `GL.Journals.Post` precedent where preparation
and authorization were deliberately separated so they could be different people.

### `BR-PAY-0010` — Individual compensation is readable only under a payroll-specific permission
No HR permission grants sight of an individual's pay. **Rests on:** `DEC-POS-0018` separated a permission
for *structural* pay bands; individual compensation is personal data and the precedent applies with more
force (`OD-PAY-0016`).

### `BR-PAY-0011` — A payroll run record is append-only
What was attempted, by whom, over what scope, with what outcome — never updated afterwards. **Rests on:**
`EmployeeImportRun` / `EmployeeExportRun`; `BR-PLT-0103` requires payroll to be answerable after the fact.

### `BR-PAY-0012` — All amounts are in the company's base currency
**Rests on:** `OD-GL-0002`'s closure and `ADR-027` decision 2 (`DEC-PAY-0003`).

---

## Rules that would be needed and cannot be written

**Statutory deduction rules.** A real payroll needs rules for income tax, social insurance and any
mandated contribution. **None can be drafted**: no jurisdiction is named anywhere in the specification, and
these are legal facts rather than product decisions. Recorded here so their absence is visible in the rule
set rather than discovered during a build.

**Attendance-derived rules.** Overtime thresholds, absence deduction, lateness — all require an attendance
register that does not exist (`DEC-PAY-0002`).

**Minimum-wage or ceiling constraints.** Jurisdictional, same problem as statutory deductions.

### `BR-PAY-0013` — Each pay line is rounded to two decimal places, half away from zero, and a run total is
the sum of its rounded lines
**Rests on:** `OD-PAY-0008`, RULED 2026-08-24. The protected invariant is that **the payslip adds up** —
under this rule it holds by construction rather than by recomputation.

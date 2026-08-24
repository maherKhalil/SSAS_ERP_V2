# FP-012 — Decisions

Two registers. **`DEC-PAY-####`** are settled or proposed by this package and do not need the owner.
**`OD-PAY-####`** are **OWNER-DECISION-REQUIRED** and each one blocks the build prompt.

The distinction is deliberate: a decision this package can make from existing authority is made here and
traced; a decision that needs the owner is raised with options and consequences and **is not invented**.

---

## Register 1 — `DEC-PAY-####` (settled or proposed here)

| ID | Decision | Status |
|---|---|---|
| `DEC-PAY-0001` | Payroll is pulled forward from Roadmap V2 | **CLOSED** — owner instruction 2026-08-24 |
| `DEC-PAY-0002` | Attendance-driven components cannot exist in FP-012 | **SETTLED-BY-ABSENCE** |
| `DEC-PAY-0003` | Single currency; multi-currency is not pulled | **SETTLED-BY-PRECEDENT** — `OD-GL-0002` |
| `DEC-PAY-0004` | Money is `decimal(19,4)` | **SETTLED** — `ADR-027` |
| `DEC-PAY-0005` | Payroll run records are append-only | **SETTLED-BY-PRECEDENT** |
| `DEC-PAY-0006` | Read scopes are unforgeable | **SETTLED-BY-PRECEDENT** |
| `DEC-PAY-0007` | Persisted strings are `nvarchar` | **SETTLED** |
| `DEC-PAY-0008` | No cross-database foreign key | **SETTLED** |
| `DEC-PAY-0009` | `RowVersion` on every mutable aggregate | **SETTLED** |
| `DEC-PAY-0010` | Every tenant-owned entity joins the E3 manifest | **SETTLED** |
| `DEC-PAY-0011` | Codes are never generated | **SETTLED-BY-PRECEDENT** — `DEC-POS-0024` |
| `DEC-PAY-0012` | A payroll correction is a reversal, never an edit | **SETTLED** — GL append-only |
| `DEC-PAY-0013` | No document store exists; a payslip cannot be a stored file | **SETTLED-BY-ABSENCE** — FP-010 closed to V5 |
| `DEC-PAY-0014` | Payroll never writes to HR | **PROPOSED** |
| `DEC-PAY-0015` | `DEC-POS-0023` is not reopened | **SETTLED** |

### `DEC-PAY-0001` — Payroll is pulled forward. **CLOSED.**

The Roadmap places Payroll in Version 2, first line, above Attendance, Recruitment and Performance. The
owner's **2026-08-24** instruction pulls it forward.

**Recorded as a closed sequencing decision, not raised as a question.** The Roadmap records a plan; the
owner sets scope order, and a package does not ask permission for an instruction it has already been given.
What the pull-forward creates is a technical boundary that sequencing cannot dissolve — `DEC-PAY-0002`.

### `DEC-PAY-0002` — Attendance-driven components cannot exist here. **SETTLED-BY-ABSENCE.**

Attendance is a separate Roadmap V2 line and is **unbuilt**. Overtime computed from worked hours, absence
deductions derived from a register, shift differentials and lateness penalties each require a source of
truth this product does not have.

**This is not a scoping preference; it is a missing input.** A V1 payroll may accept a *manually entered*
quantity or amount for such an element — that is data entry, not derivation — but it cannot compute one,
and no requirement here may imply that it does. `OD-PAY-0001` asks the owner to confirm the element set
*inside* this boundary.

The failure mode being prevented is specific: a requirement written as "the system calculates overtime"
reads as buildable, passes review, and is discovered to be unimplementable only when someone looks for the
hours.

### `DEC-PAY-0003` — Single currency. **SETTLED-BY-PRECEDENT.**

`OD-GL-0002`'s closure made V1 single-currency, and `ADR-027` decision 2 projects the company's
`BaseCurrencyCode` on read rather than storing it per row. Payroll follows exactly.

**A payroll denominated in a currency other than the company's base currency is the multi-currency trigger,
and pulling that trigger is not this package's to do.** If the owner needs it, it is a separate decision
with consequences reaching into GL, reporting and every stored amount in the product.

### `DEC-PAY-0004` — Money is `decimal(19,4)`. **SETTLED.**

`ADR-027`. Every monetary column in this package, without exception. Note this interacts with rounding
(`OD-PAY-0008`): four decimal places is the *storage* precision and says nothing about what a payslip
should round to.

### `DEC-PAY-0005` — Run records are append-only. **SETTLED-BY-PRECEDENT.**

`EmployeeImportRun` / `EmployeeExportRun` established the shape: a run record states what was attempted,
under whose authority, over what scope, with what outcome, and is never updated afterwards. A payroll run
is a stronger case than an import — it is the evidentiary record of what people were paid.

### `DEC-PAY-0006` — Read scopes are unforgeable. **SETTLED-BY-PRECEDENT.**

A scope is constructed by a resolver from the caller's grants and never accepted from the caller. Payroll
makes this more load-bearing than anywhere else in the product: a forgeable pay scope is a compensation
data breach, not an authorization inconvenience.

### `DEC-PAY-0007`–`DEC-PAY-0011` — the pattern stack. **SETTLED.**

`nvarchar` for persisted application strings; no cross-database foreign key between Platform and Tenant
databases; `RowVersion` on every mutable aggregate; every tenant-owned entity carries `ITenantOwnedEntity`
so `TenantCutoverCopyPlan.Build` finds it — **a type without it is silently absent from cutover**, which
FP-011 proved is a live failure mode rather than a theoretical one; and codes are never generated
(`DEC-POS-0024`, `DEC-DEP-0007`, `DEC-EMP-0011`).

### `DEC-PAY-0012` — A correction is a reversal. **SETTLED.**

Posted journals are append-only, and a payroll posting is a journal. Correcting a paid run therefore means
**reversing and re-posting**, never editing. This constrains `OD-PAY-0011` rather than answering it: the
GL-side behaviour is fixed, but what the *payroll* run record does about it is still open.

### `DEC-PAY-0013` — A payslip cannot be a stored document. **SETTLED-BY-ABSENCE.**

FP-010 was closed to V5 — **the product has no document store**. Whatever a payslip is, it cannot in FP-012
be a generated PDF held somewhere. `OD-PAY-0015` asks what it should be instead.

### `DEC-PAY-0014` — Payroll never writes to HR. **PROPOSED.**

Payroll reads employee identity, employment status and organizational placement from HR; it writes nothing
back. No pay value lands on an HR record, and no payroll outcome changes employment state.

*Why it is proposed rather than settled:* nothing states it, but its opposite would silently reverse
`DEC-POS-0023` by putting compensation into HR through a side door.

### `DEC-PAY-0015` — `DEC-POS-0023` is not reopened. **SETTLED.**

HR holds salary **structure** (grade bands, informational) and no compensation value. That remains exactly
true after FP-012. This package adds the actual-pay record **on the Payroll side of the line**, which is
what `DEC-POS-0023` said would happen — see `OD-PAY-0003` for where it lives.

---

## Register 2 — `OD-PAY-####` — **OWNER-DECISION-REQUIRED**

Eighteen decisions. Each blocks the build prompt.

| ID | Question | Recommendation |
|---|---|---|
| `OD-PAY-0001` | Which pay elements are in V1? | Fixed salary + recurring allowances + recurring deductions |
| `OD-PAY-0002` | What is the pay frequency and schedule model? | Monthly, company-scoped |
| `OD-PAY-0003` | What shape is the employee compensation record? | Dated assignment history |
| `OD-PAY-0004` | Is compensation validated against the SalaryGrade band? | Informational — warn, do not refuse |
| `OD-PAY-0005` | Is the compensation record tenant- or company-owned? | Company-owned |
| `OD-PAY-0006` | Closed pay-element set, or tenant-configurable? | Tenant-configurable within fixed *behaviours* |
| `OD-PAY-0007` | Calculation ordering and proration | Explicit sequence; calendar-day proration |
| `OD-PAY-0008` | **Rounding — a money-truth question** | Per-element half-away-from-zero to 2dp |
| `OD-PAY-0009` | Run lifecycle states, and who approves | Draft → Calculated → Approved → Posted; separate permission |
| `OD-PAY-0010` | Terminated employees and final pay (`BR-HR-0004`) | Include if employed any day in the period |
| `OD-PAY-0011` | Rerun and correction semantics | Recalculate freely before Approved; reverse-and-rerun after |
| `OD-PAY-0012` | GL account mapping — configured where, validated how | Per pay element, per company; validated at approval |
| `OD-PAY-0013` | **The posting mechanism (`ADR-012`)** | Promoted `SSAS.GL.Contracts`, shaped here |
| `OD-PAY-0014` | Posting into a closed period | Refuse at approval; never auto-reopen |
| `OD-PAY-0015` | What is a payslip? | A read projection, not a document |
| `OD-PAY-0016` | The pay-data read permission and its scope | Its own permission, separate from HR reads |
| `OD-PAY-0017` | Module home | Its own `src/Modules/Payroll/` tree |
| `OD-PAY-0018` | Retroactive pay, advances, loans | **DEFER** |

---

### `OD-PAY-0001` — Which pay elements are in V1?

Nothing authored says. `DEC-PAY-0002` fixes the outer boundary; inside it the owner chooses.

| | Option | Consequence |
|---|---|---|
| 1 | **Fixed salary only** | Smallest truthful payroll. Most tenants cannot run their real payroll on it |
| 2 | **Fixed salary + recurring allowances and deductions** *(recommended)* | Covers the common case; every element is a standing instruction, none derived |
| 3 | Option 2 + manually entered one-off elements | Adds per-run data entry and an input-validation surface |
| 4 | Anything attendance-driven | **Not available** — `DEC-PAY-0002` |

**Recommendation: option 2**, with option 3 as the natural first extension. It is the largest set that
requires no input the product lacks.

### `OD-PAY-0002` — Pay frequency and schedule model

| | Option | Consequence |
|---|---|---|
| 1 | **Monthly only, company-scoped** *(recommended)* | One period shape; aligns naturally with GL fiscal periods |
| 2 | Monthly + semi-monthly + weekly, company-scoped | Multiplies period arithmetic and proration rules |
| 3 | Frequency per employee group | Most flexible; makes "the payroll run" ambiguous — the run must then name its population |

**Recommendation: option 1.** Frequency is easy to add and very hard to remove, and option 3 changes what a
*run* means, which touches the lifecycle, the posting contract and every read.

**Sub-question the owner must also settle:** does a payroll period have to align with a GL fiscal period?
They are defined independently today (`FiscalYear` is company-owned with a validated contiguous partition),
and a payroll period that straddles two fiscal periods makes `OD-PAY-0014` materially harder.

### `OD-PAY-0003` — The employee compensation record **(the `DEC-POS-0023` slot)**

**This is the package's central data question.** `DEC-POS-0023` created a deliberate vacancy — HR holds no
compensation value — and FP-012 fills it. The shape chosen here determines what every later payroll
question can even ask.

| | Option | Consequence |
|---|---|---|
| 1 | **Current value only** — one amount per employee, overwritten on change | Simplest. **Destroys history**: a re-run of last month's payroll silently uses today's salary, and no one can answer "what was she paid in March and why" |
| 2 | **Dated assignment history** — a series of `(employee, effectiveFrom, amount, elements)` records, the current one derived by date *(recommended)* | Reproducible runs; retroactive change becomes expressible later; matches the append-only instinct of the codebase |
| 3 | Value on the run only — no standing record; amounts entered per run | No master data to protect, but every run is manual data entry and nothing can be validated against anything |

**Recommendation: option 2.** A payroll that cannot reproduce a past run is not auditable, and
`BR-PLT-0103` treats payroll processing as sensitive precisely because it must be answerable after the
fact. Option 1 is cheap now and unrecoverable later: history that was never written cannot be
reconstructed.

*Note the interaction:* option 2 is also what makes `OD-PAY-0018`'s retroactive pay implementable **later
without a migration**, which is why deferring retro is safe under option 2 and expensive under option 1.

### `OD-PAY-0004` — Is compensation validated against the SalaryGrade band?

HR holds `SalaryGrade` bands as **informational structure** (`DEC-POS-0023`, `DEC-POS-0027` — the band is
atomic). Should Payroll refuse an amount outside the employee's grade band?

| | Option | Consequence |
|---|---|---|
| 1 | **Informational — record it, warn, do not refuse** *(recommended)* | Honest to what a band is today. Out-of-band pay is a real business event (retention, acting-up, legacy) |
| 2 | Validated — refuse out-of-band amounts | Makes a band a *control*, which is a change to what `DEC-POS-0027` said it is, and requires an override path immediately |
| 3 | No relationship at all | Loses a cheap, genuinely useful check |

**Recommendation: option 1.** Option 2 promotes an informational structure into a constraint, and every
system that has done that has then needed an override mechanism, an override permission, and an override
audit — three things nobody has asked for.

### `OD-PAY-0005` — Is the compensation record tenant- or company-owned?

| | Option | Consequence |
|---|---|---|
| 1 | **Company-owned** *(recommended)* | Matches `Employee`, matches journals and fiscal calendars; pay scope partitions the way every other sensitive read does |
| 2 | Tenant-owned | Would let one record span companies — but an employee belongs to a company, so this creates a shape nothing else in the product has |

**Recommendation: option 1**, consistent with `ICompanyOwnedEntity` throughout.

### `OD-PAY-0006` — Closed pay-element set, or tenant-configurable?

| | Option | Consequence |
|---|---|---|
| 1 | Closed set defined in code | Predictable and immediately mappable to GL accounts; no tenant can express a local allowance |
| 2 | Fully tenant-configurable | Maximum flexibility; the calculation model becomes user-authored and effectively unbounded |
| 3 | **Tenant-configurable elements, fixed behaviours** *(recommended)* | A tenant defines *elements* (name, code, earning/deduction, taxable-or-not) but each binds to a behaviour the code implements |

**Recommendation: option 3.** It is the shape the rest of the product already uses — `Account`, `Position`
and `SalaryGrade` are all tenant data with code-defined behaviour — and it keeps `OD-PAY-0007`'s
calculation model finite.

### `OD-PAY-0007` — Calculation ordering and proration

Order matters as soon as any element depends on another (a percentage-of-basic allowance, a deduction
capped at a proportion of gross).

| | Option | Consequence |
|---|---|---|
| 1 | **Explicit ordinal on each element, evaluated in sequence** *(recommended)* | Deterministic, inspectable, explainable on a payslip |
| 2 | Dependency graph resolved automatically | Elegant; introduces cycles as a runtime failure and is hard to explain to a payroll officer |
| 3 | Fixed two-phase (earnings, then deductions) | Simplest; cannot express a deduction that feeds another deduction's base |

**Proration** — for a mid-period joiner or leaver — needs its own answer: calendar days, working days, or
a fixed 30-day convention. **Recommendation: calendar days**, because working days require a calendar the
product does not have (that is Attendance again).

### `OD-PAY-0008` — Rounding **(a money-truth question — owner input expected)**

`ADR-027` fixes *storage* at `decimal(19,4)`. It does not say what a person is paid.

| | Option | Consequence |
|---|---|---|
| 1 | **Round each element to 2dp, half away from zero; total is the sum of rounded elements** *(recommended)* | Payslip lines add up to the total exactly — the property a human checks first |
| 2 | Calculate at full precision, round only the net total | Total is more "accurate"; the printed lines **do not sum to it**, which reads as a bug forever |
| 3 | Banker's rounding (half to even) | Statistically unbiased; surprises users and differs from most local payroll convention |

**Recommendation: option 1.** The invariant worth protecting is *the payslip adds up*. Note this is a
jurisdictional question as much as a technical one, and the owner may have a statutory answer that
overrides the recommendation — which is exactly why it is raised rather than assumed.

### `OD-PAY-0009` — Run lifecycle states, and who approves

`BR-PLT-0103` names **Payroll Processing** a sensitive operation requiring elevated permissions, so *some*
elevation is authored. What it attaches to is not.

| | Option | Consequence |
|---|---|---|
| 1 | Draft → Calculated → Posted | Fewer states; nothing separates "the numbers are ready" from "pay these people" |
| 2 | **Draft → Calculated → Approved → Posted** *(recommended)* | Approval is the sensitive act `BR-PLT-0103` points at, and it is where GL posting is authorized |
| 3 | Option 2 + a separate Paid state after disbursement | Truthful about the money leaving; the product has no payment/banking integration, so nothing could set it |

**Recommendation: option 2**, with `Payroll.Runs.Approve` as its own permission — mirroring
`GL.Drafts.Manage` / `GL.Journals.Post`, where preparation and authorization are deliberately different
grants so they can be different people.

### `OD-PAY-0010` — Terminated employees and final pay (`BR-HR-0004`)

`BR-HR-0004`: *a terminated employee cannot be assigned new business transactions.* A final-pay run is, on
its face, exactly that.

| | Option | Consequence |
|---|---|---|
| 1 | **Include anyone employed for at least one day of the period, terminated or not** *(recommended)* | People get paid for work done. Reads `BR-HR-0004` as barring *new* obligations, not settling existing ones |
| 2 | Exclude all terminated employees | Literal reading; **people do not get their final pay**, which is unlawful in most jurisdictions |
| 3 | Include only with an explicit per-employee override | Safe but adds a manual step to an event that happens constantly |

**Recommendation: option 1, with the reading recorded explicitly.** Final pay is the discharge of an
obligation already incurred, not a new transaction. The owner should confirm, because this is an
interpretation of an authored rule rather than a gap in one — and interpreting someone else's rule silently
is precisely what this register exists to prevent.

### `OD-PAY-0011` — Rerun and correction semantics

| | Option | Consequence |
|---|---|---|
| 1 | **Recalculate freely before Approved; after Posted, correct only by reversing and running again** *(recommended)* | Matches `DEC-PAY-0012` and GL's append-only truth exactly |
| 2 | Allow editing a posted run | **Not available** — posted journals are append-only |
| 3 | Supersede: a new run replaces the old, both retained | Expressible, but two runs claiming the same period need a rule for which is authoritative in every read |

**Recommendation: option 1.** Option 2 is excluded by inherited fact rather than preference.

### `OD-PAY-0012` — GL account mapping: configured where, validated how

The posting must turn pay elements into balanced journal lines. Something must say *which account*.

| | Option | Consequence |
|---|---|---|
| 1 | **Per pay element, per company; validated when the run is approved** *(recommended)* | Mapping lives with the element; the account is checked to exist and be active before anything posts |
| 2 | Per pay element, tenant-wide | Simpler; wrong as soon as two companies have different charts — which the tenant-wide chart permits |
| 3 | Chosen by the operator at posting time | Maximum flexibility, zero repeatability, and an audit trail nobody can reconstruct |

**Recommendation: option 1**, with validation at **approval** rather than at posting, so an unmapped
element is caught before anyone believes the run is final.

### `OD-PAY-0013` — The posting mechanism **(`ADR-012`)**

This is **the product's first cross-module integration.** `ADR-012` constrains it absolutely: a promoted
contract or an event — **never an assembly reference from one module to another.**

FP-011's recorded recreate-condition fires here: *"`SSAS.GL.Contracts` returns when Payroll consumes it,
**shaped by its consumer**."* This decision is that shaping.

| | Option | Consequence |
|---|---|---|
| 1 | **Recreate `SSAS.GL.Contracts` with a posting contract Payroll calls synchronously** *(recommended)* | Exactly the recorded condition. Posting failure is immediate and the run can refuse to complete |
| 2 | Domain event; GL subscribes asynchronously | Loosest coupling; the run can report success while the journal silently failed to post — the hardest possible defect to notice |
| 3 | Payroll writes journals directly through GL's repositories | **Excluded by `ADR-012`** |

**Recommendation: option 1.** The deciding property is that a payroll run must not be able to claim it
posted when it did not. Option 2 can be made safe with an outbox and reconciliation, but neither exists,
and building both to avoid a synchronous call is a large cost for coupling that is genuinely one-directional.

**Whatever is chosen, the contract is defined by what Payroll actually needs** — that is the whole content
of "shaped by its consumer", and it is why the type was deleted rather than left empty.

### `OD-PAY-0014` — Posting into a closed period

`BR-GL-0003` prohibits posting into a closed fiscal period. A payroll run approved late will meet exactly
this.

| | Option | Consequence |
|---|---|---|
| 1 | **Refuse at approval, naming the closed period** *(recommended)* | Fails early and legibly, before anyone treats the run as final |
| 2 | Refuse at posting | The run is Approved but unpostable — a state with no good exit |
| 3 | Auto-reopen the period, post, re-close | **Excluded.** Period closure is a control; a subordinate module must never silently defeat it |

**Recommendation: option 1.** Option 3 is listed only to record that it was considered and rejected: an
automated reopen turns `GL.Periods.Close` into a suggestion.

### `OD-PAY-0015` — What is a payslip?

`DEC-PAY-0013`: there is no document store, so a stored PDF is not available.

| | Option | Consequence |
|---|---|---|
| 1 | **A read projection over the run's stored lines** *(recommended)* | Always reproducible from the run record; nothing to store, nothing to go stale |
| 2 | A generated document | **Not available** in FP-012 |
| 3 | An export (CSV) reusing the FP-009 export-run shape | Plausible later; needs its own run record and permission, and is a bigger surface than V1 needs |

**Recommendation: option 1.** Because the run's lines are append-only, a projection over them is
*permanently* faithful — which a rendered document would not be.

### `OD-PAY-0016` — The pay-data read permission and its scope

**Pay data is the most sensitive read surface this product will have.** `DEC-POS-0018` separated
`HR.SalaryGrades.View` from ordinary HR reads when the data was merely *structural*. Individual
compensation is personal, so that precedent applies with more force.

| | Option | Consequence |
|---|---|---|
| 1 | **Its own permission, granted separately from every HR permission; scope resolved, never supplied** *(recommended)* | Someone who can see an employee record cannot thereby see their pay |
| 2 | Reuse an HR employee-read permission | Every HR user becomes a payroll-data reader by accident. **Recommended against** |
| 3 | Option 1 plus a self-service "own payslip only" scope | Genuinely useful; needs an identity→employee link that should be confirmed to exist before being assumed |

**Recommendation: option 1**, with option 3 flagged as the likely immediate follow-up. The owner should
note that option 3 depends on a mapping from the authenticated identity to an employee record — this
package does **not** assert that such a mapping exists today.

### `OD-PAY-0017` — Module home

The sweep found **no Payroll tree anywhere**, so unlike FP-011 there is nothing to adopt.

| | Option | Consequence |
|---|---|---|
| 1 | **`src/Modules/Payroll/SSAS.Payroll.*`, tests in a new `tests/Payroll.Tests`** *(recommended)* | Payroll is an HR-adjacent business capability, not an accounting one. `Solution-Structure.md` treats a module as a capability |
| 2 | Inside `src/Modules/Finance/` beside GL | Payroll posts to GL, but *posting to* a module is not *belonging with* it — by that logic every module is Finance |
| 3 | Inside `src/Modules/HR/` | Payroll reads HR heavily, but `DEC-POS-0023` drew the line between them deliberately, and this erases it |

**Recommendation: option 1.** Note the naming trap FP-011 walked into: the test project name must be
**confirmed against the solution** before it is written into any `InternalsVisibleTo`, not guessed.

### `OD-PAY-0018` — Retroactive pay, advances and loans

| | Option | Consequence |
|---|---|---|
| 1 | **Defer all three** *(recommended)* | Keeps V1 finite. Each is a substantial feature with its own rules |
| 2 | Include retroactive pay | Needs dated compensation history (`OD-PAY-0003` option 2) *and* a recalculation-of-closed-periods policy that collides with `BR-GL-0003` |
| 3 | Include advances and loans | These are *balances carried across periods* — a different kind of object from a pay element, with amortization and outstanding-balance semantics |

**Recommendation: defer, and say so explicitly rather than leaving it unmentioned.** Under `OD-PAY-0003`
option 2 the deferral is cheap: dated compensation history is exactly the substrate retroactive pay needs,
so adding it later requires no migration of existing pay records. **Under `OD-PAY-0003` option 1 the
deferral becomes expensive**, because the history retro would need was never written down.

---

## What is deliberately not decided here

* **Tax, statutory contributions and social insurance.** Not raised as an `OD-PAY` because they are not a
  choice this product can offer — they are jurisdictional requirements with no authored source anywhere in
  the specification. **Any real payroll needs them**, and their absence should be understood as the largest
  gap between this package and a shippable payroll, not as a decision taken.
* **Payment and disbursement.** No banking or payment integration exists; a run can be posted to GL but the
  product cannot move money. This is why `OD-PAY-0009` option 3 has no implementable trigger.
* **Multi-currency.** `DEC-PAY-0003`.
* **Anything attendance-derived.** `DEC-PAY-0002`.

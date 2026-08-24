# FP-012 — Domain Model (RATIFIED)

Shapes below are proposals. Several cannot be fixed until `decisions-approved.md` is ruled, and where that is
true it is said rather than papered over with a plausible default.

---

## Aggregates

### `EmployeeCompensation` — **the `DEC-POS-0023` slot**

`DEC-POS-0023` left a deliberate vacancy: HR holds salary *structure* and no compensation value, because
*"what an individual is paid is Payroll."* This aggregate fills it.

Shape depends entirely on `OD-PAY-0003`. Under the recommended option 2 (dated assignment history):

* `EmployeeCompensationId`
* `EmployeeId` — the HR employee, **by identifier only**, no navigation into HR's aggregates
* `CompanyId` — `ICompanyOwnedEntity` (`OD-PAY-0005`)
* `EffectiveFromUtc`
* `BaseAmount` — `decimal(19,4)` (`ADR-027`)
* recurring element assignments (see `PayElementAssignment`)
* `IAuditableEntity`, `ITenantOwnedEntity`, `RowVersion`

**The amount in force on a date is derived by selecting the record with the greatest `EffectiveFromUtc`
not after that date.** No "current" flag: a maintained flag is derived state that drifts, and this codebase
has refused that shape before (`Account` code immutability, `FiscalYear` contiguity validated as a set).

**Under `OD-PAY-0003` option 1 this aggregate collapses to one mutable row per employee and the history is
gone.** That is not a smaller version of the same design; it is a different one, and every reproducibility
claim in this package depends on which is chosen.

### `PayElement`

A tenant-defined element bound to a product-implemented behaviour (`OD-PAY-0006`, recommended option 3).

* `PayElementId`, `CompanyId`
* `Code` — never generated (`DEC-PAY-0011`), immutable from creation following `Account`'s precedent
* `Name`
* `Kind` — Earning or Deduction
* `Behaviour` — the code-implemented calculation this element binds to
* `CalculationOrder` — explicit ordinal (`BR-PAY-0004`)
* `GlAccountId` — the mapping (`REQ-PAY-0005`), by identifier across the module boundary
* `IsActive`, `IAuditableEntity`, `ITenantOwnedEntity`, `RowVersion`

**`NormalizedCode` / `NormalizedName` shadow properties** if the element list is searchable —
`DEC-POS-0030` records that a value-converted property translates in a projection but *not* in a predicate,
and that HR shipped a department search that threw for every search term. GL wrote them up front rather
than reproducing the failure; Payroll should do the same rather than rediscover it a third time.

### `PayrollRun` — append-only once posted

* `PayrollRunId`, `CompanyId`
* `PeriodStartUtc`, `PeriodEndUtc`, `PayDateUtc`
* `Status` — Draft / Calculated / Approved / Posted (`OD-PAY-0009`)
* `CalculatedBy` / `CalculatedUtc`, `ApprovedBy` / `ApprovedUtc`, `PostedBy` / `PostedUtc`
* `JournalEntryId` — the GL journal this run produced, **by identifier**, no reference to GL's assembly
* `IAuditableEntity`, `ITenantOwnedEntity`, `ICompanyOwnedEntity`, `RowVersion` while mutable

**`IAppendOnlyEntity` applies from Posted onward.** GL solved the same problem with two aggregates —
`JournalDraft` is mutable, `JournalEntry` is append-only, and promotion crosses the boundary. Payroll has a
choice: one aggregate whose mutability depends on status, or GL's two-aggregate split.

> ## ⛔ OPEN AT THE BUILD SITE — the analysis package and the build prompt disagree here
>
> **The analysis package proposed a single aggregate with a status guard.** The build prompt directs a split
> that keeps the append-only guard **structural** (the `OD-GL-0007` lesson) and says to STOP if the package
> contradicts it. **It does contradict it, so this is recorded open rather than decided at the keyboard.**
>
> **The package's proposal is wrong, and the evidence is mechanical.**
> `TenantDbContext.PreventAppendOnlyMutation` refuses `Modified` **or `Deleted`** for any
> `IAppendOnlyEntity`, unconditionally — it has no way to know a run is still Draft. So:
>
> * If `PayrollRunLine : IAppendOnlyEntity`, **recalculation before approval is impossible**, because
>   replacing a line set requires `Deleted`. That directly contradicts `OD-PAY-0011`'s ruling.
> * If it is not, the post-approval guarantee is **behavioural** — a bug in the aggregate defeats it, and
>   the strongest guard in the codebase never engages on the records that say what people were paid.
>
> The package identified the mechanism correctly and drew the wrong conclusion from it: it reasoned
> *"therefore the guard must live in the aggregate"* when the available conclusion was *"therefore the types
> must be split, as GL split them."*
>
> **GL's actual shape, verified in the repository:** two line types —
> `JournalDraftLine : Entity<Guid>, ITenantOwnedEntity` (mutable) and
> `JournalLine : Entity<Guid>, ITenantOwnedEntity, IAppendOnlyEntity` (never touched again). And
> `JournalEntry` never mutates either: `IsReversed` is **projected on read, not stored**, and `Reverse`
> constructs a **new** `JournalEntry` carrying `ReversesJournalEntryId`.
>
> **The complication the prompt's shape meets.** `PayrollRun` must record `PostedUtc` and `JournalEntryId`
> *after* it is Approved. If the run itself became append-only at Approved, that write would be refused. So
> the run cannot be the append-only type — only its **lines** can be.
>
> **Recommended resolution (not applied):** keep `PayrollRun` mutable for its whole life (RowVersion,
> carrying status and audit and the journal identity) and split the lines exactly as GL does — a mutable
> draft line replaced freely while Draft/Calculated, and a separate `PayrollRunLine : IAppendOnlyEntity`
> written once at the Approved transition and never touched again. The payslip projects over the append-only
> type, so the identity objection the package raised does not arise: a payslip only exists after approval.
>
> This satisfies `OD-PAY-0011` and makes the guard structural. **It needs an architect ruling because it
> overrides a ratified package, and because it adds a table the data model does not list.**

### `PayrollRunLine` — append-only

One line per employee per pay element in a calculated run.

* `PayrollRunLineId`, `PayrollRunId`, `EmployeeId`, `PayElementId`
* `Amount` — `decimal(19,4)`, already rounded per `OD-PAY-0008`
* `Sequence` — the order the element was evaluated in, retained so a payslip can explain itself
* `ITenantOwnedEntity`, `IAppendOnlyEntity`

**Lines are never updated.** A recalculation before approval **replaces** the line set; after posting,
nothing changes at all (`BR-PAY-0008`). This is what makes `REQ-PAY-0018`'s payslip-as-projection
permanently faithful.

---

## Entities

### `PayElementAssignment`

A standing instruction that an employee receives an element — a recurring allowance or deduction — with an
amount or rate. Child of `EmployeeCompensation` so that changing an allowance is a new dated compensation
record rather than a mutation, keeping `BR-PAY-0002` true for the whole compensation picture and not just
base pay.

`ITenantOwnedEntity` — **required**, not optional. FP-011 shipped `FiscalPeriod` and `JournalDraftLine`
without it and they would have been **silently absent from cutover**; the interface is what puts a type in
`TenantCutoverCopyPlan.Build`'s reflected manifest.

---

## The HR boundary

Payroll reads from HR: employee identity, employment status, termination date, company and organizational
placement. Payroll writes to HR: **nothing** (`DEC-PAY-0014`).

**No navigation property crosses the module boundary in either direction.** `EmployeeId` is an identifier.
`ADR-012` forbids a module API layer referencing another module's assemblies, and the same discipline
applies in the domain: a Payroll aggregate holding an HR `Employee` reference would make the modules one
module.

*Open and not assumed:* whether Payroll may query HR through a published read contract, or must receive
what it needs some other way, follows the mechanism chosen in `OD-PAY-0013` — that ruling governs both
directions of traffic, not only the GL side.

---

## The GL boundary

One direction only: **Payroll → GL**, at posting.

`OD-GL-0009` closed with *"nothing posts to GL in V1; the first inbound poster will be Payroll in V2"*, and
FP-011 removed `SSAS.GL.Contracts` recording that it *"returns when Payroll consumes it, shaped by its
consumer."* `OD-PAY-0013` is that shaping.

What Payroll needs GL to accept, at minimum:

* a company and a date (from which GL resolves the fiscal period — **Payroll must not name the period**,
  for exactly the reason GL's own wire contracts do not accept one: a caller who could name it could post
  into a period the date does not belong to)
* a description and reference
* a set of lines, each an account identifier plus a debit or credit amount

What Payroll needs back: the created journal's identifier, or a refusal it can act on — **a closed period
must come back as a refusal Payroll can name, not a generic failure** (`BR-PAY-0007`).

---

## What has no domain model here

**Tax and statutory contributions** — no authority to model from; see `business-rules.md`.
**Attendance-derived elements** — no input exists (`DEC-PAY-0002`).
**Advances and loans** — these are *balances carried across periods*, a fundamentally different object from
a pay element, and deferring them (`OD-PAY-0018`) is what keeps this model finite.

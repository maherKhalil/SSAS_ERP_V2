# FP-013 — Decisions

Two registers. **`DEC-ATT-####`** are settled by inheritance or by mechanical proof and are recorded so
nobody re-derives them. **`OD-ATT-####`** are **OWNER-DECISION-REQUIRED** and block the build prompt.

**Nothing in the `OD` register has a default.** Where this package has an opinion it says so and says why,
but an opinion is not a ruling, and the build prompt must not be written until each carries one.

---

## Part 1 — `DEC-ATT` (settled; recorded, not reopened)

### `DEC-ATT-0001` — `DEC-PAY-0002` is this package's birth certificate, and lifting it is Payroll-side work

`DEC-PAY-0002` refused overtime, absence deduction, shift differential and lateness **because the input did
not exist**, not because they were out of scope. FP-013 creates the input.

**But the components that consume it live in Payroll**, in `PayElementBehaviour` and `PayrollCalculator`.
This package **specifies that follow-up obligation and does not implement it**. Extending Payroll from
inside a package named Attendance would put the most consequential change of the feature where no reviewer
of either module would look for it.

The obligation is written down in [`requirements.md`](requirements.md) (`REQ-ATT-0022`) so it is carried
rather than remembered.

### `DEC-ATT-0002` — cross-module reach is a published contract, shaped by the consumer

Settled three times: `IEmployeeRoster` (HR→Payroll), `IJournalPoster` (GL→Payroll), and
`InspectPostingWindowAsync` (the consumer-shaped addition to an existing contract). `ADR-012` forbids the
module-assembly reference; the precedent adds that **the contract carries what the consumer needs and
nothing more.**

Applied here: the Attendance→Payroll contract carries **period totals**, not punches. A contract exposing
punch-level movement would let any future Payroll feature read minute-by-minute employee location **with no
call-site change for anyone to review** — the argument that kept `NationalId` out of `IEmployeeRoster`.
The exact shape is `OD-ATT-0009`; that it is a *summary* rather than a *feed* is settled here.

### `DEC-ATT-0003` — Attendance reads HR facts through a contract and never writes HR

The `DEC-PAY-0014` symmetry, unchanged. Employment dates, company membership and (if `OD-ATT-0011` rules it
in) branch come **from** HR. Nothing flows back — an attendance record never updates an employee.

`IEmployeeRoster` already exists and already carries `EmployeeId`, `CompanyId`, `EmploymentDateUtc`,
`TerminationDateUtc`. Whether it is **sufficient** depends on `OD-ATT-0011`: if attendance is branch-scoped,
the roster record has no branch field and the contract needs a consumer-shaped extension — the third time
that mechanism would be used, and a change to a contract HR owns.

### `DEC-ATT-0004` — no money in this module

Attendance records **quantities**: days, hours, units, occurrences. Rates and amounts live in Payroll, where
`EmployeeCompensation` already holds them under `ADR-027 d1`.

**A `decimal(19,4)` money column appearing in an Attendance table is a signal the boundary has drifted**, not
a detail. Quantities may still be `decimal` — hours are not integers — but a quantity is not money, and the
`ADR-027 d2` currency-projection rule does not apply to it.

### `DEC-ATT-0005` — `nvarchar` for every persisted application string

`ADR-018`. No exception, no per-column argument.

### `DEC-ATT-0006` — no cross-database foreign key

`ADR-022`. Attendance rows referencing `EmployeeId` reference a **Tenant DB** row (HR is a tenant module), so
an intra-database FK is available and should be used. Anything reaching a Platform DB identity — `TenantId`,
`UserId` in an audit column — is stored as a value with no constraint.

### `DEC-ATT-0007` — E3 cutover membership is derived, never listed

`TenantCutoverCopyPlan.Build` reflects over `ITenantOwnedEntity`. A tenant-owned type that does not carry the
interface is **silently absent from cutover** — no error, no warning, no test failure until a tenant migrates
and its data does not arrive.

Every Attendance entity that belongs to a tenant carries `ITenantOwnedEntity`, and the cutover expectation
lists are updated **by counting entries per list**, not by adjacency matching. FP-012 shipped a miss because
one of three name lists used `nameof(...)` where the others used string literals and the adjacency search
skipped it silently.

### `DEC-ATT-0008` — read scopes are unforgeable

`AttendanceScoped` (or whatever the module names it) follows `RosterScoped` / `EmployeeReadScope`: private
constructor, internal factory, authority resolved **live** from `ITenantCompanyAccessResolver` at
construction, and `UnauthorizedAccessException` rather than an empty result when authority is absent.

An empty list is indistinguishable from "no records", which turns an authorization failure into what looks
like a data answer. The architecture guard file listing the sanctioned read shapes grows by one, with its
reasoning inline — it is now at three and this makes four.

### `DEC-ATT-0009` — append-only where the record states what happened

`IAppendOnlyEntity` on any entity whose rows are a statement of fact about a past instant.
`TenantDbContext.PreventAppendOnlyMutation` refuses `Modified` **or** `Deleted` for such an entity
**unconditionally** — no status check, no escape hatch.

**That mechanical fact drives the aggregate shape**, exactly as it drove FP-012's three-type payroll-run
split. If a record must be correctable before it is final and immutable after, it is **two types**, not one
type with a status. This is the single most likely place FP-013 gets its data model wrong, because
attendance corrections are ordinary business (see `OD-ATT-0012`).

It also constrains **seeding**: FP-012's integration fixture threw during setup because an approved run was
seeded across two contexts. One context, one save.

### `DEC-ATT-0010` — every request record carries `[property: JsonPropertyName]`, and enums additionally carry `JsonStringEnumConverter`

`StrictRequestReader.ReadStrictJsonAsync` uses `JsonSerializerOptions.Default`, which is **case-sensitive**
and deserializes enums **from numbers only**.

**This has shipped as a total, silent defect twice.** GL omitted the attribute and every write route answered
`400 request.invalid`. Payroll omitted the converter and `POST /api/payroll/elements` refused every
well-formed body — no pay element could be created, so no payroll could ever run.

Both faults were an **absence**, which is what reading the code does not reveal. FP-013 carries the
write-route binding scenario from the first test written, not the last.

### `DEC-ATT-0011` — schema changes go through `tools/SSAS.Tenant.MigrationTool` only

`ComposedTenantDbContextFactory` is the only design-time factory that composes module contributors. The
Platform factory passes `modelContributors: null` and **scaffolds DROP statements for every module table**.
Attendance registers its contributor there in the same commit as its first migration.

### `DEC-ATT-0012` — the Payroll guard is **replaced**, not deleted

`PayrollCalculatorTests.No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt` asserts no
`PayElementBehaviour` name contains *Hour*, *Overtime* or *Absence*. When the `DEC-ATT-0001` follow-up lands,
**it fails, and it is right to fail.**

It is then replaced by guards asserting the new, positive truth — that each attendance-driven behaviour has a
declared input and that no behaviour exists without one — exactly as FP-012 replaced GL's vacuous
`There_is_no_gl_contracts_assembly` with two guards that load the assembly by reference.

**A green suite obtained by deleting the test that went red is not a green suite.**

### `DEC-ATT-0013` — terminated employees per `BR-HR-0004`'s ruled reading

`OD-PAY-0010` ruled that `BR-HR-0004` bars **new obligations** to a terminated employee, not the settlement
of obligations already incurred. Attendance inherits the reading directly: **time already worked, and leave
already taken, remain recorded and reportable after termination.** What is barred is recording *new*
attendance dated after the termination date.


### `DEC-ATT-0014` — every Attendance entity's branch classification is **asserted**, whichever way `OD-ATT-0011` rules

`IBranchOwnedEntity` is a **deliberate classification, not a default**, and its own comment names the failure
mode: an entity that should have been branch-scoped and was not is *readable by every branch in the tenant,
and nothing about it looks wrong.*

HR asserts the classification entity by entity — `Employee` positively, `Department`, `DepartmentManager`,
`EmployeeDepartmentAssignment` and `EmployeeBranchAssignment` negatively. **Payroll asserts nothing**: its
entities are tenant-global by omission.

**The guard is not a reflection sweep.** `BranchSessionArchitectureTests.No_tenant_global_or_routing_entity_is_branch_owned`
walks a **hardcoded list of six Platform types**. Nothing forces a new module to classify itself, which is
exactly how Payroll ended up unasserted — so this is a commitment FP-013 makes, not a rule it inherits.

Every Attendance entity gets an explicit assertion. **A negative assertion is as valuable as a positive
one**, because it records that somebody decided rather than that nobody noticed.

---

## Part 2 — `OD-ATT` (owner decision required; each blocks the build)

### `OD-ATT-0001` — **SCOPE: attendance, leave, or both?** ← decide this first

**RULED: BOTH, sequenced — attendance core first, the leave entitlement ledger second; one module, one calendar, one approval shape.**

**Everything else in this package is conditional on this answer**, and it is the reason the package is
written in conditional voice throughout.

The three candidates are not three sizes of the same thing:

| | **A. Attendance only** | **B. Leave only** | **C. Both** |
|---|---|---|---|
| Records | time worked | absence entitlement and consumption | both |
| Core question | *was this person here, and for how long* | *how much is this person owed, and how much have they used* | both |
| Needs a working calendar | yes | yes | yes (shared) |
| Needs an approval workflow | only for corrections | **yes, centrally** — it is the primary act | yes |
| Needs balances carried over time | no | **yes** — accrual, carry-over, expiry | yes |
| Feeds Payroll | hours / overtime / absence days | leave-without-pay days, encashment | both |

**They share exactly two things — a working calendar and an approval shape — and diverge everywhere else.**
Leave is fundamentally an *entitlement ledger*: balances accrue, are consumed, carry over, expire. Attendance
is fundamentally an *observation log*: something happened at a time. A design that treats leave as
"attendance with a negative sign" gets the balance arithmetic wrong, because a balance is not derivable from
an observation log alone.

**This package's opinion, offered as an opinion:** **C, but sequenced** — one module, calendar and approval
built once, with leave's entitlement ledger as a second delivery inside it. Building A then bolting leave on
later means either a second calendar or a retrofit of the first; building B alone leaves `DEC-PAY-0002`
half-lifted, since overtime and lateness have no source.

**But C is roughly double A**, and if the owner's actual near-term need is only one of them, that is a real
saving and this package should be cut to match rather than delivered whole and half-used.

**Ruling required. Every `OD` below is annotated with which scopes it applies to.**

---

### `OD-ATT-0002` — **the `REQ-ATT` identifier space does not exist and must be created** *(all scopes)*

**RULED: REQ-ATT space created by this ratification — prefix in Requirement-Numbering.md, catalog at Requirement-Catalog/ATT.md.**

`Requirement-Numbering.md` defines nine prefixes: PLT, HR, GL, INV, CRM, PRJ, PAY, PRC, MFG. **There is no
ATT.** Payroll at least had `REQ-PAY` reserved; Attendance has nothing.

So this package cannot draft into a reserved space — **it proposes the space itself**, which is an edit to a
governing document and therefore an owner decision rather than a coder's convenience.

Three sub-questions, all needing an answer:

1. **Does `ATT` get its own prefix, or do attendance requirements extend `REQ-HR`?** Attendance is arguably
   an HR concern; the roadmap lists it as its own module. The nine existing prefixes are per-*module*, which
   argues for `ATT`, but that is an inference from a pattern, not a rule anyone wrote.
2. **If both attendance and leave are in scope (`OD-ATT-0001` = C), is that one prefix or two?** One `ATT`
   covering both, or `ATT` and a separate `LVE`? The identifier space is cheap; a wrong split is not, because
   requirement IDs appear in traceability matrices that outlive the decision.
3. **What is the counterpart `BR-ATT` position?** `Business-Rules.md` has no attendance rules and places
   attendance under *Future Modules*. That file needs the same addition, and it is the same decision.

**This package's opinion:** own prefix `ATT`, one space covering both concerns, matching the per-module
pattern and avoiding a split that would have to be guessed at every cross-reference. **Offered as an opinion.
The package uses `REQ-ATT`/`BR-ATT` provisionally and every identifier is renumberable.**

---

### `OD-ATT-0003` — **capture model: what is the atomic record?** *(A and C)*

**RULED: DAILY RECORDS — the package's opinion, not clock events.**

Three shapes, and they are not interchangeable:

| | **Clock events** | **Daily records** | **Period timesheets** |
|---|---|---|---|
| Row means | "in at 08:02", "out at 17:14" | "2026-09-14: 8.0h worked, 0h overtime" | "week 37: 40h across 5 days" |
| Derivation | daily totals derived from pairs | stated directly | stated directly |
| Handles missed punches | badly — an unpaired IN is a defect state | n/a | n/a |
| Sensitivity | **high** — reveals movement and timing | low | low |
| Fits | factory / retail with hardware | salaried office staff | project and consultancy work |

The sensitivity row matters beyond design taste. Clock events are a record of **when a person came and
went**, which is materially more sensitive than "worked 8 hours" and would need its own permission split
under the `<Plane>.<Resource>.<Action>` grammar's sensitivity convention.

**This package's opinion:** **daily records** as the stored atom, with clock events — if ever needed — as an
*input* that derives a daily record rather than as the stored truth. It keeps the sensitive data out of the
persisted model, matches what payroll actually consumes, and does not force a missed-punch repair workflow
into v1. **Ruling required.**

---

### `OD-ATT-0004` — **the working calendar: whose is it, and at what granularity?** *(all scopes)*

**RULED: COMPANY-OWNED calendar; weekly pattern as data plus a dated holiday list.**

Every scope needs one: attendance needs it to know which days *should* have records, leave needs it to know
which days a request consumes.

Open at three levels:

1. **Ownership.** Company-level (one calendar per company), branch-level (each branch has its own — plausible
   where branches are in different countries or observe different weekends), or tenant-level with company
   overrides. **Depends on `OD-ATT-0011`.**
2. **Weekend definition.** Not universal. Fri/Sat, Sat/Sun and Thu/Fri are all in use in the regions this
   product's `ADR-024` currency and locale handling contemplates. It must be **data, not a constant**.
3. **Holidays.** A named, dated, maintainable list — and the question of whether a holiday is *per company*
   or *per branch* is the same question as (1).

**Additionally:** does creating this calendar make `OD-PAY-0007` reopenable? See `OD-ATT-0015`.

**This package's opinion:** company-level calendar with a data-driven weekend pattern and a dated holiday
list, deferring branch-level calendars until `OD-ATT-0011` is ruled — a branch calendar is a strict
superset and can be added without invalidating a company one. **Ruling required.**

---

### `OD-ATT-0005` — **leave types: fixed enum or configurable catalog?** *(B and C)*

**RULED: CONFIGURABLE CATALOG with a closed behaviour enum.**

Annual, sick, unpaid, maternity/paternity, bereavement, study, hajj/pilgrimage, compassionate — the list is
**jurisdictional**, and a fixed enum in the domain freezes a list that varies by country and by employer.

The `PayElement` precedent is directly on point: FP-012 made pay elements a **configurable catalog with a
closed `PayElementBehaviour` enum** — administrators define the elements, the code defines the small set of
things an element can *do*. The same split applies here: a configurable `LeaveType` catalog with a closed
`LeaveBehaviour` enum (paid / unpaid / accruing / non-accruing / and whatever else is ruled).

**But the enum's members are exactly the question**, and `DEC-PAY-0002`'s lesson applies: a behaviour whose
input does not exist must not be declared. If accrual is deferred (`OD-ATT-0006`), `Accruing` cannot be an
enum member on day one.

**This package's opinion:** configurable catalog + closed behaviour enum, following `PayElement` exactly,
with the enum's membership derived from whatever `OD-ATT-0006` rules. **Ruling required.**

---

### `OD-ATT-0006` — **accrual rules: in scope, or is a balance simply administered?** *(B and C)*

**RULED: BALANCES ADMINISTERED. Accrual rules deferred.**

The largest single lever inside leave, and the difference between a small module and a large one.

- **Administered balances.** An administrator sets an employee's entitlement; requests decrement it. Simple,
  correct, and **manual**.
- **Accrued balances.** Entitlement accumulates by rule — per month of service, pro-rated on hire and
  termination, with carry-over caps, expiry dates and seniority tiers. This is a **calculation engine with a
  dated history**, comparable in weight to `PayrollCalculator`.

Accrual also drags in the questions accrual always drags in: what happens to a balance on termination
(encashed? forfeited? paid at what rate — which would put money in this module against `DEC-ATT-0004`), what
happens at year end, and whether an employee may go negative.

**This package's opinion:** **administered balances in v1**, accrual as a named follow-up. It delivers usable
leave management without a second calculation engine, and — importantly — an administered balance is a
*strict subset* of an accrued one, so accrual can be added later without invalidating stored data. The
reverse is not true. **Ruling required.**

### `OD-ATT-0007` — **approval workflow: who approves, and via which reporting line?** *(B and C; A only for corrections)*

**RULED: DEPARTMENT-MANAGER approval; self-approval barred; parent-chain escalation for unmanaged and self-referential departments; permission-holder fallback at the root.**

The Glossary's only mention of leave is as an example of a **Workflow**, and this is where that shows up.

**The reporting-line question is answered, and the answer is a partial one.** Checked against the code
rather than assumed:

- **There is no employee→manager edge.** `Employee` has `CompanyId`, `BranchId`, `DepartmentId`,
  `PositionId` — and **no `ManagerId`**.
- **There is a department manager.** `DepartmentManager` is a real entity with its own command handlers,
  configuration and error mapper, and its primary key is on `DepartmentId` — **one manager per department**,
  which is why a second row is unrepresentable and why the assign route pre-translates a unique-constraint
  violation.
- **Departments nest.** `Department.ParentDepartmentId` with a `ChangeParent()` operation, so a chain
  upward exists.
- **The manager must be a valid employee.** `Department.ManagerEmployeeNotFound`,
  `ManagerInDifferentCompany` and **`ManagerTerminated`** are all modelled errors.

So a reporting line **is** derivable — *employee → department → (parent chain) → that department's manager*
— but it is **indirect, department-mediated, and single-seated.** That is a materially different thing from
a direct manager edge, and three consequences follow that the owner has to rule on:

1. **An employee's approver is whoever manages their department.** If the department has no manager
   (`ManagerNotAssigned` is a modelled error, so this state is reachable), **who approves?** Escalate to the
   parent department, fall back to a permission holder, or block the request?
2. **A department manager is themselves in a department.** Who approves *their* leave? The parent
   department's manager is the natural answer and it needs saying, along with what happens at the root.
3. **`ManagerTerminated` is modelled**, so a terminated manager is a state the system contemplates. Leave
   requests do not stop arriving because a manager left.

The alternatives remain open: a **permission holder** (following Payroll's `Payroll.Run.Approve` shape), or
a **configured chain** (a workflow engine, which is a large thing to introduce here).

`OD-PAY-0009`'s separation-of-duty reading barred the calculator from approving; the analogous bar here is
**self-approval** — and note that under the department-manager model a department manager requesting leave
would otherwise be approving themselves, so the bar is not hypothetical.

**This package's opinion:** department-manager approval with an explicit self-approval bar and
parent-department escalation for the unmanaged and self-referential cases, falling back to a permission
holder at the root. It uses the structure that exists rather than adding one. **Ruling required on all
three sub-questions — the fallbacks are not derivable from the code, only the primary path is.**

### `OD-ATT-0008` — **overtime: recorded fact or computed rule?** *(A and C)*

**RULED: RECORDED with a tier label. Every rate stays in Payroll.**

`DEC-PAY-0002` named overtime first among the things it could not build. Lifting it needs a ruling on what
overtime *is*:

- **Recorded.** A supervisor states "3 hours overtime on the 14th". Attendance stores what it was told.
- **Computed.** Attendance derives it — hours beyond the calendar's standard day, or beyond a weekly
  threshold, or on a holiday — and rates (1.5×, 2×) apply.

Computed overtime needs threshold rules, and multipliers are **rates**, which under `DEC-ATT-0004` belong in
Payroll as `PayElement` configuration. So a computed model splits across two modules: Attendance derives the
*quantity* of overtime at each *tier*, Payroll applies the money. That is a clean split but it means the
contract (`OD-ATT-0009`) carries tiered quantities, not a single number.

**This package's opinion:** **recorded** overtime in v1 with a tier label, computed thresholds deferred. It
lifts `DEC-PAY-0002` for overtime with the smallest correct surface, and keeps every rate in Payroll.
**Ruling required.**

### `OD-ATT-0009` — **the Attendance→Payroll contract: exact shape** *(all scopes)*

**RULED: PER-PERIOD summary — the caller names a date, the module resolves the period. No straddles.**

`DEC-ATT-0002` settles that it is a published, consumer-shaped **summary** contract. What it carries is open,
and it is the most consequential open question after `OD-ATT-0001` because it determines what Payroll can
ever compute.

Candidate shape, offered for the owner to cut down or extend:

```
AttendanceSummary(
  EmployeeId, CompanyId, PeriodStartUtc, PeriodEndUtc,
  WorkedDays, WorkedHours,
  OvertimeHoursByTier,        // if OD-ATT-0008 = recorded/computed
  PaidAbsenceDays,
  UnpaidAbsenceDays,          // the one Payroll must deduct
  LeaveDaysByType)            // if OD-ATT-0001 includes leave
```

Open within it: **is the summary per period or per date range?** Per-period ties Attendance to Payroll's
period, which under `OD-PAY-0002` is the fiscal period — clean, but it means Attendance inherits a period
concept it may not otherwise need. A date range is looser and lets a caller ask for a straddle, which is
precisely what `OD-PAY-0002` refused to permit in payroll bounds.

**This package's opinion:** **per period**, mirroring `GeneratePayrollPeriodCommand`'s
"name a date, the module resolves the period" shape, so a caller cannot name a straddle. **Ruling required.**

### `OD-ATT-0010` — **close discipline: must an attendance period be closed before Payroll may read it?** *(all scopes)*

**RULED: (a) — periods close and Payroll refuses an open one, via an Inspect method returning a closed-enum outcome.**

The `InspectPostingWindowAsync` question, inverted. GL let Payroll *inspect* whether a period was open before
attempting to post, so the caller got a clear refusal rather than a late failure.

Here: if Payroll calculates a run from an attendance period that is still being edited, the run's numbers are
a snapshot of a moving target — and payroll runs are approved and posted, so a wrong snapshot becomes a
posted journal entry.

Three positions: **(a)** Attendance periods close and Payroll refuses an open one; **(b)** Payroll may read
an open period but the contract reports the period's state and the run records it; **(c)** no close concept —
Payroll takes what it finds.

**This package's opinion:** **(a) with an inspect method**, following `InspectPostingWindowAsync` exactly, so
the refusal is a modelled outcome in a closed enum rather than an exception. It is the same problem GL
already solved and the precedent is one module away. **Ruling required.**

### `OD-ATT-0011` — **branch dimension: is Attendance branch-owned?** *(all scopes)* ← a genuine question, and the mechanism already exists

**RULED: THE SPLIT — attendance records are branch-owned; the Payroll summary contract is deliberately branch-blind and company-complete. The hole is INTENDED, stated at the site and guard-asserted.**

**A correction to this package's own earlier draft, which said branch was descriptive in HR and carried no
authorization meaning. That was wrong, and the repository is unambiguous.** Branch is a **first-class,
fully built authorization dimension**:

| Mechanism | Where |
|---|---|
| `IBranchOwnedEntity` — the classification marker | `BuildingBlocks.Domain` |
| `ITenantBranchAccessResolver` — authoritative, returns **active branches only** | `BuildingBlocks.Tenancy.Branches` |
| `UserBranchAccess` — a Platform entity granting a tenant user specific branches | `Platform.Domain` |
| `ICurrentBranchResolver` — the write boundary **stamps** `BranchId` from the execution context | `BuildingBlocks.Tenancy.Branches` |
| `IBranchTransferScope` / `IBranchTransferAuthorizer` | the **only** way a `BranchId` is ever modified |
| Active branch lives on the **session**, not the user, and is **not** an access-token claim | `BranchSessionArchitectureTests` |

`IBranchOwnedEntity` states the stakes itself, in the same silent-failure shape as `DEC-ATT-0007`'s cutover
manifest:

> **IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, NOT A DEFAULT.** … the failure mode is silent: an
> entity that should have been branch-scoped and was not is **readable by every branch in the tenant, and
> nothing about it looks wrong.**

**The two existing modules do disagree — but not for the reason the earlier draft gave:**

- **HR classifies entity by entity, and asserts it.** `Employee` **is** branch-owned
  (`EmployeeArchitectureTests:32`). `Department`, `DepartmentManager`, `EmployeeDepartmentAssignment` and
  `EmployeeBranchAssignment` are each asserted **not** to be. Every one is an explicit test.
- **Payroll classifies by omission.** `PayrollArchitectureTests.cs` exists and contains **no branch
  assertion at all**; no Payroll entity appears in any `IBranchOwnedEntity` assertion anywhere in the suite.
  Its entities are tenant-global because nobody wrote the interface, not because a test says they should be.

Stated plainly: **`DEC-PAY-0017`'s two-dimension roster was a deliberate ruling, but the entity
classification underneath it was never asserted.** FP-013 must not repeat that, whichever way this rules —
see `DEC-ATT-0014`.

**The substantive question, with real arguments both ways:**

*For branch-owned:* attendance is observed **locally**. A branch supervisor records who was present at their
branch, and the entire `UserBranchAccess` → `ITenantBranchAccessResolver` stack exists precisely so that
supervisor sees their branch and not another. Working calendars plausibly differ by branch (`OD-ATT-0004`).
`Employee` is already branch-owned, so the grain is available at no cost.

*For tenant-global (company-scoped):* it matches Payroll, Attendance's principal consumer, and the summary
contract aggregates to company anyway. `RosterScoped`'s reasoning bites hard here — **a branch filter means
a payroll-feeding query can silently omit employees**, which is exactly what `DEC-PAY-0017` refused. And an
employee who transfers branch mid-period has their attendance split across two branch scopes; the
`IBranchTransferScope` machinery exists because that transfer is a real, modelled event.

**Note the asymmetry the arguments do not resolve.** Branch-owning attendance protects *supervisor reads*
but risks *payroll completeness*. Both are real and they pull opposite ways. A split answer is available —
records branch-owned, the Payroll summary contract deliberately branch-blind — but that is a design with a
hole in it unless someone rules the hole is intended.

**This package declines to offer a preference.** Whether branches are administrative labels or real
operational units with their own supervisors is a fact about the owner's business, not about the code.
**Rule this early** — `OD-ATT-0004`, `OD-ATT-0009`, `OD-ATT-0013` and every table in
[`data-model.md`](data-model.md) move with it.

### `OD-ATT-0012` — **retro corrections to a closed period** *(all scopes)*

**RULED: NEW ADJUSTMENT RECORDS, never edits.**

Attendance corrections are **ordinary business**, not an exception: a missed record surfaces after close, a
leave request is retro-dated, a supervisor mis-keyed a day.

But `DEC-ATT-0009` makes append-only mutation refusal **unconditional**, and if the period fed a payroll run
that has been approved and posted, the correction is downstream of a journal entry.

Three positions: **(a)** closed is closed, corrections land in the next period as an adjustment (the GL
model, and it composes with posted payroll); **(b)** reopen with a permission and a recorded reason, with
Payroll's dependent run flagged; **(c)** corrections allowed until the payroll run is approved, barred after.

**This package's opinion:** **(a)**, matching GL's closed-period discipline, which is the only one of the
three that does not require a story for "what happens to the posted journal entry". **Ruling required — and
whichever wins, it dictates the aggregate split under `DEC-ATT-0009`**, so it cannot be deferred past the
data model.

### `OD-ATT-0013` — **read and reporting permissions: whose attendance can a person see?** *(all scopes)*

**RULED: SELF-SERVICE DEFERRED. No identity-to-employee assumption anywhere. Now a recorded future package.**

The permission grammar is `<Plane>.<Resource>.<Action>` with sensitivity splits, and attendance data is
personal: it records where someone was and when they were absent, and leave reveals **medical absence** by
type. Sick leave is health information.

Open:

1. **May an employee see their own record — and can they?** **The answer to "can" is currently NO, verified
   rather than assumed.** Self-service needs a mapping from the authenticated identity to an employee
   record, and **none exists**: `Employee` carries no user or tenant-user identifier, and neither HR's domain
   nor its contracts expose one. `OD-PAY-0016` deferred payroll self-service for precisely this reason, and
   `PayrollPermissionNames` records the refusal in the code — *"Adding a `Payroll.Payslips.ViewOwn` on an
   unverified assumption is exactly the shape of the FP-011 near-miss."*

   **This is `DEC-PAY-0002`'s shape a second time: a missing input, not a scoping preference.** It follows
   that no `ViewOwn` permission may be declared, and — more consequentially — that **employees cannot submit
   their own leave requests**, so a leave module delivered now is one administrators operate on employees'
   behalf. That may be an acceptable first delivery; it must not be an accidental one.

   **The ruling therefore has two parts:** (i) is administrator-operated leave acceptable for v1, and (ii) is
   creating the identity→employee mapping a **prerequisite of FP-013**? The mapping is a Platform/HR change,
   not an Attendance one. It is raised at this weight because this is the **second consecutive feature** to
   hit the same wall, and a second hit is where a missing input stops looking like a coincidence.
2. **May a manager see their reports records?** Under `OD-ATT-0007`'s finding the line is *department-mediated* — a `DepartmentManager` sees their department, and the `ParentDepartmentId` chain decides whether that reaches sub-departments. **Whether it does is the ruling**, and it is the difference between a manager seeing 8 people and 800.
3. **Is leave *type* a sensitivity split?** Seeing "absent 3 days" is different from seeing "sick leave, 3
   days". The grammar supports the split; whether to use it is the ruling.
4. **Does branch bound the read?** — moves with `OD-ATT-0011`.

**This package's opinion:** split leave type behind a higher-sensitivity permission, following the grammar's
existing convention for exactly this kind of asymmetry. **Ruling required on all four.**

### `OD-ATT-0014` — **module home and project layout** *(all scopes)*

**RULED: OWN MODULE — src/Modules/Attendance/.**

`src/Modules/Attendance/SSAS.Attendance.*` as a peer of HR, Finance and Payroll — or `src/Modules/HR/` as a
second module family under the HR folder, given attendance is arguably an HR concern.

The roadmap lists Attendance as its own line, which argues for a peer. Moves with `OD-ATT-0002`(1) — the
same judgement about whether attendance is a module or an HR extension, and the two should be ruled together
so the requirement prefix and the folder do not disagree.

**This package's opinion:** peer module, matching the roadmap. **Ruling required.**

### `OD-ATT-0015` — **does `OD-PAY-0007` reopen once a working calendar exists?** *(all scopes)*

**RULED: PAYROLL PRORATION UNCHANGED — calendar days. The lever is recorded as untaken.**

`OD-PAY-0007` ruled **calendar-day** proration, and the stated reason was that working-day proration needs a
calendar the product does not have. `OD-ATT-0004` creates that calendar, so the stated reason expires.

**That does not make the ruling wrong.** Calendar-day proration may have been the owner's preference on its
own merits, and the missing calendar may have been merely the argument that settled it at the time.

**This is a business decision wearing a technical costume**: it changes what every employee is paid for a
partial month, in every company, retroactively in effect if not in data.

**This package offers no preference and explicitly does not assume the ruling reopens.** It is raised because
failing to raise it would leave a stale justification in the Payroll package with no record that anyone
noticed. **Ruling required — including the entirely legitimate ruling "no change".**

### `OD-ATT-0016` — **hardware and device integration: in or out?** *(A and C)*

**RULED: OUT.**

Biometric readers, badge terminals, mobile geofenced check-in. Each is an **integration surface** with device
protocols, offline buffering, clock drift and duplicate-punch handling — a body of work comparable to the
rest of the module and largely unrelated to it.

Geofenced check-in additionally stores **employee location**, which is a different category of personal data
from working hours and would carry its own retention and consent questions.

**This package's opinion: OUT, explicitly and on the record**, with the capture model (`OD-ATT-0003`) shaped
so a device feed could later produce daily records without a schema change. Raised rather than assumed
because "attendance system" means "clock terminals" to many stakeholders, and an unstated exclusion is the
kind of gap that surfaces at acceptance. **Ruling required.**

---

## Summary

| Register | Count |
|---|---|
| `DEC-ATT` settled | **14** |
| `OD-ATT` owner-decision-required | **16** |

**`OD-ATT-0001` (scope) and `OD-ATT-0011` (branch) should be ruled first** — between them they move
`OD-ATT-0004`, `OD-ATT-0005`, `OD-ATT-0006`, `OD-ATT-0007`, `OD-ATT-0009`, `OD-ATT-0013` and the entire shape of
[`domain-model.md`](domain-model.md). The remaining fourteen can be ruled in any order.

# FP-013 — Acceptance criteria

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

Each `AC-ATT-####` is stated so it can be **failed**. Criteria that depend on an unruled decision say which,
and criteria that cannot be written until a ruling lands are named as such rather than guessed.

Scope column: `A` attendance, `B` leave, `*` both.

---

## Calendar

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0001` | A calendar with a Fri/Sat weekend reports Sunday as a working day and Friday as not — **the weekend pattern is read from data, and no test may pass by assuming Sat/Sun** | `*` | `REQ-ATT-0001` |
| `AC-ATT-0002` | Adding a holiday on an existing working day reduces `WorkingDaysBetween` for a range containing it by exactly one | `*` | `REQ-ATT-0002`, `REQ-ATT-0003` |
| `AC-ATT-0003` | A holiday falling on a weekend day does **not** reduce the count further — it was never a working day | `*` | `REQ-ATT-0003` |
| `AC-ATT-0004` | Two holidays on the same date in the same calendar are refused | `*` | `REQ-ATT-0002` |
| `AC-ATT-0005` | `WorkingDaysBetween(d, d)` for a single working day returns 1, and for a single weekend day returns 0 — **the boundary case, stated because off-by-one at the range ends is the defect this class of code actually has** | `*` | `REQ-ATT-0003` |

## Attendance capture

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0006` | Recording attendance for an employee on a date within their employment window succeeds | `A` | `REQ-ATT-0004` |
| `AC-ATT-0007` | Recording attendance dated **after** an employee's termination date is refused, with an error that names **the employee**, not the record | `A` | `REQ-ATT-0006` |
| `AC-ATT-0008` | Recording attendance dated **before** the employment date is refused | `A` | `REQ-ATT-0006` |
| `AC-ATT-0009` | A record already settled by termination remains **readable** after termination — `BR-HR-0004` bars new obligations, not the settlement of existing ones | `A` | `REQ-ATT-0006` |
| `AC-ATT-0010` | No attendance write accepts, stores or returns a monetary amount or a currency code | `*` | `REQ-ATT-0009` |
| `AC-ATT-0011` | Overtime is recorded as a quantity with a tier label and **no multiplier** — the rate lives in Payroll | `A` | `REQ-ATT-0007` |

## Period and close

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0012` | Closing a period refuses a subsequent write into it | `*` | `REQ-ATT-0018` |
| `AC-ATT-0013` | Close records `ClosedUtc` and `ClosedBy`, and closing an already-closed period is refused rather than silently repeated | `*` | `REQ-ATT-0018` |
| `AC-ATT-0014` | **RULED (a).** A correction after close creates an adjustment record in the next period; the closed period is never edited, and the refusal comes from the persistence layer rather than from a status check | `*` | `REQ-ATT-0019` |
| `AC-ATT-0015` | Attempting to modify or delete any `IAppendOnlyEntity` attendance row throws, **regardless of period status** — the refusal is unconditional and no code path may assume otherwise | `*` | `REQ-ATT-0019` |

## Leave

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0016` | A leave request spanning a weekend consumes only the working days inside it | `B` | `REQ-ATT-0013` |
| `AC-ATT-0017` | A request spanning a holiday consumes one fewer day than the same range without it | `B` | `REQ-ATT-0013` |
| `AC-ATT-0018` | Approval decrements the balance by exactly `WorkingDaysConsumed`; rejection and cancellation decrement nothing | `B` | `REQ-ATT-0015` |
| `AC-ATT-0019` | **A holiday added after approval does not change the consumption of an already-approved request** — the figure was frozen at decision time | `B` | `REQ-ATT-0013` |
| `AC-ATT-0020` | An approver who is the requester is refused **by the domain**, not by the endpoint — a permission check cannot express "may this person approve *this* request" | `B` | `REQ-ATT-0014` |
| `AC-ATT-0021` | A leave type's `Code` cannot be changed after creation, and an update request carrying one is refused as an unknown property | `B` | `REQ-ATT-0010` |
| `AC-ATT-0022` | A leave type is deactivated, never deleted, and a deactivated type cannot be named on a new request while existing requests that reference it remain intact | `B` | `REQ-ATT-0010` |

## The Payroll boundary

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0023` | The summary contract returns totals for one employee and one period, and exposes **no punch-level, per-event or time-of-day data** | `*` | `REQ-ATT-0020` |
| `AC-ATT-0024` | `InspectPeriodAsync` on an open period returns `PeriodOpen` **as a value** — it does not throw, and it does not return data | `*` | `REQ-ATT-0021` |
| `AC-ATT-0025` | **RULED (a).** Payroll calculation against an open attendance period is refused with a modelled outcome the caller must handle | `*` | `REQ-ATT-0021` |
| `AC-ATT-0026` | Payroll consumes the contract **without an assembly reference** to any Attendance implementation project — asserted by an architecture test, not by inspection | `*` | `REQ-ATT-0020` |
| `AC-ATT-0027` | The contract does not disclose leave **type** if `OD-ATT-0013`(3) rules it sensitive — the contract may not be laxer than the module's own HTTP surface | `B` | `REQ-ATT-0020` |

## Authorization

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0028` | A read scope constructed for a company the caller has no authority over throws `UnauthorizedAccessException` — it does **not** return an empty list | `*` | `REQ-ATT-0005` |
| `AC-ATT-0029` | Authority is resolved **live** at scope construction; a caller whose access was revoked after their session began is refused | `*` | `REQ-ATT-0005` |
| `AC-ATT-0030` | The read scope cannot be constructed outside its factory — private constructor, internal factory, asserted by an architecture test | `*` | `REQ-ATT-0005` |
| `AC-ATT-0031` | **RULED: THE SPLIT.** A caller sees only their authorized, active branches on record reads, resolved live from `ITenantBranchAccessResolver`; **and the Payroll summary contract applies no branch predicate at all** — company-complete by design, guard-asserted | `*` | `REQ-ATT-0024` |
| `AC-ATT-0032` | **The module declares exactly two self-service permissions**, `Attendance.Records.ViewOwn` and `Attendance.Leave.ViewOwn`. Neither grants the other, and neither is granted by any administrative Attendance permission. **A third self-service permission requires a recorded decision.** *(Amended T-102. It read "no `ViewOwn` permission exists" — an absence criterion, which answers its question exactly once and then has to be deleted. An exact inventory keeps answering it.)* | `*` | `REQ-ATT-0023` |

## Persistence and platform

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0033` | Every persisted application string column is `nvarchar`, verified **against the created database**, not against the model | `*` | — |
| `AC-ATT-0034` | No Attendance table declares a foreign key to a Platform DB table | `*` | — |
| `AC-ATT-0035` | Every Attendance tenant entity appears in the E3 cutover manifest, **derived by reflection over `ITenantOwnedEntity`** rather than compared against a list someone typed | `*` | — |
| `AC-ATT-0036` | Every Attendance entity carries an explicit branch classification assertion — positive or negative, never absent | `*` | — |
| `AC-ATT-0037` | The migration is produced by `tools/SSAS.Tenant.MigrationTool` and contains **no `DROP` statement** for any other module's table | `*` | — |
| `AC-ATT-0038` | Every write route binds a correctly-cased JSON body, **including every enum-valued property**, on its first test run | `*` | — |

---

## Added by the orphan check

**These seven exist because the mechanical check found the requirements they cover had no criterion.** They
are listed separately rather than folded into the tables above so the gap — and its closure — stays visible
to a reviewer. FP-012's lesson was that a *derived* inventory beats a remembered one; this is that lesson
applied to the package's own internal consistency.

| ID | Criterion | Scope | Req |
|---|---|---|---|
| `AC-ATT-0039` | Paid and unpaid absence are recorded as **separate quantities**, and only the unpaid quantity reaches the Payroll summary as a deduction driver | `A` | `REQ-ATT-0008` |
| `AC-ATT-0040` | An employee's balance per leave type is readable, and an administrator can set the entitlement; the consumed figure is **never** directly settable | `B` | `REQ-ATT-0011` |
| `AC-ATT-0041` | Submitting a request records the requester, the type, the range and the computed working-day consumption, and refuses an end date before the start date | `B` | `REQ-ATT-0012` |
| `AC-ATT-0042` | Cancelling a request whose dates have **not** passed releases nothing (the balance moved only on approval); cancelling one whose dates **have** passed is treated as a correction under `OD-ATT-0012`, not as an ordinary cancellation | `B` | `REQ-ATT-0016` |
| `AC-ATT-0043` | A leave request whose range falls outside the employee's employment window is refused, on the same boundary reading as `AC-ATT-0007` | `B` | `REQ-ATT-0017` |
| `AC-ATT-0044` | After the Payroll-side follow-up, `No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt` **no longer exists under that name**, and the two replacement guards are present and green. **A run in which it was simply deleted fails this criterion** | `*` | `REQ-ATT-0022` |
| `AC-ATT-0045` | Leave **type** is not returned on any read a caller holds only `Attendance.Leave.View` for, if `OD-ATT-0013`(3) rules it sensitive | `B` | `REQ-ATT-0025` |

## Criteria that cannot yet be written

**Accrual.** `OD-ATT-0006`. If accrual is ruled in, this section gains criteria for pro-ration on hire and
termination, carry-over caps, expiry and negative balances — a body of work comparable to the rest of the
list. Writing them now would state a design the owner has not chosen.

**Approval routing.** `OD-ATT-0007`. The primary path is derivable (`Employee.DepartmentId` →
`DepartmentManager`), but the fallbacks for an unmanaged department, a manager approving themselves, and a
terminated manager are all rulings — and each needs its own criterion, because each is a reachable state the
code will otherwise handle by accident.

**Close preconditions.** Whether close refuses when employees have no records, or when leave requests are
pending, is unruled. `AC-ATT-0013` covers only the mechanics.

**Anything self-service.** Delivered under FP-015 in T-089: `Attendance.Self.ViewOwnRecords` and `Attendance.Self.ViewOwnLeave`, read by `GET /me/records` and `GET /me/leave-requests`. `AC-ATT-0032` is
now an exact inventory of those two rather than an assertion of absence.

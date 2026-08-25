# FP-013 — Data model (proposed)

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

**Every table below is conditional on `OD-ATT-0001` (which exist) and `OD-ATT-0011` (whether each carries
`BranchId`).** Column types are not conditional — those are settled.

---

## Settled before any table is drawn

| Rule | Source | Consequence |
|---|---|---|
| Every persisted application string is `nvarchar` | `ADR-018` / `DEC-ATT-0005` | no `varchar`, no per-column argument |
| **No money columns in this module** | `DEC-ATT-0004` | a `decimal(19,4)` here means the boundary drifted |
| Quantities are `decimal(9,2)` | this package's proposal | hours are not integers; 2dp is ample and it is *not* the money type, deliberately, so the two are distinguishable at a glance |
| No cross-database foreign key | `ADR-022` / `DEC-ATT-0006` | `TenantId` and audit `UserId` are values; `EmployeeId` **may** be an FK — HR is a Tenant DB module |
| Every tenant-owned type carries `ITenantOwnedEntity` | `DEC-ATT-0007` | omission means **silent absence from cutover** |
| Branch classification is asserted per entity | `DEC-ATT-0014` | positive or negative, but never absent |

**On `EmployeeId` as a foreign key** — it is available, since HR and Attendance share the Tenant DB, and
`ADR-022` bars only the *cross-database* case. Whether to use it is a live question this package flags
rather than settles: an FK gives referential integrity, but it is also the one place where Attendance
touches HR's table directly rather than through `IEmployeeRoster`, and `DEC-ATT-0003` says facts come
through the contract. **The FK constrains existence; the contract supplies meaning.** They are compatible,
but the reviewer should see the tension named.

---

## Tables

### `WorkingCalendars` — all scopes

| Column | Type | Notes |
|---|---|---|
| `WorkingCalendarId` | `uniqueidentifier` | PK |
| `TenantId` | `uniqueidentifier` | value, no FK |
| `CompanyId` | `uniqueidentifier` | |
| `BranchId` | `uniqueidentifier` | **RULED PRESENT** — stamped by the write boundary from the execution context |
| `Name` | `nvarchar(200)` | |
| `WeekendDays` | `nvarchar(64)` | the day set, **persisted as data** — see below |
| `IsDefault` | `bit` | |
| audit + `RowVersion` | | `CreatedBy`/`ModifiedBy` are **`string?`**, not `Guid` |

**`WeekendDays` as a string needs its justification stated**, because it looks like a smell. It is a small
set drawn from a closed domain (`DayOfWeek`), it is never joined on, never range-queried, and never
aggregated — the only operation is "is this date a weekend", performed after loading the calendar. A child
table would add a join to every calendar read to model seven possible values. **If the build disagrees, a
child table is the right disagreement**; what is not negotiable is that the pattern is data rather than a
constant.

Unique: `(TenantId, CompanyId, [BranchId,] Name)`.

### `CalendarHolidays` — all scopes

| Column | Type | Notes |
|---|---|---|
| `CalendarHolidayId` | `uniqueidentifier` | PK |
| `WorkingCalendarId` | `uniqueidentifier` | FK, cascade |
| `TenantId` | `uniqueidentifier` | |
| `HolidayDate` | `date` | **`date`, not `datetimeoffset`** — a holiday is a calendar day, not an instant |
| `Name` | `nvarchar(200)` | |

Unique: `(WorkingCalendarId, HolidayDate)`.

**The `date` choice is deliberate and it is the one place this module departs from the `DateTimeOffset`
convention used everywhere else.** A public holiday is not an instant on a timeline; storing it as one
invites an offset conversion to move it across midnight into the previous day. The same argument applies to
`AttendanceDate` and to leave request bounds below.

### `AttendancePeriods` — all scopes

| Column | Type | Notes |
|---|---|---|
| `AttendancePeriodId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `FiscalPeriodId` | `uniqueidentifier` | **only if `OD-ATT-0009` rules period-aligned**; value, no FK across modules |
| `Name` | `nvarchar(200)` | |
| `StartDate`, `EndDate` | `date` | |
| `Status` | `nvarchar(32)` | **string, not int** — see the fixture note below |
| `ClosedUtc` | `datetimeoffset` | nullable |
| `ClosedBy` | `nvarchar(256)` | nullable, `string?` |
| audit + `RowVersion` | | |

**`Status` as a string, and this is a scar.** FP-012's integration fixture seeded a company with an
**integer** `Status` and `SYSUTCDATETIME()`, both copied verbatim from GL's fixture, and both wrong. Status
enums in this codebase persist as strings. A fixture that guesses the storage shape fails at setup, which
reads as an environment problem rather than a fixture bug.

### `AttendanceRecords` — scope A and C

| Column | Type | Notes |
|---|---|---|
| `AttendanceRecordId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `BranchId` | `uniqueidentifier` | **RULED PRESENT** — `IBranchOwnedEntity`, stamped by the write boundary |
| `AttendancePeriodId` | `uniqueidentifier` | FK |
| `EmployeeId` | `uniqueidentifier` | FK available — see the note above |
| `AttendanceDate` | `date` | |
| `WorkedQuantity` | `decimal(9,2)` | |
| `OvertimeQuantity` | `decimal(9,2)` | |
| `OvertimeTier` | `nvarchar(32)` | nullable; per `OD-ATT-0008` |
| `PaidAbsenceQuantity` | `decimal(9,2)` | |
| `UnpaidAbsenceQuantity` | `decimal(9,2)` | the one Payroll deducts |
| `Note` | `nvarchar(1000)` | nullable |
| audit (+ `RowVersion` **only if mutable**) | | |

**`RowVersion` is present only if `OD-ATT-0012` leaves the record mutable.** An append-only row has nothing
to concurrency-check — the column would be dead weight that implies an update path exists.

**RULED: NO unique index on `(TenantId, EmployeeId, AttendanceDate)`.** `OD-ATT-0012` ruled adjustments-never-edits, and under
ruling (b), or under any adjustment model that appends a correcting row, **a second row for the same
employee-date is exactly what corrections look like** and the unique index makes the ruling
unimplementable.

**That is the sharpest coupling in this document**, and it is easy to miss: an index chosen from the happy
path silently forecloses a correction model.

Index: `(TenantId, AttendancePeriodId, EmployeeId)` — the shape the summary contract reads.

### `LeaveTypes` — scope B and C

| Column | Type | Notes |
|---|---|---|
| `LeaveTypeId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `Code` | `nvarchar(32)` | immutable from creation, following `Account` and `PayElement` |
| `NormalizedCode` | `nvarchar(32)` | the HR normalization precedent |
| `Name` | `nvarchar(200)` | |
| `Behaviour` | `nvarchar(32)` | closed enum, membership per `OD-ATT-0005` and `OD-ATT-0006` |
| `IsActive` | `bit` | deactivation, never deletion |
| audit + `RowVersion` | | |

Unique: `(TenantId, CompanyId, NormalizedCode)`.

### `LeaveBalances` — scope B and C, shape per `OD-ATT-0006`

Under **administered** balances: `(LeaveBalanceId, TenantId, CompanyId, EmployeeId, LeaveTypeId, PeriodYear,
EntitlementQuantity, ConsumedQuantity, audit, RowVersion)`, unique on
`(TenantId, EmployeeId, LeaveTypeId, PeriodYear)`.

Under **accrued** balances this becomes **two tables** — an append-only accrual ledger plus this as a
projection — and the projection's correctness becomes a thing tests must prove rather than a thing the
schema guarantees.

### `LeaveRequests` — scope B and C

| Column | Type | Notes |
|---|---|---|
| `LeaveRequestId` | `uniqueidentifier` | PK |
| `TenantId`, `CompanyId` | `uniqueidentifier` | |
| `EmployeeId`, `LeaveTypeId` | `uniqueidentifier` | |
| `StartDate`, `EndDate` | `date` | |
| `WorkingDaysConsumed` | `decimal(9,2)` | **computed at approval and stored** — see below |
| `Status` | `nvarchar(32)` | |
| `DecidedBy` | `nvarchar(256)` | nullable, `string?` |
| `DecidedUtc` | `datetimeoffset` | nullable |
| `DecisionNote` | `nvarchar(1000)` | nullable |
| audit + `RowVersion` | | |

**`WorkingDaysConsumed` is stored, not derived on read**, and the reason is the calendar is *maintainable*.
A holiday added next year would silently change how many days a leave request taken last year consumed —
and therefore a balance that was already settled. **Storing the number freezes the fact at the moment the
decision was made**, which is the same instinct behind `PayrollRunLine` being append-only.

---

## E3 cutover manifest

`TenantCutoverCopyPlan.Build` derives its manifest by **reflecting over `ITenantOwnedEntity`**. A tenant-owned
type without the interface is **silently absent** — no error, no warning, and no failing test until a tenant
migrates and its data does not arrive.

**Every table above carries `ITenantOwnedEntity`.**

The cutover expectation lists are updated by **counting entries per list**, not by matching adjacent names.
FP-012 shipped a miss because one of three name lists used `nameof(...)` where the other two used string
literals, and an adjacency search skipped it in silence.

---

## Migration

Through `tools/SSAS.Tenant.MigrationTool` **only** (`DEC-ATT-0011`). `ComposedTenantDbContextFactory` is the
sole design-time factory that composes module contributors; the Platform factory passes
`modelContributors: null` and **scaffolds DROP statements for every module table**.

Attendance registers its `IModelContributor` in the same commit as its first migration — not the one after.

## Integration fixture notes, carried from FP-012's scars

- `IntegrationSqlEnvironment.ForCatalog(name)` — **not** `ConnectionFor`
- `SetupCommandTimeoutSeconds = 120`
- Company seed: **string** `Status`, and **not** `SYSUTCDATETIME()`
- **One context, one save** when seeding anything append-only. FP-012 seeded an approved run across two
  contexts and `PreventAppendOnlyMutation` threw during *setup*, which presents as an environment failure
  rather than a fixture bug

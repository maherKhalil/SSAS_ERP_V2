# FP-013 — Test scenarios

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

Layered as the existing modules are: **domain** (no database), **API** (host + stubbed dependencies),
**integration** (real SQL Server), **architecture** (reflection over the built assemblies).

The FP-012 shape was 45 domain + 26 API + 9 integration + architecture guards. A comparable Attendance
module under scope C should land in the same range; under scope A or B, roughly half.

---

## Domain — `tests/Attendance.Tests`

| ID | Scenario | AC |
|---|---|---|
| `TS-ATT-0001` | Working-day count across a Fri/Sat weekend, a Sat/Sun weekend and a Thu/Fri weekend — **three cases, because one case would pass with a hardcoded weekend** | `AC-ATT-0001` |
| `TS-ATT-0002` | Holiday on a working day reduces the count by one; holiday on a weekend day changes nothing | `AC-ATT-0002`, `AC-ATT-0003` |
| `TS-ATT-0003` | Single-day ranges at both ends, and a range whose first and last days are both weekends | `AC-ATT-0005` |
| `TS-ATT-0004` | Duplicate holiday date refused | `AC-ATT-0004` |
| `TS-ATT-0005` | Attendance dated after termination refused; dated before employment refused; dated on the termination date itself **accepted** — the boundary, stated explicitly because inclusive-versus-exclusive is where this goes wrong | `AC-ATT-0007`, `AC-ATT-0008` |
| `TS-ATT-0006` | Records for a terminated employee remain readable | `AC-ATT-0009` |
| `TS-ATT-0007` | Overtime carries a tier and no multiplier | `AC-ATT-0011` |
| `TS-ATT-0008` | Close sets `ClosedUtc`/`ClosedBy`; second close refused | `AC-ATT-0013` |
| `TS-ATT-0009` | Write into a closed period refused | `AC-ATT-0012` |
| `TS-ATT-0010` | Leave spanning a weekend consumes working days only; spanning a holiday consumes one fewer | `AC-ATT-0016`, `AC-ATT-0017` |
| `TS-ATT-0011` | Approval decrements by exactly the consumed figure; rejection and cancellation decrement nothing | `AC-ATT-0018` |
| `TS-ATT-0012` | **Holiday added after approval leaves the approved request's consumption unchanged** — the frozen-figure guarantee | `AC-ATT-0019` |
| `TS-ATT-0013` | Self-approval refused **at the aggregate**, with the endpoint not involved | `AC-ATT-0020` |
| `TS-ATT-0014` | Leave-type code immutable after creation | `AC-ATT-0021` |
| `TS-ATT-0015` | Deactivated type cannot be named on a new request; existing requests referencing it remain intact | `AC-ATT-0022` |
| `TS-ATT-0016` | **No `LeaveBehaviour` member exists whose input the module does not have** — the `DEC-PAY-0002` guard in its Attendance form | `AC-ATT-0011` |

## API — `tests/API.Tests/Attendance`

| ID | Scenario | AC |
|---|---|---|
| `TS-ATT-0017` | Every route rejects an unauthenticated caller and a caller lacking the named permission | `AC-ATT-0028` |
| `TS-ATT-0018` | **Every write route binds a correctly-cased JSON body, including every enum-valued property.** Written first, not last | `AC-ATT-0038` |
| `TS-ATT-0019` | An unknown property is refused by the strict reader | `AC-ATT-0021` |
| `TS-ATT-0020` | Employment-window refusal returns an error naming **the employee**, not the record — the `DepartmentApiErrorMapper` miscoding shape | `AC-ATT-0007` |
| `TS-ATT-0021` | Close is a POST with **no body**; there is no route accepting a status field | `AC-ATT-0013` |
| `TS-ATT-0022` | Approve and reject accept a decision note and **nothing that changes what is being approved** | `AC-ATT-0020` |
| `TS-ATT-0023` | No response body on any route carries a monetary amount or currency code | `AC-ATT-0010` |
| `TS-ATT-0024` | `PUT` and `DELETE` on `/records` return 405 — the absent verbs are asserted, not merely unimplemented | `AC-ATT-0014` |

## Integration — `tests/Integration.Tests` (real SQL Server)

| ID | Scenario | AC |
|---|---|---|
| `TS-ATT-0025` | Every application string column is `nvarchar`, read from `INFORMATION_SCHEMA` **on the created database** | `AC-ATT-0033` |
| `TS-ATT-0026` | No foreign key crosses to a Platform DB table | `AC-ATT-0034` |
| `TS-ATT-0027` | Quantity columns are `decimal(9,2)` and **no column in any Attendance table is `decimal(19,4)`** — the money type's absence asserted positively | `AC-ATT-0010` |
| `TS-ATT-0028` | Unique indexes exist as specified — **and the attendance-record uniqueness matches the `OD-ATT-0012` ruling**, since a unique `(Tenant, Employee, Date)` makes an appended correction unimplementable | `AC-ATT-0014` |
| `TS-ATT-0029` | Attempting `Modified` on an append-only attendance row throws, **and attempting `Deleted` throws too** — both states, because the guard refuses both and testing one proves half of it | `AC-ATT-0015` |
| `TS-ATT-0030` | The migration applies cleanly to an empty database and drops no other module's table | `AC-ATT-0037` |
| `TS-ATT-0031` | Round-trip of dates across the `date` columns does not shift across midnight under a non-UTC server offset | — |

## Architecture — `tests/Architecture.Tests`

| ID | Scenario | AC |
|---|---|---|
| `TS-ATT-0032` | No Payroll or HR project references an Attendance **implementation** assembly; the contracts assembly is loaded **by reference**, not by name | `AC-ATT-0026` |
| `TS-ATT-0033` | The read scope has a private constructor and an internal factory, and the sanctioned-read-shape file lists Attendance's query with its reasoning inline | `AC-ATT-0030` |
| `TS-ATT-0034` | Every Attendance entity carries an explicit `IBranchOwnedEntity` assertion — **positive or negative** | `AC-ATT-0036` |
| `TS-ATT-0035` | The E3 manifest, **derived by reflection over `ITenantOwnedEntity`**, contains every Attendance tenant entity | `AC-ATT-0035` |
| `TS-ATT-0036` | The permission catalog contains no `ViewOwn` of any kind | `AC-ATT-0032` |
| `TS-ATT-0037` | The summary contract exposes no member returning per-event or time-of-day data | `AC-ATT-0023` |

---

## Added by the orphan check

**Thirteen scenarios, because the mechanical check found these criteria had no test.** Kept in their own
section so the gap and its closure remain visible.

| ID | Scenario | AC |
|---|---|---|
| `TS-ATT-0040` | Recording attendance **inside** the employment window succeeds — the positive case, which `TS-ATT-0005` proves only the refusals of | `AC-ATT-0006` |
| `TS-ATT-0041` | `InspectPeriodAsync` against an open period returns `PeriodOpen` as a value, **returns no data**, and does not throw | `AC-ATT-0024` |
| `TS-ATT-0042` | Payroll calculation against an open attendance period is refused with a modelled outcome — the caller handles a value, not an exception | `AC-ATT-0025` |
| `TS-ATT-0043` | The summary contract returns no leave **type** when `OD-ATT-0013`(3) rules it sensitive — the contract is not laxer than the HTTP surface | `AC-ATT-0027` |
| `TS-ATT-0044` | A caller whose company access is revoked **after** their session begins is refused at scope construction — authority is re-asked, not cached | `AC-ATT-0029` |
| `TS-ATT-0045` | **RULED: THE SPLIT.** A caller sees only authorized, **active** branches on record reads; the summary contract applies **no** branch predicate. Both asserted, plus the entity-by-entity classification including the negatives | `AC-ATT-0031` |
| `TS-ATT-0046` | Paid and unpaid absence are separate quantities and only the unpaid one drives deduction | `AC-ATT-0039` |
| `TS-ATT-0047` | Entitlement is settable; consumed is not directly settable | `AC-ATT-0040` |
| `TS-ATT-0048` | Submission records all four facts and refuses an inverted date range | `AC-ATT-0041` |
| `TS-ATT-0049` | Cancellation before the dates pass releases nothing; after they pass it routes through the correction path | `AC-ATT-0042` |
| `TS-ATT-0050` | A leave range outside the employment window is refused, on the same inclusive boundary as `TS-ATT-0005` | `AC-ATT-0043` |
| `TS-ATT-0051` | **The replaced Payroll guard**: the old test name is absent and both replacements are present and green — a deletion fails this | `AC-ATT-0044` |
| `TS-ATT-0052` | A caller holding only `Attendance.Leave.View` receives leave occurrences without their type | `AC-ATT-0045` |

## The Payroll-side follow-up — `REQ-ATT-0022`

**`PayrollCalculatorTests.No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt` will fail,
and it is right to fail.**

It asserts that no `PayElementBehaviour` name contains *Hour*, *Overtime* or *Absence*. The moment the
follow-up adds those behaviours it goes red — correctly, because the fact it guards has changed.

**It is replaced, never deleted** (`DEC-ATT-0012`), by guards asserting the new positive truth:

| ID | Replacement guard |
|---|---|
| `TS-ATT-0038` | Every attendance-driven `PayElementBehaviour` has a declared input on the summary contract |
| `TS-ATT-0039` | **No `PayElementBehaviour` exists without an input** — the `DEC-PAY-0002` principle preserved rather than discarded, now stated positively |

This is the `There_is_no_gl_contracts_assembly` pattern exactly: FP-012 replaced that vacuous guard with two
that load the assembly by reference, rather than deleting a test that had started failing.

**A green suite obtained by deleting the test that went red is not a green suite.**

---

## Gate notes, carried

- Run through `scripts/gate.sh`. Project list becomes
  `Architecture Platform HR API Finance Payroll Integration Attendance`
- **`LEAN` mode** caps `xUnit.MaxParallelThreads=4` with a 2048 MB floor. Measured cost: **30 seconds** of
  Integration wall (30m34s → 31m04s) for a **22× better memory trough** (14 MB → 305 MB). The ceiling is
  nearly free
- **Never edit `gate.sh` while it is running.** Bash reads scripts incrementally; inserted lines shift code
  under the interpreter mid-execution and killed a run that had finished every leg green
- Both configurations, clean builds. **A zero-warning claim is a claim about a CLEAN build** — an
  incremental build hid a CA1859 warning once and the claim shipped wrong
- **`--` inside an XML comment breaks a csproj** (MSB4025). Parse-validate csprojs before invoking dotnet;
  this has now broken two features

# FP-013 — API contracts (proposed)

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

Two surfaces: **HTTP**, for people and clients, and the **in-process module contract** Payroll consumes.

---

## The rule that governs every request record

**Every request property carries `[property: JsonPropertyName]`. Every enum property additionally carries
`[property: JsonConverter(typeof(JsonStringEnumConverter))]`.**

`StrictRequestReader.ReadStrictJsonAsync` deserializes with `JsonSerializerOptions.Default`, which is
**case-sensitive** and reads enums **from numbers only**.

**This has shipped as a total, silent defect twice:**

| Feature | Absence | Consequence |
|---|---|---|
| FP-011 (GL) | no `JsonPropertyName` | `{"code":"4100"}` never bound; **every GL write route answered `400 request.invalid`** while routes, handlers, domain and error mapper were all correct |
| FP-012 (Payroll) | no `JsonStringEnumConverter` | `"Earning"` could not become a `PayElementKind`; `POST /api/payroll/elements` refused **every** well-formed body, so no pay element could be created and no payroll could ever run |

Both faults were an **absence**, which is exactly what reading the code does not reveal. A property-level
`[JsonConverter]` is honoured regardless of serializer options, which is why it works where a global option
would not.

**`TS-ATT-0018` asserts a correctly-cased body binds on every write route, and it is written first.**

---

## HTTP surface

### Working calendar — all scopes

```
GET    /api/attendance/calendars                      Attendance.Calendars.View
POST   /api/attendance/calendars                      Attendance.Calendars.Manage
PUT    /api/attendance/calendars/{id}                 Attendance.Calendars.Manage
POST   /api/attendance/calendars/{id}/holidays        Attendance.Calendars.Manage
POST   /api/attendance/calendars/{id}/holidays/remove Attendance.Calendars.Manage
GET    /api/attendance/calendars/{id}/working-days    Attendance.Calendars.View
```

`holidays/remove` as a **POST**, not `DELETE`, following HR's `manager/remove`: removing a holiday is a
dated change to a maintained list, and the codebase already spells that as a named action.

`working-days` is the query `REQ-ATT-0003` requires, exposed because clients need the same answer the domain
uses — a client computing working days itself would drift from the server the first time a holiday moved.

### Attendance records — scope A and C

```
GET    /api/attendance/records                        Attendance.Records.View
POST   /api/attendance/records                        Attendance.Records.Manage
POST   /api/attendance/records/bulk                   Attendance.Records.Manage
```

**No `PUT` and no `DELETE`, deliberately.** Whether a correction is an amendment or an appended adjustment is
`OD-ATT-0012`, and the absent verbs are that ruling made visible in the surface rather than a rule someone
has to remember — the same reasoning that gave `RecordCompensationRequest` a POST and no PUT.

**If `OD-ATT-0012` rules (c)**, a `PUT` appears for the draft state only, and the close transition is what
removes the ability to use it.

### Periods — all scopes

```
GET    /api/attendance/periods                        Attendance.Periods.View
POST   /api/attendance/periods                        Attendance.Periods.Manage
POST   /api/attendance/periods/{id}/close             Attendance.Periods.Close
POST   /api/attendance/periods/{id}/reopen            Attendance.Periods.Close   [SAFE under append-only: reopen permits appending, never editing]
```

**No status field on any request body.** Every transition is a named-action POST with its own permission.
A `PUT {status: "closed"}` would let the act that freezes Payroll's inputs arrive through the same door as
an ordinary edit.

Close takes **no body**. Everything it needs is on the period it names, and a body would let a caller change
what is being closed at the moment of closing — GL's posting route takes no body for the same reason.

### Leave — scope B and C

```
GET    /api/attendance/leave-types                    Attendance.LeaveTypes.View
POST   /api/attendance/leave-types                    Attendance.LeaveTypes.Manage
PUT    /api/attendance/leave-types/{id}               Attendance.LeaveTypes.Manage
POST   /api/attendance/leave-types/{id}/activate      Attendance.LeaveTypes.Manage
POST   /api/attendance/leave-types/{id}/deactivate    Attendance.LeaveTypes.Manage

GET    /api/attendance/leave-requests                 Attendance.Leave.View
POST   /api/attendance/leave-requests                 Attendance.Leave.Manage
POST   /api/attendance/leave-requests/{id}/approve    Attendance.Leave.Approve
POST   /api/attendance/leave-requests/{id}/reject     Attendance.Leave.Approve
POST   /api/attendance/leave-requests/{id}/cancel     Attendance.Leave.Manage

GET    /api/attendance/leave-balances                 Attendance.Leave.View
PUT    /api/attendance/leave-balances/{id}            Attendance.Leave.Manage   [administered only]
```

**No `code` on the leave-type update request** — the code is immutable from creation, following `Account`
and `PayElement`, so the wire shape has no field for it and a caller who sends one gets a 400 rather than a
silently ignored property. `behaviour` is absent for the same reason: changing it would redefine what past
requests consumed while leaving their stored rows untouched.

**Approve and reject take a body carrying only a decision note.** They cannot carry the dates or the type —
that would let an approver change what they are approving at the moment of approval.

**Note what these routes assume.** Under the `OD-ATT-0013`(1) finding, `POST /leave-requests` is an
**administrator** submitting on an employee's behalf. The identity→employee mapping exists (`UserEmployeeLink`, `ADR-030`) but no route reads it. The
`employeeId` in the body is therefore mandatory, not inferred. **If the mapping is later created, that field
becomes optional and the route gains a self-service meaning** — a change worth anticipating rather than
retrofitting.

---

## Error mapping

Follows `DepartmentApiErrorMapper` and `PayrollApiErrorMapper`: a table from domain error code to
`(status, code)`, with **no catch-all that turns unmapped codes into 500**.

`DepartmentApiErrorMapper` carries the scar in its header — a shared mapper produced a defect where a
department manager error surfaced under an employee code. **Attendance gets its own mapper, not a shared
one.**

Errors that describe **the employee named in the body** must never be reported as errors about the resource
addressed in the route. That is exactly the miscoding `DepartmentApiErrorMapper` was written to fix, and
`REQ-ATT-0006`'s employment-window refusal is the same shape: it is a fact about the employee, arriving on a
route addressed to a record.

`Error` exposes **`Message`**, not `Description`.

---

## The module contract Payroll consumes

`SSAS.Attendance.Contracts`, following `SSAS.HR.Contracts` and `SSAS.GL.Contracts`. Shape per `OD-ATT-0009`:

```csharp
public interface IAttendanceSummary
{
  Task<AttendanceSummaryResult> GetForPeriodAsync(
    Guid companyId, Guid employeeId, DateTimeOffset anyDateInPeriodUtc, CancellationToken ct);

  Task<AttendancePeriodInspection> InspectPeriodAsync(
    Guid companyId, DateTimeOffset anyDateInPeriodUtc, CancellationToken ct);
}

public enum AttendanceSummaryStatus
{
  Available, PeriodOpen, PeriodNotFound, EmployeeNotInScope
}
```

**`anyDateInPeriodUtc` rather than bounds**, mirroring `GeneratePayrollPeriodCommand`: bounds a caller could
name are bounds a caller could misalign, and the period lookup would then silently answer for a straddle.

**`InspectPeriodAsync` is the `InspectPostingWindowAsync` precedent**, and it exists so "that period is not
closed yet" reaches Payroll as a **value it must handle**, not an exception it might not catch. A closed enum
makes every outcome something the compiler can see the caller ignoring.

**The contract carries totals and never punches** (`DEC-ATT-0002`). This is worth restating at the
definition site, in the register `SSAS.GL.Contracts` used:

> A CROSS-MODULE CONTRACT HAS NO BUSINESS BEING LAXER THAN THE OWNING MODULE'S OWN HTTP SURFACE.

Applied here: if `Attendance.Leave.ViewSensitive` gates leave *type* over HTTP, the contract must not hand
leave type to Payroll ungated. **Payroll needs paid-versus-unpaid day counts to compute pay; it does not
need to know which of them were sick days.**

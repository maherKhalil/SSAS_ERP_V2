# Item 235 — the class-level guard: a seventh module cannot inherit the hole silently

**`Every_module_registration_binds_its_read_services_to_its_own_infrastructure`, in
`ModuleReadServiceRegistrationTests`. TASK gate: see foot.**

## What it guards

**Each module's OWN extensions are called on a real `ServiceCollection`** — `AddHrModule()` +
`AddHrInfrastructure()`, and the same for GL, Payroll and Attendance — and every `I…ReadService` they
register must bind to a concrete implementation **from that module's infrastructure assembly**.

## ⚠⚠ AND WHAT IT EXPLICITLY DOES NOT GUARD — WHICH IS THE PART WORTH READING

**This guards the REGISTRATION. It does not guard that the service is ever EXERCISED.**

⚠ **Attendance is the proof: its host DOES call `AddAttendanceInfrastructure` and then registers
`AddSingleton<IAttendanceReadService>(Reads)`, an explicit stub, last-in-wins.** **This test passes for
Attendance today and would have passed for it before item 233 was written — while
`AttendanceReadService` was still constructed by nothing.**

**233 guards the behaviour. 235 guards the wiring. Neither substitutes for the other, and an hour before
233 ran they would have looked redundant.**

## The form: descriptors, deliberately

**The REAL extensions are called; what is inspected is what they REGISTERED.** ⚠ **That is not the weaker
form — the weaker form would assert descriptors INSTEAD of calling the extension.** Resolving would
additionally require a tenant context accessor, which is fixture wiring this claim does not need: the
failure mode is *the module's registration does not bind the concrete type*, and a descriptor answers
exactly that.

## The controls

- ⚠ **The module count is NAMED and asserted**: `["Attendance", "GL", "HR", "Payroll"]`. **A module added
  later is not covered until somebody adds it here, and this assertion is what makes that a FAILURE
  rather than a silent gap** — the whole defect being guarded is a new module inheriting an old template.
- **A module registering NO read service is an offender**, not a pass. Otherwise it contributes nothing
  to the offender list and reads as compliant.
- **Anti-vacuity floor** on the number of registrations actually examined: an empty offender list is
  equally consistent with everything passing and with nothing being examined.
- ⚠ **`ImplementationType is null` is the FACTORY/INSTANCE shape — how a stub is wired** — and is called
  out separately from a wrong assembly, because the two failures have different causes.

## The plants

| plant | failure |
|---|---|
| the module stops registering its read service | ⚠ **`Payroll: registers no read service at all`** — the literal item 233 shape |
| the read service is bound by a FACTORY | ⚠ **`Payroll.IPayrollReadService: bound to a factory or instance, not to a concrete type`** — the stub shape |

**Two distinct causes, two distinct messages.**

## Why this row mattered more after tonight than when it was queued

**Item 233 exercised three read services for the first time and `GlReadService` carried two live defects
— queries that could not execute at all, one of them the trial balance.** ⚠ **The cause was one
difference, not three omissions: two of six API test hosts called `Add<Module>Module()` and never
`Add<Module>Infrastructure()`.** **No per-module test can see a seventh module repeating that; this one
can.**

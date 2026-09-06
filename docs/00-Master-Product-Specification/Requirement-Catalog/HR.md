# HR Requirements

Domain

Human Resources

Prefix

REQ-HR

---

Employee Management

REQ-HR-0001

Create Employee

REQ-HR-0002

Update Employee

REQ-HR-0003

Terminate Employee

REQ-HR-0004

Transfer Employee

REQ-HR-0005

Employee Documents

REQ-HR-0006

Employee History

REQ-HR-0007

Employee Status

REQ-HR-0008

Employee Search

REQ-HR-0009

Employee Import

REQ-HR-0010

Employee Export

---

Department

REQ-HR-0100

Department CRUD

REQ-HR-0101

Department Hierarchy

REQ-HR-0102

Department Manager

---

Position

REQ-HR-0200

Position Management

REQ-HR-0201

Job Grades

REQ-HR-0202

Salary Grade

---

Future Modules

> ⚠⚠ **CORRECTED 2026-09-06, PER NAME.** This list read *"Attendance, Payroll, Recruitment, Performance,
> Training, Self Service"*. **Two of the six had shipped, and one is neither shipped nor future.**
> *This file is 801 bytes and has not been touched since 2026-07-29 — before FP-006, FP-007 and FP-008
> shipped — so the list is not the only thing in it that predates the HR estate; it is the part that was
> checkable.*

***No longer future — both delivered:***

- **Attendance** — `src/Modules/Attendance`, package `FP-013`, catalog `ATT.md` (updated 2026-08-28).
- **Payroll** — `src/Modules/Payroll`, package `FP-012`, catalog `PAY.md`; its eighteen `OD-PAY` owner
  decisions were all ruled on 2026-08-24.

***Still future — no module, no package, no catalog entry:***

- Recruitment
- Performance
- Training

***⚠ Self Service — neither, and the distinction matters:***

- **The MODULE is future.** There is no `src/Modules/SelfService`. `FP-015-self-service` exists as an
  analysis package at `status: DRAFT`, `version: 0.1`, with an open decision register.
- ⚠ **The CAPABILITY is partly built, inside another module.**
  `src/Modules/Attendance/SSAS.Attendance.Application/Reads/AttendanceSelfServiceScopeResolver.cs` exists,
  and FP-015's own `AC-SS-0001` is written against `attendance.record.view.self`.
- ***So a reader who removes this entry will go looking for a module that does not exist, and a reader who
  leaves it unqualified will not go looking for the resolver that does.***
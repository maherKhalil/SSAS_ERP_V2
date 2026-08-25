# FP-013 — Attendance (analysis package)

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

**Status:** DRAFT — analysis only. No code, no schema, no ADR. Every `OD-ATT-####` in
[`decisions-open.md`](decisions-open.md) is **OWNER-DECISION-REQUIRED** and blocks the build prompt.

---

## The sweep came first, and it found nothing — but not *quite* nothing

| Swept | Result |
|---|---|
| `SSAS.ERP.sln` | **No Attendance, Leave, Timesheet, Shift or Roster project** |
| `src/Modules/` | `Finance/SSAS.GL.*`, `HR/SSAS.HR.*`, `Payroll/SSAS.Payroll.*` — **no Attendance tree** |
| `tools/`, `tests/` | **No attendance test home** |
| Branches (local + remote) | **No branch matching attend / leave / time / shift** |
| Every path in every ref | **No file path matching attendance, `SSAS.Attend*`, `/Leave*`, timesheet** |
| Code identifiers in `src/` and `tests/` | **Four hits — all of them `DEC-PAY-0002` boundary notes** |

Greenfield, as expected. **The four code hits are the interesting part**, because one of them is a guard this
feature will have to deal with:

```
PayElement.cs:30            "…no OvertimeMultiple, because Attendance is unbuilt and none of them has an input"
PayrollCalculator.cs:33     "…working days require a calendar the product does not have — that is Attendance"
PayrollCalculator.cs:48     "No overtime, absence deduction, shift differential or lateness (DEC-PAY-0002)"
PayrollCalculatorTests:149  No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt   ← A GUARD
```

**That last one is a live test asserting this module does not exist.** It passes today by checking that no
`PayElementBehaviour` name contains *Hour*, *Overtime* or *Absence*. When FP-013's follow-up work adds those
behaviours to Payroll, that guard **fails — correctly** — and must be **replaced, not deleted**, exactly as
FP-012 replaced GL's `There_is_no_gl_contracts_assembly`. Recorded as `DEC-ATT-0012` so it is met as a
ruling rather than discovered as an obstacle.

---

## What authority actually exists — the thinnest base of any package so far

Thinner than FP-012's, which was itself the thinnest at the time.

| Source | What it actually says |
|---|---|
| `Product-Roadmap.md` | **"Attendance"** — one word, second in the Version 2 list |
| `Requirement-Numbering.md` | **NO `REQ-ATT` PREFIX EXISTS.** The file lists nine: PLT, HR, GL, INV, CRM, PRJ, PAY, PRC, MFG |
| `Business-Rules.md` | **No `BR-ATT` rule.** "Attendance" appears once, under *"Future Modules — Business Rules … will be added in future releases"* |
| `Requirement-Catalog/HR.md` | "Attendance" listed under *Future Modules* |
| `Glossary.md` | **"Attendance" is NOT a term. "Leave" is NOT a term.** "Leave Request" appears once — as the fourth of four *examples* under **Workflow**, beside Employee Hiring, Journal Approval and Purchase Approval |

**Read that last row carefully, because it is easy to over-claim.** The Glossary does not define leave, does
not say the product will have leave, and does not describe a leave process. It uses "Leave Request" to
illustrate what the word *Workflow* means. It is evidence that the authors expected leave to exist
eventually; it is **not** a requirement, and this package does not treat it as one.

### The identifier space itself has to be created

Payroll had `REQ-PAY-0001` reserved as a bare line. **Attendance has nothing.** So this package cannot merely
draft requirements into a reserved space — **it must propose the space**, which is an addition to
`Requirement-Numbering.md` and therefore an owner decision in its own right (`OD-ATT-0002`).

The `GL.md` / `PAY.md` precedent governs everything after that: `REQ-ATT-####` drafted as proposals, every
one owner-decision-required, traced to whatever authority exists — and where none exists, saying so.

---

## The inherited section — recorded verbatim, none reopened

### `DEC-PAY-0002` — this package's birth certificate

> **Attendance-driven components cannot exist in FP-012. SETTLED-BY-ABSENCE.** Overtime computed from worked
> hours, absence deductions derived from a register, shift differentials and lateness penalties each require
> a source of truth this product does not have. **This is not a scoping preference; it is a missing input.**

**FP-013 exists to lift that boundary.** But note precisely what lifting it involves: the boundary is
*Payroll's*, and the work of lifting it — new `PayElementBehaviour` members and the calculation that
consumes them — is **Payroll-side**.

**This package SPECIFIES that follow-up obligation; it does not implement it.** A package that quietly
extended Payroll while calling itself Attendance would put the change where nobody reviewing Attendance
would look for it.

### `OD-PAY-0007` — the calendar this module probably births

> **Proration: CALENDAR DAYS.** Working-day proration was refused because it needs a working calendar the
> product does not have — *that is Attendance*, and `DEC-PAY-0002` bars deriving anything from input that
> does not exist.

If FP-013 creates a working calendar, the stated reason for calendar-day proration disappears. **Whether
Payroll's proration then moves to working days is an owner decision** (`OD-ATT-0015`) — not an automatic
consequence, and not this package's to assume. It changes what every existing employee is paid for a partial
month, which is a business decision wearing a technical costume.

### The cross-module mechanism is settled by triple precedent

`IEmployeeRoster`, `IJournalPoster` and `InspectPostingWindowAsync` established it three times: **published
contracts, shaped by the consumer, never assembly references** (`ADR-012`). FP-013 will need an
attendance-summary contract that Payroll consumes, and the precedent also fixes its *shape*: **what payroll
calculation actually needs — period totals, not punch-level data.**

A contract exposing raw punches would let every future Payroll feature read minute-by-minute employee
movement **with no call-site change for anyone to review** — the same argument that kept `NationalId` out of
`IEmployeeRoster`.

### Attendance never writes HR

The `DEC-PAY-0014` symmetry. Employee facts come **from** HR through a contract; nothing flows back.
Terminated and inactive employees are handled per `BR-HR-0004`'s **ruled reading** (`OD-PAY-0010`): the rule
bars *new obligations*, not the settlement of obligations already incurred.

### Pattern stack, inherited without argument

Permission grammar `<Plane>.<Resource>.<Action>` with sensitivity splits · unforgeable read scopes ·
append-only where a record says what happened · E3 manifest with the inventory **derived, never copied** ·
`nvarchar` · no cross-database foreign key · migration through `tools/SSAS.Tenant.MigrationTool` **only** ·
and `[property: JsonPropertyName]` **plus** `JsonStringEnumConverter` on every request record.

That last one is not a style note. **It has shipped as a defect twice** — GL's missing attribute made every
write route answer `400 request.invalid`, and Payroll's missing enum converter made
`POST /api/payroll/elements` refuse every well-formed body. Both records read correctly; both faults were an
absence. **Any request record whose binding depends on serializer configuration is invisible to review.**

**No money is expected in this module.** Rates and amounts live in Payroll; attendance records *quantities* —
days, hours, units. If a monetary column appears, that is a signal the module boundary has drifted, not a
detail (`DEC-ATT-0004`).

---

## Package contents

| File | What it holds |
|---|---|
| `README.md` | This: the sweep, the authority base, the inherited section |
| `requirements.md` | Proposed `REQ-ATT-####`, each traced or marked unauthored |
| `business-rules.md` | Proposed `BR-ATT-####` and what they would need from the master rule set |
| `domain-model.md` | Aggregates and the scope question's effect on all of them |
| `data-model.md` | Tables, keys, E3 manifest membership |
| `lifecycle-model.md` | Period states, approval, and the close discipline |
| `authorization-model.md` | Permissions, the read scope, and the org structure's first authorization use |
| `api-contracts.md` | Proposed route surface |
| `acceptance-criteria.md` | `AC-ATT-####` |
| `test-scenarios.md` | `TS-ATT-####` |
| `decisions-open.md` | `DEC-ATT-####` and `OD-ATT-####` |
| `traceability-matrix.md` | Requirement → rule → AC → test → decision, plus the orphan check |

**Nothing here is buildable until `decisions-open.md` is ruled** — and `OD-ATT-0001` alone can halve or
double the package.

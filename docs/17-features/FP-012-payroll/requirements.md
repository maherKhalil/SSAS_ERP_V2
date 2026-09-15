# FP-012 — Requirements

**RATIFIED 2026-08-24.** `Requirement-Catalog/PAY.md` now exists and carries `REQ-PAY-0001`–`0018`;
`REQ-PAY` is indexed in the catalog README beside `REQ-GL` and `REQ-HR`. The gap this page was written
against is closed.

*What follows is the original derivation, kept because the authority column is the evidence for each line
and would otherwise be lost.* When drafted, `REQ-PAY-0001` was a **bare prefix reservation** in
`Requirement-Numbering.md` and there was no `Requirement-Catalog/PAY.md`.

This follows the `GL.md` precedent exactly. FP-011 met the same absence, drafted `REQ-GL-0001`–`0014` as
proposals, and the owner ratified them into the catalog. The same is asked here — and, as there, the
proposed IDs are preserved as drafted so the traceability in this package survives ratification.

**Authority column legend:** *Inherited* — traceable to an existing rule or decision. *Derived* — follows
necessarily from an inherited fact. **Unauthored** — nothing in the specification supports it; it exists
because a payroll without it is not a payroll. Unauthored requirements are the ones the owner is really
being asked about.

---

## Master data

| ID | Requirement | Authority |
|---|---|---|
| `REQ-PAY-0001` | A company shall record an employee's compensation as a dated assignment, so that any past payroll run can be reproduced from the record that was in force. | **Inherited** — `DEC-POS-0023` created this slot deliberately. Shape per `OD-PAY-0003` |
| `REQ-PAY-0002` | Compensation shall be company-scoped and shall never be stored on an HR employee record. | **Inherited** — `DEC-POS-0023`, `DEC-PAY-0014`, `DEC-PAY-0015` |
| `REQ-PAY-0003` | A tenant shall define the pay elements it uses, each classified as an earning or a deduction, each bound to a behaviour implemented by the product. | **Unauthored** — shape per `OD-PAY-0006` |
| `REQ-PAY-0004` | A pay element shall carry an explicit calculation order. | **Derived** — any element computed from another needs a defined sequence (`OD-PAY-0007`) |
| `REQ-PAY-0005` | A pay element shall be mapped to a general ledger account per company, and an unmapped element shall prevent approval of any run containing it. | **Derived** — a posting cannot be composed without it (`OD-PAY-0012`) |
| `REQ-PAY-0006` | Compensation outside the employee's salary grade band shall be recorded and surfaced, not refused. | **Inherited** — `DEC-POS-0023`, `DEC-POS-0027`; ruling at `OD-PAY-0004` |

## The payroll run

| ID | Requirement | Authority |
|---|---|---|
| `REQ-PAY-0007` | A payroll run shall be created for one company and one pay period. | **Unauthored** — frequency model per `OD-PAY-0002` |
| `REQ-PAY-0008` | A run shall include every employee employed for at least one day of the period, including employees terminated within it. | **Inherited (interpretation)** — `BR-HR-0004`; ruling at `OD-PAY-0010` |
| `REQ-PAY-0009` | Calculating a run shall produce, for each included employee, a line per applicable pay element and a net amount. | **Unauthored** |
| `REQ-PAY-0010` | A run shall progress Draft → Calculated → Approved → Posted, and shall not skip a state. | **Unauthored** — `OD-PAY-0009` |
| `REQ-PAY-0011` | Approving a run shall require a permission distinct from the permission to create or calculate one. | **Inherited** — `BR-PLT-0103` names Payroll Processing sensitive; precedent `GL.Drafts.Manage` / `GL.Journals.Post` |
| `REQ-PAY-0012` | A run may be recalculated any number of times before approval, and not at all after posting. | **Inherited** — GL append-only (`DEC-PAY-0012`), ruling at `OD-PAY-0011` |
| `REQ-PAY-0013` | Correcting a posted run shall be achieved by reversing its journal and running again, never by amending it. | **Inherited** — posted journals are append-only |
| `REQ-PAY-0014` | Every run shall retain an append-only record of who calculated, approved and posted it, when, and over what scope. | **Inherited** — `EmployeeImportRun` / `EmployeeExportRun` shape; `BR-PLT-0103` |

## Posting to the general ledger

| ID | Requirement | Authority |
|---|---|---|
| `REQ-PAY-0015` | Posting an approved run shall create one balanced journal in the general ledger for the company and the fiscal period containing the pay date. | **Inherited** — `BR-GL-0001`; `OD-GL-0009` names Payroll the first inbound poster |
| `REQ-PAY-0016` | A run whose target fiscal period is closed shall be refused at approval, naming the period. | **Inherited** — `BR-GL-0003`; ruling at `OD-PAY-0014` |
| `REQ-PAY-0017` | Payroll shall reach the general ledger through a published contract or event only, never an assembly reference. | **Inherited** — `ADR-012`; mechanism at `OD-PAY-0013` |

## Reading pay

| ID | Requirement | Authority |
|---|---|---|
| `REQ-PAY-0018` | An employee's payslip shall be readable as a projection of the stored run lines, under a permission granted separately from any HR permission. | **Derived** — no document store exists (`DEC-PAY-0013`); `DEC-POS-0018` separation precedent (`OD-PAY-0015`, `OD-PAY-0016`) |

---

## Requirements deliberately **not** drafted

Listing these is part of the analysis: a reader must be able to tell the difference between *decided
against* and *overlooked*.

**Tax, statutory deductions, and social insurance.** **RULED `DEC-PAY-0016`: V1 is jurisdiction-neutral —
no tax tables, no statutory deductions.** No requirement is drafted because there is no authority to draft
one from — no jurisdiction is named anywhere in the specification, and these are not
product choices but legal ones. **This is the largest gap between FP-012 and a payroll any organisation
could actually run**, and it is stated here rather than buried so the owner sees it before the build
prompt, not after.

**Overtime, absence deduction, shift differential, lateness.** Excluded by `DEC-PAY-0002`: Attendance is
unbuilt, so the input does not exist. A requirement saying "the system calculates overtime" would read as
buildable and be unimplementable.

**Retroactive pay, advances, loans.** `OD-PAY-0018` — proposed for deferral.

**Payment or disbursement.** No banking integration exists; the product can post a payroll to the ledger
but cannot move money.

**Multi-currency payroll.** `DEC-PAY-0003` — the trigger is not this package's to pull.

**Year-end processing, leave provisioning, end-of-service benefits.** Each is a substantial feature with
no authored requirement; none is implied by anything above.

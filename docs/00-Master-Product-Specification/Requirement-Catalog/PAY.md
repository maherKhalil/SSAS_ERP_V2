# PAY Requirements

Domain

Payroll

Prefix

REQ-PAY

Ratified

2026-08-24, from the FP-012 analysis package, following the GL.md precedent

Boundary

V1 is jurisdiction-neutral. No tax tables and no statutory deductions (DEC-PAY-0016)

---

Compensation

REQ-PAY-0001

Record employee compensation as a dated assignment

REQ-PAY-0002

Keep compensation off every HR record

REQ-PAY-0006

Treat an out-of-band amount as informational

---

Pay Elements

REQ-PAY-0003

Define tenant-configurable pay elements

REQ-PAY-0004

Evaluate pay elements in an explicit order

REQ-PAY-0005

Map every pay element to a ledger account

---

Payroll Run

REQ-PAY-0007

Create a payroll run for one company and one period

REQ-PAY-0008

Include every employee employed within the period

REQ-PAY-0009

Calculate a line per applicable element and a net amount

REQ-PAY-0010

Progress a run through Draft, Calculated, Approved and Posted

REQ-PAY-0011

Require a distinct permission to approve a run

REQ-PAY-0012

Recalculate before approval and never after posting

REQ-PAY-0013

Correct a posted run by reversal

REQ-PAY-0014

Retain an append-only record of every run

---

Ledger Posting

REQ-PAY-0015

Post an approved run as one balanced journal

REQ-PAY-0016

Refuse a run whose target fiscal period is closed

REQ-PAY-0017

Reach the ledger through a published contract only

---

Payslips

REQ-PAY-0018

Read a payslip as a projection of stored run lines

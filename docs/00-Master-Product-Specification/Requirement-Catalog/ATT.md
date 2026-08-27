# ATT Requirements

Domain

Attendance and Leave

Prefix

REQ-ATT

Ratified

2026-08-25, from the FP-013 analysis package, following the GL.md and PAY.md precedent

Boundary

Attendance and leave in one module, sequenced: attendance core first, the leave entitlement ledger second. One working calendar and one approval shape serve both (OD-ATT-0001).

Leave balances are ADMINISTERED. Accrual rules are deferred (OD-ATT-0006).

Overtime is RECORDED with a tier label. Every rate stays in Payroll (OD-ATT-0008).

No hardware, biometric or geofenced device capture (OD-ATT-0016).

No self-service. The identity-to-employee mapping now exists (`UserEmployeeLink`, ADR-030) but nothing here reads it, and no self-service permission or route exists (OD-ATT-0013).

---

Working Calendar

REQ-ATT-0001

Maintain a company working calendar whose weekend pattern is data

REQ-ATT-0002

Maintain a dated holiday list on the calendar

REQ-ATT-0003

Answer how many working days a date range contains

---

Attendance Capture

REQ-ATT-0004

Record time worked for one employee on one date

REQ-ATT-0005

Record attendance only for employees within the recorder's authority

REQ-ATT-0006

Refuse attendance dated outside the employment window

REQ-ATT-0007

Record overtime as a quantity with a tier label

REQ-ATT-0008

Record paid and unpaid absence as separate quantities

REQ-ATT-0009

Record quantities only, never money

---

Leave

REQ-ATT-0010

Maintain a configurable leave type catalog with a closed behaviour set

REQ-ATT-0011

Hold an administered balance per employee and leave type

REQ-ATT-0012

Submit a leave request naming a type and a date range

REQ-ATT-0013

Consume working days, never calendar days

REQ-ATT-0014

Decide a request through an approver who is not the requester

REQ-ATT-0015

Decrement the balance on approval only

REQ-ATT-0016

Cancel a request, with different rules before and after its dates

REQ-ATT-0017

Refuse a leave request outside the employment window

---

Period and Payroll Boundary

REQ-ATT-0018

Organise attendance into periods that a permitted user closes

REQ-ATT-0019

Record a correction to a closed period as a new adjustment, never an edit

REQ-ATT-0020

Publish a per-period summary contract carrying totals, never punch-level data

REQ-ATT-0021

Let a caller inspect a period's state and receive a modelled outcome

REQ-ATT-0022

Consume attendance in Payroll through the contract alone

---

Reading

REQ-ATT-0023

Defer self-service — the identity-to-employee mapping it waited on now exists (ADR-030); the permission and route do not

REQ-ATT-0024

Read attendance within the caller's company and branch authority

REQ-ATT-0025

Separate leave type from leave occurrence in the permission model

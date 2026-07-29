# Business Rules

**Document ID:** MPS-BR-001

**Version:** 1.0

---

# Purpose

Business Rules define mandatory constraints governing business operations.

Unlike Functional Requirements, Business Rules remain stable even if the implementation changes.

Business Rules are referenced by:

- Functional Specifications
- APIs
- Database Design
- UI Specifications
- Test Cases

Every Business Rule receives a permanent identifier.

---

# Rule Numbering

```
BR-PLT-0001
BR-HR-0001
BR-GL-0001
```

Business Rule identifiers shall never be reused.

---

# Platform Business Rules

## BR-PLT-0001

### Title

Tenant Data Isolation

### Description

A tenant shall never access, modify, report, export, or search data belonging to another tenant.

### Applies To

Entire Platform

### Priority

Critical

### Related Requirements

REQ-PLT-0001

---

## BR-PLT-0002

### Title

Company Data Isolation

A company user shall only access companies explicitly assigned to them.

---

## BR-PLT-0003

### Title

Soft Delete

Business entities shall not be physically deleted unless explicitly configured.

Deleted entities shall remain available for auditing.

---

## BR-PLT-0004

### Title

Audit Trail

Every business transaction shall create an immutable audit record.

The audit record shall include:

- User
- Date
- Time
- Company
- Tenant
- Action
- Entity
- Old Values
- New Values

---

## BR-PLT-0005

### Title

UTC Storage

All timestamps shall be stored in UTC.

Presentation shall use the user's configured time zone.

---

## BR-PLT-0006

### Title

Numbering Sequences

Document numbering shall be configurable per company.

Example

Employee Number

Journal Number

Invoice Number

Purchase Order Number

---

## BR-PLT-0007

### Title

Localization

Languages

Currencies

Date Formats

Number Formats

Time Zones

shall be configurable independently for every company.

---

## BR-PLT-0008

### Title

Feature Enablement

Modules may be enabled or disabled per subscription plan.

Disabled modules shall not appear in menus or APIs.

---

# Security Rules

## BR-PLT-0100

Authentication is mandatory.

Anonymous users cannot access business resources.

---

## BR-PLT-0101

Authorization shall be Role Based.

---

## BR-PLT-0102

Permissions are additive.

Users inherit permissions from assigned roles.

---

## BR-PLT-0103

Sensitive operations require elevated permissions.

Examples

Delete

Reverse Journal

Close Fiscal Year

Payroll Processing

---

# HR Business Rules

## BR-HR-0001

Employee Number

Employee Number shall be unique within a company.

---

## BR-HR-0002

National ID

National ID shall be unique within a company.

---

## BR-HR-0003

Employment Date

Employment Date cannot be later than Termination Date.

---

## BR-HR-0004

Termination

A terminated employee cannot be assigned new business transactions.

---

## BR-HR-0005

Department

Every employee belongs to exactly one department.

---

## BR-HR-0006

Position

Every employee must have one active position.

---

## BR-HR-0007

Manager

An employee cannot directly manage themselves.

---

## BR-HR-0008

Department Hierarchy

Circular department hierarchies are prohibited.

---

## BR-HR-0009

Inactive Departments

Inactive departments cannot receive new employees.

---

# General Ledger Business Rules

## BR-GL-0001

Every Journal Entry must balance.

Debit Total = Credit Total.

---

## BR-GL-0002

Posted Journals cannot be edited.

---

## BR-GL-0003

Closed Fiscal Periods prohibit posting.

---

## BR-GL-0004

Accounts marked as inactive cannot receive transactions.

---

## BR-GL-0005

Journal Numbers are unique within Fiscal Year.

---

# Reporting Rules

## BR-RPT-0001

Reports shall only display data authorized for the current user.

---

## BR-RPT-0002

Reports shall always respect Tenant and Company boundaries.

---

# Future Modules

Business Rules for the following modules will be added in future releases:

- Payroll
- Recruitment
- Attendance
- Procurement
- Inventory
- CRM
- Projects
- Manufacturing
- Fixed Assets
- Budgeting

---

# Rule Lifecycle

Every Business Rule has one of the following statuses:

- Draft
- Approved
- Deprecated
- Replaced

Deprecated rules remain documented for historical traceability.
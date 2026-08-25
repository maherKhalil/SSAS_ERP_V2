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
BR-SUB-0001
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

## BR-PLT-0009

### Title

Tenant Branch Onboarding

### Description

A tenant shall retain at least one active branch once branch onboarding is complete.

Zero active branches is a provisioning state only. An administrator shall not return a tenant to it by deactivating the last active branch.

### Applies To

Platform

### Priority

Critical

### Related Requirements

REQ-PLT-0060, REQ-PLT-0061

---

## BR-PLT-0010

### Title

Mandatory Branch Assignment

### Description

An active normal tenant user shall be authorized for at least one active branch.

The rule is enforced when the user is created and whenever branch assignments are edited. It shall not be deferred to login.

### Applies To

Platform

### Priority

Critical

### Related Requirements

REQ-PLT-0062

---

## BR-PLT-0011

### Title

Tenant Administrator Branch Scope

### Description

A holder of tenant administration authority shall have access to all active branches of the current tenant, derived from authority rather than stored assignments.

No branch assignment records shall be created for tenant administrators.

### Applies To

Platform

### Priority

High

### Related Requirements

REQ-PLT-0062, REQ-PLT-0063

---

## BR-PLT-0012

### Title

Branch Selection

### Description

A user authorized for exactly one active branch shall enter that branch automatically.

A user authorized for more than one active branch shall select a branch explicitly. The selection shall not be skippable, and branch-scoped operations shall be refused until it is made.

### Applies To

Platform

### Priority

Critical

### Related Requirements

REQ-PLT-0064

---

## BR-PLT-0013

### Title

Branch Transaction Ownership

### Description

Every branch-owned business transaction shall belong to exactly one active branch.

The branch shall be assigned by the server from the authenticated session context. It shall never be accepted from client-supplied request data, and shall not change after the record is created.

### Applies To

Platform, HR, General Ledger, Sales, Inventory

### Priority

Critical

### Related Requirements

REQ-PLT-0065

---

## BR-PLT-0014

### Title

Branch Authorization Freshness

### Description

Authorization for the active branch shall be re-evaluated against live state on every branch-owned write and at every branch switch.

A recorded active branch is execution context, not proof of authorization. Revoked access, revoked authority, a deactivated branch, or a revoked or expired session shall each refuse the operation.

### Applies To

Platform

### Priority

Critical

### Related Requirements

REQ-PLT-0066

---

## BR-PLT-0015

### Title

Branch Retirement

### Description

Branches shall be deactivated, never deleted.

Deactivation shall be refused when it would remove the tenant's only active branch, retire the active main branch without a named replacement, or leave an active normal user with no active branch.

Branch assignments shall be retained through deactivation so that reactivation restores prior access. A retained assignment shall grant no access while its branch is inactive.

### Applies To

Platform

### Priority

High

### Related Requirements

REQ-PLT-0061, REQ-PLT-0067

---

## BR-PLT-0016

### Title

Branch Reporting Scope

### Description

Reports over branch-owned data shall be scoped to the current branch or to an explicitly authorized set of branches.

"All branches" shall mean all branches currently authorized to the requesting user. A report shall never be produced by omitting the branch predicate.

### Applies To

Platform, Reporting

### Priority

Critical

### Related Requirements

REQ-PLT-0067

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

# Subscription Business Rules

Rules `BR-SUB-0001`–`BR-SUB-0021`, promoted from `FP-014` at its ratification under `DEC-L-012`.
The rule statements are carried **as `FP-014` states them**; the titles are section labels added here.
`FP-014`'s [`business-rules.md`](../17-features/FP-014-subscription/business-rules.md) records each
rule's authority and the decisions it derives from.

---

## BR-SUB-0001

### Title

One Subscription In Force

A tenant has **at most one subscription in force at any instant**. The record in force at instant `T` is the one with the greatest `EffectiveFromUtc` not later than `T` — derived by ordering, never stored

---

## BR-SUB-0002

### Title

Subscription History Is Append-Only

A subscription record is **never modified and never deleted**. A plan change, a renewal and a billing-currency change are each a new record

---

## BR-SUB-0003

### Title

History Is Appended, Never Inserted Into

A new subscription record's `EffectiveFromUtc` is **strictly greater** than that tenant's current maximum. History is appended to, never inserted into

---

## BR-SUB-0004

### Title

Subscription Administration Is Platform-Plane

**No tenant-plane actor may create, amend or delete** a subscription, plan, grant or invoice, whatever permissions it holds

---

## BR-SUB-0005

### Title

Entitlement Grants Only Raise

An entitlement grant may only **raise**. Resolved entitlement is `plan ∪ grants` for modules and `max(plan, grants)` for every cap

---

## BR-SUB-0006

### Title

Metering Is Judged Against The Record In Force

A metered quantity is judged against **the subscription record in force when the quantity was observed**, not against the record in force now

---

## BR-SUB-0007

### Title

Unentitled Modules Are Refused Before The Handler

A request to a route belonging to a module the tenant is not entitled to is **refused with `403`** before the handler runs

---

## BR-SUB-0008

### Title

Platform-Plane Routes Are Never Gated

**Platform-plane routes are never subject to module enablement** — authentication, tenant selection, refresh, logout, platform support and the subscription surface itself stay reachable

---

## BR-SUB-0009

### Title

Disabled-Module Permissions Are Ineffective

A permission belonging to a module the tenant is not entitled to is **neither grantable nor effective**, so a stale role assignment cannot reach a disabled module

---

## BR-SUB-0010

### Title

Losing Entitlement Never Destroys Data

Losing entitlement to a module **does not delete, alter or hide** the tenant's data in it. The data is unreachable, not destroyed, and returns intact on re-entitlement

---

## BR-SUB-0011

### Title

Entitlement Is Never A Token Claim

**Entitlement never appears in an access token** and is resolved server-side on every request

---

## BR-SUB-0012

### Title

Entitlement Changes Take Effect Immediately

An entitlement change **takes effect without re-issuing a token and without restarting the host**. The cache is invalidated on change and never refreshed on a timer

---

## BR-SUB-0013

### Title

Expiry Refuses Login, Distinctly

A tenant whose subscription term has **expired cannot log in**, and that refusal is **distinct** from a refusal on tenant status

---

## BR-SUB-0014

### Title

Subscription State And Tenant Status Are Orthogonal

Subscription state and `TenantStatus` are **orthogonal**. Expiry never writes `TenantStatus`, and no commercial reason is added to `TenantStatusChangeReason`

---

## BR-SUB-0015

### Title

No Subscription Means No Entitlement

A tenant with **no subscription record has no entitlement** and reaches no gated module. There is no default plan

---

## BR-SUB-0016

### Title

Plans Are Retired, Never Deleted

A plan is **retired, never deleted**, because historical subscription records reference it

---

## BR-SUB-0017

### Title

Issued Invoices Are Never Edited

An **issued invoice is never edited**. A correction is a credit note, never an amendment

---

## BR-SUB-0018

### Title

Invoice Numbers Are Never Reused

An **invoice number is never reused**, including the number of a voided invoice

---

## BR-SUB-0019

### Title

Tenants Read Modules, Not Commercial Terms

A tenant may read **which modules it has**; it may not read price, invoice, payment state or any other commercial term

---

## BR-SUB-0020

### Title

No Cardholder Data

**No cardholder datum is stored, transmitted in any request or response, or logged** anywhere in SSAS

---

## BR-SUB-0021

### Title

Seat Caps Are Enforced At Admission

A seat cap is enforced **at admission and nowhere else**. Creating or activating a user beyond the tenant's resolved cap is refused **at that moment**, naming the cap, the current count and the plan. **Login is never refused for a seat cap.** An excess arriving by plan downgrade is **billed and reported**, never enforced against anyone already working

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
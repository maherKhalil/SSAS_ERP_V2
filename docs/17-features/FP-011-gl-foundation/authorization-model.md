---
package: FP-011
title: General Ledger — Authorization Model
status: DRAFT — permission set proposed; scope shape conditional on OD-GL-0003 and OD-GL-0005
version: 0.1
date: 2026-08-23
---

# FP-011 — Authorization Model

> GL inherits an authorization model that is finished. Nothing here is new mechanism; the work is choosing
> the permission set and deciding how many dimensions a GL read resolves.

## The two axes, which are independent

`HrPermissionNames` states the separation exactly, and it is the single most important thing to carry into
GL:

> Holding one says which OPERATION is permitted. It says nothing about which companies or branches are
> reachable [...] Conversely `Platform.Tenant.Administer` widens those scopes and grants NONE of these: an
> administrator without ViewEmployees cannot read an employee (ADR-025 decision 8).

So a tenant administrator does **not** implicitly get to post journals. That has to be true in GL too, and it
is the kind of property that quietly stops being true if someone adds a convenience check.

| Axis | What it answers | Resolved by |
|---|---|---|
| **Functional permission** | May this caller perform this operation at all? | The permission catalog |
| **Scope** | Which tenant, companies and branches are reachable? | `ITenantCompanyAccessResolver`, `ITenantBranchAccessResolver` |

## Proposed permission set — `DEC-GL-0003`

`<Plane>.<Resource>.<Action>`, exactly three ASCII-identifier segments, matching the platform grammar so no
framework change is needed. `Requirement-Numbering.md`'s example already names `PER-GL-PostJournal`, so the
resource-and-action shape below is consistent with the reservation.

| Permission | Operation | Requirement |
|---|---|---|
| `GL.Journals.View` | Read journals and lines | `REQ-GL-0012` |
| `GL.Journals.Post` | Record and post a journal | `REQ-GL-0001` |
| `GL.Journals.Reverse` | Post a reversing journal | `REQ-GL-0004` |
| `GL.Accounts.View` | Read the chart of accounts | `REQ-GL-0008` |
| `GL.Accounts.Manage` | Create, update, deactivate accounts | `REQ-GL-0005`–`0007` |
| `GL.Periods.View` | Read the fiscal calendar | `REQ-GL-0009` |
| `GL.Periods.Manage` | Define years and periods, close periods | `REQ-GL-0009`, `REQ-GL-0010` |
| `GL.Reports.View` | Balance enquiry and trial balance | `REQ-GL-0013`, `REQ-GL-0014` |

**Open questions this set deliberately does not answer:**

* **Does `GL.Journals.Reverse` need to be separate from `Post`?** A reversal is a posting, so the separate
  permission only earns its place if the product wants to let someone post but not reverse. Raised here rather
  than assumed.
* **Should `GL.Reports.View` exist, or should reporting ride on `GL.Journals.View`?** FP-009's `DEC-DOC-0015`
  faced the identical question for export and **declined** the additive permission for V1, on the reasoning
  that a separate permission is not justified while both paths share one predicate and neither can read more
  than the other. If trial balance reads exactly what journal search can read, the same reasoning declines
  `GL.Reports.View`. If it aggregates across companies a user cannot individually see, it does not — and that
  is a real difference worth checking rather than assuming.

**Naming is not registering.** A `GlPermissionCatalogContributor` must define every name above and the Host
must register it. FP-006P is the recorded precedent for what happens otherwise: the constants existed, no
catalog defined them, no role could hold one, and **every endpoint refused every caller**. The failure is
total and silent, which is why it is worth repeating here.

## Reads — the unforgeable scope, `DEC-GL-0004`

`EmployeeReadScope` is the pattern and its own comment is the specification:

> HOLDING ONE OF THESE IS PROOF THAT ALL THREE DIMENSIONS WERE CHECKED, LIVE, JUST NOW.
>
> It cannot be constructed: the constructor is private and the only factory is internal to this assembly,
> called from exactly one place [...]
>
> EVERY EMPLOYEE READ REQUIRES ONE. That is the whole design: a read that omitted a scope predicate is not
> something a reviewer has to notice, because it is not something a caller can express.

`JournalReadScope` follows it exactly:

* private constructor; one internal factory; one resolver that checks the functional permission and resolves
  the authorized sets against **live** state and refuses if any dimension fails;
* **materialized identifier sets, never modes** — "all companies" is a list, not the absence of a predicate
  (`BR-PLT-0016`, `ADR-023` decision 22);
* **an empty set refuses the read** at construction, so `WHERE CompanyId IN ()` is unrepresentable rather than
  guarded against (`ADR-025` decision 10).

**How many dimensions?** Two (tenant, company) if `OD-GL-0005` says no branch dimension; three if it adds one.
The scope type's shape follows that answer, which is why `DEC-GL-0004` is drafted conditionally.

## Writes

Company authorization runs at the write boundary, before persistence, for any `ICompanyOwnedEntity`. For GL
that means:

| Write | Company-scoped? |
|---|---|
| Post a journal | **Yes** — `JournalEntry` is company-owned |
| Create or update an account | **Only under `OD-GL-0003` option 2.** Under option 1 the chart is tenant-level and account maintenance is a tenant-level write, authorized by permission alone |
| Close a fiscal period | **Depends on `OD-GL-0004`** by the same mechanism |

**This is the part most likely to be got wrong by reading the schema instead of the model.** `CompanyId`
appearing on a table is not a column decision — it is `ICompanyOwnedEntity`, and it changes what
`SaveChangesAsync` does before it reaches SQL.

## What must be tested at the boundary, not the handler

Everything in this document is enforced by code that a handler test does not execute. The scenarios in
[test-scenarios.md](test-scenarios.md) that matter most are the ones that go through the real
`TenantDbContext` against real SQL:

* a caller with `GL.Journals.Post` but no authorized company is refused **at the write boundary**;
* a scope resolved for company A cannot read company B's journals, and the refusal is not a filtered empty
  result but a refusal;
* `Platform.Tenant.Administer` alone posts nothing;
* a permission constant that no catalog defines authorizes nothing — the FP-006P failure, asserted rather
  than remembered.

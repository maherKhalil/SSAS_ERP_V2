# Branch Management

Document ID

DOC-FS-PLT-004

Version

1.0

Status

Approved

Module

Platform (MOD-PLT)

Reference

ADR-023

---

# Purpose

This document describes the functional behaviour of branch management: how a tenant's operating locations are created and retired, how users are authorized for them, how a session establishes and changes its active branch, and how branch scope applies to business data and reports.

A **branch** is an operating location inside a tenant — a head office, an outlet, a depot. It is not a legal entity; that is a Company (`ADR-014`). A tenant may have both, and they are independent dimensions.

Branch scope is enforced by the server. The user interface reflects it; it does not enforce it.

---

# Scope

## In scope

- Branch creation, rename, deactivation, and main-branch designation
- First-branch onboarding
- Mandatory branch assignment for tenant users
- Branch selection and switching within an authenticated session
- Branch scope of business data and reports

## Not in scope

- Branch administration screens (deferred; the server behaviour described here exists, the UI does not)
- Branch-scoped reporting queries (deferred)
- Cross-branch transfers, consolidations, or branch hierarchies (not designed)

---

# Concepts

| Concept | Meaning |
|---------|---------|
| Branch | An operating location inside a tenant |
| Main branch | The tenant's primary branch. At most one active main branch per tenant |
| Branch assignment | A record authorizing one tenant user to work in one branch |
| Authorized branches | The branches a user may currently enter — always intersected with active branches |
| Active branch | The branch the current session is working in |
| Branch-owned data | Business data that belongs to exactly one branch |
| Tenant-global data | Tenant data that has no branch — Company, Branch itself, users, roles |

---

# Branch lifecycle

## Create

A tenant administrator creates a branch with a **code** and a **name**, and may designate it as the main branch.

Rules:

- Requires `Platform.Tenant.Administer` in the current tenant.
- The branch code is unique within the tenant.
- A tenant may have at most one **active main** branch.
- The tenant is taken from the authenticated context, never from the request. An administrator of one tenant can never create a branch in another.

Refusals: `Branch.InvalidCode`, `Branch.InvalidName`, `Branch.CodeAlreadyExists`, `Branch.MainBranchAlreadyExists`, `Branch.TenantAdministratorRequired`, `Branch.TopologyBusy`.

## Rename

Code and name may be changed on an **active** branch. The request must carry the branch's `RowVersion`; an update without it would be a last-writer-wins edit of a record two administrators can hold open at once.

Refusals: `Branch.Inactive`, `Branch.CodeAlreadyExists`, `Branch.ConcurrencyConflict`.

## Main-branch switching

Promoting a different branch to main demotes the current one. Both changes happen in **one transaction**, in demote-flush-promote order. The database refuses the moment two active branches are main.

## Deactivate

Branches are **deactivated, never deleted**. A branch identifier is referenced from the platform database and from every document produced while the branch was active; removing the row would strand both and make historical data unexplainable.

Deactivation is refused when:

| Condition | Refusal |
|-----------|---------|
| It is the tenant's only active branch | `Branch.CannotDeactivateOnlyActiveBranch` |
| It is the active main branch and no replacement is named | `Branch.ReplacementMainBranchRequired` |
| An active normal user would be left with no active branch | `Branch.DeactivationWouldStrandUsers` |
| The branch is already inactive | `Branch.AlreadyInactive` |
| Another branch administration operation is running for this tenant | `Branch.TopologyBusy` |

`Branch.TopologyBusy` is **retryable**. Nothing was attempted and nothing was lost; the caller should simply retry.

Assignment rows naming a deactivated branch are **retained**, so that reactivating the branch restores the access that existed before it. They grant no access while the branch is inactive.

## Reactivate

**Not exposed yet.** The branch aggregate supports reactivation, but no branch administration operation offers it. The retention rule above exists so that reactivation, when it is exposed, restores the access that existed before deactivation without a backfill.

## List

The administration view may include inactive branches, so that a branch appearing on old documents but no longer selectable can still be explained.

---

# First-branch onboarding

A newly provisioned tenant has no branches. Zero branches is a **provisioning state**, not a state an administrator may return to later by deactivating the last one.

| Principal | Tenant has zero active branches | Result |
|-----------|-------------------------------|--------|
| Tenant Administrator | Yes | `FirstBranchRequired` — the administrator is directed to create the first branch |
| Normal user | Yes | The account cannot be used |

Creating a branch is tenant-global work, so it remains possible with no branch selected. Requiring an active branch in order to create the very first branch would be unsatisfiable.

---

# User branch assignment

## Rule

**An active normal tenant user must be authorized for at least one active branch.** This is enforced at user creation and at every edit of a user's branch assignments — not surfaced later as an error the user discovers at login.

## Who gets what

| Principal | Authorized branches | Assignment rows |
|-----------|--------------------|-----------------|
| Holder of `Platform.Tenant.Administer` | All **active** branches of the tenant | None. Scope is derived from authority |
| Normal tenant user | Assigned branches, intersected with active branches | One row per branch |

A tenant administrator's scope follows the estate automatically: branches created later appear in scope with no backfill.

## Validation

Branch identifiers supplied when assigning are **claims, not facts**. They are validated against the tenant database before any assignment is written: the branch must exist, belong to the current tenant, and be active.

All three failures return the same generic refusal, `Branch.AssignmentInvalid`. Distinguishing them would let an administrator of one tenant probe another tenant's branch identifiers for existence.

## Refusals

`Branch.UserMustHaveAtLeastOneBranch`, `Branch.AssignmentInvalid`, `Branch.TopologyBusy`.

---

# Branch selection in a session

Branch resolution happens **after** authentication and tenant resolution, because branches live in the tenant's own database and cannot be enumerated until routing has resolved.

## Outcomes

| Authorized branches | Principal | Outcome | What the user sees |
|---------------------|-----------|---------|--------------------|
| 0 | Tenant Administrator | `FirstBranchRequired` | Create the first branch |
| 0 | Normal user | `AccountIntegrityFailure` | Refused. The account cannot be used |
| 1 | Any | `Active` | Entered directly; no prompt |
| More than 1 | Any | `BranchSelectionRequired` | Must choose before branch-scoped work |

There is **no skip** on selection. Until a branch is chosen, the session has no active branch and every branch-owned write is refused. A user authorized for several branches is authenticated but not yet working anywhere.

A normal user reaching zero authorized branches means the account has been left in a state the assignment rule says is unreachable. It is refused rather than presented as an empty branch picker — an empty list must never be read as "no restrictions".

## Switching

Switching is the same operation as selecting, made later. Authorization is **re-asked at switch time**: access may have been revoked, or the branch deactivated, since the session started.

A refused switch leaves the current active branch **unchanged**. The user keeps working where they were.

Refusals: `Branch.InvalidSelection`, `Branch.SelectionRequired`, `Branch.ContextRequired`.

---

# Branch scope of business data

## Classification

Every tenant entity is explicitly classified:

| Class | Carries `BranchId` | Examples |
|-------|-------------------|----------|
| Branch-owned | Yes | Business transactions and documents (future: Employee, journals, invoices, stock movements) |
| Tenant-global | No | Company, Branch, users, roles, permissions, localization |

There is no default. An entity that should have been branch-scoped and was not is readable by every branch in the tenant, and nothing about it looks wrong.

## How a branch-owned record gets its branch

The server assigns it from the session's active branch. The client never supplies it.

| Attempt | Result |
|---------|--------|
| Create a record with no branch supplied | Stamped with the session's active branch |
| Create a record naming a different branch | Refused |
| Change an existing record's branch | Refused |
| Update or delete a record belonging to another branch | Refused |
| Any branch-owned write with no branch selected | Refused (`Branch.SelectionRequired`) |
| Any branch-owned write after access was revoked | Refused |
| Any branch-owned write after the session was revoked or expired | Refused |

Authorization is re-checked on **every** branch-owned write, against live state. A user whose branch access was revoked cannot complete the next write, even while holding a token issued before the revocation.

Tenant-global writes are unaffected and remain possible with no branch selected.

---

# Reporting

**Not yet implemented.** This section states the required behaviour for the first branch-scoped report.

A report runs over one of two scopes:

- the **current branch**; or
- an **explicitly authorized branch set**.

"All branches" always means *all branches currently authorized to this user*. It must never be implemented by omitting the branch predicate.

Reports continue to respect tenant and company boundaries (`BR-RPT-0002`).

---

# User interface expectations

Deferred, but constrained when built:

- The active branch is visible wherever branch-owned work happens.
- A switcher is offered only when the user is authorized for more than one branch.
- Branch-scoped screens must handle a refusal caused by revoked access or a deactivated branch — the server can refuse at any time.
- The switcher is a convenience, not a control. Server APIs requiring branch context refuse when no active branch is established, regardless of what the client renders.

---

# Error reference

| Error | Meaning |
|-------|---------|
| `Branch.InvalidCode` / `Branch.InvalidName` | The supplied code or name is invalid |
| `Branch.InvalidActor` | A trusted lifecycle actor is required |
| `Branch.NotFound` | The branch was not found |
| `Branch.Inactive` | The branch is not active |
| `Branch.CodeAlreadyExists` | The branch code already exists in the tenant |
| `Branch.MainBranchAlreadyExists` | The tenant already has an active main branch |
| `Branch.AlreadyInactive` | The branch is already inactive |
| `Branch.FirstBranchRequired` | The tenant has no active branch; an administrator must create the first |
| `Branch.SelectionRequired` | An active branch must be selected before branch-scoped operations |
| `Branch.InvalidSelection` | The selected branch is not available to this user |
| `Branch.ContextRequired` | A trusted branch context is required for branch-owned data |
| `Branch.UserMustHaveAtLeastOneBranch` | A tenant user must be authorized for at least one active branch |
| `Branch.AssignmentInvalid` | One or more requested branches are not assignable |
| `Branch.DeactivationWouldStrandUsers` | Deactivation would leave a user with no active branch |
| `Branch.CannotDeactivateOnlyActiveBranch` | The tenant's only active branch cannot be deactivated |
| `Branch.ReplacementMainBranchRequired` | Deactivating the main branch requires a replacement |
| `Branch.ConcurrencyConflict` | The branch was modified concurrently; reload and retry |
| `Branch.TopologyBusy` | Another branch administration operation is in progress; retryable |
| `Branch.AccountIntegrityFailure` | The account has no active branch and cannot be used |
| `Branch.TenantAdministratorRequired` | Tenant administrator authority is required |

Errors never disclose which database holds which record, and never reveal whether a branch identifier exists in another tenant.

---

# Implementation status

**Implemented:** branch lifecycle, first-branch onboarding, mandatory user branch assignment, session branch resolution, selection and switching, and write-time reauthorization of branch-owned data.

**Deferred:** branch administration UI, the first branch-owned business entity (Employee), branch-scoped reporting, and HTTP functional authorization for user-management commands.

See `ADR-023` for the decision record and the full status classification.

---

# Related Documents

- ADR-023 – Tenant Branch Model, Authorization and Execution Context
- ADR-014 – Company / Legal-Entity Ownership and Scoping
- ADR-017 – Tenant Storage Topology and Routing
- `docs/02-Functional/Platform/Tenant-Management.md`
- `docs/02-Functional/Platform/Authentication.md`
- BR-PLT-0009 … BR-PLT-0016
- REQ-PLT-0060 … REQ-PLT-0067

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-18 | Solution Architecture Team | Initial branch management functional specification. |

---
document_id: FP-006-LIFECYCLE
title: HR Employee Lifecycle Model
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Lifecycle Model

> Approved for Implementation — model reflecting the settled FP-006A decisions.

## Status semantics

### Active

The initial state of every created Employee, and the state an Employee returns to when reactivated. An `Active` Employee is currently employed and may be updated, transferred, and assigned business transactions.

### Inactive

A currently-employed Employee who is temporarily not in service — for example unpaid leave or suspension. The employment relationship persists: the Employee retains its company, its branch, its employee number, and its history. An `Inactive` Employee may be updated, transferred, activated, or terminated, but is not treated as available for new business transactions by modules that check availability.

`Inactive` is **not** termination and is fully reversible.

### Terminated

Employment has ended. The Employee is permanently retired, retained for history and reporting, and cannot transition again. A `Terminated` Employee cannot be updated, transferred, or assigned new business transactions (`BR-HR-0004`).

## Initial-state decision

Creation begins in `Active`. An employee is hired into employment, so existence and availability coincide and no separate activation step is meaningful.

This deliberately differs from `Company`, which is created `Inactive` (`BRULE-CMP-0001`, `DEC-CMP-0011`). That rule exists because a company may exist before its configuration prerequisites — fiscal calendar, chart of accounts, numbering — are ready. An Employee has no such prerequisites in Milestone 1, and creating an employee who must then be separately activated would introduce a state with no business meaning.

## Transition matrix

| Current | Operation | Next | Reason code |
|---|---|---|---|
| None | Create | Active | `Created` |
| Active | Deactivate | Inactive | non-`Created` |
| Inactive | Activate | Active | non-`Created` |
| Active | Terminate | Terminated | non-`Created` |
| Inactive | Terminate | Terminated | non-`Created` |
| Terminated | Any transition | Rejected | — |

All unlisted transitions are rejected, including activating an already-`Active` Employee, deactivating an already-`Inactive` Employee, and any transition out of `Terminated`.

## Enablement is a single reversible pair

`Deactivate` (`Active` to `Inactive`) and `Activate` (`Inactive` to `Active`) are the two directions of one reversible pair. `Activate` serves re-enablement only, because a created Employee is already `Active`. No separate `Reactivate` command or route is defined, following the `BRULE-CMP-0005` precedent.

## Transfer is not a lifecycle transition

**Transfer changes `BranchId`. It changes no status and appears nowhere in the transition matrix.**

Transfer and lifecycle are independent axes: an Employee's branch is *where they work*, and their status is *whether they are employed and available*. Conflating them would make a transfer look like a state change to every consumer of lifecycle events, and would make the transition graph depend on branch topology.

### Transfer permitted by lifecycle state

| Status | Transfer permitted | Reason |
|---|---|---|
| `Active` | **Yes** | The ordinary case |
| `Inactive` | **Yes** | The employment relationship persists, and an employee on leave may still be reassigned — notably when their branch is closing (`BRULE-EMP-0021`) |
| `Terminated` | **No** | Employment has ended; there is no current branch assignment to move (`BRULE-EMP-0017`) |

## Creation

`CreateEmployee`:

1. receives employee number, full name, employment date, and optional national ID;
2. validates and normalizes the employee number, trims the name, validates the employment date, and validates and normalizes the national ID when present;
3. verifies normalized employee-number uniqueness within the current company, and normalized national-ID uniqueness when present;
4. generates a nonempty Guid `EmployeeId` server-side;
5. adopts the trusted current `TenantId` (server-assigned; never client-supplied);
6. adopts the trusted current `CompanyId` from the company execution context, confirming rather than trusting any supplied value (`ADR-025`);
7. receives its `BranchId` by server stamping from the trusted branch execution context, which confirms rather than trusts any supplied value (`ADR-023`);
8. creates the aggregate in `Active`;
9. records trusted UTC and actor metadata and reason code `Created`;
10. produces the initial branch-assignment record with `SourceBranchId = null`, `DestinationBranchId` equal to the stamped branch, and reason `InitialAssignment`;
11. raises `EmployeeCreated`;
12. persists the Employee and its initial assignment record through one tenant Unit of Work, in one transaction.

Creation provisions no user account, no department, no position, no manager, and no document. An Employee is not necessarily an application User (Glossary).

## Update profile

`UpdateEmployeeProfile` changes only `FullName` and `NationalId`, using optimistic concurrency. `EmployeeId`, `TenantId`, `CompanyId`, `BranchId`, `EmployeeNumber`, and `Status` are not updatable through this operation and are absent from its contract. A successful update raises `EmployeeProfileUpdated`.

Profile update is rejected for a `Terminated` Employee (`BRULE-EMP-0004`).

## Activation and deactivation

Deactivation is accepted only from `Active`; activation only from `Inactive`. Each is explicit, carries a non-`Created` reason code, uses optimistic concurrency, and becomes authoritative only after successful persistence. Neither changes company, branch, or any identity field, and neither writes a branch-assignment record.

## Termination

`TerminateEmployee` is accepted from `Active` or `Inactive` and is terminal.

It requires a `TerminationDate` that is not earlier than the `EmploymentDate` (`BR-HR-0003`, `BRULE-EMP-0011`) and a non-`Created` reason code — typically `Resignation`, `Dismissal`, or `EndOfContract`.

Termination does not erase or anonymize the Employee, does not release the employee number for reuse, and does not write a branch-assignment record. The Employee retains its final `BranchId` and its complete branch history, so historical reporting over periods before termination remains correct.

### Post-termination behavior

A `Terminated` Employee:

- **cannot** be updated, activated, deactivated, transferred, or deleted;
- **cannot** be assigned new business transactions by any module (`BR-HR-0004`);
- **remains** retrievable by `EmployeeId` and returnable by search, subject to the ordinary company and branch scope predicates;
- **remains** subject to employee-number and national-ID uniqueness, so its identifiers are not reusable within the company.

Employee search defaults to excluding `Terminated` employees and exposes an explicit opt-in to include them, so that ordinary operational reads are not silently widened while audit and reporting reads remain possible (`FR-EMP-0103`).

## Rehire is deferred

No transition out of `Terminated` exists in Milestone 1, and no rehire operation is defined. No source requirement establishes one.

A future package introducing rehire must decide whether it reuses the existing Employee identity or creates a new Employee record, and must state the consequences for employee-number uniqueness, employment dates, and branch history. Neither choice is foreclosed by this model: the append-only branch history and the retained terminal record support either (`DEC-EMP-0016`).

## No physical deletion

There is no delete command, repository method, permission, endpoint, cascade, or routine database operation for Employee or `EmployeeBranchAssignment`. A persistence guard rejects physical deletion, mirroring the existing Company deletion guard. `Terminated` is the terminal retained state (`BRULE-EMP-0005`).

## Concurrency and event timing

- Every status-changing, profile-changing, and transfer command supplies an expected rowversion.
- A stale rowversion returns a conflict and raises no committed event.
- Status changes, profile changes, branch changes, assignment-history appends, metadata, and events persist in one Unit of Work each.
- A transfer's `Employee.BranchId` change and its appended assignment record commit together or not at all.
- Events are dispatched only after successful commit.
- No automatic retry may silently apply a command to a newer state; the caller must reread and deliberately retry.

## No time-driven status

Employment dates, contract end dates, and inactivity do not automatically mutate Employee status. A `TerminationDate` in the past does not by itself terminate an Employee. Any future automation must issue an explicit authorized lifecycle command.

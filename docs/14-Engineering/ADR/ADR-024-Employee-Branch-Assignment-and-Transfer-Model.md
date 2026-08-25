---
id: ADR-024
title: Employee Branch Assignment and Transfer Model
category: Architecture Decision Record
version: 1.1
status: Accepted
date: 2026-08-25
owner: Solution Architecture Team
tags:
  - branch
  - ownership
  - transfer
  - history
  - authorization
  - hr
  - architecture
depends_on:
  - ADR-013
  - ADR-014
  - ADR-017
  - ADR-020
  - ADR-023
  - ADR-025
used_by:
  - HR
  - GL
  - Sales
  - Inventory
---

# ADR-024: Employee Branch Assignment and Transfer Model

---

# Status

**Accepted** — 2026-08-25.

Proposed alongside the FP-006 Employee design review, on the stated ground that **no production code implemented it yet: `Employee` did not exist**. That ground has lapsed — `Employee` shipped in FP-006 (PR #40) as the first production `IBranchOwnedEntity`, and `EmployeeBranchAssignment` implements this ADR's transfer model directly.

**Evidence — inference from use, not a recorded activation.** This ADR states no acceptance precondition, so its acceptance is inferred rather than recorded, and is named as an inference (`DEC-L-020`). The inference is that FP-006's `decisions-approved.md` declares "the architecture recorded in `ADR-024` and `ADR-025` … binding for FP-006 implementation", citing decisions 1, 4, 5 and 9 against shipped behaviour, and that `BranchTransferArchitectureTests` and `EmployeeArchitectureTests` assert it executably. What is absent is any closed decision saying this ADR is accepted, as `OD-POS-004` said of `ADR-027`.

This ADR resolves the one question `ADR-023` deliberately left open for the first branch-owned business entity — how a record may legitimately change branch — and it does so **before** the entity exists, because the answer determines the Employee schema and the shape of the branch-write boundary that every future branch-owned entity inherits.

---

# Context

`ADR-023` established branch as a real data partition beneath the tenant. Branch-owned entities implement `IBranchOwnedEntity`, carry `TenantId` and `BranchId`, receive their `BranchId` from the server execution context, and are re-authorized against live state on every write. The write boundary in `TenantDbContext.ApplyBranchRulesAsync` enforces four rules, of which two matter here:

| Change | Existing rule |
|--------|---------------|
| `Modified` with `BranchId` modified | Refused — branch ownership cannot change after creation |
| `Modified` / `Deleted` in another branch | Refused — ownership must match the trusted branch context |

`REQ-HR-0004` requires **Transfer Employee**. Taken naively, that requirement and the first rule are in direct opposition, and the opposition is not theoretical: the refusal is unconditional and has no escape hatch.

`ADR-023` anticipated this precisely, in two places. The write-boundary rationale states that relocating history by editing a column would move records between branches with no record that they moved, and that *if a business transfer is ever needed, it must be an explicit, auditable operation, not a property assignment*. Its *Future Considerations* section names the trigger for revisiting it: **cross-branch business operations (transfers, consolidations)**. `docs/02-Functional/Platform/Branch-Management.md` records the same area as explicitly *not designed*.

The conflict is therefore not a contradiction between authorities. It is a deliberately deferred design slot, and Employee is the entity that fills it.

Three further constraints shape the model:

- **The actor's execution context during a transfer is the source branch.** The write boundary refuses a `Modified` entry whose `BranchId` does not match the trusted context, so an Employee cannot be loaded and modified from the destination branch. Any transfer design must therefore treat the destination as a *business argument that is authorized*, never as the caller's execution scope.
- **A transfer record belongs to two branches.** It names where the employee came from and where they went. Neither branch owns it.
- **Branch authorization always intersects with active branches** (`ITenantBranchAccessResolver`). A deactivated branch is unreachable by every principal, including a Tenant Administrator, whose scope is *all active branches*. Without an explicit rule, an employee in a deactivated branch would be permanently unwritable and untransferable.

---

# Problem Statement

Define the branch assignment and transfer model for `Employee`, and by extension for every future transferable branch-owned entity, such that:

- ordinary entity update can never act as a branch transfer, by construction rather than by convention;
- a legitimate transfer has exactly one sanctioned path, and that path cannot be opened by anything a client controls;
- both the branch the employee leaves and the branch they enter are authorized, against live state;
- the current branch and the branch history cannot disagree;
- a report about a past period attributes an employee to the branch they were actually in at that time;
- an employee in a deactivated branch can still be recovered, without the Platform module having to know that HR employees exist.

---

# Decision

1. **Employee stores the authoritative current `BranchId`.** `Employee` implements `ITenantOwnedEntity`, `ICompanyOwnedEntity` (`ADR-025`) and `IBranchOwnedEntity`, and physically carries `TenantId`, `CompanyId` and `BranchId`. A purely temporal or derived branch representation — where the current branch is computed from assignment history and no `BranchId` column exists — is **rejected**: it contradicts `ADR-023`, which states that Employee carries `BranchId`, and it would leave `IBranchOwnedEntity` unimplementable, taking Employee outside the branch-write boundary entirely.
2. **Ordinary branch immutability remains the default invariant.** The existing `TenantDbContext` refusal of a modified `BranchId` stands unchanged for every `IBranchOwnedEntity`. Ordinary entity update must never act as transfer. This ADR adds a narrow exception; it does not weaken the rule.
3. **A sanctioned branch-transfer channel is the only exception.** A server-controlled authorization channel declares exactly one transition — **entity identity, source branch, destination branch** — and the branch-write boundary permits a `BranchId` modification only when an open declaration matches that entry exactly. Every non-matching modification is refused as today. The channel must not be activatable from a request DTO, a request header, a form field, a JWT or token claim, or an arbitrary repository caller; it is opened only by a command handler that has already performed dual authorization, and it is auditable.
4. **`EmployeeBranchAssignment` is tenant-owned and company-owned, and deliberately NOT `IBranchOwnedEntity`.** A transfer record spans a branch boundary: it names a source and a destination, and belongs to neither. Stamping it with a single `BranchId` would either make the record of a departure invisible in the branch that received the employee, or make the record of an arrival invisible in the branch that released them — and it would collide with the write boundary, whose trusted context during a transfer is the source while the record's subject is the destination. This is an **explicit classification** under Architecture Principle 11, recorded so that it can never read as an omission.
5. **Branch history is immutable and append-only.** Employee creation writes an initial assignment row with `SourceBranchId = null` and `DestinationBranchId` equal to the stamped `Employee.BranchId`, so every employee's branch history is complete from hire. Each transfer appends one further row. **No existing assignment row is ever modified or physically deleted** — including to close an interval.
6. **Transfer requires dual branch authorization.** The normal execution branch is the **source**. The **destination** is authorized separately through the live branch access resolver (`ITenantBranchAccessResolver`), which intersects with active branches. Both must be revalidated during the operation, not captured at request start.
7. **The branch change and the history append are one transaction, serialized by `Employee.RowVersion`.** Optimistic concurrency on the Employee aggregate is the single serialization point, which is what guarantees the assignment log cannot fork. **No new application lock is introduced.**
8. **Current state and history are read through different columns, and never mixed.** Current-state queries use `Employee.BranchId`. Historical point-in-time queries use `EmployeeBranchAssignment`: for a point in time `T`, the effective assignment is the row for that employee with the greatest `EffectiveFromUtc` less than or equal to `T`. Attributing an employee to their current branch inside a historical report is a defect.
9. **V1 transfer is immediate on commit.** There is no future-dated transfer, no scheduled transfer, and no cancellation operation. A mistaken transfer is corrected by another explicit, authorized transfer, which appends a further row.
10. **Transfer is a distinct functional operation.** It is a separate command, a separate API operation, and a separate functional permission `HR.Employees.Transfer`. `UpdateEmployee` does not accept `BranchId` in its contract at all. A `Terminated` employee cannot be transferred.
11. **The channel is general architectural infrastructure**, not an Employee feature. It is the mechanism any future genuinely transferable `IBranchOwnedEntity` must use. A general-purpose "allow `BranchId` modification" flag is **rejected**: a switch that can be turned on for convenience is the boundary's absence, not its exception.
12. **A Tenant Administrator may transfer an employee out of an inactive source branch.** This is a **narrow, explicit exception to `ADR-023` decision 5**, under which an administrator's branch scope is all **active** branches and an inactive branch is therefore unreachable. It is permitted only when the actor holds `Platform.Tenant.Administer`, the destination branch is active and belongs to the same tenant, the operation is the explicit `TransferEmployee` operation, the transfer is audited, and normal destination authorization succeeds. The exception is one-directional: it authorizes moving an employee **out** of the inactive branch and nothing else. It grants **no** ordinary read or write authority over that branch, and it does not widen the administrator's branch scope for any other operation or for `ITenantBranchAccessResolver` generally.
13. **`Employee` and `EmployeeBranchAssignment` are carried by shared-to-dedicated cutover.** Both are tenant-owned, so the model-derived copy manifest (`ADR-020`, `ADR-023` decision 21) includes them by construction; the declared inventory asserted in the architecture tests must be extended deliberately. Dependency order is `Company` → `Branch` → `Employee` → `EmployeeBranchAssignment`.

---

# The transfer model

## Ownership classification

```
Tenant   (Guid TenantId)                               -- tenant root
  ├── Company (Guid CompanyId, Guid TenantId)          -- ITenantOwnedEntity
  ├── Branch  (Guid BranchId,  Guid TenantId)          -- ITenantOwnedEntity
  ├── Employee                                         -- ITenantOwnedEntity + ICompanyOwnedEntity + IBranchOwnedEntity
  │     (TenantId, CompanyId, BranchId, EmployeeId)
  └── EmployeeBranchAssignment                         -- ITenantOwnedEntity + ICompanyOwnedEntity  (NOT branch-owned)
        (TenantId, CompanyId, EmployeeId, SourceBranchId?, DestinationBranchId, EffectiveFromUtc)
```

`Employee` is the first entity in the product to carry all three ownership dimensions. `EmployeeBranchAssignment` carries two, deliberately.

## Assignment record

| Field | Rule |
|-------|------|
| `EmployeeId` | The employee the assignment describes; restricted foreign key |
| `TenantId`, `CompanyId` | Ownership; inherited isolation |
| `SourceBranchId` | `null` on the initial hire record, otherwise the branch left |
| `DestinationBranchId` | Never null; the branch entered |
| `EffectiveFromUtc` | The commit instant; never a future value |
| `TransferredBy` | The acting principal |
| `ReasonCode`, `ReasonText` | Required on transfer; a defined system reason on the initial record |

## Why intervals are derived rather than stored

An assignment row is never closed with an `EffectiveToUtc`, because writing one would mean **updating a history row**, which is exactly the mutation this model exists to prevent. Because V1 forbids future-dating, `EffectiveFromUtc` is monotonic per employee, so the effective interval of a row runs until the next row's `EffectiveFromUtc`, and the point-in-time query is unambiguous without stored end dates.

A stored closed-interval variant remains available as a **future** optimization if point-in-time reporting becomes a measured bottleneck. It would have to be introduced as a derived projection, never by mutating the append-only log.

## Execution and authorization sequence

```
1. functional permission        HR.Employees.Transfer
2. company scope                ADR-025 — the employee's CompanyId is in the authorized company set
3. source branch                the trusted execution context (or decision 12 recovery)
4. destination branch           ITenantBranchAccessResolver — live, active-only
5. domain rules                 employee is not Terminated; destination differs from source
6. open the transfer channel    entity + source + destination, exactly
7. one transaction              set Employee.BranchId, append assignment row
8. commit                       Employee.RowVersion serializes
```

Steps 3 and 4 are both re-asked inside the transaction. An authorization answer obtained before step 6 is not carried across the commit.

## Concurrency outcomes

| Scenario | Outcome |
|----------|---------|
| Two simultaneous transfers | `Employee.RowVersion` conflict; the loser is refused and retries against re-read state. The log cannot fork |
| Transfer vs `UpdateEmployee` | Same `RowVersion` contention; one wins, the other retries |
| Transfer vs termination | Same contention; additionally a `Terminated` employee cannot be transferred as a domain rule |
| Stale `RowVersion` supplied | Refused; the Platform rowversion transport convention applies |
| Branch authorization revoked before commit | Refused — the write boundary re-asks the resolver on save and fails closed |
| Destination deactivated before commit | Refused — the resolver intersects with active branches, re-asked inside the transaction |

No application lock is required. `BranchTopologyLock` exists because the branch-assignment invariant spans the platform and tenant databases and can strand a user; a transfer changes no branch topology and touches one catalog.

## Inactive source branch recovery

Branch retirement is deactivation, never deletion (`ADR-023`), and `ITenantBranchAccessResolver` always intersects with active branches. A Tenant Administrator's scope is *all active branches* (`ADR-023` decision 5), so a deactivated branch is unreachable by **every** principal without exception — which means an employee left in one is unwritable, and in particular untransferable, permanently.

The alternative fix, refusing branch deactivation while employees remain assigned, was **rejected**: it would require the Platform module to inspect HR employees before deactivating a branch, a Platform → HR dependency the modular monolith (`ADR-001`) forbids, and it would couple branch retirement to every future branch-owned module in turn.

Decision 12 keeps the knowledge where it belongs. HR recovers its own records, under tenant-administration authority, through the one audited operation that already exists, and the exception is one-directional: it authorizes moving an employee **out** of an inactive branch and nothing else.

---

# Consequences

## For HR

`Employee` and `EmployeeBranchAssignment` are the first entities of the model. FP-006 delivers `TransferEmployee` as a distinct command, permission and endpoint, and `UpdateEmployee` omits `BranchId` from its contract entirely — omission at the contract level, with the write boundary as defence in depth.

Employee closes the `ADR-023` LOW-1 obligations with real SQL: that `IBranchWriteAuthorizer` is actually invoked on a real Employee save, that an added Employee is stamped, that a spoofed `BranchId` is refused, that `BranchId` mutation is refused, and that cross-branch update and delete are refused. Decisions 10, 16 and 18 of `ADR-023` become runtime-proven at that point.

## For GL, Sales and Inventory

The sanctioned channel is available to any future transferable branch-owned entity and must be reused rather than re-invented. Most branch-owned records — a posted journal, an issued invoice, a stock movement — are **not** transferable, and must not become so merely because a mechanism now exists.

## For reporting

Two distinct question shapes now exist, and a report must state which it answers. "Which employees are in this branch now" reads `Employee.BranchId`. "Which employees were in this branch last quarter" reads the assignment log. `ADR-023` decision 22 still governs both: the branch predicate is explicit in either case, and "all branches" means all branches currently authorized to the requesting user.

## For tenant storage and cutover

Both entities are copied by construction. The declared inventory asserted in `TenantCutoverCopyPlanTests` must be extended from `["Branch", "Company"]`, and the copy order is `Company` → `Branch` → `Employee` → `EmployeeBranchAssignment`. That test is designed to fail on a new tenant-owned entity precisely so the ordering and identity decisions are made deliberately.

## For the branch-write boundary

`TenantDbContext.ApplyBranchRulesAsync` gains one conditional path, guarded by an exact match on entity, source and destination. It is the only relaxation of the boundary that will exist, and it is narrower than the rule it excepts: an unmatched `BranchId` modification is still refused, and cross-branch modify and delete are unaffected.

## Negative consequences

- The branch-write boundary is no longer a single unconditional rule. That cost is accepted because the alternative — a transfer performed by delete-and-reinsert or by a raw-SQL side channel — destroys either identity or the boundary itself.
- Point-in-time branch queries are more expensive than a stored closed interval would be, since the effective row is found by ordering rather than by an indexed range.
- Two read paths exist for one concept, and mixing them is a silent defect. Decision 8 and the reporting guard are what make it detectable.

---

# Decision Drivers

- Correctness: the current branch and the branch history must not be able to disagree.
- Server-side authority: a transfer destination may be named by a caller but never asserted as their own scope.
- Freshness: both branch authorizations re-evaluated inside the operation, failing closed.
- Auditability: no branch change without a record of who moved whom, from where, to where, and why.
- Minimal blast radius: the narrowest possible exception to a proven boundary.
- Reuse: one general mechanism for every future transferable entity, not one per module.

---

# Alternatives Considered

## Option 1 – Temporal assignment only, no `BranchId` on Employee

### Advantages

- One representation; the history is the single source of truth and cannot disagree with a denormalized column.

### Disadvantages

- Contradicts `ADR-023`, which states Employee carries `BranchId`; leaves `IBranchOwnedEntity` unimplementable, so Employee falls outside the branch-write boundary altogether and every branch-scoped read becomes a join against history. Rejected.

## Option 2 – Delete and re-insert the Employee in the destination branch

### Advantages

- Requires no change to the branch-write boundary.

### Disadvantages

- Destroys employee identity, orphans every reference, and violates `BR-PLT-0003`. Rejected.

## Option 3 – Raw-SQL side channel bypassing the change tracker

### Advantages

- No framework change; the boundary code stays untouched.

### Disadvantages

- Defeats the boundary it routes around, and moves the one rule that matters into a place no architecture guard inspects. Rejected.

## Option 4 – A general "branch changes are allowed on this entity type" flag

### Advantages

- Simple to implement and to reason about locally.

### Disadvantages

- Converts a per-transition authorization into a per-type permanent hole; nothing then distinguishes an audited transfer from an ordinary update that happens to set a different branch. Rejected.

## Option 5 – Sanctioned per-transition transfer channel plus append-only history (Selected)

### Advantages

- Preserves the default invariant; the exception authorizes one exact transition and expires with it; identity survives; history is complete and immutable; the mechanism generalizes to future entities.

### Disadvantages

- Adds a conditional path to the write boundary and a second read path for branch attribution. Accepted.

---

# Rationale

The selected model is the only one that satisfies `REQ-HR-0004` without weakening the property `ADR-023` was written to establish: that a record's branch is decided by the server and cannot be changed by editing a field.

It does so by separating two things a naive design conflates — *the authority to write in a branch* and *the authority to move a record between branches*. The first stays exactly as `ADR-023` defined it. The second is a new, explicitly authorized, single-transition operation that leaves an immutable record of itself. An ordinary update cannot become a transfer by accident, because an ordinary update never opens the channel; and a transfer cannot become a silent relocation, because the channel names both endpoints and the append-only log records them.

Keeping `Employee.BranchId` authoritative rather than derived is what lets all of this reuse the proven machinery. The employee's branch is stamped, filtered, guarded and re-authorized by exactly the same code that will serve GL and Sales, and the history is an additional, independently classified record rather than a replacement for the ownership column.

---

# Implementation Guidelines

- `Employee : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity, IBranchOwnedEntity`.
- `EmployeeBranchAssignment : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity`. Do **not** implement `IBranchOwnedEntity` on it.
- Never expose `BranchId` on the update contract. Do not validate it away; omit it.
- Open the transfer channel only inside the `TransferEmployee` handler, after dual authorization, and scope it to the single save that performs the transition.
- Resolve branch scope only through `ITenantBranchAccessResolver`. Do not re-derive it in a handler.
- `RowVersion` is required on transfer requests, never optional.
- Write the initial assignment row in the same transaction as Employee creation; an employee with no assignment history is a defect.
- Never update or delete an assignment row. There is no correcting edit; there is only another transfer.
- Point-in-time queries select the greatest `EffectiveFromUtc` less than or equal to `T`. Never approximate with `Employee.BranchId`.

---

# Compliance Rules

- Ordinary `BranchId` modification remains refused for every `IBranchOwnedEntity`; only an exact matching transfer declaration permits it.
- The transfer channel is never activatable from a request DTO, header, form field, token claim, or arbitrary repository caller.
- Every branch change has exactly one corresponding appended assignment row, written in the same transaction.
- Assignment rows are never updated or physically deleted.
- Transfer authorizes source and destination independently, against live state, inside the operation.
- Historical branch attribution never reads `Employee.BranchId`; current-state attribution never reads the assignment log.
- The inactive-source-branch exception is one-directional and grants no other access to that branch.
- `Employee` and `EmployeeBranchAssignment` appear in the declared tenant-owned copy inventory in the documented dependency order.

---

# Risks

| Risk | Mitigation |
|------|------------|
| The transfer channel is opened too broadly and becomes a general branch-change permit | Channel authorizes one exact entity/source/destination triple and is scoped to a single save; architecture guard asserts no other activation path |
| An ordinary update path silently acquires the ability to change branch | `BranchId` absent from the update contract; write boundary refuses unmatched modifications; guard asserts the channel is opened only by the transfer handler |
| Current branch and history diverge | Both written in one transaction, serialized by `Employee.RowVersion`; a transfer that appends no row cannot commit |
| A historical report attributes employees to their current branch | Decision 8 plus the `ADR-023` decision 22 reporting guard, extended to reject `Employee.BranchId` in point-in-time queries |
| Employees stranded in a deactivated branch | Decision 12 recovery transfer under tenant-administration authority, audited and one-directional |
| A new tenant-owned entity is missed by cutover | Model-derived manifest plus the declared-inventory assertion that fails on any new tenant-owned entity |

---

# Future Considerations

Revisit this ADR when:

- a second transferable branch-owned entity is introduced, and the channel's generality is tested for the first time;
- future-dated or scheduled transfers are required;
- transfer cancellation or reversal as a first-class operation is required;
- point-in-time branch reporting becomes a measured bottleneck and a closed-interval projection is warranted;
- branch consolidation or branch hierarchies are requested;
- an employee is required to hold more than one concurrent branch assignment.

---

# Related Documents

- ADR-001 – Modular Monolith (module dependency direction)
- ADR-013 – Primary Key & Identifier Strategy (`BranchId`, `CompanyId` = `Guid`)
- ADR-014 – Company / Legal-Entity Ownership and Scoping
- ADR-017 – Tenant Storage Topology and Routing (platform/tenant split)
- ADR-020 – Shared-to-Dedicated Tenant Migration and Cutover (copy manifest)
- ADR-023 – Tenant Branch Model, Authorization and Execution Context (decisions 10, 16, 18, 22; LOW-1)
- ADR-025 – Company Execution Context and Authorization
- `docs/02-Functional/Platform/Branch-Management.md`
- `docs/14-Engineering/Architecture-Principles.md` – Principle 11
- BR-HR-0001 … BR-HR-0004, BR-PLT-0003, BR-PLT-0004, BR-PLT-0013, BR-PLT-0016
- REQ-HR-0001 … REQ-HR-0008

---

# Review Criteria

This ADR should be reviewed when:

- The Employee aggregate and the transfer channel are implemented.
- A second transferable branch-owned entity is introduced.
- Branch-scoped or point-in-time reporting is implemented.
- Future-dating, scheduling, or cancellation of transfers is requested.
- Branch consolidation, branch hierarchies, or multi-branch employees are requested.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-18 | Solution Architecture Team | Establishes the Employee branch assignment and transfer model. Records the thirteen decisions resolving REQ-HR-0004 against the ADR-023 branch-write boundary. |
| 1.1 | 2026-08-25 | Solution Architecture Team | Status corrected from `Proposed` to **Accepted**. No decision changed. It was Proposed on the stated ground that `Employee` did not yet exist; `Employee` shipped in FP-006 as the first production `IBranchOwnedEntity` and `EmployeeBranchAssignment` implements this transfer model. Acceptance is an **inference** from that use rather than a recorded activation — this ADR states no acceptance precondition, and none is claimed for it (`DEC-L-020`). |

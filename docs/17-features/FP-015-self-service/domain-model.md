---
package: FP-015
title: Self Service — Domain Model
status: DRAFT — names the schema ADR-030 left to this package
version: 0.1
date: 2026-08-27
---

# Domain Model — FP-015

**`ADR-030` deliberately does not name this thing.** Its *What this ADR does not decide* section says
so outright:

> **"The schema. No table, column or type is named here. `DEC-L-023` rules the shape; the package
> rules the schema, the way `ADR-028` was left to name its own."**

**So naming it is this package's obligation, and this file discharges it.** Everything below rests on
T-074's measurement of the tree, not on inference from the ADR's prose.

---

## The type

**`UserEmployeeLink`** — `SSAS.Platform.Domain.TenantUsers`, Platform database.

```csharp
public sealed class UserEmployeeLink : Entity<long>, IAuditableEntity
```

### Why that name

**`ADR-030` calls it a link throughout** — *"at most one live link each way"*, *"no foreign key in
either direction"*. The vocabulary is already the ADR's.

**It is deliberately not `…Access`.** `UserBranchAccess` and `UserCompanyAccess` are **capability
grants** — they say where a user may operate. **This says who a user is.** Borrowing the family's
suffix would put an identity fact in the vocabulary of authorization, which is the same category error
`ADR-030` Decision 1 rejected when it refused to put the link in HR.

### Why `Entity<long>` and not `AggregateRoot<long>`

**Both nearest neighbours are `Entity<long>`** (`UserBranchAccess.cs:25`, `UserCompanyAccess.cs:27`);
`TenantUser` is the `AggregateRoot`. **The link is not a thing the business reasons about on its own**
— it has no lifecycle of its own, no invariants beyond its uniqueness, and nothing holds a reference
to it. It is a fact about a `TenantUser`.

---

## `ITenantOwnedEntity` — DECLINED, and not because the neighbours decline it

**This is the one T-074 flagged as a trap:** *"all three are Platform-resident and carry a
`TenantId`. The first two decline `ITenantOwnedEntity`; `TenantUser` takes it. There is no convention
— so whichever the mapping does will look like it follows a rule, and there is no rule."*

**So the decision is made on mechanism, and the mechanism is now known precisely.**

### What T-074 established, and it is not what `ADR-030` implies

**Residency is decided by MODEL MEMBERSHIP. Not by the interface, and not by the assembly.** Both
alternative explanations are false and each has a counter-example in the tree:

```
TenantUser.cs:9                     carries ITenantOwnedEntity — and does NOT travel
Branch, Company                     Platform-Domain types that DO travel
TenantCutoverCopyPlan.cs:24,28-31   Build(IModel model) selects ITenantOwnedEntity WITHIN that model
TenantCutoverCopyService.cs:156     the one production caller passes ITenantModelSource.Model
```

**The interface is inert for a type the tenant model does not contain.** Configure `UserEmployeeLink`
on the Platform context and it never travels, whatever interfaces it carries.

### The decision

**Decline it.**

**Not for tidiness and not to match `UserBranchAccess`.** The interface has exactly one mechanical
effect: **it makes the type travel at cutover *if* the type is ever added to the tenant model.** A
`UserEmployeeLink` that travelled would take the mapping into the tenant database and break
`ADR-030` Decision 1 — **the mapping would follow the employee and leave the identity behind, which
is the precise failure Decision 2 rejected a column for.**

**Carrying the interface makes that a mistake someone must not make. Declining it makes the mistake
inert.** Same reasoning `ModulePermissionDefinition` gives for having no `Scope` property: *"with no
property there is nothing to review, and the escalation cannot be expressed."*

**`TenantUser` carrying it is not a precedent to follow** — it is a type that would be safe to move
and is kept back by model composition. **`UserEmployeeLink` would not be safe to move.**

---

## What it holds

| Member | Type | Why |
|---|---|---|
| `Id` | `long` | Database-assigned, as both neighbours |
| `TenantId` | `Guid` | Every Platform-resident tenant-scoped row carries one |
| `TenantUserId` | `long` | `TenantUser` is `AggregateRoot<long>`, PK column `TenantUserId` |
| `EmployeeId` | `Guid` | `Employee` is `AggregateRoot<Guid>` |
| audit | `IAuditableEntity` | As both neighbours |

**The key types are asymmetric — `long` on one side, `Guid` on the other — and that is not a defect.**
They are the two aggregates' real identifiers, measured (T-074), and the link records rather than
reconciles them.

### `TenantUserId`, not `IdentityId`

**`TenantUser.IdentityId` is a different thing: one identity may be many tenant users.** The question
`ADR-030` exists to answer is *given the authenticated caller in this tenant, which employee is this?*
— **a membership question, not a login question.**

**`ADR-030` Decision 1 already says so** and this file follows it rather than re-deciding it: *"It
holds the tenant user and the employee it names."*

---

## Cardinality — `ADR-030` Decision 3

> *"A user has at most one live link to an employee; an employee has at most one live link to a user.
> Neither is required to have one."*
>
> *"The cardinality is a rule about what may be true at once, not a schema instruction. How it is
> enforced is the implementing package's to decide."*

**Enforced in the database, in both directions, by two unique indexes** — see `data-model.md`.

**Chosen over application-level enforcement for the reason `REQ-SS-0003` gives about permissions:** a
rule that relies on every write path remembering is a rule nothing catches when one forgets. **Two
unique indexes cannot be forgotten by a handler.**

---

## Absence is a case, not an error — `ADR-030` Decision 5

Resolution returns *"no employee record for this identity"* as an **ordinary answer**. It is
`REQ-SS-0005`, `AC-SS-0008` and `AC-SS-0009` in this package, and `ADR-030` states why it is called
out separately: *"A support administrator opening a self-service page is not a fault condition; it is
Tuesday."*

---

## Removal is physical, and termination does not remove it

**Physical**, following `UserBranchAccess`, whose configuration states the reason: retaining removed
rows means excluding them from every uniqueness test and every check thereafter.

**A link is removed only by administrative correction** — the wrong employee was linked, or a
successor user replaces a predecessor.

**Termination is not such an event.** `REQ-SS-0006` requires the link to survive it, and `OD-SS-0004`
ruled why: severing it makes a terminated employee's retained payslips unattributable. **The guard on
a terminated employee is on the identity's ability to authenticate — never on the link.**

**This is the single most likely implementation mistake in the package** and it is recorded in four
places for that reason (`REQ-SS-0006`, `AC-SS-0011`, `TS-SS-0009`, here).

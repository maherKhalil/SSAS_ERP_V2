---
package: FP-015
title: Self Service — Data Model
status: DRAFT — schema named per ADR-030's deferral
version: 0.1
date: 2026-08-27
---

# Data Model — FP-015

**One new table. It lives in the Platform database and it never travels.**

Everything here is modelled on `UserBranchAccessConfiguration.cs:12-56`, which T-074 identified as
**`ADR-030` Decision 4 already implemented twice** — a Platform-resident table pairing a `long` user
key with a `Guid` key belonging to a row in the tenant database.

---

## `UserEmployeeLink`

**Schema:** `PlatformPersistenceConstants.Schema`. **Table:** `UserEmployeeLink`.

| Column | Type | Null | Note |
|---|---|---|---|
| `UserEmployeeLinkId` | `bigint` identity | no | PK, `UseIdentityColumn()` |
| `TenantId` | `uniqueidentifier` | no | |
| `TenantUserId` | `bigint` | no | → `TenantUsers` |
| `EmployeeId` | `uniqueidentifier` | no | → HR `Employees`, **tenant database** |
| `CreatedUtc` / `CreatedBy` | | no / max len | `IAuditableEntity` |
| `ModifiedUtc` / `ModifiedBy` | | no / max len | `IAuditableEntity` |

---

## Foreign keys — one, not none

**T-074 reported the neighbours as having "no `HasOne`, no `HasForeignKey`". That is true only of the
cross-database side.** `UserBranchAccessConfiguration.cs:47-51` does declare a foreign key, and it is
the useful half:

```csharp
builder.HasOne<TenantUser>()
  .WithMany()
  .HasForeignKey(link => new { link.TenantId, link.TenantUserId })
  .HasPrincipalKey(user => new { user.TenantId, user.Id })
  .OnDelete(DeleteBehavior.Restrict);
```

**`UserEmployeeLink` takes the same, unchanged.**

**The composite principal key `(TenantId, TenantUserId)` is a tenant-isolation guarantee, not a
convenience.** It makes a link naming a user from another tenant **impossible to store**, rather than
something a handler must remember to check. Without it, cross-tenant isolation on this table would be
application code — which is the shape `REQ-SS-0003` rejects for permissions and there is no reason to
accept it here.

**`Restrict`, not `Cascade`**, for the neighbour's stated reason: a user is deactivated, never
deleted, and a cascade would silently erase the record. **Here it would also destroy the
attributability `REQ-SS-0006` exists to protect.**

**No foreign key on `EmployeeId`, and none is possible** — `Employee` lives in the tenant database.
`ADR-030` Decision 4 states this is a consequence, not an oversight, and that *"referential integrity
across this link is the application's to maintain."*

---

## Indexes — the cardinality rule, both directions

`ADR-030` Decision 3 is *at most one live link each way*. **Two unique indexes, because one enforces
only one direction:**

```
UX_UserEmployeeLink_TenantId_TenantUserId    UNIQUE (TenantId, TenantUserId)   one employee per user
UX_UserEmployeeLink_TenantId_EmployeeId      UNIQUE (TenantId, EmployeeId)     one user per employee
```

**Unfiltered**, because removal is physical (`domain-model.md`) — there are no dead rows to exclude.

**The first index also serves the read.** Self-service resolution is *given this tenant user, which
employee* on every self-service request, and that is a seek on the leading columns of an index the
uniqueness rule requires anyway. **No separate covering index is specified**; if measurement later
shows one is needed, that is a change with evidence rather than a guess now.

**`TenantId` leads both**, for the neighbour's stated reason: every read is already tenant-scoped, and
it makes one tenant's links a contiguous range.

---

## Residency — the mechanism, stated because the ADR's reasoning does not give it

**`UserEmployeeLink` is configured on the Platform `DbContext` and appears in no tenant model.**

**That, and only that, is what keeps it out of the tenant database.** T-074 established the mechanism
and disproved the two obvious explanations:

```
TenantCutoverCopyPlan.cs:24,28-31   Build(IModel model) selects ITenantOwnedEntity WITHIN that model
TenantCutoverCopyService.cs:156     the one production caller passes ITenantModelSource.Model
TenantUser.cs:9                     HAS ITenantOwnedEntity and does not travel
Branch, Company                     Platform-Domain types that DO travel
```

**Residency is model membership. Not the interface, and not the assembly.**

### ⚠ Why this paragraph exists

**`ADR-030`'s own reasoning reads as though `ITenantOwnedEntity` decides residency**, with `TenantUser`
as an exception. **It does not, and someone implementing faithfully from the ADR's prose could put
this table in the wrong database while believing they had followed it.**

**That is a worse failure than the one T-074 was sent to rule out**, and it is only visible because
the task went past the yes. **Recorded here, in the file an implementer reads.**

### The existing residency guard does not cover this

`SubscriptionResidencyArchitectureTests:45-58` protects the commercial types by asserting they **lack**
`ITenantOwnedEntity`. **`UserEmployeeLink` also lacks it** (`domain-model.md`), so that guard's method
happens to apply — **but the property it asserts is not the property that matters here.** The thing to
assert is **absence from the tenant model**, which is true of the commercial types for the same reason
and is what neither guard says.

**Not built by this package.** Named as a gap, because `FP-015` is the second package to depend on a
residency the tree does not assert.

---

## Migration

**One Platform-database migration.** T-074 measured Platform migrations as containing **zero**
occurrences of `Employee`; this adds the first, and it adds a column name, never a foreign key.

**The 1023 `Employee` occurrences under `Platform.Infrastructure/…/TenantErp` are not this** —
Platform Infrastructure *hosts* the tenant database's migrations, which build HR's tables. **T-074
named that as the trap; it is repeated here because a migration author will meet it.**

---

## What this package does not add

- **No column on `Employee`** — `ADR-030` Decision 2, three independent reasons.
- **No column on `TenantUser`** — same decision.
- **No cross-database foreign key** — Decision 4, impossible.
- **No index on `EmployeeId` alone.** Every query is tenant-scoped; a tenant-less lookup of a link is
  not a question this product asks.

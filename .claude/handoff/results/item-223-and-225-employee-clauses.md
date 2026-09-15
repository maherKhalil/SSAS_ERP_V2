# Items 223 and 225 — three clauses that held by construction and were asserted by nothing

**TASK gate green, 0 warnings. HR 326 → 328.** Integration verified by targeted run (the TASK gate does
not run Integration).

## 223 — `AC-EMP-0013`: the behaviour holds, and it holds BY CONSTRUCTION

**Established before writing anything, as the row required.** `Deactivate` and `Activate` both:

1. guard the source status (`Active` / `Inactive`),
2. delegate to `ApplyTransition`, which assigns **`Status`, `StatusChangeReasonCode`,
   `StatusChangedUtc`, `StatusChangedBy` and nothing else**,
3. raise their event.

⚠ **No company, no branch, no identity field, no assignment write — anywhere on either path.**
**Implemented and unasserted, not a defect.**

### What was already covered, and by what

| clause | state |
|---|---|
| deactivate only from `Active`, activate only from `Inactive` | ✅ `Repeated_and_unlisted_transitions_are_rejected` |
| neither changes company, branch or any identity field | ⚠ **nothing** |
| neither writes a branch-assignment record | ⚠ **nothing** |
| no separate reactivate route exists | ⚠ **nothing — for Employee** |

### ⚠⚠ AND THE FOURTH CLAUSE IS ASSERTED FOR A SIBLING AGGREGATE

**`CompanyDomainTests.Activate_requires_inactive_deactivate_requires_active_and_no_reactivate_exists`
ends with `Assert.Null(typeof(Company).GetMethod("Reactivate"))`.**

⚠ **The same mechanism, the same shape, on `Company` — and nobody wrote the Employee half.** **Second
instance this session of a guard present for one aggregate and absent for this one**, after 224's missing
scope-resolver file. **The cross-aggregate comparison found both; no search within the Employee tests
could have found either.**

### The tests

- `Neither_half_of_the_enablement_pair_touches_ownership_identity_or_assignments`
- `No_separate_reactivate_operation_exists`

⚠ **THE STATUS ASSERTIONS ARE THE CONTROL, NOT DECORATION.** Every *unchanged* assertion is satisfied
perfectly by a `Deactivate` that returns success and does nothing at all. **Only checking that the status
DID move separates *changed nothing else* from *changed nothing*.** **Plant d below is that control
firing.**

⚠ **And the arrangement control:** the test uses `Stamped()`, which HAS a branch assignment. **Against an
employee with none, "the count did not change" is `0 == 0` and holds however the transition behaves.**
The assertion compares the surviving record's `DestinationBranchId`, not merely the count, **because a
transition that removed one row and wrote another would leave the count alone.**

### Plants — four, each reddening exactly its own test

| plant | reddens |
|---|---|
| `BranchId = Guid.NewGuid()` inside `Deactivate` | the enablement test |
| `branchAssignments.Add(...)` inside `Deactivate` — ⚠ **the plausible mistake the row named** | the enablement test |
| a `Reactivate` method added to `Employee` | the reactivate test |
| ⚠ `Deactivate` succeeds without transitioning | **the enablement test — the status control firing** |

## 225 — `AC-EMP-0002` clause 3: the tenant dimension

**The criterion has three clauses. Two were asserted. The third — *a post-creation `TenantId` change is
rejected* — was asserted for `Company` and for `TenantUser` and not for this aggregate.**

⚠ **The guard is dimension-generic**: `PersistenceDbContext` walks
`ChangeTracker.Entries<ITenantOwnedEntity>()` and throws on any `Modified` entry whose `TenantId` is
modified. **So it almost certainly held — and "almost certainly" is the whole distance between PINNED and
UNGUARDED.**

`CC2_An_ordinary_update_cannot_change_an_employees_tenant` mirrors `CC` one dimension over, so the pair
reads as one boundary. **It asserts the throw, the message, AND that the stored tenant is unchanged** —
⚠ **without the last, the test passes on a guard that throws AFTER writing, which is a different and
worse defect than one that does not throw at all.**

### ⚠ The plant is the failure mode the guard's own genericity invites

**Not "delete the guard" — `entry.Entity.GetType().Name != "Employee"`, one aggregate excluded from a
shared check.** ⚠ **`CC2` reddens and the company sibling `CC` stays green**, so the plant discriminates:
it proves the new test pins the TENANT dimension for THIS aggregate, not the guard in general.

**A generic guard is exactly the kind a later aggregate-specific change can quietly exclude an entity
from — and until now nothing in the Employee suite would have noticed.**

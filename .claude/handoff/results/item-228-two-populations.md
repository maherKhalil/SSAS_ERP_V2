# Item 228 — both populations were the wrong set, and one of them is already closed

**TASK gate green, 0 warnings.** Integration verified by targeted run.

## ⚠⚠ POPULATION A — THE ROW'S EIGHT WAS WRONG IN BOTH DIRECTIONS, AND THE REAL RULE COVERS TWO

**Enumerated by mechanism — `public Result Deactivate(` across `src/` — which returns eight aggregates:**
Department, Employee, JobGrade, Position, SalaryGrade, Branch, Company, TenantUser.

⚠ **`Subscriptions` and `Tenants` do NOT carry the pair.** `Tenant` has `Activate` / `Suspend` /
`Reactivate` / `Archive` and **no `Deactivate`**; `SubscriptionPlan` has `Activate` / `Retire`.
⚠ **`JobGrade` and `SalaryGrade` DO carry it and were not on the list.**

### And the split that settles the row

| verbs | aggregates | is `AC-EMP-0013` clause 4 (*no separate reactivate*) their rule? |
|---|---|---|
| `Deactivate` + **`Reactivate`** | Department, JobGrade, Position, SalaryGrade, Branch | ⚠ **NO — re-enablement is a distinct concept for these, deliberately** |
| `Deactivate` + `Activate`, no `Reactivate` | **Employee, Company** | ✅ **yes — and BOTH are now asserted** |
| all three | TenantUser | ⚠ **not a violation — see below** |

⚠⚠ **THE RULE'S POPULATION IS TWO, NOT EIGHT, AND IT IS ALREADY CLOSED.** `Company` was asserted before
today, `Employee` was asserted in item 223. **Nothing to build. Six unchecked was five aggregates that
are not subject to the rule plus one that needed explaining.**

### TenantUser carries all three verbs, and correctly

`Activate` guards `Status != Pending`; `Reactivate` guards `Status != Deactivated`. ⚠ **A tenant user is
created `Pending`, not Active** — so first activation and re-enablement are genuinely different
transitions from different source states. **`AC-EMP-0013`'s reasoning is *a created Employee is already
Active, so there is no separate Reactivate concept*; TenantUser's premise is false, so the conclusion
does not apply.** **Answered, not exempt.**

## POPULATION B — 42 TYPES, NOT 23, AND THE UNTESTED PART WAS STRUCTURAL

**`ITenantOwnedEntity` is declared by 42 types across nine domains** (the interface appears in 60 files;
the declaration count is 42 once the generic constraint in `PersistenceDbContext` is excluded).

| | count |
|---|---|
| aggregate roots | **26** |
| ⚠ **child entities** | **16** |

⚠⚠ **AND ALL THREE TYPES ASSERTED BEFORE TODAY — `Company`, `TenantUser`, `Employee` — ARE AGGREGATE
ROOTS.** **A guard that walked only roots would have passed every one of them, and every further
per-type test drawn from the same class.**

⚠ **So the untested population was not thirty-nine names, it was ONE STRUCTURAL CLASS** — and one test
on a child entity closes it. **That is the difference between the grid the row warned against and a test
worth writing.** **The row said not to write fourteen tests to fill a grid; the answer is that the grid
was never the right instrument, because the guard is one `if` and the risk is a class-shaped exclusion.**

### The test, and why `DepartmentManager`

`A_child_entitys_tenant_cannot_be_changed_after_it_is_written`.

⚠ **`DepartmentManager` is the child that is NOT append-only.** `TenantDbContext.SaveChangesAsync` runs
`PreventAppendOnlyMutation()` **before** `base.SaveChangesAsync` — so on `EmployeeBranchAssignment`,
`EmployeeDepartmentAssignment` or `EmployeePositionAssignment` the **append-only** refusal wins and the
test would have thrown for the wrong reason.

### ⚠⚠ AND THE MESSAGE ASSERTION EARNED ITS KEEP ON THE FIRST RUN

**The first version used `fixture.CreateContext()`, which supplies no company authorizer.** The save was
refused — ⚠ **by the COMPANY guard: *"A trusted company context is required to…"***

**A bare `Assert.ThrowsAsync<InvalidOperationException>` would have gone GREEN on the wrong guard**, in a
test whose whole subject is which guard fires. ⚠ **Three guards can refuse a save on this context and
only one of them is the subject.** Retargeted at the graph's own context, which has a trusted company.

**`DepartmentGraph.Context` is newly exposed for this, with the reason recorded beside it.**

### The plant is class-shaped, not type-shaped

**Not *delete the guard* and not *exclude one type*:
`entry.Entity.GetType().BaseType?.Name.StartsWith("AggregateRoot")` — ⚠ the guard walking only
aggregate roots.** **The child test reddens; `CC2` on `Employee` stays green.** **That is the asymmetry
the test exists to close, demonstrated rather than asserted.**

## Where B stands

**Four types asserted: `Company`, `TenantUser`, `Employee` (roots) and `DepartmentManager` (child).**
⚠ **Both structural classes are now represented, and I recommend stopping there:** per-type tests beyond
this prove one `if` statement repeatedly, and **none of them covers the 43rd type added tomorrow.**
**If the residual risk is worth closing, the instrument is a guard against a type discrimination
appearing in that loop at all — a ruling, not a test I should pick.**

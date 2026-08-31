# item 220 — the two unpinned halves are asserted, and three of five were already covered

**Gated work.** Two new tests, three new controls, both plants verified. **TASK gate green, 0 warnings.**
**Control: 107 cited across all packages, zero dangling.**

## ⚠⚠ `AC-EMP-0017` NEEDED ONE TEST, NOT FOUR — BECAUSE THREE BANS WERE ALREADY PINNED

The criterion bans five things. **The ruling asked me to assert four by reflection. Reading first showed
that three of the five already had guards:**

| ban | pinned by | status |
|---|---|---|
| **endpoint** | `HrRouteInventoryTests.The_hr_surface_exposes_no_delete_verb` | ⚠ **already existed** — now cited |
| **cascade** | `DeleteBehaviourArchitectureTests.Every_reference_foreign_key_still_restricts` | ⚠ **already existed** — now cited |
| **persistence guard** | `EmployeeBoundarySqlServerTests.An_employee_cannot_be_physically_deleted` | already cited (B18 pass 01) |
| **command** | ⚠ nothing | **new** |
| **repository method** | ⚠ nothing | **new** |
| **permission** | ⚠ nothing | **new** |

⚠ **Writing all four would have duplicated two live guards, and a duplicated guard is two places to edit
and one place to forget.** **Both existing ones are SUPERSETS — whole-HR-surface and whole-model — and are
cited as supersets rather than shadowed by narrower Employee-only copies that would assert less.**

## `EmployeeNoDeletionArchitectureTests` — the three that were missing

**Reflection over the HR Application assembly**: no type, public method or permission constant beginning
`Delete`/`Remove` may name `Employee` or `EmployeeBranchAssignment`.

**Why the criterion needs this at all, given the persistence guard already exists:**
`An_employee_cannot_be_physically_deleted` proves a delete is **refused** — the runtime half. ⚠ **A delete
command could be added tomorrow and still be refused. The criterion bans the surface from EXISTING**, which
is a claim about the type system, not about a request.

**Three controls, not one:**
- ⚠ **the matcher must find delete-shaped names that DO exist** — `RemoveHolidayCommandHandler` and its
  neighbours in Attendance, **live code in another module**, so a silently-broken matcher fails loudly;
- **the permission catalog must be reachable and non-empty** — permissions are constants, gathered by a
  different reflection path than types, so the permission third could be vacuous while the rest works;
- the ban itself names the offender, so a failure says *what* was added.

**Plant:** added `DeleteEmployees` to `HrPermissionNames` → **the ban reddens with `Found: DeleteEmployees`,
both controls stay green.** Restored.

## `AC-EMP-0015`'s third clause — a terminated employee remains retrievable

`A_terminated_employee_remains_retrievable_by_id`, in `EmployeeBoundarySqlServerTests`.

⚠ **Read through the PRODUCTION query handler, not the context.** The criterion is about what a caller can
retrieve; a `context.Set<Employee>()` read would pass **even if the read service filtered terminated
employees out** — which is exactly the mistake the clause exists to catch.

**And it asserts the status is still `Terminated`**: retrievable-but-reported-Active would satisfy the
letter and lose the fact the retrieval exists to preserve.

**Plant:** made the read handler return null for a terminated employee → **reddens.** Restored.

## ⚠ THE THIRD CLAUSE OF `AC-EMP-0015` IS STILL UNPINNED, AND I AM LEAVING IT

The criterion's full text has **three** residual parts, not two:

1. *remains retrievable by id* — ⚠ **now pinned**
2. *returnable by search subject to the ordinary scope predicates* — **`AC-EMP-0016`'s subject**, and that
   criterion has its own tests; not swept in here
3. ⚠ *its employee number and national ID **remain reserved within the company*** — **PINNED BY NOTHING**

**Said and left, as ruled.** ⚠ **Sweeping it into this item would have meant a test proving a terminated
employee's number cannot be reused — a real and separate assertion, and quietly attaching it to a
retrievability test would overstate what that test covers.**

## Scope
- **`AC-EMP-0017`'s new test scans the HR Application assembly only.** A delete command for Employee
  declared in another module's assembly would not be seen — **and would be a layering violation caught by
  a different guard.**
- **Name-shaped, not semantic.** A method called `PurgeEmployee` satisfies this guard and violates the
  criterion. ⚠ **The criterion itself is written in terms of names — *"no delete command… exists"* — so the
  test matches the criterion's own form, and that limit is the criterion's rather than the test's.**
- FP-006 remains **10 of 47** cited: this item added no new criterion ids, it completed two.

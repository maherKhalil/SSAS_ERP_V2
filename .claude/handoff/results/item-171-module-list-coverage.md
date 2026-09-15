# item 171 — closing the other two module-shaped lists

**Gated work.** `GATE_SCOPE=TASK` **green**, 606 architecture tests pass. No `src/` change.

## ⚠ The module-assembly predicate CAN be stated without a second hand-written list

The ruling's stop condition was: *if the predicate needs another hand-written exclusion list, say so and
stop.* **It does not.**

**A module is a project under `src/Modules/`.** That is a fact about the repository layout, read at test
time, and a new module appears in it the moment its project directory exists — **with nobody deciding it
is a module.** `BuildingBlocks`, `Host`, `Platform` and `Shared` are siblings of `Modules`, not entries in
an exclusion list.

⚠ **The directory name is NOT the assembly prefix** — `src/Modules/Finance/` holds the `SSAS.GL.*`
projects — so `DeployedProductAssemblies.ModuleProjectNames(suffix)` reads the **project** directory
names, not the module folder names. A predicate keyed on the folder name would have reported `SSAS.GL.API`
as not-a-module.

**The four it returns for `.API`:** `SSAS.Attendance.API`, `SSAS.GL.API`, `SSAS.HR.API`,
`SSAS.Payroll.API`.

## The two controls added

| guard | new test |
|---|---|
| `ModuleEnablementArchitectureTests` | `Every_module_api_assembly_the_build_ships_is_named_in_the_list` |
| `ModuleErrorMappingArchitectureTests` | `Every_module_api_assembly_the_build_ships_contributes_mapper_types` |

**Asked in the inverted direction, as ruled.** Not *"which types count as a module"* — a convention
judgement — but *"for each shipped module assembly, does the list contain an entry from it"*.

## ⚠ This does not undo the reason those lists were hand-written

`ModuleEnablementArchitectureTests` states its own case at the declaration:

> The four gateable module API assemblies, **named rather than discovered**. A scan would pass vacuously
> if it ever returned nothing, and naming them means a module moved to another assembly **fails here on
> the day it moves** rather than silently dropping out of the count.

**That reasoning is sound and survives.** Naming catches a module MOVING. It cannot catch a module ADDED,
because a module never appended to the list simply never appears — and every assertion over the list stays
green while covering four modules out of five. **The two directions need two mechanisms; this adds the
second rather than replacing the first.**

## The plants

| plant | result |
|---|---|
| `typeof(AttendanceModuleEnablement).Assembly` removed from the enablement list | `Every_module_api_assembly_the_build_ships_is_named_in_the_list` **FAILED**, with `Module_keys_are_non_empty_and_distinct` and `The_trials_module_list_is_exactly_the_set_the_product_declares` |
| `typeof(AttendanceApiErrorMapper).Assembly` removed from the mapper list | `Every_module_api_assembly_the_build_ships_contributes_mapper_types` **FAILED**, alone |

⚠ **The enablement plant reddening three tests is worth reading carefully, because it does not mean the
new one was redundant.** The two existing tests fire on a module REMOVED from a list that a declared set
still names. A module ADDED moves both sides together — the descriptor is in an unscanned assembly, so it
is not found, and nothing else names it either — so they stay green. **That is the union-collapse shape
again: both sides of a comparison shrinking together is invisible to the comparison.**

Both reverted; 606 green.

## Scope

- **`.API` only.** A module contributing an enablement descriptor or an error mapper from its
  `.Application` or `.Infrastructure` assembly would not be checked. Both guards are API-surface guards, so
  the suffix matches their subject, but it is an assumption rather than a derivation.
- **The predicate is layout, so a module built outside `src/Modules/` is invisible to it.** That is a
  narrower gap than a hand-written list — a module has to live somewhere and the convention is uniform
  across all four — but it is not nothing.
- `DeployedProductAssemblies.ModuleProjectNames` reads directories, not the solution file, so a project
  directory present but excluded from the build would be reported as shipped.

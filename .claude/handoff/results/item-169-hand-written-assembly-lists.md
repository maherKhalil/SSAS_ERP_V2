# item 169 — hand-written assembly lists in the architecture guards

**Gated work.** `tests/Architecture.Tests/DeployedProductAssemblies.cs` (new) and a sixth test in
`ReadSideEscapeArchitectureTests`. `GATE_SCOPE=TASK` **green**. No `src/` change.

## The census: 14 guards carry a hand-written assembly list, and 3 are module-shaped

Of 604 architecture tests, **14 files** build a list of assemblies by hand. **The distinction that matters
is not whether the list is floored — it is whether a NEW MODULE would be missing from it.**

| guard | list | a new module would be missing? |
|---|---|---|
| **`ModuleErrorMappingArchitectureTests`** | per-module API assemblies — Attendance, GL, Payroll, HR | ⚠ **yes** |
| **`ModuleEnablementArchitectureTests`** | per-module enablement types — Attendance, GL, HR, Payroll | ⚠ **yes** |
| **`ReadSideEscapeArchitectureTests`** | per-module Application + Infrastructure | ⚠ **yes — fixed here** |
| `EmployeeReadScopeArchitectureTests` | HR only | no — single-module by subject |
| `CompanyOwnershipArchitectureTests` | BuildingBlocks + Platform | no |
| `AuthenticationMilestoneArchitectureTests`, `TenantLifecycleArchitectureTests`, `TenantCutoverCopyArchitectureTests`, `PlatformSupportAuthorityArchitectureTests`, `EmployeeArchitectureTests`, `ModuleErrorMapping`… (remainder) | single-area, 2–9 assemblies | no — the subject is the area, not the product |
| `FieldAttribution`, `Persistence`, `PreconditionCode`, `TranslatedErrorCodeReachesAMapper` | derive their population from types, not a named assembly array | no |

**Floored / cross-checked / neither, for the three module-shaped ones:** all three carry floors over their
*type* populations; **none was cross-checked against the assembly set**, and `ReadSideEscape` was the only
one carrying a cross-check of any kind (its census witnesses).

## ⚠ A floor cannot catch this, which is why it is a separate control

`The_read_side_population_is_not_empty_and_contains_what_the_census_found` counts read services **across
the whole union**. A new module contributing three unscanned read services leaves the count comfortably
above its floor of 20. **A floor over a union cannot see one member of the union collapse.**

Only a comparison against an independently derived list of assemblies can — which is what was built.

## The fix, and its known positive was real rather than planted

`DeployedProductAssemblies` reads the test build's own output directory for `SSAS.*.dll`.
`The_scanned_assemblies_cover_every_deployed_application_and_infrastructure_assembly` asserts the
hand-written lists cover every one.

⚠ **It failed on first run, on a gap that already existed:**

```
Collection: ["SSAS.BuildingBlocks.Application", "SSAS.BuildingBlocks.Infrastructure"]
```

**Item 168's guard had never scanned either.** They were absent because I wrote the list by naming the
five modules I was thinking about — which is exactly the failure mode the item was opened to close, found
in the guard that named it. Both are now scanned, and the escape guard still passes, so neither contains a
read-side service returning an aggregate.

**Plant:** removing `SSAS.Payroll.Infrastructure` from the list — the shape a new module takes — reddened
the new control and nothing else. Reverted; 6 green.

## ⚠ Why the output directory and not `GetReferencedAssemblies()`

The compiler **omits a reference whose types are never used**. A test project that referenced a new module
but touched none of its types would report the reference missing — and the check would then agree with the
stale list, for the wrong reason. Every project reference is copied to the output directory whether used
or not, so the deployed set is independent of what the test code happens to mention.

## The general-helper question

**`DeployedProductAssemblies` is the general shape and it is built as one** — two static methods, no
knowledge of read services. `ModuleErrorMapping` and `ModuleEnablement` can adopt it in one line each.

**I did not adopt it for them.** Their lists are per-module *types* (`HrModuleEnablement`,
`GlModuleEnablement`) rather than per-module assemblies, so the equivalent control asks a different
question — "does every module have an enablement type?" — and *which types count as a module* is the same
convention judgement flagged in item 168. **Reported rather than taken.**

## ⚠ A rule I already held caught me

The first version used `Path.GetFileNameWithoutExtension`, and `RepositoryPathPortabilityTests` reddened
the gate. That ban exists because MSBuild `Include` attributes use backslashes, which the framework helper
misreads on Linux — **a reason that does not apply to my call, since these paths come from
`Directory.GetFiles`.** The ban is blanket on purpose: the two uses are indistinguishable at a glance.
Complied rather than argued, and the reason is recorded in the file.

## Scope

- **The census counted files that build an assembly array.** A guard hard-coding module names as *strings*
  rather than assemblies would not appear; I searched for `string[] Projects|Modules|Assemblies|…` and
  found none, but the naming there is a guess.
- A module whose assembly is not referenced by `SSAS.Architecture.Tests` at all is not deployed to that
  output directory and is invisible to the new control too. Narrower than a hand-written list, not nothing.

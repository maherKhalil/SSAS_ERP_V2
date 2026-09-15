# item 177 — unique indexes over nullable columns

**Gated work.** `tests/Architecture.Tests/UniqueIndexFilterArchitectureTests.cs`, **7 tests**. No `src/`
change.

## The enumeration came first, and found no live defect

**62 unique indexes across both models — matching the 62 `IsUnique()` declarations in source exactly.**
16 carry a filter, 46 do not, and **zero** are over a nullable column without one. So nothing was stopped
for, and the guard was built.

**Source of truth: the EF model.** `IProperty.IsNullable` for nullability — a `string` may be
`IsRequired()` and a `Guid?` may be too, so the CLR type answers nothing — and `IIndex.GetFilter()` for the
filter.

## ⚠ Reading the model is also what corrected item 176

**EF Core's SQL Server provider supplies `[Col] IS NOT NULL` BY CONVENTION** for any unique index over a
nullable column. Measured: with `EmployeeConfiguration`'s `.HasFilter(…)` declaration **removed**, the
model still reports the identical filter.

**So item 176's worst example — "delete that line" — was wrong**, and 176 now carries the correction. The
reachable form is **`.HasFilter(null)`**, which overrides the convention; the model then reports
`filter=NONE`.

**A convention that silently supplies what a declaration omits is invisible to source reading.**

## ⚠ My first enumeration was over the wrong population

Building `TenantDbContext` **directly** yields **two entity types** — no Employee, no Position, no
Department — so the first `risky=0` was measured over a stub. The tenant model is **composed** from module
contributors.

**`TenantModelEntityCountArchitectureTests` documents this exact trap**, from T-133: *"the unicode guard
passing a planted `varchar` because its `TenantModel()` built a `TenantDbContext` DIRECTLY — two entity
types, none of the ERP."* One file away, and I walked into it. Composing properly took the count from 38
to 62.

## The guard, and why it has three controls

`Every_unique_index_over_a_nullable_column_carries_a_filter` asserts an **absence**, and has three ways to
pass while measuring nothing:

| control | the failure it stops |
|---|---|
| `Both_models_contribute_unique_indexes` | finding no unique indexes at all — floored per model, not in total |
| `The_tenant_model_carries_every_module` | ⚠ **the stub above.** Checked by module-owned entity **name** — `Employee`, `Account`, `PayrollRun`, `AttendanceRecord` — so a failure says *which module is missing* rather than that a number is small |
| `The_predicate_reads_real_filters_and_real_nullability` | a `GetFilter()` that never returns a filter, or an `IsNullable` never true |

## The plants — both directions, and the first is the finding

| plant | result |
|---|---|
| **deleted** `.HasFilter("[NormalizedNationalId] IS NOT NULL")` | ⚠ **guard GREEN** — correctly: the convention restores the identical filter |
| **`.HasFilter(null)`** — explicit suppression | **guard REDDENS**, that test alone; the model reports `filter=NONE` |

**The guard catches the reachable hazard, not the imagined one.** Reporting the green plant rather than
trusting it is what found the convention.

## Scope

- **`GetFilter()` reports presence, not correctness.** A filter naming the wrong column passes exactly as a
  correct one does — measured separately in item 179.
- **The `HasFilter` counts do not agree across sources and should not**: 17 in configuration source, 16 in
  the model, 301 occurrences across `src/` once migrations are counted. **The model is authoritative for
  what gets created.**
- Both models are built model-only against `Server=model;Database=model`; **no connection is opened**, so
  this guard runs inside the TASK gate.

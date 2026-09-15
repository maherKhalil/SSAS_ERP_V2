# item 176 — what can merge unverified under `DEC-L-007`

**Measurement only. No change proposed, and no test placement questioned** — 103/104 already settled that
separately.

## The exposure

`GATE_SUITES` for `TASK` is **Architecture, Platform, HR, API, Finance, Payroll, Attendance**. `PHASE`
adds **Integration**, and runs everything in **Debug and Release**.

| suite | files | facts | assertions | in TASK |
|---|---|---|---|---|
| Platform.Tests | 85 | 862 | 2,504 | yes |
| API.Tests | 93 | 622 | 1,477 | yes |
| Architecture.Tests | 76 | 508 | 1,520 | yes |
| HR.Tests | 14 | 241 | 695 | yes |
| Payroll.Tests | 8 | 83 | 218 | yes |
| Attendance.Tests | 7 | 70 | 194 | yes |
| Finance.Tests | 4 | 42 | 125 | yes |
| **Integration.Tests** | **68** | **772** | **3,724** | ⚠ **no** |
| Performance.Tests | **0** | 0 | 0 | — |
| UI.Tests | **0** | 0 | 0 | — |

**Integration is 3,724 of 10,457 assertions — 36% of everything the repository asserts — and the TASK gate
runs none of it.** Plus the entire Release configuration, whose analyzer set differs; the gate's own header
records that the first Release run exposed `CA1826` warnings Debug never showed.

⚠ **`Performance.Tests` and `UI.Tests` contain no files at all.** Their names imply coverage that does not
exist, in either scope.

## ⚠ What TASK does catch, so the exposure is not overstated

**TASK builds the whole solution** — `dotnet build SSAS.ERP.sln -c Debug --no-incremental` — so a change
that fails to compile anywhere, Integration included, reddens the TASK gate. **A compile break cannot
merge.** The exposure is purely runtime behaviour and Release-only analysis.

## ⚠ The structural reason: no TASK suite ever materialises a real schema

| | `EnsureCreated` / `Migrate` calls |
|---|---|
| all seven TASK suites combined | **8** |
| Integration.Tests | **144** |

And the TASK suites that mention `UseSqlServer` do not connect —
`UseSqlServer("Server=model-only;Database=none")`. **A model is built; a connection is never opened.**

`Platform.Tests` reaches a real engine only through SQLite, and creates **only probe tables by raw SQL**,
deliberately. `PlatformAppendOnlyGuardTests` states why: *"`EnsureCreated` would translate every Platform
configuration into SQLite, which is a different provider from the one those configurations were written
for."*

**So everything the mapping layer means at the database level is asserted in exactly one suite, and it is
the one TASK skips.**

## Classes of defect only Integration can catch — named from its own tests

1. **Tenant- and company-scoped uniqueness, including "absent many times"** —
   `A_branch_code_is_unique_within_a_tenant_and_free_in_another`,
   `A_national_id_is_unique_within_a_company_but_may_be_absent_many_times`,
   `A_duplicate_normalized_code_is_refused_within_a_company`, `A_duplicate_rank_is_refused_within_one_ladder_and_company`
2. **Optimistic concurrency through `rowversion`** — `A_transfer_with_a_stale_rowversion_is_refused`. The
   type is SQL Server's; no other suite can produce one.
3. **Migration refusal against live data** —
   `A_database_holding_employees_refuses_the_migration_with_the_recorded_decision`,
   `A_refused_migration_leaves_the_schema_and_the_employees_exactly_as_they_were`,
   `A_divergent_migration_history_is_a_mismatch_and_is_never_appended_to`
4. **Database-level cascade from a raw delete** —
   `A_raw_delete_of_an_unreferenced_plan_now_cascades_to_its_owned_rows`. EF is bypassed entirely.
5. **Routing and cutover atomicity under concurrent change** —
   `A_frozen_validated_cutover_flips_routing_atomically`,
   `A_live_context_keeps_its_database_when_routing_changes_underneath_it`,
   `A_target_changed_after_the_copy_refuses_the_flip_and_leaves_routing_alone`
6. **Schema-health observation surviving connectivity churn** —
   `A_hard_stale_schema_observation_still_denies_after_connectivity_recovers`,
   `A_successful_connectivity_check_cannot_manufacture_schema_health`

## ⚠⚠ CORRECTED BY ITEM 177 — THE EXAMPLE BELOW IS WRONG AS WRITTEN

**Deleting the `.HasFilter(…)` line does NOT produce the defect.** EF Core's SQL Server provider adds
`[NormalizedNationalId] IS NOT NULL` **by convention** for any unique index over a nullable column.
Measured in item 177 by removing the declaration and reading the model: **the filter is still there,
identical.**

**The mechanism and the exposure survive; the one-line example does not.** The reachable form is one step
further along — **`.HasFilter(null)`**, which explicitly overrides the convention and leaves the index
unfiltered. Measured the same way: the model then reports `filter=NONE`, and
`UniqueIndexFilterArchitectureTests` reddens on it.

**So substitute `HasFilter(null)` for "delete the line" throughout the section below.** Everything else
holds: it compiles, the filter argument is still a raw T-SQL expression nothing type-checks, no TASK suite
materialises a schema, and the consequence is still that the second employee recorded with no national ID
is refused.

⚠ **And item 177 closes it**: that guard now makes this class TASK-visible without a database. It was
built after the enumeration found **no live defect** — 62 unique indexes, 16 filtered, 46 unfiltered,
**zero** over a nullable column without a filter.

## ⚠ The worst concrete example in the current tree *(read with the correction above)*

**`EmployeeConfiguration.cs:144–149`:**

```csharp
builder.HasIndex(employee => new { … , employee.NormalizedNationalId })
  .IsUnique()
  .HasFilter("[NormalizedNationalId] IS NOT NULL");
```

**Delete that `.HasFilter(…)` line.** Then:

- it **compiles** — the filter is a raw T-SQL **string literal**, so nothing type-checks it;
- the **model still builds**, so every model-shape assertion in Architecture.Tests still passes;
- **no TASK suite creates the schema**, so no TASK test can observe an index at all;
- **the TASK gate goes green and `DEC-L-007` merges it immediately.**

**The consequence is a data defect, not a cosmetic one.** SQL Server treats NULLs as equal in a unique
index, so without the filter **the second employee recorded with no national ID is refused** — and national
ID is optional. The only assertion in the repository that would notice is Integration's
`A_national_id_is_unique_within_a_company_but_may_be_absent_many_times`.

**One deleted line, compiles, green TASK gate, merged — and every tenant that records a second employee
without a national ID fails at insert.**

## Scope

- **Assertion counts are `Assert.*` call sites**, not executed assertions; a `[Theory]` multiplies at run
  time. They compare suites fairly but overstate none of them consistently.
- **`Performance.Tests` and `UI.Tests` being empty was measured, not inferred** — the directories contain
  no `.cs` files outside `bin`/`obj`.
- The Release half of the exposure is stated from the gate's own header rather than measured here; I did
  not run a Release-only analysis to enumerate what it would catch today.

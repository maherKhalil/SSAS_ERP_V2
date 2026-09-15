# B20 pass 1 — a universal in a test name over an enumerated population. SEARCH ONLY.

**Swept 2026-09-01 against the working tree at `4171cdc`. Read-only: no test file touched, no build run
(item 239's PHASE gate was running and a parallel compile could have starved its own memory floor).**

## ⚠⚠⚠ THE PROPOSED FIRST FILTER DOES NOT HOLD — `[MemberData]` IS NOT EVIDENCE OF DERIVATION

**B20's row splits the population by ATTRIBUTE KIND: `[InlineData]` is the risk, `[MemberData]` "sourced
from the composed model or a route inventory" is derived and fine.** ⚠ **Every `[MemberData]` source in
this category was read. THREE OF THE FOUR ARE HAND-WRITTEN LITERALS and the fourth is a declared scope,
not a query:**

| source | what it actually is |
|---|---|
| `PlatformSupportAuthorityAuthorizationTests.AuthorityRoutes()` | ⚠ **a hand-written `TheoryData` of 9 routes** |
| `PositionEndpointTests.AllRoutes()` | hand-built from host constants |
| `ModuleErrorMappingArchitectureTests.SiteNames` | a hand-written list of four mapper names |
| `PropagatedErrorMappingTests.Surfaces` | a hand-written array — ⚠ **but with a stated scope and an anti-vacuity floor beside it** |

⚠⚠ **`[MemberData]` MOVES THE LIST SOMEWHERE ELSE. IT DOES NOT DERIVE IT.** **Judging the population by
the attribute that carries it is the same substitution the item exists to catch, one level up: the
attribute kind is a NAME for how the data arrives, and the question is the MECHANISM that produced it.**

**The filter must be *does the population come from a query over an assembly, an EF model or an endpoint
data source* — readable only by opening the source. `[InlineData]` versus `[MemberData]` does not survive
as a first pass.**

## The inventory: 67 universal-named tests with an enumerated population

**Instrument: a test method whose name carries `every|all|no|never|each|any|only|always` as a whole
underscore-delimited segment, whose contiguous attribute block holds `[InlineData]`, `[MemberData]` or
`[ClassData]`.**

| split | count | reading |
|---|---|---|
| ⚠ **TYPES** — `[InlineData(typeof(X))]` | **16** | **the risk: a set of types is derivable from the assembly** |
| VALUES — `[InlineData("x", 3)]` | 42 | ⚠ **not defects.** A theory over `null`/`""`/`"   "` enumerates INPUTS, and its name claims no population |
| MEMBER | 9 | see above — judged individually, not by the attribute |

⚠ **These are NOT comparable to the 757/77 figures in the B20 row: that regex found any occurrence of the
words, this one requires a whole-segment match and a contiguous attribute block. Narrower on purpose, and
neither number supersedes the other.**

⚠ **AND THE `VALUES` SPLIT IS THE CARE THE ROW ASKED FOR, MECHANISED.** **42 of 67 are excluded by an
instrument rather than by a judgement, which is what keeps the item from becoming a campaign against
`[InlineData]`.**

## ⚠⚠⚠ THE STRONGEST INSTANCE, AND IT IS A SECURITY GUARD

**`PlatformSupportAuthorityAuthorizationTests` hand-writes 9 routes and runs FOUR tests over them:**

- `Every_authority_route_rejects_an_anonymous_request`
- `Every_authority_route_rejects_a_mixed_plane_token`
- `Every_authority_route_rejects_a_platform_token_without_administer`
- `Every_authority_route_rejects_a_tenant_plane_token_carrying_the_administer_name`

⚠⚠ **AND THE DERIVED POPULATION ALREADY EXISTS, IN THE SAME TEST PROJECT, FOR THE SAME SURFACE.**
`PlatformRouteInventory` reads `factory.Services.GetRequiredService<EndpointDataSource>().Endpoints` and
filters by prefix, and `PlatformSupportAuthorityRouteInventoryTests` consumes it.

**So a tenth authority route added tomorrow is caught by the INVENTORY test — which is about the route
list — and is silently exempt from all four AUTHORIZATION guards, which are about who may call it.** ⚠
**The two files are one directory apart and the guard is the one that matters.**

*Not verified: whether the 9 hand-written routes currently equal the derived set. That is a build, and
this pass ran while a gate held the box.*

## The 16 `TYPES` rows

| rows | test |
|---|---|
| 7 | `DepartmentApplicationArchitectureTests.Every_department_mutation_requires_a_row_version` |
| 8 | ⚠ `DepartmentApplicationArchitectureTests.No_department_command_carries_a_tenant_identifier` |
| 6 | `DepartmentApplicationArchitectureTests.No_other_handler_takes_the_hierarchy_lock` |
| 4 | `PayrollHostBoundaryTests.Every_route_out_of_payroll_is_satisfied_by_a_test_stub` |
| 4 | `PositionApplicationArchitectureTests.No_position_read_scope_can_be_constructed_from_outside_the_application` |
| 4 | `PositionScopeResolverTests.No_read_scope_exposes_a_public_constructor_or_factory` |
| 3 | `PositionApplicationArchitectureTests.Every_position_read_takes_its_own_scope_as_the_first_parameter` |
| 3 | `PositionApplicationArchitectureTests.The_ordinary_update_carries_no_status_and_no_company` |
| 3 | `DepartmentArchitectureTests.No_department_foreign_key_cascades` |
| 3 | `DepartmentArchitectureTests.No_department_table_has_a_branch_column` |
| 3 | `DepartmentArchitectureTests.No_department_type_has_a_property_named_branch_id` |
| 2 | `EntitlementPermissionCouplingTests.No_tenant_authorization_handler_takes_an_entitlement_dependency` |
| 2 | `ImportExportArchitectureTests.Both_run_records_are_tenant_owned_and_append_only` |
| 2 | `ImportExportArchitectureTests.The_contributor_maps_each_run_record_to_a_tenant_table` |
| 2 | `ImportExportRunDomainTests.Both_run_records_are_tenant_owned_append_only_and_never_branch_owned` |
| 2 | `GradeDomainTests.Both_grade_aggregates_are_tenant_and_company_owned_and_never_branch_owned` |

⚠ **The four `Both_…` / two-row rows are a DIFFERENT SHAPE and probably fine: *both* names its own arity,
so the name cannot claim more than the list holds. A name is only dangerous when its quantifier is open.**

## ⚠ ITEM 241 ALREADY COVERS THREE OF THESE, AND ONE OF THEM FOR FREE

**`Every_department_mutation_requires_a_row_version` and `No_other_handler_takes_the_hierarchy_lock` are
the two 241 was written for.** ⚠⚠ **`No_department_command_carries_a_tenant_identifier` HAND-NAMES THE
SAME EIGHT COMMANDS 241 ALREADY DERIVES** — so the third partition costs one assertion against a
population the patch computes anyway. **Folding it in rather than filing it.**

## What this pass did not do

- **No build, so no count was verified against a live assembly** — every population size here is read from
  source text. ⚠ **The `PlatformSupportAuthority` hand-list has NOT been compared to the derived set.**
- **The 42 `VALUES` rows were excluded by instrument and not individually read.**
- **`PropagatedErrorMappingTests.Surfaces` is hand-written but carries a stated scope and an explicit
  anti-vacuity floor** — recorded as the best of the four, not cleared.

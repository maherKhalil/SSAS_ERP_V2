# item 162 — closing four absence claims by exercising the path

**Gated work.** `tests/API.Tests/Infrastructure/EntitlementPermissionCouplingTests.cs`, **7 tests**,
`GATE_SCOPE=TASK` **green**. No `src/` change.

Item 161 published nineteen "not implemented" claims, four of which rested on a search: `AC-SUB-0013`,
`0024`, `0025`, `0026` were recorded as *"no entitlement-to-permission coupling was FOUND"*. **A search
that found nothing is a statement about the search.** This replaces three of the four with a test.

## The finding: there is no coupling, and now that is proven rather than unrefuted

The tenant permission decision is made by `PermissionAuthorizationHandler`, which consults exactly three
things: a validated tenant, `TenantStatus` via `LiveTenantEligibilityAuthorization`, and the caller's
`permission` claims. **Entitlement is not among them, and it is not reachable from there.**

| criterion | before | now |
|---|---|---|
| `AC-SUB-0025` permission stops being satisfied when entitlement lapses | not found | **CLOSED — not implemented, proven** |
| `AC-SUB-0024` permission for an unentitled module refused at grant time | not found | **CLOSED — not implemented, proven** |
| `AC-SUB-0013` token carries no entitlement-derived claim | not found | **CLOSED for the decision path** — the permission decision reads claims only, and no entitlement reaches them |
| `AC-SUB-0026` losing entitlement deletes no row | not found | ⚠ **NOT CLOSED — see below** |

## What the tests do

- `A_permission_check_succeeds_while_the_tenant_entitlement_has_lapsed` — the decision is unchanged with a
  term that ended a day before the clock.
- `The_permission_check_consults_the_entitlement_reader_zero_times` — and it never asked.
- `No_tenant_authorization_handler_takes_an_entitlement_dependency` — over `PermissionAuthorizationHandler`
  and `RoleAuthorizationHandler`, by reflection over constructor parameters.
- `Granting_a_permission_to_a_role_takes_no_entitlement_dependency` — `AC-SUB-0024`, same technique on
  `AssignPermissionToRoleCommandHandler`.

⚠ **The structural pair is the half that survives a refactor.** An outcome test states today's behaviour;
these redden the moment anyone gives either handler an entitlement collaborator, whatever behaviour
results. That is what the item asked for and what a search can never give.

## ⚠ Both controls, and both were planted

**A zero-consultation assertion is worthless if the reader was unreachable.**

- `The_module_gate_consults_the_same_reader_which_is_what_stops_the_zero_being_vacuous` drives the **same
  reader type** through `RequireModule` and requires it to come back consulted, with the `403`.
- `The_permission_check_still_denies_without_the_claim_while_entitlement_is_lapsed` proves the permission
  gate is live rather than inert.

**Planted, both, and each reddened only its own test:**

| plant | result |
|---|---|
| gave `PermissionAuthorizationHandler` an optional `ITenantEntitlementReader` parameter | `No_tenant_authorization_handler_takes_an_entitlement_dependency(PermissionAuthorizationHandler)` **FAILED**, 6 passed |
| removed `.RequireModule(ModuleKey)` from the control route | `The_module_gate_consults_the_same_reader…` **FAILED**, 6 passed |

Both reverted, 7 green. *(The file was staged before planting, so the revert restored it from the index
rather than from `HEAD` — an unstaged plant reverts to nothing and the emptiness reads as success.)*

## ⚠ `AC-SUB-0026` is NOT closed, and here is what blocked it

The criterion asks that losing entitlement **delete no row** in the module's tables — counts before and
after, identical. **The path cannot be exercised, because there is no entitlement-lapse EVENT to exercise.**
Entitlement is resolved by reading: `HasExpiredAt` is a pure function of the term against the clock, no
row is written when a term ends, and no job runs. There is no "losing entitlement" moment at which a
deletion could occur, so there is nothing to observe before and after.

**I have not substituted another search for it.** The honest position is that the criterion's premise — an
event that could delete rows — does not exist in this design, which makes it closer to bucket 4 than to
bucket 3. **That reclassification is a judgement about the criterion and belongs to whoever owns the
package, not to this test.**

## Scope

- These tests cover the **authorization decision** and the **grant** path. A coupling composed elsewhere —
  in a claims-issuing path, or in a module handler doing its own entitlement check — is outside them.
- `AC-SUB-0013` is closed **for the decision path only**: the handler reads claims, and no entitlement
  reaches those claims by any route these tests exercise. The full criterion also asserts the token's
  claim set is exactly FP-002's, which `PlatformAccessTokenClaimsTests` covers for the **platform** plane
  and nothing covers for the tenant plane.

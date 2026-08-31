# item 207 — the five are closed, and none of them was unbuilt

**Report only.** ⚠ **All five are IMPLEMENTED. Three are pinned by a named test whose body I read; two are
guaranteed structurally with the gate itself pinned elsewhere. NOT ONE is "not implemented".**

**FP-015's provisional nine becomes TWELVE pinned of fourteen**, with the remaining two implemented and
argued below rather than promoted or demoted to close the row.

## ⚠⚠ FIRST: THE FOUR CANDIDATE FILES WERE THE WRONG FOUR, INCLUDING ONE HOMONYM

The four I named in item 206 came from **name matching**, and the search outran them immediately.

⚠ **`AttendanceRouteInventoryTests` does NOT cover `AC-SS-0013`.** Its "Entitlement" is **leave
entitlement — days of leave** — *"Entitlement is settable; consumed is not (`AC-ATT-0040`)"*. **A homonym
of module entitlement, and my own candidate list was built on it.**

**The coverage actually lives in files I had not named:** `ModuleEnablementGateTests`,
`ExpiredTenantGateTests`, `UserEmployeeResolverSeamTests`, `EmployeeTerminationAccountClosureTests`, and
two source files that name the criteria by ID.

## The five, criterion by criterion

### `AC-SS-0010` — the mapping survives termination · **PINNED**

`UserEmployeeLink.cs:68` — *"**Termination is not such an event.** `REQ-SS-0006` requires the link to
survive it, because severing it makes a terminated employee's retained payslips unattributable. **Never on
the link** — the spec records that in four places because it is the single most likely implementation
mistake in the package."*

**Pinned by `UserEmployeeResolverSeamTests.The_link_is_untouched_by_a_refusal`** — the refusal happens and
the link is unchanged, asserted on the mapping itself, which is what the criterion demands.

### `AC-SS-0011` — terminated employees' records remain attributable · **PINNED, by the same test**

The criterion says so itself: *"**This is the criterion that fails if anyone implements `AC-SS-0012` by
severing the mapping.**"* ⚠ **So its failure mode IS severing — and `The_link_is_untouched_by_a_refusal`
pins exactly that.** Reinforced structurally: termination is a **status transition** on `Employee`
(`Employee.cs:407`), not a delete, so no attribution can be lost.

### `AC-SS-0012` — a terminated employee cannot reach self-service · **PINNED**

⚠ **The source names the criterion by ID**, corrected in T-090 under `DEC-L-073`:

> *"**`AC-SS-0012` is closed at the RESOLVER** — `UserEmployeeResolver` refuses to resolve a terminated
> employee, per request and against live state, because permissions travel in an access token's claims and
> deactivating an identity cannot close one already issued. The identity guard is real and is `T-091`.
> **Two guards, neither on the link.**"*

**Pinned by `UserEmployeeResolverSeamTests.An_ended_employment_does_not_resolve`**, with the second guard
pinned by `EmployeeTerminationAccountClosureTests.Termination_closes_the_tenant_user_account`.

### `AC-SS-0013` / `AC-SS-0014` — module gating and expiry · **IMPLEMENTED, GUARANTEED BY CONSTRUCTION**

⚠ **The self routes are inside the group that carries the gate**, and both modules say so in source:

> *"It sits in the same group as everything above, so `RequireModule` and the `BR-PLT-0008` gate come free —
> **`REQ-SS-0008` costs nothing to satisfy and cannot be forgotten.**"*
> — `PayrollEndpointRouteBuilderExtensions:129`, and the same sentence at `Attendance…:151`

**The gate's behaviour is pinned**, on a probe route: `ModuleEnablementGateTests`
(`A_route_of_a_module_the_tenant_does_not_have_is_refused_with_403`) and `ExpiredTenantGateTests`
(`An_expired_tenant_is_refused_a_gated_route_with_403`, and the platform plane still reachable — which is
`AC-SS-0014`'s second half, `DEC-L-033`).

⚠ **WHAT IS NOT ASSERTED IS THE COMPOSITION: no test says "the SELF route specifically is refused when the
module is off."** The gate is proven; membership is structural and commented; **their conjunction is
inferred, not measured.**

**I am not calling that pinned, and I am not calling it unbuilt.** It is **implemented and structurally
guaranteed**.

**What would settle it:** one test per module hitting `/me/payslips` and `/me/records` on a tenant without
the entitlement and asserting 403 — the gate suites already have the harness, so it is a small addition.
**Not built here: 207 is report-only.**

## The tally

| bucket | count | criteria |
|---|---|---|
| **pinned by a named test** | **12** | the nine from item 206, plus 0010, 0011, 0012 |
| **implemented, guaranteed by construction, composition unasserted** | **2** | 0013, 0014 |
| not implemented | **0** | — |
| subject undefined | **0** | — |
| vacuously satisfied | **0** | — |

## Scope
- **Bodies read, not names** — for `UserEmployeeResolverSeamTests`, `EmployeeTerminationAccountClosureTests`,
  `ModuleEnablementGateTests`, `ExpiredTenantGateTests`, and the two route-builder sources.
- ⚠ **`AC-SS-0011`'s attribution claim is argued from the criterion's own stated failure mode plus the
  status-transition mechanism.** I did not execute a terminated employee's payslip query end to end; a
  reader who wants that stronger form should say so.

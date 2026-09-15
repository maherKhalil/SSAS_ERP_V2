# item 209 — the composition is asserted, and FP-015 is fourteen of fourteen

**Gated work.** `SelfServiceModuleGateTests` — 4 tests, green. Plant verified. **TASK gate green,
0 warnings.**

## What was missing, precisely

The gate's **behaviour** was proven (`ModuleEnablementGateTests`, `ExpiredTenantGateTests`) — **on a
synthetic `/gated` probe route.** The self routes' **membership** of the gated group was commented in
source. ⚠ **Their conjunction was asserted nowhere**, which is why item 207 held `AC-SS-0013`/`0014` at
*implemented but not pinned*.

## ⚠ Why it asserts METADATA and not a 403 — and why that is the stronger choice here

A 403 needs a host serving the **real** self routes with entitlement answering false. The module hosts
register `AlwaysEntitled` via `ModuleEndpointHostRequirements` and share **one fixture per class**, so
there is no per-test override; a second host mapping real module endpoints would duplicate their whole
dependency graph **to re-prove what the probe route already proves**.

`RequireModule` attaches `ModuleEnablementMetadata` to every endpoint it gates. **Asserting that metadata
on the real running route IS the composition claim** — *this specific route is inside the gated group* —
and nothing else. ⚠ **The gate's behaviour is deliberately not re-proven: that would be the probe test
written twice.**

## The plant: the COMPOSITION, not the gate

Moved `/me/payslips` out of the group onto `endpoints` **at the same path**, so the route still exists and
still resolves — **only its gate is gone.**

**Result: the `/api/payroll/me/payslips` case reddens; the other two routes and the control stay green.**
That is the composition failing in isolation, which is exactly what the ruling asked for. Restored from the
index; 4 of 4 green.

**Two controls, not one:**
- **route existence** is asserted before its metadata is read — a renamed route would otherwise pass over
  nothing, the vacuity that retired `DEC-L-030`'s guard;
- ⚠ **`The_module_gate_is_applied_to_the_whole_group_not_only_to_self_routes`** — if these three were the
  ONLY gated routes, the "comes free from the group" argument in source would be false. It finds gated
  administrative routes the self routes know nothing about.

**FP-015 is now 14 of 14 pinned.**

## Scope
- ⚠ **Metadata presence is a faithful proxy for the gate, not the gate.** Both come from the same
  `RequireModule` call, so they cannot diverge today — but a future `WithMetadata` without the filter would
  satisfy this test and gate nothing. **The plant tests removal from the group, which is the realistic
  failure; the synthetic one is named here rather than guarded.**

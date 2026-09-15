# item 185 — one entitlement decision, one clock

**Gated work.** `GATE_SCOPE=TASK` green, condition 4 satisfied. **Prevention, not a live defect.**

## What changed

`TenantEntitlementReader` now takes `IDateTimeProvider` and reads `clock.UtcNow` where it read
`DateTimeOffset.UtcNow` directly. `TenantModuleEntitlement` already did.

**Both halves of the entitlement decision now take their instant from the same injectable source.**

## ⚠ EF translation does not prevent it — the ruling's condition did not apply

The ruling allowed for translation blocking the injected provider. It does not: `now` is read **before**
the query and captured, so it reaches SQL as a parameter exactly as the direct call did. The `Where` is
unchanged, the index seek the file documents (`UX_TenantSubscriptions_Tenant_EffectiveFromDesc`) is
unaffected.

**Production behaviour is identical** — `UtcDateTimeProvider` returns `DateTimeOffset.UtcNow`. Nothing
observable changed; what changed is that the instant became controllable.

## Why this is prevention

Nothing today seeds a future-dated subscription record, so the two clocks never disagree. **The hazard is
that they could**: a caller setting the injected clock would have had this half silently ignore it, and
**that is exactly the shape of item 182's defect** — a fixture clock feeding one side of a comparison
while the other side read wall time.

## ⚠ Condition 4, judged rather than waved through

The gate flagged `src/` changing with no suite total moving, and it was right to: **the change has no
observable behaviour, so no behavioural test could move.** The honest response was not to explain the
flag away but to pin the seam, so `EntitlementClockArchitectureTests` asserts both participants take an
injectable clock.

**A behavioural test would be better and is not available here:** proving the reader honours the injected
instant needs a seeded future-dated record and a real `PlatformDbContext`, which lives in
`Integration.Tests` — the suite the TASK gate does not run (item 176). The structural assertion is the
half that is reachable.

**Plant:** reverting the reader to a direct clock read reddens
`Both_halves_of_the_entitlement_decision_take_an_injectable_clock` for that participant and no other. The
guard carries its own control — a type with no single public constructor fails loudly rather than passing
over nothing.

## Scope

- **Two participants**, named explicitly. The guard does not sweep the entitlement namespace, so a third
  participant added later is not covered — the same hand-written-list limit item 169 measured, at a scale
  of two.
- The change is a constructor signature, so every construction site had to move; one existed, in
  `TenantEntitlementResolverSqlServerTests`, and it already had a `TestClock` to hand.

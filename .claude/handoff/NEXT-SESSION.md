# Where to pick up — written 2026-08-26, 21:47

Read `BOARD.md` first; this is the short version of what it says.

## Nothing is in flight

T-048's diagnosis is **partly done and deliberately half-open**. Its result file carries what was
established; the rest is the first task of the next session.

## T-048 — the one that matters, and it is now smaller than it looked

**The scare is over: no committed migration was scaffolded destructively.** All 13 tenant migrations
carry zero `DropTable` in `Up`. It is a **tooling hazard, not a data one**.

**Established:** `TenantDbContextDesignTimeFactory` constructs `TenantDbContext` with four arguments
and omits its optional `IEnumerable<ITenantModelContributor>?`, so the design-time model has no module
entities. `Up` emits exactly 32 `DropTable` — 7 Attendance, 6 GL, 6 Payroll, 13 HR — which is exactly
the surface those four contributors configure.

**NOT established, and this decides the fix:** the factory has *never* supplied contributors
(`git log -S` finds nothing, ever) and predates every module migration — yet **thirteen were scaffolded
correctly anyway.** Something else supplied a complete model then and nobody knows what.

**Candidates, none tested:** a startup project whose service provider EF preferred; a different
context or command; hand-authored migrations.

**Why it matters:** if something else supplied the model, **the factory is not what changed** — and a
fix to the factory would look correct and be beside the point. Test that before proposing anything.

## Then, roughly in order

- **T-049 (mine)** — `.claude/settings.json` denies `Bash(git push origin main:*)`, which **does not
  match a bare `git push` from a checked-out `main`**. `DEC-L-007`'s "direct pushes stay denied" has
  been habit, not enforcement. Found by a harmless docs stamp landing on `main` without a PR.
  **Needs the owner's approval — it is their config.**
- **T-038** — sweep the suites for name-based absence guards (`DEC-L-031`; two found by accident).
- **T-042** — distributed entitlement-cache invalidation, owed **before** the product runs behind more
  than one instance.
- **T-025** — enumerate `depends_on` across ADR-001..016 and 023..030.
- The citation-side propagation checker — closes about **half** the class; the prose half is not
  mechanically checkable (measured: 4 of 12 sites carry structured markers).

## Two standing facts the next session should not rediscover

- **The loop has no self-starting property** (`DEC-L-042a`). When both windows are idle, only the
  owner restarts them.
- **One working tree, two windows** (`DEC-L-013`). Verify with `git show <branch>:<path>`, never by
  reading the tree while the other holds a branch, and never build while the other is mid-gate.

## Product state

`main` builds clean at 0 warnings with **3,486 tests green** in both configurations.
`BR-PLT-0008` is enforced — modules gate on subscription, tenants get a 14-day all-module trial,
expiry costs modules rather than the door.

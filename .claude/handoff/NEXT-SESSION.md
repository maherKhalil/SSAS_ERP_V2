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

**A narrowing arrived late and it rules out one whole branch of reasoning.** `git show ea335c1^`:
before the contributor mechanism existed, `OnModelCreating` applied **only** the Platform-assembly
tenant configuration namespace. So **the design-time model appears never to have contained module
entities, by any route** — while thirteen module migrations were scaffolded correctly anyway.
**That kills "the contributor refactor broke it"** and leaves *something else supplied the model*
as the surviving shape. It is a narrowing, not a cause.

**Candidates, none tested:** a startup project whose service provider EF preferred; a different
context or command; hand-authored migrations.

**No fix is proposed, deliberately.** The obvious repair — pass the contributors in the factory —
is untested against the question of *why it was ever unnecessary*. Proposing it now would be a fix
to the understood half, recommended for a defect only half explained (`DEC-L-045`).

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

## Who was in the windows (`DEC-L-050`)

Written by each side for itself. A name here is how the other window addresses you with
`SendMessage`; a name that is stale is worse than absent, because it fails as a *delivery*
rather than as a lookup. Rewrite your own line on resume - do not trust the one you find.

| Role | Session name | Last written |
|---|---|---|
| Architect | `ssas-erp-v2-88` | 2026-08-26 23:05 |
| Coder | `ssas-erp-v2-37` | 2026-08-26 23:05, by the architect - **coder to confirm or correct** |

The coder's line is second-hand: read from `ListAgents`, not written by the coder itself.
It is recorded because an unverified pointer that can be checked beats no pointer at all -
but the coder owns that row, and should overwrite it on resume rather than assume it holds.

## Standing down - 2026-08-26 23:05

Owner's instruction, both windows. Nothing is queued and nothing is running. `main` is at
`92961bb`; T-050 is merged. The work that was *not* scheduled to tomorrow by either window's
own initiative is listed under the open items above, in the order it was left.

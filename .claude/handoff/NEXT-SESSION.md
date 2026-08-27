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
| Architect | `ssas-erp-v2-b4` | 2026-08-27, rewritten after a restart |
| Coder | `ssas-erp-v2-aa [a7865a]` | 2026-08-27, written from `ListAgents` this session. The `ssas-erp-v2-37` recorded yesterday was indeed **dead**. |

The coder's line is second-hand: read from `ListAgents`, not written by the coder itself.
It is recorded because an unverified pointer that can be checked beats no pointer at all -
but the coder owns that row, and should overwrite it on resume rather than assume it holds.

## Standing down - 2026-08-26 23:05

Owner's instruction, both windows. Nothing is queued and nothing is running. `main` is at
`92961bb`; T-050 is merged. The work that was *not* scheduled to tomorrow by either window's
own initiative is listed under the open items above, in the order it was left.

---

# CURRENT ASSIGNMENT — read this first

**Coder: your task is `T-055`.** Full specification in `.claude/handoff/tasks/T-055.md`. Read it
there, not from this summary.

**One-line version:** `scripts/gate.sh` calls itself `THE PHASE-EXIT GATE` and has been run per
task. Its own header measures why that hurts — Integration Debug 32 m 21 s, Release 32 m 35 s,
against a whole-run figure of 69 minutes. **The two Integration legs are 64 m 56 s of a 69-minute
gate.** Add `GATE_SCOPE` so a per-task run does not buy a phase-exit answer.

**It is a NON-GATED task by the letter of the rule** — nothing under `src/` or `tests/` is in
scope, only `scripts/`. So: **push, and wait for `MERGE T-055`.** Do not self-merge. That is not
ceremony; it is a change to the instrument that decides whether everything else may merge, and it
is the one file where a plausible-looking edit has already twice produced a gate that reported
success on a failing run.

**Verify, do not assume, that a red suite still exits non-zero under both scopes.** T-016 found that
hole in a script that looked correct, and `DEC-L-029` found the same defect class again hours later
by the same person who had just diagnosed it.

Before starting: rewrite your own row in the pointer table above (`DEC-L-050`). Yesterday's coder
name is dead — the architect's changed too, within hours of being written down. **Do not trust a
name you did not write this session.**

## Rules issued 2026-08-26 that did not exist when you last worked

- **`DEC-L-051`** — the gate is tiered. This task implements it. **`GATE_SCOPE` does not work yet**;
  `CODER.md` carries a not-yet-implemented banner that you delete when this lands.
- **`DEC-L-052`** — the architect sizes tasks to amortise the gate. Its half, not yours.
- **`DEC-L-053`** — you may write code while a gate runs, in a **second worktree**
  (`git worktree add ../SSAS_gate <branch>`), never in the tree under test. One gate at a time;
  build sparingly during a run.

## Queued behind T-055

`T-051`→`T-054` are a written four-task partition for parallel module work (Assets and Sales), and
`T-049` (a `settings.json` deny-list gap) is **the owner's to decide, not ours** — do not touch
permission settings.

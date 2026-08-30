# Open work — reckoned 2026-08-30, from git history

**The previous reckoning was dated 2026-08-27 and listed `T-051` as the frontier. The loop is at `T-232`,
232 merges later.** Everything below was re-derived, not carried forward.

**Do not derive this from which task files lack a result file.** That method was tried and is wrong:
`.claude/handoff/results/` only goes back to `T-045`, so the absence of a result proves nothing about
anything older. It reported `T-046`, `T-050` and `T-057` as open when all three were done. **Absence of a
result file reads identically to a task never started.**

**Method that works:** `git log origin/ClaudeBranch --grep="T-0NN"`. A task with commits but **no merge**
has only the commit that created its file. **A task with a merge has been worked.**

---

## Genuinely open — re-verified 2026-08-30

| Task | Spec? | Evidence | State |
|---|---|---|---|
| **T-051** Scaffold Assets + Sales skeletons | yes | 2 commits, **0 merges** | never started |
| **T-052** Fixed Assets domain + application | yes | 1 commit, **0 merges** | never started; runs parallel with T-053 |
| **T-053** Sales domain + application | yes | 1 commit, **0 merges** | never started; Sales has no Inventory to consume, and the spec tells it to declare that and land narrower |
| **T-054** Wire both modules up, once | yes | 2 commits, **0 merges** | never started; blocked on T-052 and T-053 |
| **T-038** Sweep suites for name-based absence guards | **no spec** | 4 commits, **0 merges** | carried on the board as owed; no task file |
| **T-042** Distributed entitlement-cache invalidation | **no spec** | 1 commit, **0 merges** | owed before multi-instance deployment; no task file |
| **T-048 Option A** Make the wrong scaffolding path refuse | partial | T-048 merged (PR #97) | left unresolved because its cost was never established — it may also break `database update` |

**Note on T-051→T-054:** Fixed Assets is **V3** and Sales is **V4** on the product roadmap while V1/V2 is
unfinished. The partition is sound and retargets to any two independent modules unchanged.

## ⚠ Closed since the last reckoning — one correction

- **T-025** (enumerate `depends_on` across the ADRs) **was listed as open and is not.** It now has **8
  commits and 2 merges** — PR #133 created the task, **PR #135 landed a 170-line result and an ADR
  change.** The old entry said *"1 commit — a reference from another rule, no work"*, which was true when
  written and stopped being true.

**Also closed, and previously recorded as such:** T-046 (PR #88), T-050 (PR #99/#100), T-057 (folded into
T-058 by design), T-049 (resolved structurally by `DEC-L-058` — the loop moved to `ClaudeBranch` and `main`
is frozen).

`main` still has no server-side branch protection (`404 Branch not protected`) — **one setting, the
owner's, no longer urgent because nothing pushes there.**

---

## What is not a task but is owed

- **The device-leak janitor question is closed** (T-217): the gate now reaps stale backup devices at
  startup, because a recorder at teardown is structurally blind to a process that died.
- **Per-view rule extraction for the 54 clinical rule-encoding views.** Deliberately not started — it is
  the specification for part of the new system and **waits on the owner deciding whether HIS proceeds.**
  Their shape is established (T-231); their content is not.
- **The five `float` column names nobody can resolve from the schema** — `MISSING`, `Cleaving`, `Rael`,
  `Others`, `Modified`. **Needs someone who knows the Finance module.** That is the entire residue of a
  1,486-column analysis.

---

## What the roadmap says is actually next

`docs/00-Master-Product-Specification/Product-Roadmap.md` — **Self Service is the last item of V2**, after
Recruitment and Performance. Fixed Assets is V3; Sales is V4.

**`FP-015` does not exist.** The feature packages stop at `FP-014-subscription`, so Self Service needs a
**specification package first**, the way FP-014 did — not an implementation task.

`ADR-030-Identity-To-Employee-Mapping` exists and is what unblocks it: the identity→employee link is a
Platform-plane mapping keyed by tenant, optional on both sides, **no foreign key in either direction and
none possible** — different databases. Self Service is its first consumer.

⚠ **A CLAIM CARRIED FORWARD FROM THE PREVIOUS RECKONING AND NOW CHECKED: it said an owner decision was
needed on whether *"view my own payslip"* is a distinct permission or a scope on the existing one. THE CODE
ANSWERS IT.** Three distinct self-service permissions exist — `Attendance.Records.ViewOwn`,
`Attendance.Leave.ViewOwn`, `Payroll.Payslips.ViewOwn` — and the first two are already wired to live
routes. **It is a distinct permission, decided by construction.**

**What IS still open is `OWNER-DECISIONS.md` entry 4**, and it is a different question: permissions reach a
user only through a role, there is no per-user grant mechanism anywhere in `src/`, and **none of the three
self-service permissions appears in any seeded role.** So granting self-service to a workforce has no bulk
mechanism — that is the decision, not the permission model.

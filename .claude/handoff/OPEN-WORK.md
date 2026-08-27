# Open work — reckoned 2026-08-27, from git history

**Do not derive this from which task files lack a result file.** That method was tried and is wrong:
`.claude/handoff/results/` only goes back to `T-045`, so the absence of a result proves nothing about
anything older. It reported `T-046`, `T-050` and `T-057` as open when all three are done. **Absence of
a result file reads identically to a task never started** — the same shape as the memory sampler, in
the handoff directory.

**Method that works:** `git log origin/ClaudeBranch --grep="T-0NN"`. A task with one commit has only
the commit that created its file. A task with a merge has been worked.

---

## Genuinely open

| Task | Spec? | State |
|---|---|---|
| **T-051** Scaffold Assets + Sales skeletons | yes | **Never started.** 1 commit, 0 merges. |
| **T-052** Fixed Assets domain + application | yes | **Never started.** Runs parallel with T-053. |
| **T-053** Sales domain + application | yes | **Never started.** Sales has no Inventory to consume; the spec tells it to declare that and land narrower. |
| **T-054** Wire both modules up, once | yes | **Never started.** Blocked on T-052 and T-053. |
| **T-025** Enumerate `depends_on` across `ADR-001..016`, `023..030` | yes | 1 commit — a reference from another rule, no work. **`DEC-L-057` found the decision register has the same defect**, one register over. |
| **T-038** Sweep suites for name-based absence guards | **no spec** | Carried on the board as owed. No task file. |
| **T-042** Distributed entitlement-cache invalidation | **no spec** | Owed before multi-instance deployment. No task file. |
| **T-048 Option A** Make the wrong scaffolding path refuse | partial | T-048 merged (PR #97); Option A left unresolved because its cost was never established — it may also break `database update`. |

**Note on T-051→T-054:** Fixed Assets is **V3** and Sales is **V4** on the product roadmap, while
V1/V2 is unfinished. The partition is sound and retargets to any two independent modules unchanged.

## Closed, contrary to the filename method

- **T-046** — merged, PR #88.
- **T-050** — merged, PR #99/#100.
- **T-057** — folded into **T-058** deliberately; no separate result by design.
- **T-049** — the deny-list gap. **Resolved structurally by `DEC-L-058`**: the loop moved to
  `ClaudeBranch` and `main` is frozen. The rule is still unmatched by a bare `git push`; it now has
  nothing to damage. `main` still has no server-side branch protection (`404 Branch not protected`) —
  one setting, the owner's to enable, no longer urgent.

---

## What the roadmap says is actually next

`docs/00-Master-Product-Specification/Product-Roadmap.md` — **Self Service is the last item of V2**,
after Recruitment and Performance. Fixed Assets is V3; Sales is V4.

**`FP-015` does not exist.** The feature packages stop at `FP-014-subscription`. So Self Service
needs a **specification package first**, the way FP-014 did — not an implementation task.

`ADR-030-Identity-To-Employee-Mapping` exists and is what unblocks it: the identity→employee link is
a Platform-plane mapping keyed by tenant, optional on both sides, **no foreign key in either
direction and none possible** — different databases. Self Service is the first consumer of it.

**One owner decision is needed before the package can be shaped** — whether "view my own payslip" is
a distinct permission or whether self-access is a scope on the existing one. It changes every
self-service surface, so it is not deferrable to implementation.

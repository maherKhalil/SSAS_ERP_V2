# Two-window loop: Architect ↔ Coder

Two Claude Code windows, both open on this folder
(`C:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2` — **not** the stale duplicate one level up).

| Window | Command      | Does                                                             |
| ------ | ------------ | ---------------------------------------------------------------- |
| A      | `/architect` | Holds the module map, issues one task at a time, judges results. |
| B      | `/coder`     | Implements that task, runs the CI gate, commits, pushes, reports. |

Start **B first**, then A — the architect checks that the coder is up.

## Layout

```
.claude/
  roles/PROTOCOL.md      the contract: discovery, message envelope, loop, hard rules
  roles/ARCHITECT.md     architect charter
  roles/CODER.md         coder charter
  commands/architect.md  /architect
  commands/coder.md      /coder
  handoff/
    BOARD.md             task board + decisions log (architect-owned)
    TASK-TEMPLATE.md     shape of a task spec
    RESULT-TEMPLATE.md   shape of a result report
    tasks/T-###.md       issued specs
    results/T-###.md     delivered reports
    session/*.txt        each window's SendMessage name (git-ignored, machine-local)
```

## The loop

Architect writes `tasks/T-###.md` → messages the coder → coder implements on
`agent/T-###-<slug>`, runs build + Architecture/Platform/HR/API tests, commits, pushes,
writes `results/T-###.md` → messages back → architect verifies the diff itself and either
sends `REVISE` or accepts and issues the next task.

Both windows end their turn after sending; the incoming message wakes the other. No polling.

## If a window is closed or compacted

Re-run its slash command. Everything needed to resume is on disk: `BOARD.md` says what is in
flight, `tasks/` and `results/` hold the full history. Only the session pointer files are
transient, and startup rewrites them.

## Pull requests

Since `DEC-L-007` (owner ruling, 2026-08-25) the loop merges its own work through PRs:

- **A task touching `src/` or `tests/`** merges itself the moment its gate is green — `gh pr create`
  then `gh pr merge --merge --delete-branch`. The architect reviews afterwards and raises a follow-up
  if needed.
- **A task touching neither** — docs, tooling — has no gate to be green (`DEC-L-002`), so it pushes
  and waits for the architect's `MERGE T-###`.

Direct pushes to `main` stay denied in `settings.json`. Every merge leaves a PR on GitHub, which is
how FP-006 … FP-013 already landed.

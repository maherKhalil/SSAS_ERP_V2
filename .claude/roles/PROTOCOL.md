# Architect ↔ Coder Protocol

Two Claude Code windows, both opened on `C:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2`
(the real repo — the parent folder `Documents\SSAS_ERP_V2` is a stale duplicate, never work there).

- **Window A — Architect.** Holds the whole-system picture. Never writes production code.
- **Window B — Coder.** Implements exactly one task at a time. Never chooses what to build next.

They talk over `SendMessage`. Every message is also written to disk, so the loop survives a
window being closed, compacted, or restarted.

---

## 1. Discovery — how the windows find each other

`ListAgents` tells a session its own name and lists its peers. On startup each role writes its
own name to a pointer file, then reads the other's:

| Role      | Writes                                  | Reads                                    |
| --------- | --------------------------------------- | ---------------------------------------- |
| Architect | `.claude/handoff/session/architect.txt` | `.claude/handoff/session/coder.txt`      |
| Coder     | `.claude/handoff/session/coder.txt`     | `.claude/handoff/session/architect.txt`  |

A pointer file holds one line: the exact session name as `ListAgents` prints it, e.g.
`ssas-erp-v2-51 [61fe14]`. Send with `SendMessage({to: "<that name>", message: "..."})`.
Include the `[ref]` suffix only if two peers share a name.

If the peer's pointer file is missing, the other window is not up yet. Say so and stop —
do not guess a name, and do not do the other role's job yourself.

---

## 2. The message envelope

Every `SendMessage` body starts with one header line, then a short human summary.
The detail always lives in the referenced file, never in the message.

**Architect → Coder**

```
TASK <task-id> | <branch> | file: .claude/handoff/tasks/<task-id>.md
<one or two sentences: what and why>
```

**Coder → Architect**

```
RESULT <task-id> | <STATUS> | file: .claude/handoff/results/<task-id>.md
<one or two sentences: what happened, and the test verdict>
```

**Architect → Coder, follow-up on a task already delivered**

```
REVISE <task-id> r<n> | file: .claude/handoff/tasks/<task-id>.md#revision-<n>
<what is wrong or missing, and the acceptance bar that is still unmet>
```

**Coder → Architect, while a long task is still running**

```
PROGRESS <task-id> | <elapsed> of ~<estimate> | <what is running now>
<what has completed, what is in flight, how much longer, and the evidence it is alive>
```

**Architect → Coder, releasing a non-gated task to merge**

```
MERGE <task-id> | file: .claude/handoff/results/<task-id>.md
<accepted; merge it>
```

**Either direction, out of band**

```
QUESTION <task-id>
...
ANSWER <task-id>
...
ABORT <task-id>
<reason>
```

### Statuses

| Status           | Meaning                                                                 | Architect's next move                        |
| ---------------- | ----------------------------------------------------------------------- | -------------------------------------------- |
| `DONE`           | Acceptance criteria all met, gate green, committed and pushed.           | Verify, then accept or `REVISE`.             |
| `PARTIAL`        | Some criteria met; the rest are done but not proven, or not started.     | `REVISE` with the remaining bar.             |
| `BLOCKED`        | Cannot proceed — build/test failure outside scope, missing dependency.   | Re-scope, or issue a prerequisite task.      |
| `NEEDS-DECISION` | An architectural choice the coder is not allowed to make.                | Decide, record it, `REVISE` with the ruling. |

`DONE` is a claim about evidence, not about effort. A coder that did not run the gate reports
`PARTIAL`, never `DONE`.

---

## 3. The loop

```
Architect                                  Coder
---------                                  -----
pick next task from the roadmap
write .claude/handoff/tasks/T-###.md
update BOARD.md → IN-PROGRESS
SendMessage TASK ───────────────────────▶  read the task file
end turn, wait                             implement, on branch agent/T-###-<slug>
                                           run the gate (build + 4 suites)
                                           commit, push branch
                                           GATED + GREEN -> gh pr create && gh pr merge
                                           NOT GATED     -> stop, wait for MERGE T-###
                                           write .claude/handoff/results/T-###.md
                       ◀───────────────── SendMessage RESULT
read the result file                       end turn, wait
verify independently (diff, spot-run)
   ├─ not good enough → SendMessage REVISE ─▶ (loop)
   └─ accepted → BOARD.md → DONE
                 SendMessage next TASK ────▶ (loop)
```

Each window **ends its turn after sending** — an incoming `SendMessage` wakes it. Neither
window polls, sleeps, or busy-waits.

---

## 4. Hard rules

1. **One task in flight.** The board never shows two `IN-PROGRESS` rows.
2. **The architect does not write production code.** Not "just this one fix". If it is small
   enough to be tempting, it is small enough to be a task.
3. **The coder does not widen scope.** Anything outside the task's *Files in scope* that needs
   changing is reported as `BLOCKED` or `NEEDS-DECISION`, not silently done.
4. **Documentation wins** over both of them — see `docs/START-HERE.md`. Conflicting docs are
   resolved by the ADRs in `docs/14-Engineering/ADR/`, and an unresolvable conflict is a
   `NEEDS-DECISION`, never an invented requirement.
5. **No demo, sample, or prototype code.** This repository is production only.
6. **A green gate is the merge authority for code; the architect is the merge authority for
   everything else.** Ruled by the owner 2026-08-25 (`DEC-L-007`). A task with a file under `src/` or
   `tests/` in scope merges itself when every suite ran and passed. A task without one waits for
   `MERGE <task-id>`, because `DEC-L-002` waived its gate and the architect's review is then the only
   check that exists. Neither role pushes to `main` directly — merging goes through a PR.
7. **Files, not chat, carry the detail.** A message that cannot be reconstructed from the
   repository after a restart is a bug in how the loop is being run.

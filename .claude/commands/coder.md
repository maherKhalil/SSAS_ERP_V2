---
description: Become the Coder window — implement one architect-issued task at a time, test it, commit and push.
---

You are now the **Coder** for SSAS ERP V2, for the rest of this session.

Read these two files in full and follow them for every subsequent turn:

1. `.claude/roles/PROTOCOL.md`
2. `.claude/roles/CODER.md`

Then run your startup sequence:

1. `ListAgents` — take your own session name from the first line and write it to
   `.claude/handoff/session/coder.txt` (one line, the exact name).
2. Read `.claude/handoff/session/architect.txt`. If it exists, send the architect a short
   `READY` message so it knows you are up.
3. Confirm the toolchain once: `dotnet --version` and `git status --short`, and report anything
   already dirty on the working tree.
4. Tell the user you are ready and **end your turn**. Do not pick your own work — wait for the
   architect's `TASK` message to wake you.

$ARGUMENTS

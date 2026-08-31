# item 195 — the fifth site is recorded, not converted

**Comment only. Zero non-comment lines changed — verified by diffing the change against a
comment-and-blank filter, not by inspection.** Builds clean.

Placed at `TenantRestoreVerificationProcessLossSqlServerTests`, immediately above the `ProcessStartInfo`
and `Process.Start` it concerns, so a reader meets it before the code rather than after.

## What it records

| | |
|---|---|
| **the state** | stdout read only until `ADMITTED `, stderr only on EOF *during* the handshake; **both abandoned afterwards** |
| **the deadlock** | ⚠ **not constructible** — at most two short lines after the marker, ~40 bytes against a ~4 KB buffer, ~100× margin |
| **the evidence loss** | ⚠ **real** — `COMPLETED <status>` is emitted **only when the restore outran the observer**, which is exactly the inconclusive-trial case, and nobody reads it |
| **the cost** | **the REASON, not the correctness** — the verdict is reported as inconclusive either way |
| **why `SqlcmdChildProcess` does not fit** | it drains with a single `ReadToEndAsync` that completes only on exit; this parent must match a marker **while the child runs**, so it would trade a pipe-buffer hazard for a **constructible handshake deadlock** |

## ⚠ And the trigger for revisiting, which is the part that makes it a decision rather than a note

**INCONCLUSIVE TRIALS BECOMING FREQUENT** — while they are rare the restructure costs more than the reason
is worth. **Or the host being given more to say after admission, which changes the deadlock arithmetic**:
the ~100× margin is a fact about today's two lines, not a property of the design.

**Both are conditions a future reader can check**, which is the difference between a decision they can
re-open and one they must re-derive.

## Scope
- The comment asserts the host writes at most two lines after `ADMITTED `. That was read from
  `VerificationHost/Program.cs:86, 91, 103` on 2026-08-31 and **is exactly the fact the second trigger
  above tells a reader to re-check.**

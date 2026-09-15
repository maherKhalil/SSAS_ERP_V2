# item 194 — what starts a process at all

**Report only. No conversions made, and the one proposed is a design change rather than a substitution.**

## ⚠ THE ANSWER IS NOT A ZERO — THERE IS ONE ADDITIONAL CLASS, IN ONE FILE

The ruling pre-authorised a zero. **It is not one**, and the site beyond the *redirects-and-discards* set
is real, though smaller and of a different shape than the four.

## The mechanism, closed before counting

| | how a process can be started | sites |
|---|---|---|
| **M1** | `Process.Start(...)` | **6** |
| **M2** | `new ProcessStartInfo(...)` feeding one | **5** |
| **M3** | `new Process()` then `.Start()` | **0** |
| **M4** | any bare `.Start()` under another name | **0** — the single hit is `new Activity(…).Start()`, an OpenTelemetry span |
| **M5** | a third-party launcher (CliWrap, Medallion.Shell…) | **0** — none in `Directory.Packages.props` |

⚠ **M4 is why this is a mechanism search and not a name search:** the unfiltered sweep was what proved no
process start hides behind a differently-named variable, and it turned up a non-process `.Start()` that a
keyword filter would have silently kept or silently dropped.

⚠⚠ **AND THE FINDING THAT BOUNDS EVERYTHING ELSE: `src/` AND `tools/` START NO PROCESSES AT ALL.** The
entire population is test code. **Nothing here is a product defect**, which is the first thing worth knowing
about a hazard of this shape.

## The population: 6 files, 11 sites, three classes

| class | sites | what it is |
|---|---|---|
| **A — the fix** | `SqlcmdChildProcess.cs:67`; `SqlcmdChildProcessTests.cs:72,89` | **not instances of the problem.** The drained wrapper and its own controls |
| **B — converted (189)** | permission boundary `:339,372`; provider `:853,887`; session loss `:163,182` | both streams drained from start, exit code reported |
| ⚠ **C — handshake, then abandoned** | `TenantRestoreVerificationProcessLossSqlServerTests.cs:514,522` | **the additional class** |

**All five `ProcessStartInfo` sites redirect both streams**, so there is no fourth class of *starts a process
and lets it write to the console*.

## ⚠ CLASS C: READ UNTIL THE MARKER, THEN NOTHING

`ReadLineAsync(process, "ADMITTED ", …)` consumes stdout **only until the handshake marker**. Stderr is read
**only** on unexpected EOF *during* the handshake. **After admission both streams are abandoned for the rest
of the child's life.**

The host writes after the marker — `Program.cs:86, 91, 103`:

| line | when |
|---|---|
| `WaitingLine` | `AdmitAndWait` mode, then blocks forever |
| `RestoringLine` | before the real restore begins |
| ⚠ `COMPLETED <status>` | **only when the restore outran the observer — which the parent treats as an INCONCLUSIVE TRIAL** |

### The deadlock: NOT constructible here, and I checked rather than assuming

**At most two short lines follow the marker — on the order of 40 bytes against a ~4 KB pipe buffer.** The
hazard that made draining mandatory for the `sqlcmd` sites **cannot fire at this volume**. Reporting it as a
live risk would be the guard-against-an-unconstructible-failure mistake.

### ⚠ THE EVIDENCE LOSS IS LIVE, AND IT IS THE SAME DEFECT AS 189 BEHIND A HANDSHAKE

**The host emits `COMPLETED <status>` precisely to say why a trial was inconclusive, and the parent never
reads it.** The file's own comment states the condition — *"reached only when the restore outran the
observer"* — so **the one line that explains an inconclusive trial is written and discarded**, exactly as
`sqlcmd`'s stderr was.

**Severity is lower than the four**: an inconclusive trial is reported as inconclusive rather than as a
false pass, so nothing is silently wrong. **What is lost is the reason, not the verdict.**

## ⚠ The conversion I am NOT making, and why it is not a substitution

**`SqlcmdChildProcess` cannot be dropped in here.** It drains each stream to completion with a single
`ReadToEndAsync`, which is incompatible with a line-by-line handshake — **the parent must match `ADMITTED`
while the child is still running.** Anything else would deadlock the handshake instead of the pipe.

A conversion would mean **restructuring**: drain both streams into buffers in the background and have the
handshake match against the buffer rather than the live stream. That is a design change to a fixture whose
comments show its timing was deliberately tuned, and **it buys the reason for an inconclusive trial, not a
correctness fix**. It is proposed, not taken.

## Scope
- **Counted by mechanism across `src`, `tests` and `tools`**, excluding `obj/` and `bin/`.
- **`COMPLETED` is unread — measured, not inferred**: the only `ReadLineAsync` call in the parent takes the
  `"ADMITTED "` prefix, and no reference to `COMPLETED`, `WaitingLine` or `RestoringLine` exists in the test.
- Non-.NET process starts — MSBuild `Exec`, shell scripts under `scripts/` — were **not** surveyed; the
  ruling asked what starts a process in this code, and `scripts/gate.sh` starts many by design.

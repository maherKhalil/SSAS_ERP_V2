# item 187 — the clean PHASE re-run

**Run at `38d3237`, settled tree: `src/` and `tests/` both 0 modified at launch, nothing edited while it
ran.** The four dirty files at start were architect markdown, which is not a build input. **69 min.**

## ⚠ THE HEADLINE: INTEGRATION IS GREEN IN DEBUG FOR THE FIRST TIME SINCE 2026-08-27

`Integration.Tests (Debug)` — **846 passed, 0 failed, 23 m 10 s.** Item 184's fix holds at full-suite
scale, not only in the file. **Anyone can now say what that suite does**, which has not been true for four
days.

## The full result, both configurations

| suite | Debug | Release |
|---|---|---|
| BUILD | **0 warnings, 0 errors** | **0 warnings, 0 errors** |
| Architecture | 618 | 618 |
| Platform | 1101 | 1101 |
| HR | 326 | 326 |
| API | 956 | 956 |
| Finance | 47 | 47 |
| Payroll | 87 | 87 |
| Attendance | 81 | 81 |
| **Integration** | **846 / 846 ✅** | ⚠ **845 / 846 — one failure** |

**175 passed.** Its suites are green in both configurations; the clean re-run it was owed is done and it
is not the finding.

## ⚠ THE GATE IS RED, SO `test-baseline.txt` DID **NOT** GAIN ITS ROWS

`--- baseline: NOT updated (gate is red). .claude/handoff/test-baseline.txt still holds the last green
run's totals.` **The Integration and Release rows are still absent, and the reason is now one test rather
than an unknown suite.** One green Release Integration run closes it.

⚠ **And the gate exited 0 while printing `[GATE RED]`** — the exit status I saw was my own trailing
`echo`, not the gate's. **The counts are the record; the exit code is not.** Same class as the `MSB1009`
trap, third time.

## The single failure

`TenantBackupPermissionBoundarySqlServerTests.With_only_the_granular_permission_the_guard_sees_another_sessions_backup`
— *"the low-privilege guard never observed the competing backup, so the permission boundary is unproven."*
**Failed in 4.1 s against a 30 s deadline**, so it left the poll loop early on `competing.HasExited`, not
by timing out.

**It is NOT deterministic Release behaviour: the same Release binaries pass 4 runs out of 4 in isolation.**

| observation | result |
|---|---|
| Release, full suite under load | **fail** (1 of 1) |
| Debug, full suite under load | pass (1 of 1) |
| Release, isolated, `--no-build`, same binaries | **pass, 4 of 4** |

⚠ **One loaded observation per configuration is not enough to call it Release-specific**, and I am not
calling it that. What is established is that the binaries are fine and the failure needs load.

**Every test in the class ran ~2× faster in Release** (12.8→4.1 s, 19.9→7.3, 12.1→6.5), but that cuts
against a simple speed explanation: the gap the race must survive is between starting the backup and the
first poll, and **going faster shortens it**. The backup itself is an external `sqlcmd` process and is not
rebuilt by either configuration.

## ⚠ THE REAL DEFECT: THE TEST CANNOT SAY WHY IT FAILED

`StartCompetingBackup` sets `RedirectStandardOutput = true` and `RedirectStandardError = true` and
**neither stream is ever read; the exit code is never checked.** The process is used only for `HasExited`
and `Kill`.

**So `HasExited == true` means either "the backup finished before we looked" or "`sqlcmd` never started" —
and the test treats them identically.** That is why I cannot tell you which one happened in this run: the
evidence was discarded at the moment it existed.

Enumerated by mechanism, not by name: **four tests redirect a child's streams, and exactly one reads
them** — `TenantRestoreVerificationProcessLossSqlServerTests:541`. **The correct pattern already exists in
this same suite**, so this is an inconsistency, not a missing capability.

Secondary hazard, latent: redirecting both streams and draining neither is the classic pipe-buffer
deadlock. `Kill(entireProcessTree)` in the `finally` masks it here.

**Not fixed — I do not choose the next task.** The fix is small and the precedent is in-suite.

## Scope
- **One loaded run per configuration.** Load is the distinguishing variable and I have not isolated it.
- Memory was not the cause on the evidence: Release peaked higher (1269 MB vs 942 MB) but had *more*
  free memory throughout (min_free 1695 MB vs 1591 MB), both far above the 2048 MB floor.

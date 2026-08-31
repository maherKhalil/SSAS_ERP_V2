# item 189 — the competing process can say why it died

**Gated work.** Three fixtures converted, one new type, two new controls.

## What was wrong

All three `sqlcmd` fixtures set `RedirectStandardOutput = true` and `RedirectStandardError = true`,
**read neither, and never checked the exit code.** The process was used only for `HasExited` and `Kill`.

**So `HasExited == true` meant either "the backup finished before we looked" or "`sqlcmd` never started",
and every one of these tests treated the two identically.** Item 187's failure is the cost: it reported
*"the guard never observed the competing backup"* with no evidence for which had happened — **and the
evidence existed, in the child's stderr, at the moment it was discarded.**

## `SqlcmdChildProcess`, and why not the existing pattern

Both streams are drained **from the instant the process starts** — two `ReadToEndAsync` tasks created
before the child can write, never awaited until it is gone. `DescribeAsync` reports **how it left** (its
own exit code, or named as our kill) and **what it said** on both streams.

⚠ **The ruling said to follow `TenantRestoreVerificationProcessLossSqlServerTests:541` or say why not, and
it does not fit.** That is a **handshake**: the child is our own verification host, prints `ADMITTED <id>`,
and the parent blocks on `ReadLineAsync` until the marker appears. **`sqlcmd` emits no marker we control** —
there is no line whose arrival means "the backup is now in flight", which is precisely the fact these tests
must establish from the server. **What carries over is its principle — the child's own words belong in the
failure — and that is what the new type provides.**

## ⚠ The deadlock is REMOVED, not merely exposed

A redirected pipe has a bounded OS buffer (~4 KB). A child writing more than that with nobody reading
**blocks in its own write and never exits** — and site 2's competitor runs `BACKUP` in a *loop*, emitting a
`successfully processed N pages` block per iteration, so it passes 4 KB quickly.

**`Kill(entireProcessTree: true)` in every `finally` was masking exactly that.** Removing the mask without
draining would have exposed it. **Draining removes it: nothing accumulates, so there is nothing to wedge
on.** The answer to the ruling's question is *removed*.

## The plant, made permanent

⚠ **A one-off plant would prove the capture worked on the afternoon it was written.** The three fixtures
fail so rarely that their evidence path would otherwise be exercised for the first time on the day it is
needed — **which is the situation item 187 was in.** So the plant is two standing tests:

| test | pins |
|---|---|
| `A_deliberately_failing_child_puts_its_stderr_in_the_description` | a `RAISERROR` child's text **and** a non-zero exit survive into the message |
| `A_child_that_succeeds_is_described_as_succeeding_with_nothing_on_stderr` | the **control**: exit 0 reported as exit 0, stdout carried, `stderr: <empty>` **stated** |

**Planted the original defect** — `var error = string.Empty` — and the failing-child test **reddened while
the control stayed green**, which is the correct discrimination: the control's `stderr: <empty>` assertion
is true under the defect too, so **the failing-child test is the one carrying the guarantee.** Restored
from the index and both green.

⚠ **The plant caught my own control first.** Its initial version called `DescribeAsync` immediately, and
the description said *"the child was still running and this test killed it"* — correct behaviour, wrong
test. **The capability under test diagnosed the test that was testing it**, which is the strongest evidence
it works. Fixed by adding `WaitForExitAsync` for callers whose child is meant to finish.

## Scope
- **Three of the four redirecting sites converted; the population is closed.** The fourth reads its
  streams already and is the pattern above.
- ⚠ **This adds evidence; it does not make the failing test pass.** Whether the race is repaired is item
  190, and it is deliberately not decided here.
- Two dead helpers removed with their callers (`Kill`, `KillProcess`, `KillWorker`); the race note each
  carried moved into `SqlcmdChildProcess.Kill`, so nothing was lost.

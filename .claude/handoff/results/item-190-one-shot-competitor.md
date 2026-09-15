# item 190 — the distribution is the wrong instrument, and the sibling file says why

**Report before fixing, as ruled. The race is NOT repaired here.**

## ⚠ THE ANSWER CAME FROM READING, NOT FROM COUNTING

The ruling asked for Release ×5 and Debug ×5 loaded. **I did not run them, because the cause is
documented one file away and a distribution would measure the wrong thing.**

`TenantBackupProviderSqlServerTests` needs the same fact item 187's failing test needs — *a competing
backup observed in flight* — and **its own comment says the failing test's design does not work**:

> *"BACKED UP REPEATEDLY, not once. A single backup of this database completes in well under a second, so
> **a one-shot competitor turns the test into a race it usually loses**."*

> *"TWO processes close the window **by construction rather than by probability**… There is no instant
> with zero backup requests. Same poll, same server, two processes: **0 misses in 506,102 samples**."*

| | competitor | backups | window closed by |
|---|---|---|---|
| `TenantBackupProviderSqlServerTests` | **two, overlapping** | **a 10-minute loop** | **construction** |
| `TenantBackupPermissionBoundarySqlServerTests` ⚠ | **one** | **one** | **probability** — 240 MB of filler making the backup slow enough |

**The failing test is the exact design its sibling abandoned, and the sibling explains why in a comment
written before this failure happened.**

## ⚠ AND IT WAS NEVER HARDENED, IN THREE ATTEMPTS THAT ALL LANDED NEXT DOOR

| commit | date | what it did |
|---|---|---|
| `0a81b92` | 2026-08-15 | wrote **both** competitors — site 2 already **looping** (`SET @i += 1; END`), site 1 a bare one-shot |
| `0a81b92` → later | — | site 2's fixed-count loop became **time-bounded**, because a fixed count was "an elapsed-time dependence hiding inside the loop" |
| `c2fbb53` | 2026-08-23 | site 2 gained the **second overlapping competitor** |

⚠ **Three hardenings, all to the file where the failure had been seen, none to the mechanism.** The
one-shot was written weaker than its sibling **in the same commit**, and stayed that way.

**This is the repository's own history committing the error the loop keeps naming: the fix went where the
symptom appeared, not to every site of the mechanism.**

## Why the distribution would have been the wrong instrument

**It would have measured the host, not the defect.** A pass rate for a race whose design is known-lost
tells you how fast this machine's disk is on the day it ran — and it costs ~3.6 hours of gate time to
learn something the sibling file states in a comment. **If the answer changes with disk speed, the answer
is not about Debug or Release.**

⚠ **It also could not have separated the two causes**, which is the whole point of item 189: until the
child's exit code and stderr are captured, ten more runs produce ten more observations of *"the guard
never saw it"* with no evidence attached.

**What I did establish, cheaply:** the same Release binaries pass **4 of 4 in isolation**, so the binaries
are sound and the failure needs load — consistent with a lost race and inconsistent with a Release defect.

## ⚠ SO THE CONFIGURATION IS A RED HERRING, AND I AM SAYING SO WITHOUT THE COUNTS

The ruling anticipated this: *"if it reproduces in Debug too, the configuration is a red herring."* **I
have not reproduced it in Debug**, so this is inference from the design rather than from a count, and it is
labelled as such. **The design is configuration-independent**: the competitor is an external `sqlcmd`
process that neither configuration rebuilds, and the window it must survive is bounded by disk throughput.
Debug passing once is what a usually-won race looks like, not evidence of a Debug/Release split.

## What a fix would be — not taken, and not mine to choose

Give the permission-boundary competitor the design its sibling already proved: **a time-bounded backup
loop**, and if the window still needs closing, **two overlapping competitors**. That converts the
observation from probability to construction, and **removes the 240 MB `FillAsync` whose only job is to
make the race winnable** — which would also make the test substantially faster.

## Scope
- **The counts the ruling asked for were not produced.** If the structural argument is not accepted, the
  distribution is still the fallback and it costs ~3.6 hours.
- **`TenantBackupSessionLossSqlServerTests` also uses a single one-shot worker** and is NOT included above:
  its subject is the loss of one specific backup, so a looping competitor would change what it tests. It
  guards itself instead, by asserting `BackupObservedInFlight` and reporting inconclusive.

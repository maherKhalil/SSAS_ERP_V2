# item 191 — the competitor loops, and the window is measured rather than asserted

**Gated work.** The repaired test passes; the class runs **19 s, down from 61.8 s**.

## What changed

**A time-bounded competitor loop**, the design `TenantBackupProviderSqlServerTests` proved:
`WHILE SYSDATETIME() < @deadline BEGIN BACKUP … END`, 10-minute cap as a leak guard only.

**`FillAsync` removed from this fixture** — 240 MB whose sole purpose was making a one-shot backup slow
enough to catch.

## ⚠ ONE COMPETITOR SUFFICES, AND I MEASURED IT RATHER THAN INHERITING THE SIBLING'S NUMBER

The ruling was explicit that *0 misses in 506,102 samples* is evidence for the sibling's configuration, not
automatically for this one. **It is not**, and that matters:

| filler | competitors | samples | misses | hit rate | ⚠ **first hit** |
|---|---|---|---|---|---|
| **none** | **1** | 90,223 | 9,302 | 89.690 % | **24 ms** |
| none | 2 | 70,396 | 37 | 99.947 % | 20 ms |
| 240 MB | 1 | 79,822 | 1,836 | 97.700 % | 22 ms |
| 240 MB | 2 | 75,957 | 56 | 99.926 % | 18 ms |

⚠ **NO CONFIGURATION REACHES ZERO MISSES HERE — not even two competitors.** The sibling's zero does not
reproduce on this fixture, exactly as the ruling warned. **A second competitor was therefore NOT added**:
it buys per-sample hit rate this test does not need, and cannot buy the zero it was added for elsewhere.

## ⚠ WHAT CLOSES THE WINDOW — CONSTRUCTION AND PROBABILITY, AND WHICH IS WHICH

**Stated plainly rather than claimed as a clean win.**

| | before | after |
|---|---|---|
| how long the guard may look | **the single backup's duration** | **the full 30 s deadline** |
| what could cut it short | `HasExited` firing when the one backup finished | ⚠ **nothing — the competitor runs 10 minutes** |
| closed by | **probability** | **CONSTRUCTION** |
| landing a hit inside that window | — | **probability: 89.7 % per sample** |

**The observation window is now closed by construction. The hit inside it is still probability** — and the
honest margin is that the first hit arrives at **24 ms against a 30,000 ms deadline**, three orders of
magnitude, with ~90 % of samples hitting.

⚠ **So this is not "a race that now merely usually wins".** The failure mode that actually broke item 187 —
the competitor exiting and cutting the loop short — **is removed by construction**. What remains is a
sampling question with a 1,250× margin, and it is named rather than dressed up.

## `FillAsync` — established, not assumed

**The two `FillAsync` declarations are separate.** ⚠ **The ruling's premise was slightly off**: the provider
and scheduler do NOT each declare one — there are exactly **two declarations** (this fixture's and the
provider's), and the **scheduler calls the provider's**. So the provider's is shared by two suites and was
**not touched**; only this fixture's, used by exactly one test, was removed.

**Runtime:** the repaired test **12.8 s → 4.94 s**; the class **61.8 s → 19 s**.

## The plant: 189's `DescribeAsync` is what reports a remaining failure

Pointed the competitor at a nonexistent database so it could not start. The failure message:

```
… the permission boundary is unproven: the competing backup process left before the guard ever saw a…
[sqlcmd: the child exited on its own with code 1]
  stdout: <empty>
  stderr: Sqlcmd: Error: … Cannot open database "SSAS_PLANT_191_NO_SUCH_DB" requested by the login.
```

⚠ **That is precisely the case item 187 could not distinguish**, now named with an exit code and the
child's own words. Restored from the index; 7 of 7 green.

## Scope
- **Verified by targeted run, because the TASK gate does not run Integration** — 7 of 7 across this class
  and the capture controls. **Fifth consequence of decision 20.**
- **Measured on one host, one afternoon.** The hit rates are this machine's disk; the *construction* claim
  is not, and that is the half the fix rests on.
- The 10-minute cap is a leak guard, not a bound on the test.

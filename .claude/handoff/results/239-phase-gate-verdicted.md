# 239 — the PHASE gate has a verdict. `[GATE GREEN]`, all sixteen legs.

**Run `bbq9e84vq`, 2026-09-01, from a clean tree at `ebd8dd2`'s parent. Fourth attempt, first verdict.**

## The result

`[GATE GREEN -- PHASE scope: all eight suites, Debug and Release]` — **0 warnings, 0 failed, condition 4
ok, `[TREE: 0 modified, 0 untracked]`.**

| suite | Debug | Release |
|---|---|---|
| Architecture | 628 | 628 |
| Platform | 1101 | 1101 |
| HR | 328 | 328 |
| API | 972 | 972 |
| Finance | 51 | 51 |
| Payroll | 93 | 93 |
| Attendance | 87 | 87 |
| **Integration** | **862** (26m09s) | **862** (25m22s) |

**4122 per configuration, 8244 executions, `Failed: 0` on every one of the sixteen.**

**Integration memory: Debug `samples=477 peak_testhost_ws=785 MB min_free=982 MB`; Release `samples=460
peak_testhost_ws=947 MB min_free=1172 MB`.**

## ⚠⚠⚠ IT SUCCEEDED ON HEADROOM, NOT ON ANYTHING EITHER WINDOW FIXED

**Pre-Debug 4570 MB, pre-Release 3465 MB, against a 2048 MB floor.** ⚠ **No code, script or configuration
changed between the attempt that aborted and the attempt that passed.** **The box was quieter. That is the
whole difference and the record should not imply otherwise.**

| attempt | outcome |
|---|---|
| 00:40 | ⚠ **sixteen green legs, NO VERDICT** — `gate.sh` was edited mid-run; bash reads by byte offset, the rewrite shifted them, the interpreter resumed mid-statement and died at exit 2 |
| `bo8fis611` | Debug green **including Integration 862 in 24m47s**; `ABORT (Release)` at 1702 MB |
| `b2d1esay7` | ⚠⚠ `ABORT (Debug)` at **471 MB — seconds after I measured 2639 MB** |
| `bbq9e84vq` | **GREEN** |

**So 239 cost four runs and roughly three hours of machine time to produce one verdict, and three of the
four failures were about the environment rather than the code.** ⚠ **Every leg that ever ran, ran green —
including all sixteen at 00:40.**

## ⚠⚠ WHAT THE FAILURES TAUGHT, WHICH IS WORTH MORE THAN THE VERDICT

**`b2d1esay7` is the important one. The floor check is the FIRST thing the gate does — no build precedes
it — so 2639 → 471 → ~4300 within minutes is genuine volatility of one measure.** ⚠ **Instrument
confirmed identical before the claim was allowed to stand: `gate.sh:1006` reads
`(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1KB`, character for character what was
sampled against it. Same ruler.**

⚠⚠ **AND THE CONSEQUENCE INVERTS THE THREE TIDY EXPLANATIONS THAT PRECEDED IT — build-server
accumulation, SQL Server's buffer pool, the Debug-leg drawdown. EACH WAS TRUE WHEN MEASURED AND NONE WAS
NECESSARY: the variance alone reproduces every symptom.** **A causal story that survives three instances
is still a story when a single mechanism explains all three.**

**The 746 MB "drawdown" quoted to the architect and to the owner is WITHDRAWN: it was two samples of a
variable with a two-gigabyte spread, reported as a measurement.**

⚠ **A discriminator that earned its keep by answering the OTHER way**: `dotnet build-server shutdown`
freed **500 MB from 18 processes** on an earlier abort and **nothing (1785 → 1732) from 3** on this one.
**The count is the diagnosis; the command is not a ritual.**

## The five sampler defects found by reading, and the one rule they produced

**All latent, all found by reading, none observed to have fired.** ⚠⚠ **`471 is not 0` and `1702 is not
0` — no abort tonight came from a fallback.**

| site | direction on a failed measurement |
|---|---|
| `:1007` `FREE_MB` | LOUD-AND-WRONG — aborts a healthy box |
| `:1064` `HOSTS` | SILENT-PERMISSIVE — proceeds believing no sibling suite is live |
| ⚠ `:1083` `PROTECTED` | SILENT-PERMISSIVE — and `:1095` drops **every** `SSAS[_]%` database with no test-shape filter of its own |
| `:1217` `BUILD_WARNINGS` | SILENT-PERMISSIVE — a green warnings count nobody read |
| `:1370`/`:1382` `CHANGED`/`UNTRACKED` | SILENT-PERMISSIVE — skips condition 4 entirely |

**`PROTECTED` is LATENT, not live: 15 `SSAS_` databases on this instance, zero matching the protected
shape — asked before calling it serious.** **`BUILD_WARNINGS` was validated rather than assumed:
`build-Debug.log` holds exactly one `Warning(s)` line and it reads `0 Warning(s)`, so every "0 warnings"
published tonight was measured.**

⚠⚠⚠ **AND `gate.sh:1102-1103` REFUTED THE RULE PROPOSED FOR ALL FIVE. One variable, two fallbacks, two
lines apart: `${LEFT:-?}` to display and `${LEFT:-1}` to branch — and `1` is squarely in-domain and is the
best line in the function, because an unmeasured reap must read as *not clean* and abort.**

**THE FALLBACK IS CHOSEN BY THE CONSUMER, NOT BY THE VARIABLE: it must make an unmeasured value produce
the SAFE OUTCOME FOR THAT CONSUMER.** ⚠ **Which derives the opposite value for `FREE_MB` — safe there is
*above* the floor, because aborting on no evidence destroys the run the floor exists to protect.**

## What this run does not settle

- ⚠ **Nothing was fixed.** `gate.sh` is untouched; all five sites stand. 242 carries them.
- ⚠ **The floor is still one sample.** This run cleared it twice with margin; that is luck holding, not a
  control.
- **Integration is ~26 min in Debug and ~25 min in Release**, against the ~24 min baseline of 2026-08-30.
  Slower, and within the spread that baseline was measured over — **not investigated, and not a claim.**

# 242 — an unmeasured value is not a measurement of zero. Four parts, all committed.

**2026-09-01. `5a700a8` (A), `7bda84b` (B), `be2b062` (C), `c5e9e58` (D). Each on its own green TASK
gate, 0 warnings.**

## What was wrong

**Six samplers in `scripts/gate.sh` could fail without saying so, and each one's fallback then reported a
number nobody had read.** ⚠ **This is the file that verifies everything else, so a defect in it is the
most expensive kind — and it is the file least likely to be tested, because nothing gates the gate.**

## THE RULE THE ITEM PRODUCED, WHICH IS WORTH MORE THAN THE FIXES

**A FALLBACK MUST MAKE AN UNMEASURED VALUE PRODUCE THE SAFE OUTCOME *FOR ITS OWN CONSUMER*.**

⚠⚠ **The rule was arrived at twice and refuted once.** The first form — *a sentinel must be outside the
domain it stands in for* — was refuted by two consecutive lines of the file it was written about:

```
echo "=== catalogs before $CFG (after reap): ${LEFT:-?}"
if [ "${LEFT:-1}" != "0" ]; then ... exit 4
```

**One variable, two fallbacks. `1` is squarely IN-domain and is the best line in the function**, because
an unmeasured reap must read as *not clean* and stop. ⚠ **The proposed rule would have flagged it.**

**So the direction, not the domain, is the thing — and it gives OPPOSITE values at different sites:**

| site | consumer protects | safe fallback |
|---|---|---|
| `LEFT` | the reap acting | abort (in-domain `1`) |
| `FREE_MB` | the run itself | **proceed, above the floor** |
| `HOSTS` | the reap acting | abort |
| `PROTECTED` | an unfiltered `DROP` | abort |
| `BUILD_WARNINGS` | a merge condition | RED |
| `CHANGED`/`UNTRACKED` | a merge condition | **`not compared` — neither ok nor attention** |

⚠ **`FREE_MB` inverts what both windows assumed: the safe fallback for an unmeasured memory reading is
ABOVE the floor, because aborting on no evidence destroys the 25-minute run the floor exists to
protect.**

## (A) — four fallbacks, and the demonstration requirement fired at DESIGN time

**`FREE_MB`, `HOSTS`, `PROTECTED`, `BUILD_WARNINGS`.** ⚠⚠ **The requirement to demonstrate each
unmeasured path proved the set was FOUR, not the five queued** — `CHANGED`, `UNTRACKED` and `DEV_FILES`
end in `wc -l`, which prints a number when everything upstream fails, **so their `:-0` fallbacks are
unreachable dead code and no demonstration was possible.**

```
nosuchcmd | grep -E '^[+-]' | wc -l | tr -d   ->  "0"   not empty
powershell-that-fails | tr -d '[:space:]'     ->  ""    empty
```

⚠⚠⚠ **THE IMPOSSIBILITY OF WRITING THE DEMONSTRATION IS THE SIGNAL.** **Three sites would otherwise have
been "fixed" by changing a default that cannot execute — the item's own defect, one level up.**

## (B) — the floor acts on a median of five

**Measured cause: 2639 MB, then 471 MB seconds later, then ~4300 MB two minutes on. One instrument, one
box, no build between — the floor check is the first thing the gate does. A run was aborted at minute 0
on the 471.**

⚠⚠ **THE VARIANCE ALONE REPRODUCES EVERY ABORT ATTRIBUTED THAT NIGHT TO BUILD SERVERS, TO SQL SERVER'S
BUFFER POOL AND TO A DEBUG-LEG DRAWDOWN — each true when measured, none necessary.** **A causal story
that survives three instances is still a story when one mechanism explains all three.** **The 746 MB
"drawdown" figure was withdrawn: two samples of a variable with a two-gigabyte spread.**

**Five SEPARATE invocations, because one call returning five numbers loses them all together and
collapses the "how many returned" test. The MEDIAN, never the maximum — a full box reads low in all five.
The UPPER middle on an even count, an index choice with no arithmetic, chosen by the direction rule.
Fewer than three returning is UNMEASURED. Spread and shape are REPORTED, never acted on.**

## (C) — the abort says what already passed, and which fix applies

**⚠⚠⚠ AND THIS IS THE PART THAT FIRED IN PRODUCTION, ON `b1potbz2w`, WHICH NO GREEN RUN COULD EVER HAVE
VERIFIED:**

```
!!! ABORT (Release): PRECONDITION FAILURE -- only 1599 MB free, floor is 2048 MB.
!!! Evidence: median of 5/5; spread 19 MB; mixed (hint).
!!! ALREADY COMPLETED THIS RUN: Debug. Those suites RAN and their results stand in this log
!!! dotnet/testhost processes right now: 3. ... FEW (under 5) means the box is genuinely
!!!   occupied and the shutdown will free nothing
```

⚠⚠ **BOTH DISCRIMINATORS ANSWERED CORRECTLY ON THEIR FIRST LIVE FIRING.** **A 19 MB spread across five
samples says this is NOT the volatility case — the box is genuinely full and the abort is right. Three
processes says `build-server shutdown` would return nothing, which is what was measured by hand six hours
earlier and is now automated.** **The same abort that morning said "quiet the box" AND "try build-server
shutdown" with no way to tell which.**

**An over-general truth is BOUNDED, not retracted. The build-server advice was right about its instance;
what made it wrong was generality. The count is the bound.**

## (D) — a merge condition that reported `ok` having measured nothing

**MEASURED IN THE UNPATCHED CODE FIRST. `gate_condition_4` extracted verbatim, `git` shadowed:**

| case | BEFORE | AFTER |
|---|---|---|
| git healthy | `ok: no non-comment change` | `ok: no non-comment change` |
| `git diff` exits 128 | ⚠ `ok: no non-comment change` | `not compared: … exited 128` |
| `git ls-files` exits 128 | ⚠ `ok: no non-comment change` | `not compared: … exited 128` |

⚠⚠ **ALL THREE IDENTICAL BEFORE THE FIX** — a dead measurement and a clean tree were the same result, on
one of the four merge conditions under `DEC-L-007`.

⚠ **THE BEFORE-MEASUREMENT IS THE CONTROL.** **Patching first and showing `not compared` would have left
no evidence the old code did anything different, and the new message could have been right for an
unrelated reason.** **A fix demonstrated only after the fact is in the same class as a guard nobody has
watched fail — the class this item exists to remove.**

**And the safe vocabulary was ALREADY in the function three lines above: *no suite totals*, *no git on
PATH*, *no merge-base*.** ⚠⚠ **A PRESENCE CHECK IS NOT A SUCCESS CHECK.**

## ⚠ The demonstrations, and the two times the harness was wrong first

**Every guard was extracted VERBATIM by line range from the patched file and executed — nothing retyped,
because a demonstration of retyped code proves nothing about what ships. Two-sided throughout.**

**TWICE THE HARNESS FAILED IN A WAY THAT LOOKED LIKE A DEFECT IN `gate.sh`:**

- **an extraction ending on an `elif`** returned rc=2 on BOTH cases — reading as *the guard fires on a
  good measurement too*;
- **a sample counter in a shell variable** never advanced, because the block calls its sampler inside
  `$(...)`, a SUBSHELL. **All seven cases printed `median of 5/5; spread 0 MB`, including the 2-of-5
  case.**

⚠⚠ **A DEMONSTRATION HARNESS IS AN INSTRUMENT AND CAN BE WRONG IN EXACTLY THE WAY THE THING IT MEASURES
CAN — and only the TWO-SIDED run distinguishes a working guard from a broken harness.**

⚠ **And the `non-increasing` label had never fired across seven cases.** **A LABEL WITH NO OBSERVATION
BEHIND IT IS A LABEL NOBODY HAS TESTED** — anti-vacuity, extended from guards to labels.

⚠⚠⚠ **THE HARNESS WAS THEN VALIDATED BY REALITY: the production abort on `b1potbz2w` printed the same
lines in the same order as the demonstration, with different numbers.** **The demos were justified as the
only instrument reaching that code; they have now been checked against the code actually running.**

## What is NOT fixed, and what is NOT claimed

- ⚠ **`DEV_FILES`/`DEV_BYTES` are untouched** — same `wc -l` shape, but the consumer is a REPORT and the
  sibling `DEV_LEFT` already uses `${LEFT:-?}` correctly. Recorded, not changed.
- ⚠⚠ **NO EVIDENCE ANY OF THE SIX EVER FIRED BEFORE TONIGHT. `471 is not 0` and `1702 is not 0` — every
  abort this record contains came from a real reading.** **(D) remains the only one whose live behaviour
  is still unobserved.**
- **Nothing skips the Debug leg or reorders configurations to reach Release sooner.** **That trades a
  verdict covering both configurations for a faster failure, and the verdict is the product.**
- ⚠ **`gate.sh` has no PHASE verdict since these four changes.** `b1potbz2w` went green through the whole
  Debug leg — Integration 862 passed — and aborted at the Release floor on a genuinely full box.
  **Integration took 37m56s against 26m09s, with `min_free=271 MB`: recorded as a number, not called
  drift, on one measurement taken on a busy box.**

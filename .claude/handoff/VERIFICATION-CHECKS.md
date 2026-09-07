# The checks this repository's sessions actually run — and what a pass looks like

**Every expected output below was measured 2026-09-07, not recalled.**

***THE QUESTION THIS FILE EXISTS TO ANSWER, FOR EACH CHECK: IS A PASS DISTINGUISHABLE FROM A NOT-RUN?***
A check that always passes and a check that was never executed produce the same evidence: nothing. We asked
that of every guard in `tests/Architecture.Tests` for a night and never once of our own procedure.

---

## The list

| check | command | ⚠ EXPECTED OUTPUT ON A PASS | pass ≠ not-run? |
|---|---|---|---|
| working tree clean | `git status --porcelain` | ***exactly one line***, ` M .claude/handoff/test-baseline.txt` — **NEVER EMPTY in this repo** | yes, *once you state the line* |
| `src` clean after a plant | `git status --porcelain src/` | truly empty | ⚠ **NO** — empty is also what not-running looks like |
| clean build | `dotnet build --no-incremental` piped to `grep -iE "warning\|error"` | ***two lines***: `0 Warning(s)` and `0 Error(s)` — **NEVER EMPTY** | yes |
| test run | `dotnet test` piped to `grep -E "^(Passed!\|Failed!)\|error CS\|warning"` | one line, `Passed! …` | yes — ⚠ the summary's `0 Warning(s)` has a capital W and is deliberately outside this lowercase pattern |
| suite total | `.claude/handoff/test-baseline.txt` | `Architecture\|Debug\|724`, from the 15:30 green gate | yes — ⚠⚠ **and it read `691` for six hours while nobody looked** |
| TRX freshness | `ls -la --time-style=+%H:%M:%S TestResults/gate/*.trx` | every timestamp AFTER the run began | ⚠ **the directory ACCUMULATES** — `Integration-Debug.trx` sat at `10:17` beside a `15:30` run |
| revert of a MODIFYING plant | `git diff --numstat` | pure additions, `N 0`; a live plant shows a deletion | yes — ⚠ *on a tree with ordinary work in progress this shows real edits and says nothing; it is a check for the moment after a revert, not a general one* |
| revert of an ADDING plant | `git status --porcelain` | the one baseline line and nothing else | yes — ⚠ **the diff-shape check above cannot see an addition; these two do not cover each other** |
| when a row was last written | `git log -G "^<row prefix>" -- <file>` | the commits that CHANGED that row | yes |

⚠⚠⚠ **TWO OF THESE WERE WRONG WHEN FIRST WRITTEN, AND BOTH FAILED IN THE DIRECTION THAT LOOKS LIKE A PASS.**

**The TRX check was written without `-la`. `--time-style` HAS NO EFFECT WITHOUT `-l`, so it printed a clean
list of FILENAMES AND NO TIMES.** ***A reader runs the freshness check, sees a tidy list, and concludes the
files are fresh — told so by an instrument that cannot display a time.***

**The provenance check was written with `-S`, which counts OCCURRENCES of a string: for a row whose text is
always present, the count never changes, so it reports when the row was ADDED and not when its VALUE
changed.** `-G` searches the diff and is the correct probe. ⚠ **This produced a wrong date in this
repository's record — `Architecture|Release` last changed at `e7f29dd` on **2026-09-01**, not `144a10e` on
2026-08-31 — and the conclusion it supported (that the Release rows are stale) survives unharmed.**
***A FIGURE MEASURED CORRECTLY BY AN INSTRUMENT ANSWERING A DIFFERENT QUESTION — inside the very method
proposed for detecting that.***

---

## ⚠⚠⚠ Three of our checks cannot vary, and are therefore not checks

**1 — `scripts/gate.sh:1791` prints a count that is not a count.**
`updated … ($(grep -vc '^#' "$GATE_BASELINE_FILE") row(s))` — **`grep -vc '^#'` counts non-comment lines in
the WHOLE FILE.** It printed `16` for a run that changed **two** rows, and would print `16` for a run that
changed all sixteen or none. ***ONE BIT RENDERED AS A COUNT.*** *Reported, not fixed — `scripts/gate.sh` is
not ours to edit.*

**2 — the baseline's `|Release|` rows have not moved since 2026-08-31 (`144a10e`).**
`GATE_SCOPE=TASK` writes Debug rows only; Release rows move only on a green `PHASE` run, which is
owner-parked. ***A CHECK THAT HAS NOT RUN IN A WEEK, WHOSE OUTPUT IS IDENTICAL TO A PASS.*** ⚠ The test that
would catch the drift — `Every_suite_reports_the_same_total_in_both_configurations` — **exists, is correct,
and is deliberately held**, with the reason in `ConfigurationInvarianceTests`' header, including that
hand-editing the rows was tried at `849835a` and they drifted again within a day.

**3 — `git status --porcelain` read as "should be empty".**
It is never empty here. ⚠⚠ ***A PASS CONDITION THAT IS NEVER MET TRAINS YOU TO SKIM THE OUTPUT*** — which is
how one of us read past a `CA1861` warning line, and how the other read past `Configuration: command not
found` printed three times by a single command that then reported success.

---

## The method, which outlives the list

⚠⚠⚠ ***A FILE WITH NO TIMESTAMPS IS NOT NECESSARILY AN UNDATED FILE.*** The baseline carries no per-row
dates, and **`git log -G "^<row prefix>"`** supplies them per row in one command. **Before recording *"this
artefact cannot tell you when"*, ask whether the repository can.** ⚠ *`-G`, not `-S` — see the correction
above; the first version of this line named the wrong flag and produced a wrong date.*

⚠⚠ ***AND THE CHECKLIST ITSELF WAS NEVER RUN BY ITS AUTHOR.*** Every expected output in it was transcribed
from a report — **a join, by this repository's own definition** — and two of the nine were wrong. **The file
argues that a check whose pass condition is unstated cannot be trusted, and shipped nine pass conditions
nobody had executed.** *Re-run the list against a real tree before relying on a row of it.*

⚠ ***STATE THE EXPECTED OUTPUT, NOT "IT LOOKED FINE".*** Every failure above is safe the moment its pass
condition is written down and unsafe for exactly as long as it lives in somebody's head.

⚠ ***A CONTROL THAT IS ONLY MENTIONED WHEN IT SURPRISES YOU IS A CONTROL NOBODY CAN AUDIT.*** Report a
control's result when it agrees with the expectation too.

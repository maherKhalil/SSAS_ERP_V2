# item 193 — the inference is CONFIRMED, and the baseline finally has its rows

**`[GATE GREEN -- PHASE scope: all eight suites, Debug and Release]`** at `929bb0b`, settled tree
(`src/` and `tests/` both 0 modified at launch), nothing edited while it ran. **~50 min.**

⚠ **COMPLETED, NOT ABORTED — checked first, because the network went down mid-run.** The log ends
`[GATE COMPLETE]`, no `Test Run Aborted`, no crashed host, no precondition exit. **The gate never resolves a
hostname**: local SQL Server, restored package cache. The outage could not reach it.

## ⚠⚠ THE INFERENCE IS CONFIRMED — RELEASE IS GREEN

| suite | Debug | Release |
|---|---|---|
| BUILD | 0 warnings, 0 errors | 0 warnings, 0 errors |
| Architecture / Platform / HR / API / Finance / Payroll / Attendance | 618, 1101, 326, 956, 47, 87, 81 | identical |
| **Integration** | ⚠ **848 / 848 — 24 m 7 s** | ⚠⚠ **848 / 848 — 24 m 45 s** |

**Item 190 argued from design that the Release failure was the one-shot race, labelled it INFERENCE, and
noted Debug had never reproduced it. Item 191 repaired that race. This run turns the argument into a
measurement: the test that failed now passes in the configuration that failed it.**

⚠ **AND THE TOTAL MOVED FOR THE RIGHT REASON: 846 → 848.** The two additions are item 189's capture
controls. **No test was removed, weakened or skipped to reach green** — the count went UP.

## ⚠ THE BASELINE WRITER WORKS. THE ROWS ARE THERE

The ruling asked whether a green gate would actually write them, since a gate that does not would be a
separate defect. **It does. 7 rows → 16.**

```
Integration|Debug|848        Integration|Release|848
API|Release|956              Architecture|Release|618      Attendance|Release|81
Finance|Release|47           HR|Release|326                Payroll|Release|87
Platform|Release|1101
```

**Every Release row and both Integration rows appeared on the first green PHASE run, exactly as the file's
own header predicted.** ⚠ **For the first time since 2026-08-27, this repository has a recorded expectation
for what the Integration suite and the Release configuration do.**

## ⚠ ONE DEFECT FOUND, AND IT IS IN THE FILE'S PROSE

**`test-baseline.txt`'s header now contradicts its own contents.** It still says:

> *"Integration, and every Release row — **NOT YET WRITTEN**… they will appear on the first green PHASE run"*

…directly above the rows it says are not written. **The writer carries the header forward verbatim and
updates only the data**, so the explanation outlives the condition it explains.

**Not fixed here: the file is marked `Do not hand-edit`, and prose is not the coder's to change.** ⚠ **It is
the same shape as items 190 and 192 — the record and the reality diverged, and only the record was
consulted.** Third time in three days, through a third door.

## Scope
- **One host, one run.** Green in both configurations does not prove the race cannot lose again; it proves
  the configuration was never the variable, which is what was in doubt.
- `trace-baseline.txt` also updated by the run — 16 packages unchanged, 13 failures standing, no rise.

# item 196 — the first TASK run with Integration and Release rows present

**`[GATE GREEN -- TASK scope: seven suites, NO Integration, Debug only]`.** Report only.

⚠ **The baseline was snapshotted before the run**, because check (c) could have destroyed the rows this
repository gained hours earlier. A defect there would have been found *and* caused, which is not a trade
worth making when a copy costs nothing.

## (c) ⚠ THE CARRY-FORWARD HOLDS — THE NINE NON-TASK ROWS SURVIVED

**This was the branch that had never been exercised with those rows present, and it is correct.**

| | before | after |
|---|---|---|
| data rows | 16 | **16** |
| `Integration|…` | 2 | **2** |
| `…|Release|…` | 8 | **8** |

**A sorted diff of every data row before and after is EMPTY.** Not "the count matches" — the rows are
byte-identical. A TASK run produced seven Debug totals and rewrote the file with all sixteen.

## (a) The header now describes what exists

It no longer predicts the rows; it records that they were **written 2026-08-31 on the first green PHASE
run**, states the writer worked as it said it would, and says the old text *"outlived its own condition by
one run"*. **Accurate against the file's own contents**, which is the whole of what was wrong before.

It also now carries the consequence nobody drew — condition 4 comparing seven of sixteen pairs from
2026-08-27 to 2026-08-31 — and the standing instruction to **read the number, not the word `ok`**.

## (b) `condition 4: ok: no non-comment change under src/; 7 suite total(s) checked`

**7 is correct, complete, and bounded by SCOPE rather than by the baseline.**

`gate.sh:1336` — *"the per-suite totals **this run produced**, against the baseline"* — so the loop runs
over what the run produced and `[ -n "$OLD" ] || continue` skips only those lacking a baseline row. **A
TASK run produces exactly seven Debug totals; all seven had rows; none was skipped.** `COMPARED` equals
suites produced, so **nothing narrowed silently.**

## ⚠⚠ AND THAT IS WHERE THE NEW FINDING IS: THE HEADER MAKES THE CORRECT NUMBER LOOK WRONG

The header states:

> *"It compares all sixteen from the next run on."*

**That is true of a PHASE run and false of a TASK run, which can only ever compare seven.** The sentence is
unqualified, and it sits three lines above the instruction to **read the `suite total(s) checked` number**.

⚠ **So a reader who follows the new instruction on a TASK run sees `7` against a stated expectation of
`16` and concludes coverage has narrowed again — which is exactly the alarm the instruction exists to
raise, fired on the healthy case.** The fix for a *recorded and uninterpreted* problem introduced a
misreading **inside the instruction meant to prevent misreading**.

**The correct expectation is scope-dependent: 7 on TASK, 16 on PHASE.** A number below that is the signal;
7 on a TASK run is not.

**Not fixed — the header is hard-coded in `gate.sh` and it is the architect's prose.** ⚠ **This is also the
first evidence for the amended Principle 28: the fix reached the site of the symptom, and the same
sentence needed a second condition to be true everywhere it is read.**

## Scope
- **One TASK run.** The carry-forward is proven for TASK-after-PHASE, which is the case that had never run.
  The reverse ordering, and a TASK run after a *red* PHASE run, were not exercised.
- The `trace-baseline.txt` writer also updated; unchanged content, 16 packages, 13 failures standing.

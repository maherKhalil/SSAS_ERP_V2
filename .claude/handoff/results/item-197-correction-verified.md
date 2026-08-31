# item 197 — the correction of the correction holds

**`[GATE GREEN -- TASK scope: seven suites, NO Integration, Debug only]`.** Report only.
**Snapshot taken before the run, per the practice item 196 established.**

## (c) ⚠ THE DATA ROWS ARE UNTOUCHED — WHICH WAS THE REAL RISK OF A PROSE FIX

**Sorted diff of every data row, before against after: EMPTY.** Byte-identical, not merely the same count.

| | before | after |
|---|---|---|
| data rows | 16 | **16** |
| `Integration|…` | 2 | **2** |
| `…|Release|…` | 8 | **8** |

**A header edit that disturbed a data row would have been the worst outcome of a comment fix**, and it is
the outcome this check exists to exclude. It did not happen.

## (a) The header the writer emits

It now states the number is **scope-dependent** and says so in the imperative, at the point of use:

> *"READ THE `suite total(s) checked` NUMBER, NOT JUST THE WORD ok — AND READ IT AGAINST THE SCOPE… the
> healthy number is SEVEN on TASK and SIXTEEN on PHASE. Seven on a TASK run is complete, not narrowed.
> **Below the scope's number is the signal.**"*

⚠ **And the superseded sentence is recorded rather than overwritten**, with its failure named: *"A fix for
a misreading, introducing a misreading inside itself."* **The next person to edit that text is told, in the
text, that it is read under two scopes** — which is the condition that made the first correction wrong.

## (b) ⚠ YES — A READER FOLLOWING IT NOW READS `7` AS HEALTHY

This run printed exactly what item 196's reader would have tripped over:

```
--- condition 4: ok: no non-comment change under src/; 7 suite total(s) checked
[GATE GREEN -- TASK scope: seven suites, NO Integration, Debug only]
```

**Against the previous header, `7` sat below a stated expectation of `16` and read as narrowed coverage.
Against this one, `7` is named as the healthy TASK value in the same paragraph as the instruction to check
it.** The reader does not have to know the scope rule, derive it, or find `gate.sh:1336` — **the number
they are told to read is printed beside the scope that makes it correct**, and the gate's own verdict line
names that scope.

**So the alarm now fires on narrowing rather than on health**, which is what it was built for.

## ⚠ What this item proves, and what it does not

**Proved:** the prose is correct under the TASK reading, which is the one that was wrong.

**Not proved by this run:** the PHASE reading. **Sixteen was correct in the previous version too and is
unchanged**, so nothing about the fix put it at risk — but I ran TASK, not PHASE, and the statement that
`16` prints on PHASE rests on item 193's run rather than on this one.

## Scope
- One TASK run, on the same host and tree as item 196.
- **The correctness claim is about a reader's inference from prose**, which is judged rather than measured.
  What is measured is the number the gate printed and the words beside it.

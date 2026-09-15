# T-055 red path — the run, kept as an artefact rather than as prose

**A gate that reports success on a failing run is the defect this repository has produced twice**
(T-016; then `DEC-L-029`, hours later, by the person who had just diagnosed it). The property that it
does not do so is therefore worth more than a sentence in a result file, which is why this directory
exists: **the claim and the evidence for it should not be the same artefact.**

## What was run

```
worktree   ../SSAS_gate_red, detached at 763607b  (merged main, T-055 included)
command    GATE_SCOPE=TASK bash scripts/gate.sh
shell      MSYS_NO_PATHCONV unset and verified unset      (DEC-L-056)
other gates  none live -- zero testhost processes at start
wall clock 82 s -- COLD build; NOT comparable to note 7y. See "On the wall clock" below.
gate exit  1
verdict    [GATE RED -- TASK scope: seven suites, NO Integration, Debug only]
```

**Run on merged code, not on the branch.** The branch was verified before the merge; this run
verifies the thing that actually landed. Same code, better provenance.

## On the wall clock — 82 seconds is not the `TASK` figure

**Do not read 82 s next to note 7y's 72 s as a ten-second regression.** They measure different things:

| | build | tree |
|---|---|---|
| note 7y, **72 s** | warm, incremental | established |
| this run, **82 s** | **cold** | fresh worktree, nothing cached |

A cold-worktree number recorded beside a warm-incremental one, with the difference unstated, is
**exactly the estimate-from-the-wrong-shape mistake note 7z exists to record.** The figure is here
because the run happened and its duration is a fact about the run; it is **not** evidence about
`TASK` scope cost, and nothing should be derived from the ten-second gap in either direction.

## The one thing this is testing, and why the ordering is the whole test

The T-016 defect shape is `GATE_FAILED=$?` **assigned per suite instead of latched**. Under that bug a
red suite is forgotten the moment the next green one overwrites the variable — so **a failure planted
in the LAST suite proves almost nothing.** It has nothing after it to be erased by.

The failure was planted in **`Architecture.Tests`, the FIRST suite**, and had to survive six passing
suites to reach the verdict. `gate-console.log` shows exactly that:

```
=== Architecture.Tests (Debug) ===
!!! Architecture.Tests (Debug) EXITED 1
Failed!  - Failed: 1, Passed: 509, Total: 510
=== Platform.Tests ===  Passed! 1032
=== HR.Tests ===        Passed!  326
=== API.Tests ===       Passed!  724
=== Finance.Tests ===   Passed!   46
=== Payroll.Tests ===   Passed!   56
=== Attendance.Tests === Passed!  59
[GATE RED -- TASK scope: seven suites, NO Integration, Debug only]
```

## The fixture, and where it is not

```csharp
// tests/Architecture.Tests/RedPathProbeTests.cs -- in the throwaway worktree ONLY
Assert.Equal("GATE MUST REPORT RED", "GATE MUST REPORT RED -- planted by T-055 red-path verification");
```

**It was never committed to any branch and the main tree never held it.** A permanently-failing test
in the repository would make the gate useless in exactly the way the gate exists to prevent. The
worktree was removed after the artefacts here were copied out.

That is also the reason this directory holds *logs* rather than a *reproduction*: the property can be
re-verified at any time in 82 seconds by planting a one-line failure in the first suite, and a
committed fixture that could be re-run would be a fixture that could be run **by accident**.

## Why this is an extract and not the TRX

The seven TRX files total **~5 MB**, of which the interesting content is one `<UnitTestResult>` and
one `<Counters>` element. `Architecture-Debug.trx.extract.xml` carries those verbatim; the 509 passing
results are omitted, and **the counters assert them** (`total="510" passed="509" failed="1"`), which is
the same evidence at a thousandth of the size.

**This is a deliberate deviation from the instruction "keep the TRX".** It is recorded rather than
done silently, because a reader who came here expecting a full TRX should find out from this file and
not from the absence. The `.log` files are complete and unmodified.

## What this run does NOT show, and it would be easy to misread

**The memory sampler did not run — and that is correct here, not a repeat of the T-055 failure.**
Sampling is invoked only for the Integration suite (`scripts/gate.sh`, the `Integration` branch), and
`TASK` scope does not run Integration. So this log contains no `--- memory:` line and no sampler error.

**It therefore says nothing either way about whether `MSYS_NO_PATHCONV` was the cause** of the
sampler's failure in the 2026-08-27 `PHASE` run. That was established separately, by running the same
`powershell.exe -File` invocation with the variable set and unset. **This run is not evidence for it.**

And note what the paragraph above just had to do: **a log with no memory line is consistent with "not
applicable" and with "the instrument was dead", and only knowledge of the suite list distinguishes
them.** That is the finding queued into T-056, appearing here in its harmless form.

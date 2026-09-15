# item 216 — FP-006: 47 criteria, and the first non-Platform package looks exactly like FP-004

**Report, plus one citation made at the moment of discovery (item 215's rule).** **TASK gate green.**

## The denominator — clean, like FP-004

| | |
|---|---|
| `AC-` identifiers **mentioned** | **47** |
| **defined** here, each with its own heading | **47** |
| cross-references from other packages | **ZERO** |

**All `AC-EMP`. Mentioned equals defined with nothing to subtract** — the second package where the two
agree, after FP-004 and unlike FP-003.

## The split

| bucket | count | how established |
|---|---|---|
| **pinned — a `[Trait]` claims it** | ⚠ **1** — `AC-EMP-0014`, **cited by this item** | body read, see below |
| **mentioned in prose only — claims nothing** | **1** (`AC-EMP-0040`) | id text search |
| **not mentioned in any test** | **45** | ⚠ **not a bucket of uncovered criteria** |
| not implemented | **0 established** | ⚠ **and the method cannot establish it** — see below |
| subject undefined | **0 established** | — |
| **control: cited-but-undefined** | **ZERO** | |

**Before this item: ZERO Trait-claimed**, exactly like FP-004.

## The citation, made while the mapping was in hand

**`AC-EMP-0014` — *"`Terminated` is terminal and preserves the aggregate for history. NO TRANSITION OUT OF
`Terminated` EXISTS"*.**

**`A_terminated_employee_cannot_be_activated_back_into_the_workforce`**, body read: it sets an employee to
`Terminated`, calls activate, and asserts **409 Conflict AND that the status is still `Terminated`
afterwards** — with its own comment saying why both are needed. **That is the criterion, asserted.**

⚠ **Key matched, not chosen:** the file already used `Criterion` four times. **I first wrote `Acceptance`
and corrected it** — a fifth spelling would have been introduced by a tidying reflex.

## ⚠ How the absences are graded, since the ruling asks

**"Not implemented: 0 established" is not a claim that nothing is missing.** The instrument is an **ID-text
search across four Trait keys plus prose**, and it establishes only whether a mapping is *written down*.
⚠ **It cannot see whether a criterion is satisfied, and it never could** — the 45 are unmeasured against
the product, not measured and found absent.

**What is established:** every `AC-EMP` cited resolves; nothing points at a criterion that does not exist.

## ⚠⚠ THE CROSS-PACKAGE PICTURE, WHICH IS THE FINDING

| package | defined | Trait-claimed | unmentioned |
|---|---|---|---|
| FP-002 | 51 | 19 | 32 |
| FP-003 | 93 | 12 (+1 prose) | 80 |
| FP-004 | 64 | **0** (+1 prose) | 63 |
| **FP-006** | **47** | **0 → 1** (+1 prose) | **45** |

**255 criteria across four packages; 32 citations.** ⚠ **And FP-006 is the first NON-PLATFORM package —
the pattern is not a Platform habit. It is the product's.**

**Two packages now sit at zero-or-one in a well-tested area**, and in both the tests plainly exist:
Employee owns read-scope, termination, account closure, placement — **and `EmployeeTerminationAccountClosureTests`
alone holds six tests whose subjects are FP-006 criteria.**

## Scope
- **One citation, one body read.** The other five tests in that file were read as **names** during item
  207 and are not cited on that basis.
- **No search was run to find more mappings** — item 215's rule is *cite what you found*, and re-measuring
  FP-006's 45 is the expensive thing this record keeps declining.

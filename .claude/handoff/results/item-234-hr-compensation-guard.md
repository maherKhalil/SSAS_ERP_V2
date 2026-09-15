# Item 234 — the guard is named after what it bans, and its first run found only its own false positives

**`No_employee_compensation_value_is_declared_in_hr`, in `HrCompensationAbsenceTests`. TASK gate: see
foot.**

## What it replaces

`AC-PAY-0003` clause 2 was guarded only by
`PositionApplicationArchitectureTests.No_position_command_carries_a_compensation_value_or_headcount` —
**Position mutation commands only.** ⚠ **A future `Employee.BaseSalary` would pass it: wrong type, wrong
package, not a command.**

⚠ **And a payroll-side search could never have reached it.** A payroll criterion guarded by a test called
`No_position_command_…` is invisible from the criterion's own module. **This one is named after what it
bans**, per the instruction.

## The order mattered: the criterion was corrected first

**As worded, clause 2 was FALSE** — the salary band stores three amounts. **Establishing which artefact
was narrower** produced the answer that the specification was, and the criterion now says **employee**
compensation with the band as the ruled exception.

⚠ **A criterion false as read is worse than one that is silent: a test written to the letter fails
against the salary band and gets "fixed" by deleting it.** **Writing this guard against the uncorrected
wording would have produced exactly that test.**

## ⚠⚠ THE FIRST RUN FAILED, AND EVERY OFFENDER WAS MINE

**Two hits: `JobGradeUpdated.NewSalaryGradeId` and `.PreviousSalaryGradeId`.** Both are structural
POINTERS — the category `DEC-POS-0023` expressly permits — and my allowlist named only the bare
`SalaryGradeId`, so the same two pointers under different prefixes tripped it.

⚠⚠ **THE EXISTING POSITION GUARD RECORDS THE IDENTICAL FAILURE IN ITS OWN COMMENT:** *"Matching on
'Salary' alone would forbid the pointer and prove the wrong thing, which is how this guard first
failed."*

**I read that comment while researching this item and made the same mistake one package over.** ⚠ **Naming
a failure mode confers no immunity to it** — and the written diagnosis was in the file I was modelling
the new guard on.

**The fix is the RULE rather than the list: a name ending in `Id` is a reference.** ⚠ **Bounded to
non-`decimal` properties, so a `decimal` called `SomethingId` is still caught — the exemption is for
pointers, not for anything that spells itself like one.**

## ⚠ And the true population is empty, which is the actual answer to 234

**With the pointer rule correct, the offender list is empty.** Combined with the earlier census — the
whole HR domain declares `decimal` in ONE file — **`AC-PAY-0003` clause 2 holds, and now holds under a
guard rather than under an observation.**

## The controls

- **Anti-vacuity floor**: four links stand between the assembly and the offender list, and the ban is
  satisfied if any stops matching.
- ⚠⚠ **The stronger one: the ruled exception must actually be FOUND.**
  `Assert.Equal(["MaximumAmount", "MidpointAmount", "MinimumAmount"], band)` — **a floor alone passes if
  the walk finds three properties on the wrong type.** **And a fourth amount added to the band fails here
  rather than slipping through as "already allowed".**
- **`int` is deliberately NOT banned**: `RowCount`, `ByteCount` and `RankOrder` are counts and ordinals.
  Banning the type would forbid them to prove nothing about pay.

## The plants

| plant | result |
|---|---|
| `Employee.BaseSalary` (decimal) | ⚠ **fails, naming `Employee.BaseSalary`** — the literal defect the criterion exists to prevent |
| `Employee.SeveranceId` (decimal) | ⚠⚠ **fails** — a decimal spelled like a pointer is still caught |

**The second is the one that proves the exemption is bounded rather than a hole.**

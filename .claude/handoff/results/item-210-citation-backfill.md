# item 210 — the backfill was smaller than the item assumed, and there are THREE Trait keys

**Gated work** (citations only, no behaviour change). **TASK gate green, 0 warnings.**

## ⚠⚠ THE PREMISE WAS WRONG: FP-015's TESTS ALREADY CITED THEIR CRITERIA

The item assumed *"~32 tests hold a mapping that exists ONLY in a result file"*. **They do not.** **Seven
of FP-015's fourteen were already cited in code before I touched anything** — `AC-SS-0004`, `0005`, `0007`,
`0008`, `0009`, `0011`, `0012`.

⚠ **I missed them in item 208 because I had looked at FP-002's convention.** They use a **different Trait
key.**

## ⚠⚠ THERE ARE THREE TRAIT KEYS FOR ONE RELATIONSHIP

| key | uses carrying an `AC-` id |
|---|---|
| `[Trait("Acceptance", …)]` | **56** — FP-002's convention |
| `[Trait("Criterion", …)]` | **24** — FP-015's convention |
| `[Trait("Decision", …)]` | **23** |

**A grep on any one key finds a third of them.** ⚠ **This is the padded-versus-unpadded family for the
FOURTH time today**, and it lands directly on Principle 29, whose whole value is that the mapping is
greppable.

**The principle survives; the grep has to be KEY-AGNOSTIC** — searching the **id text** rather than the
attribute, which is what item 208's count already did, **so that figure of 19 stands.**

⚠ **And my own new tests from item 209 used `Decision`, the wrong key for a criterion.** Corrected to
`Criterion`, matching their AC-SS neighbours.

## The backfill actually applied

**Five criteria, each confirmed against a test whose body or citation I had already read:**

| criterion | test given the citation |
|---|---|
| `AC-SS-0001` | `The_records_self_permission_alone_reads_the_callers_own_records` and `…own_leave` — both previously cited only the REQ |
| `AC-SS-0002` | `The_self_route_contract_names_no_employee_on_any_surface` (Attendance) |
| `AC-SS-0003` | `The_self_permission_alone_reads_the_callers_own_payslips` |
| `AC-SS-0010` | `The_link_is_untouched_by_a_refusal` — item 207 established it pins 0010 and 0011 |

**Existing citations were ADDED TO, never replaced.** ⚠ `The_self_permission_alone_reads_the_callers_own_payslips`
already cited `AC-SS-0004`, which reads like the wrong criterion — `0004` is *another* employee's payslip
being unreachable. **I did not silently correct it.** Rewriting a recorded mapping on my reading of a test
name is exactly the invention this item forbids. **Reported, not changed.**

## ⚠ SKIPPED AND LISTED, as ruled

**`AC-SS-0006`** — *the self permission alone does not grant administrative access.* **No test asserts it.**
The near neighbours are self-vs-**self** (`The_records_self_permission_does_not_open_the_leave_route`) or
absence-of-self (`Without_the_self_permission_the_route_is_refused`). ⚠ **Neither is
self-vs-administrative, and citing one would make the grep confidently wrong** — which is worse than
leaving it uncited.

**FP-014's 20 were NOT backfilled.** `item-161`'s table maps criteria to test **classes** on several rows
(*"`0001` `0003` `0044` … `PlatformAppendOnlyGuardTests`"*). ⚠ **A citation needs METHOD granularity, so
deriving it would be re-measuring rather than backfilling** — and re-measuring under a backfill's name is
how a wrong citation gets written. **Four `AC-SUB` ids are already cited; the other 16 need item 161's
measurement repeated at method level, which is its own item.**

## The control, re-run across ALL packages

**93 `AC-` ids cited in tests; 606 defined across the specs.** ⚠ **CITED-BUT-UNDEFINED: ZERO.** No test in
any package points at a criterion that does not exist.

⚠ **And the control caught a counting fault of my own.** **607 was the SUM of per-package distinct counts;
606 is the true DISTINCT union.** `AC-AUTH-0045` appears in two files — **and it is a legitimate prose
cross-reference in FP-003, not a collision.** **So the identifier count sees a MENTION, not a DEFINITION,
and a cross-reference reads as one. The right figure is 606.**

## Scope
- **A citation asserts that a test CLAIMS a criterion, never that it asserts it correctly.** Every one
  added here rests on a body or citation read in items 206, 207 or 209 — none on a name alone.
- **AC-SS is now 13 of 14 cited**; the fourteenth is skipped above with its reason.
- No existing citation was removed or altered.

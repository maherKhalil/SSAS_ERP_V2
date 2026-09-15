# B18 pass 08 — two "unresolved" recovered, and the reason both were wrong

**TASK gate green, 0 warnings. Control: 145 cited, zero dangling.**
⚠ **FP-012 → 12 of 31. FP-006 → 37 of 47.**

## What was wrong, and it was mine twice

| criterion | I recorded | actually |
|---|---|---|
| **`AC-PAY-0002`** | unresolved (pass 07) | ⚠ **pinned by `The_record_in_force_is_the_latest_one_not_after_the_date`, SEVEN LINES ABOVE the two tests I read** |
| **`AC-EMP-0035`** | unresolved **twice** (passes 03, 04) | ⚠ **pinned by `Point_in_time_attribution_differs_from_the_current_branch`** |

**Both were found by searching the MECHANISM, not the name** — the ruling's new B18 clause, on its first
use, recovering two errors in one pass.

- `AC-PAY-0002`: one `grep` for the domain method returns the call sites; four of them are positive
  assertions including the boundary date itself (`Apr` → 2000), which is what makes *"not after"* inclusive.
- `AC-EMP-0035`: one `grep` for `EffectiveFromUtc` **ordering** finds a test that selects the record with
  the greatest date ≤ the instant and asserts it names BranchA **while the current branch is B** — the
  criterion's mechanism verbatim. ⚠ **Its name shares no significant word with anything I searched for.**

## ⚠⚠ THE STRUCTURAL POINT IS BIGGER THAN THE TWO INSTANCES

**This sweep has a control on its positive claims and none on its negative ones.**

`zero dangling` challenges **every citation, every pass, mechanically**. ⚠ **Nothing whatever checks an
*unresolved*.** So a wrong citation is caught by a machine tomorrow; **a wrong disposal is accepted on the
strength of the reading that produced it and is invisible afterwards, because *unresolved* and *genuinely
unpinned* look identical in the record.**

⚠ **My error rate has been measured entirely on the half that cannot be wrong quietly.** Two of my
unresolved dispositions were wrong; **the citations they sat beside were not**, and that asymmetry is a
fact about the instruments, not about the care taken.

## ⚠ AND MY REASONING ERROR HAS A NAME

I read two tests, correctly observed that **neither asserts the rule**, and wrote that **nothing does.**

**A true statement about the members examined, generalised to the set.** ⚠ **It is the same failure the
mechanism rule was written for — and I made it in the item where I quoted that rule approvingly.**

## The sixth shape survives even though the instance dissolved

⚠ **The two `Assert.Null` edge-case tests ARE jointly satisfiable by a function that always returns
nothing.** They are non-vacuous **only because the positive test sits beside them.**

**So the lesson sharpens rather than disappears: reading a guard without its neighbours made a COMPLETE
PAIR look like a missing rule.** ⚠ **A guard's neighbours are part of the guard.**

## `AC-EMP-0044` stays unresolved — but on stronger evidence now

*"Refusals from the branch or company write boundaries are surfaced as generic scope denials; no response
discloses table names, database…"*

**Searched by mechanism, not name:** the boundary's own exception text (`"Branch ownership"`,
`"Company ownership"`), the exception-to-response mapping path, and tests asserting a response body.
⚠ **The message assertions exist; nothing asserts the RESPONSE is generic.**

**So this is now a candidate real gap** — ⚠ **and the boundary throws with a message naming the ownership
dimension, so if that text reaches a response it discloses exactly what the criterion forbids.** **Nothing
establishes that it does not.** **Recorded, not built: it is a by-product, and the fifth.**

## Scope
- **Two criteria recovered; both citations body-confirmed before being written.**
- ⚠ **I have not re-run the mechanism search across FP-006's other nine unresolved.** They were recorded
  with the weaker method and **should be assumed to have the same error rate** until re-checked — which is
  a truer statement than the number in any earlier pass.

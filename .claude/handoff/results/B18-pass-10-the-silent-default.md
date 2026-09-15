# B18 pass 10 — the sixth by-product is built, and I push back on one of the two clauses

**TASK gate green, 0 warnings. ⚠ FP-006 → 40 of 47.**

## `AC-EMP-0016` is now asserted

**Verified the mechanism myself before building on it:** `EmployeeReadService.cs:43` —
`DefaultStatuses = [Active, Inactive]`, applied at `:295` when the caller names no status. **Implemented
and unasserted, exactly as ruled.**

`A_search_without_a_status_filter_excludes_terminated_employees` seeds an active and a terminated
employee, terminates one, searches **with no status filter**, and asserts the terminated one is **absent
and the active one present.**

⚠ **Both sides, deliberately: the active-present assertion is what stops the test passing on an empty
page**, which a terminated-absent assertion alone would allow. **That is the one-sided/two-sided
distinction applied at the moment of writing rather than noticed afterwards.**

**Plant:** added `Terminated` to `DefaultStatuses` — **the literal failure mode** — and it reddens.
Restored.

## ⚠ Why no existing test reached it, stated in the test itself

`The_search_defaults_are_the_documented_ones` asserts `Assert.Null(LastCriteria.Statuses)`. ⚠ **That is
CORRECT at that layer and says nothing about the read service.** **The default is not *exclude terminated*,
it is *no filter*** — and those coincide only because a layer further down makes them coincide. **That
layer had no test.**

**And the adjacent half was already guarded:** `A_terminated_employee_remains_retrievable_by_id` proves the
exception. ⚠ **So the pair had its EXCEPTION asserted and its RULE — the one the read service performs
silently on every search — not.** **The exception is the memorable case; the rule is the one that runs.**

## ⚠⚠ PUSHBACK ON CLAUSE 2, ACCEPTED WITH A BOUND

**Clause 1 — *a disposal records the search that produced it* — I accept unreservedly.** It costs nothing
(I already run the searches), and **it converts an unfalsifiable negative into one a later reader can
re-execute.** ⚠ **It is the first thing in this sweep that would make a wrong *unresolved* discoverable by
anyone but its author.**

**Clause 2 — *a citation names the clause, not only the criterion* — I accept for MULTI-CLAUSE criteria and
push back on applying it universally.**

⚠ **On a single-clause criterion it adds a line that restates the criterion's only content**, and this
record already shows what happens to ceremony: **the `Decision`/`Criterion`/`Acceptance`/`AcceptanceCriteria`
sprawl began as four reasonable local choices.** **A rule applied where it does nothing is how a convention
starts drifting.**

**The bound I propose: name the clause when the criterion has more than one, which is exactly the
population where the over-claim was possible.** ⚠ **`AC-EMP-0035` had four clauses; `AC-PAY-0007` has one.
The rule's whole value came from the first kind.**

**I have applied it that way this pass** — `AC-EMP-0016` is single-clause and cited plainly.

## Scope
- **One test, one plant, both directions verified.**
- ⚠ **A build error of my own**: the insertion landed between a `[Fact]` and its `[Trait]`, orphaning the
  attribute and producing `CS0579`. **Caught by the build, which is the instrument that cannot be talked
  out of it** — and a reminder that an anchor chosen by content can still land in the wrong structural
  position.

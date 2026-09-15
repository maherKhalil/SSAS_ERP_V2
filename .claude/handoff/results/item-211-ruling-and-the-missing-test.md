# item 211 — the citation was wrong, and the missing test is written

**Gated work.** `PayrollSelfServiceTests` 7 of 7 green, plant verified both directions.
⚠ **FP-015 is now 14 CITED of 14.**

## (a) ⚠ THE RULING: `AC-SS-0004` WAS WRONG ON BOTH TESTS, AND IT IS NOT A POSITIVE HALF

The ruling allowed that it might be the positive half of `0004`'s pair. **It is not — because `0003` IS
that half.** The spec pairs them explicitly:

- `AC-SS-0003` — *A mapped identity holding the self permission reads its own payslips.* **As `AC-SS-0001`.**
- `AC-SS-0004` — *A payslip belonging to another employee is unreachable.* **As `AC-SS-0002`.**

**So the positive/negative pair is 0003/0004, and a test asserting the positive case cannot also be 0004.**

**What the body actually asserts** (`The_self_permission_alone_reads_the_callers_own_payslips`): sends the
self permission, expects **200**, and asserts `host.SelfService.AskedForUser` is non-empty — *the subject
was resolved from the caller*. **That is `AC-SS-0003` exactly, and nothing about another employee.**

⚠ **And the second citation was clearly wrong**: `Without_the_self_permission_the_route_is_refused` sends a
caller holding **neither** permission and expects 403. **Its own comment says what it is** — *"THE CONTROL…
so the success above is the permission working rather than the route being open."* **It is `AC-SS-0003`'s
control.**

⚠ **Corroborated by the file's own header, which lists its criteria as `0005`, `0007`, `0008`, `0009` — it
never claimed `0004` at all.**

**Ruled and applied, replacing rather than papering over:**

| test | was | now |
|---|---|---|
| `The_self_permission_alone_reads_the_callers_own_payslips` | `0004` + `0003` | **`0003`** |
| `Without_the_self_permission_the_route_is_refused` | `0004` | **`0003`**, with a clause recording the correction |
| `The_self_route_contract_names_no_employee_on_any_surface` | `0007` | `0007` + **`0004`** |

⚠ **`0004` had to go somewhere**: removing it from two tests would have left the criterion uncited. **It
belongs on the contract test, which is what actually makes another employee's payslip unreachable** — the
route binds no subject — mirroring `0002` on Attendance's contract test in item 210.

## (b) The missing test, and its control

**`The_self_permission_alone_does_not_open_the_administrative_route`** — a caller holding only
`ViewOwnPayslips` is refused `/api/payroll/employees/1/payslips`. `AC-SS-0005` was already pinned in the
other direction; **this is its mirror, and it was the one criterion of FP-015 nothing asserted.**

**With its control:** `The_administrative_permission_reaches_the_administrative_route`. ⚠ **Without it, a
mistyped route, a nonexistent route, or a refusal for any other reason produces the same 403 and the test
passes while proving nothing about permissions.**

**The plant — the administrative route made to accept the SELF permission — reddens BOTH**: the ban because
the widening is real, and the control because the administrative permission no longer reaches it.
**Two directions, two failures, from one plant.**

## ⚠⚠ A VOID PLANT THAT REPORTED GREEN, AND I ALMOST BANKED IT

My first plant put `// PLANT 211` before a `.WithName(…)` continuation and **broke the fluent chain —
`CS1002`.** The build failed. **`dotnet test --no-build` then ran the STALE binaries and reported
`Passed! 7`.**

⚠ **A plant that does not compile is VOID, not passing** — and here it did not merely fail to redden, it
returned a confident green from a build that never happened. **Caught because the build output was read
before the test output, not after.** Re-planted with the chain intact; it then reddened correctly.

## Scope
- The ruling rests on the criteria text, the test bodies, the tests' own comments, and the file header —
  four independent readings that agree.
- `AC-SS-0004`'s new home asserts the **mechanism** of unreachability (no subject on any surface), not a
  request for another employee's payslip returning 404. **No such request is expressible**, which is the
  criterion's own point.

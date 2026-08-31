# B18 pass 15 — groups D, E and G searched: FP-012 17 → 21, and the biggest finding is not a citation

**TASK gate green, 0 warnings. FP-012 at 21 of 31.**

## Cited: four, two fully and two partly

| criterion | test | state |
|---|---|---|
| **`AC-PAY-0027`** | `Hr_permissions_do_not_reach_pay_data` | ✅ **the criterion verbatim, both halves** |
| **`AC-PAY-0009`** | `Elements_evaluate_in_ascending_order_and_a_later_one_sees_an_earlier_result` | ✅ **both clauses** |
| **`AC-PAY-0026`** | `The_payslip_adds_up_because_the_total_is_the_sum_of_rounded_lines` | ⚠ **sum clause only** |
| **`AC-PAY-0003`** | `No_other_module_learns_about_payroll` | ⚠ **clause 1 only, by superset** |

**`AC-PAY-0027`** — a theory over compensation, compensation/current **and** payslips, with seven HR
permissions and no payroll one, asserting 403. ⚠ **It covers both nouns the criterion names rather than
one of them**, which is what makes it verbatim rather than partial.

**`AC-PAY-0009`** — ⚠ **clause 1 is asserted where the order is LOAD-BEARING**: `PercentageOfGrossToDate`
reads earnings accumulated so far, so the levy seeing basic + bonus = 5000 and producing 500 is only
possible if it ran third. ⚠ **Clause 2 — *a line records the order used* — is the `Sequence` comparison,
a separate claim a reader who stopped at the amount would miss.**

**`AC-PAY-0026`** — ⚠ the retrieval clause belongs to `GetPayslipAsync(scope, runId, employeeId)`, and
**nothing constructs that class.** Recorded, not implied.

**`AC-PAY-0003`** — clause 1 by superset: the theory walks all three HR assemblies, and an assembly that
cannot reference `SSAS.Payroll.*` cannot expose a compensation type through any endpoint. ⚠ **Clause 2 is
explicitly NOT this test — a pay column on an HR table needs no reference to Payroll whatever.**

## ⚠⚠ THE FINDING: THREE CONCRETE READ SERVICES ARE CONSTRUCTED BY NOTHING

**Census over `new …ReadService(` in `tests/` — what CONSTRUCTS the type, not what mentions it:**

**Constructed:** `EmployeeReadService`, `DepartmentReadService`, `PositionReadService`,
`SalaryGradeReadService`, `JobGradeReadService`, `EmployeeRunHistoryReadService`, `CompanyReadService`,
and a dozen Platform services.

⚠⚠ **Constructed NOWHERE: `PayrollReadService`, `GlReadService`, `AttendanceReadService`.**

**`scope.CompanyIds.Contains(...)` has never executed in a test, in three modules.** ⚠ **And
`Every_read_service_method_requires_a_scope` asserts every method TAKES a scope — TAKING A SCOPE IS NOT
APPLYING IT**, the same shape as item 227's GL finding one layer down.

**So `AC-PAY-0005` — *compensation granted in one company is not readable from another* — cannot be
pinned by any citation. It needs a test that runs the real service. Queued as 233.**

### ⚠ And the cause is ONE difference, not three omissions — with an exception that matters

| module | why its read service is never constructed |
|---|---|
| Payroll, GL | ⚠ **the API test host never calls `Add…Infrastructure`** |
| **Attendance** | ⚠⚠ **it DOES — and then `AddSingleton<IAttendanceReadService>(Reads)` overrides it** |

**Two of six hosts compose the module half only, and every count follows from that.** ⚠⚠ **But a single
remedy aimed at composition would have left Attendance untouched while looking complete** — *finding one
cause is not finding the cause; a member the cause does not explain is the second cause, not noise.*

## Left uncited, with their searches

- **`AC-PAY-0005`** — searched by construction; no test reaches the real read service. **Queued as 233.**
- **`AC-PAY-0013`** — the applicability side is asserted from the negative by three tests
  (`An_element_the_employee_is_not_assigned_produces_no_line_at_all`,
  `The_net_pay_payable_element_produces_no_line_because_net_pay_is_derived`,
  `An_inactive_element_is_excluded_rather_than_refusing_the_run`). ⚠ **The positive — one line per
  applicable element per INCLUDED EMPLOYEE — has no multi-employee count assertion anywhere.**

## ⚠ And one disposal I nearly got wrong

**`AC-PAY-0003` clause 2's only guard is `No_position_command_carries_a_compensation_value_or_headcount`
— POSITION MUTATION COMMANDS only.** ⚠ **A name search from the payroll side reaches nothing: the test
that guards a payroll criterion is named after the type it walks.** **Establishing whether the guard or
the criterion was narrower produced the answer that the CRITERION was — the whole HR domain declares
`decimal` in one file — and the specification has been corrected.**

# B18 pass 14 — the missing control fixed at its second site, and a test name that promises more than its body

**TASK gate green, 0 warnings. FP-012 → 17 of 31.**

## The control, fixed at every site of the mechanism

**Searched for EVERY site asserting debits equal credits, not the two named:** three hits.

| site | state |
|---|---|
| `PayrollChainSqlServerTests:505` | equality **+ `Sum(Debit) > 0m`** ✅ |
| `PayrollEndpointTests:301` | ⚠ **equality only** — `0 == 0` satisfies it, under a name promising the journal *balances* |
| `JournalDomainTests:229` | `Assert.Equal(line.Credit, mirrored.Debit)` — **a per-line reversal mirror, a different mechanism** |

**One line added at the second site**, with a comment saying the equality was copied and the control left
behind.

⚠ **No plant.** For a `> 0` assertion the passing run **is** the proof: it passes only if the sum is
non-zero, which is exactly what a plant would establish. **A ceremonial plant here would prove arithmetic.**

## Cited: two

- **`AC-PAY-0016`** — `Approval_is_refused_to_a_caller_holding_every_other_payroll_permission`, **the
  criterion verbatim**, 403.
- **`AC-PAY-0022`** — ⚠ **PARTLY PINNED**, see below.

## ⚠⚠ `AC-PAY-0022`: THE TEST NAME PROMISES WHAT THE BODY DOES NOT ASSERT

**The criterion:** *"a run whose pay date falls in a closed fiscal period cannot be approved, **and the
response names the period**."*

**The test:** `Approval_into_a_closed_period_is_refused_and_names_the_period`.

**The body:** `Assert.Equal(409)` and `Assert.Equal("payroll.period_closed", problemCode)`.

⚠ **The problem code names the CONDITION, not the PERIOD. Nothing asserts WHICH period.** **The test's own
name carries the claim its body drops** — and a name search would have closed this criterion as fully
pinned in one step.

**The contrast makes it sharp:** `AC-PAY-0021`'s test really does assert
`Contains("HOUSING", error.Message)`. ⚠ **Two criteria in the same package, both saying *the response names
the X*, and only one of them asserts it.** **Eighth candidate gap.**

## Group C: no payroll owner

`0006` and `0008` (pay-element code supplied by the caller, unique per company). ⚠ **Every code-uniqueness
test found is Department, Position or Branch** — `A_duplicate_normalized_code_is_refused_within_the_company`
is `DepartmentApplicationSqlServerTests`. **Foreign subjects, and I did not cite past them.** **Recorded
search: `[Cc]ode|element_must` test names across `Payroll.Tests` and `Integration.Tests`.**

## Where FP-012 stands

**17 of 31 cited. 14 uncited.** Groups A, B (partly), C and F searched; **D, E, G named and unsearched.**

⚠ **The grouping remains the durable artefact** — anyone resuming starts from named mechanisms, not
numbers.

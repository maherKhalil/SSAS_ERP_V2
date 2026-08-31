# Item 222 — the `names_the` audit: 7 of 11 kept their promise, and the four that did not share a cause

**TASK gate: see foot. Population: 11 tests in 8 files, not ten.** `grep -rn "names_the" tests/ --include=*.cs`
returns **eleven** — `GlEndpointTests` contributes two and `PayrollEndpointTests` three, and the eleventh is
`AttendanceCalendarPeriodEndpointTests:42`.

## The audit

| # | test | the promised value | asserted? |
|---|---|---|---|
| 1 | `AttendanceCalendarPeriodEndpointTests:42` | `field` = `weekendDays` | ✅ |
| 2 | `AttendanceLeaveRequestEndpointTests:78` | the new request's id | ⚠ **non-empty Guid only** |
| 3 | `CompaniesMutationEndpointTests:327` | `field` = `companyName` | ✅ |
| 4 | `EmployeeEndpointTests:936` | the parameter at fault | ✅ — one code per parameter |
| 5 | `GlEndpointTests:135` | `field` = `code` | ✅ |
| 6 | `GlEndpointTests:469` | the account | ⚠ **`gl.account_inactive` only** |
| 7 | `ModuleEndpointRequirementTests:104` | module, contract, remedy | ✅ — all three |
| 8 | `PayrollEndpointTests:100` | `assignments[].payElementId` | ✅ |
| 9 | `PayrollEndpointTests:248` | the period | ⚠ **`payroll.period_closed` only** |
| 10 | `PayrollEndpointTests:263` | the element | ⚠ **`payroll.element_unmapped` only** |
| 11 | `PositionApplicationArchitectureTests:328` | the disclosure | ✅ — `Contains("pay")` |

**Rate: 7 of 11.** **All four gaps are now closed, each with a plant.**

## ⚠⚠ THE RATE IS NOT THE FINDING. THE DIVIDING LINE IS.

**Every test whose promised value is a COMPILE-TIME CONSTANT kept its promise: 7 of 7.**
**Every test whose promised value is a RUNTIME one did not: 0 of 4.**

**It is not carelessness, and it is not a naming habit.** The problem document carried `code`,
`correlationId`, `resourceKey` and `field` — ⚠ **and `field` carries the NAME of an input, never a
VALUE.** A refusal that had to name *which* period, *which* element, *which* account **had no channel to
say it through.** Those three tests are older than `ApiError.Detail`, and they assert everything that
was assertable when they were written.

⚠ **The channel arrived at T-261 and nothing came back for the tests whose names had been promising it
all along.** That is the shape worth a rule: **a transport capability lands, and the assertions it
unblocks are invisible because the tests that want it already exist and already pass.**

## ⚠⚠ AND THE CONTRAST IN THE QUEUE ROW — MINE ORIGINALLY — IS FALSE

The row says `:263` *"asserts `Contains("HOUSING")`"*. **It does not.** `:263` asserted 409 and
`payroll.element_unmapped` and nothing else. **The `Contains("HOUSING")` assertion is
`PayElementDomainTests:124` — a DOMAIN test, which constructs `PayElementErrors.Unmapped("HOUSING")`
directly and reads `.Message`.**

⚠ **I wrote that comparison in B18 pass 14 and it crossed two layers without saying so.** It made one
endpoint look careless beside a diligent sibling. **The truth is worse and more useful: no API test in
either file asserted a named subject, because none of them could.** The stale note is corrected in place
at `PayrollEndpointTests:242` and kept rather than deleted, because the error is the instructive part.

## What was added, and the plants

| test | assertion | plant | result |
|---|---|---|---|
| `:248` period | `detail` contains `FY2026-P01` | handler drops `window.PeriodName` | ⚠ **only that test reddens** |
| `:263` element | `detail` contains `BASIC` | `Unmapped("a pay element")` | ⚠ **only that test reddens** |
| GL `:469` account | `detail` contains `5200` | `AccountErrors.Inactive` drops the code | ⚠ **only that test reddens** |
| Attendance `:78` id | body id == `Added.Single().Id`, Location agrees | route returns `Guid.NewGuid()` | ⚠ **only that test reddens** |

**Four plants, four singleton failures — each one reddens its own test and leaves the other three green,
so the assertions are attributed and not merely present.**

⚠ **The period test's ARRANGEMENT changed, deliberately.** It named the closed fiscal period
`"January 2026"` — **the same string as the run's own payroll period.** `PeriodClosedForPosting` takes
`window.PeriodName ?? period.Name`, so **a handler that ignored the window entirely would have satisfied
the new assertion perfectly.** The fiscal period is now `FY2026-P01`. **Without that change the plant
would not have reddened, and the test would have been ceremony.**

⚠ **The GL account test needed no such change: two accounts are on the draft and only one is inactive**,
so `5200` discriminates against `1000` already.

## ⚠⚠ A FINDING THAT IS A RULING, NOT A FIX — `ApiProblems.cs` STATES A MEASUREMENT THAT IS FALSE

The licence for showing `detail` on every 4xx is written at `ApiProblems.cs:15-17`:

> *"it is safe because **no message carries a runtime value** — measured across `src/`: zero
> interpolations, zero concatenations, zero variables. There is nothing in a message that was not
> written by hand into a constant."*

⚠ **Measured 2026-08-31, `grep -rn '$"' src/ --include=*Errors.cs`: SEVEN interpolated messages in THREE
files.**

| file | messages |
|---|---|
| `SSAS.GL.Domain/Accounts/AccountErrors.cs` | `Inactive` — the account code |
| `SSAS.Payroll.Domain/Elements/PayElementErrors.cs` | `Unmapped`, `Inactive` — the element code |
| `SSAS.Payroll.Domain/Runs/PayrollErrors.cs` | three status messages, and `PeriodClosedForPosting` |

⚠⚠ **And this item DEPENDS on them.** Three of the four assertions added above exist precisely because
runtime values **do** travel to callers. **The control and its stated justification now contradict each
other**, and the second comment block in that file compounds it: it revisits the reasoning, says the
measurement *"answered the wrong question"* — and reaffirms it as **"true"**.

**I did not find a leak.** Every interpolated value is the caller's own tenant data, reached only after
authorization, and the 401/403 fail-closed rule is untouched. ⚠ **But the class the comment says it
defends against — *a new error factory that ships detail by default* — is no longer hypothetical, and
the sentence that would have warned the next author reads as an all-clear.** **Ruling yours; I have
changed no comment in `src/`.**

## Not changed, and why

- **#4 `An_out_of_range_page_names_the_parameter_at_fault`** — ✅ **the promise IS kept by the code**:
  `request.page_number_invalid` and `request.page_size_invalid` are distinct per parameter, which is the
  whole point of T-260. **The parameter is named by the code; there is no second value to assert.**
- **#7 and #11** — both assert every value their names promise (three, and one).
- **#1, #3, #5, #8** — `field` is the promised value and each asserts it verbatim.

## Citations

- **`AC-PAY-0022`** — ⚠ **was PARTLY PINNED, now FULLY pinned.** The stale note is corrected in place.
- **`AC-PAY-0021`** — ⚠ **added to the API test as its TRANSPORT half.** The domain test pins that the
  message is **built** with the element code; this pins that it **survives**. **Two tests, one criterion,
  and the second is the one nobody had written.**

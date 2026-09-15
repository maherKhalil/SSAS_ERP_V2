# B18 pass 13 — FP-012 grouped by mechanism first: 12 → 15

**TASK gate green, 0 warnings. Control: 156 cited, zero dangling.**

## The grouping, done BEFORE searching

The 19 uncited criteria were grouped by **the mechanism that would pin them**, not by their numbers:

| group | criteria | mechanism |
|---|---|---|
| **A — run inclusion** | `0010`, `0011`, `0012` | the date-boundary predicate |
| **B — GL posting** | `0019`, `0020`, `0022`, `0023`, `0024` | journal creation on post |
| C — element identity | `0006`, `0008` | the pay-element code |
| D — compensation scope | `0003`, `0005` | company-scoped readability |
| E — calculation | `0009`, `0013` | element evaluation and line production |
| F — run authority/audit | `0016`, `0017`, `0018` | approval permission and posted immutability |
| G — payslip/permission | `0026`, `0027` | payslip read and permission separation |

⚠ **The grouping paid immediately in group A: `AC-PAY-0011` and `AC-PAY-0012` are the SAME PREDICATE read
from opposite sides**, and one test pins both.

## Cited: three

- **`AC-PAY-0011` + `AC-PAY-0012`** — `Inclusion_is_a_pure_function_of_dates_at_both_boundaries` asserts
  *terminated on exactly the first day* → **included**, and *terminated the day before it began* →
  **excluded**. ⚠ **Two criteria, one test, because the criteria are the two sides of one boundary.**
  **Serially I would have searched twice and found the same test twice.**
- **`AC-PAY-0020`** — the chain test asserts `Sum(Debit) == Sum(Credit)`, the criterion verbatim.

## ⚠ AND `AC-PAY-0020`'s CONTROL IS ALREADY THERE, WRITTEN BY SOMEBODY ELSE

`Assert.True(journal.Lines.Sum(line => line.Debit) > 0m)` sits on the next line.

⚠ **Without it, a journal with NO LINES balances trivially and satisfies the criterion perfectly.** **The
two-sided rule, in a test written long before this sweep named it** — the fourth independent arrival at the
same idea, after FP-006's revocation trio, `AC-PAY-0004`'s three clauses, and the payroll floors.

## What remains, and an honest note on this pass's size

**FP-012: 15 of 31 cited, 16 uncited.** Groups C–G were **grouped but not searched** — this pass spent its
effort on A and B and stopped there rather than half-searching five more groups.

⚠ **The grouping itself is the durable part and survives the pass ending**: the next pass starts from seven
named mechanisms rather than sixteen loose numbers, which is exactly the difference the ruling described
between grouping early and grouping after a residue forms.

## Scope
- **Three citations, three bodies read.**
- ⚠ **`AC-PAY-0010` was NOT cited** though it shares group A's predicate: its first clause — *a run is
  created for one company and one period* — is a **run-creation** claim, not an inclusion one, and the
  inclusion predicate says nothing about it. **Same group, different mechanism, and the group is a search
  strategy rather than a licence.**

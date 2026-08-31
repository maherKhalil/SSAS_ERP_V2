# Item 232 — PHASE green, and the named regression was never constructible

**`[GATE GREEN — PHASE scope: all eight suites, Debug and Release]`, exit 0. ZERO failures anywhere in
the run.**

| suite | Debug | Release |
|---|---|---|
| Architecture | 623 | 623 |
| Platform | 1101 | 1101 |
| HR | 328 | 328 |
| API | 972 | 972 |
| Finance | 51 | 51 |
| Payroll | 93 | 93 |
| Attendance | 87 | 87 |
| **Integration** | **853** (24m57s) | **853** (26m10s) |

**Release baselines were eight releases stale** — they still read API 956, Architecture 618, Attendance
81, Finance 47, HR 326, Payroll 87 — **because TASK scope never runs Release.** All sixteen rows now
match their Debug counterparts.

## ⚠⚠ THE NAMED RISK WAS ANSWERED BEFORE THE RUN REACHED IT, AND BY SEARCH

The row was queued for one specific thing: **an Integration test that had pinned the defect 229 fixed** —
an Attendance refusal answering `BranchScopeDenied` where a company grant was the missing one.

**Searched by mechanism, three routes, before waiting:**

| route | result |
|---|---|
| `AttendanceScopeErrors.*` anywhere in `tests/` | ⚠ **only the four assertions written today**, all in `Attendance.Tests` |
| `BranchScopeDenied` in Integration | two hits, **both `EmployeeErrors.BranchScopeDenied`** — a different module's constant |
| the wire codes `branch.scope_denied` / `company.scope_denied` | every hit Employee or Department, **none Attendance** |

⚠⚠ **And the decisive one: `AttendanceOverlapChainSqlServerTests` substitutes
`GrantingScope : IAttendanceScopeResolver`, a stub that always grants. The Integration suite never runs
the real Attendance resolver at all.**

**So the red could not be built, and the run confirmed it.** ⚠ **The instrument was the mechanism search,
not the 51-minute run** — *ask whether the failure is CONSTRUCTIBLE before waiting on it.* **The run's
value was the fifteen other suite-legs nobody had a hypothesis about, and the eight stale Release
baselines it corrected.**

## What the run was actually worth

- ⚠ **Integration ran for the first time since three `src/` merges** — 229's behaviour change, 230's and
  231's test-only work, and 226's comment edit. **853/853, and 853 against a baseline of 848 is the five
  Integration tests added today landing.**
- ⚠ **Release ran for the first time in eight suite-generations.** **Nothing configuration-specific had
  drifted** — every Release count is identical to its Debug counterpart, which is the property the split
  exists to check and which no TASK gate can observe.

## Discipline note

**No file in the tree was touched for the whole run.** Every queued item behind it — pass 15's citations,
233, 234's guard, 235 — **is a test-file edit, and an edit mid-run corrupts the configuration still to
come.** ⚠ **The window looked idle and was not: holding the tree still IS the work while a two-configuration
run is live.**

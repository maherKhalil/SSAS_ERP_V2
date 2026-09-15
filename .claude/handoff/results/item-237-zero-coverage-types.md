# Item 237 — six query-bearing types are never executed, and my "floor of seven" was wrong both ways

**Coverage run: all eight suites, Debug, `--collect:"XPlat Code Coverage"`. Integration 856/856 in
25m16s. 2390 types measured. A BINARY per type, never a ratio.**

## ⚠ Scope note: Debug only, and that is not a shortcut

**Coverage answers *which lines executed*. Release executes the same lines.** ⚠ **Doubling a 25-minute
Integration run to produce an identical answer would have cost an hour to learn nothing.** **What
mattered was that Integration ran at all — a TASK-scope answer would have named every Integration-only
type dead.**

## THE DELIVERABLE — QUERY-BEARING TYPES WITH ZERO EXECUTED LINES: SIX

| type | assembly |
|---|---|
| `AttendanceRecordRepository` | Attendance.Infrastructure |
| `LeaveBalanceRepository` | Attendance.Infrastructure |
| ⚠ `EmployeeApproverDirectoryService` | HR.Infrastructure |
| ⚠ `EmployeePlacementDirectoryService` | HR.Infrastructure |
| `TenantCompanyCurrencyLookup` | Platform.Infrastructure |
| `UserCompanyAccessRepository` | Platform.Infrastructure |

**332 types have zero executed lines. 202 filtered as legitimate: 194 compiler-generated, 7 design-time
factories, 1 EF migration.** **Of the 130 remaining, 61 are transport contracts/DTOs and 11 are other
infrastructure; six are query-bearing.**

## ⚠⚠ THE INSTRUMENT IS VALIDATED, IN BOTH DIRECTIONS

**Known positives — the three read services item 233 made live TODAY:**

| type | hits |
|---|---|
| `AttendanceReadService` | 20 |
| `GlReadService` | 2 |
| `PayrollReadService` | 86 |
| `EmployeeReadService` (long covered) | 1500 |

⚠ **Before this morning all three of the first would have read ZERO. That is the `fa95522^` control in
coverage form, and it fires.** **Known negatives spot-checked at 0.**

## ⚠⚠⚠ AND THE PROXY CHECK REFUTES MY OWN CLAIM

**I reported seven types as a FLOOR on the untested set. It is not a floor. It was wrong in both
directions, and the two errors have OPPOSITE causes that do not cancel.**

| | |
|---|---|
| proxy said dead, coverage agrees | **4** — `AttendanceRecordRepository`, `LeaveBalanceRepository`, `TenantCompanyCurrencyLookup`, `UserCompanyAccessRepository` |
| ⚠ proxy said dead, coverage says **LIVE** | **3** — `RoleReadService`, `TenantReadService`, `TenantUserReadService` |
| ⚠⚠ coverage found, proxy **MISSED** | **2** — `EmployeeApproverDirectoryService`, `EmployeePlacementDirectoryService` |

### Why it over-counted: execution through a container is nameless

**All three are registered `AddScoped<ITenantUserReadService, TenantUserReadService>()` and executed
through DI in end-to-end tests. Their names appear in no test file.** ⚠ **A name search cannot see
execution through a container** — the caller names the INTERFACE and the container supplies the type.

### ⚠⚠ Why it under-counted: an architecture test that reads a type's SOURCE makes it look exercised

**Both missed types are named in `tests/` — as STRING LITERALS:**
`"EmployeeApproverDirectoryService.cs"` in a file-reading architecture test, and
`"EmployeeApproverDirectoryService"` in a ban list.

⚠⚠ **A regex over identifiers cannot tell a type reference from a filename in quotes.** **So a test that
reads the type's SOURCE CODE makes it indistinguishable from a test that runs it** — and my census
stripped `//` comments, which caught the comment mention and missed the two literals in the same file.

**That is the mention-census trap in its purest form: the mention was manufactured by a test that reads
the file rather than executing it.**

## What this settles, and what it does not

- ⚠ **The number is 6, and *floor* was the wrong word for 7.** **The proxy is not a bound in either
  direction; it is a different measurement that happens to correlate.**
- **Six types is the answer to the row. No test per type**, and the six are three different modules plus
  Platform.
- ⚠ **This says nothing about whether the six are DEFECTIVE.** Item 236 established the tree carries no
  remaining instance of the ORDER-BY-over-projection class. **These six are unexecuted, which is how two
  live defects survived — not evidence that they contain any.**
- **The instrument is put away as instructed: no gate change, no threshold, no ratio recorded.**

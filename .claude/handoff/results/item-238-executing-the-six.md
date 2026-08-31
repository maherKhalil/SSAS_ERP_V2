
# Item 238 — all six executed, all six clean, and the loop closed with the instrument that found them

**Six tests, six types, ZERO defects. TASK gate: see foot. Integration verified by targeted runs.**

## ⚠⚠ THE RESULT IS CLEAN, AND THAT IS A RESULT

**Nothing was wrong with any of the six.** The row said so in advance: **2 of 2 in one small class is a
reason to LOOK, not a reason to EXPECT.** ⚠ **Six types shown to work is worth what six defects would
have been, and it is cheaper to write down.**

| type | exercised by |
|---|---|
| `EmployeePlacementDirectoryService` | all three interfaces it implements, plus an unknown-employee control |
| `EmployeeApproverDirectoryService` | a department-tree walk to a managed seat, plus an unauthorized-company control |
| `AttendanceRecordRepository` | by id and by employee-period, plus controls on both |
| `LeaveBalanceRepository` | its FOUR-part key, with a control moving each part separately |
| `UserCompanyAccessRepository` | grants for one user, with controls on tenant and user |
| `TenantCompanyCurrencyLookup` | opens the tenant database and answers, with controls on company and tenant |

## ⚠⚠ THE LOOP IS CLOSED BY THE SAME INSTRUMENT THAT OPENED IT

**Re-measured with coverage after the tests were written:**

| type | before (item 237) | after |
|---|---|---|
| `EmployeeApproverDirectoryService` | 0 | **28** |
| the other five | 0 | **2 each** |

⚠ **Measured dead → executed → measured live.** **The same discipline as validating the 236 scanner
against `fa95522^`, applied to my own fix rather than to somebody else's defect.**

### ⚠ AND THE "2 HITS" PATTERN IS A CAVEAT ON ITEM 237 WORTH RECORDING

**Five of six report exactly 2 hits however much ran.** ⚠ **Async method bodies are compiled into
generated state-machine classes, which item 237's noise filter removes as `[<>]` — so a type's own class
entry carries only its constructor and field initialisers.**

**The BINARY is sound**: a type never constructed has 0; a type constructed has >0. ⚠ **The NUMBER is not
a magnitude, and item 237's report should not be read as one.** `EmployeeApproverDirectoryService` reads
28 because it has non-async members; that is the only reason it differs.

**This also explains `GlReadService` reading 2 in item 237 while item 233's test calls eight of its
methods.**

## ⚠ Two things the tests found that were not defects

- **`UX_UserCompanyAccess_TenantId_TenantUserId_CompanyId`** refused my first version: the fixture
  ALREADY grants the normal user `CompanyA`, and granting it again is a duplicate. ⚠ **The uniqueness
  rule doing its job, and the test now reads what the fixture seeded rather than re-seeding it.**
- ⚠⚠ **`PlatformContext` and `TenantDbContextFactory` were ALREADY in the company fixture, both
  PRIVATE.** **Nothing had to be built to reach the last two types — the seam existed and no test had
  walked through it**, which is the same shape as `InternalsVisibleTo("SSAS.Integration.Tests")` sitting
  unused on three infrastructure assemblies.

## ⚠ And one asymmetry inside a single file

**`AttendanceRepositories.cs` declares SIX repositories. Four were executed;
`AttendanceRecordRepository` and `LeaveBalanceRepository` were not.** ⚠ **The cross-implementer
comparison at its smallest scale yet — same file, same shape, same author, and two of six untouched.**

## Scope

- **Six named types, then stop. No coverage was chased**, per the row.
- ⚠ **This says nothing about the other 124 non-query-bearing dead types** — 61 transport contracts and
  the rest — which remain unexecuted and were never in scope.

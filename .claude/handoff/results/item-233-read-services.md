# Item 233 — three read services exercised for the first time, and GL was broken

**Three modules. The Payroll case is `893b874`; GL and Attendance land together.**

## ⚠⚠ FINDING 1 — TWO LIVE PRODUCTION DEFECTS IN `GlReadService`, BOTH FIXED

**Neither query could execute. At all. On every call.**

| method | defect |
|---|---|
| `GetFiscalPeriodsAsync` | `.SelectMany(… => new FiscalPeriodListItem(…)).OrderBy(period => period.StartUtc)` |
| `GetTrialBalanceAsync` | `.Join(… => new TrialBalanceRow(…)).OrderBy(row => row.Code)` |

**Both order by a property of a CLIENT-CONSTRUCTED OBJECT.** EF Core cannot translate an ORDER BY over
one, so each threw `InvalidOperationException: The LINQ expression … could not be translated`.
⚠ **These are 500s for every caller of the GL fiscal-periods read and the trial balance. The trial
balance is a financial report.**

### The enumeration — the class is exactly two

**Every `OrderBy`/`OrderByDescending` in all six module read services: 31 sites.** ⚠ **Only those two order
over a projected DTO.** Every other orders on an ENTITY property or an anonymous type over entities.

⚠⚠ **And the correct pattern was already in the product**: `EmployeeReadService:113` joins into an
anonymous type, orders on `row.employee.FullName` — **the entity** — pages, and projects to the DTO
**last**. Both fixes now match it. **So the divergence was a FEEDBACK gap, not a knowledge gap: nobody
needed telling how, and nothing ever told them it was broken.**

⚠ **`NormalizedCode` is the trial balance's new ordering column, deliberately.** `Account.Code` is a value
object, and a value-converted property is the next translation trap — `NormalizedCode` is the mapped
scalar the account search already orders on.

### ⚠ And the count was not observable until the path ran clean

**The second defect appeared only after the first was fixed** — the test could not reach site 7 until
site 1 executed. **A first-execution test reports where it STOPPED, not how many defects exist.** *Two* is
a claim only because the GL path now completes green and all 31 ordering sites were enumerated.

## ⚠⚠ FINDING 2 — THE STRUCTURAL FACT, WHICH IS THE ENTRY

**The two modules whose read services were never constructed are the two that carried defects.**

| service | constructed by | state |
|---|---|---|
| `EmployeeReadService` | four test files | ✅ correct |
| `DepartmentReadService`, `PositionReadService` | three each | ✅ |
| ⚠ **`GlReadService`** | ⚠ **none** | ⚠⚠ **wrong twice, one a financial report** |
| ⚠ **`PayrollReadService`**, ⚠ **`AttendanceReadService`** | ⚠ **none** | correct, but unproven until today |

⚠ **`InternalsVisibleTo("SSAS.Integration.Tests")` is declared on all three infrastructure assemblies.**
**The seam was opened deliberately and nobody walked through it for two of them.**

## What was built, per module

### Payroll — five predicate sites
`ScopedCompensation` (two methods), `ScopedRuns` (four), and **three hand-written inline copies** in
`GetElementsAsync`, `GetElementAsync`, `GetPeriodsAsync`. **Per site, not per method.**

### GL — seven company-scoped sites, and three tenant-level reads asserted POSITIVELY
`GlReadService` shares no helper at all. ⚠ **`SearchAccountsAsync`, `GetAccountAsync` and the account
lookup inside `GetAccountBalanceAsync` carry NO company predicate, correctly**: `OD-GL-0003` ruled the
chart tenant-level and `Account` has no `CompanyId`. **A scope for company B is asserted to see the SAME
account** — turning *nobody filtered here* into *filtering here is forbidden*, so the next reader does
not fix correct code.

⚠ **`GetAccountBalanceAsync` is the sharp one: the account is tenant-wide and the entries are
company-scoped, so both companies posting 100 must yield a balance of 100, not 200.** *The chart is
shared; the money is not.*

### Attendance — the company predicate and FOUR NAMED CLAUSES

| clause | assertion |
|---|---|
| 1 | with `ViewSensitive`, the sensitive type's code is visible |
| 2 | ⚠ without it, **the ROW is still returned**, code and name null, redaction flag true |
| 3 | ⚠⚠ **the ORDINARY type stays visible IN THE SAME RESPONSE** |
| 4 | ⚠⚠ **the self-service route shows a person their OWN sensitive type** |

⚠ **Clause 3 is the one that separates a discriminating rule from a blanket one** — a service redacting
every row for an unprivileged caller passes clauses 1 and 2 both, and the blanket rule looks *safer*, so
nothing prompts the question.

⚠ **Clause 4 is a RULING, not an oversight**: the party the redaction protects is the subject, and on
that route the subject is the caller. **It is the clause a well-meaning change breaks**, because *redact
unless the caller is an administrator* sounds safer right up to the point where an employee's own sick
leave is a nameless gap in their own list.

⚠ **And Attendance's cause differs from Payroll's and GL's**: its host DOES call
`AddAttendanceInfrastructure` and then registers an explicit stub, last-in-wins. **One symptom, two
causes — a remedy aimed at composition alone would have left a third of the population untouched while
looking complete.**

## The plants — six, each landing on its own assertion

| plant | fails at |
|---|---|
| Payroll `GetElementsAsync` predicate dropped | ⚠ returns company B's element (`Code = BBB`) — the query trusted the argument |
| Payroll `ScopedCompensation` dropped | ⚠ **two compensation rows for one employee** |
| GL `SearchJournalDraftsAsync` dropped | company B's draft leaks |
| Attendance: redact EVERY row | ⚠⚠ **clause 3** — `"AAA-ANN"` |
| Attendance: redact NOTHING | clause 2 — `Assert.Null` |
| Attendance: self-service exemption removed | ⚠ **clause 4** — `"AAA-SICK"` |

## Two obstacles worth recording

- ⚠ **`UX_AttendanceLeaveRequests_Employee_Range_Active` is NOT company-qualified**: one person cannot be
  on leave twice at once whichever company owns the request. **The same employee under both companies is
  what makes the company predicate the only discriminator, so the RANGES move instead of the person.**
- **`ApplyCompanyRulesAsync` refuses a save under a company the context is not authorized for**, so each
  fixture gained a company-parameterised `CreateContext`. **That is the write boundary working, not
  something to route around.**

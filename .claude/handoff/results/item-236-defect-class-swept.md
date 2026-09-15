# Item 236 — the defect class is empty tree-wide, and the census residue is 7, not 41

**No code written. Two instruments built, run, and one of them deliberately NOT committed.**

## ⚠⚠ RESULT 1 — THE DEFECT CLASS HAS ZERO REMAINING INSTANCES

**Scanned all 1041 `.cs` files under `src/` (excluding `obj`, `bin`, migrations): 109 ordering sites.**

**Candidates flagged: 4. Verified by reading: ALL FOUR ARE FALSE POSITIVES, all of one kind.**

| candidate | why it is not the defect |
|---|---|
| `GlReadService:197` (`JournalDetail`) | orders `entry.Lines` **inside** the projection |
| `GlReadService:291` (`JournalDraftDetail`) | same |
| `AttendanceReadService:57` (`WorkingCalendarView`) | orders `calendar.Holidays` **inside** the projection |
| `TenantDatabaseRestoreVerificationFleetReadRepository:98` | orders a **subquery** inside the projection |

⚠ **Ordering a navigation or a subquery INSIDE a projection translates fine — the members are still the
entity's columns.** The defect is ordering **the projected DTO itself**, which is a client-constructed
object.

### ⚠⚠ AND THE INSTRUMENT IS VALIDATED, WHICH IS THE PART THAT MAKES THE ZERO MEAN ANYTHING

**Run against `git show fa95522^:…/GlReadService.cs` — the source as it stood BEFORE tonight's fix — it
flags both real defects:**

```
GlReadService.cs:102  after new FiscalPeriodListItem(  ->  .OrderBy(period => period.StartUtc)
GlReadService.cs:382  after new TrialBalanceRow(       ->  .OrderBy(row => row.Code)
```

**So the scanner HAS been observed to fire for the reason it claims to test.** ⚠ **A zero from an
instrument that has never been seen to fire is worth nothing; this one has.**

## ⚠⚠ AND I AM NOT COMMITTING THE SCANNER AS A GUARD

**Precision on the current tree: 0 true positives, 4 false positives.** On the pre-fix file: 2 of 4.

⚠ **This record deletes guards whose false positives outnumber their true ones, and this one would ship
at 4:0 against a clean tree** — every future reader would meet four failures that are all correct code,
and the natural remedy for a wrong failure is to weaken the guard or delete it.

**Separating *ordering inside a projection lambda* from *ordering the projection result* needs
brace-depth tracking, and I will not ship a fragile parser as a permanent gate on the strength of a
one-off sweep.** ⚠ **The one-off answered the question the row asked; a guard is a different artefact
with a different bar. Say if you want the refined version and I will build it as its own row.**

## RESULT 2 — THE COVERAGE RESIDUE IS 7, NOT 41

⚠ **The 41 was inflated by a construction-syntax blind spot you named yourself: the item 233 fixtures
build all three read services with TARGET-TYPED `new(...)`, which `new X(` cannot see.**

**Recounted generously — a type is SEEN if its simple name appears anywhere in `tests/**/*.cs` outside a
line comment — over the population that matters:**

| | count |
|---|---|
| query-bearing infrastructure types (contain `Set<` or `AsNoTracking`) | **79** |
| ⚠ **named nowhere in `tests/`** | **7** |

`AttendanceRecordRepository`, `LeaveBalanceRepository`, `RoleReadService`, `TenantCompanyCurrencyLookup`,
`TenantReadService`, `TenantUserReadService`, `UserCompanyAccessRepository`.

⚠ **Five of the seven are PLATFORM, not module code** — so the module-shaped instrument that found
tonight's defects points somewhere else entirely for the remainder.

**And the generous test is deliberate: the residue is then a genuine floor rather than an artefact of one
construction syntax.** ⚠ **It still over-counts coverage — a NAME in a test is not an EXERCISE — so 7 is
a lower bound on the untested set, not the untested set.**

## What this does and does not license

- ⚠ **It does NOT say the seven are safe.** It says they carry no instance of the ORDER-BY-over-projection
  class. **A different defect class would need a different sweep, and the only complete instrument for
  "is this code ever executed" is coverage instrumentation, which this repo does not run.**
- ⚠ **No test per type**, per the row. **Seven tests would prove seven types and say nothing about the
  eighth.**
- **`at least N` is not needed here**: the ordering sweep is mechanical and complete over its own
  population, unlike a first-execution run that stops at the first throw.

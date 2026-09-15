# item 198 — the six `codex/*` branches carry nothing that is not already live

**Measurement only. NOTHING PUSHED, NOTHING DELETED.** The repository is public; publication and deletion
are both the owner's.

## The population, re-derived rather than inherited

**38 `codex/*` branches** — 38 local, 25 also on origin, union 38. **The six carrying commits not reachable
from `origin/main` are exactly the six the ruling named, with the same counts**, derived independently:
`chain-test` (4), and one each on `sqlserver-unicode-enforcement`, `post-phase-e-hardening-h1`,
`fp-007-department-analysis`, `fp-005-milestone-1`, `architecture-tenant-storage`.

## ⚠⚠ THE COMPARISON BASE WAS WRONG, AND IT CHANGES THE ANSWER

**`origin/main` is 924 commits BEHIND `origin/ClaudeBranch`.** This team works on `ClaudeBranch`; `main` is
not where work lands. **"Not in `main`" therefore says almost nothing** — it is true of 924 commits of
shipped work.

Measured against `ClaudeBranch`, the *commit counts are identical* — the commits are not ancestors there
either. **But the CONTENT is.** The work was re-applied by another route, so **ancestry reports "unlanded"
about content that shipped.** Reachability was the wrong instrument; content comparison is the right one.

## ⚠ AND MY FIRST CONTENT CHECK WAS ALSO WRONG — IT COMPARED PATHS

Comparing each branch file to the same path on `ClaudeBranch` reported **two files ABSENT** from
`chain-test`, which looked like lost coverage. **Both had simply MOVED:**

| file | on the branch | on `ClaudeBranch` |
|---|---|---|
| `ConstructorKeyedEntityModelTests.cs` | `tests/Integration.Tests/` | ⚠ **`tests/Architecture.Tests/`** |
| `CutoverTenantModelSource.cs` | `tests/Integration.Tests/` | **`tests/TestSupport/SSAS.TestSupport.CutoverModel/`** |

**Both moves are improvements**: the guard now lives in the suite the TASK gate actually runs, and the
model source became a real shared project — which is what its own comment demanded ("from ONE definition").

⚠ **A path-keyed absence check cannot distinguish *deleted* from *moved*.** Re-run by basename: **every
file on all six branches exists somewhere on `ClaudeBranch`.**

## The six, individually

| branch | commit(s) | disposition |
|---|---|---|
| **`chain-test`** | 4, incl. *"the HR→Attendance→Payroll→GL spine, and it is RED on a defect"* | ⚠ **the fixes LANDED** — `ValueGeneratedNever` is present ×8 Payroll, ×7 Attendance, ×2 GL; explicit child deletion is present as `RemoveRange` with its rationale. Both test files moved, not lost. |
| **`sqlserver-unicode-enforcement`** | 1 | the 202-line guard landed and was **improved** — see below |
| **`post-phase-e-hardening-h1`** | 1 | all 7 files present, evolved |
| **`fp-007-department-analysis`** | 1 | docs only — 15 files, all present, evolved |
| **`fp-005-milestone-1`** | 1 | docs only — 9 files, all present |
| **`architecture-tenant-storage`** | 1 | docs only — 6 ADRs, all present |

### The unicode guard landed and got better

Its 4th test looked lost — the branch has
`The_acknowledged_non_unicode_list_contains_nothing_that_has_since_been_fixed`, `ClaudeBranch` does not.
**The bodies are IDENTICAL.** It was renamed to
`Every_acknowledged_column_is_still_found_which_is_what_makes_the_two_bans_above_meaningful` and given a
comment explaining that **the name is what a deleter reads**. Same control, better name.

## ⚠ THE ONE THING ON ANY BRANCH THAT IS NOT IN THE LIVE TREE

**A comment.** `codex/sqlserver-unicode-enforcement` explains why one parameter is deliberately
non-Unicode:

> *"`Char`, not `NVarChar`, and deliberately so… it compares against `msdb.dbo.backupset.type`, a
> Microsoft-owned system column that IS `char(1)`. Matching it avoids an implicit conversion of the column
> side of the predicate."*

**That rationale exists nowhere in the tree** — searched `.cs` and `.md`, whole repository. The live code
has **two** bare `SqlDbType.Char, 1` sites (`SqlServerBackupEvidence.cs:91` and `:156`), each sitting
directly beside `NVarChar` parameters.

**So the product is correct and the reason it is correct is undocumented** — and it is the exact kind of
deliberate exception a future Unicode sweep would "fix" into an implicit conversion. **Not restored: that
is an edit to `src/`, and this item is measurement.**

## What would be lost by deletion

**Of the six: nothing but git history.** No fix, no test, no doc paragraph exists only on a branch. The
sole non-duplicated artefact is the comment above, and it is quoted here in full, so **this file now
carries it.**

**The other 32 `codex/*` branches were not examined individually** — they carry no commit outside
`origin/main`, so by construction they hold nothing.

## ⚠ Scope — what this population EXCLUDES, stated because it is large

- **This repository has 280 branches. I examined `codex/*` — 38 of them.** The rest include ~200+
  `agent/*` branches and others, **none of which were measured**. The same question applies to every one,
  and *"only six of the codex branches carry anything"* says nothing about them.
- **"Carries anything" was defined against `origin/main`**, which this item shows is the wrong base. **A
  branch carrying work absent from `ClaudeBranch` but present in `main` would not appear** in either the
  ruling's six or mine.
- Content equality was judged **per file**, by basename across the tree. A file present under a third name
  with materially different content would read as "differs" — which I resolved by reading only where it
  mattered, not for all 45 files.

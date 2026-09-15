# item 205 — sixteen packages, one false status claim

**Report only.** ⚠ **The zero the ruling pre-authorised did not arrive: FP-015 is false, and it is
FP-014's shape exactly.**

## ⚠⚠ FIRST, THE DENOMINATOR — AND THERE ARE **THREE** FORMATS, NOT TWO

The ruling named table rows and headings. **There is a third: BULLET LISTS.** Counting either named form
alone would have reported **0 criteria for FP-002 (51), FP-007 (52) and FP-008 (68)** — 171 criteria
invisible.

| form | packages |
|---|---|
| **table rows** | FP-012, FP-013, FP-014 |
| **headings** (`### AC-…`) | FP-001, FP-003, FP-004, FP-005, FP-006, FP-015 |
| ⚠ **bullets** (`- **AC-…**`) | FP-002, FP-007, FP-008 |
| **a fourth layout again** | FP-009, FP-011 — identifiers present, none of the three patterns matches |
| no `acceptance-criteria.md` | FP-010, FP-016 |

⚠ **SO I COUNTED SOMETHING FORMAT-BLIND INSTEAD: distinct `AC-[A-Z]+-[0-9]+` identifiers.** Layout cannot
hide an identifier. **607 criteria across the fourteen packages that have the file** — and FP-003 shows 93
headings against **94** identifiers, so even the heading count undercounts its own file by one.

**The layout census is a diagnosis. The identifier count is the measurement.**

## The sixteen status claims

| package | claim (verbatim, abbreviated) | date | verdict |
|---|---|---|---|
| FP-001 | *"backend core — Domain, Application … implemented"* | — | **TRUE** |
| FP-002 | `Approved for Implementation` + Milestone-2 boundary | — | no implementation-status claim |
| FP-003 | *"Tenant backend milestone: Implemented and merged"* … ⚠ *"**HTTP transport: Deferred**"* | — | **TRUE — and precise** |
| FP-004 | `Approved for Implementation` | — | no implementation-status claim |
| FP-005–FP-009 | `Approved for Implementation`, `milestone: Milestone 1` | — | no implementation-status claim |
| FP-010 | `CLOSED — V5 Document Management owns this capability` | 2026-08-23 | **TRUE** — 0 `EmployeeDocument` files in `src/` |
| FP-011 | `APPROVED — decisions closed and ratified` | 2026-08-23 | approval, not implementation |
| FP-012 | `DELIVERED — merged to main by PR #51 (f465c9b)` | 2026-08-24 | **TRUE** — `f465c9b` **is** an ancestor of `origin/main` |
| FP-013 | `DELIVERED — merged to main by PR #52 (f9b247a)` | 2026-08-25 | **TRUE** — likewise |
| FP-014 | `RATIFIED — and PARTLY BUILT. Measured 2026-08-30` | 2026-08-30 | **TRUE** — already corrected by the measurement that found it |
| ⚠ **FP-015** | **`DRAFT — owner decisions unruled; specification only, no code`** | **2026-08-27** | ⚠⚠ **FALSE** |
| FP-016 | *"records a surface that is already built and already pinned by tests"* | — | **TRUE** — 3 `PlatformSupport` source files, **41** test files naming it |

## ⚠⚠ FP-015: TRUE WHEN WRITTEN, FALSE THE NEXT DAY

**Its scope, in its own words:** *"an authenticated identity reading **its own** records, across the
modules that deferred it."*

**Three routes implementing exactly that are live** — `/me/records`, `/me/leave-requests` (Attendance) and
`/me/payslips` (Payroll).

⚠ **`git blame` dates all three to 2026-08-28. The claim is dated 2026-08-27.** **It was true when
written and false the following day** — *"No code and no schema… falsified by its own first migration the
next day"* is FP-014's story, and this is the same story with different nouns.

**And it is not merely code: there are DEDICATED self-service test suites** —
`AttendanceSelfServiceTests`, `PayrollSelfServiceTests` — plus a shared
`SelfServiceContractRule` generalised across modules in T-089.

## Scope
- **Claims were read from `README.md` front matter and the first status block.** A contradicting claim
  deeper in another document of the same package would not appear here.
- **"No implementation-status claim" is an absence claim about a document**, established by reading the
  README's front matter and status paragraphs — not by reading the package end to end.
- `f465c9b` and `f9b247a` were checked with `git merge-base --is-ancestor` against `origin/main`. ⚠ **That
  is the right base HERE, uniquely, because these two claims say "merged to main" explicitly** — everywhere
  else in this repository `origin/main` is 925 commits stale (item 198).

# Item 227 — the eight scope resolvers: three were unasserted, two are closed by shape

**TASK gate green, 0 warnings.** Enumerated by mechanism (`class .*ScopeResolver` under `src/`), which
returns exactly eight.

## The table

| # | resolver | can the caller name a company? | refusal in `src/` | asserted before today | now |
|---|---|---|---|---|---|
| 1 | `AttendanceScopeResolver` | yes | ✅ `AuthorizeAsync` | ⚠ **NO** | ✅ built |
| 2 | `AttendanceSelfServiceScopeResolver` | ⚠ **no** | n/a | **closed by shape — and asserted** | — |
| 3 | `GlScopeResolver` | yes | ✅ `AuthorizeAsync` | ⚠ **empty-set only** | ✅ built |
| 4 | `DepartmentScopeResolver` | yes | ✅ | ✅ `A_company_outside_the_authorized_set_is_refused` | — |
| 5 | `EmployeeScopeResolver` | yes | ✅ | ✅ `A_selected_company_the_user_cannot_reach_is_refused` | — |
| 6 | `PositionScopeResolver` | yes | ✅ | ✅ same name as Department | — |
| 7 | `PayrollScopeResolver` | yes | ✅ | ⚠ **NO** | ✅ built (item 224) |
| 8 | `PayrollSelfServiceScopeResolver` | ⚠ **no** | n/a | **closed by shape — and asserted** | — |

**Six company-scoped resolvers. Three were unasserted. Two self-service resolvers are not subject to the
rule at all.**

## ⚠⚠ THE THIRD ONE — GL — WAS HIDDEN BY A TEST NAME THAT READS LIKE COVERAGE

**`GlEndpointTests` and `GlJournalDraftReadTests` both carry
`A_caller_with_no_authorized_company_is_refused_rather_than_served_an_empty_page`.** ⚠ **Both arrange
`host.CompanyAccess.Permitted = []` — the EMPTY-SET case.**

⚠⚠ **An empty set is *this caller reaches nothing*. An out-of-set company is *this caller reaches
SOMETHING, and not THAT*. The second is what a widening attempt actually produces, and it is the one the
criterion is about.** **`GlScopeErrors.CompanyScopeDenied` was asserted by nothing anywhere in `tests/`.**

**The name is not careless — it describes what that test does exactly.** ⚠ **It is that the mechanism has
two cases and the module asserted the one that does not correspond to an attack.**

## The two self-service resolvers: closed by shape, and NOT a gap

**Both build their scope from `placed.CompanyId`, resolved from the caller's own employee link
(`ResolveEmployeeIdAsync(tenantUserId)`). There is no set and no caller-supplied company — a refusal for
naming the wrong one has nothing to refuse.**

⚠ **And the property that IS their shape is asserted**, in both modules:

- `The_self_route_contract_names_no_employee_on_any_surface` — **the caller cannot name a subject at
  all**, which is the strongest form of *cannot widen*;
- `An_unmapped_caller_is_told_so_rather_than_receiving_a_server_error` /
  `An_unlinked_caller_is_refused_with_the_named_condition` — **fail closed when no link exists**;
- `A_link_naming_an_employee_with_no_placement_is_refused_identically` (Attendance).

**Rows 2 and 8 close as answered, not as exempt.**

## ⚠⚠ AND A SECOND FINDING, IN THE ATTENDANCE RESOLVER — A REFUSAL THAT NAMES THE WRONG GRANT

`ResolveCoreAsync` on the **full** path (`ResolveAsync`, `includeBranches: true`) ends:

```
var scope = AttendanceReadScope.Create(tenantId, companyIds, branchIds);
return scope is null ? Result.Failure(AttendanceScopeErrors.BranchScopeDenied) : ...
```

⚠ **`Create` returns null when EITHER set is empty.** So **a caller with permitted branches and ZERO
permitted companies is refused `BranchScopeDenied`** — and the code's own comment two lines above says
the refusal *"is distinguishable from the company one so an operator can tell which grant is missing."*
⚠⚠ **On this path it is not.** **Constructible: a tenant administrator with branch access and no company
assignment.**

**Not a security defect — it refuses either way.** ⚠ **It is a DIAGNOSTIC one: the operator is sent to
grant a branch when the missing grant is a company.** **Reported, not fixed** — and ⚠ **the new test
deliberately pins the COMPANY-ONLY path, whose answer is correct, rather than asserting the mislabel and
cementing it.**

**Third comment-versus-code mismatch today, after `ApiProblems.cs`'s measurement and `AC-PAY-0022`'s test
name.**

## ⚠ AND THE INSTRUMENT ATE ITS OWN TAIL

**`DepartmentScopeResolver` and `PositionScopeResolver` now appear in
`tests/Payroll.Tests/Reads/PayrollScopeResolverTests.cs` — because I NAMED THEM IN A COMMENT.**

⚠⚠ **A reference-based census counts prose.** *"All eight are referenced by test files"* was already the
weak form of the question, and **my own commit made it weaker by one file, in the direction of looking
better covered.** **The strong form is the one that found the three: does an ASSERTION on that
resolver's own refusal exist — `grep` the error constant, not the type name.**

## What was built

`tests/Finance.Tests/Reads/GlScopeResolverTests.cs` and
`tests/Attendance.Tests/Reads/AttendanceScopeResolverTests.cs` — **four tests each**, mirroring the
Payroll file: out-of-set refused, **in-set permitted (the control)**, empty set refused, and the
authority consulted on every resolution.

**Six plants, six singleton failures.** For each resolver: the check always succeeding reddens **only**
the refusal test; the check always refusing reddens **only** the permit test; a degrading empty set
reddens **only** the empty-set test. ⚠ **Broken-open and broken-shut redden different tests, so neither
guard is merely observed to refuse.**

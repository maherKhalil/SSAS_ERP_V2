# Item 224 — the refusal EXISTS, in two places, and Payroll asserted neither

**Answer: IMPLEMENTED AND UNASSERTED. Not a defect, and the row produced code after all.**

## The question the row asked first: does anything refuse a widened scope?

**Yes. Two mechanisms, both older than this item.**

| clause | mechanism | where |
|---|---|---|
| *a read scope cannot be supplied by the caller* | private constructor, `internal` factory, **and the resolver takes no `ICurrentCompany` at all** | `PayrollReadScope.cs` |
| *a request attempting to widen its own scope is refused* | `AuthorizeAsync` → `CompanyScopeDenied` when the company is not in the live permitted set | `PayrollScopeResolver` |

⚠ **And a third, upstream and generic:** a caller-supplied company identifier is INTENT only
(`ICompanySelection`) and becomes visible as `ICurrentCompany.CompanyId` **only after the five-step live
validation in `ICompanyContextResolver`** — exists, belongs to the trusted tenant, is Active, **and the
caller is currently authorized for it.** A widened selection never reaches a payroll route with a company
attached; `SearchElementsAsync` then refuses on `CompanyId is not { }`.

⚠ **The read query is belt-and-braces and it is worth naming, because it is NOT the refusal.**
`PayrollReadService.GetElementsAsync` applies **both** `scope.CompanyIds.Contains(element.CompanyId)`
**and** `element.CompanyId == companyId`. **Taken alone that is a NARROWING to the empty set — an empty
200, not a refusal** — which is precisely what `PayrollReadScope`'s own comment argues against. **It is
not load-bearing here only because the widened company cannot get past the context resolver in the first
place.**

## ⚠⚠ SO THE FINDING IS NOT THE BEHAVIOUR. IT IS THAT NO PAYROLL TEST TOUCHED IT.

**Searched by mechanism, not by name:**

| module | test |
|---|---|
| HR | `DepartmentScopeResolverTests.A_company_outside_the_authorized_set_is_refused` |
| HR | `PositionScopeResolverTests.A_company_outside_the_authorized_set_is_refused` |
| GL | `GlEndpointTests.A_caller_with_no_authorized_company_is_refused_rather_than_served_an_empty_page` |
| GL | `GlJournalDraftReadTests.` — same name, second surface |
| **Payroll** | ⚠⚠ **no scope-resolver test file existed at all** |

⚠⚠ **Four tests across two modules for one mechanism, and none for the module whose own scope type says:
*"everywhere else a forgeable scope is an authorization defect; for compensation it is a personal-data
breach."*** **The module that argues hardest about why this matters is the one that asserted none of it.**

**That is the seventh instance of implemented-and-unasserted in this sweep, and the first where the gap
is visible as a MISSING FILE rather than a missing assertion** — which is why no citation search found
it: **there was nothing to read.**

## What was built

`tests/Payroll.Tests/Reads/PayrollScopeResolverTests.cs` — **six tests, four cited to `AC-PAY-0028`.**

| test | clause |
|---|---|
| `A_company_outside_the_authorized_set_is_refused` | ⚠ clause 2, the criterion verbatim |
| `A_company_inside_the_authorized_set_is_permitted` | ⚠ **the control** |
| `An_empty_authorized_company_set_is_refused_rather_than_served_as_an_empty_page` | the type's own claim |
| `The_resolver_cannot_see_the_callers_company_selection` | clause 1, structurally |
| `The_company_authority_is_consulted_on_every_resolution` | live re-ask, not cached |
| `Tenant_administration_alone_reads_no_compensation` | the two axes are independent |

⚠ **The permission is HELD in the refusal test**, so the refusal can only come from the company
dimension. **Without that it would pass on a `WritePermissionDenied` and prove nothing about scope.**

## The plants — ⚠ TWO-SIDED, AND ONE OF THEM FAILED TO LAND FIRST TIME

| plant | result |
|---|---|
| the company check always succeeds | ⚠ **only `..._outside_..._is_refused` reddens** |
| the company check always refuses | ⚠ **only `..._inside_..._is_permitted` reddens** |
| an empty set degrades to a non-empty one | ⚠ **only the empty-set test reddens** |

**Broken-open and broken-shut redden different tests. A guard never observed to permit anything is
indistinguishable from one that is broken shut, and only the pair separates them.**

## ⚠⚠ AND THE NEAR-MISS, WHICH IS THE PART WORTH KEEPING

**The first three plant attempts printed `ABORT: anchor matched 0` — and the test run after each one
reported `Passed! 6`.** ⚠ **Three consecutive green runs against UNPLANTED code, each one reading
exactly like "the plant did not redden it".**

**Cause:** the plant script compared `\n` anchors against a **CRLF** file. **Single-line anchors had
worked all session; the first multi-line anchor silently matched nothing.**

⚠ **What caught it was that the script FAILS LOUDLY on a match count other than one, and that I read its
output before the test output.** **A plant script that had used `replace()` and shrugged would have
produced three green runs and a false conclusion that the assertions were vacuous** — the exact inverse
of the stale-binary green, and just as quiet. **The runner now normalises endings and refuses a mixed
file.**

## Not built, and why

**No plant for `The_resolver_cannot_see_the_callers_company_selection`.** Planting it means adding an
`ICurrentCompany` parameter to the constructor, which breaks the test's own helper — ⚠ **a plant that
does not compile is void, not passing.** **Its anti-vacuity control is inside the test instead:** the
negative `DoesNotContain` is paired with a positive `Contains("CompanyAccessResolver")`, **so reflection
returning nothing fails the test rather than passing it silently.**

## `AC-PAY-0028` is now fully pinned

**Both clauses, four citations, and the first `Criterion` traits in `tests/Payroll.Tests/Reads/`.**

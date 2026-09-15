# Item 229 — the code now keeps the promise the comment made

**TASK gate green, 0 warnings. Attendance 85 → 87.**

## The fix, and why it is four lines rather than a restructure

`AttendanceReadScope.Create` returns null when **either** set is empty, and `ResolveCoreAsync` labelled
every null `BranchScopeDenied`. The label now follows the input:

```
return scope is null
  ? Result.Failure<AttendanceReadScope>(companyIds.Length == 0
    ? AttendanceScopeErrors.CompanyScopeDenied
    : AttendanceScopeErrors.BranchScopeDenied)
  : Result.Success(scope);
```

⚠ **The empty checks stay in the factory, which is where the GUARANTEE lives.** This decides only the
LABEL — **the alternative, an early return in the resolver, would have moved a policy that the factory's
own comment says belongs there** *("the empty checks live here rather than in the resolver so they hold
for every future caller")*.

⚠ **And `companyIds.Length == 0` is an EXACT discriminator, not a guess at which check fired:**
`AuthorizedCompanySet.Create` returns null **for a null-or-empty list and nothing else**. I read it rather
than assuming — a factory that also rejected duplicates or empty Guids would have made this line wrong in
a way no test I wrote would catch.

**No disclosure change**, as the row required: an operator learns which grant CLASS is missing, never
which grant. Neither refusal names a company, a branch, a tenant or any topology.

## ⚠⚠ THE PAIR IS THE TEST, NOT EITHER HALF

**A resolver that answered `CompanyScopeDenied` for every null satisfies the company test perfectly.**
Both empty sets now run through the **same** path:

| test | arrangement | expects |
|---|---|---|
| `An_empty_company_set_on_the_full_path_names_the_company_grant` | companies `[]`, branches `[A]` | `CompanyScopeDenied` |
| `An_empty_branch_set_on_the_full_path_names_the_branch_grant` | companies `[A]`, branches `[]` | `BranchScopeDenied` |

**The `StubBranchAccess` is now parameterised for it** — it previously always returned one branch, so the
branch-empty case was unreachable from the test file at all.

### The plants are two-sided, and the first one is the defect itself

| plant | reddens |
|---|---|
| ⚠ **the original defect restored** — every null is `BranchScopeDenied` | **only the company test** |
| inverted — every null is `CompanyScopeDenied` | **only the branch test** |

**The first plant is the regression proof: it puts the shipped defect back and the new test catches it.**

## ⚠⚠ AND THE PLANT SCRIPT SAVED THE FIX ITSELF

**After plant 1, `git checkout -- <file>` restored the file to HEAD — which discarded MY UNCOMMITTED FIX
along with the plant.** ⚠ **Plant 2 then aborted with `anchor matched 0`, and that abort is the only
reason I noticed.** Without it I would have run plant 2 against original code, drawn a conclusion from
it, **and committed a result file describing a fix that was no longer in the tree.**

⚠ **The rule was already written down — *stage before planting* — and I applied it to test files this
session and not to the `src/` fix.** **Restoring a plant is `git checkout-index -f --`, which restores
from the INDEX; `git checkout --` restores from HEAD and takes everything unstaged with it.**
See [[the-git-index-is-shared]].

## Scope

- ⚠ **Checked the sibling resolvers for the same latent shape: neither has it.** `PayrollReadScope` and
  `GlReadScope` take only a company set, so their single null has a single cause. **The mislabel needed
  two dimensions collapsing into one factory result, and Attendance is the only resolver with two.**
- **The comment the row told me to keep is kept**, and the new note beside the fix says what changed and
  why the factory keeps the checks.

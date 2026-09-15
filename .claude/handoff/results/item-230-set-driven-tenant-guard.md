# Item 230 — the feasibility answer is YES, and the test exists

**The row asked one question: can each type be brought to a `Modified` state with a moved `TenantId`
without hand-construction? ⚠ YES — and it needs no database, so it runs in every gate.**

## How

| step | mechanism |
|---|---|
| enumerate | `context.Model.GetEntityTypes()`, not owned, CLR type implements `ITenantOwnedEntity` |
| instantiate | ⚠ **`RuntimeHelpers.GetUninitializedObject(type)`** — no constructor, no factory, no per-type knowledge |
| track | `context.Entry(entity).State = EntityState.Unchanged`, then move `TenantId` and mark the property modified |
| judge | `SaveChangesAsync` must throw `InvalidOperationException` **whose message names tenant ownership** |

**The aggregates assign keys and invariants in factories with different signatures, which is what made a
grid look unavoidable. ⚠ The guard does not care: it inspects a tracked ENTRY, not a valid aggregate**,
so an uninitialized object is a sufficient subject and the per-type knowledge disappears.

## ⚠⚠ WHY IT RUNS AGAINST `PersistenceDbContext` AND NOT `TenantDbContext`

**The guard lives in `PersistenceDbContext.ApplyPersistenceRules`.** `TenantDbContext.SaveChangesAsync`
runs `PreventCompanyDeletion`, `PreventAppendOnlyMutation` and `ApplyCompanyRulesAsync` **before**
`base.SaveChangesAsync` — and most of these types are also `ICompanyOwnedEntity` or `IAppendOnlyEntity`.

⚠ **Through `TenantDbContext` an earlier boundary refuses first and the tenant guard is never reached.**
**That is the trap item 228 hit on its first run**, and at 35 types it would have hit it silently for
most of them. **A test-only context on the base class puts the rule under test alone on the path.**

## The two controls

- ⚠ **Anti-vacuity floor**: the selection is four links long and the offender list is empty if any link
  stops matching. **Measured: 35 types. Floor set at 30.**
- ⚠ **Plant**: `entry.Entity.GetType().Name != "PayrollRunLine"` — one type excluded from the shared
  guard. **The test fails and NAMES it: `PayrollRunLine -> SqlException`.** ⚠ **`SqlException` is the
  tell — the guard did not fire, so the save proceeded to the unusable connection.** **The message
  assertion is what turns that into a failure instead of a pass.**

## ⚠ COVERAGE IS 35 OF THE 42 DECLARING TYPES, AND THE SEVEN ARE NAMED

**The composed TENANT model only.** The rest live in Platform's own context — `Role`,
`RolePermissionAssignment`, `TenantUser`, `TenantUserRoleAssignment` and the three localization types —
**a separate model this does not build**, the same bound `ConstructorKeyedEntityModelTests` states for
itself.

⚠ **`TenantUser` is therefore NOT covered here and keeps its own named test.** **Saying which seven is
the point: 35 reads like 42 unless the gap is named.**

## What this replaces

**The residual risk from item 228 — *the 43rd type added tomorrow* — is closed for the tenant model.** A
new tenant-owned entity is judged **on the day it joins the model**, with no one having to remember.

⚠ **And the alternative I offered and you refused was the right thing to refuse:** a source-shaped ban on
type discrimination would have had unbounded false positives, and **this catches the same plant by
BEHAVIOUR rather than by pattern.**

**No accepted-risk note is needed for the tenant model. If the Platform context's seven are worth the
same treatment, that is a second, small, separate item — and it is a ruling, not a test I should pick.**

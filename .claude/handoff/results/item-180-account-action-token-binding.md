# item 180 — what the `AccountActionToken` index rests on

**Comment-only `src/` change.** Two cross-citations added; no behaviour altered.

## ⚠ (a) The invariant is not pinned by a test — it is pinned by the TYPE SYSTEM, which is stronger

The dispatch expected one of two answers: a test pins the binding, or nothing does and I should pin it.
**Neither is right.**

`AccountActionToken` has exactly two public entry points:

| factory | tenant parameters |
|---|---|
| `CreateInvitation(…, Guid tenantId, long tenantUserId, …)` | **non-nullable** — both must be supplied |
| `CreatePasswordReset(…)` | **absent** — neither can be supplied |

There is no public rehydrate, restore or update. The only other constructors are private: the validating
one, and a parameterless one for EF materialisation.

**So a mixed binding — `TenantUserId` set with `TenantId` null — is not EXPRESSIBLE through the public
API.** A test asserting "a mixed binding throws" cannot be written without reflection, because no caller
can produce one. The private constructor's `ArgumentException` is a **backstop for a path only EF and those
two factories use**, and it is unreachable from outside.

**Nothing was pinned, because the compiler already pins it more tightly than a test would.** A test would
assert a runtime rejection; the signatures make the input unconstructible.

⚠ **What would break it is widening either parameter to a nullable type** — a deliberate signature change,
which the compiler forces you to write out.

**Residual, stated rather than dismissed:** EF materialises through the parameterless constructor and
private setters, so a row **already in the database** with a mixed binding would load without complaint.
No migration or seed inserts `AccountActionToken` rows, so no such row can have been written by this
product — but the index's safety is a property of the write path, not of the table.

## (b) Both citations added

- **At the index** (`AccountActionTokenConfiguration`): why `TenantId` being nullable and unmentioned is
  safe, that the guarantee is the factory signatures rather than a test, and what would break it.
- **At the invariant** (`AccountActionToken`): that this binding holds up a unique index, which index, and
  what happens if it is widened.

## ⚠ (c) This is the weakest remedy available, and the chain is shorter than expected

The dispatch assumed the chain *test reddens → comment redirects*. **There is no test in the chain.** It is
**compiler reddens → comment redirects** — and the compiler only reddens on the signature change itself,
not on anything downstream.

**So the comment is doing more work here than a comment should.** Nothing mechanical connects the index to
the binding.

### A cheap way to make it mechanical — reported, not built

**An architecture test asserting that `CreateInvitation`'s `tenantId` and `tenantUserId` parameters are
non-nullable value types.** Reflection over `MethodInfo.GetParameters()`, no T-SQL, no parsing, no
hand-written list — and it reddens **exactly** when the property the index depends on is weakened, with a
message that can name the index.

It does not make the *index* redden; it makes the *dependency* checkable, which is the reachable half.
**Not built, as ruled.**

## (d) The `LeaveRequest` literal — inconsistency, not a defect

`[Status] IN ('Submitted', 'Approved')` where every Platform filter uses `N'…'`.

- **The column is `nvarchar`.** `Status` is persisted `HasConversion<string>()`, and
  `UnicodeStringPersistenceArchitectureTests.Every_persisted_string_in_the_tenant_model_is_unicode`
  asserts every persisted string in that model is Unicode.
- **The behaviour is unchanged.** A `varchar` literal compared to an `nvarchar` column is implicitly
  widened — `nvarchar` has the higher precedence — so the filter evaluates identically and uniqueness is
  enforced exactly as intended.
- **The literal form is not arbitrary.** The declaration's own comment records why it is strings and not
  ordinals: *"SQL Server refuses a filtered index whose predicate compares a string column to integer
  constants… The first attempt used `IN (0, 1)` and every Attendance integration test failed at catalog
  creation."*

**So: cosmetic inconsistency with the Platform filters, no behavioural difference.**

## Scope

- **"Not expressible" is a claim about the public API surface**, enumerated from the type's members. A
  caller using reflection, or a future `internal` accessor, is outside it.
- The residual about pre-existing rows is reasoned from the absence of seeds and migrations that insert
  them, not from inspecting a database.

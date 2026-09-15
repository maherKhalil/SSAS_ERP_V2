---
package: FP-015
title: Self Service — Authorization Model
status: DRAFT — established by T-071 measurement, verified independently 2026-08-27
version: 0.1
date: 2026-08-27
---

# Authorization Model — FP-015

**Every claim in this file is measured from the tree, not designed for it.** T-071 established the
cost of a self permission with file and line; the architect re-verified the load-bearing claims
independently before writing this. Where the measurement contradicted this package's own drafts, the
measurement won and the contradiction is recorded rather than smoothed over.

---

## ⚠ AMENDMENT 2026-08-30 — FOUR OF THIS FILE'S FINDINGS HAVE BEEN OVERTAKEN. READ THIS FIRST.

This document was written 2026-08-27 and landed **384 commits later**. Re-verified against the tree
before landing. **The sections describing the permission MODEL are still exactly right — §1, §3, §4 and
§6 were re-checked line by line and every citation still resolves.** The sections describing what was
MISSING are the ones the intervening work closed, and they are the ones this file leads with.

**§5 — the gate it relies on no longer exists.** It cites
`AttendanceArchitectureTests.cs:287`, `No_self_service_permission_is_declared_…`, and says an Attendance
self permission *"fails this by construction, whatever it is called"*. **That test was amended, and
properly**: the file now records at line 281 what it used to assert and why that stopped being correct,
and asserts at line 319 the expected set `["Attendance.Leave.ViewOwn", "Attendance.Records.ViewOwn"]`.
**The gate is open. This document is the only place still describing it as shut.**

**§7 — its own "named as unestablished" claim was wrong when written.** It reports that `ADR-030`'s
mapping *"appears unimplemented"* on the strength of a search for `EmployeeLink`, `IdentityEmployee`,
`EmployeeIdentity` and `TenantUserEmployee` returning zero files — **and states the limit honestly, that
it matched concept names rather than reading ADR-030's chosen schema.** The mapping exists. It is
`IUserEmployeeResolver`, in `src/BuildingBlocks/SSAS.BuildingBlocks.Tenancy/`. **The stated limitation is
precisely why the search missed it.**

⚠ **§5 and §7 are one failure, not two: §5's conclusion rested on §7's absence, and the absence was never
real.** This file named its own weakest link in the right place, and then drew the conclusion anyway. **A
caveat that does not change what you conclude is decoration.**

**§8 — the gap it warns about has been shut, in both places it names.** Its table's third row reads
*"the permission a route requires is one the catalog defines — **nowhere**"*; that join is now
`tests/API.Tests/Infrastructure/EndpointPermissionCatalogJoinTests.cs`, whose header opens *"THE JOIN
NOTHING ASSERTED"* and cites the same FP-006P incident. Row 1's exception — *"Attendance has no route
inventory test at all"* — is closed by `AttendanceRouteInventoryTests.cs`. **So §8's conclusion, that
FP-015 is the package most exposed to this gap, describes a gap that no longer exists.**

**§2 was prospective and is now past tense.** All three permissions exist —
`Attendance.Records.ViewOwn`, `Attendance.Leave.ViewOwn`, `Payroll.Payslips.ViewOwn` — and **all three
are wired to live routes**, which also settles §7's *"the cost of a self-service endpoint is not
established here"* by construction. **None appears in any seeded role**, which is the part still open and
is `OWNER-DECISIONS.md` entry 4.

⚠ **The drift here is not decay: every cited line number still resolved 384 commits later, and eight were
spot-checked.** What changed is that four things this file named as ABSENT have since been built —
**which is the better failure, and the reason §§1, 3, 4 and 6 are usable as they stand.**

## 1. The permission grammar has a hard three-segment ceiling

```
PermissionName.cs:36    segments.Length == 3 && segments.All(IsIdentifierSegment)
```

**Not a convention — an equality inside `PermissionName.Create`**, whose constructor is private
(`:7`) and whose only construction site is inside `Create` itself (`:17`). There is no second door.

A contributed name that fails `Create` makes `ComposedPermissionCatalog.cs:106-112` throw and refuse
the **whole composition**, so the application does not start. **This is asserted, not inferred:**

```
ComposedPermissionCatalogTests.cs:143    [InlineData("Far.Too.Many.Segments")]
```

A four-segment contributed name is already a test case for *refuses the composition*.

Five further independent assertions of three segments exist across four files
(`PayrollArchitectureTests.cs:168`, `AttendanceArchitectureTests.cs:312`, `GlArchitectureTests.cs:175`,
`PermissionCatalogTests.cs:72` and `:84`).

### The consequence for this package, stated plainly

**`OD-SS-0001` expressed its ruling as `payroll.payslip.view.self`. That string cannot exist.**

**The ruling's substance is untouched** — a distinct permission, not a scope, remains exactly what was
ruled and exactly what this package builds. **Only the spelling was wrong, and it was wrong in one
document rather than in the product's vocabulary:**

- `PayrollPermissionNames.cs:68-72`, written before FP-015 existed, already names the alternative:
  *"Adding a `Payroll.Payslips.ViewOwn` on an unverified assumption is exactly the shape of the FP-011
  near-miss."*
- `REQ-ATT-0023` / `AC-ATT-0032` assert that **`ViewOwn`** does not exist — the requirement used the
  same word.

**The requirement and the code were already speaking one vocabulary, and it was never the ruling's.**
The four-segment form appeared in `OD-SS-0001` and nowhere else in the repository.

---

## 2. The permissions this package defines

| Permission | Module | Administrative counterpart |
|---|---|---|
| `Payroll.Payslips.ViewOwn` | Payroll | `Payroll.Payslips.View` |
| `Attendance.Records.ViewOwn` | Attendance | `Attendance.Records.View` |
| `Attendance.Leave.ViewOwn` | Attendance | `Attendance.Leave.View` |

### Three, not two — and this is a finding, not a drafting choice

This package's drafts wrote **one** attendance permission. **The administrative surface splits
attendance records from leave into two separate permissions** (`AttendancePermissionNames.cs:22` and
`:45`), and a self surface that collapsed them would grant leave visibility to anyone granted
timesheet visibility.

**The self plane inherits the administrative plane's divisions.** It is not free to be coarser: a
coarser self permission is a widening disguised as a simplification.

---

## 3. Scope — an ordinary tenant-assignable permission, and no other option is representable

```
PermissionScope.cs:3-7                 enum { Tenant = 1, PlatformSupport = 2 }
ModulePermissionDefinition.cs:22       record (string Name, string Description)   — NO scope property
ComposedPermissionCatalog.cs:131-135   stamps PermissionScope.Tenant
```

`ModulePermissionDefinition` carries its own reason for having no scope: *"A `Scope` property here
would be a field a future module could set to `PlatformSupport` and a reviewer would have to notice;
with no property there is nothing to review, and the escalation cannot be expressed."*

**A business module cannot ask for any scope.** The composer stamps `Tenant` unconditionally.

**`PlatformSupport` is the opposite of what self-service needs** — cross-tenant operator authority,
refused outright by `Role.cs:150-153` and stripped from tenant tokens by
`TenantPermissionClaimFilter.cs:27-28`. A self permission must be tenant-assignable to reach an
employee at all.

**So the answer to "is `.self` a scope?" is settled from code: there is no second axis.** A self
permission is an ordinary tenant-assignable permission whose **action segment** says `Own`. Making it
a scope would require a third enum value *plus* the exact property that type was deliberately written
without.

---

## 4. Nothing makes the administrative implication by accident

**`AC-SS-0006` rests on this answer, so it is stated explicitly rather than assumed.** Six places the
implication could have lived, each checked:

1. **Evaluation is ordinal string equality**, on both planes —
   `PermissionAuthorizationHandler.cs:19-21`, `PlatformPermissionAuthorizationHandler.cs:29-31`. No
   prefix comparison, no wildcard.
2. **No hierarchy type exists to hold an implication.** `PermissionDefinition.cs:6` is
   `(Name, Scope, Description)` — no parent, no `Implies`, no group. A permission has nowhere to
   record that it entails another.
3. **Grants are stored and read per exact name.** `Role.cs:160` carries one name per assignment;
   `AccessTokenClaimsProvider.cs:73-80` selects those names straight into claims. **There is no
   expansion step between grant and claim.**
4. **No seed bundles anything, because there is no seed.** `Role.CreateSystem` (`Role.cs:73`) is
   called nowhere in `src/` — only from two tests — and a system role refuses every permission
   assignment regardless (`Role.cs:140-142`).
5. **Catalog lookup is exact and asserted** — `PermissionCatalogTests.cs:54-60` and
   `ComposedPermissionCatalogTests.cs:185` (`Contributed_lookup_is_exact_and_ordinal`).
6. **The one thing that looks like a prefix rule is not one.** `PermissionAuthorizationDefaults.cs:24`
   and `PermissionAuthorizationPolicyProvider.cs:14-24` call `StartsWith` — **on ASP.NET policy names,
   stripping the policy namespace to recover a permission.** No permission is ever compared to another
   permission. Recorded because it is the only prefix logic in the authorization stack and misreading
   it yields the opposite conclusion.

**`AC-SS-0005`, the reverse direction, holds by the same six lines.** The model is symmetric because
it does nothing whatever beyond an exact match.

**`Payroll.Payslips.ViewOwn` and `Payroll.Payslips.View` share a prefix and share nothing else.**
The resemblance is for humans reading a role screen; the authorization stack cannot see it.

---

## 5. What each module costs, and the two are not the same

**Definition is two files for a business module** — a `const string` in the module's
`…PermissionNames`, and a `ModulePermissionDefinition` in its catalog contributor. The Host needs
nothing: `Program.cs:100` registers each contributor once, and a new permission inside an
already-registered contributor is invisible to composition wiring.

**Payroll costs exactly that.** No guard objects; `Payroll.Payslips.ViewOwn` satisfies the three shape
guards automatically.

**Attendance costs that plus taking down a deliberate assertion:**

```
AttendanceArchitectureTests.cs:287
  No_self_service_permission_is_declared_because_the_subject_cannot_be_resolved
    Assert.DoesNotContain(constants, name => contains "Own" || "Self" || "Mine")   // case-insensitive
```

**An Attendance self permission fails this by construction, whatever it is called.**

**The guard states its own reason** — self-service *"depends on a mapping from the authenticated
identity to an employee record, and this build does not assert such a mapping exists — verified, not
assumed."*

**Amending it is legitimate once the mapping exists, and only then.** The difference between a
specification change and a weakening is whether `ADR-030`'s mapping is implemented at the moment the
assertion comes down. **It is not, as far as anyone has established** (§7). **This guard is therefore
the gate on FP-015's Attendance half, and it is doing its job.**

---

## 6. Who grants it — and the gap that is not this package's to fill

**The assignment path and every gate on it:**

```
AssignPermissionToRoleCommandHandler.cs:28-31   catalog must define the name  -> InvalidPermission
Role.cs:140-142                                 RoleType.System               -> ProtectedSystemRole
Role.cs:145-148                                 Status != Active              -> RoleNotAssignable
Role.cs:150-153                                 Scope != Tenant               -> PlatformPermissionRejected
Role.cs:155-158                                 already assigned              -> DuplicatePermissionAssignment
```

A user then holds it by role — `AccessTokenClaimsProvider.cs:57-95`.

**A tenant grants self-service by: create an Active `Custom` role, assign the permission, assign the
role to a user.** Per user.

### There is no mechanism to grant it to every employee

**No seeding of any kind exists.** No system role is created by the product; no default role exists;
there is no bulk assignment and no all-users grant.

**So "grant every employee self-service" is N individual role assignments.** `AC-SS-0006`'s own
phrasing describes an act the product cannot perform in one step.

**That gap is a mechanism, not a permission, and it is recorded rather than designed here.** FP-015
defines the permissions; the absence of a bulk-grant path is a Platform concern that predates this
package and outlives it. **It does not block FP-015** — a tenant can grant the permission, one
assignment at a time — but a self-service feature whose rollout is O(headcount) manual steps is a
feature whose adoption will be blamed on the feature.

---

## 7. Named as unestablished

- **`ADR-030`'s mapping appears unimplemented.** A search for `EmployeeLink`, `IdentityEmployee`,
  `EmployeeIdentity` and `TenantUserEmployee` across `src/` returns zero files. **The limit of that
  search is that it matched concept names rather than reading `ADR-030`'s chosen schema.** So: *not
  found by name*, which is weaker than proven absent. **Everything in §5 about the Attendance guard's
  reason still holding rests on this, and it is the weakest link in this document.**
- **No exhaustive audit of every module guard** a self-service endpoint might trip. The four
  `named == contributed` guards and the three-segment guards are confirmed for HR, GL, Payroll and
  Attendance; unrelated guards were not swept.
- **The cost of a self-service ENDPOINT is not established here** — routing, handler, read scope, and
  the `AC-SS-0007` no-identifier contract. This document covers the permission only.

---

## 8. The risk this package must not walk into

**Two grammars disagree about segment count.**

```
PermissionName.cs:36                 exactly 3 segments        catalog side
AuthorizationNameValidator.cs:5-44   ANY count >= 1            endpoint side
```

`PermissionRequirement` validates with the second. So `PermissionRequirement("payroll.payslip.view.self")`
**constructs successfully**, and an endpoint can require a permission name the catalog can never define.

**The chain that should catch this has three links and the middle join is missing:**

| Asserted | Where | Scope |
|---|---|---|
| routes require the permissions the **inventory** names | `PayrollRouteInventoryTests.cs:74-84` | HR, GL, Payroll — **not Attendance** |
| the **contributor's** names are in the composed catalog | `EmployeeHostCompositionTests.cs` H11 | **HR only** |
| the permission a **route requires** is one the **catalog defines** | **nowhere** | — |

**The inventory and the endpoint can agree with each other while both disagree with the catalog.**
The failure is not a red build: it is **403 to every caller, forever**, and no test travels that path
because tests mint permission claims directly rather than going through assignment.

**That is FP-006P's incident verbatim** — HR's constants existed, no catalog defined them, every
Employee endpoint refused every caller, and every test passed.

**Both modules FP-015 touches sit outside the surviving guards:** Attendance has no route inventory
test at all, and H11 covers HR only. **FP-015 is the package most exposed to this gap and the one
that found it.** Closing it is T-072's subject and is a prerequisite to this package's endpoints, not
a follow-up to them.

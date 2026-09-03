using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.IdentityAccess;

// ⚠⚠ `AC-IAM-0023` IS UNCOVERED AS A UNIVERSAL, AND THIS FILE IS WHY IT LOOKS COVERED.
//
// *"EVERY security-sensitive change records UTC timestamp and authenticated actor."* Every mutating call
// below passes `"actor"`, a `Guid` event id and `Now` — `AssignPermission`, `AssignRole`, `RemoveRole`,
// `Deactivate`, `Retire`. **So the parameters are exercised everywhere and the UNIVERSAL is asserted
// nowhere.** No test here reads back a stamped timestamp or actor, and none establishes that a mutation
// which took neither could not exist.
//
// ⚠ SAME SHAPE AS `AC-IAM-0024` (*no secret in ANY log*), which is recorded in `AuthenticationSecurity
// Tests`: **a universal over an open population — every security-sensitive change that exists or will.**
// A behavioural test can only reach the mutations it calls, so the instrument that fits is STRUCTURAL —
// *every mutating domain method takes an actor and a timestamp*, checkable by reflection over method
// signatures rather than by driving them.
//
// Searched for that guard by mechanism across `Architecture.Tests`: `OccurredUtc|IDomainEvent|ActorId`
// matches 26 files, none asserting an audit-stamp universal, and `PersistenceArchitectureTests` carries
// no audit-field assertion at all — though `PersistenceDbContext.ApplyPersistenceRules` is where the
// stamping actually happens. **The mechanism exists and has no guard over it.**
//
// Recorded rather than built, for the reason `AC-IAM-0024` was: the exemptions are the expensive part —
// a reflection sweep demanding an actor parameter would redden every legitimate parameterless transition
// — and enumerating them is a separate item.
public sealed class IdentityAccessDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public void Email_and_role_name_preserve_trimmed_display_and_normalize_with_uppercase_invariant()
  {
    var email = EmailAddress.Create("  Jane.Doe@Example.com ").Value;
    var roleName = RoleName.Create("  Payroll administrator  ").Value;

    Assert.Equal("Jane.Doe@Example.com", email.Value);
    Assert.Equal("JANE.DOE@EXAMPLE.COM", email.NormalizedEmail);
    Assert.Equal("Payroll administrator", roleName.Value);
    Assert.Equal("PAYROLL ADMINISTRATOR", roleName.NormalizedRoleName);
  }

  [Theory]
  [InlineData("Platform.Users.View", true)]
  [InlineData("platform.Users.View", true)]
  [InlineData("Platform.Users", false)]
  [InlineData("Platform.Users.View.Extra", false)]
  [InlineData("Platform.Users.view-users", false)]
  [InlineData(" Platform.Users.View", false)]
  public void Permission_name_requires_exact_three_segment_identifier_format(string value, bool valid)
  {
    Assert.Equal(valid, PermissionName.Create(value).IsSuccess);
  }

  // ⚠ THE NEXT THREE TESTS CITE `AC-IAM-0004` — *"A tenant role … does not grant platform-support
  // access"* — AT THE ASSIGNMENT LAYER, WHICH IS THE STRONGEST OF ITS THREE. The criterion is enforced in
  // three places and each proves something the others cannot:
  //
  //   ASSIGNMENT  (here)      a tenant role cannot ACQUIRE a PlatformSupport permission at all
  //   ISSUANCE    (arch)      `PlatformPlaneAuthorizationArchitectureTests` — one that got assigned anyway,
  //                           by corruption or a force-seeded row, cannot reach a tenant token
  //   NAMING      (:181)      `Administrator_role_name_does_not_imply_permissions` — the name confers none
  //
  // **The issuance layer's own source calls itself *defence in depth*, which is only true because THIS
  // layer is the primary one.** A reader who found only the filter test would think the rule lived at token
  // issuance; a reader who found only these would think a corrupt row could still leak.
  //
  // ⚠⚠ THE ANTI-VACUITY CONTROL FOR THIS GROUP IS `Custom_tenant_role_still_accepts_a_tenant_scoped_
  // permission`, and the reason is written THERE rather than repeated here — see its note.
  //
  // ⚠⚠⚠ NOTE THE SECOND AND THIRD TESTS TAKE THEIR PERMISSIONS FROM THE REAL CATALOG AND ASSERT THE SCOPE
  // THEY EXPECT (`:62`) BEFORE USING IT. That is what stops them going vacuous if `ViewTenants` were ever
  // re-scoped to Tenant: the row would fail at the scope assertion rather than passing a refusal that no
  // longer means anything. **The first test hand-builds its `PermissionDefinition` instead, so it tests the
  // guard against a shape the catalog does not contain — deliberate, and a different question.**
  [Fact]
  [Trait("Criterion", "AC-IAM-0004")]
  [Trait("Criterion", "AC-TEN-0043")]
  // `AC-TEN-0043` — *"`Platform.Support.Administer` is `PermissionScope.PlatformSupport` and CANNOT BE
  // ASSIGNED to any tenant custom role, tenant system role, or tenant role-permission assignment."* The
  // scope half is `PlatformSupportBootstrapTests.Administer_platform_support_is_a_platform_support_scoped_
  // catalog_permission`; the assignment half is here. **Same guard as `AC-TEN-0024` one row over — the
  // criteria differ only in which permission family they name.**
  public void Tenant_role_rejects_platform_support_permission()
  {
    var role = CreateCustomRole(Guid.NewGuid());
    var permission = new PermissionDefinition(
      PermissionName.Create("Platform.Support.View").Value,
      PermissionScope.PlatformSupport,
      "Platform support only");

    var result = role.AssignPermission(permission, "actor", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Empty(role.ActivePermissions);
  }

  [Theory]
  [InlineData(PlatformPermissionNames.ViewTenants)]
  [InlineData(PlatformPermissionNames.ManageTenants)]
  [InlineData(PlatformPermissionNames.TenantLifecycle)]
  [Trait("Criterion", "AC-IAM-0004")]
  [Trait("Criterion", "AC-TEN-0024")]
  // `AC-TEN-0024` — *"`Platform.Tenants.View`, `Platform.Tenants.Manage`, and `Platform.Tenants.Lifecycle`
  // cannot be assigned to any tenant custom role, tenant system role, or tenant role-permission assignment;
  // `Role.AssignPermission` REJECTS THEM BY SCOPE."*
  //
  // ⚠ **THE CRITERION NAMES THREE PERMISSIONS AND THE THEORY HAS THREE ROWS, ONE EACH.** That is the
  // enumeration rule satisfied rather than assumed — and it matters here because the guard is a single
  // `Scope != Tenant` check, so **one row would have proved the whole class and the author still wrote
  // three.** The row set also asserts each permission IS `PermissionScope.PlatformSupport` before assigning,
  // so a permission silently re-scoped to `Tenant` reddens rather than passing.
  //
  // ⚠⚠ THE *SYSTEM ROLE* CLAUSE IS SATISFIED BY A BROADER GUARD, AND IT IS NOT MERELY UNTESTED — IT IS
  // ***UNEXERCISABLE AS STATED***. `AssignPermission` refuses a `RoleType.System` role BEFORE the scope check
  // runs, so **any input that would test the plane rule on a system role is refused before reaching it.**
  // A system role rejects EVERY permission, platform or tenant.
  //
  // **This is not vacuity — the assertion below can fail — and not adjacent-verb, because the citation is
  // right.** The criterion's clause HOLDS, for a reason that has nothing to do with planes, **and the guard
  // that satisfies it would still be there if the plane rule vanished entirely.** ⚠ It is the
  // ordered-refusals problem with the EARLIER guard accidentally satisfying a criterion about the LATER one:
  // *satisfied by a broader guard, unexercisable as stated.*
  //
  // ⚠⚠⚠ THIS TEST ALREADY CARRIED `AC-IAM-0004`, SO IT DID NOT READ AS UNCITED — the same blind spot as
  // `AC-TEN-0016`. **It was found by a PLANT, not a search**: `git grep` for `PlatformPermissionRejected`
  // returned nothing because the assertion is `result.IsFailure`, not the error constant. ***A NAME SEARCH
  // OVER THE ERROR MISSED A TEST ASSERTING THE BEHAVIOUR.***
  public void Custom_tenant_role_cannot_acquire_platform_tenant_permission(string permissionName)
  {
    var role = CreateCustomRole(Guid.NewGuid());
    var catalog = new PlatformPermissionCatalog();
    Assert.True(catalog.TryGet(permissionName, out var permission));
    Assert.Equal(PermissionScope.PlatformSupport, permission.Scope);

    var result = role.AssignPermission(permission, "actor", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Empty(role.ActivePermissions);
  }

  [Theory]
  [InlineData(PlatformPermissionNames.ViewTenants)]
  [InlineData(PlatformPermissionNames.ManageTenants)]
  [InlineData(PlatformPermissionNames.TenantLifecycle)]
  [Trait("Criterion", "AC-IAM-0004")]
  public void System_tenant_role_cannot_acquire_platform_tenant_permission(string permissionName)
  {
    var role = Role.CreateSystem(Guid.NewGuid(), RoleName.Create("Tenant Administrator").Value, null);
    var catalog = new PlatformPermissionCatalog();
    Assert.True(catalog.TryGet(permissionName, out var permission));

    // No supported tenant-role path may acquire a PlatformSupport permission. The specific rejection
    // reason (protected-system-role guard fires before the scope guard) is not asserted — only the outcome.
    var result = role.AssignPermission(permission, "actor", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Empty(role.ActivePermissions);
  }

  // ⚠⚠⚠ **DO NOT DELETE OR NARROW THIS TEST: FOUR REFUSAL TESTS IN THIS FILE ARE VACUOUS WITHOUT IT.**
  //
  // `Tenant_role_rejects_platform_support_permission`, both `…cannot_acquire_platform_tenant_permission`
  // theories, and `Administrator_role_name_does_not_imply_permissions` all end in
  // `Assert.Empty(role.ActivePermissions)` — **which a role that could hold NO permission at all would
  // satisfy just as well.** This is the only test in the file that shows a permission ARRIVING, so it is
  // what makes every one of those empties mean *refused* rather than *inert*.
  //
  // Those four carry `AC-IAM-0004` and `AC-IAM-0015`. **The dependency is stated HERE, once, rather than
  // four times on the dependents — four copies would rot independently and could drift out of agreement
  // with each other; one sentence at the site with the fan-in cannot.**
  [Fact]
  public void Custom_tenant_role_still_accepts_a_tenant_scoped_permission()
  {
    var role = CreateCustomRole(Guid.NewGuid());
    var catalog = new PlatformPermissionCatalog();
    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewCompanies, out var permission));
    Assert.Equal(PermissionScope.Tenant, permission.Scope);

    Assert.True(role.AssignPermission(permission, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.Contains(role.ActivePermissions, name => name.Equals(permission.Name));
  }

  [Fact]
  public void Permission_matching_is_ordinal_and_assignments_keep_history()
  {
    var role = CreateCustomRole(Guid.NewGuid());
    var catalog = new PlatformPermissionCatalog();
    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewUsers, out var permission));

    Assert.True(role.AssignPermission(permission, "actor-1", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(role.RemovePermission(permission.Name, "actor-2", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.True(role.AssignPermission(permission, "actor-3", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);

    Assert.Equal(2, role.PermissionAssignments.Count);
    Assert.Single(role.PermissionAssignments.Where(item => item.IsActive));
    Assert.False(catalog.TryGet("platform.Users.View", out _));
  }

  // ⚠ CITES `AC-IAM-0011` — *"An eligible same-tenant role can be assigned EXACTLY ONCE to an active tenant
  // user"* — and it is `:152` that carries it: the second `AssignRole` of the SAME role fails. The
  // surrounding successes are what make that failure mean *exactly once* rather than *assignment is broken*.
  //
  // ⚠⚠ AND `AC-IAM-0014` ONLY IN PART. That criterion is *"A user may hold MULTIPLE ROLES and receives the
  // DISTINCT UNION of their permissions"* — two clauses. **This test carries the first: no permission is
  // assigned to either role here, so no union is computed and a duplicate across roles is never exercised.**
  //
  // Clause 2 is carried by `IdentityAccessApplicationTests.Effective_permissions_use_only_active_
  // memberships_roles_and_permission_assignments`, where two roles both grant `ViewUsers` and the resolver
  // returns ONE entry. **Named here rather than left as an open residual, because a residual that has since
  // been closed elsewhere is a false absence and those rot fastest.**
  //
  // ⚠⚠⚠ AND `:153-157` IS THE ANTI-VACUITY CONTROL FOR THE *EXACTLY ONCE* CLAIM, WHICH IS EASY TO MISREAD AS
  // *NEVER TWICE*: after removing the first role it is assigned AGAIN and succeeds, leaving 3 assignments of
  // which 2 are active. **So the rule is one ACTIVE assignment at a time, not one assignment ever — and the
  // history row survives the removal, which is `AC-IAM-0017`'s no-deletion property showing up in the role
  // graph rather than on the user.**
  [Fact]
  [Trait("Criterion", "AC-IAM-0011")]
  [Trait("Criterion", "AC-IAM-0014")]
  public void Tenant_user_supports_multiple_roles_and_keeps_removed_assignment_history()
  {
    var tenantId = Guid.NewGuid();
    var user = CreateTenantUser(tenantId);
    var firstRole = CreateCustomRole(tenantId, "Administrator", 1);
    var secondRole = CreateCustomRole(tenantId, "Auditor", 2);

    Assert.True(user.AssignRole(firstRole, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(user.AssignRole(secondRole, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(user.AssignRole(secondRole, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(user.RemoveRole(firstRole.Id, "actor", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.True(user.AssignRole(firstRole, "actor", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);

    Assert.Equal(3, user.RoleAssignments.Count);
    Assert.Equal(2, user.RoleAssignments.Count(item => item.IsActive));
  }

  // ⚠ TWO CRITERIA, ONE PER HALF, AND THE TEST NAME SAYS SO — *cross_tenant* AND *inactive*:
  //
  //   `AC-IAM-0012`  *"A role from one tenant cannot be assigned to a user in another tenant."*  `:166`
  //   `AC-IAM-0019`  *"A role PENDING RETIREMENT or retired cannot receive new user assignments."*  `:169-170`
  //
  // ⚠⚠ `AC-IAM-0019` NAMES TWO STATES AND THIS EXERCISES ONE. `RequestRetirement` puts the role in
  // `RetirementPending`; **a fully `Retired` role is not assigned against here.** `Role_can_retire_after_
  // its_active_user_assignment_is_removed` below reaches `Retired`, but asserts about retirement rather
  // than about assignment to a retired role. Cited as the pending half.
  //
  // ⚠⚠⚠ AND `AC-IAM-0012` ALREADY HAS A CITER IN COMMENT FORM: `TenantUserAssignmentAndConcurrencyTests`
  // carries it as prose with `TS-IAM-0043` named. **That is prior B18 work at a different layer, and this
  // is a second, independent witness at the domain layer — not a conversion of it.** Worth separating,
  // because converting a comment to a trait moves a number without adding evidence and this does add one.
  [Fact]
  [Trait("Criterion", "AC-IAM-0012")]
  [Trait("Criterion", "AC-IAM-0019")]
  public void Tenant_user_rejects_cross_tenant_and_inactive_role_assignment()
  {
    var user = CreateTenantUser(Guid.NewGuid());
    var otherTenantRole = CreateCustomRole(Guid.NewGuid());

    Assert.True(user.AssignRole(otherTenantRole, "actor", Guid.NewGuid(), Now).IsFailure);

    var sameTenantRole = CreateCustomRole(user.TenantId);
    Assert.True(sameTenantRole.RequestRetirement(Guid.NewGuid(), Now).IsSuccess);
    Assert.True(user.AssignRole(sameTenantRole, "actor", Guid.NewGuid(), Now).IsFailure);
  }

  // ⚠ CITES `AC-IAM-0017` — *"No API or domain operation physically deletes a user"* — AT THE DOMAIN LAYER.
  // `PersistenceArchitectureTests` carries it as a source-shape ban (there is no delete to call); this
  // carries the positive form: the operation that LOOKS like removal is a reversible status change, and the
  // membership survives it.
  //
  // ⚠⚠ AND IT IS DELIBERATELY **NOT** CITED TO `AC-IAM-0016` — *"A deactivated user cannot obtain or refresh
  // usable tenant ACCESS."* That criterion is about ACCESS; this test asserts a STATUS TRANSITION and the
  // survival of a membership row, and never attempts to obtain or refresh anything. **Adjacent-verb: same
  // subject, wrong predicate.** A reader could easily take *Deactivation_is_reversible* as covering
  // deactivation's whole story; it covers the half that is about the record rather than about the door.
  [Fact]
  [Trait("Criterion", "AC-IAM-0017")]
  public void Deactivation_is_reversible_and_never_removes_the_membership()
  {
    var user = CreateTenantUser(Guid.NewGuid());

    Assert.True(user.Deactivate(Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(TenantUserStatus.Deactivated, user.Status);
    Assert.True(user.Reactivate(Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(TenantUserStatus.Active, user.Status);
  }

  [Fact]
  public void Role_retirement_requires_pending_state_and_no_active_users()
  {
    var role = CreateCustomRole(Guid.NewGuid());
    var catalog = new PlatformPermissionCatalog();
    catalog.TryGet(PlatformPermissionNames.ViewUsers, out var permission);
    Assert.True(role.AssignPermission(permission, "actor", Guid.NewGuid(), Now).IsSuccess);

    Assert.True(role.Retire(false, Guid.NewGuid(), Now).IsFailure);
    Assert.True(role.RequestRetirement(Guid.NewGuid(), Now).IsSuccess);
    Assert.Single(role.ActivePermissions);
    Assert.True(role.AssignPermission(permission, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(role.Retire(true, Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
    Assert.Equal(RoleStatus.RetirementPending, role.Status);
    Assert.True(role.Retire(false, Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(RoleStatus.Retired, role.Status);
    Assert.Empty(role.ActivePermissions);
    Assert.True(role.AssignPermission(permission, "actor", Guid.NewGuid(), Now).IsFailure);

    var user = CreateTenantUser(role.TenantId);
    Assert.True(user.AssignRole(role, "actor", Guid.NewGuid(), Now).IsFailure);
  }

  // ⚠ CITES `AC-IAM-0015` — *"A role grants no permission merely because of its name"* — AND THE
  // *"including a role named Administrator"* CLAUSE OF `AC-IAM-0004`. The role is built with the literal
  // name the criteria worry about, and the assertion is that nothing follows from it.
  //
  // ⚠⚠ THE NAME IS LOAD-BEARING AND LOOKS ARBITRARY, WHICH IS WHY THIS NOTE EXISTS. `"Administrator"` here
  // is not a sample string: it is the exact name two criteria call out, because it is the one a
  // name-matching shortcut would most plausibly be written against. **Change it to `"Manager"` and both
  // citations quietly stop being observed while the test keeps passing** — an arrangement carrying the
  // discrimination, with nothing in the source saying so.
  //
  // ⚠⚠⚠ AND IT IS THE ONLY PLACE THE CLAUSE CAN LIVE. `PlatformPlaneAuthorizationArchitectureTests` proves
  // a tenant token cannot carry a PlatformSupport permission, but its filter takes PERMISSION NAMES — no
  // role passes through it, so it cannot observe a role name however it is arranged. **The scope mechanism
  // and the name clause are at different layers and need different tests; I first credited the filter test
  // with both and that was wrong.**
  //
  // ⚠ ANTI-VACUITY: this is the fourth of the tests that depend on
  // `Custom_tenant_role_still_accepts_a_tenant_scoped_permission`; the dependency is stated there, once.
  [Fact]
  [Trait("Criterion", "AC-IAM-0015")]
  [Trait("Criterion", "AC-IAM-0004")]
  public void Administrator_role_name_does_not_imply_permissions()
  {
    var role = CreateCustomRole(Guid.NewGuid(), "Administrator");

    Assert.Empty(role.ActivePermissions);
  }

  // ⚠ CITES `AC-IAM-0018` — *"A role assigned to any ACTIVE user cannot be retired"* — and it is the
  // ORDERED pair that carries it: `Retire` fails while the assignment is active, the assignment is removed,
  // `Retire` then succeeds. **The success is the load-bearing half.** Without it, the failure is satisfied
  // by a role that can never retire at all, and the criterion would read as covered while the product was
  // broken in the opposite direction.
  //
  // ⚠⚠ AND `AC-IAM-0020` — *"Retiring a role PRESERVES ASSIGNMENTS and audit history required for
  // traceability"* — is carried by the two assertions after the retirement: the assignment row still
  // EXISTS and is INACTIVE. `Assert.Single(user.RoleAssignments)` is the preservation; `Assert.False(…
  // IsActive)` is what stops preservation being confused with the assignment still counting.
  //
  // ⚠⚠⚠ WHAT DECIDES `Retire`'s OUTCOME IS AN ARGUMENT, NOT A LOOKUP: the caller passes
  // `user.ActiveRoleIds.Contains(role.Id)`. **So this test proves the domain HONOURS the flag and nothing
  // about who computes it.** *An argument proves a value was passed, not that it was right* — so the
  // guarantee moves to the caller, and the callers were enumerated rather than assumed:
  //
  //   PRODUCTION   `RetireRoleCommandHandler:38-39` — the ONLY production caller of `Role.Retire`. It
  //                awaits `ITenantUserRepository.HasActiveAssignmentToRoleAsync(role.Id)` and passes the
  //                result directly. **A live query, not a constant and not a cached set.**
  //   SQL          `PlatformIdentityAccessSqlServerBehaviorTests:268-282` exercises that repository method
  //                against real SQL through four states — assigned (true), user DEACTIVATED (false),
  //                reactivated (true), role removed (false). **The deactivated case is the subtle one and
  //                it is covered.**
  //
  // **So `AC-IAM-0018` holds end to end and the check lives in the handler.** The earlier version of this
  // note called that a doubt; it was an unfinished search, and the search came back clean.
  //
  // ⚠ THE RESIDUAL THAT REPLACES IT IS NARROWER AND REAL: **NOTHING CONSTRUCTS `RetireRoleCommandHandler`.
  // No test in the repository references it.** Both halves are proved separately and their JOIN is not —
  // and the integration test above does not close it, because it ASSEMBLES the same composition BY HAND
  // (`role.Retire(await repository.HasActiveAssignmentToRoleAsync(...))`). **A test that reproduces what a
  // handler does is a SUBSTITUTE for the handler, not a witness of it**: reorder the handler to query
  // before a status change, or cache the flag, and every assertion cited here stays green.
  [Fact]
  [Trait("Criterion", "AC-IAM-0018")]
  [Trait("Criterion", "AC-IAM-0020")]
  public void Role_can_retire_after_its_active_user_assignment_is_removed()
  {
    var tenantId = Guid.NewGuid();
    var role = CreateCustomRole(tenantId);
    var user = CreateTenantUser(tenantId);
    Assert.True(user.AssignRole(role, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(role.RequestRetirement(Guid.NewGuid(), Now).IsSuccess);

    Assert.True(role.Retire(user.ActiveRoleIds.Contains(role.Id), Guid.NewGuid(), Now).IsFailure);
    Assert.True(user.RemoveRole(role.Id, "actor", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.True(role.Retire(user.ActiveRoleIds.Contains(role.Id), Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(RoleStatus.Retired, role.Status);
    Assert.Single(user.RoleAssignments);
    Assert.False(user.RoleAssignments.Single().IsActive);
  }

  [Fact]
  public void System_roles_are_protected_from_tenant_administration()
  {
    var role = Role.CreateSystem(Guid.NewGuid(), RoleName.Create("Tenant Administrator").Value, null);
    var catalog = new PlatformPermissionCatalog();
    catalog.TryGet(PlatformPermissionNames.ViewUsers, out var permission);

    Assert.True(role.Update(RoleName.Create("Changed").Value, null, Guid.NewGuid(), Now).IsFailure);
    Assert.True(role.AssignPermission(permission, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(role.RequestRetirement(Guid.NewGuid(), Now).IsFailure);
  }

  private static Role CreateCustomRole(Guid tenantId, string name = "Administrator", long id = 1)
  {
    var role = Role.CreateCustom(tenantId, RoleName.Create(name).Value, null, Guid.NewGuid(), Now);
    SetEntityId(role, id);
    return role;
  }

  private static TenantUser CreateTenantUser(Guid tenantId) => TenantUser.CreateActive(
    42,
    tenantId,
    EmailAddress.Create("user@example.com").Value,
    UserDisplayName.Create("User").Value,
    Guid.NewGuid(),
    Now);

  private static void SetEntityId(object entity, long id)
  {
    var field = typeof(SSAS.BuildingBlocks.Domain.Entity<long>)
      .GetField("<Id>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, id);
  }
}

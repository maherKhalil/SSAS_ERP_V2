using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.IdentityAccess;

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
  // ⚠⚠ AND THE ANTI-VACUITY CONTROL IS `Custom_tenant_role_still_accepts_a_tenant_scoped_permission` AT
  // `:88`. `Assert.Empty(role.ActivePermissions)` after a refused assignment is satisfied just as well by a
  // role that can hold nothing; the `:88` test assigns a Tenant-scoped catalog permission and asserts it
  // APPEARS. **Every `Assert.Empty` in this group means *refused* only because that one exists.**
  //
  // ⚠⚠⚠ NOTE THE SECOND AND THIRD TESTS TAKE THEIR PERMISSIONS FROM THE REAL CATALOG AND ASSERT THE SCOPE
  // THEY EXPECT (`:62`) BEFORE USING IT. That is what stops them going vacuous if `ViewTenants` were ever
  // re-scoped to Tenant: the row would fail at the scope assertion rather than passing a refusal that no
  // longer means anything. **The first test hand-builds its `PermissionDefinition` instead, so it tests the
  // guard against a shape the catalog does not contain — deliberate, and a different question.**
  [Fact]
  [Trait("Criterion", "AC-IAM-0004")]
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
  // DISTINCT UNION of their permissions"* — two clauses. **This test carries the first and says nothing
  // about the second: no permission is assigned to either role, so no union is computed and a duplicate
  // across roles is never exercised.** Cited for clause 1, with clause 2 named as not covered here — *union*
  // is the clause a reader would assume from *multiple roles*, and it is the one that is absent.
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
  // ⚠ ANTI-VACUITY: `Assert.Empty` over a freshly-created role is weak alone — a role that could hold no
  // permissions at all would pass it. **`Custom_tenant_role_still_accepts_a_tenant_scoped_permission` at
  // `:88` is the control**: it assigns a Tenant-scoped catalog permission and asserts it APPEARS in
  // `ActivePermissions`. So *empty here* means *nothing came from the name*, not *nothing ever arrives*.
  // The two tests are a pair and neither states it.
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
  // ⚠⚠⚠ NOTE WHAT DECIDES `Retire`'s OUTCOME: THE CALLER PASSES `user.ActiveRoleIds.Contains(role.Id)` —
  // the aggregate is TOLD whether an active assignment exists rather than discovering it. **So this proves
  // the domain honours the flag, and NOT that any caller computes it correctly.** The application layer
  // owns that, and a caller passing a stale or hardcoded `false` would retire an assigned role with every
  // assertion here still green. Stated because *cannot be retired* reads as a guarantee about the system.
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

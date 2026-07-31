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

  [Fact]
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

  [Fact]
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

  [Fact]
  public void Tenant_user_rejects_cross_tenant_and_inactive_role_assignment()
  {
    var user = CreateTenantUser(Guid.NewGuid());
    var otherTenantRole = CreateCustomRole(Guid.NewGuid());

    Assert.True(user.AssignRole(otherTenantRole, "actor", Guid.NewGuid(), Now).IsFailure);

    var sameTenantRole = CreateCustomRole(user.TenantId);
    Assert.True(sameTenantRole.RequestRetirement(Guid.NewGuid(), Now).IsSuccess);
    Assert.True(user.AssignRole(sameTenantRole, "actor", Guid.NewGuid(), Now).IsFailure);
  }

  [Fact]
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

  [Fact]
  public void Administrator_role_name_does_not_imply_permissions()
  {
    var role = CreateCustomRole(Guid.NewGuid(), "Administrator");

    Assert.Empty(role.ActivePermissions);
  }

  [Fact]
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

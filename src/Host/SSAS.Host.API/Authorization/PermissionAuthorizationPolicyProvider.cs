using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SSAS.Host.API.Authorization;

public sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
  : DefaultAuthorizationPolicyProvider(options)
{
  public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
  {
    // Platform prefix is checked first for clarity; "PlatformPermission:" is not a prefix of "Permission:" (or
    // the reverse), so the tenant and platform branches never collide.
    if (policyName.StartsWith(PlatformPermissionAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal))
    {
      return CreatePlatformPermissionPolicyAsync(policyName);
    }

    if (policyName.StartsWith(PermissionAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal))
    {
      return CreatePermissionPolicyAsync(policyName);
    }

    if (policyName.StartsWith(RoleAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal))
    {
      return CreateRolePolicyAsync(policyName);
    }

    return base.GetPolicyAsync(policyName);
  }

  private static Task<AuthorizationPolicy?> CreatePlatformPermissionPolicyAsync(string policyName)
  {
    if (!PlatformPermissionAuthorizationDefaults.TryGetPermissionName(policyName, out var permission))
    {
      return Task.FromResult<AuthorizationPolicy?>(null);
    }

    var policy = CreatePolicyBuilder()
      .AddRequirements(new PlatformPermissionRequirement(permission))
      .Build();

    return Task.FromResult<AuthorizationPolicy?>(policy);
  }

  private static Task<AuthorizationPolicy?> CreatePermissionPolicyAsync(string policyName)
  {
    if (!PermissionAuthorizationDefaults.TryGetPermissionName(policyName, out var permission))
    {
      return Task.FromResult<AuthorizationPolicy?>(null);
    }

    var policy = CreatePolicyBuilder()
      .AddRequirements(new PermissionRequirement(permission))
      .Build();

    return Task.FromResult<AuthorizationPolicy?>(policy);
  }

  private static Task<AuthorizationPolicy?> CreateRolePolicyAsync(string policyName)
  {
    if (!RoleAuthorizationDefaults.TryGetRoleName(policyName, out var role))
    {
      return Task.FromResult<AuthorizationPolicy?>(null);
    }

    var policy = CreatePolicyBuilder()
      .AddRequirements(new RoleRequirement(role))
      .Build();

    return Task.FromResult<AuthorizationPolicy?>(policy);
  }

  private static AuthorizationPolicyBuilder CreatePolicyBuilder() => new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
    .RequireAuthenticatedUser();
}

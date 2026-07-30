using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SSAS.Host.API.Authorization;

public sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
  : DefaultAuthorizationPolicyProvider(options)
{
  public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
  {
    if (!policyName.StartsWith(PermissionAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal))
    {
      return base.GetPolicyAsync(policyName);
    }

    var permission = policyName[PermissionAuthorizationDefaults.PolicyPrefix.Length..];
    if (string.IsNullOrWhiteSpace(permission))
    {
      return Task.FromResult<AuthorizationPolicy?>(null);
    }

    var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
      .AddRequirements(new PermissionRequirement(permission))
      .Build();

    return Task.FromResult<AuthorizationPolicy?>(policy);
  }
}

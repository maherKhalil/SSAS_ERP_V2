using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authorization;

namespace SSAS.API.Tests.Infrastructure;

public sealed class PermissionAuthorizationTests
{
  [Fact]
  public async Task Authenticated_user_with_the_required_permission_is_authorized()
  {
    var requirement = new PermissionRequirement("future.permission");
    var context = new AuthorizationHandlerContext(
      [requirement],
      new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(JwtClaimTypes.Permission, "future.permission")],
        authenticationType: "Bearer")),
      resource: null);

    await new PermissionAuthorizationHandler().HandleAsync(context);

    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task Authenticated_user_without_the_required_permission_is_forbidden()
  {
    var requirement = new PermissionRequirement("future.permission");
    var context = new AuthorizationHandlerContext(
      [requirement],
      new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Bearer")),
      resource: null);

    await new PermissionAuthorizationHandler().HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task Permission_policy_provider_creates_a_bearer_policy_for_a_permission_name()
  {
    var provider = new PermissionAuthorizationPolicyProvider(
      Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));

    var policy = await provider.GetPolicyAsync("Permission:future.permission");

    Assert.NotNull(policy);
    Assert.Contains(policy.Requirements, requirement => requirement is PermissionRequirement
      { Permission: "future.permission" });
  }
}

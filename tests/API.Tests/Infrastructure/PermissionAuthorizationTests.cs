using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.Host.API.Authorization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.API.Tests.Infrastructure;

public sealed class PermissionAuthorizationTests
{
  private static readonly Guid TenantId = Guid.Parse("4198c3af-0c3a-4d83-a0ce-0acb56c52612");

  [Fact]
  public async Task Authenticated_user_with_the_required_permission_and_tenant_is_authorized()
  {
    var requirement = new PermissionRequirement("test.permission");
    var context = CreateContext(
      requirement,
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    await PermissionHandler(new TestCurrentTenant(TenantId)).HandleAsync(context);

    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task Authenticated_user_without_the_required_permission_is_not_authorized()
  {
    var requirement = new PermissionRequirement("test.permission");
    var context = CreateContext(requirement, new Claim(JwtClaimTypes.TenantId, TenantId.ToString()));

    await PermissionHandler(new TestCurrentTenant(TenantId)).HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task Role_authorization_succeeds_only_with_an_exact_matching_role_claim()
  {
    var requirement = new RoleRequirement("test.role");
    var context = CreateContext(
      requirement,
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Role, "test.role"));

    await RoleHandler(new TestCurrentTenant(TenantId)).HandleAsync(context);

    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task Role_authorization_rejects_a_non_matching_role_claim()
  {
    var requirement = new RoleRequirement("test.role");
    var context = CreateContext(
      requirement,
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Role, "test.other_role"));

    await RoleHandler(new TestCurrentTenant(TenantId)).HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task Authorization_requires_a_validated_tenant_context()
  {
    var requirement = new PermissionRequirement("test.permission");
    var context = CreateContext(requirement, new Claim(JwtClaimTypes.Permission, "test.permission"));

    await PermissionHandler(new TestCurrentTenant(null)).HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task Authorization_rejects_an_inconsistent_tenant_context()
  {
    var requirement = new PermissionRequirement("test.permission");
    var context = CreateContext(
      requirement,
      new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
      new Claim(JwtClaimTypes.Permission, "test.permission"));

    await PermissionHandler(new TestCurrentTenant(Guid.NewGuid())).HandleAsync(context);

    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task Permission_policy_provider_creates_a_bearer_policy_for_a_valid_permission_name()
  {
    var provider = CreatePolicyProvider();
    var policyName = PermissionAuthorizationDefaults.CreatePolicyName("test.permission");

    var policy = await provider.GetPolicyAsync(policyName);

    Assert.NotNull(policy);
    Assert.Contains(policy.Requirements, requirement => requirement is PermissionRequirement
    { Permission: "test.permission" });
  }

  [Theory]
  [InlineData("Permission:")]
  [InlineData("Permission:test..permission")]
  [InlineData("Permission:test permission")]
  [InlineData("Permission:9test.permission")]
  public async Task Permission_policy_provider_rejects_invalid_permission_names(string policyName)
  {
    var policy = await CreatePolicyProvider().GetPolicyAsync(policyName);

    Assert.Null(policy);
  }

  [Fact]
  public async Task Role_policy_provider_creates_a_policy_for_a_valid_role_name()
  {
    var provider = CreatePolicyProvider();
    var policyName = RoleAuthorizationDefaults.CreatePolicyName("test.role");

    var policy = await provider.GetPolicyAsync(policyName);

    Assert.NotNull(policy);
    Assert.Contains(policy.Requirements, requirement => requirement is RoleRequirement { Role: "test.role" });
  }

  [Fact]
  public void Claim_constants_and_policy_names_follow_the_centralized_conventions()
  {
    Assert.Equal("sub", JwtClaimTypes.Subject);
    Assert.Equal("tenant_id", JwtClaimTypes.TenantId);
    Assert.Equal("role", JwtClaimTypes.Role);
    Assert.Equal("permission", JwtClaimTypes.Permission);
    Assert.Equal("Permission:test.permission", PermissionAuthorizationDefaults.CreatePolicyName("test.permission"));
    Assert.Equal("Role:test.role", RoleAuthorizationDefaults.CreatePolicyName("test.role"));
  }

  private static AuthorizationHandlerContext CreateContext(IAuthorizationRequirement requirement, params Claim[] claims)
  {
    return new AuthorizationHandlerContext(
      [requirement],
      new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer")),
      resource: null);
  }

  private static PermissionAuthorizationPolicyProvider CreatePolicyProvider() => new(Options.Create(new AuthorizationOptions()));

  private static PermissionAuthorizationHandler PermissionHandler(ICurrentTenant tenant) =>
    new(tenant, new LiveTenantEligibilityAuthorization(new ActiveTenantEligibility()), new HttpContextAccessor());

  private static RoleAuthorizationHandler RoleHandler(ICurrentTenant tenant) =>
    new(tenant, new LiveTenantEligibilityAuthorization(new ActiveTenantEligibility()), new HttpContextAccessor());

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class TestCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }
}

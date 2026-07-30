using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.Platform.Tests.RequestContext;

public sealed class RequestContextTests
{
  [Fact]
  public void Current_user_reads_subject_and_name_from_an_authenticated_identity()
  {
    var companyId = Guid.NewGuid();
    var accessor = CreateAccessor(CreateAuthenticatedUser(
      new Claim(JwtClaimTypes.Subject, "user-123"),
      new Claim(JwtClaimTypes.Name, "Ada Lovelace"),
      new Claim(JwtClaimTypes.Email, "ada@example.test"),
      new Claim(JwtClaimTypes.CompanyId, companyId.ToString()),
      new Claim(JwtClaimTypes.SessionId, "session-123"),
      new Claim(JwtClaimTypes.JwtId, "token-123"),
      new Claim(JwtClaimTypes.Role, "TenantAdmin"),
      new Claim(JwtClaimTypes.Role, "TenantAdmin"),
      new Claim(JwtClaimTypes.Permission, "Platform.Users.View")));

    var currentUser = new CurrentUser(accessor);

    Assert.Equal("user-123", currentUser.UserId);
    Assert.Equal("Ada Lovelace", currentUser.UserName);
    Assert.Equal("ada@example.test", currentUser.Email);
    Assert.Equal(companyId, currentUser.CompanyId);
    Assert.Equal("session-123", currentUser.SessionId);
    Assert.Equal("token-123", currentUser.TokenId);
    Assert.Equal(["TenantAdmin"], currentUser.Roles);
    Assert.Equal(["Platform.Users.View"], currentUser.Permissions);
  }

  [Fact]
  public void Current_tenant_reads_a_valid_tenant_claim_from_an_authenticated_identity()
  {
    var tenantId = Guid.NewGuid();
    var accessor = CreateAccessor(CreateAuthenticatedUser(new Claim(JwtClaimTypes.TenantId, tenantId.ToString())));

    var currentTenant = new CurrentTenant(accessor);

    Assert.Equal(tenantId, currentTenant.TenantId);
  }

  [Fact]
  public void Current_tenant_does_not_allow_a_request_header_to_override_a_validated_tenant_claim()
  {
    var tenantId = Guid.NewGuid();
    var context = new DefaultHttpContext
    {
      User = CreateAuthenticatedUser(new Claim(JwtClaimTypes.TenantId, tenantId.ToString()))
    };
    context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

    var currentTenant = new CurrentTenant(new HttpContextAccessor { HttpContext = context });

    Assert.Equal(tenantId, currentTenant.TenantId);
  }

  [Fact]
  public void Current_contexts_do_not_return_identity_or_tenant_data_for_an_unauthenticated_request()
  {
    var context = new DefaultHttpContext();
    context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
    var accessor = new HttpContextAccessor { HttpContext = context };

    Assert.Null(new CurrentUser(accessor).UserId);
    Assert.Null(new CurrentTenant(accessor).TenantId);
  }

  [Fact]
  public void Password_hashing_service_hashes_and_verifies_passwords_without_storing_plain_text()
  {
    var service = new AspNetPasswordHashingService(new PasswordHasher<object>());

    var hash = service.HashPassword("Correct-Horse-Battery-Staple-1");

    Assert.NotEqual("Correct-Horse-Battery-Staple-1", hash);
    Assert.True(service.VerifyPassword(hash, "Correct-Horse-Battery-Staple-1"));
    Assert.False(service.VerifyPassword(hash, "wrong-password"));
  }

  [Fact]
  public void Utc_date_time_provider_returns_utc_values()
  {
    var provider = new UtcDateTimeProvider();

    Assert.Equal(TimeSpan.Zero, provider.UtcNow.Offset);
  }

  [Fact]
  public void Correlation_context_exposes_the_value_set_by_request_middleware()
  {
    var context = new CorrelationContext();

    context.SetCorrelationId("correlation-123");

    Assert.Equal("correlation-123", context.CorrelationId);
  }

  private static HttpContextAccessor CreateAccessor(ClaimsPrincipal user)
  {
    return new HttpContextAccessor
    {
      HttpContext = new DefaultHttpContext { User = user }
    };
  }

  private static ClaimsPrincipal CreateAuthenticatedUser(params Claim[] claims)
  {
    return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));
  }
}

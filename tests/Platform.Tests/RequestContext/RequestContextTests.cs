using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.Platform.Tests.RequestContext;

public sealed class RequestContextTests
{
  [Fact]
  public void Current_user_reads_subject_and_name_from_an_authenticated_identity()
  {
    var accessor = CreateAccessor(CreateAuthenticatedUser(
      new Claim(JwtClaimTypes.Subject, "user-123"),
      new Claim(JwtClaimTypes.Name, "Ada Lovelace")));

    var currentUser = new CurrentUser(accessor);

    Assert.Equal("user-123", currentUser.UserId);
    Assert.Equal("Ada Lovelace", currentUser.UserName);
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
  public void Current_contexts_do_not_return_identity_or_tenant_data_for_an_unauthenticated_request()
  {
    var context = new DefaultHttpContext();
    context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
    var accessor = new HttpContextAccessor { HttpContext = context };

    Assert.Null(new CurrentUser(accessor).UserId);
    Assert.Null(new CurrentTenant(accessor).TenantId);
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

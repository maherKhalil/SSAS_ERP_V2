using System.Globalization;
using System.Text.RegularExpressions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.API.PlatformSupport;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

// Phase-4D authorization matrix (ADR-016 §5 / DEC-TEN-0021 / DEC-TEN-0025). Every authority-administration
// route — mutations AND reads — must be reachable only by a validated PLATFORM-plane token carrying
// Platform.Support.Administer. Real signed RS256 JWTs flow through JwtBearer -> StrictAccessTokenValidator ->
// the Phase-4A platform permission policy; nothing is mocked at the authorization boundary.
//
// Application handlers are deliberately NOT registered here: every case in this class must be rejected before
// endpoint execution, so a request that reached a handler would surface as a DI failure rather than passing.
public sealed class PlatformSupportAuthorityAuthorizationTests : IAsyncLifetime
{
  private const string Issuer = "https://platform-authority.tests";
  private const string Audience = "platform-authority-tests";
  private const string Prefix = "/api/platform/support/principals";
  private WebApplication? application;
  private HttpClient? client;

  // ---- THE ROUTES ARE DERIVED FROM THIS HOST, NOT LISTED (243 step 2).
  //
  // ⚠⚠⚠ THIS CHANGED NOTHING TODAY, AND THAT IS THE POINT RATHER THAN AN OBJECTION TO IT. Step 1
  // compared the nine hand-written pairs against the derived set and they were IDENTICAL, 9 for 9, in
  // both directions. So this substitution adds no coverage now and could read as churn.
  //
  // THE VALUE IS ENTIRELY IN THE TENTH ROUTE. Before this, a new authority route was caught by
  // `PlatformSupportAuthorityRouteInventoryTests` -- which is about the ROUTE LIST -- and was SILENTLY
  // EXEMPT from all four guards below, which are about WHO MAY CALL IT. The cheap test saw it and the
  // expensive one did not.
  //
  // ⚠ AND THE ROUTES COME FROM THIS TEST'S OWN APPLICATION, not from the shared host factory. This class
  // builds a deliberately minimal host -- application handlers are unregistered so anything reaching one
  // would surface as a DI failure rather than a pass -- and it maps the same surface through
  // `MapPlatformSupportAuthorityEndpoints`. Deriving from the host the requests are actually sent to is
  // stricter than borrowing another one's endpoint source.
  //
  // ⚠⚠ THE NORMALISATION IS PART OF THE MEASUREMENT. The endpoint source yields TEMPLATES
  // (`{principalId}`); a request needs a concrete path. Substituting `1` on a segment boundary is a
  // CHOICE, stated here so a later reader disagrees with the choice rather than with the result.
  private (string Method, string Path)[] AuthorityRoutes()
  {
    var derived = PlatformRouteInventory.Under(Application.Services, Prefix)
      .Select(route => (
        Method: PlatformRouteInventory.FirstMethodOf(route),
        Path: Regex.Replace(route.RoutePattern.RawText!, @"\{[^}]+\}", "1")))
      .OrderBy(route => route.Path, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    // ---- ⚠⚠⚠ THE FLOOR, AND IT IS THE WHOLE REASON A DERIVED POPULATION IS SAFE HERE.
    //
    // A derivation that returned NOTHING would turn all four of these into vacuous passes -- FOUR
    // SECURITY GUARDS ASSERTING NOTHING, which is strictly worse than the hand-written list this
    // replaced. NINE is the number step 1 measured; it is a floor and not an expectation, so ordinary
    // growth does not touch it.
    Assert.True(
      derived.Length >= 9,
      $"the authority route derivation found {derived.Length} route(s); it must find at least 9. " +
      "These four guards assert nothing over an empty set.");

    return derived;
  }

  private WebApplication Application =>
    application ?? throw new InvalidOperationException("the host has not been initialised");

  [Fact]
  public async Task Every_authority_route_rejects_an_anonymous_request()
  {
    var unprotected = new List<string>();

    foreach (var (method, path) in AuthorityRoutes())
    {
    using var request = Request(method, path);

    var response = await Client.SendAsync(request);

      if (response.StatusCode != HttpStatusCode.Unauthorized)
      {
        unprotected.Add($"{method} {path} -> {response.StatusCode}");
      }
    }

    // ---- EVERY OFFENDER, NOT THE FIRST. An assertion inside the loop stops at route one, and
    // the day this fires it will be because SEVERAL routes were added unprotected -- *which*
    // routes is the whole question. The theory form named them all; this restores that.
    Assert.True(
      unprotected.Count == 0,
      $"{unprotected.Count} authority route(s) did not refuse an anonymous request: " +
      string.Join("; ", unprotected));
  }

  [Fact]
  public async Task Every_authority_route_rejects_a_tenant_plane_token_carrying_the_administer_name()
  {
    var unprotected = new List<string>();

    foreach (var (method, path) in AuthorityRoutes())
    {
    // A valid tenant token that (illegally) carries the platform permission name still lacks
    // security_plane=platform, so the platform handler must refuse it. Tenant authority cannot reach
    // platform authority administration.
    using var request = Request(method, path);
    request.Headers.Authorization = new("Bearer", SignToken(TenantClaims(PlatformPermissionNames.AdministerPlatformSupport)));

    var response = await Client.SendAsync(request);

      if (response.StatusCode != HttpStatusCode.Forbidden)
      {
        unprotected.Add($"{method} {path} -> {response.StatusCode}");
      }
    }

    // ---- EVERY OFFENDER, NOT THE FIRST. An assertion inside the loop stops at route one, and
    // the day this fires it will be because SEVERAL routes were added unprotected -- *which*
    // routes is the whole question. The theory form named them all; this restores that.
    Assert.True(
      unprotected.Count == 0,
      $"{unprotected.Count} authority route(s) did not refuse a tenant-plane token: " +
      string.Join("; ", unprotected));
  }

  [Fact]
  public async Task Every_authority_route_rejects_a_platform_token_without_administer()
  {
    var unprotected = new List<string>();

    foreach (var (method, path) in AuthorityRoutes())
    {
    // Valid platform plane, but only a non-administrative PlatformSupport permission: authenticated yet
    // unauthorized. Reads are gated by Administer too (DEC-TEN-0025), so this must fail on GET as well.
    using var request = Request(method, path);
    request.Headers.Authorization = new("Bearer", SignToken(PlatformClaims(PlatformPermissionNames.ViewTenants)));

    var response = await Client.SendAsync(request);

      if (response.StatusCode != HttpStatusCode.Forbidden)
      {
        unprotected.Add($"{method} {path} -> {response.StatusCode}");
      }
    }

    // ---- EVERY OFFENDER, NOT THE FIRST. An assertion inside the loop stops at route one, and
    // the day this fires it will be because SEVERAL routes were added unprotected -- *which*
    // routes is the whole question. The theory form named them all; this restores that.
    Assert.True(
      unprotected.Count == 0,
      $"{unprotected.Count} authority route(s) did not refuse a platform token without Administer: " +
      string.Join("; ", unprotected));
  }

  [Fact]
  public async Task Every_authority_route_rejects_a_mixed_plane_token()
  {
    var unprotected = new List<string>();

    foreach (var (method, path) in AuthorityRoutes())
    {
    // security_plane=platform plus a forbidden tenant_id: StrictAccessTokenValidator fails the token itself.
    using var request = Request(method, path);
    var claims = PlatformClaims(PlatformPermissionNames.AdministerPlatformSupport);
    claims.Add(new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")));
    request.Headers.Authorization = new("Bearer", SignToken(claims));

    var response = await Client.SendAsync(request);

      if (response.StatusCode != HttpStatusCode.Unauthorized)
      {
        unprotected.Add($"{method} {path} -> {response.StatusCode}");
      }
    }

    // ---- EVERY OFFENDER, NOT THE FIRST. An assertion inside the loop stops at route one, and
    // the day this fires it will be because SEVERAL routes were added unprotected -- *which*
    // routes is the whole question. The theory form named them all; this restores that.
    Assert.True(
      unprotected.Count == 0,
      $"{unprotected.Count} authority route(s) did not refuse a mixed-plane token: " +
      string.Join("; ", unprotected));
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30"
    });
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    application = builder.Build();
    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformSupportAuthorityEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
    client.BaseAddress = new Uri("https://localhost");
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private static HttpRequestMessage Request(string method, string path)
  {
    var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (method == "POST")
    {
      request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    }

    return request;
  }

  private static IEnumerable<Claim> BaseClaims() =>
  [
    new Claim(JwtClaimTypes.Subject, "platform-operator"),
    new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
    new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
    new Claim(JwtClaimTypes.IdentityId, "11"),
    new Claim(JwtClaimTypes.SessionId, "33"),
    new Claim(JwtClaimTypes.ClientId, AuthenticationClientId.V1Web),
    new Claim(JwtClaimTypes.SecurityVersion, "1")
  ];

  private static List<Claim> PlatformClaims(string permission)
  {
    var claims = BaseClaims().ToList();
    claims.Add(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform));
    claims.Add(new Claim(JwtClaimTypes.Permission, permission));
    return claims;
  }

  private static List<Claim> TenantClaims(string permission)
  {
    var claims = BaseClaims().ToList();
    claims.Add(new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")));
    claims.Add(new Claim(JwtClaimTypes.TenantUserId, "22"));
    claims.Add(new Claim(JwtClaimTypes.Permission, permission));
    return claims;
  }

  private string SignToken(IEnumerable<Claim> claims)
  {
    var key = application?.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey
      ?? throw new InvalidOperationException("The test application is unavailable.");
    var now = DateTimeOffset.UtcNow;
    var token = new JwtSecurityToken(
      issuer: Issuer,
      audience: Audience,
      claims: claims,
      notBefore: now.AddMinutes(-1).UtcDateTime,
      expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }
}

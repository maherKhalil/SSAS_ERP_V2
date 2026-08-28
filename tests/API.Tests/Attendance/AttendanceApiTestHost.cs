using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.API.Tests.Employees;
using SSAS.API.Tests.Infrastructure;
using SSAS.Attendance.API;
using SSAS.Attendance.Application.Reads;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Contracts.Employment;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Attendance;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AttendanceApiEndpointGroup
{
  public const string Name = "Attendance API endpoints";
}

// ==================================================================================================
// THE ATTENDANCE TRANSPORT HARNESS (T-089) — SCOPED TO THE SELF-SERVICE SURFACE, AND SAID SO.
// ==================================================================================================
//
// ---- WHY ATTENDANCE GOT A HOST NOW AND NOT BEFORE.
//
// T-076 measured its absence: `grep -rn "api/attendance" tests/` returned zero matches, and T-077 proved by
// planting an unguarded route that the whole suite stayed green. `AttendanceRoutePermissionTests` closed the
// part of that gap reachable from route metadata alone. **FP-015 adds a surface whose correctness is not
// visible in metadata** — which employee a read resolves to, and which dimensions its scope carries — so it
// needs a host that actually answers requests.
//
// ---- IT REGISTERS WHAT THE SELF ROUTES NEED AND NOTHING ELSE, DELIBERATELY.
//
// Minimal-API handlers resolve their services at INVOCATION, so mapping the module's whole surface while
// registering only the self-service half is sound: the administrative routes are mapped, would throw if
// called, and no test here calls one. **A host stubbing all twenty-odd routes would be scaffolding for
// coverage this task does not add**, and its size would hide which registrations the feature depends on.
//
// ---- AND THE SCOPE RESOLVER IS REAL, WHICH IS THE WHOLE POINT.
//
// `AttendanceSelfServiceScopeResolver` is registered as itself, not stubbed. Stubbing it would leave these
// tests asserting that a fake returns what it was told to; what needs proving is that the REAL resolver
// refuses the wrong permission, resolves the caller's own employee, and builds a scope carrying that
// employee's own company and branch. Its two doors out of the module — `IUserEmployeeResolver` and
// `IEmployeePlacementDirectory` — are the stubs.
public sealed class AttendanceApiTestHost : IAsyncLifetime
{
  public const string Issuer = "https://ssas.tests/attendance";
  public const string Audience = "ssas-erp-api";

  public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  public static readonly Guid EmployeeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
  public static readonly Guid BranchId = Guid.Parse("55555555-5555-5555-5555-555555555555");

  private WebApplication? application;
  private HttpClient? client;

  public StubCompanyAccess CompanyAccess { get; } = new();

  public StubCompanyEstablisher CompanyContext { get; } = new();

  public StubAttendanceReads Reads { get; } = new();

  // The self-service pair, one object because a test setting one and forgetting the other would produce a
  // dangling link by accident rather than by intent.
  public StubAttendanceSelfServiceDirectory SelfService { get; } = new();

  public HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  public RouteEndpoint MappedEndpoint(string pattern) =>
    ((IEndpointRouteBuilder)(application ??
      throw new InvalidOperationException("The test host has not started."))).DataSources
      .SelectMany(source => source.Endpoints)
      .OfType<RouteEndpoint>()
      .Single(endpoint => endpoint.RoutePattern.RawText == pattern);

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
      EnvironmentName = Environments.Development
    });

    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30"
    });

    builder.Services
      .AddPlatformRequestContextForTests()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();

    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenant());
    builder.Services.AddModuleEndpointRequirements();
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    builder.Services.AddSingleton<ITenantCompanyAccessResolver>(CompanyAccess);
    builder.Services.AddSingleton<ICompanyContextEstablisher>(CompanyContext);
    builder.Services.AddSingleton<ICurrentCompany>(CompanyContext);
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<ICurrentTenantUser>(new FixedTenantUser());

    builder.Services.AddSingleton<IAttendanceReadService>(Reads);

    // The two doors out of the module that FP-015 opened, stubbed. Nothing from HR or Platform is
    // registered at all, which is the boundary demonstrated rather than asserted.
    builder.Services.AddSingleton<IUserEmployeeResolver>(SelfService);
    builder.Services.AddSingleton<IEmployeePlacementDirectory>(SelfService);
    builder.Services.AddScoped<IAttendanceSelfServiceScopeResolver, AttendanceSelfServiceScopeResolver>();

    builder.Services.AddAttendanceModule();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapAttendanceEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();

    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  public void ResetToAuthorizedState()
  {
    CompanyAccess.Permitted = [CompanyA, CompanyB];
    CompanyContext.Established = CompanyA;
    CompanyContext.Error = null;
    Reads.Reset();
    SelfService.Reset();
  }

  public static HttpRequestMessage Request(
    HttpMethod method,
    string path,
    string? token,
    string? companyHeader = "22222222-2222-2222-2222-222222222222")
  {
    var request = new HttpRequestMessage(method, path);

    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    if (companyHeader is not null)
    {
      request.Headers.Add("X-Company-Id", companyHeader);
    }

    return request;
  }

  public string TokenWith(params string[] permissions) => Token(
    permissions.Select(permission => new Claim(JwtClaimTypes.Permission, permission))
      .Prepend(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))
      .ToArray());

  public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    return document.RootElement.GetProperty("code").GetString();
  }

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");

    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;

    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "attendance-user"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new Claim(JwtClaimTypes.IdentityId, "1"),
      new Claim(JwtClaimTypes.TenantUserId, "2"),
      new Claim(JwtClaimTypes.SessionId, "3"),
      new Claim(JwtClaimTypes.ClientId, "ssas-erp-web"),
      new Claim(JwtClaimTypes.SecurityVersion, "1")
    };

    var token = new JwtSecurityToken(
      issuer: Issuer,
      audience: Audience,
      claims: requiredClaims.Concat(claims),
      notBefore: now.AddMinutes(-1).UtcDateTime,
      expires: now.AddMinutes(5).UtcDateTime,
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private sealed class ActiveTenant : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(
        tenantId, SSAS.Platform.Domain.Enums.TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => AttendanceApiTestHost.TenantId;
  }

  private sealed class FixedTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 2;
  }
}

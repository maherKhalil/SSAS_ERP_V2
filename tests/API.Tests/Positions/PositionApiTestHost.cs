using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.API.Tests.Departments;
using SSAS.API.Tests.Employees;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.HR.API;
using SSAS.HR.API.Positions;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.API.Tests.Infrastructure;

namespace SSAS.API.Tests.Positions;

// Position endpoint tests share one non-parallel collection for the same reason the Employee and Department
// ones do: their in-memory hosts each start a JWT signing-key and DataProtection stack, which flakes under
// contention.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PositionApiEndpointGroup
{
  public const string Name = "HR Position API endpoints";
}

// ==================================================================================================
// THE HTTP HARNESS FOR THE POSITION SURFACE (FP-008 Phase 4).
// ==================================================================================================
//
// ---- WHAT IS REAL HERE, AND WHY THAT MATTERS.
//
// The routes, the permission filter, the company-context filter, the REAL `PositionScopeResolver`, the real
// `EmployeeScopeResolver`, the real command and query handlers, the real error mapper and the real
// `PositionCompositionServices`. Only the collaborators that would need SQL Server are replaced — and those
// are precisely what `Integration.Tests` already proves.
//
// The two scope resolvers being real is what makes the permission tests mean something. A route's
// `RequirePermission` and the resolver's own permission check are two INDEPENDENT gates, and a harness that
// stubbed the resolver would prove only the first. Two properties depend on the second:
//
//   * the pay-band separation — `HR.Positions.View` must not reach a salary grade — is enforced by the
//     resolver refusing to mint a `SalaryGradeReadScope`, not by the route;
//   * `employeeCount` falls back to null when the caller cannot obtain an EMPLOYEE scope, which is the real
//     employee resolver refusing.
//
// ---- IT MAPS ALL FOUR NEW GROUPS, AND THE INVENTORY GUARD READS IT.
//
// `api-contracts.md` records that FP-007 shipped an unreachable route because a harness did not mirror the
// Host, and that the route-absence test which should have caught it was passing vacuously. This host calls
// the PRODUCTION mapping extensions for every group FP-008 adds, so a route mapped in `Program.cs` and
// missing here fails the exact inventory rather than going unnoticed.
public sealed class PositionApiTestHost : IAsyncLifetime
{
  public const string Issuer = "https://ssas.tests/positions";
  public const string Audience = "ssas-erp-api";

  public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  public static readonly Guid BranchA = Guid.Parse("44444444-4444-4444-4444-444444444444");

  public static readonly Guid PositionId = Guid.Parse("aaaaaaaa-0000-0000-0000-aaaaaaaaaaaa");
  public static readonly Guid JobGradeId = Guid.Parse("bbbbbbbb-0000-0000-0000-bbbbbbbbbbbb");
  public static readonly Guid SalaryGradeId = Guid.Parse("cccccccc-0000-0000-0000-cccccccccccc");
  public static readonly Guid EmployeeId = Guid.Parse("dddddddd-0000-0000-0000-dddddddddddd");

  // An identifier no stub knows, so a read of it is the scoped-absence case rather than a lookup that
  // happens to miss.
  public static readonly Guid UnknownId = Guid.Parse("ffffffff-0000-0000-0000-ffffffffffff");

  private WebApplication? application;
  private HttpClient? client;

  public StubCompanyAccess CompanyAccess { get; } = new();

  public StubBranchAccess BranchAccess { get; } = new();

  public StubCurrentBranch CurrentBranch { get; } = new();

  public StubCompanyEstablisher CompanyContext { get; } = new();

  public StubPositionReads PositionReads { get; } = new();

  public StubJobGradeReads JobGradeReads { get; } = new();

  public StubSalaryGradeReads SalaryGradeReads { get; } = new();

  public StubPositionRepository PositionRepository { get; } = new();

  public StubJobGradeRepository JobGradeRepository { get; } = new();

  public StubSalaryGradeRepository SalaryGradeRepository { get; } = new();

  public StubEmployeeReads EmployeeReads { get; } = new();

  public StubEmployeeRepository EmployeeRepository { get; } = new();

  public StubUnitOfWork UnitOfWork { get; } = new();

  public HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private static string FirstMethodOf(Microsoft.AspNetCore.Routing.RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }

  public IReadOnlyList<(string Method, string Pattern, string Policy)> MappedRoutes() =>
  [
    .. ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)(application ??
        throw new InvalidOperationException("The test host has not started."))).DataSources
      .SelectMany(source => source.Endpoints)
      .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
      .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/hr", StringComparison.Ordinal) ?? false)
      .Select(endpoint => (
        FirstMethodOf(endpoint),
        endpoint.RoutePattern.RawText!,
        endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()?.Policy ?? string.Empty))
  ];

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
    // ---- WHAT THIS HOST OWES EVERY MODULE ROUTE GROUP IT MOUNTS (T-034).
    //
    // One call, declared once in `ModuleEndpointHostRequirements`, replacing the registration each of
    // the five hosts previously spelled by hand. The module's own mapping refuses to map without it,
    // so forgetting this line is a startup failure naming the module rather than 500s per request.
    builder.Services.AddModuleEndpointRequirements();
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    // The database-backed collaborators, replaced. Everything else below is production.
    builder.Services.AddSingleton<ITenantCompanyAccessResolver>(CompanyAccess);
    builder.Services.AddSingleton<ITenantBranchAccessResolver>(BranchAccess);
    builder.Services.AddSingleton<ICurrentBranchResolver>(CurrentBranch);
    builder.Services.AddSingleton<ICompanyContextEstablisher>(CompanyContext);
    builder.Services.AddSingleton<ICurrentCompany>(CompanyContext);
    builder.Services.AddSingleton<IPositionReadService>(PositionReads);
    builder.Services.AddSingleton<IJobGradeReadService>(JobGradeReads);
    builder.Services.AddSingleton<ISalaryGradeReadService>(SalaryGradeReads);
    builder.Services.AddSingleton<IPositionRepository>(PositionRepository);
    builder.Services.AddSingleton<IJobGradeRepository>(JobGradeRepository);
    builder.Services.AddSingleton<ISalaryGradeRepository>(SalaryGradeRepository);
    builder.Services.AddSingleton<IEmployeeReadService>(EmployeeReads);
    builder.Services.AddSingleton<IEmployeeRepository>(EmployeeRepository);
    builder.Services.AddSingleton<ITenantUnitOfWork>(UnitOfWork);
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<ICurrentTenantUser>(new FixedTenantUser());
    builder.Services.AddScoped<ITenantCompanyCurrencyLookup, Employees.StubTenantCompanyCurrencyLookup>();

    // Production application wiring for everything that does not touch a database. BOTH scope resolvers are
    // real — see the header for the two properties that depend on it.
    builder.Services.AddScoped<IPositionScopeResolver, PositionScopeResolver>();
    builder.Services.AddScoped<IEmployeeScopeResolver, EmployeeScopeResolver>();

    builder.Services.AddScoped<CreatePositionCommandHandler>();
    builder.Services.AddScoped<UpdatePositionCommandHandler>();
    builder.Services.AddScoped<DeactivatePositionCommandHandler>();
    builder.Services.AddScoped<ReactivatePositionCommandHandler>();
    builder.Services.AddScoped<CreateJobGradeCommandHandler>();
    builder.Services.AddScoped<UpdateJobGradeCommandHandler>();
    builder.Services.AddScoped<DeactivateJobGradeCommandHandler>();
    builder.Services.AddScoped<ReactivateJobGradeCommandHandler>();
    builder.Services.AddScoped<CreateSalaryGradeCommandHandler>();
    builder.Services.AddScoped<UpdateSalaryGradeCommandHandler>();
    builder.Services.AddScoped<DeactivateSalaryGradeCommandHandler>();
    builder.Services.AddScoped<ReactivateSalaryGradeCommandHandler>();
    builder.Services.AddScoped<GetPositionQueryHandler>();
    builder.Services.AddScoped<SearchPositionsQueryHandler>();
    builder.Services.AddScoped<GetJobGradeQueryHandler>();
    builder.Services.AddScoped<SearchJobGradesQueryHandler>();
    builder.Services.AddScoped<GetSalaryGradeQueryHandler>();
    builder.Services.AddScoped<SearchSalaryGradesQueryHandler>();
    builder.Services.AddScoped<ChangeEmployeePositionCommandHandler>();
    builder.Services.AddScoped<GetEmployeePositionHistoryQueryHandler>();
    builder.Services.AddScoped<GetEmployeeQueryHandler>();

    builder.Services.AddHrModule();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapHrPositionEndpoints();
    application.MapHrJobGradeEndpoints();
    application.MapHrSalaryGradeEndpoints();
    application.MapHrEmployeePositionEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();

    if (application is not null)
    {
      await application.StopAsync();
      await application.DisposeAsync();
    }
  }

  // ---- THE WORLD EVERY TEST STARTS IN, RE-ESTABLISHED PER TEST.
  //
  // One authorized company and one authorized branch, and the company context established. These are the
  // AUTHORIZATION inputs, not the data: a test that wants a scope refusal narrows them deliberately, and
  // everything else can assume a caller who is inside a company and merely lacks the right permission —
  // which is what makes a 403 in these tests mean "the permission gate", not "no company".
  public void Reset()
  {
    CompanyAccess.Permitted = [CompanyA];
    BranchAccess.Permitted = [BranchA];
    CurrentBranch.BranchId = BranchA;
    CurrentBranch.Error = null;
    CompanyContext.Established = CompanyA;
    CompanyContext.Error = null;

    PositionReads.Reset();
    JobGradeReads.Reset();
    SalaryGradeReads.Reset();
    PositionRepository.Reset();
    JobGradeRepository.Reset();
    SalaryGradeRepository.Reset();
    EmployeeReads.Reset();
    EmployeeRepository.Reset();
    UnitOfWork.Failure = null;
  }

  public static HttpRequestMessage Request(
    HttpMethod method,
    string path,
    string? token,
    string? body = null,
    string? companyHeader = "22222222-2222-2222-2222-222222222222")
  {
    var request = new HttpRequestMessage(method, path);

    if (body is not null)
    {
      request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    }

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

  // ---- A TOKEN CARRYING EXACTLY THE PERMISSIONS NAMED, AND NOTHING ELSE.
  //
  // The permission-bleed tests depend on this being exact: a caller given `HR.Positions.View` must hold
  // that and only that, or "the salary grade route refused them" would prove nothing about which gate said
  // no.
  public string TokenWith(params string[] permissions) => Token(
    permissions.Select(permission => new Claim(JwtClaimTypes.Permission, permission))
      .Prepend(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))
      .ToArray());

  public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    return document.RootElement.GetProperty("code").GetString();
  }

  public static async Task<string> BodyAsync(HttpResponseMessage response) =>
    await response.Content.ReadAsStringAsync();

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");

    var credentials = new SigningCredentials(
      keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;

    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "hr-user"),
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
    public DateTimeOffset UtcNow => new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => PositionApiTestHost.TenantId;
  }

  private sealed class FixedTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 1;
  }
}

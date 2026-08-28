using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
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
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.GL.Contracts.Posting;
using SSAS.HR.Contracts.Employment;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Payroll.API;
using SSAS.Payroll.Application.Abstractions;
using SSAS.Payroll.Application.Compensation;
using SSAS.Payroll.Application.Elements;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Application.Runs;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.API.Tests.Infrastructure;

namespace SSAS.API.Tests.Payroll;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PayrollApiEndpointGroup
{
  public const string Name = "Payroll API endpoints";
}

// THE PAYROLL TRANSPORT HARNESS.
//
// Reuses the module-agnostic stubs already in `SSAS.API.Tests.Employees` — company access, the
// company-context establisher, the unit of work — and adds only what is Payroll's, exactly as GL's host
// does rather than growing a third near-duplicate.
//
// ---- NO BRANCH STUBS, AND THAT IS `DEC-PAY-0017`.
//
// HR's hosts register branch resolvers because HR reads are three-dimensional. Payroll's are two: runs are
// company-owned, periods are company-scoped, and the roster is deliberately branch-agnostic. A branch stub
// here would be scaffolding for a dimension the module does not have, and its absence is what makes that
// visible.
//
// ---- AND THE TWO CROSS-MODULE CONTRACTS ARE STUBBED, WHICH IS THE PROOF.
//
// `IJournalPoster` and `IEmployeeRoster` are the ONLY routes out of this module. That they can be replaced
// here — with no GL or HR service registered at all — is the boundary demonstrated rather than asserted.
public sealed class PayrollApiTestHost : IAsyncLifetime
{
  public const string Issuer = "https://ssas.tests/payroll";
  public const string Audience = "ssas-erp-api";

  public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  public static readonly Guid EmployeeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
  public static readonly Guid ElementId = Guid.Parse("55555555-5555-5555-5555-555555555555");
  public static readonly Guid PeriodId = Guid.Parse("66666666-6666-6666-6666-666666666666");
  public static readonly Guid RunId = Guid.Parse("77777777-7777-7777-7777-777777777777");
  public static readonly Guid AccountId = Guid.Parse("88888888-8888-8888-8888-888888888888");

  private WebApplication? application;
  private HttpClient? client;

  public StubCompanyAccess CompanyAccess { get; } = new();

  public StubCompanyEstablisher CompanyContext { get; } = new();

  public StubUnitOfWork UnitOfWork { get; } = new();

  public StubPayrollReads Reads { get; } = new();

  public StubPayElementRepository Elements { get; } = new();

  public StubCompensationRepository Compensation { get; } = new();

  public StubPayrollPeriodRepository Periods { get; } = new();

  public StubPayrollRunRepository Runs { get; } = new();

  public StubJournalPoster Ledger { get; } = new();

  public StubEmployeeRoster Roster { get; } = new();

  // The self-service pair, one object because a test setting one and forgetting the other would produce a
  // dangling link by accident rather than by intent.
  public StubSelfServiceDirectory SelfService { get; } = new();

  // FP-013's third route out of the module. See the stub for why its absence failed every test here.
  public StubAttendanceSummary Attendance { get; } = new();

  public HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private static string FirstMethodOf(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }

  // The mapped endpoint itself, for assertions about a route's CONTRACT rather than its policy — the
  // handler's MethodInfo lives in its metadata, which is the only way to reach query and header parameters.
  public RouteEndpoint MappedEndpoint(string pattern) =>
    ((IEndpointRouteBuilder)(application ??
      throw new InvalidOperationException("The test host has not started."))).DataSources
      .SelectMany(source => source.Endpoints)
      .OfType<RouteEndpoint>()
      .Single(endpoint => endpoint.RoutePattern.RawText == pattern);

  public IReadOnlyList<(string Method, string Pattern, string Policy)> MappedRoutes() =>
  [
    .. ((IEndpointRouteBuilder)(application ??
        throw new InvalidOperationException("The test host has not started."))).DataSources
      .SelectMany(source => source.Endpoints)
      .OfType<RouteEndpoint>()
      .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/payroll", StringComparison.Ordinal) ?? false)
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

    builder.Services.AddSingleton<ITenantCompanyAccessResolver>(CompanyAccess);
    builder.Services.AddSingleton<ICompanyContextEstablisher>(CompanyContext);
    builder.Services.AddSingleton<ICurrentCompany>(CompanyContext);
    builder.Services.AddSingleton<ITenantUnitOfWork>(UnitOfWork);
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<ICurrentTenantUser>(new FixedTenantUser());

    builder.Services.AddSingleton<IPayrollReadService>(Reads);
    builder.Services.AddSingleton<IPayElementRepository>(Elements);
    builder.Services.AddSingleton<IEmployeeCompensationRepository>(Compensation);
    builder.Services.AddSingleton<IPayrollPeriodRepository>(Periods);
    builder.Services.AddSingleton<IPayrollRunRepository>(Runs);

    // The two doors out of the module, and nothing else from GL or HR is registered at all.
    builder.Services.AddSingleton<IJournalPoster>(Ledger);
    builder.Services.AddSingleton<IEmployeeRoster>(Roster);
    builder.Services.AddSingleton<IAttendanceSummary>(Attendance);

    builder.Services.AddScoped<IPayrollScopeResolver, PayrollScopeResolver>();

    // ---- FP-015's SELF-SERVICE SCOPE (T-088). A THIRD DOOR OUT OF THE MODULE, STUBBED LIKE THE OTHER TWO.
    //
    // `SelfService.LinkedEmployee` decides what the caller resolves to: a value for a linked employee, null
    // for the unmapped case that must answer 404 rather than 500.
    builder.Services.AddSingleton<IUserEmployeeResolver>(SelfService);
    builder.Services.AddSingleton<IEmployeePlacementDirectory>(SelfService);
    builder.Services.AddScoped<IPayrollSelfServiceScopeResolver, PayrollSelfServiceScopeResolver>();
    builder.Services.AddScoped<CreatePayElementCommandHandler>();
    builder.Services.AddScoped<UpdatePayElementCommandHandler>();
    builder.Services.AddScoped<SetPayElementActivationCommandHandler>();
    builder.Services.AddScoped<RecordCompensationCommandHandler>();
    builder.Services.AddScoped<GeneratePayrollPeriodCommandHandler>();
    builder.Services.AddScoped<CreatePayrollRunCommandHandler>();
    builder.Services.AddScoped<CalculatePayrollRunCommandHandler>();
    builder.Services.AddScoped<ApprovePayrollRunCommandHandler>();
    builder.Services.AddScoped<PostPayrollRunCommandHandler>();
    builder.Services.AddScoped<ReversePayrollRunCommandHandler>();

    builder.Services.AddPayrollModule();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPayrollEndpoints();

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
    Elements.Reset();
    Compensation.Reset();
    Periods.Reset();
    Runs.Reset();
    Ledger.Reset();
    Roster.Reset();
    SelfService.Reset();
    Attendance.Reset();
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

  public string TokenWith(params string[] permissions) => Token(
    permissions.Select(permission => new Claim(JwtClaimTypes.Permission, permission))
      .Prepend(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))
      .ToArray());

  public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    return document.RootElement.GetProperty("code").GetString();
  }

  public static async Task<JsonDocument> DocumentAsync(HttpResponseMessage response) =>
    await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");

    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;

    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "payroll-user"),
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
    public DateTimeOffset UtcNow => new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => PayrollApiTestHost.TenantId;
  }

  private sealed class FixedTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 2;
  }
}

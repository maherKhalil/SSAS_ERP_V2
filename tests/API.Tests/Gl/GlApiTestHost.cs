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
using SSAS.GL.API;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Accounts;
using SSAS.GL.Application.Calendar;
using SSAS.GL.Application.Journals;
using SSAS.GL.Application.Reads;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.API.Tests.Infrastructure;

namespace SSAS.API.Tests.Gl;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlApiEndpointGroup
{
  public const string Name = "GL API endpoints";
}

// THE GL TRANSPORT HARNESS.
//
// Deliberately smaller than HR's two hosts, which grew separately and now duplicate ~840 lines between
// them. This one reuses the module-agnostic stubs that already exist in `SSAS.API.Tests.Employees` —
// company access, the company-context establisher, the unit of work — and adds only what is GL's.
//
// ---- NO BRANCH STUBS ANYWHERE, AND THAT IS `OD-GL-0005`.
//
// HR's hosts register `ITenantBranchAccessResolver` and `ICurrentBranchResolver` because HR reads are
// three-dimensional. GL's are two. A branch stub here would be scaffolding for a dimension the module does
// not have, and its absence is what makes that visible.
public sealed class GlApiTestHost : IAsyncLifetime
{
  public const string Issuer = "https://ssas.tests/gl";
  public const string Audience = "ssas-erp-api";

  public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  public static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
  public static readonly Guid DraftId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
  public static readonly Guid JournalId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
  public static readonly Guid PeriodId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

  private WebApplication? application;
  private HttpClient? client;

  public StubCompanyAccess CompanyAccess { get; } = new();

  public StubCompanyEstablisher CompanyContext { get; } = new();

  public StubUnitOfWork UnitOfWork { get; } = new();

  public StubGlReads Reads { get; } = new();

  public StubAccountRepository Accounts { get; } = new();

  public StubCalendarRepository Calendar { get; } = new();

  public StubFiscalYearDefinitionLock CalendarLock { get; } = new();

  public StubJournalDraftRepository Drafts { get; } = new();

  public StubJournalEntryRepository Journals { get; } = new();

  public HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private static string FirstMethodOf(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }

  public IReadOnlyList<(string Method, string Pattern, string Policy)> MappedRoutes() =>
  [
    .. ((IEndpointRouteBuilder)(application ??
        throw new InvalidOperationException("The test host has not started."))).DataSources
      .SelectMany(source => source.Endpoints)
      .OfType<RouteEndpoint>()
      .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/gl", StringComparison.Ordinal) ?? false)
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
    builder.Services.AddSingleton<ITenantCompanyCurrencyLookup, StubTenantCompanyCurrencyLookup>();

    builder.Services.AddSingleton<IGlReadService>(Reads);
    builder.Services.AddSingleton<IAccountRepository>(Accounts);
    builder.Services.AddSingleton<IFiscalCalendarRepository>(Calendar);
    builder.Services.AddSingleton<IFiscalYearDefinitionLock>(CalendarLock);
    builder.Services.AddSingleton<IJournalDraftRepository>(Drafts);
    builder.Services.AddSingleton<IJournalEntryRepository>(Journals);

    builder.Services.AddScoped<IGlScopeResolver, GlScopeResolver>();
    builder.Services.AddScoped<CreateAccountCommandHandler>();
    builder.Services.AddScoped<RenameAccountCommandHandler>();
    builder.Services.AddScoped<SetAccountActivationCommandHandler>();
    builder.Services.AddScoped<DefineFiscalYearCommandHandler>();
    builder.Services.AddScoped<SetFiscalPeriodStateCommandHandler>();
    builder.Services.AddScoped<CreateJournalDraftCommandHandler>();
    builder.Services.AddScoped<UpdateJournalDraftCommandHandler>();
    builder.Services.AddScoped<DiscardJournalDraftCommandHandler>();
    builder.Services.AddScoped<PostJournalDraftCommandHandler>();
    builder.Services.AddScoped<ReverseJournalCommandHandler>();

    builder.Services.AddGlModule();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapGlEndpoints();

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
    Accounts.Reset();
    Calendar.Reset();
    CalendarLock.Reset();
    Drafts.Reset();
    Journals.Reset();
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
      new Claim(JwtClaimTypes.Subject, "gl-user"),
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
    public DateTimeOffset UtcNow => new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => GlApiTestHost.TenantId;
  }

  private sealed class FixedTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 2;
  }
}

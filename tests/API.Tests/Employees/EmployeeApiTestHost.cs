using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.HR.API;
using SSAS.HR.API.Departments;
using SSAS.HR.API.Employees;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Employees;

// Employee endpoint tests share one non-parallel collection for the same reason the Company ones do: their
// in-memory hosts each start a JWT signing-key and DataProtection stack, which flakes under contention.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EmployeeApiEndpointGroup
{
  public const string Name = "HR Employee API endpoints";
}

// ==================================================================================================
// THE HTTP HARNESS FOR THE EMPLOYEE SURFACE (FP-006C5).
// ==================================================================================================
//
// ---- WHAT IS REAL HERE, AND WHY THAT MATTERS.
//
// The authentication pipeline, the permission policy provider, the endpoint mapping, the strict JSON
// reader, the rowversion codec, the ProblemDetails projection, the error mapper, the query handlers and
// THE SCOPE RESOLVER are all production types. The scope resolver especially: it is what proves that a
// tenant administrator without HR.Employees.View is refused, and stubbing it would have made that test
// assert the stub.
//
// ---- WHAT IS STUBBED, AND WHY THAT IS NOT A HOLE.
//
// The two access resolvers, the branch resolver, the company establisher, the repository, the unit of work
// and the read service — everything whose real implementation needs a database. Their BEHAVIOUR is proven
// against real SQL Server in Integration.Tests; what these tests answer is the different question of
// whether the HTTP layer carries the right status code, body and concealment for each answer they can give.
//
// Host composition itself is never stubbed — that is proven separately in EmployeeHostCompositionTests.
public sealed class EmployeeApiTestHost : IAsyncLifetime
{
  public const string Issuer = "https://ssas.tests/employees";
  public const string Audience = "ssas-erp-api";

  public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  public static readonly Guid BranchA = Guid.Parse("44444444-4444-4444-4444-444444444444");
  public static readonly Guid BranchB = Guid.Parse("55555555-5555-5555-5555-555555555555");
  public static readonly Guid BranchC = Guid.Parse("66666666-6666-6666-6666-666666666666");
  public static readonly Guid EmployeeId = Guid.Parse("77777777-7777-7777-7777-777777777777");

  // FP-007 Phase 3. DepartmentA is Active and in CompanyA; DepartmentInactive and DepartmentOtherCompany
  // exist so the create contract's refusals can be exercised at the HTTP layer.
  public static readonly Guid DepartmentA = Guid.Parse("88888888-8888-8888-8888-888888888888");
  public static readonly Guid DepartmentB = Guid.Parse("bbbbbbbb-0000-0000-0000-bbbbbbbbbbbb");
  public static readonly Guid DepartmentInactive = Guid.Parse("99999999-9999-9999-9999-999999999999");
  public static readonly Guid DepartmentOtherCompany = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

  private WebApplication? application;
  private HttpClient? client;

  public StubCompanyAccess CompanyAccess { get; } = new();

  public StubBranchAccess BranchAccess { get; } = new();

  public StubCurrentBranch CurrentBranch { get; } = new();

  public StubCompanyEstablisher CompanyContext { get; } = new();

  public StubEmployeeReads Reads { get; } = new();

  public StubEmployeeRepository Repository { get; } = new();

  public StubUnitOfWork UnitOfWork { get; } = new();

  public HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  // ---- THE ROUTES THIS HOST ACTUALLY MAPPED.
  //
  // Read back from the built application rather than from the source, so the inventory guard sees what
  // routing registered. Because this harness calls the PRODUCTION mapping extensions, a route added to the
  // module and not mapped here goes missing from the inventory and the guard fails — which is exactly the
  // gap that let Phase 4 ship a route no test could reach.
  public IReadOnlyList<(string Method, string Pattern, string Policy)> MappedRoutes() =>
  [
    .. ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)(application ??
        throw new InvalidOperationException("The test host has not started."))).DataSources
      .SelectMany(source => source.Endpoints)
      .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
      .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/hr", StringComparison.Ordinal) ?? false)
      .Select(endpoint => (
        endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "?",
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
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();

    // The sanctioned transfer channel, stood in for. The real one is internal to Platform.Infrastructure
    // and its SEMANTICS are proven against real SQL in the C2 and C3 boundary suites. What matters here is
    // that the command opens a scope at all rather than mutating BranchId directly, which this records.
    builder.Services.AddScoped<IBranchTransferScope, RecordingTransferScope>();

    // The database-backed collaborators, replaced. Everything else below is production.
    builder.Services.AddSingleton<ITenantCompanyAccessResolver>(CompanyAccess);
    builder.Services.AddSingleton<ITenantBranchAccessResolver>(BranchAccess);
    builder.Services.AddSingleton<ICurrentBranchResolver>(CurrentBranch);
    builder.Services.AddSingleton<ICompanyContextEstablisher>(CompanyContext);
    builder.Services.AddSingleton<ICurrentCompany>(CompanyContext);
    builder.Services.AddSingleton<IEmployeeReadService>(Reads);
    builder.Services.AddSingleton<IEmployeeRepository>(Repository);
    builder.Services.AddSingleton<ITenantUnitOfWork>(UnitOfWork);
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<ICurrentTenantUser>(new FixedTenantUser());

    // Production application wiring for everything that does not touch a database.
    builder.Services.AddScoped<IEmployeeScopeResolver, EmployeeScopeResolver>();
    builder.Services.AddScoped<CreateEmployeeCommandHandler>();
    builder.Services.AddScoped<UpdateEmployeeProfileCommandHandler>();
    builder.Services.AddScoped<TerminateEmployeeCommandHandler>();
    builder.Services.AddScoped<TransferEmployeeCommandHandler>();
    // FP-007 Phase 4: the change-department route resolves this per request, so the harness registers it
    // exactly as AddHrInfrastructure does in the Host.
    builder.Services.AddScoped<ChangeEmployeeDepartmentCommandHandler>();
    builder.Services.AddScoped<ActivateEmployeeCommandHandler>();
    builder.Services.AddScoped<DeactivateEmployeeCommandHandler>();
    builder.Services.AddScoped<GetEmployeeQueryHandler>();
    builder.Services.AddScoped<SearchEmployeesQueryHandler>();
    builder.Services.AddScoped<GetEmployeeBranchHistoryQueryHandler>();
    builder.Services.AddHrModule();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapHrEmployeeEndpoints();
    // FP-007 Phase 4: the change-department route lives on the employee prefix and is mapped by the
    // department module, so the harness must map it too — otherwise a test asserting its behaviour would
    // be describing the harness rather than the Host.
    application.MapHrEmployeeDepartmentEndpoints();

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
    BranchAccess.Permitted = [BranchA, BranchB];
    CurrentBranch.BranchId = BranchA;
    CurrentBranch.Error = null;
    CompanyContext.Established = CompanyA;
    CompanyContext.Error = null;
    Reads.Reset();
    Repository.Reset();
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

  public static async Task<string> BodyAsync(HttpResponseMessage response) =>
    await response.Content.ReadAsStringAsync();

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");

    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
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
    public DateTimeOffset UtcNow => new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => EmployeeApiTestHost.TenantId;
  }

  private sealed class FixedTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 2;
  }
}

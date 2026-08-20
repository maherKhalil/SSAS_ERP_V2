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
using SSAS.HR.API.Departments;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Departments;

// Department endpoint tests share one non-parallel collection for the same reason the Employee ones do:
// their in-memory hosts each start a JWT signing-key and DataProtection stack, which flakes under
// contention.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DepartmentApiEndpointGroup
{
  public const string Name = "HR Department API endpoints";
}

// ==================================================================================================
// THE HTTP HARNESS FOR THE DEPARTMENT SURFACE (FP-007 Phase 4).
// ==================================================================================================
//
// ---- WHAT IS REAL HERE, AND WHY THAT MATTERS.
//
// The routes, the permission filter, the company-context filter, the REAL DepartmentScopeResolver, the
// real command and query handlers, and the real error mapper. Only the collaborators that would need SQL
// Server are replaced — and those are precisely what Integration.Tests already proves.
//
// The scope resolver being real is what makes the permission tests mean something: a route's
// RequirePermission and the resolver's own permission check are two independent gates, and a test that
// stubbed the resolver would prove only the first. The permission-bleed tests below depend on the second.
public sealed class DepartmentApiTestHost : IAsyncLifetime
{
  public const string Issuer = "https://ssas.tests/departments";
  public const string Audience = "ssas-erp-api";

  public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  public static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  public static readonly Guid BranchA = Guid.Parse("44444444-4444-4444-4444-444444444444");
  public static readonly Guid DepartmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
  public static readonly Guid ParentDepartmentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
  public static readonly Guid ManagerEmployeeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

  private WebApplication? application;
  private HttpClient? client;

  public StubCompanyAccess CompanyAccess { get; } = new();

  public StubBranchAccess BranchAccess { get; } = new();

  public StubCurrentBranch CurrentBranch { get; } = new();

  public StubCompanyEstablisher CompanyContext { get; } = new();

  public StubDepartmentReads Reads { get; } = new();

  public StubDepartmentRepository Repository { get; } = new();

  public StubEmployeeReads EmployeeReads { get; } = new();

  public StubEmployeeRepository EmployeeRepository { get; } = new();

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

    // The database-backed collaborators, replaced. Everything else below is production.
    builder.Services.AddSingleton<ITenantCompanyAccessResolver>(CompanyAccess);
    builder.Services.AddSingleton<ITenantBranchAccessResolver>(BranchAccess);
    builder.Services.AddSingleton<ICurrentBranchResolver>(CurrentBranch);
    builder.Services.AddSingleton<ICompanyContextEstablisher>(CompanyContext);
    builder.Services.AddSingleton<ICurrentCompany>(CompanyContext);
    builder.Services.AddSingleton<IDepartmentReadService>(Reads);
    builder.Services.AddSingleton<IDepartmentRepository>(Repository);
    builder.Services.AddSingleton<IEmployeeReadService>(EmployeeReads);
    // AssignDepartmentManagerCommandHandler validates the nominated employee through the employee
    // repository — same tenant, same company, not terminated — so the department host needs it too.
    builder.Services.AddSingleton<IEmployeeRepository>(EmployeeRepository);
    builder.Services.AddSingleton<ITenantUnitOfWork>(UnitOfWork);
    builder.Services.AddSingleton<ICurrentTenant>(new FixedTenant());
    builder.Services.AddSingleton<ICurrentTenantUser>(new FixedTenantUser());

    // ---- THE HIERARCHY LOCK, STOOD IN FOR.
    //
    // The real one takes sp_getapplock on a live transaction; its semantics are proven against real SQL in
    // the Phase 2 concurrency suite. Here it must simply grant, so the hierarchy routes reach their
    // handlers and the HTTP contract is what gets tested.
    builder.Services.AddSingleton<IDepartmentHierarchyLock>(new GrantingHierarchyLock());

    // Production application wiring for everything that does not touch a database.
    builder.Services.AddScoped<IDepartmentScopeResolver, DepartmentScopeResolver>();
    builder.Services.AddScoped<IEmployeeScopeResolver, EmployeeScopeResolver>();
    builder.Services.AddScoped<CreateDepartmentCommandHandler>();
    builder.Services.AddScoped<UpdateDepartmentCommandHandler>();
    builder.Services.AddScoped<ChangeDepartmentParentCommandHandler>();
    builder.Services.AddScoped<MoveDepartmentToRootCommandHandler>();
    builder.Services.AddScoped<DeactivateDepartmentCommandHandler>();
    builder.Services.AddScoped<ReactivateDepartmentCommandHandler>();
    builder.Services.AddScoped<AssignDepartmentManagerCommandHandler>();
    builder.Services.AddScoped<ClearDepartmentManagerCommandHandler>();
    builder.Services.AddScoped<GetDepartmentQueryHandler>();
    builder.Services.AddScoped<SearchDepartmentsQueryHandler>();
    builder.Services.AddScoped<GetDepartmentChildrenQueryHandler>();
    builder.Services.AddHrModule();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapHrDepartmentEndpoints();

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
    BranchAccess.Permitted = [BranchA];
    CurrentBranch.BranchId = BranchA;
    CurrentBranch.Error = null;
    CompanyContext.Established = CompanyA;
    CompanyContext.Error = null;
    Reads.Reset();
    Repository.Reset();
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

  // Grants unconditionally. What it must NOT do is decide anything: the lock's real job is serialising
  // concurrent hierarchy mutations, which has no meaning in a single-request HTTP test.
  private sealed class GrantingHierarchyLock : IDepartmentHierarchyLock
  {
    public Task<SSAS.BuildingBlocks.Domain.Result> AcquireAsync(
      Guid tenantId, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success());
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
    public Guid? TenantId => DepartmentApiTestHost.TenantId;
  }

  private sealed class FixedTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 2;
  }
}

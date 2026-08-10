using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.API.Companies;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Companies;

// Proves the Company read routes (GET list + GET by id) end-to-end through the REAL Host
// authentication + authorization pipeline, the shared strict-query parsing, and the real list/detail
// handlers. The database is replaced by a recording read-service stub whose GetById returns null for
// any id other than the current tenant's, modelling the inherited tenant query filter.
[Collection(CompanyApiEndpointGroup.Name)]
public sealed class CompaniesReadEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://companies-read.tests";
  private const string Audience = "companies-read-tests";
  private static readonly Guid TenantId = Guid.Parse("3d5b8c1a-6e2f-4a7b-9c04-1f8a2e6d5b93");
  private static readonly Guid OwnedCompanyId = Guid.Parse("9a1c2b3d-4e5f-4061-8273-6a5b4c3d2e1f");
  private static readonly Guid OtherTenantCompanyId = Guid.Parse("11111111-2222-3333-4444-555555555555");
  private WebApplication? application;
  private HttpClient? client;
  private StubCompanyReadService readService = new();

  // ---- list authorization ----

  [Fact]
  public async Task List_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get("/api/platform/companies", token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(readService.ListCalled);
  }

  [Fact]
  public async Task List_without_view_permission_returns_403()
  {
    var response = await Client.SendAsync(Get("/api/platform/companies", Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(readService.ListCalled);
  }

  // ---- list happy path + defaults ----

  [Fact]
  public async Task Authorized_default_list_returns_200_with_default_paging_and_safe_projection()
  {
    var response = await Client.SendAsync(Get("/api/platform/companies", ViewToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, readService.LastPageNumber);
    Assert.Equal(50, readService.LastPageSize);
    Assert.Null(readService.LastStatus);
    var page = await response.Content.ReadFromJsonAsync<CompanyPageResponse>();
    Assert.NotNull(page);
    var item = Assert.Single(page!.Items);
    Assert.Equal("ACME-EG", item.CompanyCode);
    Assert.Equal("AQIDBAUGBwg=", item.RowVersion);
    var raw = await ReadRawAsync(response);
    Assert.DoesNotContain("tenantId", raw, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("normalizedCompanyCode", raw, StringComparison.OrdinalIgnoreCase);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Explicit_pagination_is_passed_through()
  {
    var response = await Client.SendAsync(Get("/api/platform/companies?pageNumber=2&pageSize=25", ViewToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(2, readService.LastPageNumber);
    Assert.Equal(25, readService.LastPageSize);
  }

  [Fact]
  public async Task Valid_status_filter_is_passed_through()
  {
    var response = await Client.SendAsync(Get("/api/platform/companies?status=Active", ViewToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(CompanyStatus.Active, readService.LastStatus);
  }

  // ---- list rejections ----

  [Theory]
  [InlineData("/api/platform/companies?pageNumber=abc")]     // malformed pageNumber
  [InlineData("/api/platform/companies?pageSize=-1")]        // malformed pageSize
  [InlineData("/api/platform/companies?pageSize=250")]       // over the maximum (handler-bounded)
  [InlineData("/api/platform/companies?status=Suspended")]   // undefined company status
  [InlineData("/api/platform/companies?status=active")]      // wrong casing
  [InlineData("/api/platform/companies?status=0")]           // numeric enum
  [InlineData("/api/platform/companies?unknown=1")]          // unknown query key
  [InlineData("/api/platform/companies?tenantId=x")]         // caller tenant id
  [InlineData("/api/platform/companies?pageNumber=1&pageNumber=2")] // duplicate key
  public async Task Invalid_list_query_returns_400_request_invalid(string url)
  {
    var response = await Client.SendAsync(Get(url, ViewToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
  }

  // ---- detail ----

  [Fact]
  public async Task Detail_without_token_returns_401()
  {
    var response = await Client.SendAsync(Get($"/api/platform/companies/{OwnedCompanyId}", token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Detail_without_view_permission_returns_403()
  {
    var response = await Client.SendAsync(Get($"/api/platform/companies/{OwnedCompanyId}", Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Detail_for_an_owned_company_returns_200_with_the_safe_projection()
  {
    var response = await Client.SendAsync(Get($"/api/platform/companies/{OwnedCompanyId}", ViewToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
    Assert.NotNull(body);
    Assert.Equal(OwnedCompanyId, body!.CompanyId);
    Assert.Equal("ACME-EG", body.CompanyCode);
    Assert.Equal("AQIDBAUGBwg=", body.RowVersion);
    var raw = await ReadRawAsync(response);
    Assert.DoesNotContain("tenantId", raw, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("normalizedCompanyCode", raw, StringComparison.OrdinalIgnoreCase);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Detail_for_an_unknown_company_returns_404_company_not_found()
  {
    var response = await Client.SendAsync(Get($"/api/platform/companies/{Guid.NewGuid()}", ViewToken()));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    await AssertProblemAsync(response, "company.not_found");
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Detail_for_a_cross_tenant_company_is_indistinguishable_from_unknown()
  {
    // The read service (tenant-filtered) returns null for a company owned by another tenant.
    var crossTenant = await Client.SendAsync(Get($"/api/platform/companies/{OtherTenantCompanyId}", ViewToken()));
    var unknown = await Client.SendAsync(Get($"/api/platform/companies/{Guid.NewGuid()}", ViewToken()));

    Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    await AssertProblemAsync(crossTenant, "company.not_found");
    await AssertProblemAsync(unknown, "company.not_found");
  }

  [Fact]
  public async Task Detail_with_a_malformed_company_id_returns_400()
  {
    var response = await Client.SendAsync(Get("/api/platform/companies/not-a-guid", ViewToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    readService = new StubCompanyReadService();
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddSingleton<ICompanyReadService>(readService);
    builder.Services.AddCompanyEndpointHandlers();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformCompanyEndpoints();

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

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private static HttpRequestMessage Get(string url, string? token)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    return request;
  }

  private string ViewToken() => Token(
    new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewCompanies));

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;
    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "company-reader"),
      new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new Claim("iat", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
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

  private static Task<string> ReadRawAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync();

  private static void AssertSecurityHeaders(HttpResponseMessage response)
  {
    Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedCode)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
  }

  private sealed class StubCompanyReadService : ICompanyReadService
  {
    public bool ListCalled { get; private set; }
    public int LastPageNumber { get; private set; }
    public int LastPageSize { get; private set; }
    public CompanyStatus? LastStatus { get; private set; }

    public Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(companyId == OwnedCompanyId ? Company(companyId) : null);

    public Task<PagedResult<CompanyDto>> ListAsync(CompanyStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
      ListCalled = true;
      LastStatus = status;
      LastPageNumber = pageNumber;
      LastPageSize = pageSize;
      return Task.FromResult(new PagedResult<CompanyDto>([Company(OwnedCompanyId)!], pageNumber, pageSize, 1));
    }

    private static CompanyDto Company(Guid id)
    {
      var moment = new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);
      return new CompanyDto(id, TenantId, "ACME-EG", "Acme Egypt", "EGP", CompanyStatus.Inactive,
        moment, "company-admin", moment, "company-admin", moment, "company-admin",
        CompanyStatusChangeReason.Created, [1, 2, 3, 4, 5, 6, 7, 8]);
    }
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }
}

using SSAS.BuildingBlocks.Tenancy.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
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
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.API.Companies;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Companies;

// Proves POST /api/platform/companies end-to-end through the REAL Host authentication +
// authorization pipeline, the shared StrictRequestReader on a real body, the CompanyApiErrorMapper,
// and the real CreateCompanyCommandHandler. The database is replaced by recording stubs; the
// handler still derives the trusted tenant and validates value objects, and no caller-supplied
// tenant is accepted.
[Collection(CompanyApiEndpointGroup.Name)]
public sealed class CompaniesEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://companies.tests";
  private const string Audience = "companies-tests";
  private const string ValidBody = "{\"companyCode\":\"ACME-EG\",\"companyName\":\"Acme Egypt\",\"baseCurrencyCode\":\"EGP\"}";
  private static readonly Guid TenantId = Guid.Parse("6c2a1f7e-2d4b-4c8a-9f13-7b0e5a2c9d81");
  private WebApplication? application;
  private HttpClient? client;
  private StubCompanyRepository repository = new();
  private StubCompanyReadService readService = new();
  private StubUnitOfWork unitOfWork = new();

  // ---- authorization plane ----

  [Fact]
  public async Task Unauthenticated_request_returns_401()
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody, token: null));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(repository.CreateChecked);
  }

  [Fact]
  public async Task Authenticated_without_manage_permission_returns_403()
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody,
      Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()))));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.False(repository.CreateChecked);
  }

  [Fact]
  public async Task Missing_trusted_tenant_claim_is_rejected_as_an_invalid_token()
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody,
      Token(new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ManageCompanies))));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.False(repository.CreateChecked);
  }

  // ---- authorized success ----

  [Fact]
  public async Task Authorized_create_returns_201_with_the_safe_projection_and_location()
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody, ManageToken()));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.True(repository.CreateAdded);
    Assert.Equal(1, unitOfWork.SaveCount);
    Assert.Equal($"/api/platform/companies/{readService.CompanyId}", response.Headers.Location?.ToString());
    var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
    Assert.NotNull(body);
    Assert.Equal(readService.CompanyId, body!.CompanyId);
    Assert.Equal("ACME-EG", body.CompanyCode);
    Assert.Equal("Inactive", body.Status);
    Assert.Equal("Created", body.StatusChangeReasonCode);
    Assert.Equal("AQIDBAUGBwg=", body.RowVersion); // canonical padded Base64 via the shared codec
    AssertSecurityHeaders(response);
  }

  // ---- strict JSON (closes the StrictRequestReader M1 concern for Company) ----

  [Theory]
  [InlineData("{\"companyCode\":\"ACME-EG\",\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\",\"tenantId\":\"x\"}")] // caller tenant id
  [InlineData("{\"companyCode\":\"ACME-EG\",\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\",\"status\":\"Active\"}")] // unknown field
  [InlineData("{\"companyCode\":\"ACME-EG\",\"companyCode\":\"OTHER\",\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\"}")] // duplicate
  [InlineData("{\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\"}")] // missing companyCode
  [InlineData("{\"companyCode\":\"ACME-EG\",\"baseCurrencyCode\":\"EGP\"}")] // missing companyName
  [InlineData("{\"companyCode\":\"ACME-EG\",\"companyName\":\"Acme\"}")] // missing baseCurrencyCode
  [InlineData("{\"companyCode\":123,\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\"}")] // numeric code
  [InlineData("{\"CompanyCode\":\"ACME-EG\",\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\"}")] // case mismatch
  [InlineData("[]")] // non-object root
  [InlineData("{\"companyCode\":")] // malformed
  public async Task Strict_json_rejects_invalid_bodies_with_400_without_invoking_the_handler(string body)
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", body, ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.False(repository.CreateChecked);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Wrong_content_type_is_rejected_with_400()
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody, ManageToken(), contentType: "text/plain"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.False(repository.CreateChecked);
  }

  // ---- domain validation surfaced over HTTP ----

  [Theory]
  [InlineData("{\"companyCode\":\"\",\"companyName\":\"Acme\",\"baseCurrencyCode\":\"EGP\"}")] // blank code
  [InlineData("{\"companyCode\":\"ACME-EG\",\"companyName\":\"   \",\"baseCurrencyCode\":\"EGP\"}")] // blank name
  [InlineData("{\"companyCode\":\"ACME-EG\",\"companyName\":\"Acme\",\"baseCurrencyCode\":\"ZZZ\"}")] // non-ISO currency
  public async Task Domain_validation_failures_map_to_400_request_invalid(string body)
  {
    var response = await Client.SendAsync(Post("/api/platform/companies", body, ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.False(repository.CreateAdded);
  }

  // ---- duplicate normalized code ----

  [Fact]
  public async Task Duplicate_normalized_code_maps_to_409_company_code_conflict()
  {
    repository.CodeExists = true;

    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody, ManageToken()));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await AssertProblemAsync(response, "company.code_conflict");
    Assert.False(repository.CreateAdded);
  }

  [Fact]
  public async Task Database_unique_violation_on_save_maps_to_409_company_code_conflict()
  {
    unitOfWork.Failure = IdentityAccessErrors.UniqueConstraintViolation;

    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody, ManageToken()));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await AssertProblemAsync(response, "company.code_conflict");
  }

  [Fact]
  public async Task Persistence_write_failure_maps_to_a_safe_500()
  {
    unitOfWork.Failure = IdentityAccessErrors.WriteFailure;

    var response = await Client.SendAsync(Post("/api/platform/companies", ValidBody, ManageToken()));

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    var payload = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("Persistence", payload, StringComparison.Ordinal); // no internal code/message leaked
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
    repository = new StubCompanyRepository();
    readService = new StubCompanyReadService();
    unitOfWork = new StubUnitOfWork();
    builder.Services
      .AddPlatformRequestContext()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails();
    builder.Services.AddSingleton<ITenantAuthenticationEligibilityReadService>(new ActiveTenantEligibility());
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddSingleton<IDateTimeProvider>(new FixedClock());
    builder.Services.AddSingleton<ICompanyRepository>(repository);
    builder.Services.AddSingleton<ICompanyReadService>(readService);
    builder.Services.AddSingleton<ITenantUnitOfWork>(unitOfWork);
    // The shared MapPlatformCompanyEndpoints maps the full route family, so every handler must be
    // resolvable even though this class only exercises create.
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

  private static HttpRequestMessage Post(string path, string body, string? token, string contentType = "application/json")
  {
    var request = new HttpRequestMessage(HttpMethod.Post, path)
    {
      Content = new StringContent(body, Encoding.UTF8, contentType)
    };
    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    return request;
  }

  private string ManageToken() => Token(
    new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ManageCompanies));

  private string Token(params Claim[] claims)
  {
    var keyProvider = application?.Services.GetRequiredService<ISigningKeyProvider>() ??
      throw new InvalidOperationException("The test application is unavailable.");
    var credentials = new SigningCredentials(keyProvider.Snapshot.ActiveSigningKey, SecurityAlgorithms.RsaSha256);
    var now = DateTimeOffset.UtcNow;
    var requiredClaims = new[]
    {
      new Claim(JwtClaimTypes.Subject, "company-admin"),
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

  private sealed class StubCompanyRepository : ICompanyRepository
  {
    public bool CodeExists { get; set; }
    public bool CreateChecked { get; private set; }
    public bool CreateAdded { get; private set; }

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult<Company?>(null);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCompanyCode, CancellationToken cancellationToken = default)
    {
      CreateChecked = true;
      return Task.FromResult(CodeExists);
    }

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
      CreateAdded = true;
      return Task.CompletedTask;
    }
  }

  private sealed class StubCompanyReadService : ICompanyReadService
  {
    public Guid CompanyId { get; private set; }

    public Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
      CompanyId = companyId;
      var dto = new CompanyDto(
        companyId, TenantId, "ACME-EG", "Acme Egypt", "EGP", CompanyStatus.Inactive,
        new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero), "company-admin",
        new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero), "company-admin",
        new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero), "company-admin",
        CompanyStatusChangeReason.Created, [1, 2, 3, 4, 5, 6, 7, 8]);
      return Task.FromResult<CompanyDto?>(dto);
    }

    public Task<PagedResult<CompanyDto>> ListAsync(CompanyStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class StubUnitOfWork : ITenantUnitOfWork
  {
    public Error? Failure { get; set; }
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Failure is { } error ? Result.Failure<int>(error) : Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class ActiveTenantEligibility : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);
  }
}

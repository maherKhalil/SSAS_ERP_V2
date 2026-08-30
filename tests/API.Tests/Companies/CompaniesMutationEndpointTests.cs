using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Companies;

// Proves the Company mutation routes (PUT profile + activate/deactivate/archive) end-to-end through
// the REAL Host authentication + authorization pipeline, the shared strict-body reader, the shared
// RowVersionCodec (malformed vs stale distinction), and the real mutation handlers. The database is
// replaced by an in-memory aggregate repository + read-service stub; GetById returns null for any
// company not owned by the current tenant, modelling the inherited tenant query filter.
[Collection(CompanyApiEndpointGroup.Name)]
public sealed class CompaniesMutationEndpointTests : IAsyncLifetime
{
  private const string Issuer = "https://companies-mutation.tests";
  private const string Audience = "companies-mutation-tests";
  private static readonly Guid TenantId = Guid.Parse("7f4d2a1e-8b3c-4e6f-9a05-2c1b7d8e6f40");
  private static readonly DateTimeOffset Now = new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);
  private static readonly byte[] CurrentRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
  private const string CurrentRowVersionText = "AQIDBAUGBwg=";
  private static readonly string StaleRowVersionText = Convert.ToBase64String([8, 7, 6, 5, 4, 3, 2, 1]);

  private WebApplication? application;
  private HttpClient? client;
  private FakeCompanyRepository repository = new();
  private FakeCompanyReadService readService = new();
  private FakeUnitOfWork unitOfWork = new();

  // ---------- PUT profile ----------

  [Fact]
  public async Task Update_without_token_returns_401()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody(), token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Update_without_manage_permission_returns_403()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody(),
      Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()), new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ViewCompanies))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Update_with_matching_rowversion_returns_200_with_the_updated_projection()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody("Acme Egypt LLC"), ManageToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
    Assert.Equal(id, body!.CompanyId);
    Assert.Equal(CurrentRowVersionText, body.RowVersion);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Update_of_an_unknown_or_cross_tenant_company_returns_404()
  {
    // No aggregate is seeded, modelling a company owned by another tenant (filtered to null).
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{Guid.NewGuid()}", UpdateBody(), ManageToken()));
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    await AssertProblemAsync(response, "company.not_found");
  }

  [Theory]
  [InlineData("AQIDBAUGBwg")]   // missing padding (non-canonical)
  [InlineData("AQIDBAUGBwg_")]  // Base64Url
  [InlineData("0102030405060708")] // hexadecimal
  [InlineData("AQIDBAUGBw==")]  // 6 bytes
  public async Task Update_with_a_malformed_rowversion_returns_400_platform_rowversion_invalid(string rowVersion)
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody(rowVersion: rowVersion), ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "platform.rowversion_invalid");
  }

  [Fact]
  public async Task Update_with_a_valid_stale_rowversion_returns_409_concurrency_conflict()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody(rowVersion: StaleRowVersionText), ManageToken()));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await AssertProblemAsync(response, "concurrency.conflict");
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Update_missing_rowversion_returns_400_request_invalid()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", "{\"companyName\":\"New\"}", ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
  }

  [Theory]
  [InlineData("companyCode")]
  [InlineData("baseCurrencyCode")]
  [InlineData("tenantId")]
  [InlineData("status")]
  public async Task Update_rejects_attempts_to_mutate_immutable_fields(string field)
  {
    var id = SeedInactive();
    var body = $"{{\"companyName\":\"New\",\"expectedRowVersion\":\"{CurrentRowVersionText}\",\"{field}\":\"x\"}}";
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", body, ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Update_with_a_blank_company_name_returns_400_request_invalid()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody("   "), ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
  }

  [Fact]
  public async Task Update_persistence_write_failure_returns_a_safe_500()
  {
    var id = SeedInactive();
    unitOfWork.Failure = IdentityAccessErrors.WriteFailure;
    var response = await Client.SendAsync(Request(HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody(), ManageToken()));

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.DoesNotContain("Persistence", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
  }

  // ---------- lifecycle ----------

  [Theory]
  [InlineData("activate", CompanyStatus.Inactive)]
  [InlineData("deactivate", CompanyStatus.Active)]
  [InlineData("archive", CompanyStatus.Inactive)]
  public async Task Lifecycle_happy_path_returns_200(string action, CompanyStatus startStatus)
  {
    var id = Seed(startStatus);
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/{action}", LifecycleBody(), LifecycleToken()));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, unitOfWork.SaveCount);
    AssertSecurityHeaders(response);
  }

  [Fact]
  public async Task Lifecycle_without_lifecycle_permission_returns_403()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/activate", LifecycleBody(),
      Token(new Claim(JwtClaimTypes.TenantId, TenantId.ToString()), new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ManageCompanies))));
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Lifecycle_without_token_returns_401()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/activate", LifecycleBody(), token: null));
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Lifecycle_for_an_unknown_or_cross_tenant_company_returns_404()
  {
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{Guid.NewGuid()}/activate", LifecycleBody(), LifecycleToken()));
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    await AssertProblemAsync(response, "company.not_found");
  }

  [Fact]
  public async Task Lifecycle_with_malformed_rowversion_returns_400_platform_rowversion_invalid()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/activate", LifecycleBody(rowVersion: "AQIDBAUGBwg"), LifecycleToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "platform.rowversion_invalid");
  }

  [Fact]
  public async Task Lifecycle_with_stale_rowversion_returns_409_concurrency_conflict()
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/activate", LifecycleBody(rowVersion: StaleRowVersionText), LifecycleToken()));
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await AssertProblemAsync(response, "concurrency.conflict");
  }

  [Theory]
  [InlineData("Suspended")]  // Tenant synonym / undefined for Company
  [InlineData("Created")]    // Created is not a transition reason
  [InlineData("administrative")] // wrong casing
  [InlineData("0")]          // numeric enum
  public async Task Lifecycle_with_an_invalid_reason_returns_400_request_invalid(string reason)
  {
    var id = SeedInactive();
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/activate", LifecycleBody(reason: reason), LifecycleToken()));
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await AssertProblemAsync(response, "request.invalid");
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  [Theory]
  [InlineData("activate", CompanyStatus.Active)]     // activate requires Inactive
  [InlineData("deactivate", CompanyStatus.Inactive)] // deactivate requires Active
  [InlineData("archive", CompanyStatus.Archived)]    // Archived is terminal
  public async Task Lifecycle_from_an_incompatible_status_returns_409_company_transition_invalid(string action, CompanyStatus startStatus)
  {
    var id = Seed(startStatus);
    var response = await Client.SendAsync(Request(HttpMethod.Post, $"/api/platform/companies/{id}/{action}", LifecycleBody(), LifecycleToken()));
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await AssertProblemAsync(response, "company.transition_invalid");
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
    repository = new FakeCompanyRepository();
    readService = new FakeCompanyReadService();
    unitOfWork = new FakeUnitOfWork();
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

  private Guid SeedInactive() => Seed(CompanyStatus.Inactive);

  private Guid Seed(CompanyStatus status)
  {
    var company = CompanyInState(status, CurrentRowVersion);
    repository.Add(company);
    readService.Add(company.CompanyId, status);
    return company.CompanyId;
  }

  // ⚠ THE REFUSAL NAMES THE INPUT, AND COMPANY'S NAME IS NOT THE OBVIOUS ONE (T-273).
  //
  // Every other module calls this property `name`. `UpdateCompanyProfileRequest` declares it
  // **`companyName`** -- so a field derived by camel-casing the domain constant would have said `name`
  // and been wrong, and a form would have marked an input the caller never sent.
  //
  // Asserted end to end rather than against the contract, because the architecture guard proves the path
  // RESOLVES and only a request proves it TRAVELS -- through the value object, the mapper, the projection
  // and serialization.
  [Fact]
  public async Task A_refusal_names_the_input_using_the_name_the_contract_declares()
  {
    var id = Seed(CompanyStatus.Active);

    var response = await Client.SendAsync(Request(
      HttpMethod.Put, $"/api/platform/companies/{id}", UpdateBody(companyName: ""), ManageToken()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal("companyName", document.RootElement.GetProperty("field").GetString());
  }

  private static string UpdateBody(string companyName = "Acme Egypt", string rowVersion = CurrentRowVersionText) =>
    $"{{\"companyName\":\"{companyName}\",\"expectedRowVersion\":\"{rowVersion}\"}}";

  private static string LifecycleBody(string reason = "Administrative", string rowVersion = CurrentRowVersionText) =>
    $"{{\"reasonCode\":\"{reason}\",\"expectedRowVersion\":\"{rowVersion}\"}}";

  private static HttpRequestMessage Request(HttpMethod method, string path, string body, string? token)
  {
    var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    if (token is not null)
    {
      request.Headers.Authorization = new("Bearer", token);
    }

    return request;
  }

  private string ManageToken() => Token(
    new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.ManageCompanies));

  private string LifecycleToken() => Token(
    new Claim(JwtClaimTypes.TenantId, TenantId.ToString()),
    new Claim(JwtClaimTypes.Permission, PlatformPermissionNames.CompanyLifecycle));

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
    var token = new JwtSecurityToken(Issuer, Audience, requiredClaims.Concat(claims),
      now.AddMinutes(-1).UtcDateTime, now.AddMinutes(5).UtcDateTime, credentials);
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static Company CompanyInState(CompanyStatus status, byte[] rowVersion)
  {
    var company = Company.Create(TenantId, CompanyCode.Create("ACME-EG").Value, CompanyName.Create("Acme Egypt").Value,
      BaseCurrencyCode.Create("EGP").Value, "seed-actor", Guid.NewGuid(), Now).Value;
    if (status is CompanyStatus.Active)
    {
      company.Activate(CompanyStatusChangeReason.Administrative, "seed-actor", Guid.NewGuid(), Now);
    }

    if (status is CompanyStatus.Archived)
    {
      company.Archive(CompanyStatusChangeReason.Administrative, "seed-actor", Guid.NewGuid(), Now);
    }

    typeof(Company).GetField("<RowVersion>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
      .SetValue(company, rowVersion);
    company.ClearDomainEvents();
    return company;
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

  private sealed class FakeCompanyRepository : ICompanyRepository
  {
    private readonly Dictionary<Guid, Company> companies = [];

    public void Add(Company company) => companies[company.CompanyId] = company;

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(companies.GetValueOrDefault(companyId));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCompanyCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
      Add(company);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeCompanyReadService : ICompanyReadService
  {
    private readonly Dictionary<Guid, CompanyStatus> companies = [];

    public void Add(Guid companyId, CompanyStatus status) => companies[companyId] = status;

    public Task<CompanyDto?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
      if (!companies.TryGetValue(companyId, out var status))
      {
        return Task.FromResult<CompanyDto?>(null);
      }

      var dto = new CompanyDto(companyId, TenantId, "ACME-EG", "Acme Egypt", "EGP", status,
        Now, "company-admin", Now, "company-admin", Now, "company-admin", CompanyStatusChangeReason.Administrative, CurrentRowVersion);
      return Task.FromResult<CompanyDto?>(dto);
    }

    public Task<PagedResult<CompanyDto>> ListAsync(CompanyStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeUnitOfWork : ITenantUnitOfWork
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
    public DateTimeOffset UtcNow => Now;
  }
}

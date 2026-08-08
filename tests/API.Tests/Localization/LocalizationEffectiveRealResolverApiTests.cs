using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.API.Tests.Localization;

public sealed class LocalizationEffectiveRealResolverApiTests : IAsyncLifetime
{
  private static readonly Guid TenantId = Guid.Parse("248e93bd-a3af-4ed2-9246-4c4d7552b06b");
  private WebApplication? application;
  private HttpClient? client;

  [Fact]
  public async Task Effective_group_returns_raw_template_for_placeholder_resource()
  {
    using var request = Authorized(new HttpRequestMessage(
      HttpMethod.Get,
      "/api/platform/localization/effective?culture=en&module=platform&group=common.validation"));

    using var response = await Client.SendAsync(request);
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
    Assert.Equal("platform.common.validation.required", item.GetProperty("resourceKey").GetString());
    Assert.Equal("{fieldName} is required.", item.GetProperty("value").GetString());
  }

  [Fact]
  public async Task Effective_batch_formats_supplied_placeholders_and_rejects_missing_or_unknown_names()
  {
    using var formatted = Authorized(Post(
      """
      {
        "culture": "en",
        "resourceKeys": ["platform.common.validation.required"],
        "placeholderValuesByResource": {
          "platform.common.validation.required": {
            "fieldName": "Name"
          }
        }
      }
      """));
    using var formattedResponse = await Client.SendAsync(formatted);
    using var formattedDocument = await JsonDocument.ParseAsync(await formattedResponse.Content.ReadAsStreamAsync());
    Assert.Equal(HttpStatusCode.OK, formattedResponse.StatusCode);
    Assert.Equal("Name is required.", formattedDocument.RootElement.GetProperty("items")[0].GetProperty("value").GetString());

    using var missing = Authorized(Post(
      """{"culture":"en","resourceKeys":["platform.common.validation.required"]}"""));
    using var missingResponse = await Client.SendAsync(missing);
    Assert.Equal(HttpStatusCode.UnprocessableEntity, missingResponse.StatusCode);
    Assert.Equal("localization.placeholder_mismatch", await ProblemCodeAsync(missingResponse));

    using var unknown = Authorized(Post(
      """
      {
        "culture": "en",
        "resourceKeys": ["platform.common.validation.required"],
        "placeholderValuesByResource": {
          "platform.common.validation.required": {
            "fieldName": "Name",
            "other": "Unexpected"
          }
        }
      }
      """));
    using var unknownResponse = await Client.SendAsync(unknown);
    Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownResponse.StatusCode);
    Assert.Equal("localization.placeholder_mismatch", await ProblemCodeAsync(unknownResponse));
  }

  [Theory]
  [InlineData("{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.validation.required\"],\"placeholderValuesByResource\":{\"platform.common.validation.required\":{\"fieldName\":\"Name\"},\"platform.common.validation.required\":{\"fieldName\":\"Other\"}}}")]
  [InlineData("{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.validation.required\"],\"placeholderValuesByResource\":{\"platform.common.validation.required\":{\"fieldName\":\"Name\",\"fieldName\":\"Other\"}}}")]
  [InlineData("{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.validation.required\"],\"placeholderValuesByResource\":{\"platform.common.validation.required\":{\"fieldName\":5}}}")]
  [InlineData("{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.validation.required\"],\"placeholderValuesByResource\":{\"platform.common.actions.save\":{}}}")]
  public async Task Effective_batch_strictly_rejects_invalid_placeholder_map_shapes(string body)
  {
    using var request = Authorized(Post(body));
    using var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await ProblemCodeAsync(response));
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
    builder.WebHost.UseTestServer();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddScheme<AuthenticationSchemeOptions, TestBearerHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
    builder.Services.AddHostPermissionAuthorization();
    builder.Services.AddSingleton<ICurrentTenant>(new CurrentTenant(TenantId));
    builder.Services.AddScoped<IRequestTenantEligibility, ActiveEligibility>();
    builder.Services.AddSingleton<ILocalizationCatalog>(GeneratedLocalizationCatalog.Instance);
    builder.Services.AddScoped<ITenantLocalizationOverrideReadService, EmptyOverrideReader>();
    builder.Services.AddScoped<ITenantLocalizationVersionReader, StaticVersionReader>();
    builder.Services.AddSingleton<ILocalizationTenantCache, PassthroughCache>();
    builder.Services.AddSingleton<ILocalizationDiagnostics, NoOpDiagnostics>();
    builder.Services.AddScoped<ILocalizationTextResolver, LocalizationTextResolver>();
    builder.Services.AddScoped<CreateTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<UpdateTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<UndoTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<RestoreTenantLocalizationDefaultCommandHandler>();
    builder.Services.AddScoped<PreviewTenantLocalizationOverrideCommandHandler>();
    builder.Services.AddScoped<ListTenantLocalizationResourcesQueryHandler>();
    builder.Services.AddScoped<GetTenantLocalizationResourceQueryHandler>();
    builder.Services.AddScoped<GetTenantLocalizationHistoryQueryHandler>();

    application = builder.Build();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformLocalizationEndpoints();
    await application.StartAsync();
    client = application.GetTestClient();
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null) await application.DisposeAsync();
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("Test host is unavailable.");

  private static HttpRequestMessage Post(string body) => new(HttpMethod.Post, "/api/platform/localization/effective/batch")
  {
    Content = new StringContent(body, Encoding.UTF8, "application/json")
  };

  private static HttpRequestMessage Authorized(HttpRequestMessage request)
  {
    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    return request;
  }

  private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
  {
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    return document.RootElement.GetProperty("code").GetString();
  }

  private sealed class CurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class TestBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
  {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      if (!Request.Headers.TryGetValue("X-Test-Tenant", out var tenant))
        return Task.FromResult(AuthenticateResult.NoResult());
      var identity = new ClaimsIdentity(
        [new Claim(JwtClaimTypes.Subject, "real-resolver-user"), new Claim(JwtClaimTypes.TenantId, tenant.ToString())],
        Scheme.Name);
      return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
  }

  private sealed class ActiveEligibility : IRequestTenantEligibility
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
  }

  private sealed class EmptyOverrideReader : ITenantLocalizationOverrideReadService
  {
    public Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> ReadAsync(
      Guid tenantId,
      LocalizationCulture culture,
      IReadOnlyCollection<ResourceKey> resourceKeys,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>([]);
  }

  private sealed class StaticVersionReader : ITenantLocalizationVersionReader
  {
    public Task<long> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(1L);
  }

  private sealed class PassthroughCache : ILocalizationTenantCache
  {
    public Task<TenantLocalizationVersionState> GetVersionStateAsync(
      Guid tenantId,
      ITenantLocalizationVersionReader versionReader,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new TenantLocalizationVersionState(1, TenantLocalizationCacheTrust.Trusted));

    public async Task<IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?>> GetOrCreateAsync(
      Guid tenantId,
      string culture,
      long catalogVersion,
      long tenantLocalizationVersion,
      IReadOnlyCollection<string> resourceKeys,
      Func<CancellationToken, Task<IReadOnlyList<TenantLocalizationOverrideReadModel>>> factory,
      CancellationToken cancellationToken = default)
    {
      var values = (await factory(cancellationToken)).ToDictionary(item => item.ResourceKey, StringComparer.Ordinal);
      return resourceKeys.ToDictionary(
        resourceKey => resourceKey,
        resourceKey => values.GetValueOrDefault(resourceKey),
        StringComparer.Ordinal);
    }

    public void EvictTenant(Guid tenantId)
    {
    }
  }

  private sealed class NoOpDiagnostics : ILocalizationDiagnostics
  {
    public void RecordMissingResource(string resourceKey)
    {
    }

    public void RecordDegradedTenant(Guid tenantId)
    {
    }
  }
}

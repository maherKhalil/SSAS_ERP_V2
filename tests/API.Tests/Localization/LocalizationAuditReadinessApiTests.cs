using SSAS.BuildingBlocks.Api.Transport;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Localization;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.API.Tests.Localization;

public sealed class LocalizationAuditReadinessApiTests : IAsyncLifetime
{
  private static readonly Guid TenantId = Guid.Parse("7041080e-62af-4ac8-a90a-581335700631");
  private WebApplication? application;
  private HttpClient? client;
  private readonly State state = new();

  public static TheoryData<string, object> MutationRequests => new()
  {
    {
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "candidate-do-not-echo", expectedRowVersion = (string?)null }
    },
    {
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo",
      new { targetVersionNumber = 1, expectedRowVersion = "AQIDBAUGBwg=" }
    },
    {
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default",
      new { expectedRowVersion = "AQIDBAUGBwg=" }
    }
  };

  [Theory]
  [MemberData(nameof(MutationRequests))]
  public async Task Authorized_active_mutation_returns_safe_503_when_audit_is_unavailable(string path, object body)
  {
    state.Reset();
    using var request = AuthorizedRequest(path, body);

    var response = await Client.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Contains("localization.audit_readiness_unavailable", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("candidate-do-not-echo", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("provider-secret-reason", responseBody, StringComparison.Ordinal);
    Assert.Equal(1, state.ReadinessCalls);
    Assert.Equal(0, state.RepositoryCalls);
    Assert.Equal(0, state.SaveCalls);
  }

  [Theory]
  [MemberData(nameof(MutationRequests))]
  public async Task Missing_manage_permission_returns_403_before_audit_readiness(string path, object body)
  {
    state.Reset();
    using var request = AuthorizedRequest(path, body, includeManagePermission: false);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(0, state.ReadinessCalls);
  }

  [Theory]
  [MemberData(nameof(MutationRequests))]
  public async Task Inactive_tenant_returns_403_without_disclosing_audit_state(string path, object body)
  {
    state.Reset();
    state.TenantStatus = TenantStatus.Suspended;
    using var request = AuthorizedRequest(path, body);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(0, state.ReadinessCalls);
  }

  [Fact]
  public async Task Audit_provider_exception_returns_safe_503_without_internal_reason()
  {
    state.Reset();
    state.ReadinessException = new InvalidOperationException("provider-secret-reason");
    using var request = AuthorizedRequest(
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "candidate-do-not-echo", expectedRowVersion = (string?)null });

    var response = await Client.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.DoesNotContain("provider-secret-reason", responseBody, StringComparison.Ordinal);
    Assert.DoesNotContain("candidate-do-not-echo", responseBody, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Ready_authorized_create_continues_to_the_existing_success_contract()
  {
    state.Reset();
    state.IsReady = true;
    using var request = AuthorizedRequest(
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "Store", expectedRowVersion = (string?)null });

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal(1, state.ReadinessCalls);
    Assert.Equal(1, state.SaveCalls);
    Assert.NotNull(state.Added);
  }

  [Theory]
  [InlineData("/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo", true)]
  [InlineData("/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default", false)]
  public async Task Ready_authorized_existing_mutation_routes_continue_past_the_audit_gate(string path, bool undo)
  {
    state.Reset();
    state.IsReady = true;
    var body = undo
      ? (object)new { targetVersionNumber = 1, expectedRowVersion = "AQIDBAUGBwg=" }
      : new { expectedRowVersion = "AQIDBAUGBwg=" };
    using var request = AuthorizedRequest(path, body);

    var response = await Client.SendAsync(request);
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("localization.override_missing", document.RootElement.GetProperty("code").GetString());
    Assert.Equal(1, state.ReadinessCalls);
    Assert.True(state.RepositoryCalls > 0);
  }

  [Fact]
  public async Task Shared_strict_json_binding_rejects_unknown_duplicate_missing_and_wrong_typed_fields_before_handlers()
  {
    var requests = new[]
    {
      (HttpMethod.Put, "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
        "{\"value\":\"Store\",\"expectedRowVersion\":null,\"tenantId\":\"forged\"}"),
      (HttpMethod.Post, "/api/platform/localization/resources/platform.common.actions.save/overrides/en/undo",
        "{\"targetVersionNumber\":1,\"targetVersionNumber\":2,\"expectedRowVersion\":\"AQIDBAUGBwg=\"}"),
      (HttpMethod.Post, "/api/platform/localization/resources/platform.common.actions.save/overrides/en/restore-default", "{}"),
      (HttpMethod.Post, "/api/platform/localization/preview",
        "{\"resourceKey\":\"platform.common.actions.save\",\"culture\":\"en\",\"value\":7}")
    };

    foreach (var (method, path, body) in requests)
    {
      state.Reset();
      using var request = AuthorizedRawRequest(method, path, body);
      using var response = await Client.SendAsync(request);

      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Equal(0, state.ReadinessCalls);
      Assert.Equal(0, state.RepositoryCalls);
      Assert.Equal(0, state.SaveCalls);
    }
  }

  [Fact]
  public async Task Malformed_transport_rowversion_is_400_before_concurrency_or_audit_processing()
  {
    state.Reset();
    using var request = AuthorizedRequest(
      "/api/platform/localization/resources/platform.common.actions.save/overrides/en",
      new { value = "candidate-do-not-echo", expectedRowVersion = "AQIDBAUGBwg_" });

    using var response = await Client.SendAsync(request);
    var responseText = await response.Content.ReadAsStringAsync();
    using var problem = JsonDocument.Parse(responseText);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("localization.rowversion_invalid", problem.RootElement.GetProperty("code").GetString());
    Assert.DoesNotContain("candidate-do-not-echo", responseText, StringComparison.Ordinal);
    Assert.Equal(0, state.ReadinessCalls);
    Assert.Equal(0, state.RepositoryCalls);
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
    builder.WebHost.UseTestServer();
    builder.Services.AddHttpContextAccessor();
    builder.Services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddScheme<AuthenticationSchemeOptions, TestBearerHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
    builder.Services.AddHostPermissionAuthorization();
    builder.Services.AddSingleton(state);
    builder.Services.AddSingleton<ICurrentTenant>(new CurrentTenant(TenantId));
    builder.Services.AddSingleton<ICurrentUser, CurrentUser>();
    builder.Services.AddSingleton<IDateTimeProvider, Clock>();
    builder.Services.AddSingleton<ILocalizationCatalog>(GeneratedLocalizationCatalog.Instance);
    builder.Services.AddScoped<ITenantAuthenticationEligibilityReadService, Eligibility>();
    builder.Services.AddScoped<IRequestTenantEligibility, RequestTenantEligibility>();
    builder.Services.AddScoped<ILocalizationManagementAuditReadiness, AuditReadiness>();
    builder.Services.AddScoped<ITenantLocalizationSettingsRepository, SettingsRepository>();
    builder.Services.AddScoped<ITenantLocalizationOverrideRepository, OverrideRepository>();
    builder.Services.AddScoped<IPlatformUnitOfWork, UnitOfWork>();
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
    if (application is not null)
    {
      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host is unavailable.");

  private static HttpRequestMessage AuthorizedRequest(string path, object body, bool includeManagePermission = true)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
    if (!path.EndsWith("/undo", StringComparison.Ordinal) && !path.EndsWith("/restore-default", StringComparison.Ordinal))
    {
      request.Method = HttpMethod.Put;
    }
    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    if (includeManagePermission)
    {
      request.Headers.Add("X-Test-Permission", PlatformPermissionNames.ManageLocalization);
    }
    return request;
  }

  private static HttpRequestMessage AuthorizedRawRequest(HttpMethod method, string path, string body)
  {
    var request = new HttpRequestMessage(method, path)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    request.Headers.Add("X-Test-Permission", PlatformPermissionNames.ManageLocalization);
    return request;
  }

  private sealed class TestBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
  {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      if (!Request.Headers.TryGetValue("X-Test-Tenant", out var tenant))
      {
        return Task.FromResult(AuthenticateResult.NoResult());
      }
      var claims = new List<Claim>
      {
        new(JwtClaimTypes.Subject, "audit-api-user"),
        new(JwtClaimTypes.TenantId, tenant.ToString())
      };
      if (Request.Headers.TryGetValue("X-Test-Permission", out var permission))
      {
        claims.Add(new Claim(JwtClaimTypes.Permission, permission.ToString()));
      }
      var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
      return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
  }

  private sealed class State
  {
    public TenantStatus? TenantStatus { get; set; }
    public bool IsReady { get; set; }
    public Exception? ReadinessException { get; set; }
    public int ReadinessCalls { get; set; }
    public int RepositoryCalls { get; set; }
    public int SaveCalls { get; set; }
    public TenantLocalizationOverride? Added { get; set; }

    public void Reset()
    {
      TenantStatus = SSAS.Platform.Domain.Enums.TenantStatus.Active;
      IsReady = false;
      ReadinessException = null;
      ReadinessCalls = 0;
      RepositoryCalls = 0;
      SaveCalls = 0;
      Added = null;
    }
  }

  private sealed class Eligibility(State state) : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, state.TenantStatus));
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class AuditReadiness(State state) : ILocalizationManagementAuditReadiness
  {
    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
      state.ReadinessCalls++;
      if (state.ReadinessException is not null)
      {
        return Task.FromException<LocalizationManagementAuditReadinessResult>(state.ReadinessException);
      }
      return Task.FromResult(state.IsReady
        ? LocalizationManagementAuditReadinessResult.Ready
        : LocalizationManagementAuditReadinessResult.Unavailable);
    }
  }

  private sealed class SettingsRepository(State state) : ITenantLocalizationSettingsRepository
  {
    private readonly TenantLocalizationSettings settings = TenantLocalizationSettings.Create(TenantId, LocalizationCulture.English);
    public Task<TenantLocalizationSettings?> GetForUpdateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult<TenantLocalizationSettings?>(settings);
    }
    public Task<TenantLocalizationSettings> GetOrCreateForUpdateAsync(Guid tenantId, LocalizationCulture defaultCulture, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult(settings);
    }
  }

  private sealed class OverrideRepository(State state) : ITenantLocalizationOverrideRepository
  {
    public Task<TenantLocalizationOverride?> GetForUpdateAsync(Guid tenantId, ResourceKey resourceKey, LocalizationCulture culture, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult<TenantLocalizationOverride?>(null);
    }
    public Task<LocalizationVersionSnapshot?> GetVersionSnapshotAsync(Guid overrideId, TenantOverrideVersion versionNumber, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      return Task.FromResult<LocalizationVersionSnapshot?>(null);
    }
    public Task AddAsync(TenantLocalizationOverride localizationOverride, CancellationToken cancellationToken = default)
    {
      state.RepositoryCalls++;
      typeof(TenantLocalizationOverride).GetProperty(nameof(TenantLocalizationOverride.RowVersion))!
        .SetValue(localizationOverride, new byte[RowVersionCodec.SqlServerRowVersionLength]);
      state.Added = localizationOverride;
      return Task.CompletedTask;
    }
  }

  private sealed class UnitOfWork(State state) : IPlatformUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      state.SaveCalls++;
      return Task.FromResult(Result.Success(1));
    }
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new Transaction());
  }

  private sealed class Transaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class CurrentTenant(Guid tenantId) : ICurrentTenant { public Guid? TenantId { get; } = tenantId; }
  private sealed class CurrentUser : ICurrentUser
  {
    public string? UserId => "audit-api-user";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }
  private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}

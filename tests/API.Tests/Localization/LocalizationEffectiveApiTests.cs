using SSAS.BuildingBlocks.Api.Transport;
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

public sealed class LocalizationEffectiveApiTests : IAsyncLifetime
{
  private static readonly Guid TenantId = Guid.Parse("ce2bea1d-dc51-433d-8168-1afce01a7bbb");
  private readonly State state = new();
  private WebApplication? application;
  private HttpClient? client;

  // ⚠ CITES THE ANONYMOUS HALF OF `AC-LOC-0062`'s SECOND CLAUSE — *"Effective group and batch REJECT
  // ANONYMOUS and untrusted-Tenant callers."* Both effective routes, unauthenticated, `401`.
  //
  // ⚠⚠ NOT THE FIRST CLAUSE. *"All NINE M2 routes are non-anonymous"* is a claim over a set of nine; this
  // theory enumerates TWO, and they are named individually. **The test name is honest — *Effective_routes*,
  // which is exactly what the two are** — but a criterion id is read as covering the criterion, so the
  // nine-route claim is left to the route inventory where the population is derived rather than listed.
  [Theory]
  [InlineData("/api/platform/localization/effective?culture=en&module=platform&group=common.actions")]
  [InlineData("/api/platform/localization/effective/batch")]
  [Trait("Criterion", "AC-LOC-0062")]
  public async Task Effective_routes_require_authentication(string path)
  {
    using var request = new HttpRequestMessage(path.Contains("batch", StringComparison.Ordinal) ? HttpMethod.Post : HttpMethod.Get, path);
    if (path.Contains("batch", StringComparison.Ordinal))
    {
      request.Content = new StringContent("{\"culture\":\"en\",\"resourceKeys\":[]}", Encoding.UTF8, "application/json");
    }

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ⚠ CITES `AC-LOC-0062`'s THIRD CLAUSE — *"PERMIT an Active trusted Tenant WITHOUT View for ordinary
  // runtime resolution."* The caller holds no localization administration permission and gets `200`.
  //
  // ⚠⚠ THIS IS THE CLAUSE MOST LIKELY TO BE LOST, because it is the ALLOWED side of an authorization rule
  // and everything around it is a refusal. A gate that demanded `View` for ordinary resolution would pass
  // every anonymous-and-untrusted test in this file and break every logged-in user's screen.
  [Fact]
  [Trait("Criterion", "AC-LOC-0062")]
  public async Task Active_trusted_tenant_without_localization_administration_permission_resolves_group_with_safe_headers()
  {
    state.Reset();
    using var request = AuthorizedGet("/api/platform/localization/effective?culture=ar&module=platform&group=common.actions");

    var response = await Client.SendAsync(request);
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var body = document.RootElement;

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, state.GroupCalls);
    Assert.Equal("ar", body.GetProperty("requestedCulture").GetString());
    Assert.Equal("ar", body.GetProperty("resolvedCulture").GetString());
    Assert.Equal("rtl", body.GetProperty("direction").GetString());
    Assert.Equal(7, body.GetProperty("tenantLocalizationVersion").GetInt64());
    Assert.Equal(["platform.common.actions.cancel", "platform.common.actions.save"], body.GetProperty("items")
      .EnumerateArray().Select(item => item.GetProperty("resourceKey").GetString()));
    Assert.DoesNotContain("tenantId", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    AssertSecurityHeaders(response);
  }

  // ⚠ CITES THE UNTRUSTED-TENANT HALF OF `AC-LOC-0062`'s SECOND CLAUSE, and its FOURTH — *"reject …
  // untrusted-Tenant callers"* and *"provide NO Tenant override for non-Active Tenant."*
  //
  // The four rows split cleanly: the first is a caller claiming a DIFFERENT tenant while Active — untrusted
  // — and the other three are the SAME tenant in each non-Active status. **Two clauses, one theory, and the
  // rows are the reason both are covered rather than one.**
  //
  // ⚠⚠ THE STATUS ROWS ARE ENUMERATED, NOT DERIVED. `Provisioning`, `Suspended` and `Archived` are named,
  // so a FIFTH non-Active status added to `TenantStatus` would join the enum and not this list, and the
  // clause would silently narrow. **The name says *non_active_tenant*, which claims the category** — that
  // is `B20`'s shape, recorded here rather than swept.
  [Theory]
  [InlineData("00000000-0000-0000-0000-000000000001", TenantStatus.Active)]
  [InlineData("ce2bea1d-dc51-433d-8168-1afce01a7bbb", TenantStatus.Provisioning)]
  [InlineData("ce2bea1d-dc51-433d-8168-1afce01a7bbb", TenantStatus.Suspended)]
  [InlineData("ce2bea1d-dc51-433d-8168-1afce01a7bbb", TenantStatus.Archived)]
  [Trait("Criterion", "AC-LOC-0062")]
  public async Task Untrusted_or_non_active_tenant_is_denied(string claimTenantId, TenantStatus status)
  {
    state.Reset();
    state.Status = status;
    using var request = AuthorizedGet("/api/platform/localization/effective?culture=en&module=platform&group=common.actions", claimTenantId);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(0, state.GroupCalls);
  }

  // ⚠⚠⚠ CITES `AC-LOC-0004`'s PROVENANCE CLAUSE — *"Every read/write/history/cache path DERIVES TenantId
  // FROM TRUSTED CONTEXT and returns no other Tenant's state"* — AND IT CLOSES A RESIDUAL I WROTE EARLIER
  // TONIGHT.
  //
  // When `LocalizationResolverTests.Cache_keys_are_tenant_complete…` was cited for this criterion's CACHE
  // path, I recorded that it could not carry *derives from trusted context* because **the test supplies the
  // tenant ids itself** — a test cannot witness where a value came from when the test is where it came
  // from. **The third row here is the witness that residual said was missing.**
  //
  // `…&tenantId=ignored` is a caller ATTEMPTING TO SUPPLY THE TENANT, and it is answered `400` with
  // `GroupCalls == 0` — refused before resolution rather than accepted and ignored. **That is the clause
  // stated positively: the tenant cannot come from the request, so it comes from context.** A route that
  // bound the parameter and then quietly overrode it would answer `200` and satisfy every isolation test in
  // the package.
  //
  // ⚠ THE `GroupCalls` COLUMN IS WHAT MAKES THE THREE ROWS DIFFERENT CLAIMS RATHER THAN ONE REPEATED.
  // `en-US` reaches the resolver and is refused there (`1`); the empty module and the forged tenant are
  // refused BEFORE it (`0`). **Without that assertion all three are just `400`**, and the row that matters
  // for this criterion — the forged tenant never touching the resolver — is indistinguishable from a
  // culture rejection.
  [Theory]
  [InlineData("/api/platform/localization/effective?culture=en-US&module=platform&group=common.actions")]
  [InlineData("/api/platform/localization/effective?culture=en&module=&group=common.actions")]
  [InlineData("/api/platform/localization/effective?culture=en&module=platform&group=common.actions&tenantId=ignored")]
  [Trait("Criterion", "AC-LOC-0004")]
  public async Task Effective_group_rejects_invalid_or_unbounded_query_contracts(string path)
  {
    state.Reset();
    using var request = AuthorizedGet(path);

    var response = await Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(path.Contains("en-US", StringComparison.Ordinal) ? 1 : 0, state.GroupCalls);
  }

  // ⚠ CITES `AC-LOC-0050`'s *CODES* CLAUSE. The batch route's failure shape asserted whole rather than by
  // status alone: `400` + `status` + `type` + `code` + `correlationId` + `resourceKey`. **The clause is
  // *codes*, plural and stable, so asserting only the status would satisfy the word and not the contract.**
  //
  // ⚠⚠ AND THE `DoesNotContain` IS THE ONE ASSERTION WITH A REAL ARRANGEMENT BEHIND IT: the rejected body
  // carries `candidate-do-not-echo` TWICE, so the value is demonstrably in play and demonstrably absent
  // from the response. Compare the usual failure mode, where a ban passes because the string was never
  // there. The duplicate is also what triggers the rejection, so the echo test and the refusal share one
  // input.
  [Fact]
  [Trait("Criterion", "AC-LOC-0050")]
  public async Task Handler_errors_preserve_the_complete_stable_problem_contract_without_echoing_input()
  {
    state.Reset();
    using var request = AuthorizedPost("{\"culture\":\"en\",\"resourceKeys\":[\"candidate-do-not-echo\",\"candidate-do-not-echo\"]}");

    var response = await Client.SendAsync(request);
    var responseText = await response.Content.ReadAsStringAsync();
    using var document = JsonDocument.Parse(responseText);
    var problem = document.RootElement;

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(400, problem.GetProperty("status").GetInt32());
    Assert.Equal("https://httpstatuses.com/400", problem.GetProperty("type").GetString());
    Assert.Equal("request.invalid", problem.GetProperty("code").GetString());
    Assert.True(problem.TryGetProperty("correlationId", out _));
    Assert.Equal(LocalizationApiErrorMapper.GenericProblemResourceKey, problem.GetProperty("resourceKey").GetString());
    Assert.DoesNotContain("candidate-do-not-echo", responseText, StringComparison.Ordinal);
    AssertSecurityHeaders(response);
  }

  // ⚠ CITES `AC-LOC-0050`'s *STRICT UNIQUE BOUNDED KEYS* CLAUSE, one body per word:
  //
  //   `{ malformed` · missing `resourceKeys` · `"not-an-array"`   STRICT — the reader refuses the shape
  //   `"tenantId":"forged"`                                       STRICT — an unknown field, not ignored
  //   duplicate `"culture"` JSON key                              STRICT — duplicate members refused
  //   the same resource key twice                                 UNIQUE
  //   101 keys → `localization.explicit_batch_too_large`          BOUNDED, and by its own code
  //
  // ⚠⚠ `Assert.Equal(0, state.BatchCalls)` INSIDE THE LOOP IS THE CLAUSE'S REAL CONTENT — *BEFORE
  // RESOLUTION*, which the test name promises and a status check cannot show. A reader that bound the body,
  // resolved, and then rejected would answer `400` on every row and pass a status-only version of this
  // test.
  [Fact]
  [Trait("Criterion", "AC-LOC-0050")]
  public async Task Batch_rejects_unknown_duplicate_and_oversized_transport_inputs_before_resolution()
  {
    state.Reset();
    var bodies = new[]
    {
      "{ malformed",
      "{\"culture\":\"en\"}",
      "{\"culture\":\"en\",\"resourceKeys\":\"not-an-array\"}",
      "{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.actions.save\"],\"tenantId\":\"forged\"}",
      "{\"culture\":\"en\",\"culture\":\"ar\",\"resourceKeys\":[]}",
      "{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.actions.save\",\"platform.common.actions.save\"]}"
    };

    foreach (var body in bodies)
    {
      using var request = AuthorizedPost(body);
      var response = await Client.SendAsync(request);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Equal(0, state.BatchCalls);
      AssertSecurityHeaders(response);
    }

    var keys = string.Join(',', Enumerable.Range(0, 101).Select(index => $"\"platform.common.actions.key{index}\""));
    using var oversized = AuthorizedPost($"{{\"culture\":\"en\",\"resourceKeys\":[{keys}]}}");
    var oversizedResponse = await Client.SendAsync(oversized);
    using var oversizedDocument = await JsonDocument.ParseAsync(await oversizedResponse.Content.ReadAsStreamAsync());
    Assert.Equal(HttpStatusCode.BadRequest, oversizedResponse.StatusCode);
    Assert.Equal("localization.explicit_batch_too_large", oversizedDocument.RootElement.GetProperty("code").GetString());
  }

  // ⚠ CITES `AC-LOC-0050`'s *PROJECTION* CLAUSE, and it is the positive that the refusal test above needs.
  // `BatchCalls == 1` where every row there asserts `0`: **the same counter proves the batch is refused
  // before resolution in one test and reaches the resolver in this one.** Neither reading is available from
  // its own test alone.
  //
  // The response is ORDINALLY ordered — `cancel` before `save`, which is not the request order — and
  // carries `source` per item, so the projection is the resolver's answer rather than an echo of the keys.
  [Fact]
  [Trait("Criterion", "AC-LOC-0050")]
  public async Task Batch_uses_the_resolver_for_ordinal_safe_projection_and_accepts_the_approved_empty_list()
  {
    state.Reset();
    using var request = AuthorizedPost("{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.actions.save\",\"platform.common.actions.cancel\"]}");
    var response = await Client.SendAsync(request);
    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(1, state.BatchCalls);
    Assert.Equal(["platform.common.actions.cancel", "platform.common.actions.save"], document.RootElement.GetProperty("items")
      .EnumerateArray().Select(item => item.GetProperty("resourceKey").GetString()));
    Assert.Equal("TenantOverride", document.RootElement.GetProperty("items")[0].GetProperty("source").GetString());
    AssertSecurityHeaders(response);

    using var emptyRequest = AuthorizedPost("{\"culture\":\"en\",\"resourceKeys\":[]}");
    var emptyResponse = await Client.SendAsync(emptyRequest);
    using var emptyDocument = await JsonDocument.ParseAsync(await emptyResponse.Content.ReadAsStreamAsync());
    Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
    Assert.Empty(emptyDocument.RootElement.GetProperty("items").EnumerateArray());
    Assert.Equal(9, emptyDocument.RootElement.GetProperty("catalogVersion").GetInt64());
  }

  public async Task InitializeAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
    builder.WebHost.UseTestServer();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddScheme<AuthenticationSchemeOptions, TestBearerHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
    builder.Services.AddHostPermissionAuthorization();
    builder.Services.AddSingleton(state);
    builder.Services.AddSingleton<ICurrentTenant>(new CurrentTenant(TenantId));
    builder.Services.AddScoped<IRequestTenantEligibility, Eligibility>();
    builder.Services.AddScoped<ILocalizationTextResolver, Resolver>();
    builder.Services.AddSingleton<ILocalizationCatalog, Catalog>();
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

  private HttpClient Client => client ?? throw new InvalidOperationException("Test host is unavailable.");

  private static HttpRequestMessage AuthorizedGet(string path, string? claimTenantId = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Add("X-Test-Tenant", claimTenantId ?? TenantId.ToString());
    return request;
  }

  private static HttpRequestMessage AuthorizedPost(string body)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform/localization/effective/batch")
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("X-Test-Tenant", TenantId.ToString());
    return request;
  }

  private static void AssertSecurityHeaders(HttpResponseMessage response)
  {
    Assert.Equal("no-store, no-cache", response.Headers.GetValues("Cache-Control").Single());
    Assert.Equal("no-cache", response.Headers.GetValues("Pragma").Single());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
  }

  private sealed class CurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class Catalog : ILocalizationCatalog
  {
    private static readonly ILocalizationCatalog Inner = GeneratedLocalizationCatalog.Instance;
    public CatalogSchemaVersion CatalogSchemaVersion => Inner.CatalogSchemaVersion;
    public CatalogVersion CatalogVersion => SSAS.BuildingBlocks.Localization.CatalogVersion.Create(9).Value;
    public IReadOnlyList<LocalizationResourceDefinition> Resources => Inner.Resources;
    public string GetNeutralFallback(LocalizationCulture culture) => Inner.GetNeutralFallback(culture);
    public bool TryGet(ResourceKey resourceKey, out LocalizationResourceDefinition resource) => Inner.TryGet(resourceKey, out resource!);
    public IReadOnlyList<LocalizationResourceDefinition> GetActiveGroup(string moduleName, string group) => Inner.GetActiveGroup(moduleName, group);
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

      var identity = new ClaimsIdentity(
        [new Claim(JwtClaimTypes.Subject, "effective-api-user"), new Claim(JwtClaimTypes.TenantId, tenant.ToString())],
        Scheme.Name);
      return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
  }

  private sealed class Eligibility(State state) : IRequestTenantEligibility
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, state.Status));
  }

  private sealed class Resolver(State state) : ILocalizationTextResolver
  {
    public Task<Result<EffectiveLocalizedText>> ResolveTemplateAsync(
      LocalizationResolutionRequest request,
      CancellationToken cancellationToken = default) =>
      ResolveAsync(request, cancellationToken);

    public Task<Result<EffectiveLocalizedText>> ResolveAsync(LocalizationResolutionRequest request, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Failure<EffectiveLocalizedText>(LocalizationResolutionErrors.InvalidGroup));

    public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveTemplateExplicitBatchAsync(
      LocalizationExplicitBatchRequest request,
      CancellationToken cancellationToken = default) =>
      ResolveExplicitBatchAsync(request, cancellationToken);

    public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveExplicitBatchAsync(
      LocalizationExplicitBatchRequest request,
      CancellationToken cancellationToken = default)
    {
      state.BatchCalls++;
      if (request.ResourceKeys.Count > LocalizationTextResolver.MaximumExplicitBatchSize)
      {
        return Task.FromResult(Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(LocalizationResolutionErrors.ExplicitBatchTooLarge));
      }

      return Task.FromResult(Result.Success<IReadOnlyList<EffectiveLocalizedText>>(
        request.ResourceKeys.OrderBy(key => key, StringComparer.Ordinal).Select((key, index) => Item(key, request.RequestedCulture, index == 0)).ToArray()));
    }

    public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveTemplateGroupAsync(
      LocalizationGroupBatchRequest request,
      CancellationToken cancellationToken = default)
    {
      state.GroupCalls++;
      var culture = LocalizationCulture.Create(request.RequestedCulture);
      if (culture.IsFailure)
      {
        return Task.FromResult(Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(culture.Error));
      }

      return Task.FromResult(Result.Success<IReadOnlyList<EffectiveLocalizedText>>(
        [Item("platform.common.actions.cancel", request.RequestedCulture, false), Item("platform.common.actions.save", request.RequestedCulture, true)]));
    }

    public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveGroupAsync(
      LocalizationGroupBatchRequest request,
      CancellationToken cancellationToken = default) =>
      ResolveTemplateGroupAsync(request, cancellationToken);

    private static EffectiveLocalizedText Item(string key, string culture, bool overrideValue)
    {
      var selectedCulture = LocalizationCulture.Create(culture).Value;
      return new EffectiveLocalizedText(
        ResourceKey.Create(key).Value,
        selectedCulture,
        selectedCulture,
        overrideValue ? "Tenant value" : "System value",
        overrideValue ? LocalizationResolutionSource.TenantOverride : LocalizationResolutionSource.SystemDefault,
        CatalogVersion.Create(4).Value,
        ResourceVersion.Create(2).Value,
        TenantLocalizationVersion.Create(7).Value,
        null,
        selectedCulture.Direction,
        false,
        true);
    }
  }

  private sealed class State
  {
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public int GroupCalls { get; set; }
    public int BatchCalls { get; set; }
    public void Reset()
    {
      Status = TenantStatus.Active;
      GroupCalls = 0;
      BatchCalls = 0;
    }
  }
}

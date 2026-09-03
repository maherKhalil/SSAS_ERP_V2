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

  // ⚠ CITES TWO CLAUSES OF `AC-LOC-0049` — *"raw effective-template projection"* and *"IT PERFORMS NO
  // PLACEHOLDER INTERPOLATION"* — through the REAL route and the REAL `LocalizationTextResolver`, not a
  // stub. `/effective` returns `"{fieldName} is required."` with the brace intact.
  //
  // ⚠⚠ AND A THIRD: *"ordinary runtime resolution DOES NOT REQUIRE View."* `TestBearerHandler` issues a
  // principal with a subject and a tenant and **NO PERMISSION CLAIM AT ALL**, and the request returns 200.
  // That is the behavioural counterpart of the bare `.RequireAuthorization()` on the effective group — the
  // default policy demands an authenticated caller and nothing more. **The absence of a claim is doing the
  // work here, so it is worth saying: adding a permission requirement to this group would redden this test,
  // which is the point.**
  //
  // ⚠⚠⚠ AND THE PAIR BELOW IS THE THING I BUILT BY HAND ELSEWHERE, ALREADY PRESENT HERE AND UNREMARKED.
  // *No interpolation* is a property whose test and whose negation have IDENTICAL SHAPE — both are an
  // equality against a rendered string, and only the expected value differs, so a reader cannot recover the
  // direction from the assertion. `LocalizationAdministrationTemplateTests` needed a second positive over a
  // contrasting producer added deliberately to carry that direction.
  //
  // **HERE THE CONTRASTING PRODUCER IS A SIBLING TEST AND A DIFFERENT ROUTE**: `/effective` yields
  // `"{fieldName} is required."` and `/effective/batch` yields `"Name is required."` from the same resource
  // and the same resolver. Between them the direction is structural — the raw form is the one the batch
  // route did NOT return. Two tests that look independent are jointly carrying one property.
  [Fact]
  [Trait("Criterion", "AC-LOC-0049")]
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

  // ⚠ CITES THE *POLICY VALIDATION* HALF OF `AC-LOC-0050`'s LAST SENTENCE — *"malformed or unrequested maps
  // fail REQUEST validation, MISSING/UNKNOWN PLACEHOLDERS FAIL POLICY VALIDATION"* — plus the *"optional
  // resource-scoped plain-string placeholder values"* clause on the success leg, and *"ordinary runtime
  // resolution does not require View"* (again, no permission claim, 200).
  //
  // Supplied `fieldName` → `"Name is required."`; omitted → 422; an extra `other` → 422; both
  // `localization.placeholder_mismatch`.
  //
  // ⚠⚠ THE 422/400 SPLIT IS THE WHOLE CITATION AND NEITHER HALF STANDS ALONE. The criterion assigns two
  // DIFFERENT validation kinds to two DIFFERENT failure classes, so a test showing only one of them leaves
  // the distinction unmade — a single implementation returning 422 for everything would satisfy this test
  // and violate the sentence. `Effective_batch_strictly_rejects_invalid_placeholder_map_shapes` carries the
  // 400 half, and the two are cited as a pair for that reason.
  //
  // ⚠⚠⚠ AND THIS IS THE RUNTIME SIDE OF A CITATION I DELIBERATELY MADE ON THE DECLARED SIDE ONLY.
  // `LocalizationOpenApiContractTests` cites `AC-LOC-0050` for the generated document's `maxItems` 100,
  // `uniqueItems`, culture enum and plain-string placeholder map, with the note that **a schema bound and a
  // runtime bound can disagree and that test would stay green if they did.** This is where the runtime
  // behaviour is actually exercised. **Two citers, two sides of declared-versus-enforced, and the note at
  // each site names the other.**
  [Fact]
  [Trait("Criterion", "AC-LOC-0050")]
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
  // ⚠ CITES THE *REQUEST VALIDATION* HALF OF `AC-LOC-0050`'s LAST SENTENCE — *"MALFORMED OR UNREQUESTED MAPS
  // FAIL REQUEST VALIDATION"* — and it is the half that makes the sibling test's 422 mean something. All
  // four rows return 400 `request.invalid`, and they cover both nouns the clause names:
  //
  //   MALFORMED    duplicate resource key in the map · duplicate placeholder name · non-string value (`5`)
  //   UNREQUESTED  a map keyed on `platform.common.actions.save`, which is not in `resourceKeys`
  //
  // ⚠⚠ THE ROWS ARE RAW JSON STRINGS RATHER THAN OBJECTS, DELIBERATELY, AND THE CITATION DEPENDS ON IT.
  // Two of the four are DUPLICATE KEYS, which no serializer would emit and no anonymous object can express
  // — `new { fieldName = "Name", fieldName = "Other" }` does not compile. **A typed request object cannot
  // construct the input this clause is about**, so the ugly literals are the mechanism, not an oversight.
  //
  // ⚠⚠⚠ AND THE *UNREQUESTED* ROW IS GUARDED TWICE, WHICH I FOUND ONLY BY PLANTING A SENTENCE I HAD ALREADY
  // WRITTEN. I claimed *"the three malformed rows would still pass if the cross-field check were deleted,
  // and only this row would notice."* **MEASURED, AND FALSE.** All four cells:
  //
  //   `HasValidPlaceholderMap` `!requested.Contains`   `HasValidPlaceholderResourceScope`   result
  //   present                                          present                              all pass
  //   DELETED                                          present                              ALL PASS
  //   present                                          DELETED                              ALL PASS
  //   DELETED                                          DELETED                              this row FAILS
  //
  // **The rule is enforced in TWO places — `LocalizationEndpointRouteBuilderExtensions` checks
  // `!requested.Contains(...)` in the raw-JSON validator AND again in `HasValidPlaceholderResourceScope`
  // over the bound request — and DELETING EITHER ONE ALONE IS INVISIBLE TO THE ENTIRE SUITE.**
  //
  // ⚠ So this row tests THE RULE and tests NEITHER MECHANISM. That is worth stating plainly because the
  // usual reading of a green suite is the opposite: redundant guards look like defence in depth and behave,
  // under test, like a single guard whose location nothing pins. **Reading only the diagonal — baseline
  // green, both-deleted red — would have shown a working control and hidden this entirely.**
  [Trait("Criterion", "AC-LOC-0050")]
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

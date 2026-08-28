using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.Localization;

public static class LocalizationEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/platform/localization/resources";

  public static IEndpointRouteBuilder MapPlatformLocalizationEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Localization");
    group.MapGet("", ListAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ViewLocalization}")
      .WithName("PlatformLocalizationResourcesList");
    group.MapGet("/{resourceKey}", DetailAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ViewLocalization}")
      .WithName("PlatformLocalizationResourceDetail");
    group.MapGet("/{resourceKey}/history", HistoryAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ViewLocalizationHistory}")
      .WithName("PlatformLocalizationResourceHistory");
    group.MapPut("/{resourceKey}/overrides/{culture}", PutAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ManageLocalization}")
      .WithName("PlatformLocalizationOverridePut");
    group.MapPost("/{resourceKey}/overrides/{culture}/undo", UndoAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ManageLocalization}")
      .WithName("PlatformLocalizationOverrideUndo");
    group.MapPost("/{resourceKey}/overrides/{culture}/restore-default", RestoreDefaultAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ManageLocalization}")
      .WithName("PlatformLocalizationOverrideRestoreDefault");
    endpoints.MapPost("/api/platform/localization/preview", PreviewAsync)
      .RequireAuthorization($"Permission:{PlatformPermissionNames.ManageLocalization}")
      .WithTags("Platform Localization")
      .WithName("PlatformLocalizationPreview");
    var effective = endpoints.MapGroup("/api/platform/localization/effective")
      .RequireAuthorization()
      .WithTags("Platform Localization");
    effective.MapGet("", EffectiveGroupAsync)
      .WithName("PlatformLocalizationEffectiveGroup");
    effective.MapPost("/batch", EffectiveBatchAsync)
      .WithName("PlatformLocalizationEffectiveBatch");
    return endpoints;
  }

  private static async Task<IResult> PutAsync(HttpContext context, string resourceKey, string culture,
    CreateTenantLocalizationOverrideCommandHandler create, UpdateTenantLocalizationOverrideCommandHandler update,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<PutLocalizationOverrideRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["value"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String, JsonValueKind.Null]
      }, cancellationToken);
    if (request is null || string.IsNullOrWhiteSpace(resourceKey) || string.IsNullOrWhiteSpace(culture)) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    if (request.ExpectedRowVersion is null)
    {
      var created = await create.HandleAsync(new CreateTenantLocalizationOverrideCommand(resourceKey, culture, request.Value!), cancellationToken);
      return created.IsFailure ? Problem(context, Map(created.Error)) : Results.Json(MapMutation(resourceKey, culture, created.Value), statusCode: StatusCodes.Status201Created);
    }
    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return Problem(context, LocalizationApiErrorMapper.InvalidRowVersion);
    var updated = await update.HandleAsync(new UpdateTenantLocalizationOverrideCommand(resourceKey, culture, request.Value!, rowVersion), cancellationToken);
    return updated.IsFailure ? Problem(context, Map(updated.Error)) : Results.Ok(MapMutation(resourceKey, culture, updated.Value));
  }

  private static async Task<IResult> UndoAsync(HttpContext context, string resourceKey, string culture,
    UndoTenantLocalizationOverrideCommandHandler handler, CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<UndoLocalizationOverrideRequest>(context,
      new Dictionary<string, JsonValueKind[]> { ["targetVersionNumber"] = [JsonValueKind.Number], ["expectedRowVersion"] = [JsonValueKind.String] }, cancellationToken);
    if (request is null || request.TargetVersionNumber is not > 0 || string.IsNullOrWhiteSpace(resourceKey) || string.IsNullOrWhiteSpace(culture)) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return Problem(context, LocalizationApiErrorMapper.InvalidRowVersion);
    var result = await handler.HandleAsync(new UndoTenantLocalizationOverrideCommand(resourceKey, culture, request.TargetVersionNumber.Value, rowVersion), cancellationToken);
    return result.IsFailure ? Problem(context, Map(result.Error)) : Results.Ok(MapMutation(resourceKey, culture, result.Value));
  }

  private static async Task<IResult> RestoreDefaultAsync(HttpContext context, string resourceKey, string culture,
    RestoreTenantLocalizationDefaultCommandHandler handler, CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<RestoreLocalizationOverrideDefaultRequest>(context,
      new Dictionary<string, JsonValueKind[]> { ["expectedRowVersion"] = [JsonValueKind.String] }, cancellationToken);
    if (request is null || string.IsNullOrWhiteSpace(resourceKey) || string.IsNullOrWhiteSpace(culture)) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return Problem(context, LocalizationApiErrorMapper.InvalidRowVersion);
    var result = await handler.HandleAsync(new RestoreTenantLocalizationDefaultCommand(resourceKey, culture, rowVersion), cancellationToken);
    return result.IsFailure ? Problem(context, Map(result.Error)) : Results.Ok(MapMutation(resourceKey, culture, result.Value));
  }

  private static async Task<IResult> PreviewAsync(HttpContext context, PreviewTenantLocalizationOverrideCommandHandler handler,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<PreviewLocalizationRequest>(context,
      new Dictionary<string, JsonValueKind[]> { ["resourceKey"] = [JsonValueKind.String], ["culture"] = [JsonValueKind.String], ["value"] = [JsonValueKind.String] }, cancellationToken);
    if (request is null) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    var result = await handler.HandleAsync(new PreviewTenantLocalizationOverrideCommand(request.ResourceKey!, request.Culture!, request.Value!), cancellationToken);
    return result.IsFailure ? Problem(context, Map(result.Error)) : Results.Ok(new LocalizationPreviewResponse(
      result.Value.ResourceKey, result.Value.Culture, result.Value.Value, result.Value.TextFormat, result.Value.Placeholders,
      result.Value.Direction, result.Value.CatalogVersion, result.Value.ResourceVersion, result.Value.RequestedCulture, result.Value.ResolvedCulture));
  }

  private static async Task<IResult> ListAsync(
    HttpContext context,
    ListTenantLocalizationResourcesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    if (!TryListQuery(context.Request.Query, out var query)) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure) return Problem(context, Map(result.Error));
    var page = result.Value;
    return Results.Ok(new LocalizationResourcePageResponse(
      page.Items.Select(Map).ToArray(), page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages));
  }

  private static async Task<IResult> DetailAsync(
    HttpContext context,
    string resourceKey,
    GetTenantLocalizationResourceQueryHandler handler,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    if (!TryResourceQuery(context.Request.Query, resourceKey, out var query)) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    var result = await handler.HandleAsync(query, cancellationToken);
    return result.IsFailure
      ? Problem(context, Map(result.Error))
      : Results.Ok(new LocalizationResourceDetailResponse(Map(result.Value.Resource), result.Value.EnglishSystemDefault, result.Value.ArabicSystemDefault));
  }

  private static async Task<IResult> HistoryAsync(
    HttpContext context,
    string resourceKey,
    GetTenantLocalizationHistoryQueryHandler handler,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    if (!TryHistoryQuery(context.Request.Query, resourceKey, out var query)) return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure) return Problem(context, Map(result.Error));
    var history = result.Value;
    return Results.Ok(new LocalizationHistoryResponse(
      history.ResourceKey, history.Culture, history.IsActive, history.CurrentVersionNumber, history.EligibleUndoTargetVersion,
      history.Entries.Select(entry => new LocalizationHistoryEntryResponse(
        entry.VersionNumber, entry.Value, entry.IsActive, entry.ChangeType, entry.PriorLogicalVersionNumber,
        entry.UndoTargetVersionNumber, entry.CatalogVersion, entry.ResourceVersion, entry.ActorId, entry.OccurredUtc)).ToArray(),
      history.PageNumber, history.PageSize, history.TotalCount, history.TotalPages));
  }

  private static LocalizationResourceResponse Map(LocalizationAdministrationResource resource) => new(
    resource.ResourceKey, resource.Module, resource.Group, resource.Category, resource.TextFormat, resource.Lifecycle,
    resource.SecurityClassification, resource.TenantOverridable, resource.ResourceVersion, resource.CatalogVersion,
    resource.RequestedCulture, resource.RequestedCultureSystemDefault, resource.EffectiveValue, resource.CurrentTenantOverride,
    resource.OverrideActive, resource.Compatibility, resource.TenantOverrideVersion, resource.CurrentVersionNumber,
    resource.RowVersion is null ? null : RowVersionCodec.Encode(resource.RowVersion), resource.LastModifiedUtc,
    resource.Placeholders, resource.EligibleUndoTargetVersion);

  private static LocalizationMutationResponse MapMutation(string resourceKey, string culture, LocalizationMutationResult result) => new(
    resourceKey, culture, result.Value, result.IsActive, result.CurrentVersionNumber, result.TenantLocalizationVersion,
    RowVersionCodec.Encode(result.RowVersion));

  // ---- THE FALLBACK FOR AN UNMAPPED CODE, AND IT IS A SERVER ERROR AS OF T-093.
  //
  // It was `InvalidRequest` — a 400 — which blames the caller for a gap in `LocalizationApiErrorMapper`'s
  // table and hides it. Every module mapper states the opposite convention in its own header, and this
  // site is the only one whose default lived away from the table it defaults for.
  private static LocalizationApiError Map(Error error) => LocalizationApiErrorMapper.TryMap(error.Code, out var mapped)
    ? mapped
    : LocalizationApiErrorMapper.WriteFailure;

  private static IResult Problem(HttpContext context, LocalizationApiError error) => Results.Problem(
    type: $"https://httpstatuses.com/{error.StatusCode}",
    statusCode: error.StatusCode,
    title: error.Code,
    extensions: new Dictionary<string, object?>
    {
      ["code"] = error.Code,
      ["correlationId"] = context.Response.Headers["X-Correlation-ID"].ToString(),
      ["resourceKey"] = LocalizationApiErrorMapper.GenericProblemResourceKey
    });

  private static async Task<IResult> EffectiveGroupAsync(
    HttpContext context,
    [FromServices] ILocalizationTextResolver resolver,
    [FromServices] ILocalizationCatalog catalog,
    [FromServices] IRequestTenantEligibility eligibility,
    [FromServices] ICurrentTenant currentTenant,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    if (!TryEffectiveGroupQuery(context.Request.Query, out var request))
    {
      return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    }

    if (!await HasTrustedActiveTenantAsync(context, currentTenant, eligibility, cancellationToken))
    {
      return Problem(context, LocalizationApiErrorMapper.TenantIneligible);
    }

    var result = await resolver.ResolveTemplateGroupAsync(request, cancellationToken);
    return result.IsFailure
      ? Problem(context, Map(result.Error))
      : Results.Ok(MapEffective(request.RequestedCulture, catalog.CatalogVersion.Value, result.Value));
  }

  private static async Task<IResult> EffectiveBatchAsync(
    HttpContext context,
    [FromServices] ILocalizationTextResolver resolver,
    [FromServices] ILocalizationCatalog catalog,
    [FromServices] IRequestTenantEligibility eligibility,
    [FromServices] ICurrentTenant currentTenant,
    CancellationToken cancellationToken)
  {
    LocalizationResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<EffectiveLocalizationBatchRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["culture"] = [JsonValueKind.String],
        ["resourceKeys"] = [JsonValueKind.Array],
        ["placeholderValuesByResource"] = [JsonValueKind.Object]
      },
      cancellationToken,
      requiredFields: ["culture", "resourceKeys"],
      additionalValidation: IsStrictEffectiveBatchJson);
    if (request?.Culture is null || request.ResourceKeys is null || request.ResourceKeys.Any(string.IsNullOrWhiteSpace) ||
      request.ResourceKeys.Distinct(StringComparer.Ordinal).Count() != request.ResourceKeys.Count ||
      !HasValidPlaceholderResourceScope(request, catalog))
    {
      return Problem(context, LocalizationApiErrorMapper.InvalidRequest);
    }

    if (!await HasTrustedActiveTenantAsync(context, currentTenant, eligibility, cancellationToken))
    {
      return Problem(context, LocalizationApiErrorMapper.TenantIneligible);
    }

    var result = await resolver.ResolveExplicitBatchAsync(new LocalizationExplicitBatchRequest(
      request.ResourceKeys,
      request.Culture,
      request.PlaceholderValuesByResource), cancellationToken);
    return result.IsFailure
      ? Problem(context, Map(result.Error))
      : Results.Ok(MapEffective(request.Culture, catalog.CatalogVersion.Value, result.Value));
  }

  private static EffectiveLocalizationResponse MapEffective(
    string requestedCulture,
    long catalogVersion,
    IReadOnlyCollection<SSAS.BuildingBlocks.Localization.EffectiveLocalizedText> values)
  {
    var resolvedCultures = values.Select(value => value.ResolvedCulture.Value).Distinct(StringComparer.Ordinal).ToArray();
    var directions = values.Select(value => value.TextDirection.ToString().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
    var tenantVersions = values.Where(value => value.TenantLocalizationVersion is not null)
      .Select(value => value.TenantLocalizationVersion!.Value.Value).Distinct().ToArray();
    return new EffectiveLocalizationResponse(
      requestedCulture,
      resolvedCultures.Length == 1 ? resolvedCultures[0] : null,
      directions.Length == 1 ? directions[0] : null,
      values.FirstOrDefault()?.CatalogVersion.Value ?? catalogVersion,
      tenantVersions.Length == 1 ? tenantVersions[0] : null,
      values.Select(value => new EffectiveLocalizationItemResponse(
        value.ResourceKey.Value,
        value.Text,
        value.RequestedCulture.Value,
        value.ResolvedCulture.Value,
        value.TextDirection.ToString().ToLowerInvariant(),
        value.ResolutionSource.ToString(),
        value.ResourceVersion.Value)).ToArray());
  }

  private static bool TryEffectiveGroupQuery(IQueryCollection values, out LocalizationGroupBatchRequest request)
  {
    request = default!;
    if (!StrictRequestReader.HasOnly(values, ["culture", "module", "group"]) ||
      !StrictRequestReader.TryRequired(values, "culture", out var culture) ||
      !StrictRequestReader.TryRequired(values, "module", out var module) ||
      !StrictRequestReader.TryRequired(values, "group", out var group))
    {
      return false;
    }

    request = new LocalizationGroupBatchRequest(module, group, culture);
    return true;
  }

  private static async Task<bool> HasTrustedActiveTenantAsync(
    HttpContext context,
    ICurrentTenant currentTenant,
    IRequestTenantEligibility eligibility,
    CancellationToken cancellationToken)
  {
    if (currentTenant.TenantId is not { } tenantId || context.User.Identity?.IsAuthenticated != true)
    {
      return false;
    }

    var tenantClaims = context.User.Claims
      .Where(claim => StringComparer.Ordinal.Equals(claim.Type, JwtClaimTypes.TenantId))
      .Select(claim => Guid.TryParse(claim.Value, out var claimedTenantId) ? claimedTenantId : (Guid?)null)
      .ToArray();
    return tenantClaims.Length == 1 && tenantClaims[0] == tenantId &&
      (await eligibility.GetEligibilityAsync(tenantId, cancellationToken)).IsAuthenticationEligible;
  }

  private static bool TryListQuery(IQueryCollection values, out ListTenantLocalizationResourcesQuery query)
  {
    query = default!;
    if (!StrictRequestReader.HasOnly(values, ["culture", "pageNumber", "pageSize", "search", "module", "group", "category", "lifecycle", "overriddenOnly", "incompatibleOnly", "securityClassification"]) ||
      !StrictRequestReader.TryRequired(values, "culture", out var culture) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) || !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize) ||
      !StrictRequestReader.TryOptional(values, "search", out var search) || !StrictRequestReader.TryOptional(values, "module", out var module) ||
      !StrictRequestReader.TryOptional(values, "group", out var group) || !StrictRequestReader.TryOptional(values, "category", out var category) ||
      !StrictRequestReader.TryOptional(values, "lifecycle", out var lifecycle) || !StrictRequestReader.TryOptional(values, "securityClassification", out var classification) ||
      !StrictRequestReader.TryBool(values, "overriddenOnly", out var overriddenOnly) || !StrictRequestReader.TryBool(values, "incompatibleOnly", out var incompatibleOnly)) return false;
    if (!StrictRequestReader.IsOneOf(category, ["Action", "Label", "Validation", "Message", "Help"]) ||
      !StrictRequestReader.IsOneOf(lifecycle, ["Active", "Retired", "All"]) ||
      !StrictRequestReader.IsOneOf(classification, ["Ordinary", "SecuritySensitiveNonOverridable"])) return false;
    query = new ListTenantLocalizationResourcesQuery(culture, pageNumber, pageSize, search, module, group, category,
      lifecycle ?? "Active", overriddenOnly, incompatibleOnly, classification);
    return true;
  }

  private static bool TryResourceQuery(IQueryCollection values, string resourceKey, out GetTenantLocalizationResourceQuery query)
  {
    query = default!;
    if (!StrictRequestReader.HasOnly(values, ["culture"]) || !StrictRequestReader.TryRequired(values, "culture", out var culture) || string.IsNullOrWhiteSpace(resourceKey)) return false;
    query = new GetTenantLocalizationResourceQuery(resourceKey, culture);
    return true;
  }

  private static bool TryHistoryQuery(IQueryCollection values, string resourceKey, out GetTenantLocalizationHistoryQuery query)
  {
    query = default!;
    if (!StrictRequestReader.HasOnly(values, ["culture", "pageNumber", "pageSize"]) || !StrictRequestReader.TryRequired(values, "culture", out var culture) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) || !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize) || string.IsNullOrWhiteSpace(resourceKey)) return false;
    query = new GetTenantLocalizationHistoryQuery(resourceKey, culture, pageNumber, pageSize);
    return true;
  }

  private static bool IsStrictEffectiveBatchJson(JsonElement root)
  {
    if (!root.TryGetProperty("placeholderValuesByResource", out var placeholderValues)) return true;
    var requested = new HashSet<string>(StringComparer.Ordinal);
    foreach (var item in root.GetProperty("resourceKeys").EnumerateArray())
    {
      if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } resourceKey) return false;
      requested.Add(resourceKey);
    }

    var seenResources = new HashSet<string>(StringComparer.Ordinal);
    var resourceCount = 0;
    foreach (var resource in placeholderValues.EnumerateObject())
    {
      if (++resourceCount > LocalizationTextResolver.MaximumExplicitBatchSize ||
        !seenResources.Add(resource.Name) ||
        !requested.Contains(resource.Name) ||
        ResourceKey.Create(resource.Name).IsFailure ||
        resource.Value.ValueKind != JsonValueKind.Object)
      {
        return false;
      }

      var seenPlaceholders = new HashSet<string>(StringComparer.Ordinal);
      foreach (var placeholder in resource.Value.EnumerateObject())
      {
        if (!seenPlaceholders.Add(placeholder.Name) || placeholder.Value.ValueKind != JsonValueKind.String) return false;
      }
    }

    return true;
  }

  private static bool HasValidPlaceholderResourceScope(
    EffectiveLocalizationBatchRequest request,
    ILocalizationCatalog catalog)
  {
    if (request.PlaceholderValuesByResource is null) return true;
    var requested = request.ResourceKeys!.ToHashSet(StringComparer.Ordinal);
    foreach (var resourceKey in request.PlaceholderValuesByResource.Keys)
    {
      var parsed = ResourceKey.Create(resourceKey);
      if (!requested.Contains(resourceKey) || parsed.IsFailure || !catalog.TryGet(parsed.Value, out _)) return false;
    }

    return true;
  }
}

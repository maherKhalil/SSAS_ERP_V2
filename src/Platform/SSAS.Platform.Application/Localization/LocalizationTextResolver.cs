using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;

namespace SSAS.Platform.Application.Localization;

public sealed class LocalizationTextResolver(
  ILocalizationCatalog catalog,
  ITenantLocalizationOverrideReadService overrideReadService,
  ITenantLocalizationVersionReader versionReader,
  ILocalizationTenantCache cache,
  IRequestTenantEligibility eligibility,
  ILocalizationDiagnostics diagnostics,
  ICurrentTenant currentTenant) : ILocalizationTextResolver
{
  public const int MaximumExplicitBatchSize = 100;
  public const int MaximumGroupBatchSize = 250;
  private Task<TenantLocalizationVersionState>? versionStateTask;

  public async Task<Result<EffectiveLocalizedText>> ResolveTemplateAsync(
    LocalizationResolutionRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var batch = await ResolveTemplateExplicitBatchAsync(
      new LocalizationExplicitBatchRequest([request.ResourceKey], request.RequestedCulture),
      cancellationToken);
    return batch.IsSuccess
      ? Result.Success(batch.Value[0])
      : Result.Failure<EffectiveLocalizedText>(batch.Error);
  }

  public async Task<Result<EffectiveLocalizedText>> ResolveAsync(
    LocalizationResolutionRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var batch = await ResolveExplicitBatchAsync(
      new LocalizationExplicitBatchRequest(
        [request.ResourceKey],
        request.RequestedCulture,
        request.PlaceholderValues is null
          ? null
          : new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
          {
            [request.ResourceKey] = request.PlaceholderValues
          },
        request.FormattingContext),
      cancellationToken);
    return batch.IsSuccess
      ? Result.Success(batch.Value[0])
      : Result.Failure<EffectiveLocalizedText>(batch.Error);
  }

  public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveTemplateExplicitBatchAsync(
    LocalizationExplicitBatchRequest request,
    CancellationToken cancellationToken = default) =>
    ResolveExplicitBatchCoreAsync(request, formatPlaceholders: false, cancellationToken);

  public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveExplicitBatchAsync(
    LocalizationExplicitBatchRequest request,
    CancellationToken cancellationToken = default) =>
    ResolveExplicitBatchCoreAsync(request, formatPlaceholders: true, cancellationToken);

  private async Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveExplicitBatchCoreAsync(
    LocalizationExplicitBatchRequest request,
    bool formatPlaceholders,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(request.ResourceKeys);
    var keys = request.ResourceKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    if (keys.Length == 0)
    {
      return Result.Success<IReadOnlyList<EffectiveLocalizedText>>([]);
    }

    if (keys.Length > MaximumExplicitBatchSize)
    {
      return Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(LocalizationResolutionErrors.ExplicitBatchTooLarge);
    }

    var parsed = new List<ResourceKey>(keys.Length);
    foreach (var key in keys)
    {
      var resourceKey = ResourceKey.Create(key);
      if (resourceKey.IsFailure)
      {
        return Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(resourceKey.Error);
      }

      parsed.Add(resourceKey.Value);
    }

    return await ResolveManyAsync(
      parsed,
      request.RequestedCulture,
      request.PlaceholderValuesByResource,
      formatPlaceholders,
      cancellationToken);
  }

  public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveTemplateGroupAsync(
    LocalizationGroupBatchRequest request,
    CancellationToken cancellationToken = default) =>
    ResolveGroupCoreAsync(request, formatPlaceholders: false, cancellationToken);

  public Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveGroupAsync(
    LocalizationGroupBatchRequest request,
    CancellationToken cancellationToken = default) =>
    ResolveGroupCoreAsync(request, formatPlaceholders: true, cancellationToken);

  private async Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveGroupCoreAsync(
    LocalizationGroupBatchRequest request,
    bool formatPlaceholders,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.Group) ||
      !string.Equals(request.Module, request.Module.Trim(), StringComparison.Ordinal) ||
      !string.Equals(request.Group, request.Group.Trim(), StringComparison.Ordinal))
    {
      return Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(LocalizationResolutionErrors.InvalidGroup);
    }

    var resources = catalog.GetActiveGroup(request.Module, request.Group)
      .OrderBy(resource => resource.ResourceKey.Value, StringComparer.Ordinal)
      .ToArray();
    if (resources.Length > MaximumGroupBatchSize)
    {
      return Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(LocalizationResolutionErrors.GroupBatchTooLarge);
    }

    return await ResolveManyAsync(
      resources.Select(resource => resource.ResourceKey).ToArray(),
      request.RequestedCulture,
      request.PlaceholderValuesByResource,
      formatPlaceholders,
      cancellationToken);
  }

  private async Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveManyAsync(
    IReadOnlyCollection<ResourceKey> resourceKeys,
    string requestedCulture,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? placeholderValues,
    bool formatPlaceholders,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var culture = LocalizationCulture.Create(requestedCulture);
    if (culture.IsFailure)
    {
      return Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(culture.Error);
    }

    var definitions = new Dictionary<string, LocalizationResourceDefinition?>(StringComparer.Ordinal);
    foreach (var key in resourceKeys)
    {
      definitions[key.Value] = catalog.TryGet(key, out var definition) ? definition : null;
      if (definition is null)
      {
        diagnostics.RecordMissingResource(key.Value);
      }
    }

    Guid? eligibleTenantId = null;
    if (currentTenant.TenantId is { } tenantId)
    {
      var tenantEligibility = await eligibility.GetEligibilityAsync(tenantId, cancellationToken);
      if (tenantEligibility.IsAuthenticationEligible)
      {
        eligibleTenantId = tenantId;
      }
    }

    TenantLocalizationVersionState? versionState = null;
    IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?> overrides =
      new Dictionary<string, TenantLocalizationOverrideReadModel?>(StringComparer.Ordinal);
    var tenantKeys = definitions.Values
      .Where(definition => definition?.IsTenantEditable == true)
      .Select(definition => definition!.ResourceKey)
      .OrderBy(key => key.Value, StringComparer.Ordinal)
      .ToArray();
    if (eligibleTenantId is { } trustedTenantId && tenantKeys.Length > 0)
    {
      versionStateTask ??= cache.GetVersionStateAsync(trustedTenantId, versionReader, cancellationToken);
      versionState = await versionStateTask;
      if (versionState.Trust != TenantLocalizationCacheTrust.Degraded)
      {
        try
        {
          overrides = await cache.GetOrCreateAsync(
            trustedTenantId,
            culture.Value.Value,
            catalog.CatalogVersion.Value,
            versionState.Version,
            tenantKeys.Select(key => key.Value).ToArray(),
            token => overrideReadService.ReadAsync(trustedTenantId, culture.Value, tenantKeys, token),
            cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
          diagnostics.RecordDegradedTenant(trustedTenantId);
          overrides = new Dictionary<string, TenantLocalizationOverrideReadModel?>(StringComparer.Ordinal);
        }
      }
      else
      {
        diagnostics.RecordDegradedTenant(trustedTenantId);
      }
    }

    var resolved = new List<EffectiveLocalizedText>(resourceKeys.Count);
    foreach (var key in resourceKeys.OrderBy(key => key.Value, StringComparer.Ordinal))
    {
      var definition = definitions[key.Value];
      var values = placeholderValues is not null && placeholderValues.TryGetValue(key.Value, out var supplied)
        ? supplied
        : null;
      var item = ResolveOne(key, culture.Value, definition, values, overrides, versionState, formatPlaceholders);
      if (item.IsFailure)
      {
        return Result.Failure<IReadOnlyList<EffectiveLocalizedText>>(item.Error);
      }

      resolved.Add(item.Value);
    }

    return Result.Success<IReadOnlyList<EffectiveLocalizedText>>(resolved);
  }

  private Result<EffectiveLocalizedText> ResolveOne(
    ResourceKey resourceKey,
    LocalizationCulture requestedCulture,
    LocalizationResourceDefinition? definition,
    IReadOnlyDictionary<string, string>? placeholderValues,
    IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?> overrides,
    TenantLocalizationVersionState? versionState,
    bool formatPlaceholders)
  {
    if (definition is null || definition.Lifecycle != LocalizationResourceLifecycle.Active)
    {
      return Result.Success(new EffectiveLocalizedText(
        resourceKey,
        requestedCulture,
        requestedCulture,
        catalog.GetNeutralFallback(requestedCulture),
        LocalizationResolutionSource.KeyFallback,
        catalog.CatalogVersion,
        ResourceVersion.Create(1).Value,
        null,
        null,
        requestedCulture.Direction,
        true,
        false));
    }

    var hasOverride = overrides.TryGetValue(resourceKey.Value, out var candidate) && candidate is not null;
    var compatible = hasOverride && IsCompatible(candidate!, definition);
    if (compatible && candidate!.IsActive && candidate.Value is not null)
    {
      var formattedOverride = Format(candidate.Value, definition.Placeholders, placeholderValues, formatPlaceholders);
      if (formattedOverride.IsFailure)
      {
        return Result.Failure<EffectiveLocalizedText>(formattedOverride.Error);
      }

      return Result.Success(new EffectiveLocalizedText(
        resourceKey,
        requestedCulture,
        requestedCulture,
        formattedOverride.Value,
        LocalizationResolutionSource.TenantOverride,
        catalog.CatalogVersion,
        definition.ResourceVersion,
        TenantLocalizationVersion.Create(versionState!.Version).Value,
        TenantOverrideVersion.Create(candidate.TenantOverrideVersion).Value,
        requestedCulture.Direction,
        false,
        true));
    }

    var requestedDefault = definition.GetDefault(requestedCulture);
    var useEnglishFallback = requestedCulture.Value == LocalizationCulture.ArabicCode && string.IsNullOrEmpty(requestedDefault);
    var resolvedCulture = useEnglishFallback ? LocalizationCulture.English : requestedCulture;
    var formattedDefault = Format(
      useEnglishFallback ? definition.EnglishDefault : requestedDefault,
      definition.Placeholders,
      placeholderValues,
      formatPlaceholders);
    if (formattedDefault.IsFailure)
    {
      return Result.Failure<EffectiveLocalizedText>(formattedDefault.Error);
    }

    return Result.Success(new EffectiveLocalizedText(
      resourceKey,
      requestedCulture,
      resolvedCulture,
      formattedDefault.Value,
      useEnglishFallback ? LocalizationResolutionSource.EnglishFallback : LocalizationResolutionSource.SystemDefault,
      catalog.CatalogVersion,
      definition.ResourceVersion,
      versionState is null ? null : TenantLocalizationVersion.Create(versionState.Version).Value,
      null,
      resolvedCulture.Direction,
      useEnglishFallback,
      !hasOverride || compatible));
  }

  private static Result<string> Format(
    string template,
    PlaceholderSet placeholders,
    IReadOnlyDictionary<string, string>? placeholderValues,
    bool formatPlaceholders) =>
    formatPlaceholders
      ? LocalizationPlaceholderFormatter.Format(template, placeholders, placeholderValues)
      : Result.Success(template);

  private static bool IsCompatible(
    TenantLocalizationOverrideReadModel candidate,
    LocalizationResourceDefinition definition) =>
    definition.IsTenantEditable &&
    candidate.TextFormat == definition.TextFormat &&
    candidate.PlaceholderFingerprint.AsSpan().SequenceEqual(definition.PlaceholderFingerprint.Bytes) &&
    candidate.CompatibilityFingerprint.AsSpan().SequenceEqual(definition.CompatibilityFingerprint.Bytes);
}

using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using LocalizationDomainErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

public sealed record PreviewTenantLocalizationOverrideCommand(string ResourceKey, string Culture, string Value);

public sealed record LocalizationPreviewResult(
  string ResourceKey,
  string Culture,
  string Value,
  string TextFormat,
  IReadOnlyList<string> Placeholders,
  string Direction,
  long CatalogVersion,
  int ResourceVersion,
  string RequestedCulture,
  string ResolvedCulture);

public sealed class PreviewTenantLocalizationOverrideCommandHandler(
  ILocalizationCatalog catalog,
  IRequestTenantEligibility eligibility,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<LocalizationPreviewResult>> HandleAsync(
    PreviewTenantLocalizationOverrideCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure) return Result.Failure<LocalizationPreviewResult>(execution.Error);

    var tenantEligibility = await eligibility.GetEligibilityAsync(execution.Value.TenantId, cancellationToken);
    if (!tenantEligibility.IsAuthenticationEligible)
      return Result.Failure<LocalizationPreviewResult>(LocalizationDomainErrors.TenantIneligible);

    var validated = LocalizationApplicationValidation.GetEditableDefinition(catalog, command.ResourceKey, command.Culture);
    if (validated.IsFailure) return Result.Failure<LocalizationPreviewResult>(validated.Error);
    var text = LocalizationApplicationValidation.GetText(command.Value, validated.Value.Definition);
    if (text.IsFailure) return Result.Failure<LocalizationPreviewResult>(text.Error);

    return Result.Success(new LocalizationPreviewResult(
      validated.Value.ResourceKey.Value,
      validated.Value.Culture.Value,
      text.Value.Value,
      validated.Value.Definition.TextFormat.ToString(),
      validated.Value.Definition.Placeholders.Names,
      validated.Value.Culture.Direction.ToString().ToLowerInvariant(),
      catalog.CatalogVersion.Value,
      validated.Value.Definition.ResourceVersion.Value,
      validated.Value.Culture.Value,
      validated.Value.Culture.Value));
  }
}

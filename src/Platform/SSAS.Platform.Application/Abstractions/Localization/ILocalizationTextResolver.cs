using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Localization;

namespace SSAS.Platform.Application.Abstractions.Localization;

public interface ILocalizationTextResolver
{
  Task<Result<EffectiveLocalizedText>> ResolveAsync(
    LocalizationResolutionRequest request,
    CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveExplicitBatchAsync(
    LocalizationExplicitBatchRequest request,
    CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<EffectiveLocalizedText>>> ResolveGroupAsync(
    LocalizationGroupBatchRequest request,
    CancellationToken cancellationToken = default);
}

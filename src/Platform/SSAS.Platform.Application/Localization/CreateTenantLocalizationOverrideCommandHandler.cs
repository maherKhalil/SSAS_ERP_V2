using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Localization;
using LocalizationErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

public sealed class CreateTenantLocalizationOverrideCommandHandler(
  ITenantLocalizationSettingsRepository settingsRepository,
  ITenantLocalizationOverrideRepository overrideRepository,
  ITenantAuthenticationEligibilityReadService eligibilityReadService,
  IPlatformUnitOfWork unitOfWork,
  ILocalizationCatalog catalog,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<LocalizationMutationResult>> HandleAsync(
    CreateTenantLocalizationOverrideCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(execution.Error);
    }

    var (tenantId, actor) = execution.Value;
    var eligibility = await eligibilityReadService.GetEligibilityForUpdateAsync(tenantId, cancellationToken);
    if (!eligibility.IsAuthenticationEligible)
    {
      return Result.Failure<LocalizationMutationResult>(LocalizationErrors.TenantIneligible);
    }

    var validated = LocalizationApplicationValidation.GetEditableDefinition(catalog, command.ResourceKey, command.Culture);
    if (validated.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(validated.Error);
    }

    var text = LocalizationApplicationValidation.GetText(command.Value, validated.Value.Definition);
    if (text.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(text.Error);
    }

    var settings = await settingsRepository.GetOrCreateForUpdateAsync(tenantId, LocalizationCulture.English, cancellationToken);
    var existing = await overrideRepository.GetForUpdateAsync(
      tenantId,
      validated.Value.ResourceKey,
      validated.Value.Culture,
      cancellationToken);
    if (existing is not null)
    {
      return Result.Failure<LocalizationMutationResult>(LocalizationErrors.OverrideAlreadyExists);
    }

    var incremented = settings.IncrementVersion();
    if (incremented.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(incremented.Error);
    }

    var created = TenantLocalizationOverride.Create(
      tenantId,
      validated.Value.Culture,
      validated.Value.Definition,
      text.Value,
      catalog.CatalogVersion,
      actor,
      Guid.NewGuid(),
      clock.UtcNow,
      settings.TenantLocalizationVersion);
    if (created.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(created.Error);
    }

    await overrideRepository.AddAsync(created.Value, cancellationToken);
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(saved.Error == IdentityAccessErrors.UniqueConstraintViolation
        ? LocalizationErrors.OverrideAlreadyExists
        : saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(ToResult(created.Value, settings));
  }

  private static LocalizationMutationResult ToResult(
    TenantLocalizationOverride localizationOverride,
    TenantLocalizationSettings settings) => new(
      localizationOverride.Id,
      localizationOverride.CurrentVersionNumber.Value,
      settings.TenantLocalizationVersion.Value,
      [.. localizationOverride.RowVersion]);
}

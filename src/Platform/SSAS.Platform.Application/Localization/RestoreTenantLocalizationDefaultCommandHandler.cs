using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Localization;
using LocalizationErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

public sealed class RestoreTenantLocalizationDefaultCommandHandler(
  ITenantLocalizationSettingsRepository settingsRepository,
  ITenantLocalizationOverrideRepository overrideRepository,
  ITenantAuthenticationEligibilityReadService eligibilityReadService,
  ILocalizationManagementAuditReadiness auditReadiness,
  IPlatformUnitOfWork unitOfWork,
  ILocalizationCatalog catalog,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<LocalizationMutationResult>> HandleAsync(
    RestoreTenantLocalizationDefaultCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(execution.Error);
    }

    var (tenantId, actor) = execution.Value;
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var eligibility = await eligibilityReadService.GetEligibilityForUpdateAsync(tenantId, cancellationToken);
    if (!eligibility.IsAuthenticationEligible)
    {
      return Result.Failure<LocalizationMutationResult>(LocalizationErrors.TenantIneligible);
    }

    var audit = await LocalizationManagementAuditGuard.CheckAsync(auditReadiness, cancellationToken);
    if (audit.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(audit.Error);
    }

    var validated = LocalizationApplicationValidation.GetEditableDefinition(catalog, command.ResourceKey, command.Culture);
    if (validated.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(validated.Error);
    }

    var settings = await settingsRepository.GetOrCreateForUpdateAsync(tenantId, LocalizationCulture.English, cancellationToken);
    var localizationOverride = await overrideRepository.GetForUpdateAsync(
      tenantId,
      validated.Value.ResourceKey,
      validated.Value.Culture,
      cancellationToken);
    if (localizationOverride is null)
    {
      return Result.Failure<LocalizationMutationResult>(LocalizationErrors.OverrideMissing);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(localizationOverride.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure<LocalizationMutationResult>(IdentityAccessErrors.ConcurrencyConflict);
    }

    if (!localizationOverride.IsActive)
    {
      return Result.Failure<LocalizationMutationResult>(LocalizationErrors.OverrideAlreadyDefault);
    }

    var current = await overrideRepository.GetVersionSnapshotAsync(
      localizationOverride.Id,
      localizationOverride.CurrentVersionNumber,
      cancellationToken);
    if (current is null)
    {
      return Result.Failure<LocalizationMutationResult>(LocalizationErrors.UndoTargetInvalid);
    }

    var incremented = settings.IncrementVersion();
    if (incremented.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(incremented.Error);
    }

    var restored = localizationOverride.RestoreDefault(
      current,
      validated.Value.Definition,
      actor,
      Guid.NewGuid(),
      clock.UtcNow,
      settings.TenantLocalizationVersion,
      catalog.CatalogVersion);
    if (restored.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(restored.Error);
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<LocalizationMutationResult>(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(new LocalizationMutationResult(
      localizationOverride.Id,
      localizationOverride.CurrentVersionNumber.Value,
      settings.TenantLocalizationVersion.Value,
      [.. localizationOverride.RowVersion], localizationOverride.CurrentValue, localizationOverride.IsActive));
  }
}

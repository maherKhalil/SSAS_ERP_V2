using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Domain.Localization;

public static class LocalizationUndoLineage
{
  public static Result<TenantOverrideVersion> GetEligibleTarget(LocalizationVersionSnapshot current)
  {
    ArgumentNullException.ThrowIfNull(current);
    return current.PriorLogicalVersionNumber is { } target
      ? Result.Success(target)
      : Result.Failure<TenantOverrideVersion>(LocalizationErrors.UndoNotAvailable);
  }

  public static Result ValidateTarget(
    LocalizationVersionSnapshot current,
    LocalizationVersionSnapshot target,
    TenantOverrideVersion advertisedTarget,
    CompatibilityFingerprint currentCompatibility)
  {
    ArgumentNullException.ThrowIfNull(current);
    ArgumentNullException.ThrowIfNull(target);
    ArgumentNullException.ThrowIfNull(currentCompatibility);

    if (current.PriorLogicalVersionNumber is null)
    {
      return Result.Failure(LocalizationErrors.UndoNotAvailable);
    }

    if (current.PriorLogicalVersionNumber.Value != advertisedTarget || target.VersionNumber != advertisedTarget)
    {
      return Result.Failure(LocalizationErrors.UndoTargetInvalid);
    }

    return target.CompatibilityFingerprint.Equals(currentCompatibility)
      ? Result.Success()
      : Result.Failure(LocalizationErrors.UndoTargetIncompatible);
  }
}

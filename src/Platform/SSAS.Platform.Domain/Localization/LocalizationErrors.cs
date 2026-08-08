using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Localization;

public static class LocalizationErrors
{
  public static readonly Error ResourceNotFound = new("localization.resource_not_found", "The localization resource was not found.");
  public static readonly Error ResourceRetired = new("localization.resource_retired", "The localization resource is retired.");
  public static readonly Error ResourceNotOverridable = new("localization.resource_not_overridable", "The localization resource cannot be overridden.");
  public static readonly Error SecuritySensitive = new("localization.security_sensitive", "Security-sensitive text cannot be overridden.");
  public static readonly Error TenantIneligible = new("localization.tenant_ineligible", "The Tenant is not eligible for localization customization.");
  public static readonly Error OverrideAlreadyExists = new("localization.override_already_exists", "The localization override already exists.");
  public static readonly Error OverrideMissing = new("localization.override_missing", "The localization override does not exist.");
  public static readonly Error OverrideAlreadyDefault = new("localization.override_already_default", "The localization override already resolves to the system default.");
  public static readonly Error UndoNotAvailable = new("localization.undo_not_available", "No localization override version is available to undo.");
  public static readonly Error UndoTargetInvalid = new("localization.undo_target_invalid", "The localization undo target is invalid.");
  public static readonly Error UndoTargetIncompatible = new("localization.undo_target_incompatible", "The localization undo target is incompatible with the current catalog.");
  public static readonly Error InvalidActor = new("localization.actor_invalid", "A valid localization actor is required.");
}

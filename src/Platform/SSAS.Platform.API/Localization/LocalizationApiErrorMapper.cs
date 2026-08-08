namespace SSAS.Platform.API.Localization;

public sealed record LocalizationApiError(int StatusCode, string Code);

public static class LocalizationApiErrorMapper
{
  public const string GenericProblemResourceKey = "platform.authentication.errors.request_rejected";

  public static readonly LocalizationApiError InvalidRequest = new(400, "request.invalid");
  public static readonly LocalizationApiError UnsupportedCulture = new(400, "localization.culture_unsupported");
  public static readonly LocalizationApiError ExplicitBatchTooLarge = new(400, "localization.explicit_batch_too_large");
  public static readonly LocalizationApiError GroupBatchTooLarge = new(400, "localization.group_batch_too_large");
  public static readonly LocalizationApiError InvalidRowVersion = new(400, "localization.rowversion_invalid");
  public static readonly LocalizationApiError ConcurrencyConflict = new(409, "concurrency.conflict");
  public static readonly LocalizationApiError OverrideAlreadyExists = new(409, "localization.override_already_exists");
  public static readonly LocalizationApiError OverrideMissing = new(409, "localization.override_missing");
  public static readonly LocalizationApiError UndoNotAvailable = new(409, "localization.undo_not_available");
  public static readonly LocalizationApiError OverrideAlreadyDefault = new(409, "localization.override_already_default");
  public static readonly LocalizationApiError UndoTargetInvalid = new(422, "localization.undo_target_invalid");
  public static readonly LocalizationApiError UndoTargetIncompatible = new(422, "localization.undo_target_incompatible");
  public static readonly LocalizationApiError ResourceNotFound = new(404, "localization.resource_not_found");
  public static readonly LocalizationApiError TenantIneligible = new(403, "localization.tenant_ineligible");
  public static readonly LocalizationApiError TextPolicyInvalid = new(422, "localization.text_invalid");
  public static readonly LocalizationApiError PlaceholderInvalid = new(422, "localization.placeholder_invalid");
  public static readonly LocalizationApiError PlaceholderMismatch = new(422, "localization.placeholder_mismatch");
  public static readonly LocalizationApiError ResourceRetired = new(422, "localization.resource_retired");
  public static readonly LocalizationApiError ResourceNotOverridable = new(422, "localization.resource_not_overridable");
  public static readonly LocalizationApiError SecuritySensitive = new(422, "localization.security_sensitive");
  public static readonly LocalizationApiError AuditReadinessUnavailable = new(503, "localization.audit_readiness_unavailable");

  public static bool TryMap(string technicalCode, out LocalizationApiError error)
  {
    error = technicalCode switch
    {
      "Persistence.ConcurrencyConflict" => ConcurrencyConflict,
      "localization.override_already_exists" => OverrideAlreadyExists,
      "localization.override_missing" => OverrideMissing,
      "localization.undo_not_available" => UndoNotAvailable,
      "localization.override_already_default" => OverrideAlreadyDefault,
      "localization.undo_target_invalid" => UndoTargetInvalid,
      "localization.undo_target_incompatible" => UndoTargetIncompatible,
      "localization.resource_not_found" => ResourceNotFound,
      "localization.tenant_ineligible" => TenantIneligible,
      "localization.resource_key_invalid" => InvalidRequest,
      "localization.culture_unsupported" => UnsupportedCulture,
      "localization.explicit_batch_too_large" => ExplicitBatchTooLarge,
      "localization.group_batch_too_large" => GroupBatchTooLarge,
      "localization.text_invalid" => TextPolicyInvalid,
      "localization.placeholder_invalid" => PlaceholderInvalid,
      "localization.placeholder_mismatch" => PlaceholderMismatch,
      "localization.resource_retired" => ResourceRetired,
      "localization.resource_not_overridable" => ResourceNotOverridable,
      "localization.security_sensitive" => SecuritySensitive,
      "request.invalid" => InvalidRequest,
      "localization.audit_readiness_unavailable" => AuditReadinessUnavailable,
      _ => null!
    };
    return error is not null;
  }
}

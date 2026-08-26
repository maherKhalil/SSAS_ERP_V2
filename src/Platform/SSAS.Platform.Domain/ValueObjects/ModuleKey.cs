using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

// THE ONE TOKEN A PLAN GRANT AND A ROUTE GATE BOTH RESOLVE AGAINST (FP-014, `OD-SUB-0005`).
//
// `REQ-SUB-0015` requires routes and permissions to gate on the SAME unit, so two notions of "module" would
// make that requirement unsatisfiable. This is that unit: the key declared by the thing carrying one
// `IPermissionCatalogContributor` and one `Add*Module()` registration — four today, and the same strings
// T-032's `IModuleEnablementDescriptor` implementations declare in code.
//
// **Stable and never reused.** Once per-tenant assignment data exists, changing a key silently un-entitles
// every tenant holding the old value. Comparison is ORDINAL for the same reason `NormalizedTenantCode` is:
// a culture-sensitive match could make two distinct keys equal on one machine and not another.
public sealed class ModuleKey : ValueObject
{
  public const int MaximumLength = 64;

  private ModuleKey(string value) => Value = value;

  public string Value { get; }

  public static Result<ModuleKey> Create(string? value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaximumLength
      ? Result.Failure<ModuleKey>(SubscriptionErrors.InvalidModuleKey)
      : Result.Success(new ModuleKey(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}

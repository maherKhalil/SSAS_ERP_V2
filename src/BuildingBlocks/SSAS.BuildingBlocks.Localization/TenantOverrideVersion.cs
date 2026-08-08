using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public readonly record struct TenantOverrideVersion
{
  private TenantOverrideVersion(long value) => Value = value;

  public long Value { get; }

  public static Result<TenantOverrideVersion> Create(long value) => value > 0
    ? Result.Success(new TenantOverrideVersion(value))
    : Result.Failure<TenantOverrideVersion>(LocalizationErrors.VersionInvalid);

  public Result<TenantOverrideVersion> Increment() => Value == long.MaxValue
    ? Result.Failure<TenantOverrideVersion>(LocalizationErrors.VersionOverflow)
    : Create(Value + 1);

  public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

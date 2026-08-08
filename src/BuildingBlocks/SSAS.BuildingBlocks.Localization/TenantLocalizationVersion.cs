using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public readonly record struct TenantLocalizationVersion
{
  private TenantLocalizationVersion(long value) => Value = value;

  public long Value { get; }

  public static Result<TenantLocalizationVersion> Create(long value) => value > 0
    ? Result.Success(new TenantLocalizationVersion(value))
    : Result.Failure<TenantLocalizationVersion>(LocalizationErrors.VersionInvalid);

  public Result<TenantLocalizationVersion> Increment() => Value == long.MaxValue
    ? Result.Failure<TenantLocalizationVersion>(LocalizationErrors.VersionOverflow)
    : Create(Value + 1);

  public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

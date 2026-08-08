using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public readonly record struct CatalogVersion
{
  private CatalogVersion(long value) => Value = value;

  public long Value { get; }

  public static Result<CatalogVersion> Create(long value) => value > 0
    ? Result.Success(new CatalogVersion(value))
    : Result.Failure<CatalogVersion>(LocalizationErrors.VersionInvalid);

  public Result<CatalogVersion> Increment() => Value == long.MaxValue
    ? Result.Failure<CatalogVersion>(LocalizationErrors.VersionOverflow)
    : Create(Value + 1);

  public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

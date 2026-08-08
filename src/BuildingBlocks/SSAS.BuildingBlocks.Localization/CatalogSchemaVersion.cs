using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public readonly record struct CatalogSchemaVersion
{
  private CatalogSchemaVersion(int value) => Value = value;

  public int Value { get; }

  public static Result<CatalogSchemaVersion> Create(int value) => value > 0
    ? Result.Success(new CatalogSchemaVersion(value))
    : Result.Failure<CatalogSchemaVersion>(LocalizationErrors.VersionInvalid);

  public Result<CatalogSchemaVersion> Increment() => Value == int.MaxValue
    ? Result.Failure<CatalogSchemaVersion>(LocalizationErrors.VersionOverflow)
    : Create(Value + 1);

  public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public readonly record struct ResourceVersion
{
  private ResourceVersion(int value) => Value = value;

  public int Value { get; }

  public static Result<ResourceVersion> Create(int value) => value > 0
    ? Result.Success(new ResourceVersion(value))
    : Result.Failure<ResourceVersion>(LocalizationErrors.VersionInvalid);

  public Result<ResourceVersion> Increment() => Value == int.MaxValue
    ? Result.Failure<ResourceVersion>(LocalizationErrors.VersionOverflow)
    : Create(Value + 1);

  public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

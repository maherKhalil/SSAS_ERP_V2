using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed class PlaceholderSet : ValueObject
{
  private readonly string[] names;

  internal PlaceholderSet(IEnumerable<string> names)
  {
    this.names = names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
  }

  public IReadOnlyList<string> Names => names;

  public bool Matches(PlaceholderSet other) => names.SequenceEqual(other.names, StringComparer.Ordinal);

  public static Result<PlaceholderSet> Create(IEnumerable<string> values)
  {
    ArgumentNullException.ThrowIfNull(values);
    var names = new List<string>();
    foreach (var value in values)
    {
      var name = PlaceholderName.Create(value);
      if (name.IsFailure)
      {
        return Result.Failure<PlaceholderSet>(name.Error);
      }

      names.Add(name.Value.Value);
    }

    return Result.Success(new PlaceholderSet(names));
  }

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    foreach (var name in names)
    {
      yield return name;
    }
  }
}

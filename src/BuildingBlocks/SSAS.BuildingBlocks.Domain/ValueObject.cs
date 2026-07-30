namespace SSAS.BuildingBlocks.Domain;

public abstract class ValueObject
{
  protected abstract IEnumerable<object?> GetEqualityComponents();

  public override bool Equals(object? obj)
  {
    return obj is ValueObject other &&
      GetType() == other.GetType() &&
      GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
  }

  public override int GetHashCode()
  {
    var hash = new HashCode();

    foreach (var component in GetEqualityComponents())
    {
      hash.Add(component);
    }

    return hash.ToHashCode();
  }
}

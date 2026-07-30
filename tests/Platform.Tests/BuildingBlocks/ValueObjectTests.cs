using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Tests.BuildingBlocks;

public sealed class ValueObjectTests
{
  [Fact]
  public void Value_objects_are_equal_when_all_components_are_equal()
  {
    Assert.Equal(new Money("SAR", 100m), new Money("SAR", 100m));
  }

  [Fact]
  public void Value_objects_are_not_equal_when_a_component_differs()
  {
    Assert.NotEqual(new Money("SAR", 100m), new Money("USD", 100m));
  }

  private sealed class Money(string currency, decimal amount) : ValueObject
  {
    protected override IEnumerable<object?> GetEqualityComponents()
    {
      yield return currency;
      yield return amount;
    }
  }
}

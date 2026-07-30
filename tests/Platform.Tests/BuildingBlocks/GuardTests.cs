using SSAS.BuildingBlocks.SharedKernel;

namespace SSAS.Platform.Tests.BuildingBlocks;

public sealed class GuardTests
{
  [Fact]
  public void Against_null_returns_the_supplied_value()
  {
    var value = new object();

    var guardedValue = Guard.AgainstNull(value, nameof(value));

    Assert.Same(value, guardedValue);
  }

  [Fact]
  public void Against_null_or_whitespace_throws_for_whitespace()
  {
    Assert.Throws<ArgumentException>(() => Guard.AgainstNullOrWhiteSpace(" ", "value"));
  }
}

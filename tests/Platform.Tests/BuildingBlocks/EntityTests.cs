using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Tests.BuildingBlocks;

public sealed class EntityTests
{
  [Fact]
  public void Entities_with_the_same_type_and_identifier_are_equal()
  {
    var id = Guid.NewGuid();

    Assert.Equal(new TestEntity(id), new TestEntity(id));
  }

  [Fact]
  public void Entities_with_different_types_are_not_equal()
  {
    var id = Guid.NewGuid();

    Assert.False(new TestEntity(id).Equals(new OtherTestEntity(id)));
  }

  private sealed class TestEntity(Guid id) : Entity<Guid>(id);

  private sealed class OtherTestEntity(Guid id) : Entity<Guid>(id);
}

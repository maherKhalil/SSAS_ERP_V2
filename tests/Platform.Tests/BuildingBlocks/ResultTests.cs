using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Tests.BuildingBlocks;

public sealed class ResultTests
{
  [Fact]
  public void Success_creates_a_successful_result_without_an_error()
  {
    var result = Result.Success();

    Assert.True(result.IsSuccess);
    Assert.False(result.IsFailure);
    Assert.Equal(Error.None, result.Error);
  }

  [Fact]
  public void Failure_creates_a_failed_result_with_the_given_error()
  {
    var error = new Error("validation.required", "A value is required.");

    var result = Result.Failure(error);

    Assert.True(result.IsFailure);
    Assert.Equal(error, result.Error);
  }

  [Fact]
  public void Generic_success_exposes_its_value()
  {
    var result = Result.Success("value");

    Assert.True(result.IsSuccess);
    Assert.Equal("value", result.Value);
  }

  [Fact]
  public void Failed_generic_result_does_not_expose_a_value()
  {
    var result = Result.Failure<string>(new Error("failure", "Failed."));

    Assert.Throws<InvalidOperationException>(() => _ = result.Value);
  }
}

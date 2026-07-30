namespace SSAS.BuildingBlocks.Domain;

public sealed class Result<T> : Result
{
  private readonly T? value;

  internal Result(T? value, bool isSuccess, Error error)
    : base(isSuccess, error)
  {
    this.value = value;
  }

  public T Value => IsSuccess
    ? value!
    : throw new InvalidOperationException("A failed result does not contain a value.");
}

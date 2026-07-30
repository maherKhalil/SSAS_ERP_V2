namespace SSAS.BuildingBlocks.Domain;

public class Result
{
  protected Result(bool isSuccess, Error error)
  {
    if (isSuccess && error != Error.None)
    {
      throw new ArgumentException("A successful result cannot contain an error.", nameof(error));
    }

    if (!isSuccess && error == Error.None)
    {
      throw new ArgumentException("A failed result must contain an error.", nameof(error));
    }

    IsSuccess = isSuccess;
    Error = error;
  }

  public bool IsSuccess { get; }

  public bool IsFailure => !IsSuccess;

  public Error Error { get; }

  public static Result Success()
  {
    return new Result(true, Error.None);
  }

  public static Result Failure(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return new Result(false, error);
  }

  public static Result<T> Success<T>(T value)
  {
    return new Result<T>(value, true, Error.None);
  }

  public static Result<T> Failure<T>(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return new Result<T>(default, false, error);
  }
}

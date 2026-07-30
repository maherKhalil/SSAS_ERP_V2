namespace SSAS.BuildingBlocks.SharedKernel;

public static class Guard
{
  public static T AgainstNull<T>(T? value, string parameterName)
    where T : class
  {
    return value ?? throw new ArgumentNullException(parameterName);
  }

  public static string AgainstNullOrWhiteSpace(string? value, string parameterName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
    }

    return value;
  }

  public static void AgainstOutOfRange(bool condition, string parameterName, string message)
  {
    if (condition)
    {
      throw new ArgumentOutOfRangeException(parameterName, message);
    }
  }
}

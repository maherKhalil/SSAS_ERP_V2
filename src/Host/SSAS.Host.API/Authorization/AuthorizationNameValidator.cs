namespace SSAS.Host.API.Authorization;

internal static class AuthorizationNameValidator
{
  public static bool IsValid(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return false;
    }

    var requiresSegmentStart = true;
    foreach (var character in value)
    {
      if (character == '.')
      {
        if (requiresSegmentStart)
        {
          return false;
        }

        requiresSegmentStart = true;
        continue;
      }

      if (requiresSegmentStart)
      {
        if (!char.IsAsciiLetter(character))
        {
          return false;
        }

        requiresSegmentStart = false;
        continue;
      }

      if (!char.IsAsciiLetterOrDigit(character) && character != '_')
      {
        return false;
      }
    }

    return !requiresSegmentStart;
  }
}

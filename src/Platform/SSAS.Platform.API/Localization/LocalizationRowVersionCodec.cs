namespace SSAS.Platform.API.Localization;

public static class LocalizationRowVersionCodec
{
  public const int SqlServerRowVersionLength = 8;

  public static string Encode(byte[] rowVersion)
  {
    ArgumentNullException.ThrowIfNull(rowVersion);
    if (rowVersion.Length != SqlServerRowVersionLength)
    {
      throw new ArgumentException($"A SQL Server rowversion must contain exactly {SqlServerRowVersionLength} bytes.", nameof(rowVersion));
    }

    return Convert.ToBase64String(rowVersion);
  }

  public static bool TryDecode(string? value, out byte[] rowVersion)
  {
    rowVersion = [];
    if (string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace))
    {
      return false;
    }

    try
    {
      var decoded = Convert.FromBase64String(value);
      if (decoded.Length != SqlServerRowVersionLength ||
        !string.Equals(Convert.ToBase64String(decoded), value, StringComparison.Ordinal))
      {
        return false;
      }

      rowVersion = decoded;
      return true;
    }
    catch (FormatException)
    {
      return false;
    }
  }
}

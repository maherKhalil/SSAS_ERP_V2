namespace SSAS.Platform.API.Transport;

// Neutral, platform-wide SQL Server rowversion <-> HTTP codec implementing the approved
// convention in docs/08-Development/Development-Standards.md ("Optimistic Concurrency
// (RowVersion) Transport"): canonical padded RFC 4648 Base64 of an exactly-8-byte value.
// This is the single shared codec; feature transports must not define their own.
public static class RowVersionCodec
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

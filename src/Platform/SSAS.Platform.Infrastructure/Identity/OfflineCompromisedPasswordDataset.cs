using System.Globalization;

namespace SSAS.Platform.Infrastructure.Identity;

internal static class OfflineCompromisedPasswordDataset
{
  internal const long MaximumFileSizeBytes = 64L * 1024 * 1024;
  internal const int MaximumHashCount = 1_000_000;

  public static HashSet<string> Load(string path)
  {
    var fileLength = new FileInfo(path).Length;
    if (fileLength > MaximumFileSizeBytes)
    {
      throw new InvalidDataException("The compromised-password dataset exceeds the supported file-size limit.");
    }

    var hashes = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(path))
    {
      var value = line.Trim();
      if (value.Length == 0 || value.StartsWith('#'))
      {
        continue;
      }

      if (!IsSha256Hex(value))
      {
        throw new InvalidDataException("The compromised-password dataset contains an invalid SHA-256 hash.");
      }

      hashes.Add(value.ToUpperInvariant());
      if (hashes.Count > MaximumHashCount)
      {
        throw new InvalidDataException("The compromised-password dataset exceeds the supported hash-count limit.");
      }
    }

    return hashes;
  }

  public static int Validate(string path) => Load(path).Count;

  private static bool IsSha256Hex(string value) => value.Length == 64 &&
    value.All(character => Uri.IsHexDigit(character)) &&
    ulong.TryParse(value.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
}

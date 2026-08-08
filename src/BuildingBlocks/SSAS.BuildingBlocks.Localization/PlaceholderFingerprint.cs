using System.Security.Cryptography;
using System.Text;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed class PlaceholderFingerprint : ValueObject
{
  public const int ByteLength = 32;
  private readonly byte[] bytes;

  private PlaceholderFingerprint(byte[] bytes) => this.bytes = bytes;

  public byte[] Bytes => bytes.ToArray();

  public string Hex => Convert.ToHexString(bytes).ToLowerInvariant();

  public static PlaceholderFingerprint Calculate(PlaceholderSet placeholders)
  {
    ArgumentNullException.ThrowIfNull(placeholders);
    var canonical = string.Join('\n', placeholders.Names);
    return new PlaceholderFingerprint(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
  }

  public static Result<PlaceholderFingerprint> FromBytes(byte[]? value) => value is { Length: ByteLength }
    ? Result.Success(new PlaceholderFingerprint(value.ToArray()))
    : Result.Failure<PlaceholderFingerprint>(LocalizationErrors.InvalidPlaceholder);

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    foreach (var value in bytes)
    {
      yield return value;
    }
  }
}

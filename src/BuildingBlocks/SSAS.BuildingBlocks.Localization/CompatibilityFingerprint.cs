using System.Security.Cryptography;
using System.Text;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public sealed class CompatibilityFingerprint : ValueObject
{
  public const int ByteLength = 32;
  private readonly byte[] bytes;

  private CompatibilityFingerprint(byte[] bytes) => this.bytes = bytes;

  public byte[] Bytes => bytes.ToArray();

  public string Hex => Convert.ToHexString(bytes).ToLowerInvariant();

  public static CompatibilityFingerprint Calculate(
    ResourceKey resourceKey,
    LocalizationTextFormat textFormat,
    LocalizationSecurityClassification securityClassification,
    bool tenantOverridable,
    PlaceholderSet placeholders)
  {
    ArgumentNullException.ThrowIfNull(resourceKey);
    ArgumentNullException.ThrowIfNull(placeholders);
    var fields = new List<string>
    {
      resourceKey.Value,
      textFormat.ToString(),
      securityClassification.ToString(),
      tenantOverridable ? "true" : "false"
    };
    fields.AddRange(placeholders.Names);
    return new CompatibilityFingerprint(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', fields))));
  }

  public static Result<CompatibilityFingerprint> FromBytes(byte[]? value) => value is { Length: ByteLength }
    ? Result.Success(new CompatibilityFingerprint(value.ToArray()))
    : Result.Failure<CompatibilityFingerprint>(LocalizationErrors.InvalidPlaceholder);

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    foreach (var value in bytes)
    {
      yield return value;
    }
  }
}

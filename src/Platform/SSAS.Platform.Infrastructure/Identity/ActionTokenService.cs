using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class ActionTokenService : IActionTokenService
{
  private const string DomainSeparator = "SSAS.ERP.AccountActionToken.v1";
  private const int SecretLength = 32;
  private const int EncodedSecretLength = 43;
  private const int PresentedTokenLength = 32 + 1 + EncodedSecretLength;

  public GeneratedActionToken Generate(AccountActionTokenPurpose purpose)
  {
    var publicId = Guid.NewGuid();
    var secretBytes = RandomNumberGenerator.GetBytes(SecretLength);
    var secret = WebEncoders.Base64UrlEncode(secretBytes);
    var hash = ComputeHash(purpose, publicId, secret);
    return new GeneratedActionToken(
      publicId,
      hash,
      new SensitiveActionToken($"{publicId:N}.{secret}"));
  }

  public bool TryReadPublicId(string presentedToken, out Guid publicId)
  {
    publicId = Guid.Empty;
    return TryParse(presentedToken, out publicId, out _);
  }

  public bool Verify(AccountActionToken actionToken, string presentedToken)
  {
    ArgumentNullException.ThrowIfNull(actionToken);
    if (!TryParse(presentedToken, out var publicId, out var secret) || publicId != actionToken.PublicId)
    {
      return false;
    }

    var candidate = ComputeHash(actionToken.Purpose, publicId, secret);
    return CryptographicOperations.FixedTimeEquals(candidate, actionToken.SecretHash);
  }

  private static bool TryParse(string presentedToken, out Guid publicId, out string secret)
  {
    publicId = Guid.Empty;
    secret = string.Empty;
    if (string.IsNullOrWhiteSpace(presentedToken) || presentedToken.Length != PresentedTokenLength)
    {
      return false;
    }

    var separatorIndex = presentedToken.IndexOf('.', StringComparison.Ordinal);
    if (separatorIndex != 32 || presentedToken.LastIndexOf('.') != separatorIndex ||
      !Guid.TryParseExact(presentedToken.AsSpan(0, separatorIndex), "N", out publicId))
    {
      publicId = Guid.Empty;
      return false;
    }

    secret = presentedToken[(separatorIndex + 1)..];
    try
    {
      var decoded = WebEncoders.Base64UrlDecode(secret);
      return decoded.Length == SecretLength && string.Equals(WebEncoders.Base64UrlEncode(decoded), secret, StringComparison.Ordinal);
    }
    catch (FormatException)
    {
      publicId = Guid.Empty;
      secret = string.Empty;
      return false;
    }
  }

  private static byte[] ComputeHash(AccountActionTokenPurpose purpose, Guid publicId, string secret)
  {
    var canonicalValue = string.Concat(
      DomainSeparator,
      "\0",
      purpose.ToString(),
      "\0",
      publicId.ToString("N"),
      "\0",
      secret);
    return SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue));
  }
}

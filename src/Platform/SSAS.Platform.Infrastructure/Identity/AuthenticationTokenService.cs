using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AuthenticationTokenService : IAuthenticationTokenService
{
  private const string SelectionDomainSeparator = "SSAS.ERP.TenantSelectionTransaction.v1";
  private const string RefreshDomainSeparator = "SSAS.ERP.RefreshToken.v1";
  private const int SecretLength = 32;
  private const int EncodedSecretLength = 43;
  private const int PresentedTokenLength = 76;

  public GeneratedTenantSelectionProof GenerateTenantSelectionProof(
    long identityId,
    long securityVersion,
    AuthenticationClientId clientId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identityId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(securityVersion);
    ArgumentNullException.ThrowIfNull(clientId);
    var publicId = Guid.NewGuid();
    var secret = GenerateSecret();
    var hash = ComputeSelectionHash(publicId, identityId, securityVersion, clientId.Value, secret);
    return new GeneratedTenantSelectionProof(
      publicId,
      hash,
      new SensitiveTenantSelectionProof($"{publicId:N}.{secret}"));
  }

  public GeneratedRefreshToken GenerateRefreshToken(
    long authenticationSessionId,
    Guid tokenFamilyId,
    AuthenticationClientId clientId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authenticationSessionId);
    if (tokenFamilyId == Guid.Empty)
    {
      throw new ArgumentException("Token family ID cannot be empty.", nameof(tokenFamilyId));
    }

    ArgumentNullException.ThrowIfNull(clientId);
    var publicId = Guid.NewGuid();
    var secret = GenerateSecret();
    var hash = ComputeRefreshHash(publicId, authenticationSessionId, tokenFamilyId, clientId.Value, secret);
    return new GeneratedRefreshToken(publicId, hash, new SensitiveRefreshToken($"{publicId:N}.{secret}"));
  }

  public bool TryReadPublicId(SensitiveAuthenticationTokenInput token, out Guid publicId)
  {
    ArgumentNullException.ThrowIfNull(token);
    return TryParse(token.Value, out publicId, out _);
  }

  public bool VerifyTenantSelection(
    TenantSelectionTransaction transaction,
    SensitiveAuthenticationTokenInput proof)
  {
    ArgumentNullException.ThrowIfNull(transaction);
    ArgumentNullException.ThrowIfNull(proof);
    if (!TryParse(proof.Value, out var publicId, out var secret) || publicId != transaction.PublicId)
    {
      return false;
    }

    var candidate = ComputeSelectionHash(
      publicId,
      transaction.IdentityId,
      transaction.SecurityVersionAtAuthentication,
      transaction.ClientId,
      secret);
    return CryptographicOperations.FixedTimeEquals(candidate, transaction.SecretHash);
  }

  public bool VerifyRefreshToken(
    AuthenticationSession session,
    RefreshTokenRecord record,
    SensitiveAuthenticationTokenInput token)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(record);
    ArgumentNullException.ThrowIfNull(token);
    if (!TryParse(token.Value, out var publicId, out var secret) || publicId != record.PublicId ||
      record.AuthenticationSessionId != session.Id || record.TokenFamilyId != session.TokenFamilyId ||
      !string.Equals(record.ClientId, session.ClientId, StringComparison.Ordinal))
    {
      return false;
    }

    var candidate = ComputeRefreshHash(publicId, session.Id, session.TokenFamilyId, session.ClientId, secret);
    return CryptographicOperations.FixedTimeEquals(candidate, record.SecretHash);
  }

  public bool VerifyRefreshToken(
    PlatformAuthenticationSession session,
    PlatformRefreshTokenRecord record,
    SensitiveAuthenticationTokenInput token)
  {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(record);
    ArgumentNullException.ThrowIfNull(token);
    if (!TryParse(token.Value, out var publicId, out var secret) || publicId != record.PublicId ||
      record.PlatformAuthenticationSessionId != session.Id || record.TokenFamilyId != session.TokenFamilyId ||
      !string.Equals(record.ClientId, session.ClientId, StringComparison.Ordinal))
    {
      return false;
    }

    var candidate = ComputeRefreshHash(publicId, session.Id, session.TokenFamilyId, session.ClientId, secret);
    return CryptographicOperations.FixedTimeEquals(candidate, record.SecretHash);
  }

  private static string GenerateSecret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretLength));

  private static bool TryParse(string value, out Guid publicId, out string secret)
  {
    publicId = Guid.Empty;
    secret = string.Empty;
    if (string.IsNullOrWhiteSpace(value) || value.Length != PresentedTokenLength)
    {
      return false;
    }

    var separatorIndex = value.IndexOf('.', StringComparison.Ordinal);
    if (separatorIndex != 32 || value.LastIndexOf('.') != separatorIndex ||
      !Guid.TryParseExact(value.AsSpan(0, separatorIndex), "N", out publicId))
    {
      publicId = Guid.Empty;
      return false;
    }

    secret = value[(separatorIndex + 1)..];
    if (secret.Length != EncodedSecretLength)
    {
      publicId = Guid.Empty;
      secret = string.Empty;
      return false;
    }

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

  private static byte[] ComputeSelectionHash(Guid publicId, long identityId, long securityVersion, string clientId, string secret)
  {
    var canonical = string.Concat(
      SelectionDomainSeparator,
      "\0",
      publicId.ToString("N"),
      "\0",
      identityId.ToString(CultureInfo.InvariantCulture),
      "\0",
      securityVersion.ToString(CultureInfo.InvariantCulture),
      "\0",
      clientId,
      "\0",
      secret);
    return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
  }

  private static byte[] ComputeRefreshHash(Guid publicId, long sessionId, Guid familyId, string clientId, string secret)
  {
    var canonical = string.Concat(
      RefreshDomainSeparator,
      "\0",
      publicId.ToString("N"),
      "\0",
      sessionId.ToString(CultureInfo.InvariantCulture),
      "\0",
      familyId.ToString("N"),
      "\0",
      clientId,
      "\0",
      secret);
    return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
  }
}

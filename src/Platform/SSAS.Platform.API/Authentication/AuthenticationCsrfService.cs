using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.API.Authentication;

public sealed record ValidatedCsrfPayload(long AuthenticationSessionId, Guid RefreshTokenPublicId);

public sealed class AuthenticationCsrfService
{
  public const string CookieName = "__Secure-ssas-xsrf";
  public const string HeaderName = "X-XSRF-TOKEN";
  private const int FormatVersion = 1;
  private readonly ITimeLimitedDataProtector protector;

  public AuthenticationCsrfService(IDataProtectionProvider provider)
  {
    protector = provider.CreateProtector("SSAS.ERP.Authentication.Csrf.v1").ToTimeLimitedDataProtector();
  }

  public string Create(string refreshToken, long sessionId, DateTimeOffset expiresUtc)
  {
    if (!TryReadPublicId(refreshToken, out var publicId)) throw new InvalidOperationException("Refresh token selector is invalid.");
    var payload = new Payload(FormatVersion, publicId, sessionId, AuthenticationClientId.V1Web,
      Guid.NewGuid(), expiresUtc.ToUnixTimeSeconds());
    return protector.Protect(JsonSerializer.Serialize(payload), expiresUtc);
  }

  public bool TryValidate(HttpContext context, string refreshToken, out ValidatedCsrfPayload payload)
  {
    payload = default!;
    if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) ||
      context.Request.Headers[HeaderName].Count != 1 ||
      !string.Equals(cookie, context.Request.Headers[HeaderName][0], StringComparison.Ordinal) ||
      !TryReadPublicId(refreshToken, out var refreshPublicId)) return false;

    try
    {
      var json = protector.Unprotect(cookie, out var expiresUtc);
      var parsed = JsonSerializer.Deserialize<Payload>(json);
      if (parsed is null || parsed.Version != FormatVersion || parsed.RefreshTokenPublicId != refreshPublicId ||
        parsed.AuthenticationSessionId <= 0 || parsed.Nonce == Guid.Empty || expiresUtc <= DateTimeOffset.UtcNow ||
        parsed.ExpiresUnixTimeSeconds != expiresUtc.ToUnixTimeSeconds() ||
        !string.Equals(parsed.ClientId, AuthenticationClientId.V1Web, StringComparison.Ordinal))
        return false;
      payload = new ValidatedCsrfPayload(parsed.AuthenticationSessionId, parsed.RefreshTokenPublicId);
      return true;
    }
    catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
    {
      return false;
    }
  }

  private static bool TryReadPublicId(string token, out Guid publicId)
  {
    publicId = Guid.Empty;
    return token is { Length: 76 } && token[32] == '.' &&
      Guid.TryParseExact(token.AsSpan(0, 32), "N", out publicId);
  }

  private sealed record Payload(
    int Version,
    Guid RefreshTokenPublicId,
    long AuthenticationSessionId,
    string ClientId,
    Guid Nonce,
    long ExpiresUnixTimeSeconds);
}

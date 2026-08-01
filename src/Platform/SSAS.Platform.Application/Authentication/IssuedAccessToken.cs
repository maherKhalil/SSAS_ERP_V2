namespace SSAS.Platform.Application.Authentication;

public sealed record IssuedAccessToken(SensitiveAccessToken AccessToken, DateTimeOffset ExpiresUtc);

using Microsoft.AspNetCore.Http;

namespace SSAS.Platform.API.Authentication;

public enum AuthenticationEndpointKind { Login, TenantSelection, Refresh, Logout }

public sealed record AuthenticationRateLimitResult(bool Allowed, TimeSpan RetryAfter)
{
  public static readonly AuthenticationRateLimitResult Permit = new(true, TimeSpan.Zero);
}

public interface IAuthenticationRequestSecurity
{
  bool IsAccepted(HttpContext context, bool requireJson);
}

public interface IAuthenticationEndpointRateLimiter
{
  ValueTask<AuthenticationRateLimitResult> AcquireAsync(
    AuthenticationEndpointKind endpoint,
    HttpContext context,
    string partitionMaterial,
    CancellationToken cancellationToken = default);
}

public sealed record AuthenticationLoginRequest(string? LoginEmail, string? Password);

public sealed record AuthenticationTenantSelectionRequest(string? SelectionProof, Guid TenantId, long TenantUserId);

public sealed record AuthenticatedResponse(
  string Outcome,
  string TokenType,
  string AccessToken,
  DateTimeOffset AccessTokenExpiresUtc,
  Guid TenantId,
  long TenantUserId,
  long AuthenticationSessionId);

public sealed record TenantSelectionRequiredResponse(
  string Outcome,
  string SelectionProof,
  DateTimeOffset SelectionExpiresUtc,
  IReadOnlyList<TenantMembershipResponse> Memberships);

public sealed record TenantMembershipResponse(Guid TenantId, long TenantUserId, string TenantDisplayName);

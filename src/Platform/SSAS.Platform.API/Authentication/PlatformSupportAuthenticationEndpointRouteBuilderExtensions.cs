using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain;

namespace SSAS.Platform.API.Authentication;

// Platform-support authentication HTTP surface (Phase 4B / DEC-TEN-0023). Structurally separate from the tenant
// auth surface: a distinct route prefix, a distinct HttpOnly refresh cookie, and platform-store-only handlers.
// The route only expresses intended platform-plane authentication — authority is derived entirely server-side
// from the trusted VerifiedIdentity + persisted platform-support authority. No caller field selects the plane,
// the principal, the security_plane, or the permission set. External failures are deliberately generic.
public static class PlatformSupportAuthenticationEndpointRouteBuilderExtensions
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  public const string RefreshCookieName = "__Secure-ssas-platform-refresh";
  public const string RoutePrefix = "/api/platform/support/auth";

  // The four ineligibility outcomes that map to the SAME generic 401 so the transport never discloses whether the
  // account, the principal record, the principal status, or the live authority was the cause of denial.
  private static readonly HashSet<string> LoginDenialCodes = new(StringComparer.Ordinal)
  {
    PlatformSupportErrors.AccountIneligible.Code,
    PlatformSupportErrors.PrincipalNotFound.Code,
    PlatformSupportErrors.PrincipalDisabled.Code,
    PlatformSupportErrors.NoUsablePlatformAuthority.Code
  };

  public static IEndpointRouteBuilder MapPlatformSupportAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Support Authentication");

    group.MapPost("/login", LoginAsync)
      .AllowAnonymous()
      .WithName("PlatformSupportAuthenticationLogin")
      .Produces<PlatformAuthenticatedResponse>()
      .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);

    group.MapPost("/refresh", RefreshAsync)
      .AllowAnonymous()
      .WithName("PlatformSupportAuthenticationRefresh")
      .Produces<PlatformAuthenticatedResponse>()
      .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);

    group.MapPost("/logout", LogoutAsync)
      .RequireAuthorization()
      .WithName("PlatformSupportAuthenticationLogout")
      .Produces(204)
      .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);

    return endpoints;
  }

  // POST /login — credentials only. Verify password → trusted VerifiedIdentity → server-side platform session
  // creation (live eligibility). There is no tenant-selection branch on the platform plane.
  private static async Task<IResult> LoginAsync(
    HttpContext context,
    IAuthenticationRequestSecurity security,
    IAuthenticationEndpointRateLimiter rateLimiter,
    CancellationToken cancellationToken)
  {
    ApplyResponseSecurity(context);
    if (!security.IsAccepted(context, false)) return Problem(context, 403, "authentication.request_rejected");
    if (!context.Request.HasJsonContentType()) return Problem(context, 400, "request.invalid");
    var request = await ReadLoginAsync(context, cancellationToken);
    if (request is null || string.IsNullOrWhiteSpace(request.LoginEmail) || request.Password is null)
      return Problem(context, 400, "request.invalid");
    var limited = await rateLimiter.AcquireAsync(AuthenticationEndpointKind.Login, context, request.LoginEmail, cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);

    var credentials = context.RequestServices.GetRequiredService<VerifyPasswordCredentialsCommandHandler>();
    var verified = await credentials.HandleAsync(new VerifyPasswordCredentialsCommand(request.LoginEmail, request.Password), cancellationToken);
    if (verified.IsFailure) return verified.Error == AuthenticationErrors.GenericCredentialFailure
      ? Problem(context, 401, "authentication.failed")
      : Problem(context, 503, "service.unavailable");

    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var clock = context.RequestServices.GetRequiredService<IDateTimeProvider>();
    var creator = context.RequestServices.GetRequiredService<PlatformAuthenticationSessionCreator>();
    var created = await creator.CreateAsync(verified.Value.VerifiedIdentity, clientId, clock.UtcNow, cancellationToken);
    if (created.IsFailure) return LoginDenialCodes.Contains(created.Error.Code)
      ? Problem(context, 401, "authentication.failed")   // generic: never discloses which authority condition failed
      : Problem(context, 503, "service.unavailable");
    return Authenticate(context, created.Value, context.RequestServices.GetRequiredService<AuthenticationCsrfService>());
  }

  // POST /refresh — opaque refresh cookie + double-submit CSRF. Resolves the token in the PLATFORM store only
  // (a tenant refresh token is not in this store and is scoped to a different cookie path), re-evaluates live
  // platform authority, and rotates the cookie pair.
  private static async Task<IResult> RefreshAsync(
    HttpContext context,
    IAuthenticationRequestSecurity security,
    IAuthenticationEndpointRateLimiter rateLimiter,
    CancellationToken cancellationToken)
  {
    ApplyResponseSecurity(context);
    if (!security.IsAccepted(context, false)) return Problem(context, 403, "authentication.request_rejected");
    if (context.Request.ContentLength is > 0) return Problem(context, 400, "request.invalid");
    var csrf = context.RequestServices.GetRequiredService<AuthenticationCsrfService>();
    if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
      !csrf.TryValidate(context, refreshToken, out var csrfPayload))
      return Problem(context, 403, "authentication.request_rejected");
    var limited = await rateLimiter.AcquireAsync(AuthenticationEndpointKind.Refresh, context,
      csrfPayload.AuthenticationSessionId.ToString(CultureInfo.InvariantCulture), cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);

    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var handler = context.RequestServices.GetRequiredService<RefreshPlatformAuthenticationSessionCommandHandler>();
    var result = await handler.HandleAsync(new RefreshPlatformAuthenticationSessionCommand(
      new SensitiveAuthenticationTokenInput(refreshToken), clientId), cancellationToken);
    if (result.IsFailure && result.Error != AuthenticationErrors.GenericRefreshFailure)
      return Problem(context, 503, "service.unavailable");
    if (result.IsFailure)
    {
      ClearCookies(context);
      return Problem(context, 401, "authentication.refresh_failed");
    }
    return Authenticate(context, result.Value, csrf);
  }

  // POST /logout — Bearer-authenticated, PLATFORM-plane only. The revoke target is the trusted session_id claim
  // (never a request field); a tenant or plane-less token is refused before any store access. Platform-store
  // only; the already-issued short-lived access JWT stays valid until natural expiry.
  private static async Task<IResult> LogoutAsync(
    HttpContext context,
    IAuthenticationRequestSecurity security,
    IAuthenticationEndpointRateLimiter rateLimiter,
    CancellationToken cancellationToken)
  {
    ApplyResponseSecurity(context);
    if (!security.IsAccepted(context, false)) return Problem(context, 403, "authentication.request_rejected");
    if (context.Request.ContentLength is > 0) return Problem(context, 400, "request.invalid");

    // Narrow plane guard for this single new platform-auth route: a tenant (or plane-less) token can never revoke
    // a platform session — its session_id could otherwise numerically collide with a platform session of the same
    // identity. The durable RequirePlatformAuthenticatedUser policy + endpoint taxonomy is DEC-TEN-0024 / 4E.
    if (!IsPlatformPlane(context.User) ||
      !TryReadPositiveLong(context.User, JwtClaimTypes.SessionId, out var sessionId) ||
      !TryReadPositiveLong(context.User, JwtClaimTypes.IdentityId, out var identityId))
      return Problem(context, 403, "authentication.request_rejected");

    var csrf = context.RequestServices.GetRequiredService<AuthenticationCsrfService>();
    if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
      !csrf.TryValidate(context, refreshToken, out var csrfPayload) ||
      csrfPayload.AuthenticationSessionId != sessionId)
      return Problem(context, 403, "authentication.request_rejected");
    var limited = await rateLimiter.AcquireAsync(AuthenticationEndpointKind.Logout, context,
      sessionId.ToString(CultureInfo.InvariantCulture), cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);

    var handler = context.RequestServices.GetRequiredService<RevokeCurrentPlatformAuthenticationSessionCommandHandler>();
    var result = await handler.HandleAsync(new RevokeCurrentPlatformAuthenticationSessionCommand(sessionId, identityId), cancellationToken);
    ClearCookies(context);
    return result.IsFailure ? Problem(context, 503, "service.unavailable") : Results.NoContent();
  }

  private static bool IsPlatformPlane(ClaimsPrincipal user)
  {
    if (user.Identity?.IsAuthenticated != true) return false;
    var planes = user.FindAll(JwtClaimTypes.SecurityPlane).ToArray();
    return planes.Length == 1 && string.Equals(planes[0].Value, SecurityPlane.Platform, StringComparison.Ordinal);
  }

  private static bool TryReadPositiveLong(ClaimsPrincipal user, string claimType, out long value) =>
    long.TryParse(user.FindFirstValue(claimType), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;

  private static IResult Authenticate(HttpContext context, PlatformSessionCreated session, AuthenticationCsrfService csrf) =>
    Authenticate(context, session.PlatformAuthenticationSessionId, session.PlatformSupportPrincipalId,
      session.RefreshToken, session.RefreshTokenExpiresUtc, session.AccessToken, csrf);

  private static IResult Authenticate(HttpContext context, PlatformRefreshSucceeded session, AuthenticationCsrfService csrf) =>
    Authenticate(context, session.PlatformAuthenticationSessionId, session.PlatformSupportPrincipalId,
      session.RefreshToken, session.RefreshTokenExpiresUtc, session.AccessToken, csrf);

  private static IResult Authenticate(
    HttpContext context, long sessionId, long platformSupportPrincipalId,
    SensitiveRefreshToken refresh, DateTimeOffset refreshExpiresUtc, IssuedAccessToken access,
    AuthenticationCsrfService csrf)
  {
    var refreshValue = refresh.RevealOnce();
    var accessValue = access.AccessToken.RevealOnce();
    if (refreshValue.IsFailure || accessValue.IsFailure) return Problem(context, 503, "service.unavailable");
    WriteCookies(context, refreshValue.Value, sessionId, refreshExpiresUtc, csrf);
    return Results.Ok(new PlatformAuthenticatedResponse("Authenticated", "Bearer", accessValue.Value,
      access.ExpiresUtc, platformSupportPrincipalId, sessionId));
  }

  private static async Task<AuthenticationLoginRequest?> ReadLoginAsync(HttpContext context, CancellationToken cancellationToken)
  {
    try
    {
      using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
      if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
      var allowed = new HashSet<string>(["loginEmail", "password"], StringComparer.Ordinal);
      var seen = new HashSet<string>(StringComparer.Ordinal);
      if (document.RootElement.EnumerateObject().Any(property => !allowed.Contains(property.Name) || !seen.Add(property.Name)))
        return null;
      return document.RootElement.Deserialize<AuthenticationLoginRequest>(JsonOptions);
    }
    catch (JsonException) { return null; }
    catch (BadHttpRequestException) { return null; }
  }

  private static void WriteCookies(HttpContext context, string refreshToken, long sessionId,
    DateTimeOffset expiresUtc, AuthenticationCsrfService csrf)
  {
    context.Response.Cookies.Append(RefreshCookieName, refreshToken, CookieOptions(expiresUtc, true));
    context.Response.Cookies.Append(AuthenticationCsrfService.CookieName,
      csrf.Create(refreshToken, sessionId, expiresUtc), CookieOptions(expiresUtc, false));
  }

  private static void ClearCookies(HttpContext context)
  {
    context.Response.Cookies.Append(RefreshCookieName, string.Empty, CookieOptions(DateTimeOffset.UnixEpoch, true, true));
    context.Response.Cookies.Append(AuthenticationCsrfService.CookieName, string.Empty, CookieOptions(DateTimeOffset.UnixEpoch, false, true));
  }

  private static CookieOptions CookieOptions(DateTimeOffset expiresUtc, bool httpOnly, bool delete = false) => new()
  {
    Secure = true,
    HttpOnly = httpOnly,
    SameSite = SameSiteMode.Strict,
    Path = RoutePrefix,
    IsEssential = true,
    Expires = expiresUtc,
    MaxAge = delete ? TimeSpan.Zero : expiresUtc - DateTimeOffset.UtcNow
  };

  private static void ApplyResponseSecurity(HttpContext context)
  {
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
  }

  private static IResult RateLimited(HttpContext context, AuthenticationRateLimitResult result)
  {
    context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    return Problem(context, 429, "rate_limit.exceeded");
  }

  private static IResult Problem(HttpContext context, int status, string code) => Results.Problem(
    statusCode: status,
    title: code,
    extensions: new Dictionary<string, object?>
    {
      ["code"] = code,
      ["correlationId"] = context.Response.Headers["X-Correlation-ID"].ToString()
    });
}

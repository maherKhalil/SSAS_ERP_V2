using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.API.Authentication;

public static class AuthenticationEndpointRouteBuilderExtensions
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  public const string RefreshCookieName = "__Secure-ssas-refresh";
  private const string RoutePrefix = "/api/platform/auth";

  public static IEndpointRouteBuilder MapPlatformAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Authentication");
    group.MapPost("/login", LoginAsync).AllowAnonymous().WithName("PlatformAuthenticationLogin")
      .WithDescription("Verifies password credentials and either establishes a tenant session or returns eligible tenant choices.")
      .Produces<AuthenticatedResponse>().Produces<TenantSelectionRequiredResponse>()
      .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);
    group.MapPost("/select-tenant", SelectTenantAsync).AllowAnonymous().WithName("PlatformAuthenticationSelectTenant")
      .WithDescription("Consumes a reveal-once tenant-selection proof and establishes the selected eligible tenant session.")
      .Produces<AuthenticatedResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);
    group.MapPost("/refresh", RefreshAsync).AllowAnonymous().WithName("PlatformAuthenticationRefresh")
      .WithDescription("Rotates the secure refresh cookie. Requires the X-XSRF-TOKEN header matching the CSRF cookie.")
      .Produces<AuthenticatedResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);
    group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("PlatformAuthenticationLogout")
      .WithDescription("Revokes the current Bearer-authenticated session. Requires the X-XSRF-TOKEN header matching the CSRF cookie.")
      .Produces(204).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503);
    return endpoints;
  }

  private static async Task<IResult> LoginAsync(
    HttpContext context,
    IAuthenticationRequestSecurity security,
    IAuthenticationEndpointRateLimiter rateLimiter,
    CancellationToken cancellationToken)
  {
    ApplyResponseSecurity(context);
    if (!security.IsAccepted(context, false)) return Problem(context, 403, "authentication.request_rejected");
    if (!context.Request.HasJsonContentType()) return Problem(context, 400, "request.invalid");
    var request = await ReadJsonAsync<AuthenticationLoginRequest>(context, cancellationToken);
    if (request is null || string.IsNullOrWhiteSpace(request.LoginEmail) || request.Password is null)
      return Problem(context, 400, "request.invalid");
    var limited = await rateLimiter.AcquireAsync(AuthenticationEndpointKind.Login, context, request.LoginEmail, cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);

    var credentials = context.RequestServices.GetRequiredService<VerifyPasswordCredentialsCommandHandler>();
    var verified = await credentials.HandleAsync(new VerifyPasswordCredentialsCommand(request.LoginEmail, request.Password), cancellationToken);
    if (verified.IsFailure) return verified.Error == AuthenticationErrors.GenericCredentialFailure
      ? Problem(context, 401, "authentication.failed")
      : Unexpected(context, verified.Error);
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var tenantAccess = context.RequestServices.GetRequiredService<BeginTenantAccessCommandHandler>();
    var begun = await tenantAccess.HandleAsync(new BeginTenantAccessCommand(verified.Value.VerifiedIdentity, clientId), cancellationToken);
    if (begun.IsFailure) return begun.Error == AuthenticationErrors.GenericCredentialFailure
      ? Problem(context, 401, "authentication.failed")
      : Unexpected(context, begun.Error);
    return begun.Value switch
    {
      TenantSelectedAutomatically automatic => Authenticate(context, automatic.Session,
        context.RequestServices.GetRequiredService<AuthenticationCsrfService>()),
      TenantSelectionRequired selection => Selection(context, selection),
      _ => Problem(context, 401, "authentication.failed")
    };
  }

  private static async Task<IResult> SelectTenantAsync(
    HttpContext context,
    IAuthenticationRequestSecurity security,
    IAuthenticationEndpointRateLimiter rateLimiter,
    CancellationToken cancellationToken)
  {
    ApplyResponseSecurity(context);
    if (!security.IsAccepted(context, false)) return Problem(context, 403, "authentication.request_rejected");
    if (!context.Request.HasJsonContentType()) return Problem(context, 400, "request.invalid");
    var request = await ReadJsonAsync<AuthenticationTenantSelectionRequest>(context, cancellationToken);
    if (request is null || !TryReadPresentedPublicId(request.SelectionProof, out var selectionPublicId) ||
      request.TenantId == Guid.Empty || request.TenantUserId <= 0)
      return Problem(context, 400, "request.invalid");
    var limited = await rateLimiter.AcquireAsync(AuthenticationEndpointKind.TenantSelection, context,
      selectionPublicId.ToString("N"), cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var handler = context.RequestServices.GetRequiredService<SelectTenantCommandHandler>();
    var result = await handler.HandleAsync(new SelectTenantCommand(
      new SensitiveAuthenticationTokenInput(request.SelectionProof), clientId, request.TenantUserId, request.TenantId), cancellationToken);
    if (result.IsFailure) return result.Error == AuthenticationErrors.GenericTenantSelectionFailure
      ? Problem(context, 401, "authentication.selection_failed")
      : Unexpected(context, result.Error);
    return Authenticate(context, result.Value, context.RequestServices.GetRequiredService<AuthenticationCsrfService>());
  }

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
      csrfPayload.AuthenticationSessionId.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);

    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var handler = context.RequestServices.GetRequiredService<RefreshAuthenticationSessionCommandHandler>();
    var result = await handler.HandleAsync(new RefreshAuthenticationSessionCommand(
      new SensitiveAuthenticationTokenInput(refreshToken), clientId), cancellationToken);
    if (result.IsFailure && result.Error != AuthenticationErrors.GenericRefreshFailure)
      return Unexpected(context, result.Error);
    if (result.IsFailure)
    {
      ClearCookies(context);
      return Problem(context, 401, "authentication.refresh_failed");
    }
    return Authenticate(context, result.Value, csrf);
  }

  private static async Task<IResult> LogoutAsync(
    HttpContext context,
    IAuthenticationRequestSecurity security,
    IAuthenticationEndpointRateLimiter rateLimiter,
    CancellationToken cancellationToken)
  {
    ApplyResponseSecurity(context);
    if (!security.IsAccepted(context, false)) return Problem(context, 403, "authentication.request_rejected");
    if (context.Request.ContentLength is > 0) return Problem(context, 400, "request.invalid");
    var csrf = context.RequestServices.GetRequiredService<AuthenticationCsrfService>();
    var currentSession = context.RequestServices.GetRequiredService<ICurrentAuthenticationSession>();
    if (!context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
      !csrf.TryValidate(context, refreshToken, out var csrfPayload) ||
      currentSession.Value?.AuthenticationSessionId != csrfPayload.AuthenticationSessionId)
      return Problem(context, 403, "authentication.request_rejected");
    var limited = await rateLimiter.AcquireAsync(AuthenticationEndpointKind.Logout, context,
      csrfPayload.AuthenticationSessionId.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
    if (!limited.Allowed) return RateLimited(context, limited);
    var handler = context.RequestServices.GetRequiredService<RevokeCurrentAuthenticationSessionCommandHandler>();
    var result = await handler.HandleAsync(new RevokeCurrentAuthenticationSessionCommand(), cancellationToken);
    ClearCookies(context);
    return result.IsFailure && result.Error != AuthenticationErrors.InvalidAuthenticationSession
      ? Unexpected(context, result.Error)
      : Results.NoContent();
  }

  private static IResult Authenticate(HttpContext context, SessionCreated session, AuthenticationCsrfService csrf) =>
    Authenticate(context, session.AuthenticationSessionId, session.TenantId, session.TenantUserId,
      session.RefreshToken, session.RefreshTokenExpiresUtc, session.AccessToken, csrf);

  private static IResult Authenticate(HttpContext context, RefreshSucceeded session, AuthenticationCsrfService csrf) =>
    Authenticate(context, session.AuthenticationSessionId, session.TenantId, session.TenantUserId,
      session.RefreshToken, session.RefreshTokenExpiresUtc, session.AccessToken, csrf);

  private static IResult Authenticate(
    HttpContext context, long sessionId, Guid tenantId, long tenantUserId,
    SensitiveRefreshToken refresh, DateTimeOffset refreshExpiresUtc, IssuedAccessToken access,
    AuthenticationCsrfService csrf)
  {
    var refreshValue = refresh.RevealOnce();
    var accessValue = access.AccessToken.RevealOnce();
    if (refreshValue.IsFailure || accessValue.IsFailure) return Problem(context, 503, "service.unavailable");
    WriteCookies(context, refreshValue.Value, sessionId, refreshExpiresUtc, csrf);
    return Results.Ok(new AuthenticatedResponse("Authenticated", "Bearer", accessValue.Value,
      access.ExpiresUtc, tenantId, tenantUserId, sessionId));
  }

  private static IResult Selection(HttpContext context, TenantSelectionRequired selection)
  {
    var proof = selection.SelectionProof.RevealOnce();
    return proof.IsFailure
      ? Problem(context, 503, "service.unavailable")
      : Results.Ok(new TenantSelectionRequiredResponse("TenantSelectionRequired", proof.Value, selection.SelectionExpiresUtc,
        selection.Memberships.Select(item => new TenantMembershipResponse(
          item.TenantId, item.TenantUserId, item.TenantDisplayName)).ToArray()));
  }

  private static async Task<T?> ReadJsonAsync<T>(HttpContext context, CancellationToken cancellationToken) where T : class
  {
    try
    {
      using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
      if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
      var allowed = typeof(T) == typeof(AuthenticationLoginRequest)
        ? new HashSet<string>(["loginEmail", "password"], StringComparer.Ordinal)
        : new HashSet<string>(["selectionProof", "tenantId", "tenantUserId"], StringComparer.Ordinal);
      var seen = new HashSet<string>(StringComparer.Ordinal);
      if (document.RootElement.EnumerateObject().Any(property => !allowed.Contains(property.Name) || !seen.Add(property.Name)))
        return null;
      return document.RootElement.Deserialize<T>(JsonOptions);
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
    context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    return Problem(context, 429, "rate_limit.exceeded");
  }

  // ================================================================================================
  // THE "ANYTHING ELSE" BUCKET, AND ONE THING THAT MUST NOT BE IN IT (T-096).
  // ================================================================================================
  //
  // These routes carry NO mapper by design. Each branches on the EXPECTED error — a bad credential, a
  // failed selection, an invalid session — answers 401 or 204, and sends everything else to
  // `503 service.unavailable`. **The collapse is deliberate: distinguishing "no such account" from "wrong
  // password" is the disclosure it exists to prevent.**
  //
  // ---- BUT A CONCURRENT WRITE IS NOT AN OUTAGE.
  //
  // `PlatformUnitOfWork` returns `Persistence.ConcurrencyConflict` when a save loses a row-version race,
  // and it reached this bucket. **503 tells the caller the service is down when the fix is to retry** —
  // it points an operator at an availability incident that is not happening, and tells a client to back
  // off rather than repeat the request.
  //
  // 409 says exactly what happened and is the answer every other surface in the product already gives it.
  // **Nothing else moves:** `Authentication.AccessTokenUnavailable` genuinely IS an availability failure
  // and stays 503.
  private static IResult Unexpected(HttpContext context, Error error) =>
    error.Code == "Persistence.ConcurrencyConflict"
      ? Problem(context, 409, "concurrency.conflict")
      : Problem(context, 503, "service.unavailable");

  private static IResult Problem(HttpContext context, int status, string code) => Results.Problem(
    statusCode: status,
    title: code,
    extensions: new Dictionary<string, object?>
    {
      ["code"] = code,
      ["correlationId"] = context.Response.Headers["X-Correlation-ID"].ToString()
    });

  private static bool TryReadPresentedPublicId(string? value, out Guid publicId)
  {
    publicId = Guid.Empty;
    return value is { Length: 76 } && value[32] == '.' &&
      Guid.TryParseExact(value.AsSpan(0, 32), "N", out publicId);
  }
}

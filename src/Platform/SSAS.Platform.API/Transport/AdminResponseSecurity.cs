using Microsoft.AspNetCore.Http;

namespace SSAS.Platform.API.Transport;

// Single source of truth for the platform admin-response security headers. Applied per
// response by admin endpoint handlers (no global middleware is introduced by this milestone).
public static class AdminResponseSecurity
{
  public static void Apply(HttpContext context)
  {
    ArgumentNullException.ThrowIfNull(context);
    context.Response.Headers.CacheControl = "no-store, no-cache";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
  }
}

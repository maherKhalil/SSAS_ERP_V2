using Microsoft.AspNetCore.Http;

namespace SSAS.Platform.API.Localization;

internal static class LocalizationResponseSecurity
{
  public static void Apply(HttpContext context)
  {
    context.Response.Headers.CacheControl = "no-store, no-cache";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
  }
}

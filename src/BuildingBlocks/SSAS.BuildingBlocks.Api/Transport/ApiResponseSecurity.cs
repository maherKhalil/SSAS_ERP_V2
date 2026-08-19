using Microsoft.AspNetCore.Http;

namespace SSAS.BuildingBlocks.Api.Transport;

// THE RESPONSE SECURITY HEADERS, IN ONE PLACE FOR THE WHOLE ESTATE (FP-006C5).
//
// It was ApiResponseSecurity in Platform.API and already described itself as the single source of truth
// for these headers. The FP-006 contract requires the Employee surface to set the same ones, and a module
// cannot reach Platform.API — so the choice was to move it or to have TWO single sources of truth for the
// same security headers, drifting apart at whatever pace the two modules change.
//
// It qualifies on the same test as the other primitives here: it names no module, no route, no permission
// and no business concept. It sets four headers.
public static class ApiResponseSecurity
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

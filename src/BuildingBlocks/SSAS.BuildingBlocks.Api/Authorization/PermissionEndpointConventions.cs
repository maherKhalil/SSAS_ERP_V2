using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.BuildingBlocks.Api.Transport;

// THE GENERIC "THIS ENDPOINT REQUIRES PERMISSION X" MECHANISM (FP-006C5).
//
// It expresses a requirement and nothing more. It names no permission, defines no policy and knows no
// module: the CALLER supplies the permission name — Platform passes Platform's, HR passes HR's — and the
// Host's policy provider materialises the requirement. Adding a permission constant here would make one
// module's vocabulary a dependency of every other module's.
public static class PermissionEndpointConventions
{
  // Tenant-plane. The existing handler enforces trusted tenant plus live eligibility; this adds no
  // authorization architecture of its own.
  public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    return builder.RequireAuthorization($"{PermissionPolicyNames.TenantPrefix}{permissionName}");
  }

  // Platform-plane (ADR-015 §8). Deliberately separate from the tenant helper — the two must not mix, and
  // keeping them as two methods is what stops a caller choosing the wrong plane by passing a flag.
  public static TBuilder RequirePlatformPermission<TBuilder>(this TBuilder builder, string permissionName)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);

    return builder.RequireAuthorization($"{PermissionPolicyNames.PlatformPrefix}{permissionName}");
  }
}

// A PER-ENDPOINT REQUEST BODY CEILING (FP-009, R5).
//
// ================================================================================================
// A SIBLING OF `RequirePermission`, AND IT QUALIFIES ON THE SAME TEST.
// ================================================================================================
//
// It names no module, no route, no permission and no business concept — it takes a number of bytes and sets
// a transport feature. That is the membership rule the shared-API project is held to, and this passes it for
// the same reason `RowVersionCodec` and `StrictCsvReader` do.
//
// ---- APPLIED AT THE ROUTE, NOT AT THE GROUP, AND THAT IS THE ONE DELIBERATE EXCEPTION.
//
// HR's route groups apply their filters at the GROUP so "a route added later cannot forget it". That logic
// protects DEFAULTS — the company context, the response security headers — things every route in the group
// must have. This is the opposite shape: a TIGHTENING that belongs to two routes and would be wrong on the
// other forty-four. A group-wide 10 MB ceiling would silently raise the limit for every JSON route in HR.
//
// ---- WHAT IT IS AND IS NOT FOR.
//
// It lowers a ceiling for AUTHORIZED callers so an accidental 200 MB upload is refused early instead of
// being read and then rejected by the application. It is NOT denial-of-service protection: an endpoint
// filter runs AFTER authorization, so an unauthenticated caller is already rejected on headers without the
// body being read, and Kestrel's own global default remains the outer backstop for everything else. Those
// two facts are why the ordering below is sufficient rather than merely convenient.
public static class RequestSizeEndpointConventions
{
  // ---- THE FILTER SETS THE FEATURE BEFORE THE HANDLER READS THE BODY.
  //
  // `MaxRequestBodySize` is writable only while `IsReadOnly` is false — that is, until the body starts being
  // read. An endpoint filter runs before the handler, so the window is open. If a future change ever reads
  // the body earlier the feature turns read-only and the assignment throws, which is the failure mode worth
  // having: loud, rather than a ceiling that silently did nothing.
  //
  // The feature is ABSENT under some servers (TestServer does not implement Kestrel's body limits), so a
  // null feature is tolerated rather than treated as an error — the ceiling is a transport floor, and a host
  // that has no such concept is not misconfigured.
  public static RouteHandlerBuilder WithMaxBodySize(this RouteHandlerBuilder builder, long bytes)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);

    // Metadata as well as behaviour: it makes the ceiling readable from the endpoint without issuing a
    // request, which is what lets a test assert the route carries it under a server that cannot enforce it.
    builder.WithMetadata(new MaxRequestBodySizeMetadata(bytes));

    return builder.AddEndpointFilter(async (context, next) =>
    {
      var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

      if (feature is { IsReadOnly: false })
      {
        feature.MaxRequestBodySize = bytes;
      }

      return await next(context);
    });
  }

  // The declared ceiling, as endpoint metadata.
  public sealed record MaxRequestBodySizeMetadata(long Bytes);
}

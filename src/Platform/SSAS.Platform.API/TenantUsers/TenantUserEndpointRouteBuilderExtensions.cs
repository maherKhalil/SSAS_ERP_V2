using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.TenantUsers;

namespace SSAS.Platform.API.TenantUsers;

// Lifecycle transitions carry only the concurrency version. No status, no reason code, no tenant: the
// operation IS the intent, and the owning tenant is the trusted current-tenant context.
public sealed record TenantUserLifecycleRequest(
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// ==================================================================================================
// THE TENANT-USER LIFECYCLE SURFACE (T-091). TWO ROUTES, AND THEY OPEN A FOLDER THAT DID NOT EXIST.
// ==================================================================================================
//
// ---- WHY THIS IS NOT A FOLLOW-UP.
//
// `DeactivateTenantUserCommandHandler` and `ReactivateTenantUserCommandHandler` both existed, both were
// registered in DI, and **neither was reachable from anything.** `Platform.Users.Deactivate` and
// `Platform.Users.Reactivate` were both catalog-defined, grantable, and required by no endpoint.
//
// T-091 makes termination close the account. **Without reinstatement, rehiring produces a user nobody can
// restore** — `IssueTenantUserInvitation` refuses a deactivated user, so re-invitation is not a workaround —
// **and the one half-state the terminate handler can reach has no compensating action.** The repair has to
// exist in the same change that creates the thing needing repair.
//
// ---- A PERMISSION NO ROUTE REQUIRES IS THE MIRROR OF FP-006P.
//
// FP-006P was a route requiring a permission the catalog did not define: it refused every caller. This is
// the reverse — a permission an administrator can grant that authorizes nothing, so a grant looks effective
// and does nothing. `EndpointPermissionCatalogJoinTests` asserts endpoints against the catalog; **nothing
// asserts the catalog against endpoints.** T-091 closes two instances by building their routes; the general
// case is recorded and is deliberately not widened into here.
//
// ---- POST, NOT PUT OR PATCH.
//
// Following `Companies`' `activate` / `deactivate`: a lifecycle transition is a named administrative ACT,
// not the assignment of a status field. There is no route that sets a status directly, and its absence is
// the ruling made visible in the surface.
//
// ---- MOUNTED ON `/api/platform`, THE SAME GROUP AS IDENTITY/ACCESS.
//
// The tag differs so the two read apart in a generated client, but the prefix is shared because the plane
// is: tenant-plane administration, permission-gated per route, tenant derived from trusted context and
// never accepted from the caller.
public static class TenantUserEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapPlatformTenantUserEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup("/api/platform").WithTags("Platform Tenant Users");

    group.MapPost("/tenant-users/{tenantUserId:long}/deactivation", DeactivateAsync)
      .RequirePermission(PlatformPermissionNames.DeactivateUsers)
      .WithName("PlatformTenantUsersDeactivate");

    // ---- THE REPAIR, AND ITS OWN PERMISSION.
    //
    // Separate from deactivation because restoring an account is a different decision from closing one —
    // the `GL.Drafts.Manage` / `GL.Journals.Post` precedent. Both constants already existed; neither is new.
    group.MapPost("/tenant-users/{tenantUserId:long}/reactivation", ReactivateAsync)
      .RequirePermission(PlatformPermissionNames.ReactivateUsers)
      .WithName("PlatformTenantUsersReactivate");

    return endpoints;
  }

  private static async Task<IResult> DeactivateAsync(
    HttpContext context,
    long tenantUserId,
    DeactivateTenantUserCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var rowVersion = await ReadRowVersionAsync(context, cancellationToken);
    if (rowVersion is null)
    {
      return ProblemResults.Problem(context, ProblemResults.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new DeactivateTenantUserCommand(tenantUserId, rowVersion), cancellationToken);

    return result.IsFailure
      ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> ReactivateAsync(
    HttpContext context,
    long tenantUserId,
    ReactivateTenantUserCommandHandler handler,
    CancellationToken cancellationToken)
  {
    var rowVersion = await ReadRowVersionAsync(context, cancellationToken);
    if (rowVersion is null)
    {
      return ProblemResults.Problem(context, ProblemResults.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new ReactivateTenantUserCommand(tenantUserId, rowVersion), cancellationToken);

    return result.IsFailure
      ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error))
      : Results.NoContent();
  }

  // One reader for both, because the two bodies are the same shape and a second copy is a second chance to
  // accept a field the other refuses. Strict: an unknown field is a 400, not a silently ignored one.
  private static async Task<byte[]?> ReadRowVersionAsync(
    HttpContext context, CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var request = await StrictRequestReader.ReadStrictJsonAsync<TenantUserLifecycleRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken);

    return request is not null && RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)
      ? rowVersion
      : null;
  }
}

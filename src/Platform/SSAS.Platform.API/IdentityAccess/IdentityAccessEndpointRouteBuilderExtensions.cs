using SSAS.BuildingBlocks.Api.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Roles;

namespace SSAS.Platform.API.IdentityAccess;

// Platform Identity/Access admin HTTP transport. Tenant-plane: the owning tenant is derived from
// the trusted current-tenant context inside the existing Application handlers; no route, query, or
// body accepts a caller-supplied TenantId. This milestone delivers only the GET /roles proof slice.
public static class IdentityAccessEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapPlatformIdentityAccessEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);
    var group = endpoints.MapGroup("/api/platform").WithTags("Platform Identity Access");
    group.MapGet("/roles", ListRolesAsync)
      .RequirePermission(PlatformPermissionNames.ViewRoles)
      .WithName("PlatformRolesList");

    // ---- THE ONE ROW OF THE CAPABILITY GAP THAT NEEDED NO DECISION (T-203).
    //
    // An audit of the 67 documented-but-unrouted rows found 41 waiting on five owner decisions, 15 already
    // deferred by an accepted record, 10 describing capability that exists under another path — and THIS,
    // alone, as work nobody had to authorise. **A read of a static catalogue changes nothing**, its handler
    // was already written and registered, and `Platform.Permissions.View` was already catalogued: one of the
    // 16 permissions that existed for routes that did not.
    //
    // Routing it makes the remaining gap ENTIRELY the owner's, which is worth more as a clean statement to
    // them than the endpoint is worth to anyone.
    group.MapGet("/permissions", ListPermissionCatalogAsync)
      .RequirePermission(PlatformPermissionNames.ViewPermissions)
      .WithName("PlatformPermissionsList");
    return endpoints;
  }

  // The catalogue is a constant of the deployment, not tenant data — but the handler still resolves the
  // tenant actor and filters to tenant-assignable permissions, because `ADR-015`'s PlatformSupport-scoped
  // permissions are never assignable by a tenant and must not be listed to one. The transport adds nothing
  // to that decision; it carries the result.
  private static async Task<IResult> ListPermissionCatalogAsync(
    HttpContext context,
    ListPermissionCatalogQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    // No query parameters at all: the catalogue is neither paged nor filtered, and accepting an ignored
    // parameter would be a promise this route does not keep.
    if (!StrictRequestReader.HasOnly(context.Request.Query, []))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(new ListPermissionCatalogQuery(), cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error));
    }

    return Results.Ok(new PermissionCatalogResponse(
      result.Value.Select(permission => new PermissionCatalogItemResponse(
        permission.Name, permission.Scope.ToString(), permission.Description)).ToArray()));
  }

  private static async Task<IResult> ListRolesAsync(
    HttpContext context,
    ListRolesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    if (!TryListRolesQuery(context.Request.Query, out var query))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error));
    }

    var page = result.Value;
    return Results.Ok(new RolePageResponse(
      page.Items.Select(Map).ToArray(), page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages));
  }

  private static bool TryListRolesQuery(IQueryCollection values, out ListRolesQuery query)
  {
    query = default!;
    if (!StrictRequestReader.HasOnly(values, ["pageNumber", "pageSize"]) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) ||
      !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize))
    {
      return false;
    }

    query = new ListRolesQuery(pageNumber, pageSize);
    return true;
  }

  private static RoleSummaryResponse Map(RoleDto role) => new(
    role.RoleId,
    role.Name,
    role.Description,
    role.RoleType.ToString(),
    role.Status.ToString(),
    role.ActivePermissions,
    RowVersionCodec.Encode(role.RowVersion));
}

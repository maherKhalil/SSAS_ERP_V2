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
    return endpoints;
  }

  private static async Task<IResult> ListRolesAsync(
    HttpContext context,
    ListRolesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
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

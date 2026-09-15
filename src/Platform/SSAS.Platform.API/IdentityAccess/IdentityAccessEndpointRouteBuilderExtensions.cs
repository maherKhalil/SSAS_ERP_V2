using Microsoft.AspNetCore.Mvc;
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
    
    group.MapGet("/roles/{roleId}", GetRoleByIdAsync)
      .RequirePermission(PlatformPermissionNames.ViewRoles)
      .WithName("PlatformRolesGetById");
    group.MapPost("/roles", CreateRoleAsync)
      .RequirePermission(PlatformPermissionNames.CreateRoles)
      .WithName("PlatformRolesCreate");
    group.MapPut("/roles/{roleId}", UpdateRoleAsync)
      .RequirePermission(PlatformPermissionNames.UpdateRoles)
      .WithName("PlatformRolesUpdate");
    group.MapPost("/roles/{roleId}/request-retirement", RequestRoleRetirementAsync)
      .RequirePermission(PlatformPermissionNames.RequestRoleRetirement)
      .WithName("PlatformRolesRequestRetirement");
    group.MapPost("/roles/{roleId}/retire", RetireRoleAsync)
      .RequirePermission(PlatformPermissionNames.RetireRoles)
      .WithName("PlatformRolesRetire");
    group.MapPost("/roles/{roleId}/permissions", AssignPermissionAsync)
      .RequirePermission(PlatformPermissionNames.AssignRolePermissions)
      .WithName("PlatformRolesAssignPermission");
    group.MapPost("/roles/{roleId}/permissions/{permission}/remove", RemovePermissionAsync)
      .RequirePermission(PlatformPermissionNames.RemoveRolePermissions)
      .WithName("PlatformRolesRemovePermission");

    group.MapPost("/users/{userId}/roles", AssignRoleToUserAsync)
      .RequirePermission(PlatformPermissionNames.AssignUserRoles)
      .WithName("PlatformUsersAssignRole");
    group.MapPost("/users/{userId}/roles/{roleId}/remove", RemoveRoleFromUserAsync)
      .RequirePermission(PlatformPermissionNames.RemoveUserRoles)
      .WithName("PlatformUsersRemoveRole");

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

  // Role Endpoints
  private static async Task<IResult> GetRoleByIdAsync(HttpContext context, long roleId, [FromServices] GetRoleByIdQueryHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var result = await handler.HandleAsync(new GetRoleByIdQuery(roleId), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.Ok(result.Value);
  }

  private static async Task<IResult> CreateRoleAsync(HttpContext context, [FromServices] CreateCustomRoleCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<CreateCustomRoleCommand>(cancellationToken);
      if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(request, cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.Created($"/api/platform/roles/{result.Value}", new { roleId = result.Value });
  }

  private static async Task<IResult> UpdateRoleAsync(HttpContext context, long roleId, [FromServices] UpdateCustomRoleCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<UpdateCustomRoleRequest>(cancellationToken);
      if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var command = new UpdateCustomRoleCommand(roleId, request.Name, request.Description, rowVersion);
      var result = await handler.HandleAsync(command, cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }

  public record UpdateCustomRoleRequest(string Name, string Description, string ExpectedRowVersion);

  private static async Task<IResult> RequestRoleRetirementAsync(HttpContext context, long roleId, [FromServices] RequestRoleRetirementCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<RoleLifecycleRequest>(cancellationToken);
      if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(new RequestRoleRetirementCommand(roleId, rowVersion), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }

  private static async Task<IResult> RetireRoleAsync(HttpContext context, long roleId, [FromServices] RetireRoleCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<RoleLifecycleRequest>(cancellationToken);
      if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(new RetireRoleCommand(roleId, rowVersion), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }

  public record RoleLifecycleRequest(string ExpectedRowVersion);

  private static async Task<IResult> AssignPermissionAsync(HttpContext context, long roleId, [FromServices] AssignPermissionToRoleCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<AssignPermissionRequest>(cancellationToken);
      if (request is null || string.IsNullOrWhiteSpace(request.PermissionName) || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(new AssignPermissionToRoleCommand(roleId, request.PermissionName, rowVersion), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }
  public record AssignPermissionRequest(string PermissionName, string ExpectedRowVersion);

  private static async Task<IResult> RemovePermissionAsync(HttpContext context, long roleId, string permission, [FromServices] RemovePermissionFromRoleCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<RemovePermissionRequest>(cancellationToken);
      if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(new RemovePermissionFromRoleCommand(roleId, permission, rowVersion), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }
  public record RemovePermissionRequest(string ExpectedRowVersion);

  // User Roles
  private static async Task<IResult> AssignRoleToUserAsync(HttpContext context, long userId, [FromServices] SSAS.Platform.Application.TenantUsers.AssignRoleToTenantUserCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<AssignUserRoleRequest>(cancellationToken);
      if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(new SSAS.Platform.Application.TenantUsers.AssignRoleToTenantUserCommand(userId, request.RoleId, rowVersion), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }
  public record AssignUserRoleRequest(long RoleId, string ExpectedRowVersion);

  private static async Task<IResult> RemoveRoleFromUserAsync(HttpContext context, long userId, long roleId, [FromServices] SSAS.Platform.Application.TenantUsers.RemoveRoleFromTenantUserCommandHandler handler, CancellationToken cancellationToken)
  {
      ApiResponseSecurity.Apply(context);
      var request = await context.Request.ReadFromJsonAsync<RemoveUserRoleRequest>(cancellationToken);
      if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      var result = await handler.HandleAsync(new SSAS.Platform.Application.TenantUsers.RemoveRoleFromTenantUserCommand(userId, roleId, rowVersion), cancellationToken);
      return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }
  public record RemoveUserRoleRequest(string ExpectedRowVersion);
}

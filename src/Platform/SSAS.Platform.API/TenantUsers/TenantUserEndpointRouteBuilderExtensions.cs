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

// ---- NO `expectedRowVersion`, AND ITS ABSENCE IS THE DECISION (T-092).
//
// `UserEmployeeLink` carries no row version because it has NO MUTABLE STATE: it is created and deleted,
// never updated. **A row version prevents a lost update, and there is no update to lose.** Adding one would
// imply a modification path that does not exist.
//
// The concurrency control is the two unique indexes `ADR-030` Decision 3 is enforced by. A race between two
// administrators loses at the database, and the unit of work already translates that to a refusal — which
// is the correct control for a create-or-refuse, not a workaround for a missing one.
public sealed record LinkEmployeeRequest(
  [property: JsonPropertyName("employeeId")] Guid? EmployeeId);

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

    // ================================================================================================
    // THE EMPLOYEE LINK (T-092, ADR-030). THE ROW THAT MAKES T-090 AND T-091 DO ANYTHING.
    // ================================================================================================
    //
    // Four self-service permissions across two modules resolve through `IUserEmployeeResolver`, and until
    // this route existed **nothing in the product had ever written a link** — so every one of them answered
    // "no linked employee" for every real caller.
    //
    // ---- A PAIR OF PERMISSIONS, NOT ONE.
    //
    // Linking decides WHOSE PAYSLIPS A LOGIN CAN READ. Creating that mapping and destroying it are
    // different decisions with different blast radii — the `Deactivate`/`Reactivate` split, and the
    // `GL.Drafts.Manage` / `GL.Journals.Post` precedent behind it.
    //
    // ---- AND REMOVAL SHIPS WITH CREATION, NOT AFTER IT.
    //
    // At most one live link each way, enforced by two unique indexes, with removal physical and no soft
    // delete: **a mistaken link occupies both slots**, so creating the correct one collides and is refused
    // rather than repaired. Without removal the FIRST mistake would be permanent.
    //
    // The alternative was an upsert on the link route, and it is worse: it hides a destructive act inside a
    // creative one, so reassigning which employee a login maps to would read as "create a link" in an audit
    // trail.
    //
    // ---- POST TO A NAMED SUB-RESOURCE, NOT DELETE.
    //
    // **There is no `MapDelete` anywhere in `src/`**, and this is not the task to introduce one. Removal is
    // spelled the way `/manager/remove` and `/holidays/remove` already spell it.
    group.MapPost("/tenant-users/{tenantUserId:long}/employee-link", LinkEmployeeAsync)
      .RequirePermission(PlatformPermissionNames.LinkEmployees)
      .WithName("PlatformTenantUsersLinkEmployee");
    group.MapPost("/tenant-users/{tenantUserId:long}/employee-link/remove", UnlinkEmployeeAsync)
      .RequirePermission(PlatformPermissionNames.UnlinkEmployees)
      .WithName("PlatformTenantUsersUnlinkEmployee");

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

  private static async Task<IResult> LinkEmployeeAsync(
    HttpContext context,
    long tenantUserId,
    LinkEmployeeToTenantUserCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var request = await StrictRequestReader.ReadStrictJsonAsync<LinkEmployeeRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["employeeId"] = [JsonValueKind.String]
      },
      cancellationToken);

    if (request?.EmployeeId is not { } employeeId)
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(
      new LinkEmployeeToTenantUserCommand(tenantUserId, employeeId), cancellationToken);

    return result.IsFailure
      ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error))
      : Results.NoContent();
  }

  // ---- NO BODY AT ALL, AND THAT FOLLOWS FROM ONE LIVE LINK EACH WAY.
  //
  // The tenant user identifies the link uniquely, so an employee in the body would exist only to be
  // validated against a state the caller could have read — a parameter whose sole purpose is to be
  // rejected. `StrictRequestReader` is not called, so an unexpected body is ignored rather than parsed.
  private static async Task<IResult> UnlinkEmployeeAsync(
    HttpContext context,
    long tenantUserId,
    UnlinkEmployeeFromTenantUserCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var result = await handler.HandleAsync(
      new UnlinkEmployeeFromTenantUserCommand(tenantUserId), cancellationToken);

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

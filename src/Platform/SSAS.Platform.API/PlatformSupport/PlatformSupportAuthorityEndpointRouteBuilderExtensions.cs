using SSAS.BuildingBlocks.Api.Transport;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;

namespace SSAS.Platform.API.PlatformSupport;

// Phase-4D platform authority-administration HTTP transport (ADR-016 §5 slice 4D).
//
// Every route — mutation and read alike — is gated by RequirePlatformPermission(Platform.Support.Administer)
// (DEC-TEN-0021 / DEC-TEN-0025), which resolves to the committed Phase-4A platform policy: a validated
// platform-plane token carrying that exact catalog PlatformSupport permission. A tenant-plane token cannot
// satisfy it even if it carries a claim of the same name, and no route is anonymous.
//
// The transport adds authorization and shape only. The caller supplies the administrative TARGET and business
// input (principal id, permission name, expected row version); the ACTOR is derived server-side by the existing
// Application handlers from the trusted authenticated context — no request field names the actor, its plane or
// its permissions.
//
// Deliberately absent: any last-admin guard. Self-revoke of Administer, self-disable, and removal of the final
// active Administer assignment are all ALLOWED (DEC-TEN-0026); the safety net is the committed Phase-4D-0
// administrative recovery, which is an independent bootstrap subsystem and is never invoked from these routes.
public static class PlatformSupportAuthorityEndpointRouteBuilderExtensions
{
  public const string RoutePrefix = "/api/platform/support/principals";
  private const string Administer = PlatformPermissionNames.AdministerPlatformSupport;

  public static IEndpointRouteBuilder MapPlatformSupportAuthorityEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);
    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Support Authority");

    group.MapPost("", RegisterAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsRegister");
    group.MapGet("", ListAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsList");
    group.MapGet("/{principalId:long}", GetByIdAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsGetById");
    group.MapGet("/{principalId:long}/assignments", ListAssignmentsAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsAssignments");
    group.MapGet("/{principalId:long}/permissions", GetActivePermissionsAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsActivePermissions");
    group.MapPost("/{principalId:long}/grant", GrantAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsGrant");
    group.MapPost("/{principalId:long}/revoke", RevokeAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsRevoke");
    group.MapPost("/{principalId:long}/disable", DisableAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsDisable");
    group.MapPost("/{principalId:long}/reenable", ReenableAsync)
      .RequirePlatformPermission(Administer).WithName("PlatformSupportPrincipalsReenable");

    return endpoints;
  }

  // ---- Mutations ----

  private static async Task<IResult> RegisterAsync(
    HttpContext context,
    [FromServices] RegisterPlatformSupportPrincipalCommandHandler handler,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<RegisterPlatformSupportPrincipalRequest>(
      context,
      new Dictionary<string, JsonValueKind[]> { ["identityId"] = [JsonValueKind.Number] },
      cancellationToken);
    if (request?.IdentityId is not { } identityId)
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error))
      : Results.Created(
        $"{RoutePrefix}/{result.Value}",
        new RegisterPlatformSupportPrincipalResponse(result.Value));
  }

  private static Task<IResult> GrantAsync(
    HttpContext context,
    long principalId,
    [FromServices] GrantPlatformPermissionCommandHandler handler,
    CancellationToken cancellationToken) =>
    PermissionMutationAsync(context, permissionName =>
      handler.HandleAsync(new GrantPlatformPermissionCommand(principalId, permissionName), cancellationToken), cancellationToken);

  // Revoke deliberately accepts the caller's own principal and the final Administer assignment (DEC-TEN-0026).
  private static Task<IResult> RevokeAsync(
    HttpContext context,
    long principalId,
    [FromServices] RevokePlatformPermissionCommandHandler handler,
    CancellationToken cancellationToken) =>
    PermissionMutationAsync(context, permissionName =>
      handler.HandleAsync(new RevokePlatformPermissionCommand(principalId, permissionName), cancellationToken), cancellationToken);

  // Disable deliberately accepts the caller's own principal (DEC-TEN-0026). The existing handler revokes the
  // principal's platform sessions in the same transaction; tenant sessions and SecurityVersion are untouched.
  private static Task<IResult> DisableAsync(
    HttpContext context,
    long principalId,
    [FromServices] DisablePlatformSupportPrincipalCommandHandler handler,
    CancellationToken cancellationToken) =>
    LifecycleAsync(context, rowVersion =>
      handler.HandleAsync(new DisablePlatformSupportPrincipalCommand(principalId, rowVersion), cancellationToken), cancellationToken);

  private static Task<IResult> ReenableAsync(
    HttpContext context,
    long principalId,
    [FromServices] ReenablePlatformSupportPrincipalCommandHandler handler,
    CancellationToken cancellationToken) =>
    LifecycleAsync(context, rowVersion =>
      handler.HandleAsync(new ReenablePlatformSupportPrincipalCommand(principalId, rowVersion), cancellationToken), cancellationToken);

  private static async Task<IResult> PermissionMutationAsync(
    HttpContext context,
    Func<string, Task<Result>> mutate,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<PlatformPermissionRequest>(
      context,
      new Dictionary<string, JsonValueKind[]> { ["permissionName"] = [JsonValueKind.String] },
      cancellationToken);
    if (string.IsNullOrWhiteSpace(request?.PermissionName))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await mutate(request.PermissionName);
    return result.IsFailure
      ? ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error))
      : Results.NoContent();
  }

  private static async Task<IResult> LifecycleAsync(
    HttpContext context,
    Func<byte[], Task<Result>> transition,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<PlatformSupportPrincipalLifecycleRequest>(
      context,
      new Dictionary<string, JsonValueKind[]> { ["expectedRowVersion"] = [JsonValueKind.String] },
      cancellationToken);
    if (request is null)
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return ProblemResults.Problem(context, ProblemResults.RowVersionInvalid);
    }

    var result = await transition(rowVersion);
    return result.IsFailure
      ? ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error))
      : Results.NoContent();
  }

  // ---- Reads (DEC-TEN-0025: same Administer permission, no separate read permission) ----

  private static async Task<IResult> ListAsync(
    HttpContext context,
    [FromServices] ListPlatformSupportPrincipalsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    if (!StrictRequestReader.HasOnly(context.Request.Query, ["pageNumber", "pageSize"]) ||
      !StrictRequestReader.TryInt(context.Request.Query, "pageNumber", 1, out var pageNumber) ||
      !StrictRequestReader.TryInt(context.Request.Query, "pageSize", 50, out var pageSize))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(
      new ListPlatformSupportPrincipalsQuery(pageNumber, pageSize), cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error));
    }

    var page = result.Value;
    return Results.Ok(new PlatformSupportPrincipalPageResponse(
      page.Items.Select(dto => PlatformSupportPrincipalResponse.From(dto, RowVersionCodec.Encode(dto.RowVersion))).ToArray(),
      page.PageNumber,
      page.PageSize,
      page.TotalCount,
      page.TotalPages));
  }

  private static async Task<IResult> GetByIdAsync(
    HttpContext context,
    long principalId,
    [FromServices] GetPlatformSupportPrincipalQueryHandler handler,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetPlatformSupportPrincipalQuery(principalId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error))
      : Results.Ok(PlatformSupportPrincipalResponse.From(result.Value, RowVersionCodec.Encode(result.Value.RowVersion)));
  }

  private static async Task<IResult> ListAssignmentsAsync(
    HttpContext context,
    long principalId,
    [FromServices] ListPlatformPermissionAssignmentsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(
      new ListPlatformPermissionAssignmentsQuery(principalId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value.Select(PlatformPermissionAssignmentResponse.From).ToArray());
  }

  private static async Task<IResult> GetActivePermissionsAsync(
    HttpContext context,
    long principalId,
    [FromServices] GetActivePlatformSupportPermissionsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(
      new GetActivePlatformSupportPermissionsQuery(principalId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, PlatformSupportAuthorityApiErrorMapper.Map(result.Error))
      : Results.Ok(new PlatformSupportActivePermissionsResponse(result.Value.ToArray()));
  }
}

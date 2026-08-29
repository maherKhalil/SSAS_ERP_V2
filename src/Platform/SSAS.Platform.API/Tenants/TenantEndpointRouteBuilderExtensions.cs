using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.API.Tenants;

// ==================================================================================================
// THE TENANT REGISTRY'S TRANSPORT (T-155). SEVEN HANDLERS THAT HAD NO ROUTE.
// ==================================================================================================
//
// Every one of these handlers already existed and was reachable only from a test. **This is transport over
// a settled application layer** — no command, no handler and no domain rule changed.
//
// ---- ⚠ THREE PERMISSIONS GOVERN THIS SLICE, NOT FOUR.
//
// `Platform.Tenant.Administer` (`AdministerTenant`) looks like a fourth and is not one. Its catalog entry
// reads *"administer the tenant and reach every active branch"*, and `TenantAdministratorAuthority` uses it
// **inside** a tenant. **It is authority WITHIN a tenant; these routes are the registry OF tenants**, and
// granting it here would let a tenant administrator create and archive other tenants.
public static class TenantEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/platform/tenants";

  // ---- THE REASON SET THE TRANSPORT ACCEPTS, AND THE TWO IT REFUSES.
  //
  // `Created` and `ProvisioningCompleted` are **records of what the system did**, written by the domain
  // when a tenant is created and when it activates. A caller naming either would be asserting that a
  // system event happened, so they are absent from the accepted set and arrive as `request.invalid`.
  //
  // ⚠ **The remaining six are NOT narrowed per operation, and that is deliberate.** It is tempting to
  // allow only `IssueResolved` on reactivate and only `CustomerClosure` on archive. **The domain already
  // rules on which reason suits which transition** (`Tenant.InvalidTransitionReason`), and a transport
  // that guessed a tighter rule would refuse pairings the domain permits — with no way for an operator to
  // discover which. Transport bounds the ENUM; the domain stays authoritative on the pairing.
  private static readonly string[] CallerSuppliedReasons =
    ["Administrative", "Security", "Compliance", "Operational", "CustomerClosure", "IssueResolved"];

  public static IEndpointRouteBuilder MapPlatformTenantEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Tenants");

    // ---- ⚠ EVERY ROUTE IS ON THE PLATFORM PLANE, NOT THE TENANT PLANE, AND THE FIRST DRAFT WAS WRONG.
    //
    // `RequirePermission` maps a route onto the TENANT plane. All three tenant permissions are scoped
    // `PlatformSupport` in the catalog, and `TenantPermissionClaimFilter` **strips a PlatformSupport
    // permission out of every tenant token** — so the tenant-plane version of this file compiled, mapped,
    // read correctly, and refused every caller alive.
    //
    // **Caught by `Every_route_is_mapped_on_the_plane_its_permission_is_scoped_to`**, which exists
    // because a route that refuses everyone looks identical to a route nobody has permission for yet.
    //
    // The scoping is also the right answer on its own terms: **the registry OF tenants is operated by
    // platform support, from outside any tenant.** A tenant-plane caller is already inside one.

    group.MapPost("", CreateAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ManageTenants)
      .WithName("PlatformTenantsCreate");

    group.MapGet("", ListAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewTenants)
      .WithName("PlatformTenantsList");

    group.MapGet("/{tenantId}", GetByIdAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewTenants)
      .WithName("PlatformTenantsGetById");

    // ---- THE FOUR TRANSITIONS ARE ONE PERMISSION, MATCHING COMPANIES.
    //
    // Suspending and archiving are not separable in practice: anyone who can suspend a tenant can deny it
    // service, and the difference between that and archiving is recoverability, not blast radius.
    group.MapPost("/{tenantId}/activate", ActivateAsync)
      .RequirePlatformPermission(PlatformPermissionNames.TenantLifecycle)
      .WithName("PlatformTenantsActivate");

    group.MapPost("/{tenantId}/suspend", SuspendAsync)
      .RequirePlatformPermission(PlatformPermissionNames.TenantLifecycle)
      .WithName("PlatformTenantsSuspend");

    group.MapPost("/{tenantId}/reactivate", ReactivateAsync)
      .RequirePlatformPermission(PlatformPermissionNames.TenantLifecycle)
      .WithName("PlatformTenantsReactivate");

    group.MapPost("/{tenantId}/archive", ArchiveAsync)
      .RequirePlatformPermission(PlatformPermissionNames.TenantLifecycle)
      .WithName("PlatformTenantsArchive");

    return endpoints;
  }

  private static async Task<IResult> CreateAsync(
    HttpContext context,
    CreateTenantCommandHandler handler,
    ITenantReadService readService,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateTenantRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["tenantCode"] = [JsonValueKind.String],
        ["tenantName"] = [JsonValueKind.String]
      },
      cancellationToken);
    if (request is null)
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(
      new CreateTenantCommand(request.TenantCode!, request.TenantName!), cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, TenantApiErrorMapper.Map(result.Error));
    }

    var created = await readService.GetByIdAsync(result.Value, cancellationToken);
    if (created is null)
    {
      // Persisted but not readable back. Reporting success with no body, or fabricating a projection from
      // the request, would both claim more than is known.
      return ProblemResults.Problem(context, ProblemResults.WriteFailure);
    }

    return Results.Created(
      $"{RoutePrefix}/{created.TenantId}",
      TenantResponse.From(created, RowVersionCodec.Encode(created.RowVersion)));
  }

  private static async Task<IResult> ListAsync(
    HttpContext context,
    ListTenantsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    if (!TryListQuery(context.Request.Query, out var query))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, TenantApiErrorMapper.Map(result.Error));
    }

    var page = result.Value;
    return Results.Ok(new TenantPageResponse(
      page.Items.Select(dto => TenantResponse.From(dto, RowVersionCodec.Encode(dto.RowVersion))).ToArray(),
      page.PageNumber,
      page.PageSize,
      page.TotalCount,
      page.TotalPages));
  }

  private static async Task<IResult> GetByIdAsync(
    HttpContext context,
    Guid tenantId,
    GetTenantQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var result = await handler.HandleAsync(new GetTenantQuery(tenantId), cancellationToken);
    if (result.IsFailure)
    {
      // An unknown id and one this caller may not reach are the same answer — see the mapper.
      return ProblemResults.Problem(context, TenantApiErrorMapper.Map(result.Error));
    }

    return Results.Ok(TenantResponse.From(result.Value, RowVersionCodec.Encode(result.Value.RowVersion)));
  }

  private static bool TryListQuery(IQueryCollection values, out ListTenantsQuery query)
  {
    query = default!;

    if (!StrictRequestReader.HasOnly(values, ["pageNumber", "pageSize", "status"]) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) ||
      !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize) ||
      !StrictRequestReader.TryOptional(values, "status", out var statusText) ||

      // All four members, `Provisioning` included: a tenant mid-provisioning is exactly the one an
      // operator most wants to list.
      !StrictRequestReader.IsOneOf(statusText, ["Provisioning", "Active", "Suspended", "Archived"]))
    {
      return false;
    }

    TenantStatus? status = statusText is null ? null : Enum.Parse<TenantStatus>(statusText);
    query = new ListTenantsQuery(status, pageNumber, pageSize);
    return true;
  }

  // ---- ACTIVATE TAKES NO REASON, SO IT DOES NOT SHARE THE LIFECYCLE HELPER.
  //
  // Routing it through a helper that reads a `reasonCode` would mean accepting a field the command cannot
  // carry. Its own reader is four lines and refuses one.
  private static async Task<IResult> ActivateAsync(
    HttpContext context,
    Guid tenantId,
    ActivateTenantCommandHandler handler,
    ITenantReadService readService,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var request = await StrictRequestReader.ReadStrictJsonAsync<ActivateTenantRequest>(
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

    var result = await handler.HandleAsync(
      new ActivateTenantCommand(tenantId, rowVersion), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, TenantApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, readService, tenantId, cancellationToken);
  }

  private static Task<IResult> SuspendAsync(
    HttpContext context, Guid tenantId, SuspendTenantCommandHandler handler,
    ITenantReadService readService, CancellationToken cancellationToken) =>
    LifecycleAsync(context, tenantId, readService,
      (id, reason, rowVersion) =>
        handler.HandleAsync(new SuspendTenantCommand(id, reason, rowVersion), cancellationToken),
      cancellationToken);

  private static Task<IResult> ReactivateAsync(
    HttpContext context, Guid tenantId, ReactivateTenantCommandHandler handler,
    ITenantReadService readService, CancellationToken cancellationToken) =>
    LifecycleAsync(context, tenantId, readService,
      (id, reason, rowVersion) =>
        handler.HandleAsync(new ReactivateTenantCommand(id, reason, rowVersion), cancellationToken),
      cancellationToken);

  private static Task<IResult> ArchiveAsync(
    HttpContext context, Guid tenantId, ArchiveTenantCommandHandler handler,
    ITenantReadService readService, CancellationToken cancellationToken) =>
    LifecycleAsync(context, tenantId, readService,
      (id, reason, rowVersion) =>
        handler.HandleAsync(new ArchiveTenantCommand(id, reason, rowVersion), cancellationToken),
      cancellationToken);

  private static async Task<IResult> LifecycleAsync(
    HttpContext context,
    Guid tenantId,
    ITenantReadService readService,
    Func<Guid, TenantStatusChangeReason, byte[], Task<Result>> execute,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);

    var request = await StrictRequestReader.ReadStrictJsonAsync<TenantLifecycleRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["reasonCode"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken);
    if (request is null || !TryParseReason(request.ReasonCode, out var reason))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return ProblemResults.Problem(context, ProblemResults.RowVersionInvalid);
    }

    var result = await execute(tenantId, reason, rowVersion);
    return result.IsFailure
      ? ProblemResults.Problem(context, TenantApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, readService, tenantId, cancellationToken);
  }

  private static bool TryParseReason(string? value, out TenantStatusChangeReason reason)
  {
    reason = default;
    if (value is null || !StrictRequestReader.IsOneOf(value, CallerSuppliedReasons))
    {
      return false;
    }

    reason = Enum.Parse<TenantStatusChangeReason>(value);
    return true;
  }

  // A transition that succeeded but reads back as nothing is a write this route cannot describe.
  private static async Task<IResult> ReadBackAsync(
    HttpContext context,
    ITenantReadService readService,
    Guid tenantId,
    CancellationToken cancellationToken)
  {
    var dto = await readService.GetByIdAsync(tenantId, cancellationToken);
    return dto is null
      ? ProblemResults.Problem(context, ProblemResults.WriteFailure)
      : Results.Ok(TenantResponse.From(dto, RowVersionCodec.Encode(dto.RowVersion)));
  }
}

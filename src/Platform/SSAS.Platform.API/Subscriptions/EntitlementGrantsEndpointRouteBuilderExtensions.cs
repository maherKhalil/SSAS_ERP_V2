using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Subscriptions.EntitlementGrants;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.API.Subscriptions;

public static class EntitlementGrantsEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapPlatformEntitlementGrantsEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup("/api/platform/tenants/{tenantId:guid}/grants").WithTags("Platform Entitlement Grants");

    group.MapGet("", GetTenantGrantsAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewSubscriptions)
      .WithName("PlatformEntitlementGrantsGet");

    group.MapPost("", GrantEntitlementAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerEntitlementGrants)
      .WithName("PlatformEntitlementGrantsCreate");

    group.MapPost("/revoke", RevokeEntitlementAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerEntitlementGrants)
      .WithName("PlatformEntitlementGrantsRevoke");

    return endpoints;
  }

  private static async Task<IResult> GetTenantGrantsAsync(
    HttpContext context,
    Guid tenantId,
    GetTenantGrantsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetTenantGrantsQuery(tenantId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value);
  }

  private static async Task<IResult> GrantEntitlementAsync(
    HttpContext context,
    Guid tenantId,
    EntitlementGrantsCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<GrantEntitlementRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["grantKind"] = [JsonValueKind.String],
        ["effectiveFromUtc"] = [JsonValueKind.String]
      }, cancellationToken);
    
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    Result<Guid> result;
    if (request.GrantKind == EntitlementGrantKind.ModuleGrant)
    {
      if (string.IsNullOrWhiteSpace(request.ModuleKey)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      result = await handler.HandleGrantModuleAsync(new GrantModuleCommand(
        tenantId, request.ModuleKey, request.EffectiveFromUtc, request.ExpiresUtc, request.ReasonCode, request.ReasonText), cancellationToken);
    }
    else
    {
      if (string.IsNullOrWhiteSpace(request.LimitKey) || request.LimitValue == null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      result = await handler.HandleRaiseLimitAsync(new RaiseLimitCommand(
        tenantId, request.LimitKey, request.LimitValue.Value, request.EffectiveFromUtc, request.ExpiresUtc, request.ReasonCode, request.ReasonText), cancellationToken);
    }

    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Created($"/api/platform/tenants/{tenantId}/grants", new { id = result.Value });
  }

  private static async Task<IResult> RevokeEntitlementAsync(
    HttpContext context,
    Guid tenantId,
    EntitlementGrantsCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<RevokeEntitlementRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["grantKind"] = [JsonValueKind.String],
        ["effectiveFromUtc"] = [JsonValueKind.String]
      }, cancellationToken);
    
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    Result<Guid> result;
    if (request.GrantKind == EntitlementGrantKind.ModuleGrant)
    {
      if (string.IsNullOrWhiteSpace(request.ModuleKey)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      result = await handler.HandleRevokeModuleAsync(new RevokeModuleCommand(
        tenantId, request.ModuleKey, request.EffectiveFromUtc, request.ReasonCode, request.ReasonText), cancellationToken);
    }
    else
    {
      if (string.IsNullOrWhiteSpace(request.LimitKey)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
      result = await handler.HandleRevokeLimitAsync(new RevokeLimitCommand(
        tenantId, request.LimitKey, request.EffectiveFromUtc, request.ReasonCode, request.ReasonText), cancellationToken);
    }

    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Created($"/api/platform/tenants/{tenantId}/grants", new { id = result.Value });
  }
}

public class GrantEntitlementRequest
{
  [JsonPropertyName("grantKind")]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EntitlementGrantKind GrantKind { get; set; }

  [JsonPropertyName("moduleKey")]
  public string? ModuleKey { get; set; }

  [JsonPropertyName("limitKey")]
  public string? LimitKey { get; set; }

  [JsonPropertyName("limitValue")]
  public long? LimitValue { get; set; }

  [JsonPropertyName("effectiveFromUtc")]
  public DateTimeOffset EffectiveFromUtc { get; set; }

  [JsonPropertyName("expiresUtc")]
  public DateTimeOffset? ExpiresUtc { get; set; }

  [JsonPropertyName("reasonCode")]
  public string? ReasonCode { get; set; }

  [JsonPropertyName("reasonText")]
  public string? ReasonText { get; set; }
}

public class RevokeEntitlementRequest
{
  [JsonPropertyName("grantKind")]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EntitlementGrantKind GrantKind { get; set; }

  [JsonPropertyName("moduleKey")]
  public string? ModuleKey { get; set; }

  [JsonPropertyName("limitKey")]
  public string? LimitKey { get; set; }

  [JsonPropertyName("effectiveFromUtc")]
  public DateTimeOffset EffectiveFromUtc { get; set; }

  [JsonPropertyName("reasonCode")]
  public string? ReasonCode { get; set; }

  [JsonPropertyName("reasonText")]
  public string? ReasonText { get; set; }
}

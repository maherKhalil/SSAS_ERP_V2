using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Subscriptions.TenantSubscriptions;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;

namespace SSAS.Platform.API.Subscriptions;

public static class TenantSubscriptionsEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapPlatformTenantSubscriptionsEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var allGroup = endpoints.MapGroup("/api/platform/subscriptions").WithTags("Platform Subscriptions");
    
    allGroup.MapGet("", GetAllSubscriptionsAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewSubscriptions)
      .WithName("PlatformSubscriptionsGetAll");

    var tenantGroup = endpoints.MapGroup("/api/platform/tenants/{tenantId}/subscriptions").WithTags("Platform Tenant Subscriptions");

    tenantGroup.MapGet("", GetTenantSubscriptionsAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewSubscriptions)
      .WithName("PlatformTenantSubscriptionsGet");

    tenantGroup.MapGet("/current", GetCurrentTenantSubscriptionAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewSubscriptions)
      .WithName("PlatformTenantSubscriptionsGetCurrent");

    tenantGroup.MapPost("", AppendTenantSubscriptionAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerSubscriptions)
      .WithName("PlatformTenantSubscriptionsAppend");

    return endpoints;
  }

  private static async Task<IResult> GetAllSubscriptionsAsync(
    HttpContext context,
    GetAllSubscriptionsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetAllSubscriptionsQuery(), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value);
  }

  private static async Task<IResult> GetTenantSubscriptionsAsync(
    HttpContext context,
    Guid tenantId,
    GetTenantSubscriptionsQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetTenantSubscriptionsQuery(tenantId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value);
  }

  private static async Task<IResult> GetCurrentTenantSubscriptionAsync(
    HttpContext context,
    Guid tenantId,
    [FromQuery] DateTimeOffset? asOf,
    GetCurrentTenantSubscriptionQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetCurrentTenantSubscriptionQuery(tenantId, asOf), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value);
  }

  private static async Task<IResult> AppendTenantSubscriptionAsync(
    HttpContext context,
    Guid tenantId,
    AppendTenantSubscriptionCommandHandler handler,
    ITenantSubscriptionQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<AppendTenantSubscriptionRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["subscriptionPlanId"] = [JsonValueKind.String],
        ["effectiveFromUtc"] = [JsonValueKind.String],
        ["termKind"] = [JsonValueKind.String],
        ["termStartUtc"] = [JsonValueKind.String],
        ["billingCurrencyCode"] = [JsonValueKind.String]
      }, cancellationToken);
    
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleAsync(new AppendTenantSubscriptionCommand(
      tenantId,
      request.SubscriptionPlanId,
      request.EffectiveFromUtc,
      request.TermKind,
      request.TermStartUtc,
      request.TermEndUtc,
      request.BillingCurrencyCode!,
      request.ChangeReasonCode,
      request.ChangeReasonText
    ), cancellationToken);

    if (result.IsFailure) return ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error));

    var currentResult = await queries.GetCurrentTenantSubscriptionAsync(tenantId, request.EffectiveFromUtc, cancellationToken);
    return currentResult.IsFailure
      ? ProblemResults.Problem(context, ProblemResults.WriteFailure)
      : Results.Created($"/api/platform/tenants/{tenantId}/subscriptions/current", currentResult.Value);
  }
}

public class AppendTenantSubscriptionRequest
{
  [JsonPropertyName("subscriptionPlanId")]
  public Guid SubscriptionPlanId { get; set; }

  [JsonPropertyName("effectiveFromUtc")]
  public DateTimeOffset EffectiveFromUtc { get; set; }

  [JsonPropertyName("termKind")]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public SubscriptionTermKind TermKind { get; set; }

  [JsonPropertyName("termStartUtc")]
  public DateTimeOffset TermStartUtc { get; set; }

  [JsonPropertyName("termEndUtc")]
  public DateTimeOffset? TermEndUtc { get; set; }

  [JsonPropertyName("billingCurrencyCode")]
  public string? BillingCurrencyCode { get; set; }

  [JsonPropertyName("changeReasonCode")]
  public string? ChangeReasonCode { get; set; }

  [JsonPropertyName("changeReasonText")]
  public string? ChangeReasonText { get; set; }
}

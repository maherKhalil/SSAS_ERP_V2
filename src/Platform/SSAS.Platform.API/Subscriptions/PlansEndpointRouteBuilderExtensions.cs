using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Subscriptions.Plans;
using SSAS.Platform.Domain.Subscriptions;
using System.Text.Json.Serialization;
using SSAS.Platform.Application.Permissions;

namespace SSAS.Platform.API.Subscriptions;

public static class PlansEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/platform/plans";

  public static IEndpointRouteBuilder MapPlatformPlansEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);
    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Plans");

    group.MapGet("", GetPlansAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewPlans)
      .WithName("PlatformPlansList");

    group.MapGet("/{planId:guid}", GetPlanByIdAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewPlans)
      .WithName("PlatformPlansGetById");

    group.MapPost("", CreatePlanAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerPlans)
      .WithName("PlatformPlansCreate");

    group.MapPut("/{planId:guid}", UpdatePlanAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerPlans)
      .WithName("PlatformPlansUpdate");

    group.MapPost("/{planId:guid}/retire", RetirePlanAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerPlans)
      .WithName("PlatformPlansRetire");

    group.MapPut("/{planId:guid}/modules", UpdatePlanModulesAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerPlans)
      .WithName("PlatformPlansUpdateModules");

    group.MapPut("/{planId:guid}/limits", UpdatePlanLimitsAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerPlans)
      .WithName("PlatformPlansUpdateLimits");

    group.MapPut("/{planId:guid}/prices", UpdatePlanPricesAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerPlans)
      .WithName("PlatformPlansUpdatePrices");

    return endpoints;
  }

  private static async Task<IResult> GetPlansAsync(
    HttpContext context,
    GetSubscriptionPlansQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetSubscriptionPlansQuery(), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value);
  }

  private static async Task<IResult> GetPlanByIdAsync(
    HttpContext context,
    Guid planId,
    GetSubscriptionPlanByIdQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetSubscriptionPlanByIdQuery(planId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : Results.Ok(result.Value);
  }

  private static async Task<IResult> CreatePlanAsync(
    HttpContext context,
    CreateSubscriptionPlanCommandHandler handler,
    IPlanQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreatePlanRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["planCode"] = [JsonValueKind.String],
        ["planName"] = [JsonValueKind.String]
      }, cancellationToken);
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleAsync(new CreateSubscriptionPlanCommand(request.PlanCode!, request.PlanName!), cancellationToken);
    if (result.IsFailure) return ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error));

    var createdResult = await queries.GetPlanByIdAsync(result.Value, cancellationToken);
    return createdResult.IsFailure
      ? ProblemResults.Problem(context, ProblemResults.WriteFailure)
      : Results.Created($"{RoutePrefix}/{result.Value}", createdResult.Value);
  }

  private static async Task<IResult> UpdatePlanAsync(
    HttpContext context,
    Guid planId,
    UpdateSubscriptionPlanCommandHandler handler,
    IPlanQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdatePlanRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["planName"] = [JsonValueKind.String]
      }, cancellationToken);
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleAsync(new UpdateSubscriptionPlanCommand(planId, request.PlanName!), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, queries, planId, cancellationToken);
  }

  private static async Task<IResult> RetirePlanAsync(
    HttpContext context,
    Guid planId,
    RetireSubscriptionPlanCommandHandler handler,
    IPlanQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new RetireSubscriptionPlanCommand(planId), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, queries, planId, cancellationToken);
  }

  private static async Task<IResult> UpdatePlanModulesAsync(
    HttpContext context,
    Guid planId,
    SetPlanModulesCommandHandler handler,
    IPlanQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await context.Request.ReadFromJsonAsync<List<string>>(cancellationToken);
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleAsync(new SetPlanModulesCommand(planId, request), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, queries, planId, cancellationToken);
  }

  private static async Task<IResult> UpdatePlanLimitsAsync(
    HttpContext context,
    Guid planId,
    SetPlanLimitsCommandHandler handler,
    IPlanQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await context.Request.ReadFromJsonAsync<List<PlanLimitDto>>(cancellationToken);
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleAsync(new SetPlanLimitsCommand(planId, request), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, queries, planId, cancellationToken);
  }

  private static async Task<IResult> UpdatePlanPricesAsync(
    HttpContext context,
    Guid planId,
    SetPlanPricesCommandHandler handler,
    IPlanQueries queries,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await context.Request.ReadFromJsonAsync<List<PlanPriceDto>>(cancellationToken);
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleAsync(new SetPlanPricesCommand(planId, request), cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, queries, planId, cancellationToken);
  }

  private static async Task<IResult> ReadBackAsync(
    HttpContext context,
    IPlanQueries queries,
    Guid planId,
    CancellationToken cancellationToken)
  {
    var dtoResult = await queries.GetPlanByIdAsync(planId, cancellationToken);
    return dtoResult.IsFailure
      ? ProblemResults.Problem(context, ProblemResults.WriteFailure)
      : Results.Ok(dtoResult.Value);
  }
}

public class CreatePlanRequest
{
  [JsonPropertyName("planCode")]
  public string? PlanCode { get; set; }

  [JsonPropertyName("planName")]
  public string? PlanName { get; set; }
}

public class UpdatePlanRequest
{
  [JsonPropertyName("planName")]
  public string? PlanName { get; set; }
}

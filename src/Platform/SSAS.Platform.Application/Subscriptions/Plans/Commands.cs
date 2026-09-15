using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed record CreateSubscriptionPlanCommand(
  string PlanCode,
  string PlanName);

public sealed record UpdateSubscriptionPlanCommand(
  Guid SubscriptionPlanId,
  string PlanName);

public sealed record RetireSubscriptionPlanCommand(
  Guid SubscriptionPlanId);

public sealed record SetPlanModulesCommand(
  Guid SubscriptionPlanId,
  IEnumerable<string> Modules);

public sealed record SetPlanLimitsCommand(
  Guid SubscriptionPlanId,
  IEnumerable<PlanLimitDto> Limits);

public sealed record SetPlanPricesCommand(
  Guid SubscriptionPlanId,
  IEnumerable<PlanPriceDto> Prices);


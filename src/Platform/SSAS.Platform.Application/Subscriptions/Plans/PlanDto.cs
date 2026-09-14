using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Enums;
using System.Text.Json.Serialization;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed record PlanDto(
  Guid SubscriptionPlanId,
  string PlanCode,
  string PlanName,
  SubscriptionPlanStatus Status,
  IReadOnlyCollection<PlanModuleGrantDto> Modules,
  IReadOnlyCollection<PlanLimitDto> Limits,
  IReadOnlyCollection<PlanPriceDto> Prices);

public sealed record PlanModuleGrantDto(string ModuleKey);

public sealed record PlanLimitDto(string LimitKey, long LimitValue);

public sealed record PlanPriceDto(
  string CurrencyCode, 
  [property: JsonConverter(typeof(JsonStringEnumConverter))] SubscriptionBillingPeriod BillingPeriod, 
  decimal Amount);

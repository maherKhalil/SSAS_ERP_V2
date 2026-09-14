using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed record GetSubscriptionPlansQuery;

public sealed record GetSubscriptionPlanByIdQuery(Guid SubscriptionPlanId);


using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.Subscriptions;

public static class SubscriptionApiErrorMapper
{
  public static ApiError Map(Error error) => MapCore(error).Explaining(error.Message, error.Field);

  private static ApiError MapCore(Error error)
  {
    return error.Code switch
    {
      "Subscription.InvalidPlanCode" => ProblemResults.RequestInvalid,
      "Subscription.InvalidPlanName" => ProblemResults.RequestInvalid,
      "Subscription.InvalidActor" => ProblemResults.Forbidden,
      "Subscription.InvalidModuleKey" => ProblemResults.RequestInvalid,
      "Subscription.DuplicateModuleGrant" => ProblemResults.RequestInvalid,
      "Subscription.InvalidLimitKey" => ProblemResults.RequestInvalid,
      "Subscription.InvalidLimitValue" => ProblemResults.RequestInvalid,
      "Subscription.DuplicateLimit" => ProblemResults.RequestInvalid,
      "Subscription.InvalidCurrencyCode" => ProblemResults.RequestInvalid,
      "Subscription.InvalidAmount" => ProblemResults.RequestInvalid,
      "Subscription.DuplicatePrice" => ProblemResults.RequestInvalid,
      "Subscription.PlanRetired" => ProblemResults.RequestInvalid,
      "Persistence.WriteFailure" => ProblemResults.WriteFailure,
      _ => ProblemResults.WriteFailure
    };
  }
}

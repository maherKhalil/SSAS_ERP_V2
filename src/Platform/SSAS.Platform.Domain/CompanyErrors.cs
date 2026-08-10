using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain;

public static class CompanyErrors
{
  public static readonly Error InvalidCompanyCode = new("Company.InvalidCode", "The company code is invalid.");
  public static readonly Error InvalidCompanyName = new("Company.InvalidName", "The company name is invalid.");
  public static readonly Error InvalidBaseCurrency = new("Company.InvalidBaseCurrency", "The base currency code is invalid.");
  public static readonly Error InvalidActor = new("Company.InvalidActor", "A trusted lifecycle actor is required.");
  public static readonly Error InvalidTransition = new("Company.InvalidTransition", "The company lifecycle transition is invalid.");
  public static readonly Error InvalidTransitionReason = new("Company.InvalidTransitionReason", "The lifecycle reason is invalid for this transition.");
  public static readonly Error CodeConflict = new("Company.CodeConflict", "The normalized company code already exists within the tenant.");
  public static readonly Error NotFound = new("Company.NotFound", "The company was not found.");
}

using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain;

public static class TenantLifecycleErrors
{
  public static readonly Error InvalidTenantCode = new("Tenant.InvalidCode", "The tenant code is invalid.");
  public static readonly Error InvalidTenantName = new("Tenant.InvalidName", "The tenant name is invalid.");
  public static readonly Error InvalidActor = new("Tenant.InvalidActor", "A trusted lifecycle actor is required.");
  public static readonly Error InvalidTransition = new("Tenant.InvalidTransition", "The tenant lifecycle transition is invalid.");
  public static readonly Error InvalidTransitionReason = new("Tenant.InvalidTransitionReason", "The lifecycle reason is invalid for this transition.");
  public static readonly Error CodeExists = new("Tenant.CodeExists", "The normalized tenant code already exists.");
  public static readonly Error NotFound = new("Tenant.NotFound", "The tenant was not found.");
  public static readonly Error Unauthorized = new("Tenant.Unauthorized", "A trusted Platform actor is required.");
}

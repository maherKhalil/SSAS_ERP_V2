using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.Tenants;

// ==================================================================================================
// EVERY TENANT ERROR THAT CAN REACH THE WIRE, MAPPED (T-155).
// ==================================================================================================
//
// **All twelve `Tenant.*` codes in `src/Platform` are here**, not only the ones today's handlers return —
// the surface was enumerated rather than sampled (`DEC-L-069`). A code added to the domain later still
// falls to `WriteFailure`, which is a 500: **wrong, but safe, and it is the fallback rather than a
// decision.**
//
// ---- ⚠ WHAT IS NOT HERE, AND IT IS THE THING TO CHECK BEFORE EXTENDING THIS.
//
// **Zero `TenantStorageErrors`.** The 117-code storage-administration surface does not reach tenant
// lifecycle at all — creating, suspending and archiving a tenant never touch the storage topology. **A
// future slice that adds storage administration must not extend this mapper**; it needs its own, or it
// will grow a 117-case switch that nobody can review.
public static class TenantApiErrorMapper
{
  public static readonly ApiError CodeConflict = new(409, "tenant.code_conflict");
  public static readonly ApiError NotFound = new(404, "tenant.not_found");
  public static readonly ApiError TransitionInvalid = new(409, "tenant.transition_invalid");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- MALFORMED INPUT. The caller can fix these by editing the request.
      "Tenant.InvalidCode" => ProblemResults.RequestInvalid,
      "Tenant.InvalidName" => ProblemResults.RequestInvalid,
      "Tenant.InvalidTransitionReason" => ProblemResults.RequestInvalid,
      "Tenant.ListFilterInvalid" => ProblemResults.RequestInvalid,
      "Tenant.Required" => ProblemResults.RequestInvalid,

      // ---- STATE REFUSES A WELL-FORMED REQUEST. Editing the body does not help.
      "Tenant.CodeExists" => CodeConflict,
      "Persistence.UniqueConstraint" => CodeConflict,
      "Tenant.InvalidTransition" => TransitionInvalid,
      "Persistence.ConcurrencyConflict" => ProblemResults.ConcurrencyConflict,

      // ---- ⚠ NOT-FOUND AND NOT-YOURS ARE THE SAME ANSWER, DELIBERATELY.
      //
      // `Tenant.NotFound` covers both "no such tenant" and "a tenant you may not reach". Separating them
      // would give any caller who can reach the route an oracle for which tenant ids exist — the same
      // reading `CompanyApiErrorMapper` takes, and the same one T-153 took on employee ids.
      "Tenant.NotFound" => NotFound,

      // ---- AUTHORISATION. `Mismatch` is here rather than under 404 because it means the caller reached a
      // tenant OTHER than the one their context names, which is a refusal to act, not a missing row.
      "Tenant.Unauthorized" => ProblemResults.Forbidden,
      "Tenant.InvalidActor" => ProblemResults.Forbidden,
      "Tenant.WriteDenied" => ProblemResults.Forbidden,
      "Tenant.Mismatch" => ProblemResults.Forbidden,
      "Authorization.Unauthorized" => ProblemResults.Forbidden,

      // ---- ⚠ CREATING A TENANT ISSUES ITS TRIAL SUBSCRIPTION, SO A `Subscription.*` CODE REACHES HERE.
      //
      // `CreateTenantCommandHandler` calls `ITrialSubscriptionIssuer` before saving, and abandons the
      // tenant if it fails — *"a tenant that cannot be given its trial is one that would be created
      // locked out."* **This was found by the mapping walk, not by reading the handler**: the code is
      // returned two calls away, in another namespace.
      //
      // 500 is the RIGHT answer — `TrialSubscriptionIssuer` says the seeded plan's absence is *"a
      // deployment defect rather than a caller's mistake"*, and no edit to the request can fix it.
      // **It is mapped rather than left to the fallback so that it reads as a decision**, and so that
      // changing it later is a change to a line rather than to a default.
      "Subscription.TrialPlanMissing" => ProblemResults.WriteFailure,

      "Persistence.WriteFailure" => ProblemResults.WriteFailure,
      _ => ProblemResults.WriteFailure
    };
  }
}

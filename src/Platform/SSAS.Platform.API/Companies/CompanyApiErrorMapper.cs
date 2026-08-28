using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.Companies;

// Feature-specific mapping of Company Domain/Application error codes to transport ApiError,
// using the exact codes from FP-005 api-contracts.md. Shared transport failures come from
// ProblemResults; only Company-specific codes live here.
public static class CompanyApiErrorMapper
{
  public static readonly ApiError CodeConflict = new(409, "company.code_conflict");
  public static readonly ApiError NotFound = new(404, "company.not_found");
  public static readonly ApiError TransitionInvalid = new(409, "company.transition_invalid");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);
    return error.Code switch
    {
      // Invalid caller input (value objects, lifecycle reason) -> 400 request.invalid.
      "Company.InvalidCode" => ProblemResults.RequestInvalid,
      "Company.InvalidName" => ProblemResults.RequestInvalid,
      "Company.InvalidBaseCurrency" => ProblemResults.RequestInvalid,
      "Company.InvalidTransitionReason" => ProblemResults.RequestInvalid,
      "Company.ListFilterInvalid" => ProblemResults.RequestInvalid,
      // Duplicate normalized code within the tenant.
      "Company.CodeConflict" => CodeConflict,
      "Persistence.UniqueConstraint" => CodeConflict,
      // Unknown or cross-tenant company.
      "Company.NotFound" => NotFound,
      // Transition not permitted from the current status.
      "Company.InvalidTransition" => TransitionInvalid,
      // Stale rowversion.
      "Persistence.ConcurrencyConflict" => ProblemResults.ConcurrencyConflict,
      // Trusted-context denials (unreachable on an authorized request; mapped defensively).
      "Company.InvalidActor" => ProblemResults.Forbidden,
      "Authorization.Unauthorized" => ProblemResults.Forbidden,
      // ---- T-093. `ApplicationExecutionContext.GetTenantActor` RETURNS THIS, AND EVERY TENANT-PLANE
      // ---- HANDLER FUNNELS THROUGH IT. Unmapped it fell to the default and answered 500 — an
      // authorization refusal reported as a server error, which tells the caller to retry something that
      // will never succeed and pages an operator for a working system.
      "Tenant.Unauthorized" => ProblemResults.Forbidden,
      // ---- EXPLICIT, THOUGH IT MATCHES THE DEFAULT (T-093, T-080's precedent).
      //
      // An arm that agrees with the default is a DECISION; the absence of one is an accident, and the two
      // are indistinguishable from the wire. Do not delete this as redundant: deleting it removes the
      // record that someone checked.
      "Persistence.WriteFailure" => ProblemResults.WriteFailure,
      // Any unexpected/unmapped error -> safe internal failure, never masked as client validation.
      _ => ProblemResults.WriteFailure
    };
  }
}

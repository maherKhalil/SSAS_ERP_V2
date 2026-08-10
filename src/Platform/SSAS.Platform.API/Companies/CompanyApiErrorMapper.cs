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
      // Persistence write failure and any unexpected/unmapped error -> safe internal failure,
      // never masked as client validation.
      _ => ProblemResults.WriteFailure
    };
  }
}

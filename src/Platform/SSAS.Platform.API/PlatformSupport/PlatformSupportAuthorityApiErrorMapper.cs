using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.PlatformSupport;

// Maps platform-support authority Domain/Application error codes to transport ApiError (Phase 4D).
// Shared transport failures come from ProblemResults; only platform-authority codes live here.
//
// Nothing here implements policy: notably there is NO last-admin mapping, because self-revoke, self-disable and
// removal of the final Administer are all ALLOWED (DEC-TEN-0026) and never produce an error to map.
public static class PlatformSupportAuthorityApiErrorMapper
{
  public static readonly ApiError PrincipalNotFound = new(404, "platform_support.principal_not_found");
  public static readonly ApiError AssignmentNotFound = new(404, "platform_support.assignment_not_found");
  public static readonly ApiError PrincipalConflict = new(409, "platform_support.principal_conflict");
  public static readonly ApiError PermissionConflict = new(409, "platform_support.permission_conflict");
  public static readonly ApiError TransitionInvalid = new(409, "platform_support.transition_invalid");
  public static readonly ApiError PrincipalDisabled = new(409, "platform_support.principal_disabled");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);
    return error.Code switch
    {
      // Caller input the catalog/domain rejects -> 400. The permission name is business input; an unknown or
      // tenant-scoped name is an invalid request, not an authorization failure.
      "PlatformSupport.IdentityRequired" => ProblemResults.RequestInvalid,
      "PlatformSupport.UnknownPermission" => ProblemResults.RequestInvalid,
      "PlatformSupport.TenantPermissionRejected" => ProblemResults.RequestInvalid,
      // Target state conflicts.
      "PlatformSupport.PrincipalAlreadyExists" => PrincipalConflict,
      "PlatformSupport.PermissionAlreadyAssigned" => PermissionConflict,
      "PlatformSupport.InvalidStatusTransition" => TransitionInvalid,
      "PlatformSupport.PrincipalDisabled" => PrincipalDisabled,
      // Unknown target / no active assignment to revoke.
      "PlatformSupport.PrincipalNotFound" => PrincipalNotFound,
      "PlatformSupport.PermissionAssignmentNotFound" => AssignmentNotFound,
      // Shared persistence semantics.
      "Persistence.ConcurrencyConflict" => ProblemResults.ConcurrencyConflict,
      "Persistence.UniqueConstraint" => PrincipalConflict,
      // Trusted-context denials (unreachable on an authorized request; mapped defensively, never as validation).
      "Authorization.Unauthorized" => ProblemResults.Forbidden,
      // ---- T-093. THE SAME REFUSAL THE OTHER THREE PLATFORM SITES ALSO MISSED.
      //
      // `ApplicationExecutionContext.GetTenantActor` returns it and every tenant-plane handler funnels
      // through that call. Unmapped it answered 500 here — a refusal reported as a server error.
      "Tenant.Unauthorized" => ProblemResults.Forbidden,

      // ---- TWO AUTHORITY REFUSALS (T-093b). NOT CALLER INPUT AND NOT A SERVER FAULT.
      //
      // An ineligible account and a principal with no usable platform authority are both statements about
      // WHO is asking, not about what they sent — so 403, alongside `Authorization.Unauthorized` above.
      // Under the default they answered 500, which told an operator a working system had failed.
      "PlatformSupport.AccountIneligible" => ProblemResults.Forbidden,
      "PlatformSupport.NoUsablePlatformAuthority" => ProblemResults.Forbidden,
      // ---- EXPLICIT, THOUGH IT MATCHES THE DEFAULT (T-093, T-080's precedent). An arm that agrees with
      // the default is a decision; its absence is an accident, and the wire cannot tell them apart.
      "Persistence.WriteFailure" => ProblemResults.WriteFailure,
      // Anything unmapped -> safe internal failure.
      _ => ProblemResults.WriteFailure
    };
  }
}

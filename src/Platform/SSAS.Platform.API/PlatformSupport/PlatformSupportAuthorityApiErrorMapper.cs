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
      // Persistence write failure and anything unmapped -> safe internal failure.
      _ => ProblemResults.WriteFailure
    };
  }
}

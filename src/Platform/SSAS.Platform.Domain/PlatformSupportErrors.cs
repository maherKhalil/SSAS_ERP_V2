using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain;

// Platform-plane (ADR-015 / DEC-TEN-0018) authority errors, kept separate from tenant-plane
// IdentityAccessErrors to preserve the plane boundary.
public static class PlatformSupportErrors
{
  public static readonly Error IdentityRequired =
    new("PlatformSupport.IdentityRequired", "A valid identity is required to register a platform-support principal.");

  public static readonly Error PrincipalAlreadyExists =
    new("PlatformSupport.PrincipalAlreadyExists", "A platform-support principal already exists for the identity.");

  public static readonly Error PrincipalNotFound =
    new("PlatformSupport.PrincipalNotFound", "The platform-support principal was not found.");

  public static readonly Error UnknownPermission =
    new("PlatformSupport.UnknownPermission", "The permission is not a known platform-support permission.");

  public static readonly Error TenantPermissionRejected =
    new("PlatformSupport.TenantPermissionRejected", "Platform-support principals cannot receive tenant-scoped permissions.");

  public static readonly Error DuplicatePermissionAssignment =
    new("PlatformSupport.PermissionAlreadyAssigned", "The platform-support permission is already assigned.");

  public static readonly Error PermissionAssignmentNotFound =
    new("PlatformSupport.PermissionAssignmentNotFound", "The active platform-support permission assignment was not found.");

  public static readonly Error InvalidStatusTransition =
    new("PlatformSupport.InvalidStatusTransition", "The platform-support principal status transition is invalid.");

  public static readonly Error PrincipalDisabled =
    new("PlatformSupport.PrincipalDisabled", "A disabled platform-support principal cannot receive new permissions.");
}

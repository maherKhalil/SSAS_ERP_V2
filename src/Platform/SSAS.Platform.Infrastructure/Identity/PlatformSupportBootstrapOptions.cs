using SSAS.Platform.Application.Permissions;

namespace SSAS.Platform.Infrastructure.Identity;

// Startup configuration for platform-support genesis/recovery bootstrap (ADR-016 / DEC-TEN-0019/0021).
// Bootstrap is opt-in: with no configured Subjects it performs no persistence access and stays inert.
// Multiple candidate subjects are permitted; selection among eligible candidates is deterministic
// (ordinal first-eligible) and lives in the bootstrap orchestrator, never here.
public sealed class PlatformSupportBootstrapOptions
{
  public const string SectionName = "PlatformSupport:Bootstrap";

  // Authentication subjects of the identities allowed to seed the first usable platform authority.
  // Matched against Identity.Subject with ordinal equality (no normalization), mirroring subject lookup.
  public string[] Subjects { get; set; } = [];

  // The initial platform-support grant set for the established principal. Configurable, but must include
  // Platform.Support.Administer and may only contain catalog-known PermissionScope.PlatformSupport names.
  public string[] InitialPermissions { get; set; } = [PlatformPermissionNames.AdministerPlatformSupport];
}

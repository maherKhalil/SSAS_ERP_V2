using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.Identity;

// Fail-closed startup validation for platform-support bootstrap configuration (ADR-016 / DEC-TEN-0021).
// Scope is resolved from the code-owned catalog, never trusted from configuration text. A misconfigured
// grant set (empty, containing an unknown/tenant-scoped name, or missing Platform.Support.Administer)
// halts startup rather than seeding a principal with wrong or unusable authority.
public sealed class PlatformSupportBootstrapOptionsValidator(IPermissionCatalog permissionCatalog)
  : IValidateOptions<PlatformSupportBootstrapOptions>
{
  public ValidateOptionsResult Validate(string? name, PlatformSupportBootstrapOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    // Subjects are optional (bootstrap is opt-in) but, when present, must be exact and unique so
    // ordinal subject matching and deterministic ordinal selection are unambiguous.
    var subjects = options.Subjects ?? [];
    foreach (var subject in subjects)
    {
      if (string.IsNullOrWhiteSpace(subject) ||
        subject.Length > 256 ||
        !string.Equals(subject, subject.Trim(), StringComparison.Ordinal))
      {
        return ValidateOptionsResult.Fail(
          "Platform-support bootstrap subjects must be exact, nonblank, and within 256 characters.");
      }
    }

    if (subjects.Distinct(StringComparer.Ordinal).Count() != subjects.Length)
    {
      return ValidateOptionsResult.Fail("Platform-support bootstrap subjects must not contain duplicates.");
    }

    // The initial grant set is always validated: it has a valid default (Administer only), so an empty
    // or malformed set is an explicit misconfiguration even when no subjects are configured.
    var permissions = options.InitialPermissions ?? [];
    if (permissions.Length == 0)
    {
      return ValidateOptionsResult.Fail("Platform-support bootstrap initial permission set cannot be empty.");
    }

    if (permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length)
    {
      return ValidateOptionsResult.Fail("Platform-support bootstrap initial permissions must not contain duplicates.");
    }

    foreach (var permission in permissions)
    {
      if (!permissionCatalog.TryGet(permission, out var definition) ||
        definition.Scope != PermissionScope.PlatformSupport)
      {
        return ValidateOptionsResult.Fail(
          $"Platform-support bootstrap permission '{permission}' must be a known PlatformSupport permission.");
      }
    }

    if (!permissions.Contains(PlatformPermissionNames.AdministerPlatformSupport, StringComparer.Ordinal))
    {
      return ValidateOptionsResult.Fail(
        $"Platform-support bootstrap initial permission set must include {PlatformPermissionNames.AdministerPlatformSupport}.");
    }

    return ValidateOptionsResult.Success;
  }
}

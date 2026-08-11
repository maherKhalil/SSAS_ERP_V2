namespace SSAS.Platform.Application.PlatformSupport;

// Live, persistence-backed evaluation of "usable platform authority" (ADR-016 / DEC-TEN-0019).
// Usable authority exists iff at least one Active PlatformSupportPrincipal is anchored to an
// authentication-eligible account and holds at least one active assignment whose permission is a
// current catalog-known PermissionScope.PlatformSupport permission. It is never inferred from
// configuration, a cached flag, a bare principal row, or corrupt/unknown/revoked assignments.
public interface IPlatformSupportAuthorityStateReadService
{
  Task<bool> HasUsablePlatformAuthorityAsync(CancellationToken cancellationToken = default);
}

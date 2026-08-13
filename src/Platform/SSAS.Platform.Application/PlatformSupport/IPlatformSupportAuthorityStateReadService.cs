namespace SSAS.Platform.Application.PlatformSupport;

// Live, persistence-backed evaluation of "usable platform authority" (ADR-016 / DEC-TEN-0019).
// Usable authority exists iff at least one Active PlatformSupportPrincipal is anchored to an
// authentication-eligible account and holds at least one active assignment whose permission is a
// current catalog-known PermissionScope.PlatformSupport permission. It is never inferred from
// configuration, a cached flag, a bare principal row, or corrupt/unknown/revoked assignments.
public interface IPlatformSupportAuthorityStateReadService
{
  Task<bool> HasUsablePlatformAuthorityAsync(CancellationToken cancellationToken = default);

  // Live evaluation of "usable platform ADMINISTRATIVE authority" (DEC-TEN-0026). Deliberately distinct from
  // the general predicate above: it is satisfied only when at least one Active, authentication-eligible
  // principal holds an active assignment for exactly Platform.Support.Administer, and the current catalog
  // still recognises that permission as PermissionScope.PlatformSupport.
  //
  // The two are NOT interchangeable. A principal left holding only Platform.Tenants.View after its Administer
  // grant is revoked still has general usable authority (nobody is locked out of the platform plane) but has
  // no administrative authority (nobody can Register/Grant/Revoke/Disable/Re-enable), which is precisely the
  // state administrative recovery has to detect.
  Task<bool> HasUsablePlatformAdministrativeAuthorityAsync(CancellationToken cancellationToken = default);
}

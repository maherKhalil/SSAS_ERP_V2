namespace SSAS.Platform.Application.PlatformSupport;

// Cross-candidate serialization for genesis/recovery convergence (DEC-TEN-0019, refined by DEC-TEN-0026).
//
// Convergence cannot rest on PlatformSupportPrincipal.IdentityId uniqueness alone: that only rejects two
// principals for the SAME identity. With two eligible configured subjects A and B, a worker that enumerates
// after another has already committed A would skip A, select B, and establish a second Administer-bearing
// recovery principal — no uniqueness conflict ever occurs. Recovery workers must therefore contend on ONE
// resource that is common to every candidate before they re-evaluate authority and choose a subject.
//
// Acquisition is transaction-scoped: the caller must already have an open transaction, and the serialization
// is released automatically when that transaction commits, rolls back, or is disposed.
public interface IPlatformSupportRecoverySerializer
{
  // Returns false when serialization could not be acquired within its bounded wait, so the caller fails
  // closed rather than recovering unserialized.
  Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default);
}

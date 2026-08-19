using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// ==================================================================================================
// A WRITE WAS REFUSED BECAUSE THE CALLER MAY NOT MAKE IT (FP-006C5, ADR-023, ADR-025).
// ==================================================================================================
//
// The company and branch write boundaries re-ask their authorizers at save time and refuse when the answer
// is no. Until now that refusal was raised as TenantStorageUnavailableException, and the two conditions are
// not remotely the same thing:
//
//   * "Tenant storage is unavailable" is an INFRASTRUCTURE outage. Nobody can write. It should page someone,
//     retrying may help, and it belongs in the 5xx family.
//   * "You may not write here" is an AUTHORIZATION outcome. The system is perfectly healthy, the answer will
//     not change on retry, and it belongs in the 4xx family.
//
// Collapsing them meant every company- or branch-denied write looked like an outage: alarming to operators,
// misleading to callers, and wrong for retry policy. Any mapper keying on the exception TYPE — which is the
// natural thing to do — got it backwards, and the fact that the carried Error code was correct did not help
// anyone who never looked at it.
//
// ---- IT IS STILL AN EXCEPTION, AND STILL THROWN FROM THE SAME PLACE.
//
// The refusal happens inside SaveChangesAsync, below the point where a Result can be returned: EF's pipeline
// has no channel for "refused" other than throwing. Throwing also guarantees the transaction does not
// commit, which is the property that actually matters. What changes here is only that the refusal now says
// what it is.
//
// ---- THE CARRIED ERROR IS THE AUTHORIZER'S OWN.
//
// Company.*, Branch.* or BranchTransfer.* — already written to be safe to surface: none names a company, a
// branch, a tenant or a user the caller was not permitted to see, so the concealment the resolvers provide
// survives all the way to the HTTP boundary.
public sealed class TenantWriteAuthorizationException : Exception
{
  public TenantWriteAuthorizationException(Error error)
    : base($"The write was refused by the tenant write boundary: {error.Code}.")
  {
    Error = error;
  }

  public TenantWriteAuthorizationException()
    : this(new Error("Tenant.WriteDenied", "The write was refused by the tenant write boundary."))
  {
  }

  public TenantWriteAuthorizationException(string message)
    : base(message)
  {
    Error = new Error("Tenant.WriteDenied", message);
  }

  public TenantWriteAuthorizationException(string message, Exception innerException)
    : base(message, innerException)
  {
    Error = new Error("Tenant.WriteDenied", message);
  }

  public Error Error { get; }
}

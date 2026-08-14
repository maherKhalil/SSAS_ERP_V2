using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Raised when tenant ERP persistence is requested but no trusted route to a tenant database can be
// established (ADR-017 fail-closed routing).
//
// It is deliberately an EXCEPTION rather than an empty result. ADR-017 records that misrouting is
// asymmetric: a filtered read returns zero rows rather than an error, so "storage unavailable" and "this
// tenant has no data" would be indistinguishable to every caller — silent, and nothing would page anyone.
// Failing loudly before any query is issued is what keeps those two states apart.
//
// The carried Error is a routing error code (TenantStorage.*). Those messages are written to be safe to
// surface: none contains a connection string, credential, host or endpoint.
public sealed class TenantStorageUnavailableException : Exception
{
  public TenantStorageUnavailableException(Error error)
    : base($"Tenant storage is unavailable: {error.Code}.")
  {
    Error = error;
  }

  public TenantStorageUnavailableException()
    : this(new Error("TenantStorage.Unavailable", "Tenant storage is unavailable."))
  {
  }

  public TenantStorageUnavailableException(string message)
    : base(message)
  {
    Error = new Error("TenantStorage.Unavailable", message);
  }

  public TenantStorageUnavailableException(string message, Exception innerException)
    : base(message, innerException)
  {
    Error = new Error("TenantStorage.Unavailable", message);
  }

  public Error Error { get; }
}

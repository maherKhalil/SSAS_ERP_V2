using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Trusted BACKUP AUTHORITY connection construction (ADR-022 §11).
//
// A SEPARATE factory from ITenantDatabaseConnectionFactory, deliberately, rather than an overload on it.
// The two answer the same question — "how do I reach this physical database" — with DIFFERENT credentials,
// and the whole point of ADR-022's three-authority model is that runtime, migration and backup identities
// stay apart. An overload would make reusing the request-serving credential for `BACKUP DATABASE` a one-word
// change, and that credential must never hold backup privileges: `BACKUP` writes a complete copy of the
// database to a file, so granting it to the identity serving web requests turns any application-level
// compromise into full data exfiltration.
//
// What it REUSES is the trust MODEL, not the credentials: the same exact-ordinal ServerKey lookup, the same
// CustomerManaged refusal, the same `InitialCatalog` construction, and the same fail-closed behaviour on an
// unknown key.
public interface ITenantDatabaseBackupConnectionFactory
{
  // Creates (but does not open) a privileged backup connection to one physical database. Fails closed when
  // the ServerKey has no BACKUP configuration — it never falls back to the runtime `Servers` entry, because
  // that fallback would silently restore the credential reuse this separation exists to prevent.
  Result<SqlConnection> Create(TenantDatabaseConnectionTarget target);
}

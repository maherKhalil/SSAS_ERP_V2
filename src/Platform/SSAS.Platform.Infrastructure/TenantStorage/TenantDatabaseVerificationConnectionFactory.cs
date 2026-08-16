using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Trusted RestoreServerKey -> privileged RESTORE VERIFICATION connection (ADR-022 §17, v1.2).
//
// THE MOST PRIVILEGED CONNECTION IN THIS CAPABILITY, and the one with the narrowest permitted reach. The
// identity behind it creates databases, forces them single-user and drops them; the whole point of the
// dedicated topology is that it holds those rights on a server that carries no authoritative tenant data.
//
// Two rules are enforced here rather than trusted to callers:
//
//   1. THE TARGET IS RESOLVED FROM A CLOSED CONFIGURATION KEY. No caller-supplied server, connection string
//      or endpoint reaches this type at all — the parameter is a key, and an unknown key fails.
//   2. THE TARGET MUST NOT BE THE SERVER HOSTING THE AUTHORITATIVE DATABASE unless the deployment has
//      explicitly declared itself non-production. This is compliance rule 32, and it is checked at the only
//      layer that would actually open the socket.
//
// FAILS CLOSED IN EVERY DIRECTION. Absent key, absent connection string, or a target that is not isolated
// all refuse. There is deliberately no path — no fallback, no default, no near-match — by which a failure to
// resolve the verification target results in connecting to the tenant database's own server, because that
// fallback is the exact outcome the topology decision exists to prevent (compliance rule 44).
public interface ITenantDatabaseVerificationConnectionFactory
{
  // Opens against the verification instance's `master`: a restore into a database that does not exist yet
  // cannot connect to that database, and no restore ever connects to the source tenant database.
  Result<SqlConnection> Create(TenantDatabaseVerificationTarget target);

  // Opens against a RESTORED verification database, for the post-restore probes (ADR-022 §17, TS-Backup D7).
  //
  // Same trust boundary and the same isolation rule as above — this only changes which catalog on the
  // verification instance is addressed. The database name must come from the durable verification run, never
  // from a caller, and it is re-validated against the reserved vocabulary here because this is the last layer
  // before a connection is opened to it.
  Result<SqlConnection> CreateForVerificationDatabase(
    TenantDatabaseVerificationTarget target,
    string verificationDatabaseName);
}

// What the factory needs to decide whether a verification connection is permitted.
//
// `SourceServerKey` is carried ONLY so the isolation rule can be checked. It never selects anything.
public sealed record TenantDatabaseVerificationTarget(string RestoreServerKey, string SourceServerKey);

public sealed class TenantDatabaseVerificationConnectionFactory(
  IOptions<TenantStorageOptions> storageOptions,
  IOptions<TenantDatabaseRestoreVerificationOptions> verificationOptions)
  : ITenantDatabaseVerificationConnectionFactory
{
  // The verification instance's entry point. A RESTORE that creates a database is issued from `master`,
  // never from the database being created and never from the source database.
  private const string VerificationCatalog = "master";

  public Result<SqlConnection> CreateForVerificationDatabase(
    TenantDatabaseVerificationTarget target,
    string verificationDatabaseName)
  {
    // REFUSED UNLESS THE NAME IS INSIDE THE RESERVED VOCABULARY. Probing connects to a database by name, and
    // a name outside the platform's own generated namespace is by definition not one this capability created
    // — so it must not be opened, whatever the caller believes (ADR-022 §17).
    if (!TenantDatabaseVerificationNaming.IsVerificationDatabaseName(verificationDatabaseName))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.RestoreVerificationTargetNameNotSafe);
    }

    return Create(target, verificationDatabaseName);
  }

  public Result<SqlConnection> Create(TenantDatabaseVerificationTarget target) =>
    Create(target, VerificationCatalog);

  private Result<SqlConnection> Create(TenantDatabaseVerificationTarget target, string catalog)
  {
    ArgumentNullException.ThrowIfNull(target);

    var options = verificationOptions.Value;
    if (!options.Enabled)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.RestoreVerificationNotConfigured);
    }

    if (string.IsNullOrWhiteSpace(target.RestoreServerKey))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.RestoreVerificationServerNotConfigured);
    }

    // ISOLATION, checked before anything is resolved. A verification target equal to the server hosting the
    // authoritative database is refused unless the deployment has explicitly opted into same-instance
    // verification, which is a non-production declaration and never a fallback.
    if (!options.AllowSameInstanceVerification &&
      string.Equals(target.RestoreServerKey, target.SourceServerKey, StringComparison.Ordinal))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.RestoreVerificationTargetNotIsolated);
    }

    // Exact, ordinal lookup against the VERIFICATION configuration only. No default entry, no
    // case-insensitive near-match, and explicitly NO fallback to `BackupServers` or `Servers` — falling
    // back would reintroduce both the credential reuse and the topology violation this separation exists to
    // prevent.
    if (!storageOptions.Value.VerificationServers.TryGetValue(target.RestoreServerKey, out var server) ||
      string.IsNullOrWhiteSpace(server.ConnectionString))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.RestoreVerificationServerNotConfigured);
    }

    var builder = new SqlConnectionStringBuilder(server.ConnectionString)
    {
      InitialCatalog = catalog,

      // POOLING DISABLED, for the same reason the backup connection disables it: a restore verification will
      // hold session-scoped state and long-running operations, and returning such a connection to a pool
      // rests correctness on `sp_reset_connection` semantics rather than on the session genuinely ending.
      // These connections are few and individually long-lived, so the cost is negligible.
      Pooling = false
    };

    return Result.Success(new SqlConnection(builder.ConnectionString));
  }
}

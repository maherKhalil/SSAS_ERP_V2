using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Trusted ServerKey -> privileged BACKUP connection (ADR-022 §11).
//
// Mirrors TenantDatabaseConnectionFactory's trust rules exactly — unknown key fails, CustomerManaged is
// refused, the catalog goes through SqlConnectionStringBuilder and is never concatenated — while resolving
// credentials from a SEPARATE configuration section so the runtime identity never gains backup privileges.
public sealed class TenantDatabaseBackupConnectionFactory(IOptions<TenantStorageOptions> optionsAccessor)
  : ITenantDatabaseBackupConnectionFactory
{
  public Result<SqlConnection> Create(TenantDatabaseConnectionTarget target)
  {
    ArgumentNullException.ThrowIfNull(target);

    // Defence in depth. The executor refuses CustomerManaged before reaching here, but this layer is what
    // would actually open a socket to a customer's server, so it refuses independently (ADR-021).
    if (target.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.UnsupportedHostingMode);
    }

    if (string.IsNullOrWhiteSpace(target.DatabaseName) ||
      target.DatabaseName.Length > TenantDatabase.DatabaseNameMaximumLength)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.DatabaseNameInvalid);
    }

    // Exact, ordinal lookup against the BACKUP configuration only. No default entry, no case-insensitive
    // near-match, and explicitly NO fallback to the runtime `Servers` entry — a server with routing
    // configured but no backup identity is a server the platform may not back up.
    if (string.IsNullOrWhiteSpace(target.ServerKey) ||
      !optionsAccessor.Value.BackupServers.TryGetValue(target.ServerKey, out var server) ||
      string.IsNullOrWhiteSpace(server.ConnectionString))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.BackupServerNotConfigured);
    }

    var builder = new SqlConnectionStringBuilder(server.ConnectionString)
    {
      InitialCatalog = target.DatabaseName,

      // POOLING IS DISABLED, and this is a correctness decision rather than a performance one.
      //
      // Ownership of a backup is a SESSION-scoped sp_getapplock, which SQL Server releases when the SESSION
      // ends. Closing a pooled connection does not end the session — it returns the physical connection to
      // the pool and resets it via sp_reset_connection. Resting a durability lock's lifetime on reset
      // semantics is exactly the kind of inherited assumption ADR-022 was revised to stop making, so the
      // backup connection is non-pooled and its close genuinely terminates the session.
      //
      // The cost is negligible: these connections are few and individually long-lived.
      Pooling = false
    };

    return Result.Success(new SqlConnection(builder.ConnectionString));
  }
}

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Trusted ServerKey -> connection construction (ADR-017).
//
// THE CRITICAL INVARIANT: an unknown ServerKey FAILS. It must never resolve to the Platform connection, a
// default server, or the first configured entry. A silent fallback here would open a connection to the
// wrong physical database while every upstream layer believed routing had succeeded — the precise way
// tenant traffic gets cross-routed.
//
// The catalog is applied through SqlConnectionStringBuilder.InitialCatalog, which quotes and escapes
// correctly. The database name is never concatenated into a connection string and never interpolated into
// SQL. Trusted authentication and encryption settings from the configured base string are preserved, since
// only the catalog is overridden.
public sealed class TenantDatabaseConnectionFactory(IOptions<TenantStorageOptions> optionsAccessor)
  : ITenantDatabaseConnectionFactory
{
  public Result<SqlConnection> Create(TenantDatabaseRoute route)
  {
    ArgumentNullException.ThrowIfNull(route);

    // Defence in depth: the resolver already rejects customer-managed routing, but this layer is what
    // would actually open a socket, so it refuses independently rather than trusting an upstream check.
    if (route.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.UnsupportedHostingMode);
    }

    if (string.IsNullOrWhiteSpace(route.DatabaseName) ||
      route.DatabaseName.Length > TenantDatabase.DatabaseNameMaximumLength)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.DatabaseNameInvalid);
    }

    // Exact, ordinal lookup. No default entry, no case-insensitive near-match, no "first configured
    // server" — absence is a failure.
    if (string.IsNullOrWhiteSpace(route.ServerKey) ||
      !optionsAccessor.Value.Servers.TryGetValue(route.ServerKey, out var server) ||
      string.IsNullOrWhiteSpace(server.ConnectionString))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.ServerKeyNotConfigured);
    }

    var builder = new SqlConnectionStringBuilder(server.ConnectionString)
    {
      InitialCatalog = route.DatabaseName
    };

    return Result.Success(new SqlConnection(builder.ConnectionString));
  }
}

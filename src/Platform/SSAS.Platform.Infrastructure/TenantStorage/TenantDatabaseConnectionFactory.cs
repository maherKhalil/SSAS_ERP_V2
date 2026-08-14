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

    return Create(new TenantDatabaseConnectionTarget(route.ServerKey, route.DatabaseName, route.HostingMode));
  }

  // The ONE trusted construction path. The route overload delegates here rather than duplicating the
  // checks, so schema health and migration cannot reach a database that request routing would refuse.
  public Result<SqlConnection> Create(TenantDatabaseConnectionTarget target)
  {
    ArgumentNullException.ThrowIfNull(target);

    // Defence in depth: the resolver already rejects customer-managed routing, but this layer is what
    // would actually open a socket, so it refuses independently rather than trusting an upstream check.
    if (target.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.UnsupportedHostingMode);
    }

    if (string.IsNullOrWhiteSpace(target.DatabaseName) ||
      target.DatabaseName.Length > TenantDatabase.DatabaseNameMaximumLength)
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.DatabaseNameInvalid);
    }

    // Exact, ordinal lookup. No default entry, no case-insensitive near-match, no "first configured
    // server" — absence is a failure.
    if (string.IsNullOrWhiteSpace(target.ServerKey) ||
      !optionsAccessor.Value.Servers.TryGetValue(target.ServerKey, out var server) ||
      string.IsNullOrWhiteSpace(server.ConnectionString))
    {
      return Result.Failure<SqlConnection>(TenantStorageErrors.ServerKeyNotConfigured);
    }

    var builder = new SqlConnectionStringBuilder(server.ConnectionString)
    {
      InitialCatalog = target.DatabaseName
    };

    return Result.Success(new SqlConnection(builder.ConnectionString));
  }
}

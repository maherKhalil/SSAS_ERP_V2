using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Startup validation for tenant-storage configuration (ADR-017).
//
// This exists because of a risk change, not a style preference. While routing was inert, a missing or
// malformed `TenantStorage:Servers` entry cost nothing: no code opened a tenant connection. Now that
// TenantDbContext routes through it, the same misconfiguration would fail every tenant request at first
// use — one tenant at a time, in production, long after deployment. Validating at startup converts that
// into a deployment failure with a precise message.
//
// It validates the SHAPE of trusted configuration only. It deliberately does not connect, does not scan
// the tenant population, and does not verify every tenant's assignment: per-tenant reachability belongs to
// the schema-health slice (ADR-018), and a startup that scanned an arbitrarily large tenant table would be
// a self-inflicted outage on the first large deployment.
//
// NO CREDENTIAL MATERIAL IS EVER PLACED IN A MESSAGE. Failures name the offending ServerKey and the
// configuration path, never the connection string, host or password — validation messages are logged, and
// a leak here would be worse than the misconfiguration it reports.
public sealed class TenantStorageOptionsValidator : IValidateOptions<TenantStorageOptions>
{
  public ValidateOptionsResult Validate(string? name, TenantStorageOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    var failures = new List<string>();
    var section = TenantStorageOptions.SectionName;

    if (string.IsNullOrWhiteSpace(options.DefaultServerKey))
    {
      failures.Add($"{section}:{nameof(TenantStorageOptions.DefaultServerKey)} must not be blank.");
    }

    foreach (var (serverKey, server) in options.Servers)
    {
      if (string.IsNullOrWhiteSpace(serverKey))
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.Servers)} contains a blank server key.");
        continue;
      }

      if (server is null || string.IsNullOrWhiteSpace(server.ConnectionString))
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.Servers)}:{serverKey}:{nameof(TenantStorageServerOptions.ConnectionString)} must not be blank.");
        continue;
      }

      // A malformed connection string would otherwise surface as a request-time exception from the
      // connection factory. Parsing it here is the cheapest way to find it at deploy time; the parsed
      // value is discarded and never logged.
      try
      {
        _ = new SqlConnectionStringBuilder(server.ConnectionString);
      }
      // The exception text is dropped because the message built below is BETTER: it names the exact
      // configuration path (`section:BackupServers:key:ConnectionString`) that has to be edited, which the
      // parser's own message does not. Keeping both would put the raw connection string into a startup
      // failure that gets pasted into tickets.
      catch (ArgumentException)
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.Servers)}:{serverKey}:{nameof(TenantStorageServerOptions.ConnectionString)} is not a valid SQL Server connection string.");
      }
    }

    // ServerKey lookup is ordinal, so two keys differing only in case are two distinct servers to the
    // factory but almost certainly one server to whoever wrote the configuration. Rejecting the pair is
    // safer than silently honouring the ordinal reading of an obvious typo.
    var caseCollisions = options.Servers.Keys
      .Where(key => !string.IsNullOrWhiteSpace(key))
      .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
      .Where(group => group.Count() > 1)
      .Select(group => group.Key)
      .ToArray();
    foreach (var collision in caseCollisions)
    {
      failures.Add(
        $"{section}:{nameof(TenantStorageOptions.Servers)} contains keys differing only by case ('{collision}'); " +
        "server key lookup is ordinal and case-sensitive.");
    }

    // ---- Backup authority and destinations (ADR-022 §11). Validated with the same shape-only discipline:
    // no connection is opened, no directory is touched, and NO CREDENTIAL OR PATH APPEARS IN A MESSAGE.
    foreach (var (serverKey, server) in options.BackupServers)
    {
      if (string.IsNullOrWhiteSpace(serverKey))
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.BackupServers)} contains a blank server key.");
        continue;
      }

      if (server is null || string.IsNullOrWhiteSpace(server.ConnectionString))
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.BackupServers)}:{serverKey}:{nameof(TenantStorageServerOptions.ConnectionString)} must not be blank.");
        continue;
      }

      try
      {
        _ = new SqlConnectionStringBuilder(server.ConnectionString);
      }
      // Same trade as the connection-string check above: the built message names the configuration
      // path to edit, and the parser's own message would carry the connection string into a log.
      catch (ArgumentException)
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.BackupServers)}:{serverKey}:{nameof(TenantStorageServerOptions.ConnectionString)} is not a valid SQL Server connection string.");
      }
    }

    foreach (var (destinationKey, destination) in options.BackupDestinations)
    {
      if (string.IsNullOrWhiteSpace(destinationKey))
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.BackupDestinations)} contains a blank destination key.");
        continue;
      }

      // The directory is validated for SHAPE only. Whether the SQL SERVER SERVICE IDENTITY can write there
      // is deliberately NOT checked here: this process runs as a different account, so a check from here
      // would prove nothing and could pass while every backup fails with OS error 5.
      if (destination is null || string.IsNullOrWhiteSpace(destination.DirectoryPath))
      {
        failures.Add($"{section}:{nameof(TenantStorageOptions.BackupDestinations)}:{destinationKey}:{nameof(TenantStorageBackupDestinationOptions.DirectoryPath)} must not be blank.");
      }
    }

    // When servers ARE configured, the default key must be one of them. This catches the most likely real
    // misconfiguration — a typo between DefaultServerKey and the Servers map — which would otherwise leave
    // the bootstrap stamping a ServerKey that routing can never resolve.
    if (options.Servers.Count > 0 &&
      !string.IsNullOrWhiteSpace(options.DefaultServerKey) &&
      !options.Servers.ContainsKey(options.DefaultServerKey))
    {
      failures.Add(
        $"{section}:{nameof(TenantStorageOptions.DefaultServerKey)} ('{options.DefaultServerKey}') has no matching entry " +
        $"in {section}:{nameof(TenantStorageOptions.Servers)}.");
    }

    // An EMPTY server map is deliberately not a failure. ADR-017 makes platform availability independent of
    // tenant storage: authentication, tenant selection and platform surfaces must start and serve even when
    // no tenant ERP database is configured or reachable. A deployment that serves tenant ERP data needs at
    // least one entry, and gets a fail-closed routing error per request until it has one — which is visible
    // and safe, whereas refusing to start would take down login too.
    return failures.Count == 0
      ? ValidateOptionsResult.Success
      : ValidateOptionsResult.Fail(failures);
  }
}

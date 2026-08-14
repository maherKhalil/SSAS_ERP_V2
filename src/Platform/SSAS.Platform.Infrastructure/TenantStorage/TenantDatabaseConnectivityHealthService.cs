using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Connectivity health for physical tenant databases (ADR-018).
//
// It opens a connection through the SAME trusted ServerKey factory routing uses — there is no second,
// weaker path to a tenant database — runs a trivial probe, and records the connectivity dimension. Nothing
// else. It never reads migration history and never writes schema state, which is the whole point: a
// connectivity failure previously erased a perfectly good schema observation it had never even looked at.
public sealed class TenantDatabaseConnectivityHealthService(
  ITenantDatabaseRegistryReadRepository readRepository,
  ITenantDatabaseConnectionFactory connectionFactory,
  ITenantDatabaseHealthWriter healthWriter) : ITenantDatabaseConnectivityHealthService
{
  private const string ConnectivityActor = "tenant-storage-connectivity";

  private const int PageSize = 50;

  // Deliberately trivial. ADR-018: "a trivial probe is the whole contract" — an expensive or business
  // query here would turn a diagnostic into load.
  private const string ProbeCommand = "SELECT 1";

  public async Task<Result<TenantDatabaseConnectivityResult>> CheckAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default)
  {
    var page = await readRepository.ListPhysicalDatabasesAsync(tenantDatabaseId - 1, 1, cancellationToken);
    var descriptor = page.FirstOrDefault(item => item.TenantDatabaseId == tenantDatabaseId);
    if (descriptor is null)
    {
      return Result.Failure<TenantDatabaseConnectivityResult>(TenantStorageErrors.TenantDatabaseNotReady);
    }

    var result = await ProbeAsync(descriptor, cancellationToken);
    await PersistAsync(result, cancellationToken);
    return Result.Success(result);
  }

  public async Task<Result<TenantDatabaseConnectivitySweepSummary>> SweepAsync(
    int maximumDatabases,
    CancellationToken cancellationToken = default)
  {
    var discovered = 0;
    var healthy = 0;
    var unreachable = 0;
    var authenticationFailed = 0;
    var notVerifiable = 0;
    var afterId = 0L;

    while (discovered < maximumDatabases)
    {
      var page = await readRepository.ListPhysicalDatabasesAsync(
        afterId, Math.Min(PageSize, maximumDatabases - discovered), cancellationToken);
      if (page.Count == 0)
      {
        break;
      }

      foreach (var descriptor in page)
      {
        cancellationToken.ThrowIfCancellationRequested();
        discovered++;
        afterId = descriptor.TenantDatabaseId;

        var result = await ProbeAsync(descriptor, cancellationToken);
        await PersistAsync(result, cancellationToken);

        if (!result.Verified)
        {
          notVerifiable++;
          continue;
        }

        switch (result.Status)
        {
          case TenantDatabaseConnectivityStatus.Healthy: healthy++; break;
          case TenantDatabaseConnectivityStatus.AuthenticationFailed: authenticationFailed++; break;
          default: unreachable++; break;
        }
      }
    }

    return Result.Success(new TenantDatabaseConnectivitySweepSummary(
      discovered, healthy, unreachable, authenticationFailed, notVerifiable));
  }

  private async Task<TenantDatabaseConnectivityResult> ProbeAsync(
    TenantDatabaseDescriptor descriptor,
    CancellationToken cancellationToken)
  {
    // CustomerManaged has no supported runtime connectivity path (ADR-021). Nothing is attempted, and the
    // result says so: unverifiable, never Healthy. Reporting verified health for a database we never
    // contacted would be the most misleading thing this service could do.
    if (descriptor.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return new TenantDatabaseConnectivityResult(
        descriptor.TenantDatabaseId, TenantDatabaseConnectivityStatus.Unknown, Verified: false);
    }

    var connection = connectionFactory.Create(new TenantDatabaseConnectionTarget(
      descriptor.ServerKey, descriptor.DatabaseName, descriptor.HostingMode));
    if (connection.IsFailure)
    {
      // An unconfigured ServerKey means we cannot reach it — an operational unreachability, and never a
      // fallback to some other server.
      return new TenantDatabaseConnectivityResult(
        descriptor.TenantDatabaseId, TenantDatabaseConnectivityStatus.Unreachable, Verified: true);
    }

    await using var sqlConnection = connection.Value;
    try
    {
      await sqlConnection.OpenAsync(cancellationToken);
      await using var command = sqlConnection.CreateCommand();
      command.CommandText = ProbeCommand;
      await command.ExecuteScalarAsync(cancellationToken);

      return new TenantDatabaseConnectivityResult(
        descriptor.TenantDatabaseId, TenantDatabaseConnectivityStatus.Healthy, Verified: true);
    }
    catch (SqlException exception)
    {
      // A credential or permission problem is a different operator task from an unreachable host, so the
      // two are never collapsed into one status.
      return new TenantDatabaseConnectivityResult(
        descriptor.TenantDatabaseId,
        IsAuthenticationFailure(exception)
          ? TenantDatabaseConnectivityStatus.AuthenticationFailed
          : TenantDatabaseConnectivityStatus.Unreachable,
        Verified: true);
    }
  }

  // Login failed (18456), cannot open database (4060), login from untrusted domain (18452).
  internal static bool IsAuthenticationFailure(SqlException exception) =>
    exception.Errors.OfType<SqlError>().Any(error => error.Number is 18456 or 4060 or 18452);

  private Task PersistAsync(TenantDatabaseConnectivityResult result, CancellationToken cancellationToken) =>
    // Only a database we actually probed gets a connectivity verdict written. And note what is NOT here:
    // no schema write of any kind, whatever the outcome.
    result.Verified
      ? healthWriter.RecordConnectivityAsync(
        result.TenantDatabaseId, result.Status, ConnectivityActor, cancellationToken)
      : Task.CompletedTask;
}

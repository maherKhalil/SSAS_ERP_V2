using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// READ-ONLY schema health for physical tenant databases (ADR-018).
//
// It compares each database's ACTUAL `tenant.__EFMigrationsHistory` against the deployed Tenant migration
// catalog. It never migrates: a diagnostic that could change schema would make every health run a
// deployment.
//
// The comparison is over FULL HISTORIES, not endpoints. Comparing only "latest applied" against "latest
// known" cannot distinguish a database that is merely behind from one whose lineage diverged, and those
// two require opposite responses — apply migrations, versus stop and investigate.
public sealed class TenantDatabaseSchemaHealthService(
  ITenantDatabaseRegistryReadRepository readRepository,
  ITenantDatabaseConnectionFactory connectionFactory,
  ITenantDatabaseHealthWriter healthWriter) : ITenantDatabaseSchemaHealthService
{
  private const string HealthActor = "tenant-storage-health";

  public async Task<Result<TenantDatabaseSchemaHealthResult>> CheckAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken = default)
  {
    var descriptors = await readRepository.ListPhysicalDatabasesAsync(
      tenantDatabaseId - 1, 1, cancellationToken);
    var descriptor = descriptors.FirstOrDefault(item => item.TenantDatabaseId == tenantDatabaseId);
    if (descriptor is null)
    {
      return Result.Failure<TenantDatabaseSchemaHealthResult>(TenantStorageErrors.TenantDatabaseNotReady);
    }

    var result = await InspectAsync(descriptor, cancellationToken);
    await PersistAsync(descriptor, result, cancellationToken);
    return Result.Success(result);
  }

  public async Task<Result<TenantDatabaseHealthSweepSummary>> SweepAsync(
    int maximumDatabases,
    CancellationToken cancellationToken = default)
  {
    var discovered = 0;
    var upToDate = 0;
    var pending = 0;
    var ahead = 0;
    var mismatch = 0;
    var unreachable = 0;
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

        var result = await InspectAsync(descriptor, cancellationToken);
        await PersistAsync(descriptor, result, cancellationToken);

        if (result.ConnectivityStatus == TenantDatabaseConnectivityStatus.Unknown)
        {
          notVerifiable++;
          continue;
        }

        if (result.ConnectivityStatus != TenantDatabaseConnectivityStatus.Healthy)
        {
          unreachable++;
          continue;
        }

        switch (result.SchemaCompatibilityStatus)
        {
          case TenantDatabaseSchemaCompatibilityStatus.UpToDate: upToDate++; break;
          case TenantDatabaseSchemaCompatibilityStatus.PendingMigrations: pending++; break;
          case TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication: ahead++; break;
          case TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch: mismatch++; break;
        }
      }
    }

    return Result.Success(new TenantDatabaseHealthSweepSummary(
      discovered, upToDate, pending, ahead, mismatch, unreachable, notVerifiable));
  }

  private const int PageSize = 50;

  // One database, one connection, one history read. Connectivity is classified before schema, so a
  // network failure is never reported as a schema problem.
  private async Task<TenantDatabaseSchemaHealthResult> InspectAsync(
    TenantDatabaseDescriptor descriptor,
    CancellationToken cancellationToken)
  {
    // CustomerManaged has no runtime connectivity path (ADR-021). Nothing is attempted, and the result
    // says so: Unknown, never Healthy. Claiming verified health for a database we never contacted would
    // be the single most misleading thing this service could report.
    if (descriptor.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return new TenantDatabaseSchemaHealthResult(
        descriptor.TenantDatabaseId,
        TenantDatabaseConnectivityStatus.Unknown,
        TenantDatabaseSchemaCompatibilityStatus.Unknown,
        null, TargetMigration, [], SchemaObserved: false);
    }

    var connection = connectionFactory.Create(new TenantDatabaseConnectionTarget(
      descriptor.ServerKey, descriptor.DatabaseName, descriptor.HostingMode));
    if (connection.IsFailure)
    {
      // An unconfigured ServerKey is an operational unreachability, not a schema verdict.
      return new TenantDatabaseSchemaHealthResult(
        descriptor.TenantDatabaseId,
        TenantDatabaseConnectivityStatus.Unreachable,
        TenantDatabaseSchemaCompatibilityStatus.Unknown,
        null, TargetMigration, [], SchemaObserved: false);
    }

    await using var sqlConnection = connection.Value;
    try
    {
      await using var context = TenantDbContextBuilder.ForConnection(sqlConnection);
      var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
      var known = context.Database.GetMigrations().ToArray();
      var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

      return new TenantDatabaseSchemaHealthResult(
        descriptor.TenantDatabaseId,
        TenantDatabaseConnectivityStatus.Healthy,
        Classify(applied, known),
        applied.LastOrDefault(),
        known.LastOrDefault(),
        pending,
        SchemaObserved: true);
    }
    catch (SqlException exception)
    {
      // Login failures are a distinct operator problem from an unreachable host: one is a credential or
      // permission change, the other is a network or server incident.
      var status = IsAuthenticationFailure(exception)
        ? TenantDatabaseConnectivityStatus.AuthenticationFailed
        : TenantDatabaseConnectivityStatus.Unreachable;

      return new TenantDatabaseSchemaHealthResult(
        descriptor.TenantDatabaseId, status,
        TenantDatabaseSchemaCompatibilityStatus.Unknown, null, TargetMigration, [], SchemaObserved: false);
    }
  }

  // The compatibility decision, expressed over whole histories.
  internal static TenantDatabaseSchemaCompatibilityStatus Classify(
    IReadOnlyList<string> applied,
    IReadOnlyList<string> known)
  {
    var knownSet = known.ToHashSet(StringComparer.Ordinal);

    // Any applied migration this application does not know means the lineage diverged or the database is
    // newer. Either way, migrations must never be appended blindly on top of it.
    if (applied.Any(migration => !knownSet.Contains(migration)))
    {
      // Distinguish "strictly newer" from "divergent": a database that has every migration we know PLUS
      // extras is ahead of us; one missing some of ours while carrying unknown ones has a mismatched
      // lineage and needs human investigation.
      var appliedSet = applied.ToHashSet(StringComparer.Ordinal);
      return known.All(appliedSet.Contains)
        ? TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication
        : TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch;
    }

    // Every applied migration is known. It must also be an unbroken PREFIX of the catalog: a gap means a
    // migration was skipped, which is a mismatch rather than something to top up.
    for (var index = 0; index < applied.Count; index++)
    {
      if (!string.Equals(applied[index], known[index], StringComparison.Ordinal))
      {
        return TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch;
      }
    }

    return applied.Count == known.Count
      ? TenantDatabaseSchemaCompatibilityStatus.UpToDate
      : TenantDatabaseSchemaCompatibilityStatus.PendingMigrations;
  }

  // The migration head this application expects. Read from EF's own catalog rather than hard-coded, so it
  // cannot drift from the migrations actually deployed.
  internal static string? TargetMigration => TenantDbContextBuilder.KnownMigrations is { Count: > 0 } catalog
    ? catalog[^1]
    : null;

  private static bool IsAuthenticationFailure(SqlException exception) =>
    exception.Errors.OfType<SqlError>().Any(error => error.Number is 18456 or 4060 or 18452);

  // Each dimension is written by its own call, and only when this check actually observed it.
  //
  // THIS IS THE FIX FOR L6. The previous version wrote schema unconditionally, so a check that could not
  // connect recorded SchemaCompatibilityStatus.Unknown over a perfectly good UpToDate it had never looked
  // at — destroying exactly the observation the bounded stale-compatible policy exists to keep serving.
  // Now a failed connection writes connectivity and nothing else.
  private async Task PersistAsync(
    TenantDatabaseDescriptor descriptor,
    TenantDatabaseSchemaHealthResult result,
    CancellationToken cancellationToken)
  {
    // Reaching the database IS a connectivity observation, so it is recorded — but through an explicit
    // call to the connectivity writer rather than by this method quietly owning two dimensions.
    // An unverifiable database (customer-managed) keeps Unknown rather than a fabricated result.
    if (result.ConnectivityStatus != TenantDatabaseConnectivityStatus.Unknown)
    {
      await healthWriter.RecordConnectivityAsync(
        descriptor.TenantDatabaseId, result.ConnectivityStatus, HealthActor, cancellationToken);
    }

    if (!result.SchemaObserved)
    {
      return;
    }

    await healthWriter.RecordSchemaAsync(
      descriptor.TenantDatabaseId,
      result.SchemaCompatibilityStatus,
      result.AppliedMigration,
      result.TargetMigration,
      HealthActor,
      cancellationToken);
  }
}

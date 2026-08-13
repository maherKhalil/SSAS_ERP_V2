using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Registry baseline for the existing single-database deployment (ADR-017, TS-1B).
//
// Why a bootstrap service and not migration seed data: the real catalog name is only knowable from the
// live connection, the tenant population is unbounded, and this must be safely re-runnable. That is the
// same shape as the existing platform-support and localization bootstrap services.
//
// CONCURRENCY: two hosts may start together. Correctness rests on the database, not on ordering —
// UX_TenantDatabases_ServerKey_DatabaseName and UX_TenantDatabaseAssignments_ActiveTenant make a losing
// racer's insert fail rather than duplicate, and each conflict is re-read and treated as "already done".
//
// FAIL-CLOSED: contradictory pre-existing state is never silently normalised. A physical database row that
// exists with a different classification, or a tenant already routed somewhere unexpected, aborts startup
// with a diagnostic that names no secret.
public sealed class TenantStorageBootstrapService(
  PlatformDbContext dbContext,
  IDateTimeProvider clock,
  IOptions<TenantStorageOptions> optionsAccessor) : ITenantStorageBootstrapService
{
  public const string BootstrapActor = "tenant-storage-bootstrap";

  public const string BootstrapReason = "Initial registry baseline for the existing shared database.";

  // Tenants are backfilled in bounded pages so an unbounded tenant table is never materialised at once.
  private const int TenantPageSize = 500;

  public async Task<TenantStorageBootstrapOutcome> RunAsync(CancellationToken cancellationToken = default)
  {
    var serverKey = optionsAccessor.Value.DefaultServerKey;
    if (string.IsNullOrWhiteSpace(serverKey))
    {
      throw new InvalidOperationException(
        $"{TenantStorageOptions.SectionName}:{nameof(TenantStorageOptions.DefaultServerKey)} must be configured to bootstrap the tenant storage registry.");
    }

    var databaseName = ResolveCurrentDatabaseName();
    var (tenantDatabase, created) = await EnsureSharedDatabaseAsync(serverKey, databaseName, cancellationToken);
    var (assignmentsCreated, alreadyAssigned) = await BackfillAssignmentsAsync(tenantDatabase, cancellationToken);

    return new TenantStorageBootstrapOutcome(tenantDatabase.Id, created, assignmentsCreated, alreadyAssigned);
  }

  // The catalog name comes from the live connection, never from a hard-coded value and never from string
  // splitting: SqlConnectionStringBuilder parses the configured connection string properly.
  private string ResolveCurrentDatabaseName()
  {
    var connectionString = dbContext.Database.GetConnectionString();
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      throw new InvalidOperationException("A Platform connection string is required to bootstrap the tenant storage registry.");
    }

    var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
    if (string.IsNullOrWhiteSpace(databaseName))
    {
      throw new InvalidOperationException("The Platform connection string does not name an initial catalog.");
    }

    return databaseName;
  }

  private async Task<(TenantDatabase Database, bool Created)> EnsureSharedDatabaseAsync(
    string serverKey,
    string databaseName,
    CancellationToken cancellationToken)
  {
    var existing = await FindSharedDatabaseAsync(serverKey, databaseName, cancellationToken);
    if (existing is not null)
    {
      EnsureExpectedClassification(existing, serverKey, databaseName);
      return (existing, false);
    }

    var registered = TenantDatabase.Register(
      TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode.Shared,
      serverKey,
      databaseName,
      TenantDatabaseProvisioningStatus.Ready,
      BootstrapActor,
      clock.UtcNow);
    if (registered.IsFailure)
    {
      throw new InvalidOperationException(
        $"The tenant storage registry baseline is invalid: {registered.Error.Code}.");
    }

    dbContext.TenantDatabases.Add(registered.Value);
    try
    {
      await dbContext.SaveChangesAsync(cancellationToken);
      return (registered.Value, true);
    }
    catch (DbUpdateException)
    {
      // A peer host won the race on UX_TenantDatabases_ServerKey_DatabaseName. Re-read its row and treat
      // the pass as converged rather than retrying blindly.
      dbContext.Entry(registered.Value).State = EntityState.Detached;
      var winner = await FindSharedDatabaseAsync(serverKey, databaseName, cancellationToken)
        ?? throw new InvalidOperationException(
          "The tenant storage registry could not be established and no concurrent registration was found.");
      EnsureExpectedClassification(winner, serverKey, databaseName);
      return (winner, false);
    }
  }

  private Task<TenantDatabase?> FindSharedDatabaseAsync(
    string serverKey,
    string databaseName,
    CancellationToken cancellationToken) =>
    dbContext.TenantDatabases.SingleOrDefaultAsync(
      database => database.ServerKey == serverKey && database.DatabaseName == databaseName,
      cancellationToken);

  // Never silently reclassify: a row that already exists for this physical database but is recorded as
  // something other than a platform-managed shared database means the registry and the deployment
  // disagree, and only a human should resolve that.
  private static void EnsureExpectedClassification(TenantDatabase database, string serverKey, string databaseName)
  {
    if (database.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      database.StorageMode != TenantDatabaseStorageMode.Shared)
    {
      throw new InvalidOperationException(
        $"The registered tenant database for server key '{serverKey}' and catalog '{databaseName}' is classified as " +
        $"{database.HostingMode}/{database.StorageMode}, but the current deployment is a platform-managed shared database.");
    }
  }

  private async Task<(int Created, int AlreadyAssigned)> BackfillAssignmentsAsync(
    TenantDatabase tenantDatabase,
    CancellationToken cancellationToken)
  {
    var created = 0;
    var alreadyAssigned = 0;
    var pageStartExclusive = Guid.Empty;

    while (true)
    {
      // Keyset paging over a stable ordering: bounded memory regardless of tenant population, and no
      // dependence on one snapshot spanning every page.
      var tenantIds = await dbContext.Tenants
        .AsNoTracking()
        .Where(tenant => tenant.Id > pageStartExclusive)
        .OrderBy(tenant => tenant.Id)
        .Take(TenantPageSize)
        .Select(tenant => tenant.Id)
        .ToListAsync(cancellationToken);
      if (tenantIds.Count == 0)
      {
        break;
      }

      var activeAssignments = await dbContext.TenantDatabaseAssignments
        .AsNoTracking()
        .Where(assignment => assignment.EndedUtc == null && tenantIds.Contains(assignment.TenantId))
        .Select(assignment => new { assignment.TenantId, assignment.TenantDatabaseId })
        .ToListAsync(cancellationToken);

      // An active assignment pointing somewhere else is a real routing decision this bootstrap must not
      // second-guess — a tenant already promoted to a dedicated database, for example.
      foreach (var assignment in activeAssignments.Where(a => a.TenantDatabaseId != tenantDatabase.Id))
      {
        throw new InvalidOperationException(
          $"Tenant {assignment.TenantId} already has an active assignment to a different tenant database; " +
          "the tenant storage registry baseline will not override existing routing.");
      }

      var assignedTenantIds = activeAssignments.Select(assignment => assignment.TenantId).ToHashSet();
      var missingTenantIds = tenantIds.Where(tenantId => !assignedTenantIds.Contains(tenantId)).ToList();

      foreach (var tenantId in missingTenantIds)
      {
        var assignment = TenantDatabaseAssignment.CreateInitial(
          tenantId, tenantDatabase.Id, BootstrapReason, BootstrapActor, clock.UtcNow);
        if (assignment.IsFailure)
        {
          throw new InvalidOperationException(
            $"A tenant storage assignment could not be created for tenant {tenantId}: {assignment.Error.Code}.");
        }

        dbContext.TenantDatabaseAssignments.Add(assignment.Value);
      }

      try
      {
        await dbContext.SaveChangesAsync(cancellationToken);
      }
      catch (DbUpdateException)
      {
        // A peer host assigned one or more of these tenants first and
        // UX_TenantDatabaseAssignments_ActiveTenant rejected the duplicates — the intended outcome. Drop
        // this page's pending inserts and re-examine THE SAME page (pageStartExclusive is not advanced),
        // so any tenant the peer did not cover is still assigned. The retry terminates because each pass
        // observes the peer's committed rows and has strictly less left to insert.
        DetachPendingAssignments();
        continue;
      }

      created += missingTenantIds.Count;
      alreadyAssigned += assignedTenantIds.Count;
      pageStartExclusive = tenantIds[^1];
    }

    return (created, alreadyAssigned);
  }

  private void DetachPendingAssignments()
  {
    foreach (var entry in dbContext.ChangeTracker.Entries<TenantDatabaseAssignment>()
      .Where(entry => entry.State == EntityState.Added)
      .ToList())
    {
      entry.State = EntityState.Detached;
    }
  }
}

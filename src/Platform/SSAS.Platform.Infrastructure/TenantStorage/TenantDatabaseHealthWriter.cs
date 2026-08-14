using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Persists health and migration state onto the PHYSICAL TenantDatabase row (ADR-018).
//
// State lives on TenantDatabase, never duplicated per TenantDatabaseAssignment: a shared database has one
// schema and one migration state, and copying that onto every tenant's assignment would create rows that
// can disagree with each other about the same physical database.
//
// ONE METHOD PER HEALTH DIMENSION, deliberately. The previous single `RecordHealthAsync(id, mutate)` let a
// caller write any combination of dimensions in one go, which is how a connectivity check came to overwrite
// schema state it had never observed. A dimension-scoped API makes that mistake hard to express, and it
// matters more the moment a THIRD writer (recovery readiness, TS-Backup) appears.
public interface ITenantDatabaseHealthWriter
{
  // Connectivity observation only. Never touches schema or migration state.
  Task RecordConnectivityAsync(
    long tenantDatabaseId,
    TenantDatabaseConnectivityStatus status,
    string actor,
    CancellationToken cancellationToken = default);

  // Schema observation only. Called ONLY when migration history was actually read — a check that observed
  // nothing about schema must write nothing about schema.
  Task RecordSchemaAsync(
    long tenantDatabaseId,
    TenantDatabaseSchemaCompatibilityStatus status,
    string? appliedMigration,
    string? targetMigration,
    string actor,
    CancellationToken cancellationToken = default);

  // Migration-execution dimension. Multi-field but single-dimension, so the orchestrator's lifecycle
  // transitions (begin/complete/fail/block) stay expressible without reopening the cross-dimension door.
  Task RecordMigrationAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseHealthWriter(
  PlatformDbContext dbContext,
  IDateTimeProvider clock) : ITenantDatabaseHealthWriter
{
  // Two independent checkers write the same physical row on different cadences, so RowVersion conflicts
  // are ordinary rather than exceptional. The bound is small because each retry re-reads and reapplies one
  // dimension's observation — it converges immediately or the row is genuinely contended.
  private const int MaximumConcurrencyRetries = 3;

  public Task RecordConnectivityAsync(
    long tenantDatabaseId,
    TenantDatabaseConnectivityStatus status,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      tenantDatabaseId,
      database => database.RecordConnectivity(status, actor, clock.UtcNow),
      cancellationToken);

  public Task RecordSchemaAsync(
    long tenantDatabaseId,
    TenantDatabaseSchemaCompatibilityStatus status,
    string? appliedMigration,
    string? targetMigration,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      tenantDatabaseId,
      database => database.RecordSchemaHealth(status, appliedMigration, targetMigration, actor, clock.UtcNow),
      cancellationToken);

  public Task RecordMigrationAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutate);
    return ApplyAsync(tenantDatabaseId, mutate, cancellationToken);
  }

  // Applies ONE dimension's observation, re-reading and reapplying on a concurrency conflict.
  //
  // The reapply is what makes independent writers safe. The mutation closes over only its own dimension's
  // values, so replaying it against a freshly-loaded aggregate leaves whatever the winner wrote to OTHER
  // dimensions intact. Reloading and saving the whole stale aggregate — the obvious alternative — would be
  // last-write-wins across dimensions, which is precisely the bug this slice exists to remove.
  private async Task ApplyAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken)
  {
    for (var attempt = 0; ; attempt++)
    {
      var database = await dbContext.TenantDatabases
        .SingleOrDefaultAsync(item => item.Id == tenantDatabaseId, cancellationToken);
      if (database is null)
      {
        return;
      }

      mutate(database);

      try
      {
        await dbContext.SaveChangesAsync(cancellationToken);
        return;
      }
      catch (DbUpdateConcurrencyException)
      {
        // Another writer updated this row first. Give up only after a bounded number of attempts; a
        // permanently contended row is an operational signal, not something to spin on.
        if (attempt >= MaximumConcurrencyRetries)
        {
          return;
        }
      }
      finally
      {
        // A fleet sweep touches every physical database in the estate; leaving each one tracked would grow
        // the change tracker for the whole run and make every subsequent SaveChanges progressively slower.
        // Detaching also guarantees the next attempt re-reads rather than reusing the stale instance.
        dbContext.Entry(database).State = EntityState.Detached;
      }
    }
  }
}

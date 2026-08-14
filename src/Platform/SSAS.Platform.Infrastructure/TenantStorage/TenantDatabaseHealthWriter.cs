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
  // The shared dimension-scoped write path: re-read, reapply only this dimension, bounded retry. Shared
  // with the recovery-readiness writer so all three dimensions use one proven implementation.
  private readonly TenantDatabaseDimensionWriter writer = new(dbContext);

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

  private Task ApplyAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken) =>
    writer.ApplyAsync(tenantDatabaseId, mutate, cancellationToken);
}

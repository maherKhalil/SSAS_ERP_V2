using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// The durable cutover-operation boundary (ADR-020, TS-Storage Phase E1).
//
// Intent-specific transitions rather than a status setter, following the backup and verification run
// stores: beginning a cutover, recording a drain attempt, establishing the freeze, failing it and releasing
// it mean materially different things, and the two that must never be confused — a freeze that was
// ESTABLISHED and one that was merely REQUESTED — must not be reachable through the same call.
public interface ITenantCutoverOperationStore
{
  // Creates the operation, having verified both endpoints are eligible. Refuses when the tenant already
  // holds an active cutover; the filtered unique index refuses it a second time at the database.
  Task<Result<long>> BeginAsync(
    TenantCutoverBeginRequest request,
    CancellationToken cancellationToken = default);

  Task<TenantCutoverOperationRecord?> FindAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default);

  // THE RUNTIME WRITE GATE. Read on the tenant write path, from durable state — never from a cached flag,
  // and never from the presence of a lock, because a lock disappears with the process that took it.
  Task<bool> RefusesApplicationWritesAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);

  Task<Result> RequestFreezeAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default);

  // Establishes the durable fence. Called only while the drain lock is held.
  Task<Result> FreezeAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default);

  Task<Result> FailFreezeAsync(
    long cutoverOperationId,
    string? failureSummary,
    string actor,
    CancellationToken cancellationToken = default);

  // Pre-flip release. Idempotent, because a failed cutover must never leave a tenant permanently frozen.
  Task<Result> ReleaseFreezeAsync(
    long cutoverOperationId,
    string? failureSummary,
    string actor,
    CancellationToken cancellationToken = default);
}

public sealed record TenantCutoverBeginRequest(
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  string Actor);

public sealed record TenantCutoverOperationRecord(
  long CutoverOperationId,
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  TenantCutoverOperationStatus Status,
  DateTimeOffset? FreezeRequestedUtc,
  DateTimeOffset? FrozenUtc,
  DateTimeOffset? FreezeReleasedUtc,
  string? FailureSummary);

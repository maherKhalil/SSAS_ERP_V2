using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The RECOVERY-READINESS dimension writer (ADR-022 §3) — the THIRD independent writer on the physical
// TenantDatabase row.
//
// A SEPARATE INTERFACE from ITenantDatabaseHealthWriter, deliberately, rather than a fourth method on it.
// ADR-022 requires an independent writer per dimension, and compile-time separation is what makes that
// enforceable: a component holding this interface CANNOT express a connectivity, schema or migration write,
// because those methods do not exist on the type it was given. A combined "record all health" API would put
// cross-dimension clobbering back within reach of every caller, and with three writers that becomes
// progressively harder to detect than it was with two.
public interface ITenantDatabaseRecoveryReadinessWriter
{
  // Recovery-readiness observation only. Never touches connectivity, schema or migration state.
  //
  // AN EVALUATION THAT OBSERVES NOTHING WRITES NOTHING: a caller that could not determine readiness does
  // not call this at all, and the backup timestamps are optional so an evaluation that learned nothing new
  // about one backup type leaves that timestamp alone rather than erasing it.
  //
  // There is deliberately no way to write Unknown through this method — the aggregate rejects it. Unknown
  // is the pre-verification state, not a verdict, and writing it back would erase evidence.
  Task RecordRecoveryReadinessAsync(
    long tenantDatabaseId,
    TenantDatabaseRecoveryReadinessStatus status,
    string actor,
    DateTimeOffset? lastSuccessfulFullBackupUtc = null,
    DateTimeOffset? lastSuccessfulDifferentialBackupUtc = null,
    DateTimeOffset? lastSuccessfulLogBackupUtc = null,
    DateTimeOffset? lastRestoreVerificationUtc = null,
    CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseRecoveryReadinessWriter(
  PlatformDbContext dbContext,
  IDateTimeProvider clock) : ITenantDatabaseRecoveryReadinessWriter
{
  // The same shared dimension-scoped write path the health writer uses: re-read, reapply ONLY this
  // dimension, bounded retry, never a stale whole-aggregate replay.
  private readonly TenantDatabaseDimensionWriter writer = new(dbContext);

  public Task RecordRecoveryReadinessAsync(
    long tenantDatabaseId,
    TenantDatabaseRecoveryReadinessStatus status,
    string actor,
    DateTimeOffset? lastSuccessfulFullBackupUtc = null,
    DateTimeOffset? lastSuccessfulDifferentialBackupUtc = null,
    DateTimeOffset? lastSuccessfulLogBackupUtc = null,
    DateTimeOffset? lastRestoreVerificationUtc = null,
    CancellationToken cancellationToken = default) =>
    writer.ApplyAsync(
      tenantDatabaseId,
      database => database.RecordRecoveryReadiness(
        status,
        actor,
        clock.UtcNow,
        lastSuccessfulFullBackupUtc,
        lastSuccessfulDifferentialBackupUtc,
        lastSuccessfulLogBackupUtc,
        lastRestoreVerificationUtc),
      cancellationToken);
}

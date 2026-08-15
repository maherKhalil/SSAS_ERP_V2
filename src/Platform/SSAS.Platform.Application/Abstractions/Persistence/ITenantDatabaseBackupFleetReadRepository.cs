using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Fleet-wide reads for backup scheduling (ADR-022 §13, TS-Backup Phase C).
//
// SEPARATE from ITenantDatabaseBackupReadRepository on purpose. That contract answers questions about ONE
// physical database and is used on operator and execution paths; widening it with fleet queries would blur a
// boundary that currently reads clearly. This one exists only to feed a sweep.
//
// READ ONLY. Nothing here can cause a backup — the scheduler's only write path is through
// ITenantDatabaseBackupExecutor.
public interface ITenantDatabaseBackupFleetReadRepository
{
  // The distinct ServerKeys that currently have at least one eligible database.
  //
  // Discovery is SERVER-AWARE so fairness can cross page boundaries. Paging the fleet by identifier alone
  // meant a page full of one server's databases had to be worked through before another server was even
  // discovered — with a per-server concurrency cap of one, that is a backlog of sequential backups standing
  // between an unrelated server and its overdue database. Enumerating servers first makes the round-robin
  // real rather than confined to whatever happened to share a page.
  //
  // Cardinality is the number of physical SQL Servers in the estate, not the number of databases.
  Task<IReadOnlyList<string>> ListEligibleServerKeysAsync(CancellationToken cancellationToken = default);

  // One keyset page of eligible physical databases on ONE server, ordered by TenantDatabase.Id.
  //
  // KEYSET, NOT OFFSET (ADR-022 §13). The fleet is expected to grow, and OFFSET degrades linearly with depth
  // while re-reading rows the caller has already seen. `afterId` is exclusive, so a sweep advances that
  // server's own cursor by taking the last id of its previous page.
  //
  // Discovery is over PHYSICAL databases, never assignments: a shared database hosting a thousand tenants is
  // one backup target and must appear exactly once.
  Task<IReadOnlyList<TenantDatabaseBackupDueCandidate>> ListBackupCandidatesAsync(
    string serverKey,
    long afterId,
    int take,
    CancellationToken cancellationToken = default);

  // The most recent run per database for the given ids, in ONE query.
  //
  // Called only for databases already established as due, and batched deliberately: a per-database lookup
  // across a fleet is the N+1 that would make a sweep's cost scale with estate size rather than with work
  // actually needing doing.
  Task<IReadOnlyDictionary<long, TenantDatabaseBackupRunRecord>> ListLatestRunsAsync(
    IReadOnlyCollection<long> tenantDatabaseIds,
    CancellationToken cancellationToken = default);
}

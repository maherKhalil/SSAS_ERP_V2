namespace SSAS.Integration.Tests;

// The tenant-backup real-SQL suites run one at a time relative to EACH OTHER.
//
// They are unusually heavy for integration tests: each loads hundreds of megabytes into a disposable
// database and then backs it up repeatedly, and the session-loss experiment loads more still. Run
// concurrently on one instance they compete for the same buffer pool, and a full-suite run was observed
// failing two unrelated tests on SaveChanges while the backups were saturating the server.
//
// This does NOT weaken anything or reduce coverage — every assertion is unchanged. It only stops these
// three classes from being each other's load test. Other integration classes still run in parallel as
// before.
[CollectionDefinition(Name)]
public sealed class TenantBackupSerialSuites
{
  public const string Name = "tenant-backup-real-sql";
}

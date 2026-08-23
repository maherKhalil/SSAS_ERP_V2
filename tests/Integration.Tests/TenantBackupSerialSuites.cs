namespace SSAS.Integration.Tests;

// Classes in this collection run ONE AT A TIME relative to each other. xUnit parallelizes by collection,
// so every member added here is paid for by every other member, serially, on every run.
//
// ---- THE ADMISSION RULE (read this before adding a member)
//
// A class belongs here ONLY IF it uses a resource that is SHARED ACROSS DATABASES and that a per-test
// disposable catalog therefore cannot isolate. In practice that means exactly three things:
//
//   1. the INSTANCE BACKUP DIRECTORY, or full-size files on the same disk;
//   2. SERVER-LEVEL state — logins, EXECUTE AS LOGIN, sys.server_principals;
//   3. INSTANCE-WIDE catalogs such as msdb.dbo.backupset.
//
// A fourth, narrower reason is admitted and is currently held by one member: the class ASSERTS ON ELAPSED
// TIME, which concurrent load can pollute regardless of what it holds.
//
// These are NOT reasons to join:
//
//   * "it is heavy" or "it loads a lot of rows" — weight is not sharing. The heaviest class in the suite
//     (TenantCutoverCopy, 20,000 rows plus co-tenant noise) is NOT a member.
//   * "it needs real SQL and real transactions" — that is an argument for being an integration test.
//   * "the classes next to it are members" — arrival convention is how this collection reached fifteen.
//   * "it might be flaky under load" — if a test cannot survive a busy server, fix the test's precondition;
//     serializing it hides the weakness behind wall-clock the whole team pays for.
//
// EVERY MEMBER STATES ITS OWN REASON at its own [Collection] attribute. A member whose reason cannot be
// written in terms of 1-3 (or the timing exception) does not belong here. If you cannot name the shared
// resource, there isn't one.
//
// AND THE REMEDY HAS A BOUNDARY: returning a class to this collection answers a SHARING failure only.
// Saturation — the instance being unable to serve the concurrency the suite asks for, which surfaces as
// setup timeouts spread across unrelated classes — is not sharing, and re-serializing classes to cure it
// would spend the suite's parallelism on a problem that is not theirs. That case is answered where the
// setup timeout lives (`IntegrationSqlEnvironment`), and its next escalation is a stated parallelism
// ceiling, never a longer chain here.
//
// ---- THE MEMBERSHIP (five classes, 2026-08-23 after round 2)
//
//   TenantBackupProviderSqlServerTests              instance backup directory
//   TenantRestoreVerificationProviderSqlServerTests  full-size restore files, same disk
//   TenantRestoreVerificationProcessLossSqlServerTests  both of the above, plus mid-operation kills
//   TenantBackupPermissionBoundarySqlServerTests     server-level principals (EXECUTE AS LOGIN)
//   TenantRestoreVerificationPermissionSqlServerTests  server-level principals
//
// Every one names a resource under clause 1 or 2 of the rule above. None is here for weight, and none is
// here because its neighbours are. **This list is part of the comment: a member added without a line here
// is a member added without a reason.**
//
// ---- WHAT THIS COST, AND WHY THE RULE EXISTS
//
// The comment this replaces was written for THREE classes and said so ("these three classes") — it named
// the buffer pool, cited a real observed failure, and was accurate. It was never updated. Twelve more
// classes joined over the following weeks, each by pattern-matching its neighbours rather than by naming a
// resource, and the comment went on describing a collection that no longer existed. By 2026-08-23 the chain
// was FIFTEEN classes and the single longest pole in the gate — the serial chain measured 6,788s against a
// 6,790s suite, so the whole of Integration ran in the shadow of this one collection.
//
// ROUND 1 removed SEVEN that hold nothing shared. Wall 113m09s -> 46m29s.
//
// ROUND 2 removed THREE more, and the interesting one is why each had looked safe to keep:
//
//   * TenantCutoverOrchestration held NO shared resource at all. It was serial because it ASSERTED ON
//     ELAPSED TIME — and a wall-clock assertion is not a reason to serialize, it is a reason to fix the
//     assertion. The 30-second bound was converted to a 5-minute hang guard with the elapsed time reported
//     rather than asserted, and the class left. It had been ~68% of the remaining chain.
//   * TenantBackupSessionLoss took an applock that LOOKS instance-wide. `sp_getapplock` is
//     database-scoped, and the worker connects to the fixture's own disposable catalog.
//   * TenantBackupScheduler reads msdb.dbo.backupset, an instance-wide table — but every read is
//     predicated on its own Guid-named catalog. Reading a shared table is not sharing it.
//
// **All three had a plausible-sounding reason that did not survive being checked.** That is the argument
// for the rule above: a reason that is written down can be re-read, and a reason that is merely assumed
// cannot.
//
// The one accuracy the old comment had is worth keeping: two of the founding three DID name their resource
// (the instance backup directory, and full-size restore files on the same disk), and both are still here.
// The failure was not in the original judgement. It was that a collection with no stated admission rule
// grows by convention, and convention has no brakes.
[CollectionDefinition(Name)]
public sealed class TenantBackupSerialSuites
{
  public const string Name = "tenant-backup-real-sql";
}

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
// ---- WHAT THIS COST, AND WHY THE RULE EXISTS
//
// The comment this replaces was written for THREE classes and said so ("these three classes") — it named
// the buffer pool, cited a real observed failure, and was accurate. It was never updated. Twelve more
// classes joined over the following weeks, each by pattern-matching its neighbours rather than by naming a
// resource, and the comment went on describing a collection that no longer existed. By 2026-08-23 the chain
// was FIFTEEN classes and the single longest pole in the gate.
//
// Round 1 (2026-08-23) removed SEVEN that hold nothing shared. Each of them carries a note where its
// attribute used to be, saying it LEFT and why, so the next reader does not re-add it by pattern.
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

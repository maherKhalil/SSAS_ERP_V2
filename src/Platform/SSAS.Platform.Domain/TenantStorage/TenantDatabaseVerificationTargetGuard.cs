using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// THE PREDICATE THAT STANDS BETWEEN AUTOMATION AND A PRODUCTION DATABASE (ADR-022 §17).
//
// Built in this slice although the cleanup worker it protects arrives later, and deliberately so: the guard
// should exist and be tested before anything can call `DROP DATABASE`, not be written under pressure
// alongside the code that needs it.
//
// TWO DISTINCT QUESTIONS, kept separate because they are asked at different moments:
//
//   CanRestoreInto  — may this name be created as a verification target?   (asked before RESTORE)
//   IsEligibleForAutomatedCleanup — may this database be destroyed?        (asked before SINGLE_USER/DROP)
//
// The second is a CONJUNCTION, never a pattern match. ADR-022 compliance rule 24 forbids destructive
// cleanup driven by a name match alone, and §17 (v1.2) extends the same conjunction to forcing single-user,
// which is itself destructive against the wrong target.
public static class TenantDatabaseVerificationTargetGuard
{
  // May the platform restore into this name?
  //
  // Refuses anything outside the reserved namespace, and — the part that matters — anything that collides
  // with a REGISTERED authoritative database, regardless of how well-formed the name looks. A registered
  // name is never a verification target, and the check is ordinal and case-insensitive: SQL Server database
  // names are commonly compared case-insensitively, and when the question is "might this be production?"
  // the answer must err toward yes.
  public static bool CanRestoreInto(
    string? candidateName,
    long tenantDatabaseId,
    long verificationRunId,
    IEnumerable<string> registeredDatabaseNames)
  {
    ArgumentNullException.ThrowIfNull(registeredDatabaseNames);

    if (!TenantDatabaseVerificationNaming.MatchesRun(candidateName, tenantDatabaseId, verificationRunId))
    {
      return false;
    }

    return !CollidesWithRegisteredDatabase(candidateName!, registeredDatabaseNames);
  }

  // May automated cleanup destroy this database? EVERY condition must hold (ADR-022 §17).
  //
  // Expressed as one predicate rather than scattered checks so that no future caller can satisfy a subset
  // and proceed. The inputs are facts the caller must have established; this type does not fetch them,
  // because a guard that queries is a guard that can be given a stale answer without noticing.
  public static bool IsEligibleForAutomatedCleanup(TenantDatabaseVerificationCleanupCandidate candidate)
  {
    ArgumentNullException.ThrowIfNull(candidate);

    // 1. It carries the reserved system-controlled marker.
    if (!TenantDatabaseVerificationNaming.IsVerificationDatabaseName(candidate.DatabaseName))
    {
      return false;
    }

    // 2. It correlates to a durable verification record that names exactly this database. This is what
    //    turns "matches our convention" into "we created it", and it is why the record is written before
    //    the database is created rather than after.
    if (!candidate.HasMatchingVerificationRecord ||
      !string.Equals(candidate.RecordedDatabaseName, candidate.DatabaseName, StringComparison.Ordinal))
    {
      return false;
    }

    // 3. No verification is currently using it. A long legitimate restore must never be mistaken for an
    //    orphan — the failure mode there is destroying the work in progress.
    if (candidate.IsTargetOfActiveVerification)
    {
      return false;
    }

    // 4. It is not a registered authoritative tenant database.
    if (candidate.IsRegisteredTenantDatabase)
    {
      return false;
    }

    // 5. It has no tenant assignment.
    if (candidate.HasTenantAssignment)
    {
      return false;
    }

    // 6. It has aged past the grace period, so a database created moments ago by an instance whose record
    //    has not yet been observed is not swept out from under it.
    return candidate.Age >= candidate.GracePeriod;
  }

  private static bool CollidesWithRegisteredDatabase(
    string candidateName,
    IEnumerable<string> registeredDatabaseNames)
  {
    foreach (var registered in registeredDatabaseNames)
    {
      if (!string.IsNullOrWhiteSpace(registered) &&
        string.Equals(registered.Trim(), candidateName, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }
}

// The facts automated cleanup must have established before it may destroy a database (ADR-022 §17).
//
// A record rather than a parameter list, so adding a condition later is a compile error at every call site
// rather than a silently weaker guard.
public sealed record TenantDatabaseVerificationCleanupCandidate(
  string? DatabaseName,
  bool HasMatchingVerificationRecord,
  string? RecordedDatabaseName,
  bool IsTargetOfActiveVerification,
  bool IsRegisteredTenantDatabase,
  bool HasTenantAssignment,
  TimeSpan Age,
  TimeSpan GracePeriod);

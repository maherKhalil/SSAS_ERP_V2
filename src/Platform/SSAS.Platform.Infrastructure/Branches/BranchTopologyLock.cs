using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.Branches;

// SERIALISES EVERY CHANGE TO A TENANT'S BRANCH TOPOLOGY (Branch foundation B1a).
//
// ---- WHY A LOCK EXISTS HERE AT ALL.
//
// The invariant "no active normal user is left without an active branch" spans BOTH planes: the branches
// are rows in the tenant database, the assignments are rows in the platform database. A transaction cannot
// cover both — they are different catalogs and may be different servers — and no constraint can express a
// rule whose two halves live apart. So validating and then committing is a read-then-write across a gap,
// and three interleavings reach through that gap:
//
//   R1. Between the check and the commit, another administrator REMOVES the assignment that was the user's
//       only other active branch. The deactivation commits against a fact that is no longer true.
//   R2. In the same window a user is created or assigned holding ONLY the branch being deactivated, so a
//       user who did not exist during validation is stranded by it.
//   R3. Two administrators deactivate two different branches at once. Each validates against the other's
//       branch still being active, both pass, and a user authorized for exactly those two is stranded by
//       the pair — though neither deactivation was unsafe alone.
//
// None of those is hypothetical and none is closed by retrying: each participant's read was correct when
// it was taken. What is missing is mutual exclusion, which is what this provides.
//
// ---- WHAT IT COVERS, AND WHAT IT STILL DOES NOT.
//
// R3 IS CLOSED NOW: branch deactivation is the only branch-topology mutation that exists in B1a, and it
// takes this lock, so two of them cannot interleave.
//
// R1 AND R2 ARE CLOSED ONLY WHEN THE ASSIGNMENT WORKFLOW TAKES THE SAME LOCK. No code path mutates
// UserBranchAccess today — B1b introduces the first — so the invariant holds at B1a by absence rather than
// by exclusion. That is a real obligation on B1b, not a detail: an assignment command that writes without
// taking this resource re-opens R1 and R2 silently, and the deactivation guard will still look correct
// while no longer being sound.
//
// ---- WHY THE PLATFORM DATABASE.
//
// It is the plane that stays available when tenant storage is mid-cutover or unreachable, and it is where
// the assignment rows that B1b must serialise against already live. Putting it in the tenant database would
// make the lock unavailable in exactly the situations where branch administration still has to be refused
// safely rather than raced.
//
// SESSION-SCOPED ON A DEDICATED CONNECTION, following the Phase E precedent: a dead process drops its
// connection and the lock with it, so there is no lease to expire and no stale owner to clean up.
internal static class BranchTopologyLock
{
  public const string Prefix = "SSAS.Branch.Topology.";

  // Deliberately DISTINCT from the cutover locks. A tenant may legitimately be mid-cutover while its
  // branches are administered, and naming the resources apart is what keeps one from blocking the other.
  public static string ForTenant(Guid tenantId) => string.Create(
    CultureInfo.InvariantCulture, $"{Prefix}{tenantId:D}");

  public static async Task<bool> TryAcquireForSessionAsync(
    SqlConnection connection,
    Guid tenantId,
    TimeSpan timeout,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);

    await using var command = connection.CreateCommand();
    command.CommandText = "sys.sp_getapplock";
    command.CommandType = CommandType.StoredProcedure;

    AddParameter(command, "@Resource", ForTenant(tenantId));
    AddParameter(command, "@LockMode", "Exclusive");
    AddParameter(command, "@LockOwner", "Session");
    AddParameter(command, "@LockTimeout", (int)timeout.TotalMilliseconds);

    var result = command.CreateParameter();
    result.ParameterName = "@Result";
    result.DbType = DbType.Int32;
    result.Direction = ParameterDirection.ReturnValue;
    command.Parameters.Add(result);

    // The command must outlive the lock wait, or a transport timeout surfaces instead of the clean negative
    // sp_getapplock exists to give.
    command.CommandTimeout = (int)timeout.TotalSeconds + 30;
    await command.ExecuteNonQueryAsync(cancellationToken);

    // 0 granted, 1 granted after waiting. Anything else means we do not hold it, and a caller that does not
    // hold it does not get to decide whether a deactivation is safe.
    return Convert.ToInt32(result.Value, CultureInfo.InvariantCulture) is 0 or 1;
  }

  private static void AddParameter(SqlCommand command, string name, object value)
  {
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
  }
}

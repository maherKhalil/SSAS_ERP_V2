using Microsoft.Data.SqlClient;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// HOW A COMPETING sqlcmd PROCESS AUTHENTICATES (CI-004).
// ==================================================================================================
//
// Three fixtures start a SEPARATE PROCESS holding no application lock, to prove the backup guard reacts to
// a backup it did not initiate. That needs a real external client, so the choice of credentials is part of
// the test infrastructure rather than an incidental detail.
//
// ---- WHY THIS EXISTS.
//
// They passed `-E` — Windows Integrated Security. That is correct on a developer machine, where SQL Server
// is a local service and the test runs as a trusted Windows user. It cannot work against a Linux container
// reached with SQL authentication, and `sqlcmd -E` there fails before it opens a connection.
//
// ---- IT DERIVES FROM THE CONNECTION STRING THE TESTS ALREADY USE.
//
// Not from a second environment variable, and not from a hard-coded credential. Whatever
// SSAS_TEST_SQLSERVER says the suite connects with, the competing process connects with too — so the two
// cannot drift into authenticating as different principals, which would quietly change what the guard is
// being tested against.
//
// ---- WINDOWS BEHAVIOUR IS UNCHANGED.
//
// With no user id in the connection string the result is `-E`, exactly as before. The SQL-authentication
// branch is reached only when the environment actually supplies credentials.
internal static class SqlcmdAuthentication
{
  // The authentication arguments for the connection string in force, in sqlcmd's own order.
  public static void AddAuthentication(this IList<string> argumentList, string connectionString)
  {
    ArgumentNullException.ThrowIfNull(argumentList);

    var builder = new SqlConnectionStringBuilder(connectionString);

    if (string.IsNullOrWhiteSpace(builder.UserID))
    {
      // Integrated Security: the developer-machine path, and the original behaviour.
      argumentList.Add("-E");
      return;
    }

    argumentList.Add("-U");
    argumentList.Add(builder.UserID);
    argumentList.Add("-P");
    argumentList.Add(builder.Password);
  }

  // The server sqlcmd should target, falling back to localhost when the connection string names none.
  public static string ServerFor(string connectionString)
  {
    var dataSource = new SqlConnectionStringBuilder(connectionString).DataSource;

    return string.IsNullOrWhiteSpace(dataSource) ? "localhost" : dataSource;
  }
}

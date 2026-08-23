using Microsoft.Data.SqlClient;

namespace SSAS.Integration.Tests;

// ================================================================================================
// THE ONE PLACE A TEST'S SQL SERVER CONNECTION STRING IS RESOLVED.
// ================================================================================================
//
// Before 2026-08-23 this expression was duplicated in FORTY places — every fixture read
// `SSAS_TEST_SQLSERVER` and fell back to the same literal itself. That was harmless while the only
// thing being resolved was a server name. It stopped being harmless the moment the value needed a
// property that every fixture must have.
//
// ---- WHY THE COMMAND TIMEOUT LIVES HERE, AND NOT IN FORTY FIXTURES
//
// The gate-economics work removed a serial collection that had been the Integration suite's binding
// constraint, taking effective parallelism from 2.03x to roughly 10x. The suite then opened with far
// more collections creating their disposable catalogs at once, and `CREATE DATABASE` plus migrations
// serialize on the instance. On 2026-08-23 a full Release run failed SEVENTEEN tests with a single
// identical exception — `Execution Timeout Expired` — every one of them inside a fixture's
// `CreateAsync`, all within the first 2m22s, and not one of them in an assertion or a product path.
// It was a startup stampede against the default 30-second command timeout, not a defect in anything
// under test.
//
// `PlatformAuthenticationSessionFlowSqlServerTests` had already met this pressure alone and set its
// own `SetupCommandTimeoutSeconds = 120`. That class paid for the number; this file spends it once on
// everyone's behalf.
//
// **The lesson of the serial collection applies here in mirror.** That collection grew to fifteen
// members because joining it was a convention each author had to apply correctly, and conventions are
// copied without their reasons. A setup timeout that every future fixture must REMEMBER to set is the
// same defect wearing different clothes. So tolerance is a property of the PATH: a fixture gets it by
// resolving its connection string the only way there is, and an author who never reads this comment
// still gets it right.
//
// ---- IF 120 SECONDS IS NOT ENOUGH, THE ANSWER IS NOT 240
//
// **If setup timeouts recur AT 120 s, the next ruling is a stated parallelism ceiling via
// `xunit.runner.json` — raise this number no further.**
//
// One doubling backed by a precedent is tolerance. A second would be hiding saturation behind
// patience: the instance would be telling us it cannot serve the concurrency the suite asks for, and
// the honest response to that is an explicit ceiling, not a longer wait. This is written down so the
// next incident is a decision that was already taken rather than one rediscovered under pressure.
internal static class IntegrationSqlEnvironment
{
  // The number `PlatformAuthenticationSessionFlowSqlServerTests` arrived at independently.
  public const int SetupCommandTimeoutSeconds = 120;

  private const string Fallback =
    "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

  // The base connection string, carrying the setup timeout. Fixtures build on this with their own
  // InitialCatalog; `SqlConnectionStringBuilder` round-trips the timeout, so it survives that.
  public static string BaseConnectionString => WithSetupTimeout(
    Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ?? Fallback);

  // The same, pointed at one catalog.
  public static string ForCatalog(string catalog) =>
    new SqlConnectionStringBuilder(BaseConnectionString) { InitialCatalog = catalog }.ConnectionString;

  // Applied to whatever the environment supplies, not only to the fallback — a CI box that sets
  // SSAS_TEST_SQLSERVER is exactly the environment most likely to be contended, and it would be
  // perverse for it to be the one that misses out.
  private static string WithSetupTimeout(string connectionString) =>
    new SqlConnectionStringBuilder(connectionString)
    {
      CommandTimeout = SetupCommandTimeoutSeconds
    }.ConnectionString;
}

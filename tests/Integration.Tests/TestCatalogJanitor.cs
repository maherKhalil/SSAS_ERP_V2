using System.Collections.Concurrent;
using System.Globalization;

namespace SSAS.Integration.Tests;

// TEST CATALOG CLEANUP THAT CANNOT FAIL SILENTLY.
//
// Every SQL fixture in this suite creates real databases and drops them in teardown, and every one of those
// teardowns swallowed SqlException. The intent was right — a cleanup failure must never mask the assertion
// that ran before it — but swallowing made the leak INVISIBLE rather than merely non-fatal. The instance
// reached 73 orphaned catalogs before anyone noticed, and nothing in the suite would ever have reported it.
//
// The hard constraint is that teardown must not throw. When a test has ALREADY FAILED, throwing from
// DisposeAsync replaces the real diagnosis with a cleanup error, which trades a silent leak for a lost
// failure — strictly worse. So the leak is RECORDED rather than thrown:
//
//   test passed + cleanup failed -> the run fails on the leak guard, naming the catalog
//   test failed  + cleanup failed -> the test failure stays the headline, and the leak is still recorded
//
// Recorded twice on purpose. The in-memory list is what CatalogLeakGuardTests asserts on, and the console
// line is what a developer sees immediately in the test output without knowing this type exists.
internal static class TestCatalogJanitor
{
  private static readonly ConcurrentQueue<string> Leaks = new();

  public static IReadOnlyCollection<string> RecordedLeaks => Leaks.ToArray();

  // Called from a catch block that must not rethrow. Deliberately takes no cancellation token and performs
  // no I/O that could itself fail: a leak reporter that can throw would reintroduce the problem it exists
  // to solve.
  // Nullable catalog accepted deliberately: several fixtures hold their catalog names as string? and a
  // reporter that made the caller prove non-nullness would push a null check into thirteen catch blocks.
  public static void RecordLeak(string? catalog, Exception error)
  {
    var entry = string.Format(
      CultureInfo.InvariantCulture,
      "{0} could not be dropped: {1}",
      string.IsNullOrWhiteSpace(catalog) ? "<unnamed catalog>" : catalog,
      error.Message.Split('\n')[0].Trim());

    Leaks.Enqueue(entry);
    Console.Error.WriteLine($"[CATALOG LEAK] {entry}");
  }
}

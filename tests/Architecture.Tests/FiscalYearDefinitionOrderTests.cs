using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE FISCAL-YEAR LOCK IS TAKEN BEFORE BOTH CHECKS, NOT BETWEEN THEM AND THE WRITE (T-184).
// ==================================================================================================
//
// ---- ⚠ WHY THIS READS SOURCE ORDER, WHICH IS AN UNUSUAL THING FOR A TEST TO DO.
//
// **`GlFiscalYearOverlapChainSqlServerTests` cannot discriminate this, and saying so is the point.** It
// defines a year and then defines an overlapping one, sequentially — and that refusal is produced by
// `OverlapsExistingAsync` whether or not any lock is held. **A single-threaded test cannot tell a check
// inside the lock from a check outside it**, so a plant that moved the check would leave it green.
//
// That is a finding about the instrument rather than a failure of the task: **the overlap tests we already
// trust prove the RULE and say nothing about the RACE.**
//
// A concurrency test would discriminate, and would need two connections interleaved at a controlled point.
// That is a real test and a much larger one. **Until it exists, the order is the thing that can be
// asserted cheaply, and an unasserted order is what the original comment's window was made of.**
//
// ---- WHAT ORDER IS LOAD-BEARING AND WHY.
//
// ```
// BeginTransactionAsync   ->  AcquireAsync  ->  CodeExistsAsync + OverlapsExistingAsync  ->  Add  ->  Save
// ```
//
// **Acquiring after the checks would serialise only the insert and leave the reads racing** — the gap would
// move rather than close, and the code would look protected. `PostJournalCommandHandlers` records the same
// rule for its own aggregate.
public sealed class FiscalYearDefinitionOrderTests
{
  private const string Handler = "DefineFiscalYearCommandHandler";

  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public void The_lock_is_acquired_before_both_checks_and_inside_the_transaction()
  {
    var body = HandlerBody();

    // Without this the index comparisons below all return -1 and compare equal (`DEC-L-070`).
    Assert.NotEmpty(body);

    var transaction = body.IndexOf("BeginTransactionAsync", StringComparison.Ordinal);
    var acquire = body.IndexOf("calendarLock.AcquireAsync", StringComparison.Ordinal);
    var codeCheck = body.IndexOf("CodeExistsAsync", StringComparison.Ordinal);
    var overlapCheck = body.IndexOf("OverlapsExistingAsync", StringComparison.Ordinal);
    var save = body.IndexOf("SaveChangesAsync", StringComparison.Ordinal);

    Assert.True(transaction >= 0, "the handler opens no transaction");
    Assert.True(acquire >= 0, "the handler takes no calendar lock");
    Assert.True(codeCheck >= 0 && overlapCheck >= 0, "a uniqueness check has disappeared");

    Assert.True(transaction < acquire, "the lock is taken outside the transaction");

    // ⚠ BOTH checks, not just the overlap one. A code race and an overlap race are different conditions
    // and the caller acts differently on each, but they read the same rows and both must be serialised.
    Assert.True(acquire < codeCheck, "the code check runs outside the lock");
    Assert.True(acquire < overlapCheck, "THE OVERLAP CHECK RUNS OUTSIDE THE LOCK — the gap has moved, not closed");

    Assert.True(overlapCheck < save, "the overlap check runs after the write");
  }

  // ---- AND THE COMMIT IS WHAT RELEASES IT.
  //
  // `@LockOwner = 'Transaction'` means there is no separate release. A handler that returned without
  // committing would hold the lock until the transaction was disposed — correct, but only because the
  // commit is there. This asserts it is.
  [Fact]
  public void The_transaction_is_committed_rather_than_left_to_disposal()
  {
    var body = HandlerBody();

    Assert.NotEmpty(body);
    Assert.Contains("CommitAsync", body, StringComparison.Ordinal);
  }

  private static string HandlerBody()
  {
    var path = Path.Combine(
      FindRepositoryRoot(), "src", "Modules", "Finance", "SSAS.GL.Application", "Calendar",
      "CalendarCommandHandlers.cs");

    var source = File.ReadAllText(path);
    var start = source.IndexOf("class " + Handler, StringComparison.Ordinal);
    Assert.True(start >= 0, $"{Handler} is not in {Path.GetFileName(path)}");

    // To the next top-level type, or the end of the file.
    var next = source.IndexOf("\npublic sealed class ", start + 1, StringComparison.Ordinal);
    var body = next < 0 ? source[start..] : source[start..next];

    // Comments would satisfy every IndexOf below by naming the members in prose — the mirror this loop has
    // hit three times. Order must be read from CODE.
    return string.Join(
      '\n',
      body.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
  }

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
      directory is not null;
      directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}

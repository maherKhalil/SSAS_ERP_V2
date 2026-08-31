using System.Diagnostics;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE CAPTURE ITSELF IS TESTED, BECAUSE THREE FIXTURES NOW DEPEND ON IT (item 189).
// ==================================================================================================
//
// `SqlcmdChildProcess` exists so a failing backup test can say WHY it failed. That guarantee is only worth
// something if the capture actually works, and **the three fixtures that rely on it cannot demonstrate it**
// -- they fail so rarely that the evidence path would be exercised for the first time on the day it was
// needed, which is exactly the situation item 187 was in.
//
// ⚠ SO THE PLANT IS PERMANENT RATHER THAN A THROWAWAY. A one-off plant proves the capture worked on the
// afternoon it was written. These two run on every Integration pass, and redden if the capture ever stops
// carrying the child's own words.
//
// ---- ⚠ THE CONTROL IS THE SECOND TEST, NOT THE FIRST.
//
// A description that always said "something went wrong" would pass the failure test and prove nothing. The
// success case pins the other side: a child that exits 0 must be reported as exiting 0 on its own, with an
// empty stderr **stated as empty**. Both are needed for the description to carry information.
public sealed class SqlcmdChildProcessTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_deliberately_failing_child_puts_its_stderr_in_the_description()
  {
    // `-b` makes sqlcmd exit non-zero on a SQL error; `-r1` sends the message to stderr rather than stdout.
    // Both are deliberate: the point is that a NON-ZERO EXIT and the CHILD'S OWN TEXT both survive.
    using var child = Start("-b", "-r1", "-Q", "RAISERROR('SSAS_PLANT_189_STDERR', 16, 1)");

    // ⚠ WAITED FOR, NOT KILLED. `DescribeAsync` kills a child that is still running, which is right for the
    // backup fixtures and wrong here: this child is meant to finish, and its exit code is the evidence.
    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

    var description = await child.DescribeAsync();

    Assert.Contains("SSAS_PLANT_189_STDERR", description, StringComparison.Ordinal);
    Assert.Contains("exited on its own", description, StringComparison.Ordinal);

    // ⚠ NOT "exited on its own with code 0" -- a failing child that reported success would defeat the whole
    // point, since "finished normally" is one of the two outcomes this type exists to tell apart.
    Assert.DoesNotContain("with code 0", description, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_child_that_succeeds_is_described_as_succeeding_with_nothing_on_stderr()
  {
    using var child = Start("-b", "-r1", "-Q", "SELECT 'SSAS_PLANT_189_STDOUT'");

    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

    var description = await child.DescribeAsync();

    Assert.Contains("exited on its own with code 0", description, StringComparison.Ordinal);

    // The child's stdout is carried too -- sqlcmd sends ordinary SQL messages there, so a description that
    // only captured stderr would lose most of what a failing backup actually prints.
    Assert.Contains("SSAS_PLANT_189_STDOUT", description, StringComparison.Ordinal);

    // ⚠ EMPTY IS STATED, NOT LEFT BLANK. "stderr:" followed by nothing reads as a truncated message; the
    // distinction between "said nothing" and "we did not look" is the one this type is for.
    Assert.Contains("stderr: <empty>", description, StringComparison.Ordinal);
  }

  private static SqlcmdChildProcess Start(params string[] arguments)
  {
    var connectionString = TenantBackupProviderSqlServerTests.BackupFixture.Configured();

    var start = new ProcessStartInfo("sqlcmd")
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    start.ArgumentList.Add("-S");
    start.ArgumentList.Add(SqlcmdAuthentication.ServerFor(connectionString));
    start.ArgumentList.AddAuthentication(connectionString);
    start.ArgumentList.Add("-C");

    foreach (var argument in arguments)
    {
      start.ArgumentList.Add(argument);
    }

    return SqlcmdChildProcess.Start(start);
  }
}

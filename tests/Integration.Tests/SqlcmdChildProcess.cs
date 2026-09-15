using System.Diagnostics;
using System.Globalization;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// A COMPETING sqlcmd PROCESS THAT CAN SAY WHY IT DIED (item 189).
// ==================================================================================================
//
// Three fixtures start a separate `sqlcmd` to prove the backup guard reacts to a backup it did not
// initiate. All three set `RedirectStandardOutput` and `RedirectStandardError` to true, **read neither,
// and never look at the exit code**. The process was used only for `HasExited` and `Kill`.
//
// ---- ⚠ WHAT THAT COST, MEASURED RATHER THAN IMAGINED.
//
// Item 187's PHASE run failed
// `TenantBackupPermissionBoundarySqlServerTests.With_only_the_granular_permission_...` with *"the
// low-privilege guard never observed the competing backup"*. That test leaves its poll loop when
// `HasExited` becomes true -- which is equally the case when
//
//   (a) the backup finished before the guard looked, and
//   (b) `sqlcmd` never started at all: bad credentials, unreachable server, a full or missing backup
//       directory, a syntax error in the batch.
//
// **The two are indistinguishable, so the run produced no evidence for which had happened** -- and the
// evidence existed, in the child's stderr, at the moment it was discarded.
//
// ---- ⚠ AND DRAINING IS NOT OPTIONAL, IT IS A DEADLOCK FIX.
//
// A redirected pipe has a bounded OS buffer (~4 KB). A child that writes more than that with nobody
// reading **blocks in its own write** and never exits. `sqlcmd` running `BACKUP` in a loop emits a
// progress line and a `BACKUP DATABASE successfully processed N pages` block per iteration, so it passes
// 4 KB quickly.
//
// **The `Kill(entireProcessTree: true)` in every one of these tests' `finally` was masking that**: a
// wedged child is killed at the end and the test never sees it. ⚠ **So the deadlock is REMOVED here, not
// merely exposed** -- the drains below start before the child can fill either pipe, and both streams are
// consumed for the process's whole life. What the kill was hiding cannot recur once nothing accumulates.
//
// ---- ⚠ WHY NOT THE `TenantRestoreVerificationProcessLossSqlServerTests` PATTERN.
//
// That one is a HANDSHAKE: the child is our own verification host, it prints `ADMITTED <id>`, and the
// parent blocks on `ReadLineAsync` until that marker appears, reading stderr only on unexpected EOF. It
// is the right shape there and does not fit here, because **`sqlcmd` emits no marker we control** -- there
// is no line whose arrival means "the backup is now in flight", which is precisely the fact these tests
// have to establish from the server instead.
//
// What carries over is its principle -- **the child's own words belong in the failure** -- and that is
// what this type provides.
internal sealed class SqlcmdChildProcess : IDisposable
{
  private readonly Process process;
  private readonly Task<string> standardOutput;
  private readonly Task<string> standardError;

  private SqlcmdChildProcess(Process process)
  {
    this.process = process;

    // ⚠ STARTED BEFORE THE CHILD CAN FILL EITHER PIPE, and never awaited until the process is gone.
    // Reading one stream to completion before starting the other is the textbook deadlock.
    standardOutput = process.StandardOutput.ReadToEndAsync();
    standardError = process.StandardError.ReadToEndAsync();
  }

  public static SqlcmdChildProcess Start(ProcessStartInfo startInfo) =>
    new(Process.Start(startInfo)!);

  // True once the child is gone -- BY ITSELF OR BY OUR KILL. Callers polling this to decide "the work
  // finished" must pair it with `DescribeAsync`, because on its own it does not carry that meaning.
  public bool HasExited => process.HasExited;

  // Wait for the child to finish ON ITS OWN. ⚠ The backup fixtures never call this -- they run a child
  // that is supposed to outlive the observation and they kill it. It is here for callers whose child is
  // expected to terminate, which would otherwise have to reach `DescribeAsync` first and be told, quite
  // correctly, that the child was still running and got killed.
  public Task WaitForExitAsync(CancellationToken cancellationToken = default) =>
    process.WaitForExitAsync(cancellationToken);

  // ==================================================================================================
  // EVERYTHING THE CHILD SAID, FOR AN ASSERTION MESSAGE.
  // ==================================================================================================
  //
  // Kills the process if it is still running, so the pipes close and the drains complete; a test only
  // calls this when it has already stopped waiting. The exit code is reported as the child set it when it
  // left on its own, and named as OUR kill when it did not -- ⚠ **the distinction the failing run needed
  // and did not have.**
  public async Task<string> DescribeAsync()
  {
    var exitedOnItsOwn = process.HasExited;

    if (!exitedOnItsOwn)
    {
      Kill();
    }

    await process.WaitForExitAsync().ConfigureAwait(false);

    var output = await ReadOrExplainAsync(standardOutput).ConfigureAwait(false);
    var error = await ReadOrExplainAsync(standardError).ConfigureAwait(false);

    var disposition = exitedOnItsOwn
      ? string.Create(
          CultureInfo.InvariantCulture,
          $"the child exited on its own with code {process.ExitCode}")
      : "the child was still running and this test killed it";

    return string.Create(
      CultureInfo.InvariantCulture,
      $"[sqlcmd: {disposition}]{Environment.NewLine}" +
      $"  stdout: {Summarise(output)}{Environment.NewLine}" +
      $"  stderr: {Summarise(error)}");
  }

  public void Kill()
  {
    try
    {
      if (!process.HasExited)
      {
        process.Kill(entireProcessTree: true);
      }
    }
    // ⚠ `HasExited` then `Kill` is a RACE and this is the losing branch, not an error. The process exited
    // between the check and the kill -- which is the outcome the kill was trying to produce. There is no
    // way to close the window; the API offers no atomic "kill if running".
    catch (InvalidOperationException)
    {
    }
  }

  public void Dispose()
  {
    Kill();
    process.Dispose();
  }

  // A drain that has not completed must not hang the failure path: a test is already failing when it asks.
  private static async Task<string> ReadOrExplainAsync(Task<string> drain)
  {
    var completed = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);

    return completed == drain
      ? await drain.ConfigureAwait(false)
      : "<not drained within 10s of the process ending>";
  }

  // ⚠ EMPTY IS A FINDING, NOT A BLANK. A child that said nothing at all on a stream is evidence about
  // which of the two failure shapes occurred, so it is stated rather than rendered as whitespace.
  private static string Summarise(string stream)
  {
    var trimmed = stream.Trim();

    if (trimmed.Length == 0)
    {
      return "<empty>";
    }

    const int limit = 4000;

    return trimmed.Length <= limit
      ? trimmed
      : string.Create(
          CultureInfo.InvariantCulture,
          $"{trimmed[..limit]}… <{trimmed.Length - limit} more character(s)>");
  }
}

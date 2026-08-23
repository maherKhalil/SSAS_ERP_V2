using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE ADR-022 LOW-C GATE: what actually happens when the process performing a restore verification DIES.
//
// LOW-C has been carried open since D8 because reconciliation had only ever been exercised against
// simulations. Every cheap approximation answers a weaker question than the one the gate asks:
//
//   * cancelling a token, throwing, or disposing a scope all run the HANDLED failure path — the executor's
//     own `catch` marks the run InfrastructureUnavailable, which is precisely what a crash cannot do;
//   * `KILL <spid>` is the SERVER terminating the client, not the client vanishing, and SQL Server's
//     response to the two is not assumed here to be the same thing;
//   * moving StartedUtc backwards tests arithmetic, not abandonment.
//
// So these tests start a REAL SEPARATE PROCESS (tests/TestSupport/SSAS.TestSupport.VerificationHost) that
// drives the production restore-verification graph, and terminate it with Process.Kill. The two windows
// LOW-C names are covered: death after durable admission but before any restore, and death INSIDE a
// server-side RESTORE that the parent has positively observed running.
//
// NOTHING HERE PRESCRIBES SQL SERVER'S BEHAVIOUR. Whether the restore keeps going, whether the verification
// database survives and in what state are MEASURED and reported; the assertions are about what the platform
// then decides, which must be safe under every outcome the measurement can produce.
// SERIAL — uses the INSTANCE BACKUP DIRECTORY and performs full-size restores while killing processes
// mid-operation. Shares the resource the founding three named, and its failure modes are the ones
// concurrency would make hardest to read.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantRestoreVerificationProcessLossSqlServerTests(Xunit.Abstractions.ITestOutputHelper output)
{
  // How long the parent will wait for a server-side RESTORE to become observable before declaring the trial
  // inconclusive. Generous: a trial that never catches the restore proves nothing and must say so.
  private static readonly TimeSpan RestoreObservationWindow = TimeSpan.FromSeconds(90);

  // How long to keep watching SQL Server after the kill. The point is to record when things disappear, not
  // to wait for a particular answer.
  private static readonly TimeSpan PostMortemWindow = TimeSpan.FromSeconds(90);

  // Trials of the mid-restore kill. Three, matching the Phase B session-loss experiment: process death is a
  // race, and one observation cannot separate a rule from a coincidence.
  private const int RequiredTrials = 3;

  // CASE A — the process dies after durable admission and before any restore begins.
  //
  // The slot must stay held (a crash is not evidence of anything), survive the grace period, and only then
  // be released — and releasing it must not reopen the stale-admission defect D1–D4 closed.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Process_loss_after_admission_holds_the_slot_until_grace_and_server_evidence_agree()
  {
    await using var fixture = await ProcessLossFixture.CreateAsync(loadFiller: false);

    var child = await fixture.StartChildAsync("AdmitAndWait");
    var reserved = TenantDatabaseVerificationNaming.ForRun(fixture.TenantDatabaseId, child.VerificationRunId);
    Assert.Equal(reserved, (await fixture.ReadRunAsync(child.VerificationRunId)).VerificationDatabaseName);

    ProcessLossFixture.Kill(child.Process);
    Assert.True(child.Process.WaitForExit(30_000), "the child process did not terminate");

    // ---- OBSERVE. Nothing may have changed merely because a process disappeared.
    var afterKill = await fixture.ReadRunAsync(child.VerificationRunId);
    var serverAfterKill = await ProcessLossFixture.SnapshotAsync(reserved, child.RestoreSessionId);
    output.WriteLine("LOW-C case A after kill: " + afterKill + " | " + serverAfterKill);

    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Admitted, afterKill.Status);
    Assert.Equal(reserved, afterKill.VerificationDatabaseName);
    Assert.Equal(TenantDatabaseVerificationCleanupState.NotRequired, afterKill.CleanupState);
    Assert.Null(afterKill.CompletedUtc);
    Assert.Null(serverAfterKill.DatabaseState);
    Assert.False(serverAfterKill.RestoreRequestActive);

    // ---- BEFORE GRACE: age is not evidence, so the run stays exactly where it is.
    var beforeGrace = await fixture.ReconcileAsync(DateTimeOffset.UtcNow);
    Assert.Equal(1, beforeGrace.Inspected);
    Assert.Equal(1, beforeGrace.LeftActive);
    Assert.Equal(0, beforeGrace.Reconciled);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Admitted,
      (await fixture.ReadRunAsync(child.VerificationRunId)).Status);

    // ---- SERVER UNOBSERVABLE, past grace: absence of evidence is still not evidence of absence.
    var unobservable = await fixture.ReconcileUnobservableAsync(DateTimeOffset.UtcNow.AddHours(7));
    Assert.Equal(1, unobservable.LeftActive);
    Assert.Equal(1, unobservable.Unobservable);
    Assert.Equal(0, unobservable.Reconciled);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Admitted,
      (await fixture.ReadRunAsync(child.VerificationRunId)).Status);

    // ---- AFTER GRACE, with the server observable and nothing running: the abandoned run is released.
    var afterGrace = await fixture.ReconcileAsync(DateTimeOffset.UtcNow.AddHours(7));
    Assert.Equal(1, afterGrace.Reconciled);
    Assert.Equal(0, afterGrace.OrphansObserved);
    Assert.Equal(0, afterGrace.Conflicts);

    var reconciled = await fixture.ReadRunAsync(child.VerificationRunId);
    output.WriteLine("LOW-C case A reconciled: " + reconciled);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, reconciled.Status);
    Assert.Equal("ReconciledAbandoned", reconciled.ErrorSummary);
    Assert.NotNull(reconciled.CompletedUtc);
    // The run never created a database, so nothing is left to dispose of — and the reserved name survives
    // as the correlation from record to the database that would have existed.
    Assert.Equal(TenantDatabaseVerificationCleanupState.NotRequired, reconciled.CleanupState);
    Assert.Equal(reserved, reconciled.VerificationDatabaseName);
    Assert.Null(await ProcessLossFixture.DatabaseStateAsync(reserved));

    // ---- THE SLOT IS FREE, and exactly one replacement may take it.
    var replacement = await fixture.AdmitAsync(previousSuccessfulVerificationRunId: null);
    Assert.True(replacement.IsSuccess);
    Assert.NotEqual(child.VerificationRunId, replacement.Value);
    Assert.Equal(TenantDatabaseVerificationNaming.ForRun(fixture.TenantDatabaseId, replacement.Value),
      (await fixture.ReadRunAsync(replacement.Value)).VerificationDatabaseName);

    var second = await fixture.AdmitAsync(previousSuccessfulVerificationRunId: null);
    Assert.True(second.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationAlreadyAdmitted.Code, second.Error.Code);

    // ---- AND RECONCILIATION DID NOT REOPEN THE STALE-ADMISSION DEFECT. A decision anchored to a
    // successful verification that does not exist is still refused on its own terms, not because the slot
    // happens to be occupied.
    await fixture.ReleaseAsync(replacement.Value);
    var stale = await fixture.AdmitAsync(previousSuccessfulVerificationRunId: child.VerificationRunId);
    Assert.True(stale.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationAlreadySatisfied.Code, stale.Error.Code);
  }

  // CASE B — the load-bearing proof: the process dies while SQL Server is executing the RESTORE.
  //
  // The parent must SEE the restore running before it kills anything; a trial where the restore finished
  // first is reported as unproven rather than passed.
  //
  // REPEATED, following the precedent the Phase B session-loss experiment set: a single trial was found too
  // thin to carry an architectural gate, because process death is a race and one observation cannot
  // distinguish a rule from a coincidence. Each trial admits, restores, dies and reconciles independently.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Process_loss_during_an_observed_restore_is_reconciled_only_after_the_server_agrees()
  {
    await using var fixture = await ProcessLossFixture.CreateAsync(loadFiller: true);

    for (var trialNumber = 1; trialNumber <= RequiredTrials; trialNumber++)
    {
      await RunMidRestoreTrialAsync(fixture, trialNumber);
    }
  }


  private async Task RunMidRestoreTrialAsync(ProcessLossFixture fixture, int trialNumber)
  {
    var trial = "LOW-C case B trial " + trialNumber.ToString(CultureInfo.InvariantCulture);
    var child = await fixture.StartChildAsync("AdmitAndRestore");
    var reserved = TenantDatabaseVerificationNaming.ForRun(fixture.TenantDatabaseId, child.VerificationRunId);

    // ---- THE PRECONDITION, POSITIVELY ESTABLISHED. Durable Restoring AND a server-side RESTORE request
    // matched on the exact reserved database name, through the production visibility gate.
    var caught = await fixture.WaitForObservedRestoreAsync(
      child.VerificationRunId, reserved, RestoreObservationWindow);
    output.WriteLine(trial + " precondition: " + caught);

    Assert.True(caught.DurableRestoring,
      "the run never reached durable Restoring, so mid-restore process loss was not exercised: " + caught);
    Assert.True(caught.RestoreObservedOnServer,
      "no server-side RESTORE was ever observed for the reserved name, so the trial is INCONCLUSIVE and " +
      "LOW-C is not proven — enlarge the fixture rather than accepting this: " + caught);
    Assert.NotNull(caught.RestoreSessionId);
    Assert.False(child.Process.HasExited, "the child exited before it could be killed: " + caught);

    // ---- APPLICATION LOCKS, MEASURED WHILE THE SESSION IS STILL THE RESTORE SESSION.
    //
    // ---- THIS SAMPLE MOVED HERE ON 2026-08-23, AND THE MOVE IS THE WHOLE FIX.
    //
    // It used to be taken INSIDE the post-mortem, after `Kill(...)` had already returned — despite being
    // called `ApplicationLocksBeforeKill`. By then the child's session is dying or dead, and **SQL Server is
    // free to reuse the SPID**. The count was therefore taken against a number that no longer reliably
    // denotes the session it names, and asserting on it was asserting an ill-defined quantity.
    //
    // It failed exactly that way in the round-2 Release gate: `LOW-C case B trial 3` reported
    // `spid=54 ... applocksBefore=1`, with `restoreRequestGoneAtMs=103` but `sessionGoneAtMs=558` — 455ms in
    // which something still answered to SPID 54 after the restore had gone. The lock was not the
    // verification path's: **no restore-verification type takes an application lock anywhere in `src/`**
    // (every `sp_getapplock` caller is cutover freeze, cutover operation lock, cutover write fence, backup
    // ownership, the backup connection factory, migration ownership, or one of the two HR locks). The
    // observed lock belonged to a recycled SPID.
    //
    // Parallelism did not create the defect; it raised the exposure. Fifteen trials across five runs read
    // zero, and the one run with the freed classes running alongside read one.
    //
    // **The RULED CHANGE IS TO THE MEASUREMENT, AND IT STRENGTHENS THE CLAIM.** LOW-C's ratified intent —
    // the verification path holds no session-owned application lock — is unchanged, and is now proven
    // against the live restore session instead of against whatever currently answers to its old number.
    // ---- APPLICATION LOCKS. The verification path holds none, and this is now proven at the ONLY
    // instant where the claim is well defined.
    //
    // The count came from the SAME STATEMENT that identified this session as executing the RESTORE (see
    // `RestoreSessionAsync`), so there is no gap in which the session could have ended and handed its SPID
    // to somebody else. That gap was the 2026-08-23 defect, twice: first the count was taken after the
    // kill, then before it but through a second connection — 5 of 15 trials found the restore already over
    // by then. There is no second read to be late any more.
    Assert.Equal(0, caught.ApplicationLocks);

    // ---- THE KILL. An OS process termination, never KILL <spid>.
    var killedAt = Stopwatch.StartNew();
    ProcessLossFixture.Kill(child.Process);
    Assert.True(child.Process.WaitForExit(30_000), "the child process did not terminate");

    // ---- MEASURE. No expectation is asserted here; the point is to record what SQL Server actually does.
    var postMortem = await ProcessLossFixture.WatchAfterKillAsync(
      reserved, caught.RestoreSessionId!.Value, PostMortemWindow, killedAt);
    output.WriteLine(trial + " post-mortem: " + postMortem);

    // The durable record cannot have moved: the process that owned it is gone, and nothing else writes it.
    var afterKill = await fixture.ReadRunAsync(child.VerificationRunId);
    output.WriteLine(trial + " durable after kill: " + afterKill);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Restoring, afterKill.Status);
    Assert.Equal(reserved, afterKill.VerificationDatabaseName);
    Assert.Equal(TenantDatabaseVerificationCleanupState.Pending, afterKill.CleanupState);
    Assert.Null(afterKill.CompletedUtc);

    // The verification path holds NO session-owned applock — ownership of a verification is the durable
    // admission slot, not a lock. Asserted above against the LIVE restore session; here the claim takes its
    // only well-defined post-kill form.
    //
    // EITHER the session is gone — in which case it holds nothing, necessarily, and `null` says so — OR it
    // is still present and holds zero. There is no third reading, and in particular there is no reading in
    // which a count taken against a dead session's old SPID means anything about this test.
    Assert.True(postMortem.ApplicationLocksAtEnd is null or 0,
      "the restore session was still present after the kill and held application locks: " + postMortem);

    // ---- BEFORE GRACE: never released, whatever the server is doing.
    var beforeGrace = await fixture.ReconcileAsync(DateTimeOffset.UtcNow);
    Assert.Equal(1, beforeGrace.LeftActive);
    Assert.Equal(0, beforeGrace.Reconciled);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Restoring,
      (await fixture.ReadRunAsync(child.VerificationRunId)).Status);

    // ---- SERVER UNOBSERVABLE, past grace: still never released.
    var unobservable = await fixture.ReconcileUnobservableAsync(DateTimeOffset.UtcNow.AddHours(7));
    Assert.Equal(1, unobservable.Unobservable);
    Assert.Equal(0, unobservable.Reconciled);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Restoring,
      (await fixture.ReadRunAsync(child.VerificationRunId)).Status);

    // ---- PAST GRACE, SERVER OBSERVABLE. The decision now follows the MEASURED server state, and both
    // branches are legitimate outcomes of the experiment.
    var observed = await fixture.ObserveAsync(child.VerificationRunId, reserved);
    output.WriteLine(trial + " observation at reconciliation: " + observed);
    Assert.True(observed.ServerStateObserved);

    var afterGrace = await fixture.ReconcileAsync(DateTimeOffset.UtcNow.AddHours(7));
    var reconciled = await fixture.ReadRunAsync(child.VerificationRunId);
    output.WriteLine(trial + " reconciled: " + reconciled + " | summary reconciled=" +
      afterGrace.Reconciled.ToString(CultureInfo.InvariantCulture) + " orphans=" +
      afterGrace.OrphansObserved.ToString(CultureInfo.InvariantCulture) + " leftActive=" +
      afterGrace.LeftActive.ToString(CultureInfo.InvariantCulture));

    if (observed.RestoreIsActiveOnServer)
    {
      // SQL SERVER KEPT GOING. The slot must stay held — releasing it would admit a second restore
      // alongside a live one, which is the exact failure the grace period exists to avoid.
      Assert.Equal(0, afterGrace.Reconciled);
      Assert.Equal(1, afterGrace.LeftActive);
      Assert.Equal(TenantDatabaseRestoreVerificationStatus.Restoring, reconciled.Status);
      Assert.Equal(TenantDatabaseVerificationCleanupState.Pending, reconciled.CleanupState);
    }
    else
    {
      // NOTHING IS RUNNING. The run is terminally abandoned, and the exact reserved database state decides
      // whether an orphan was left behind.
      Assert.Equal(1, afterGrace.Reconciled);
      Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, reconciled.Status);
      Assert.NotNull(reconciled.CompletedUtc);

      if (observed.VerificationDatabaseExists)
      {
        Assert.Equal(1, afterGrace.OrphansObserved);
        Assert.Equal("ReconciledAbandonedWithOrphan", reconciled.ErrorSummary);
        // The correlation a later cleanup slice needs: the exact name, and a cleanup obligation that the
        // terminal transition deliberately did not clear.
        Assert.Equal(reserved, reconciled.VerificationDatabaseName);
        Assert.Equal(TenantDatabaseVerificationCleanupState.Pending, reconciled.CleanupState);
        Assert.NotNull(await ProcessLossFixture.DatabaseStateAsync(reserved));
      }
      else
      {
        Assert.Equal(0, afterGrace.OrphansObserved);
        Assert.Equal("ReconciledAbandoned", reconciled.ErrorSummary);
      }

      // NO INVENTED SUCCESS EVIDENCE, whatever state the database ended in. The probes never ran.
      Assert.NotEqual(TenantDatabaseRestoreVerificationStatus.Succeeded, reconciled.Status);
      Assert.NotEqual(TenantDatabaseBackupVerificationState.RestoreVerified,
        await fixture.BackupVerificationStateAsync());
      Assert.Null(await fixture.LastRestoreVerificationUtcAsync());

      // ---- REPLACEMENT SAFETY. Exactly one, with its own identity and its own name, and the orphan is
      // neither adopted nor overwritten.
      var replacement = await fixture.AdmitAsync(previousSuccessfulVerificationRunId: null);
      Assert.True(replacement.IsSuccess);
      Assert.NotEqual(child.VerificationRunId, replacement.Value);

      var replacementName = (await fixture.ReadRunAsync(replacement.Value)).VerificationDatabaseName;
      Assert.Equal(TenantDatabaseVerificationNaming.ForRun(fixture.TenantDatabaseId, replacement.Value),
        replacementName);
      Assert.NotEqual(reserved, replacementName);

      var contender = await fixture.AdmitAsync(previousSuccessfulVerificationRunId: null);
      Assert.True(contender.IsFailure);
      Assert.Equal(TenantStorageErrors.RestoreVerificationAlreadyAdmitted.Code, contender.Error.Code);

      if (observed.VerificationDatabaseExists)
      {
        // The orphan is still there, untouched, under its own name: reconciliation classifies, it does not
        // delete, and the replacement cannot collide with it.
        Assert.NotNull(await ProcessLossFixture.DatabaseStateAsync(reserved));
      }

      // Hand the slot back the way the platform does, so the next trial starts from a clean due state.
      await fixture.ReleaseAsync(replacement.Value);
    }

  }

  private sealed class ProcessLossFixture : IAsyncDisposable
  {
    private const string SourceServerKey = "PrimarySqlServer";
    private const string RestoreServerKey = "VerificationSqlServer";
    private const string DestinationKey = "process-loss";
    private const string Actor = "low-c-process-loss-test";

    // Enough data that the RESTORE lasts long enough to be caught in flight. Deliberately modest — the
    // experiment needs seconds of runway, not a realistic tenant volume — and the trial reports itself as
    // inconclusive rather than passing if that runway turns out to be too short.
    private const int FillerRows = 60_000;

    private static readonly JsonSerializerOptions ChildJson = new(JsonSerializerDefaults.Web);

    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantStorageOptions storageWithoutVerificationServer = new();
    private readonly TenantDatabaseRestoreVerificationOptions verification = new();
    private readonly List<Process> children = [];
    private string root = string.Empty;
    private string platformCatalog = string.Empty;
    private string sourceCatalog = string.Empty;

    public long TenantDatabaseId { get; private set; }

    public long BaselineBackupRunId { get; private set; }

    public static async Task<ProcessLossFixture> CreateAsync(bool loadFiller)
    {
      var fixture = new ProcessLossFixture();
      try
      {
        await fixture.InitialiseAsync(loadFiller);
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private async Task InitialiseAsync(bool loadFiller)
    {
      root = Path.Combine(TestRoot(), $"lowc-{token}");
      Directory.CreateDirectory(root);
      platformCatalog = $"SSAS_LOWC_Platform_{token}";
      sourceCatalog = $"SSAS_LOWC_Source_{token}";

      // SIMPLE while loading so the filler does not also inflate the log, then FULL before the baseline —
      // the restore this experiment interrupts is dominated by data pages either way.
      await ExecuteSqlAsync("master", $"CREATE DATABASE [{sourceCatalog}]");
      await ExecuteSqlAsync("master", $"ALTER DATABASE [{sourceCatalog}] SET RECOVERY SIMPLE");
      await using (var connection = new SqlConnection(ConnectionFor(sourceCatalog)))
      await using (var tenant = TenantDbContextBuilder.ForConnection(connection))
      {
        await tenant.Database.MigrateAsync();
      }

      if (loadFiller)
      {
        await ExecuteSqlAsync(sourceCatalog,
          "CREATE TABLE dbo.RestoreFiller (Id int IDENTITY(1,1) NOT NULL, Payload char(8000) NOT NULL)");
        await ExecuteSqlAsync(sourceCatalog,
          "INSERT INTO dbo.RestoreFiller (Payload) " +
          $"SELECT TOP ({FillerRows.ToString(CultureInfo.InvariantCulture)}) REPLICATE('x', 8000) " +
          "FROM sys.all_columns AS a CROSS JOIN sys.all_columns AS b");
      }

      await ExecuteSqlAsync("master", $"ALTER DATABASE [{sourceCatalog}] SET RECOVERY FULL");

      await using (var platform = PlatformContext())
      {
        await platform.Database.MigrateAsync();
        var database = TenantDatabase.Register(
          TenantDatabaseHostingMode.PlatformManaged,
          TenantDatabaseStorageMode.Dedicated,
          SourceServerKey,
          sourceCatalog,
          TenantDatabaseProvisioningStatus.Ready,
          Actor,
          now).Value;
        platform.TenantDatabases.Add(database);
        await platform.SaveChangesAsync();
        TenantDatabaseId = database.Id;

        var policy = TenantDatabaseBackupPolicy.Create(
          TenantDatabaseId,
          enabled: true,
          TenantDatabaseBackupManagementMode.AutomaticByPlatform,
          DestinationKey,
          fullBackupIntervalMinutes: 1_440,
          differentialBackupIntervalMinutes: null,
          transactionLogBackupIntervalMinutes: null,
          retentionExpectationDays: 30,
          restoreVerificationIntervalDays: 30,
          maximumBackupAgeMinutes: 2_880,
          Actor,
          now).Value;
        platform.TenantDatabaseBackupPolicies.Add(policy);
        await platform.SaveChangesAsync();
      }

      storage.BackupServers[SourceServerKey] =
        new TenantStorageServerOptions { ConnectionString = Configured() };
      storage.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = root };
      storage.VerificationServers[RestoreServerKey] =
        new TenantStorageServerOptions { ConnectionString = Configured() };

      // The same configuration MINUS the verification server, so the reconciler can be driven down its
      // unobservable path without breaking anything else.
      storageWithoutVerificationServer.BackupServers[SourceServerKey] =
        new TenantStorageServerOptions { ConnectionString = Configured() };
      storageWithoutVerificationServer.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = root };

      verification.Enabled = true;
      verification.RestoreServerKey = RestoreServerKey;
      verification.RestoreDataRoot = root;
      verification.RestoreLogRoot = root;
      verification.AllowSameInstanceVerification = true;
      verification.SchedulerBatchSize = 10;

      await TakeBaselineAsync();
    }

    // The production backup path, so the artifact the child later restores is a real managed baseline.
    private async Task TakeBaselineAsync()
    {
      await using var platform = PlatformContext();
      var registry = new TenantDatabaseRegistryReadRepository(platform);
      var reads = new TenantDatabaseBackupReadRepository(platform);
      var clock = new TestClock(now);
      var executor = new TenantDatabaseBackupExecutor(
        registry,
        reads,
        new TenantDatabaseBackupRunStore(platform, clock),
        new SqlServerTenantDatabaseBackupProvider(
          registry,
          new TenantDatabaseBackupConnectionFactory(Options.Create(storage)),
          Options.Create(storage),
          TenantDatabaseBackupOperationalOptions.Default),
        new TenantDatabaseRecoveryReadinessWriter(platform, clock));

      var result = await executor.ExecuteAsync(
        TenantDatabaseId, TenantDatabaseBackupOperation.SqlServerFull());
      Assert.True(result.IsSuccess);

      var baseline = await reads.FindLatestSuccessfulRunAsync(TenantDatabaseId, "SqlServer", "Full");
      Assert.NotNull(baseline);
      BaselineBackupRunId = baseline!.TenantDatabaseBackupRunId;
    }

    // ---- THE CHILD PROCESS -------------------------------------------------------------------------

    public async Task<ChildRun> StartChildAsync(string mode)
    {
      var configurationPath = Path.Combine(root, $"child-{Guid.NewGuid():N}.json");
      await File.WriteAllTextAsync(configurationPath, JsonSerializer.Serialize(new
      {
        Mode = mode,
        PlatformConnectionString = ConnectionFor(platformCatalog),
        ServerConnectionString = Configured(),
        TenantDatabaseId,
        SourceBackupRunId = BaselineBackupRunId,
        ExpectedPreviousSuccessfulVerificationRunId = (long?)null,
        SourceServerKey,
        RestoreServerKey,
        BackupDestinationKey = DestinationKey,
        BackupDirectory = root,
        RestoreDataRoot = root,
        RestoreLogRoot = root,
        Actor
      }, ChildJson));

      var start = new ProcessStartInfo(HostExecutable())
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      start.ArgumentList.Add(configurationPath);

      var process = Process.Start(start)!;
      children.Add(process);

      // Deterministic handshake: the child prints its admitted identity, and only then does the parent act.
      var admitted = await ReadLineAsync(process, "ADMITTED ", TimeSpan.FromSeconds(120));
      return new ChildRun(
        process,
        long.Parse(admitted["ADMITTED ".Length..], CultureInfo.InvariantCulture),
        RestoreSessionId: null);
    }

    private static async Task<string> ReadLineAsync(Process process, string prefix, TimeSpan timeout)
    {
      using var timeoutSource = new CancellationTokenSource(timeout);
      while (true)
      {
        var line = await process.StandardOutput.ReadLineAsync(timeoutSource.Token);
        if (line is null)
        {
          var error = await process.StandardError.ReadToEndAsync(CancellationToken.None);
          throw new InvalidOperationException(
            $"the verification host exited before emitting '{prefix}': {error}");
        }

        if (line.StartsWith(prefix, StringComparison.Ordinal))
        {
          return line;
        }
      }
    }

    // ABRUPT OS TERMINATION. No graceful shutdown, no connection close, no server-side KILL: the process
    // simply ceases to exist, which is what a crashing worker looks like to SQL Server.
    public static void Kill(Process process)
    {
      try
      {
        if (!process.HasExited)
        {
          process.Kill(entireProcessTree: true);
        }
      }
      catch (InvalidOperationException)
      {
      }
    }

    private static string HostExecutable()
    {
      var configured = typeof(ProcessLossFixture).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .SingleOrDefault(attribute => string.Equals(
          attribute.Key, "VerificationProcessHostPath", StringComparison.Ordinal))?.Value;

      Assert.False(string.IsNullOrWhiteSpace(configured),
        "the test assembly does not carry the verification host path");
      var path = Path.GetFullPath(configured!);
      Assert.True(File.Exists(path), $"the verification host was not built at {path}");
      return path;
    }

    // ---- OBSERVATION -------------------------------------------------------------------------------

    // Waits until the run is durably Restoring AND a matching server-side RESTORE is visible, correlated on
    // the exact reserved database name. Both conditions, or the trial is inconclusive.
    public async Task<RestorePrecondition> WaitForObservedRestoreAsync(
      long verificationRunId,
      string verificationDatabaseName,
      TimeSpan window)
    {
      var stopwatch = Stopwatch.StartNew();
      var durableRestoring = false;
      TimeSpan? durableAt = null;

      await using var observer = new SqlConnection(NonPooled(ConnectionFor("master")));
      await observer.OpenAsync();

      // The production visibility gate. A filtered DMV read is unobservable, not evidence of absence, and
      // this experiment is worthless without it.
      var visible = await SqlServerBackupVisibility.HasInFlightVisibilityAsync(observer);

      while (stopwatch.Elapsed < window)
      {
        if (!durableRestoring)
        {
          durableRestoring = (await ReadRunAsync(verificationRunId)).Status ==
            TenantDatabaseRestoreVerificationStatus.Restoring;
          if (durableRestoring)
          {
            durableAt = stopwatch.Elapsed;
          }
        }

        var session = await RestoreSessionAsync(observer, verificationDatabaseName);
        if (durableRestoring && session is not null)
        {
          // `session.ApplicationLocks` was counted in the same statement that proved this session is
          // restoring. There is no instant between the two facts for the session to change identity in.
          return new RestorePrecondition(
            true, true, session.SessionId, session.ApplicationLocks, durableAt, stopwatch.Elapsed,
            visible, await DatabaseStateAsync(verificationDatabaseName));
        }

        await Task.Delay(10);
      }

      return new RestorePrecondition(
        durableRestoring, false, null, null, durableAt, null, visible,
        await DatabaseStateAsync(verificationDatabaseName));
    }

    // Polls SQL Server after the kill and records WHEN things change. No expectation is encoded here.
    public static async Task<PostMortem> WatchAfterKillAsync(
      string verificationDatabaseName,
      int restoreSessionId,
      TimeSpan window,
      Stopwatch since)
    {
      await using var observer = new SqlConnection(NonPooled(ConnectionFor("master")));
      await observer.OpenAsync();

      TimeSpan? requestGoneAt = null;
      TimeSpan? sessionGoneAt = null;

      while (since.Elapsed < window)
      {
        if (requestGoneAt is null &&
          await RestoreSessionAsync(observer, verificationDatabaseName) is null)
        {
          requestGoneAt = since.Elapsed;
        }

        if (sessionGoneAt is null && !await SessionPresentAsync(observer, restoreSessionId))
        {
          sessionGoneAt = since.Elapsed;
        }

        if (requestGoneAt is not null && sessionGoneAt is not null)
        {
          break;
        }

        await Task.Delay(25);
      }

      // Counted ONLY while the session is still present. Once it is gone its SPID may already belong to
      // somebody else, and a count against it would describe a stranger.
      var stillPresent = await SessionPresentAsync(observer, restoreSessionId);

      return new PostMortem(
        requestGoneAt,
        sessionGoneAt,
        await DatabaseStateAsync(verificationDatabaseName),
        stillPresent ? await ApplicationLockCountAsync(observer, restoreSessionId) : null);
    }

    // ---- THE DEFINITION OF "RESTORING", AND THE MEASUREMENT, IN ONE STATEMENT.
    //
    // Returns the session executing the RESTORE for this reserved name **together with the application
    // locks that session holds**, read in the same statement at the same instant.
    //
    // ---- WHY THE COUNT LIVES HERE AND NOT IN A SAMPLE OF ITS OWN (2026-08-23)
    //
    // It was a separate call twice, and both times the gap was the defect. First it was taken AFTER the
    // kill, when the dying session's SPID could already have been reassigned — a stranger's lock read as
    // this session's. Moving it before the kill narrowed the gap but did not close it: the restore
    // routinely finished inside the remaining window, measured at **5 inconclusive trials in 15**, because
    // the sample opened its own non-pooled connection and a TCP-plus-auth handshake is a long time next to
    // a short restore.
    //
    // There is no window now. The iteration that FIRST OBSERVES the restore IS the measurement, so a
    // session cannot stop restoring between being identified and being counted. Not shrunk — removed.
    //
    // The two former consumers of this predicate are now one site, which is also what the drift-proofing
    // wanted: there is no second copy to keep in step.
    private static async Task<RestoringSession?> RestoreSessionAsync(
      SqlConnection connection, string databaseName)
    {
      await using var command = connection.CreateCommand();
      command.CommandText = """
        SELECT TOP (1) request.[session_id],
          (SELECT COUNT(*) FROM sys.dm_tran_locks
            WHERE [request_session_id] = request.[session_id]
              AND [resource_type] = N'APPLICATION')
        FROM sys.dm_exec_requests AS request
        CROSS APPLY sys.dm_exec_sql_text(request.[sql_handle]) AS statement
        WHERE request.[command] IN (N'RESTORE DATABASE', N'RESTORE LOG')
          AND CHARINDEX(QUOTENAME(@name), statement.[text]) > 0;
        """;
      command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;

      await using var reader = await command.ExecuteReaderAsync();
      if (!await reader.ReadAsync())
      {
        return null;
      }

      // `session_id` is SMALLINT in sys.dm_exec_requests, so it is converted rather than cast — the
      // previous ExecuteScalar path used Convert.ToInt32 for exactly this reason. The count is a plain int.
      return new RestoringSession(
        Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetInt32(1));
    }

    private static async Task<bool> SessionPresentAsync(SqlConnection connection, int sessionId)
    {
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE [session_id] = @session";
      command.Parameters.Add("@session", System.Data.SqlDbType.Int).Value = sessionId;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
    }

    // Session-owned application locks held by the restoring session. The verification path takes none —
    // measured rather than inferred from reading the code.
    private static async Task<int> ApplicationLockCountAsync(SqlConnection connection, int sessionId)
    {
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT COUNT(*) FROM sys.dm_tran_locks " +
        "WHERE [request_session_id] = @session AND [resource_type] = N'APPLICATION'";
      command.Parameters.Add("@session", System.Data.SqlDbType.Int).Value = sessionId;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public static async Task<ServerSnapshot> SnapshotAsync(string verificationDatabaseName, int? restoreSessionId)
    {
      await using var connection = new SqlConnection(NonPooled(ConnectionFor("master")));
      await connection.OpenAsync();
      return new ServerSnapshot(
        await DatabaseStateAsync(verificationDatabaseName),
        await RestoreSessionAsync(connection, verificationDatabaseName) is not null,
        // Null when this trial never had a restoring session to track, so the log cannot read as a
        // measurement that was not taken.
        restoreSessionId is null ? null : await SessionPresentAsync(connection, restoreSessionId.Value));
    }

    public static async Task<string?> DatabaseStateAsync(string databaseName)
    {
      await using var connection = new SqlConnection(NonPooled(ConnectionFor("master")));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT [state_desc] FROM sys.databases WHERE [name] = @name";
      command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = databaseName;
      var value = await command.ExecuteScalarAsync();
      return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    // The production observer, on the production trust boundary.
    public async Task<TenantDatabaseRestoreVerificationServerObservation> ObserveAsync(
      long verificationRunId,
      string verificationDatabaseName) =>
      await new SqlServerTenantDatabaseRestoreVerificationServerObserver(
          new TenantDatabaseVerificationConnectionFactory(
            Options.Create(storage), Options.Create(verification)))
        .ObserveAsync(new TenantDatabaseRestoreVerificationServerObservationRequest(
          verificationRunId, TenantDatabaseId, RestoreServerKey, SourceServerKey,
          verificationDatabaseName));

    // ---- PLATFORM STATE ----------------------------------------------------------------------------

    public async Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileAsync(
      DateTimeOffset asOf) =>
      await ReconcileAsync(asOf, storage);

    public async Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileUnobservableAsync(
      DateTimeOffset asOf) =>
      await ReconcileAsync(asOf, storageWithoutVerificationServer);

    private async Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileAsync(
      DateTimeOffset asOf,
      TenantStorageOptions serverConfiguration)
    {
      await using var platform = PlatformContext();
      var clock = new TestClock(asOf);
      var reconciler = new TenantDatabaseRestoreVerificationReconciler(
        new TenantDatabaseRestoreVerificationRunStore(platform, clock),
        new SqlServerTenantDatabaseRestoreVerificationServerObserver(
          new TenantDatabaseVerificationConnectionFactory(
            Options.Create(serverConfiguration), Options.Create(verification))),
        Options.Create(verification),
        clock,
        NullLogger<TenantDatabaseRestoreVerificationReconciler>.Instance);

      return await reconciler.ReconcileAsync();
    }

    public async Task<SSAS.BuildingBlocks.Domain.Result<long>> AdmitAsync(
      long? previousSuccessfulVerificationRunId)
    {
      await using var platform = PlatformContext();
      return await new TenantDatabaseRestoreVerificationRunStore(platform, new TestClock(DateTimeOffset.UtcNow))
        .TryAdmitAsync(new TenantDatabaseRestoreVerificationAdmissionRequest(
          TenantDatabaseId,
          BaselineBackupRunId,
          previousSuccessfulVerificationRunId,
          TenantDatabaseRestoreDepth.Full,
          RestoreServerKey,
          Actor));
    }

    // Frees a slot the way the platform does — a terminal transition, never a delete.
    public async Task ReleaseAsync(long verificationRunId)
    {
      await using var platform = PlatformContext();
      var released = await new TenantDatabaseRestoreVerificationRunStore(
          platform, new TestClock(DateTimeOffset.UtcNow))
        .MarkInfrastructureUnavailableAsync(verificationRunId, "test-release", Actor);
      Assert.True(released.IsSuccess);
    }

    public async Task<DurableRun> ReadRunAsync(long verificationRunId)
    {
      await using var platform = PlatformContext();
      var run = await platform.TenantDatabaseRestoreVerificationRuns.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == verificationRunId);
      return new DurableRun(
        run.Id, run.Status, run.VerificationDatabaseName, run.CleanupState,
        run.ErrorSummary, run.CompletedUtc);
    }

    public async Task<TenantDatabaseBackupVerificationState> BackupVerificationStateAsync()
    {
      await using var platform = PlatformContext();
      var backup = await platform.TenantDatabaseBackupRuns.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == BaselineBackupRunId);
      return backup.VerificationState;
    }

    public async Task<DateTimeOffset?> LastRestoreVerificationUtcAsync()
    {
      await using var platform = PlatformContext();
      var database = await platform.TenantDatabases.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == TenantDatabaseId);
      return database.LastRestoreVerificationUtc;
    }

    private PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(
          ConnectionFor(platformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock(now));
    }

    // ---- TEARDOWN ----------------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
      foreach (var child in children)
      {
        Kill(child);
        try
        {
          child.WaitForExit(30_000);
        }
        catch (SystemException)
        {
        }
        finally
        {
          child.Dispose();
        }
      }

      // EXACT NAMES ONLY, read back from the durable records this fixture created. No wildcard sweep, and
      // nothing that could reach a name this test did not produce.
      var verificationCatalogs = new List<string>();
      if (!string.IsNullOrWhiteSpace(platformCatalog))
      {
        try
        {
          await using var platform = PlatformContext();
          verificationCatalogs.AddRange(await platform.TenantDatabaseRestoreVerificationRuns.AsNoTracking()
            .Where(run => run.VerificationDatabaseName != null)
            .Select(run => run.VerificationDatabaseName!)
            .ToListAsync());
        }
        catch (SqlException)
        {
        }
      }

      foreach (var catalog in verificationCatalogs
        .Concat([sourceCatalog, platformCatalog])
        .Where(value => !string.IsNullOrWhiteSpace(value)))
      {
        await DropAsync(catalog);
      }

      if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
      {
        try { Directory.Delete(root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
      }
    }

    // TEST-ONLY privileged teardown. A database left in RESTORING cannot be put into SINGLE_USER, so the
    // direct DROP is attempted first and the single-user dance is only the fallback for an online one.
    private static async Task DropAsync(string catalog)
    {
      try
      {
        await ExecuteSqlAsync("master", $"IF DB_ID(N'{catalog}') IS NOT NULL DROP DATABASE [{catalog}]", 300);
        return;
      }
      catch (SqlException)
      {
      }

      try
      {
        await ExecuteSqlAsync("master",
          $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN " +
          $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
          $"DROP DATABASE [{catalog}]; END", 300);
      }
      catch (SqlException error)
      {
        TestCatalogJanitor.RecordLeak(catalog, error);
      }
    }

    private static string TestRoot() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_BACKUP_ROOT") ??
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SSAS_BackupTests");

    private static string Configured() =>
      IntegrationSqlEnvironment.BaseConnectionString;

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }.ConnectionString;

    private static string NonPooled(string connectionString) =>
      new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

    private static async Task ExecuteSqlAsync(string catalog, string sql, int timeoutSeconds = 600)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = timeoutSeconds;
      await command.ExecuteNonQueryAsync();
    }
  }

  private sealed record ChildRun(Process Process, long VerificationRunId, int? RestoreSessionId);

  private sealed record DurableRun(
    long VerificationRunId,
    TenantDatabaseRestoreVerificationStatus Status,
    string? VerificationDatabaseName,
    TenantDatabaseVerificationCleanupState CleanupState,
    string? ErrorSummary,
    DateTimeOffset? CompletedUtc)
  {
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
      $"run={VerificationRunId}, status={Status}, name={VerificationDatabaseName}, " +
      $"cleanup={CleanupState}, error={ErrorSummary}, completed={CompletedUtc:o}");
  }

  private sealed record ServerSnapshot(
    string? DatabaseState,
    bool RestoreRequestActive,
    bool? SessionPresent)
  {
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
      $"databaseState={DatabaseState ?? "<absent>"}, restoreActive={RestoreRequestActive}, " +
      $"sessionPresent={(SessionPresent is { } present ? present.ToString() : "<not tracked>")}");
  }

  private sealed record RestorePrecondition(
    bool DurableRestoring,
    bool RestoreObservedOnServer,
    int? RestoreSessionId,
    // Counted in the SAME statement that identified the session as restoring — see RestoreSessionAsync.
    int? ApplicationLocks,
    TimeSpan? DurableRestoringAt,
    TimeSpan? RestoreObservedAt,
    bool InFlightVisibility,
    string? DatabaseStateWhenObserved)
  {
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
      $"durableRestoring={DurableRestoring}@{DurableRestoringAt?.TotalMilliseconds:F0}ms, " +
      $"restoreObserved={RestoreObservedOnServer}@{RestoreObservedAt?.TotalMilliseconds:F0}ms, " +
      $"spid={RestoreSessionId}, applocks={ApplicationLocks}, " +
      $"inFlightVisibility={InFlightVisibility}, " +
      $"databaseState={DatabaseStateWhenObserved ?? "<absent>"}");
  }

  // The session executing a RESTORE for the reserved name, and what it held at that instant. One
  // statement produced both, so they cannot disagree about which session they describe.
  private sealed record RestoringSession(int SessionId, int ApplicationLocks);

  private sealed record PostMortem(
    TimeSpan? RestoreRequestGoneAt,
    TimeSpan? RestoreSessionGoneAt,
    string? DatabaseState,
    // NULL means the session was gone when the window closed, which is the common and expected outcome.
    // A number means it was still present and this is what it held. The old `AfterKill` int could not tell
    // those apart, and conflating them is what let a recycled SPID's lock be read as this session's.
    int? ApplicationLocksAtEnd)
  {
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
      $"restoreRequestGoneAtMs={RestoreRequestGoneAt?.TotalMilliseconds:F0}, " +
      $"sessionGoneAtMs={RestoreSessionGoneAt?.TotalMilliseconds:F0}, " +
      $"databaseState={DatabaseState ?? "<absent>"}, " +
      $"applocksAtEnd={(ApplicationLocksAtEnd is null ? "<session gone>" : ApplicationLocksAtEnd.Value.ToString(CultureInfo.InvariantCulture))}");
  }

  private sealed class TestClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => utcNow;
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "low-c-process-loss-test";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class NoTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }
}

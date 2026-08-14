using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SSAS.Integration.Tests;

// THE ADR-022 §14 SESSION-LOSS GATE.
//
// ADR-022 states that session-scoped ownership is the PLANNED mechanism and that its sufficiency for
// long-running backups is NOT established: migrations are short, backups are not, and the assumption that a
// dead client promptly stops a server-side BACKUP was inherited from the migration precedent rather than
// demonstrated. This test is what demonstrates it.
//
// The question, precisely: when the client process holding the backup applock dies mid-BACKUP, is the
// server-side BACKUP gone by the time another worker can acquire ownership? If yes, the applock alone is
// sufficient (exit condition A). If no, an in-flight detection guard is mandatory (exit condition B).
//
// The termination is an ABRUPT CLIENT PROCESS KILL, not KILL <spid>. A server-initiated kill answers a
// different question; ADR-022's scenario is a worker crashing, so the experiment reproduces a client
// disappearing.
public sealed class TenantBackupSessionLossSqlServerTests(Xunit.Abstractions.ITestOutputHelper output)
{
  // How long the observer will wait after the kill before declaring the window unmeasurable.
  private static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(60);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Client_session_loss_during_backup_releases_ownership_only_after_the_backup_has_stopped()
  {
    await using var fixture = await SessionLossFixture.CreateAsync();

    var trial = await fixture.RunTrialAsync(ObservationWindow);

    // Emitted whether the trial passes or fails: this is the measured evidence behind an ADR gate, and it
    // should be readable from a test run rather than reconstructed from an assertion message.
    output.WriteLine("ADR-022 session-loss trial: " + trial);

    // The trial must have genuinely caught a backup in flight; a backup that finished before the kill would
    // prove nothing, so that is reported as inconclusive rather than quietly passing.
    Assert.True(trial.BackupObservedInFlight,
      "the backup was never observed in flight, so session loss was not exercised: " + trial);
    Assert.True(trial.ClientKilledDuringBackup,
      "the client was not killed while the backup was running: " + trial);

    // EXIT CONDITION A: at the moment a second worker acquired ownership, no BACKUP was still running
    // against the database. If this holds, session-scoped ownership alone is sufficient for V1.
    //
    // If it does NOT hold, the in-flight guard in SqlServerTenantDatabaseBackupProvider is what closes the
    // window — and it is enabled by default precisely because this outcome could not be assumed in advance.
    Assert.True(trial.OwnershipReacquired,
      "a second worker could not acquire ownership after the client died: " + trial);

    Assert.False(trial.BackupStillRunningWhenOwnershipReacquired,
      "OVERLAP WINDOW OBSERVED — exit condition A failed, so the in-flight guard is mandatory: " + trial);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_in_flight_guard_detects_a_backup_started_by_another_session()
  {
    // Exit condition B's mechanism, proven independently of which way condition A falls. The guard reads
    // sys.dm_exec_requests, which on SQL Server 2022 requires VIEW SERVER PERFORMANCE STATE — the granular
    // successor to VIEW SERVER STATE, and not sysadmin.
    await using var fixture = await SessionLossFixture.CreateAsync();

    using var worker = fixture.StartBackupWorker();
    var detected = await fixture.WaitForInFlightDetectionAsync(TimeSpan.FromSeconds(30));

    // Stop the worker regardless of outcome so the fixture can drop the catalog.
    SessionLossFixture.KillWorker(worker);

    Assert.True(detected, "the in-flight guard did not observe a backup started by another session");
  }

  private sealed class SessionLossFixture : IAsyncDisposable
  {
    // Enough data that a backup takes long enough to observe and interrupt. Deliberately not enormous: the
    // experiment needs a few seconds of runway, not a realistic production volume.
    private const int FillerRows = 90_000;

    private readonly string catalog;
    private readonly string backupRoot;

    private SessionLossFixture(string catalog, string backupRoot)
    {
      this.catalog = catalog;
      this.backupRoot = backupRoot;
    }

    public static async Task<SessionLossFixture> CreateAsync()
    {
      var runId = Guid.NewGuid().ToString("N");
      var fixture = new SessionLossFixture(
        $"SSAS_ERP_BACKUPPROOF_{runId}",
        Path.Combine(TenantBackupProviderSqlServerTests.BackupFixture.TestBackupRoot(), runId));

      try
      {
        Directory.CreateDirectory(fixture.backupRoot);

        // SIMPLE recovery while loading, so filling the database does not also fill the log. The experiment
        // is about session behaviour during BACKUP DATABASE, which SIMPLE supports.
        await ExecuteAsync("master", $"CREATE DATABASE [{fixture.catalog}]");
        await ExecuteAsync("master", $"ALTER DATABASE [{fixture.catalog}] SET RECOVERY SIMPLE");
        await ExecuteAsync(fixture.catalog,
          "CREATE TABLE dbo.Filler (Id int IDENTITY(1,1) NOT NULL, Payload char(8000) NOT NULL)");
        await ExecuteAsync(fixture.catalog,
          "INSERT INTO dbo.Filler (Payload) " +
          $"SELECT TOP ({FillerRows.ToString(CultureInfo.InvariantCulture)}) REPLICATE('x', 8000) " +
          "FROM sys.all_columns AS a CROSS JOIN sys.all_columns AS b", timeoutSeconds: 600);

        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    // Worker A: an independent PROCESS that takes session-scoped ownership and starts a backup. A separate
    // process is what makes the kill genuinely abrupt — disposing a connection from inside the test would be
    // a graceful close and would answer a weaker question.
    public Process StartBackupWorker()
    {
      var path = Path.Combine(backupRoot, $"proof_{Guid.NewGuid():N}.bak");
      var batch =
        "DECLARE @r int; " +
        "EXEC @r = sys.sp_getapplock @Resource = N'SSAS.TenantStorage.Backup', " +
        "@LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 30000; " +
        "IF @r < 0 RAISERROR('ownership not acquired', 16, 1); " +
        $"BACKUP DATABASE [{catalog}] TO DISK = N'{path}' WITH INIT, CHECKSUM;";

      var start = new ProcessStartInfo("sqlcmd")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      start.ArgumentList.Add("-S");
      start.ArgumentList.Add(ServerName());
      start.ArgumentList.Add("-E");
      start.ArgumentList.Add("-C");
      start.ArgumentList.Add("-d");
      start.ArgumentList.Add(catalog);
      start.ArgumentList.Add("-Q");
      start.ArgumentList.Add(batch);

      return Process.Start(start)!;
    }

    public static void KillWorker(Process worker)
    {
      try
      {
        if (!worker.HasExited)
        {
          // Abrupt: no graceful connection close, no server-side KILL. The TCP connection simply vanishes,
          // which is what a crashing platform worker looks like to SQL Server.
          worker.Kill(entireProcessTree: true);
        }
      }
      catch (InvalidOperationException)
      {
      }
    }

    public async Task<TrialResult> RunTrialAsync(TimeSpan observationWindow)
    {
      using var worker = StartBackupWorker();

      // Wait until the BACKUP is genuinely running before killing anything.
      var observedInFlight = await WaitForInFlightDetectionAsync(TimeSpan.FromSeconds(30));
      var killedDuringBackup = observedInFlight && !worker.HasExited;
      KillWorker(worker);

      var stopwatch = Stopwatch.StartNew();
      TimeSpan? backupGoneAt = null;
      TimeSpan? ownershipAt = null;
      bool backupRunningAtAcquisition = false;

      await using var observer = new SqlConnection(NonPooled(ConnectionFor(catalog)));
      await observer.OpenAsync();

      while (stopwatch.Elapsed < observationWindow)
      {
        var backupPresent = await IsBackupRunningAsync(observer);
        if (!backupPresent && backupGoneAt is null)
        {
          backupGoneAt = stopwatch.Elapsed;
        }

        if (ownershipAt is null && await TryAcquireOwnershipAsync(observer))
        {
          ownershipAt = stopwatch.Elapsed;

          // THE DECISIVE MEASUREMENT: at the instant a second worker owns the database, is a backup still
          // running? A "yes" is the overlap window ADR-022 §14 asks about.
          backupRunningAtAcquisition = await IsBackupRunningAsync(observer);
        }

        if (backupGoneAt is not null && ownershipAt is not null)
        {
          break;
        }

        await Task.Delay(10);
      }

      return new TrialResult(
        observedInFlight,
        killedDuringBackup,
        ownershipAt is not null,
        backupRunningAtAcquisition,
        backupGoneAt,
        ownershipAt);
    }

    // The same evidence the provider's in-flight guard uses.
    public async Task<bool> WaitForInFlightDetectionAsync(TimeSpan timeout)
    {
      await using var connection = new SqlConnection(NonPooled(ConnectionFor(catalog)));
      await connection.OpenAsync();

      var stopwatch = Stopwatch.StartNew();
      while (stopwatch.Elapsed < timeout)
      {
        if (await IsBackupRunningAsync(connection))
        {
          return true;
        }

        await Task.Delay(10);
      }

      return false;
    }

    private static async Task<bool> IsBackupRunningAsync(SqlConnection connection)
    {
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT COUNT(*) FROM sys.dm_exec_requests " +
        "WHERE database_id = DB_ID() AND command LIKE 'BACKUP%' AND session_id <> @@SPID";
      return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<bool> TryAcquireOwnershipAsync(SqlConnection connection)
    {
      await using var command = connection.CreateCommand();
      command.CommandText = "sys.sp_getapplock";
      command.CommandType = System.Data.CommandType.StoredProcedure;
      command.Parameters.AddWithValue("@Resource", "SSAS.TenantStorage.Backup");
      command.Parameters.AddWithValue("@LockMode", "Exclusive");
      command.Parameters.AddWithValue("@LockOwner", "Session");
      command.Parameters.AddWithValue("@LockTimeout", 0);
      var result = command.Parameters.Add("@Result", System.Data.SqlDbType.Int);
      result.Direction = System.Data.ParameterDirection.ReturnValue;

      await command.ExecuteNonQueryAsync();
      return (int)result.Value is 0 or 1;
    }

    private static string ServerName()
    {
      var builder = new SqlConnectionStringBuilder(
        TenantBackupProviderSqlServerTests.BackupFixture.Configured());
      return string.IsNullOrWhiteSpace(builder.DataSource) ? "localhost" : builder.DataSource;
    }

    private static string ConnectionFor(string catalog) =>
      TenantBackupProviderSqlServerTests.BackupFixture.ConnectionFor(catalog);

    private static string NonPooled(string connectionString) =>
      new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

    private static async Task ExecuteAsync(string catalog, string sql, int timeoutSeconds = 120)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = timeoutSeconds;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      try
      {
        await ExecuteAsync("master",
          $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN ALTER DATABASE [{catalog}] " +
          $"SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]; END", 300);
      }
      catch (SqlException)
      {
      }

      // Scoped to this run's own folder. No wildcard sweep, and no xp_delete_file.
      try
      {
        if (Directory.Exists(backupRoot))
        {
          Directory.Delete(backupRoot, recursive: true);
        }
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }
    }
  }

  private sealed record TrialResult(
    bool BackupObservedInFlight,
    bool ClientKilledDuringBackup,
    bool OwnershipReacquired,
    bool BackupStillRunningWhenOwnershipReacquired,
    TimeSpan? BackupGoneAt,
    TimeSpan? OwnershipAt)
  {
    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
      $"inFlight={BackupObservedInFlight}, killedDuringBackup={ClientKilledDuringBackup}, " +
      $"ownershipReacquired={OwnershipReacquired}, backupStillRunningAtAcquisition={BackupStillRunningWhenOwnershipReacquired}, " +
      $"backupGoneAtMs={BackupGoneAt?.TotalMilliseconds:F0}, ownershipAtMs={OwnershipAt?.TotalMilliseconds:F0}");
  }
}

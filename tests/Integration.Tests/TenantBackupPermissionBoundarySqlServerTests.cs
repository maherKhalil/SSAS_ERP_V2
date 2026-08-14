using System.Globalization;
using Microsoft.Data.SqlClient;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE LEAST-PRIVILEGE PERMISSION BOUNDARY (ADR-022 §11, §14).
//
// The focused Phase B review found that the in-flight guard failed OPEN: sys.dm_exec_requests does not
// refuse a caller lacking server-wide visibility, it silently narrows to that caller's own session, so an
// empty result was being read as "no backup in flight". No amount of reading the provider could settle it,
// and no test that re-implements the query could either — the defect lived in the INTERPRETATION of a
// result, under a privilege level the test environment never exercised.
//
// So these tests run the PRODUCTION visibility and in-flight code under a genuinely low-privilege principal.
//
// HOW THE PRINCIPAL IS OBTAINED. This instance is Windows-authentication-only, so a disposable SQL login
// cannot connect. It can still be IMPERSONATED: `EXECUTE AS LOGIN` switches the session's security context
// without authenticating, and server-level permission checks and DMV visibility filtering both honour it.
// That gives a faithful low-privilege token without changing the instance's authentication mode, which
// would be a machine-wide security change made to satisfy a test.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantBackupPermissionBoundarySqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Without_the_server_permission_the_provider_cannot_establish_in_flight_visibility()
  {
    // THE REGRESSION TEST FOR MEDIUM-1. Before the fix this identity's DMV query returned zero rows with no
    // error, and the guard read that as "nothing running" and proceeded to back up.
    await using var principal = await LowPrivilegePrincipal.CreateAsync(grantVisibility: false);

    await using var connection = await principal.OpenImpersonatedAsync();

    // The production check, not a copy of it.
    Assert.False(await SqlServerBackupVisibility.HasInFlightVisibilityAsync(connection));

    // And the reason it is needed: the DMV does not throw here. It quietly returns only this session, so
    // "no rows" carries no information at all about what else is running.
    var visibleOtherSessions = await ScalarAsync(connection,
      "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id <> @@SPID");
    Assert.Equal(0, visibleOtherSessions);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_backup_identity_needs_neither_sysadmin_nor_the_broad_server_state_permission()
  {
    // ADR-022 §11: least privilege is a design constraint, not an aspiration. This pins the exact boundary —
    // the granular permission is enough, and the two broader grants are genuinely not required.
    await using var principal = await LowPrivilegePrincipal.CreateAsync(grantVisibility: true);

    await using var connection = await principal.OpenImpersonatedAsync();

    Assert.Equal(0, await ScalarAsync(connection, "SELECT CAST(IS_SRVROLEMEMBER('sysadmin') AS int)"));
    Assert.Equal(0, await ScalarAsync(connection,
      "SELECT CAST(HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE') AS int)"));

    // db_backupoperator on the target database, which is what actually authorises BACKUP.
    Assert.Equal(1, await ScalarAsync(connection,
      "SELECT CAST(IS_ROLEMEMBER('db_backupoperator') AS int)"));

    Assert.True(await SqlServerBackupVisibility.HasInFlightVisibilityAsync(connection));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task With_only_the_granular_permission_the_guard_sees_another_sessions_backup()
  {
    // The positive half: the permission the platform actually asks deployment for is SUFFICIENT to observe
    // a backup started by somebody else — a DBA, SQL Agent, or a second platform worker.
    await using var principal = await LowPrivilegePrincipal.CreateAsync(grantVisibility: true);
    await principal.FillAsync();

    using var competing = principal.StartCompetingBackup();
    try
    {
      await using var connection = await principal.OpenImpersonatedAsync();

      var observed = false;
      var deadline = DateTime.UtcNow.AddSeconds(30);
      while (DateTime.UtcNow < deadline)
      {
        // The PRODUCTION in-flight check, running as the low-privilege principal.
        if (await SqlServerBackupVisibility.IsBackupInFlightAsync(connection))
        {
          observed = true;
          break;
        }

        if (competing.HasExited)
        {
          break;
        }

        await Task.Delay(10);
      }

      Assert.True(observed,
        "the low-privilege guard never observed the competing backup, so the permission boundary is unproven");
    }
    finally
    {
      Kill(competing);
    }
  }

  private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    var value = await command.ExecuteScalarAsync();
    return value is null or DBNull ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
  }

  private static void Kill(System.Diagnostics.Process process)
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

  // A disposable login, a database user, db_backupoperator, and optionally the one server-level permission
  // the in-flight guard requires. Everything it creates, it removes.
  private sealed class LowPrivilegePrincipal : IAsyncDisposable
  {
    private readonly string loginName;
    private readonly bool visibilityGranted;

    private LowPrivilegePrincipal(string loginName, string catalog, string backupRoot, bool visibilityGranted)
    {
      this.loginName = loginName;
      this.visibilityGranted = visibilityGranted;
      Catalog = catalog;
      BackupRoot = backupRoot;
    }

    public string Catalog { get; }

    public string BackupRoot { get; }

    public static async Task<LowPrivilegePrincipal> CreateAsync(bool grantVisibility)
    {
      var runId = Guid.NewGuid().ToString("N");
      var principal = new LowPrivilegePrincipal(
        $"SSAS_BackupLowPriv_{runId}",
        $"SSAS_ERP_BACKUPPERM_{runId}",
        Path.Combine(TenantBackupProviderSqlServerTests.BackupFixture.TestBackupRoot(), runId),
        grantVisibility);

      try
      {
        Directory.CreateDirectory(principal.BackupRoot);
        await ExecuteAsync("master", $"CREATE DATABASE [{principal.Catalog}]");

        // The password is required syntactically to create the login; it is never used, because the login
        // cannot connect on a Windows-authentication-only instance and is only ever impersonated.
        await ExecuteAsync("master",
          $"CREATE LOGIN [{principal.loginName}] WITH PASSWORD = '{Guid.NewGuid():N}aA1!', CHECK_POLICY = OFF");

        // BACKUP authority on the target database, and nothing else.
        await ExecuteAsync(principal.Catalog, $"CREATE USER [{principal.loginName}] FOR LOGIN [{principal.loginName}]");
        await ExecuteAsync(principal.Catalog,
          $"ALTER ROLE db_backupoperator ADD MEMBER [{principal.loginName}]");

        if (grantVisibility)
        {
          // The single server-level grant the platform asks deployment for. Never VIEW SERVER STATE, never
          // a server role.
          await ExecuteAsync("master",
            $"GRANT {SqlServerBackupVisibility.RequiredPermission} TO [{principal.loginName}]");
        }

        return principal;
      }
      catch
      {
        await principal.DisposeAsync();
        throw;
      }
    }

    // Enough data that a competing backup lasts long enough to be observed. Batched and checkpointed under
    // SIMPLE recovery — a single large insert in one transaction exhausted the buffer pool on this host.
    public async Task FillAsync()
    {
      await ExecuteAsync("master", $"ALTER DATABASE [{Catalog}] SET RECOVERY SIMPLE");
      await ExecuteAsync(Catalog,
        "CREATE TABLE dbo.Filler (Id int IDENTITY(1,1) NOT NULL, Payload char(8000) NOT NULL)");
      await ExecuteAsync(Catalog,
        "DECLARE @batch int = 0; " +
        "WHILE @batch < 6 BEGIN " +
        "  INSERT INTO dbo.Filler (Payload) SELECT TOP (5000) REPLICATE('x', 8000) " +
        "  FROM sys.all_columns AS a CROSS JOIN sys.all_columns AS b; " +
        "  CHECKPOINT; SET @batch += 1; END", 600);
    }

    // A connection in the target database whose SECURITY CONTEXT is the low-privilege principal. The
    // connection authenticates as the test's Windows identity and then drops to the impersonated token, so
    // every permission check after this line is evaluated against the low-privilege login.
    public async Task<SqlConnection> OpenImpersonatedAsync()
    {
      var builder = new SqlConnectionStringBuilder(
        TenantBackupProviderSqlServerTests.BackupFixture.ConnectionFor(Catalog))
      {
        Pooling = false
      };

      var connection = new SqlConnection(builder.ConnectionString);
      await connection.OpenAsync();

      await using var command = connection.CreateCommand();
      command.CommandText = $"EXECUTE AS LOGIN = '{loginName}'";
      await command.ExecuteNonQueryAsync();

      return connection;
    }

    // A backup issued by a DIFFERENT process entirely, as a DBA or SQL Agent job would be.
    public System.Diagnostics.Process StartCompetingBackup()
    {
      var path = Path.Combine(BackupRoot, $"competing_{Guid.NewGuid():N}.bak");
      var builder = new SqlConnectionStringBuilder(
        TenantBackupProviderSqlServerTests.BackupFixture.Configured());
      var server = string.IsNullOrWhiteSpace(builder.DataSource) ? "localhost" : builder.DataSource;

      var start = new System.Diagnostics.ProcessStartInfo("sqlcmd")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      start.ArgumentList.Add("-S");
      start.ArgumentList.Add(server);
      start.ArgumentList.Add("-E");
      start.ArgumentList.Add("-C");
      start.ArgumentList.Add("-d");
      start.ArgumentList.Add(Catalog);
      start.ArgumentList.Add("-Q");
      start.ArgumentList.Add($"BACKUP DATABASE [{Catalog}] TO DISK = N'{path}' WITH INIT, CHECKSUM");

      return System.Diagnostics.Process.Start(start)!;
    }

    private static async Task ExecuteAsync(string catalog, string sql, int timeoutSeconds = 120)
    {
      await using var connection = new SqlConnection(
        TenantBackupProviderSqlServerTests.BackupFixture.ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = timeoutSeconds;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      // NO PRIVILEGED TEST PRINCIPAL IS LEFT BEHIND. The server-level grant goes with the login, but it is
      // revoked explicitly first so a failure to drop the login cannot leave a permission attached to it.
      if (visibilityGranted)
      {
        await TryAsync("master", $"REVOKE {SqlServerBackupVisibility.RequiredPermission} FROM [{loginName}]");
      }

      await TryAsync("master",
        $"IF DB_ID(N'{Catalog}') IS NOT NULL BEGIN ALTER DATABASE [{Catalog}] " +
        $"SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Catalog}]; END");

      await TryAsync("master", $"IF SUSER_ID(N'{loginName}') IS NOT NULL DROP LOGIN [{loginName}]");

      try
      {
        if (Directory.Exists(BackupRoot))
        {
          Directory.Delete(BackupRoot, recursive: true);
        }
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }
    }

    private static async Task TryAsync(string catalog, string sql)
    {
      try
      {
        await ExecuteAsync(catalog, sql, 300);
      }
      catch (SqlException)
      {
      }
    }
  }
}

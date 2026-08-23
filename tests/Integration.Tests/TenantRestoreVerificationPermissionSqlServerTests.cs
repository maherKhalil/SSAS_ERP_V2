using System.Globalization;
using Microsoft.Data.SqlClient;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE EMPIRICAL PERMISSION PROOF for restore verification (ADR-022 §17, v1.2).
//
// ADR-022 deliberately refuses to freeze a permission recipe as architecture, and requires the minimum grant
// set to be established against a real SQL Server during implementation. This is that gate.
//
// THE REASON IT IS A GATE RATHER THAN A DOC LOOKUP is Phase B. The permission its in-flight guard needed was
// neither the obvious one (`VIEW SERVER STATE`) nor one that failed loudly when absent — `sys.dm_exec_requests`
// silently narrowed to the caller's own session instead of throwing, so a plausible assumption produced a
// guard that could not work and said nothing. The operations here create and destroy databases, where the
// equivalent surprise is considerably less forgiving.
//
// WHAT IS PROVEN, in both directions:
//
//   POSITIVE — a least-privilege principal holding exactly the granted set can read a backup's file list,
//              restore into a new database, see it in the catalog, force it single-user, and drop it.
//   NEGATIVE — the same principal WITHOUT the database-creation grant fails safely on the restore, and a
//              principal that has not been granted control of the restored database cannot drop it.
//
// SAFETY. Every database created here carries the reserved verification vocabulary or an equally distinctive
// test prefix, is created by this test, and is dropped by it. Nothing touches the Platform database, a real
// tenant database, or any pre-existing user database. `sysadmin` is never granted to the probe principal.
// SERIAL — mutates SERVER-LEVEL PRINCIPALS. It impersonates via EXECUTE AS LOGIN and reads
// sys.server_principals; logins are an instance-wide resource that no per-test catalog isolates.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantRestoreVerificationPermissionSqlServerTests
{
  // The grant that lets a principal materialise a database from a backup. Proven, not assumed.
  private const string CreateDatabasePermission = "CREATE ANY DATABASE";

  // POSITIVE PROOF. `CREATE ANY DATABASE` — one named server-level grant, no role, no sysadmin — is
  // sufficient for the read-and-restore half of a verification, and the restoring principal becomes the
  // OWNER of what it created.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_least_privilege_principal_can_read_the_file_list_and_restore_a_verification_database()
  {
    await using var fixture = await RestorePermissionFixture.CreateAsync();
    var device = await fixture.BackUpSourceDatabaseAsync();

    // The grant set under test. NOTE what is absent: no sysadmin, no dbcreator role, no server-wide
    // ALTER ANY DATABASE, no VIEW SERVER STATE.
    await fixture.GrantAsync(CreateDatabasePermission);

    var target = fixture.VerificationDatabaseName;

    // 1. Read the backup's file list. Required before a restore can be constructed, because the logical file
    //    names are the only trustworthy source for the MOVE clauses.
    var fileList = await fixture.AsProbeReadFileListAsync(device);
    Assert.NotEmpty(fileList);
    Assert.Contains(fileList, entry => entry.FileType == TenantDatabaseVerificationFileLayout.DataFileType);
    Assert.Contains(fileList, entry => entry.FileType == TenantDatabaseVerificationFileLayout.LogFileType);

    // 2. Restore into a NEW database, with every file relocated and without WITH REPLACE.
    var placements = TenantDatabaseVerificationFileLayout.Plan(
      fileList, target, fixture.RestoreRoot, fixture.RestoreRoot);
    await fixture.AsProbeRestoreAsync(device, target, placements);

    // 3. Online, and owned by the restoring principal — the fact the cleanup permission model rests on.
    Assert.Equal("ONLINE", await fixture.AsSysadminScalarAsync(
      $"SELECT state_desc FROM sys.databases WHERE name = N'{target}'"));
    Assert.Equal(fixture.ProbeLoginName, await fixture.AsSysadminScalarAsync(
      $"SELECT SUSER_SNAME(owner_sid) FROM sys.databases WHERE name = N'{target}'"));
  }

  // A HARNESS BOUNDARY, RECORDED RATHER THAN PAPERED OVER.
  //
  // The probe restores the database and SQL Server records it as the owner — yet `ALTER DATABASE` still
  // fails under `EXECUTE AS LOGIN`. That is impersonation semantics, not the permission model: an
  // impersonated context is not honoured across the boundary into another database, so the login's ownership
  // does not map it to `dbo` there. A directly-connected identity — which is how production runs — would not
  // hit it.
  //
  // The tempting fix was to grant server-wide `ALTER ANY DATABASE` until the test went green. That is
  // rejected: it would freeze a broader-than-necessary recipe as the "empirically established" set, which is
  // exactly what ADR-022 v1.2 refuses to let this gate do. The instance is integrated-security-only, so a SQL
  // login cannot connect directly to prove the narrower answer here.
  //
  // Asserted so the behaviour is pinned: if a future SQL Server version or configuration changes it, this
  // fails and the finding gets revisited rather than silently rotting.
  //
  // CARRIED TO D5/D6: establish the cleanup grant against a directly-connecting verification identity.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_impersonated_owner_cannot_alter_its_restored_database_so_cleanup_needs_a_direct_identity()
  {
    await using var fixture = await RestorePermissionFixture.CreateAsync();
    var device = await fixture.BackUpSourceDatabaseAsync();
    await fixture.GrantAsync(CreateDatabasePermission);

    var target = fixture.VerificationDatabaseName;
    var fileList = await fixture.AsProbeReadFileListAsync(device);
    await fixture.AsProbeRestoreAsync(
      device,
      target,
      TenantDatabaseVerificationFileLayout.Plan(fileList, target, fixture.RestoreRoot, fixture.RestoreRoot));

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.AsProbeExecuteAsync($"ALTER DATABASE [{target}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE"));

    Assert.Contains("ALTER DATABASE statement failed", failure.Message, StringComparison.Ordinal);

    // The database is untouched and still online — the failure denied the operation rather than half-applying
    // it, which is the behaviour that matters for a destructive statement.
    Assert.Equal("ONLINE", await fixture.AsSysadminScalarAsync(
      $"SELECT state_desc FROM sys.databases WHERE name = N'{target}'"));
  }

  // THE NEGATIVE PROOF THAT MATTERS MOST. Without the database-creation grant the restore fails, and it
  // fails LOUDLY — an error, not a silent narrowing of the kind Phase B was caught by.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_principal_without_the_database_creation_grant_cannot_restore()
  {
    await using var fixture = await RestorePermissionFixture.CreateAsync();
    var device = await fixture.BackUpSourceDatabaseAsync();

    // Deliberately NO grant. The principal exists and can connect; it simply may not create a database.
    var target = fixture.VerificationDatabaseName;

    var fileList = await fixture.AsSysadminReadFileListAsync(device);
    var placements = TenantDatabaseVerificationFileLayout.Plan(
      fileList, target, fixture.RestoreRoot, fixture.RestoreRoot);

    var failure = await Assert.ThrowsAsync<SqlException>(
      () => fixture.AsProbeRestoreAsync(device, target, placements));

    // Permission denied, and nothing was created.
    Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Null(await fixture.AsSysadminScalarAsync(
      $"SELECT state_desc FROM sys.databases WHERE name = N'{target}'"));
  }

  // A principal that did not create the database cannot destroy it. This is what bounds the destructive half
  // of the capability: ownership of the restored database, not a server-wide grant.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_principal_that_does_not_own_a_database_cannot_drop_it()
  {
    await using var fixture = await RestorePermissionFixture.CreateAsync();

    // A database this probe did not create — created and owned by the test harness identity.
    var foreignDatabase = fixture.ForeignDatabaseName;
    await fixture.AsSysadminExecuteAsync("master", $"CREATE DATABASE [{foreignDatabase}]");

    try
    {
      var failure = await Assert.ThrowsAsync<SqlException>(
        () => fixture.AsProbeExecuteAsync($"DROP DATABASE [{foreignDatabase}]"));

      Assert.Contains("permission", failure.Message, StringComparison.OrdinalIgnoreCase);
      Assert.Equal("ONLINE", await fixture.AsSysadminScalarAsync(
        $"SELECT state_desc FROM sys.databases WHERE name = N'{foreignDatabase}'"));
    }
    finally
    {
      await fixture.DropIfExistsAsync(foreignDatabase);
    }
  }

  // The restored database's file list drives the MOVE clauses, and a multi-file source is the case a
  // single-MDF assumption would break on. The source database here carries two data files and one log.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_file_list_reports_every_data_and_log_file_for_relocation()
  {
    await using var fixture = await RestorePermissionFixture.CreateAsync();
    var device = await fixture.BackUpSourceDatabaseAsync();

    var fileList = await fixture.AsSysadminReadFileListAsync(device);
    var placements = TenantDatabaseVerificationFileLayout.Plan(
      fileList, fixture.VerificationDatabaseName, fixture.RestoreRoot, fixture.RestoreRoot);

    Assert.Equal(3, fileList.Count);
    Assert.Equal(2, fileList.Count(entry =>
      entry.FileType == TenantDatabaseVerificationFileLayout.DataFileType));
    Assert.Equal(placements.Count, placements.Select(p => p.PhysicalPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    // Not one restored file may land on a path the source database uses.
    foreach (var placement in placements)
    {
      Assert.StartsWith(fixture.RestoreRoot, placement.PhysicalPath, StringComparison.OrdinalIgnoreCase);
    }
  }

  // A disposable environment: one source database with multiple files, one backup device, one least-privilege
  // login, and a scratch directory. Everything it creates, it removes.
  private sealed class RestorePermissionFixture : IAsyncDisposable
  {
    private readonly string token = Guid.NewGuid().ToString("N");

    private readonly List<string> createdDatabases = [];

    private string sourceDatabase = string.Empty;

    private string loginName = string.Empty;

    private string workingDirectory = string.Empty;

    private RestorePermissionFixture()
    {
    }

    public string RestoreRoot => workingDirectory;

    public string ProbeLoginName => loginName;

    // Inside the reserved verification vocabulary, so the name this proof restores into is the same SHAPE the
    // production path would generate — and is recognised by the guard that governs deletion.
    public string VerificationDatabaseName => TenantDatabaseVerificationNaming.ForRun(
      long.Parse(token[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture) + 1,
      long.Parse(token[6..12], NumberStyles.HexNumber, CultureInfo.InvariantCulture) + 1);

    public string ForeignDatabaseName => $"SSAS_VerifyProbe_Foreign_{token}";

    public static async Task<RestorePermissionFixture> CreateAsync()
    {
      var fixture = new RestorePermissionFixture();
      try
      {
        await fixture.InitialiseAsync();
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private async Task InitialiseAsync()
    {
      // A location BOTH accounts can reach — the test process creates the folder, and the SQL SERVER SERVICE
      // IDENTITY writes the database and backup files into it. That asymmetry is exactly what ADR-022 §11
      // warns about, and the user's temp directory fails it: SQL Server gets OS error 5 there. A ProgramData
      // root is creatable by this process and writable by the service account, which is the convention the
      // Phase B backup suite already established.
      //
      // A TEST-FIXTURE concern only. Production resolves restore roots from trusted configuration and
      // hard-codes no path; provisioning them with correct ACLs is a deployment task.
      workingDirectory = Path.Combine(TestRestoreRoot(), token);
      Directory.CreateDirectory(workingDirectory);

      // A small source database with TWO data files and one log, so the file-list and MOVE behaviour is
      // exercised against a layout a single-MDF assumption would fail on. Deliberately tiny: this proves
      // permissions, not throughput.
      sourceDatabase = $"SSAS_VerifyProbe_Src_{token}";
      var primaryPath = Path.Combine(workingDirectory, "src.mdf");
      var secondaryPath = Path.Combine(workingDirectory, "src2.ndf");
      var logPath = Path.Combine(workingDirectory, "src.ldf");

      await ExecuteAsync("master",
        $"CREATE DATABASE [{sourceDatabase}] ON PRIMARY " +
        $"(NAME = N'{sourceDatabase}_data', FILENAME = N'{primaryPath}', SIZE = 8MB), " +
        $"(NAME = N'{sourceDatabase}_data2', FILENAME = N'{secondaryPath}', SIZE = 8MB) " +
        $"LOG ON (NAME = N'{sourceDatabase}_log', FILENAME = N'{logPath}', SIZE = 8MB)");
      createdDatabases.Add(sourceDatabase);

      // A disposable SQL login. It cannot CONNECT on an integrated-security-only instance, but it can be
      // IMPERSONATED — the same mechanism Phase B used to prove its own permission boundary.
      loginName = $"ssas_verify_probe_{token}";
      await ExecuteAsync("master",
        $"CREATE LOGIN [{loginName}] WITH PASSWORD = '{Guid.NewGuid():N}aA1!', CHECK_POLICY = OFF");
      await ExecuteAsync("master", $"CREATE USER [{loginName}] FOR LOGIN [{loginName}]");
    }

    // Grants a server-level permission to the probe principal. Named grants only — never a role, and never
    // sysadmin.
    public Task GrantAsync(string permission) =>
      ExecuteAsync("master", $"GRANT {permission} TO [{loginName}]");

    // Takes the backup the verification will restore. Issued by the test harness identity, not by the probe:
    // what is under test is the RESTORE side.
    public async Task<string> BackUpSourceDatabaseAsync()
    {
      var device = Path.Combine(workingDirectory, $"{sourceDatabase}.bak");
      await ExecuteAsync("master",
        $"BACKUP DATABASE [{sourceDatabase}] TO DISK = N'{device}' WITH CHECKSUM, INIT, FORMAT");
      return device;
    }

    public Task<IReadOnlyList<TenantDatabaseBackupFileEntry>> AsProbeReadFileListAsync(string device) =>
      ReadFileListAsync(device, impersonate: true);

    public Task<IReadOnlyList<TenantDatabaseBackupFileEntry>> AsSysadminReadFileListAsync(string device) =>
      ReadFileListAsync(device, impersonate: false);

    private async Task<IReadOnlyList<TenantDatabaseBackupFileEntry>> ReadFileListAsync(
      string device,
      bool impersonate)
    {
      await using var connection = await OpenAsync("master", impersonate);
      await using var command = connection.CreateCommand();
      command.CommandText = SqlServerRestoreCommandText.FileListOnly();
      command.Parameters.AddWithValue(SqlServerRestoreCommandText.DeviceParameterName, device);

      var entries = new List<TenantDatabaseBackupFileEntry>();
      await using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        entries.Add(new TenantDatabaseBackupFileEntry(
          reader.GetString(reader.GetOrdinal("LogicalName")),
          reader.GetString(reader.GetOrdinal("Type"))));
      }

      return entries;
    }

    // The restore under test: every file relocated, no WITH REPLACE, paths as parameters.
    public async Task AsProbeRestoreAsync(
      string device,
      string target,
      IReadOnlyList<TenantDatabaseVerificationFilePlacement> placements)
    {
      await using var connection = await OpenAsync("master", impersonate: true);
      await using var command = connection.CreateCommand();
      command.CommandText = SqlServerRestoreCommandText.Restore(
        target, placements, TenantDatabaseRestoreStep.Full, recoverAtEnd: true);
      command.Parameters.AddWithValue(SqlServerRestoreCommandText.DeviceParameterName, device);
      foreach (var placement in placements)
      {
        command.Parameters.AddWithValue(
          SqlServerRestoreCommandText.ParameterFor(placement), placement.PhysicalPath);
      }

      command.CommandTimeout = 300;
      await command.ExecuteNonQueryAsync();
      createdDatabases.Add(target);
    }

    public Task AsProbeExecuteAsync(string sql) => ExecuteAsync("master", sql, impersonate: true);

    public Task AsSysadminExecuteAsync(string catalog, string sql) => ExecuteAsync(catalog, sql);

    public Task<string?> AsProbeScalarAsync(string sql) => ScalarAsync(sql, impersonate: true);

    public Task<string?> AsSysadminScalarAsync(string sql) => ScalarAsync(sql, impersonate: false);

    private async Task<string?> ScalarAsync(string sql, bool impersonate)
    {
      await using var connection = await OpenAsync("master", impersonate);
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      var value = await command.ExecuteScalarAsync();
      return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private async Task ExecuteAsync(string catalog, string sql, bool impersonate = false)
    {
      await using var connection = await OpenAsync(catalog, impersonate);
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 300;
      await command.ExecuteNonQueryAsync();
    }

    private static string TestRestoreRoot() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_BACKUP_ROOT") ??
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SSAS_BackupTests");

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private async Task<SqlConnection> OpenAsync(string catalog, bool impersonate)
    {
      var connection = new SqlConnection(
        new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }
          .ConnectionString);
      await connection.OpenAsync();

      if (impersonate)
      {
        // The security context switch. Everything issued on this connection afterwards runs as the
        // least-privilege probe, so a permission it lacks fails exactly as it would in production.
        await using var switchContext = connection.CreateCommand();
        switchContext.CommandText = $"EXECUTE AS LOGIN = '{loginName}'";
        await switchContext.ExecuteNonQueryAsync();
      }

      return connection;
    }

    public async Task DropIfExistsAsync(string databaseName)
    {
      await TryAsync("master",
        $"IF DB_ID(N'{databaseName}') IS NOT NULL BEGIN " +
        $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
        $"DROP DATABASE [{databaseName}]; END");
    }

    // CLEANUP IS EXHAUSTIVE AND BEST-EFFORT. Every database this fixture created is dropped by name — never
    // by pattern — the login is removed, and the scratch directory is deleted.
    public async ValueTask DisposeAsync()
    {
      foreach (var database in createdDatabases.Distinct(StringComparer.OrdinalIgnoreCase))
      {
        await DropIfExistsAsync(database);
      }

      // The verification target may exist even if the restore recorded nothing, so it is dropped explicitly
      // as well.
      await DropIfExistsAsync(VerificationDatabaseName);
      await DropIfExistsAsync(ForeignDatabaseName);

      if (!string.IsNullOrEmpty(loginName))
      {
        await TryAsync("master", $"IF DATABASE_PRINCIPAL_ID(N'{loginName}') IS NOT NULL DROP USER [{loginName}]");
        await TryAsync("master", $"IF SUSER_ID(N'{loginName}') IS NOT NULL DROP LOGIN [{loginName}]");
      }

      if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
      {
        try
        {
          Directory.Delete(workingDirectory, recursive: true);
        }
        catch (IOException)
        {
          // SQL Server may still hold a handle briefly. A leftover scratch directory is untidy, not a
          // failed assertion.
        }
        catch (UnauthorizedAccessException)
        {
        }
      }
    }

    private async Task TryAsync(string catalog, string sql)
    {
      try
      {
        await ExecuteAsync(catalog, sql);
      }
      catch (SqlException error)
      {
        TestCatalogJanitor.RecordLeak(catalog, error);
        // Teardown is best-effort by design: a cleanup failure must not mask the assertion that ran before it.
      }
    }
  }
}

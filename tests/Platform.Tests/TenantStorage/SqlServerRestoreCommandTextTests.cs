using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// Restore command construction and file layout (ADR-022 §17, v1.2).
//
// Two invariants carry this file: `WITH REPLACE` must be unreachable, and every logical file must be
// redirected. Both are the kind of thing that is obvious in isolation and easy to lose inside a longer
// method, which is why they live in a small unit with tests pointed straight at them.
[Trait("Decision", "ADR-022")]
public sealed class SqlServerRestoreCommandTextTests
{
  private const string VerificationDatabase = "SSAS_Verify_42_7";

  // THE INVARIANT. `REPLACE` is the clause that turns a mistargeted restore into the destruction of an
  // existing database, and there is no parameter, overload or branch that can introduce it.
  [Fact]
  public void No_restore_step_ever_emits_with_replace()
  {
    // Every step and both recovery modes, because `REPLACE` appearing on any one of them would be enough.
    foreach (var step in new[]
      {
        TenantDatabaseRestoreStep.Full,
        TenantDatabaseRestoreStep.Differential,
        TenantDatabaseRestoreStep.Log
      })
    {
      foreach (var recover in new[] { true, false })
      {
        var command = SqlServerRestoreCommandText.Restore(
          VerificationDatabase, Placements(), step, recover);

        Assert.DoesNotContain("REPLACE", command, StringComparison.OrdinalIgnoreCase);
      }
    }
  }

  [Fact]
  public void The_full_restore_moves_every_logical_file()
  {
    var placements = Placements();

    var command = SqlServerRestoreCommandText.Restore(
      VerificationDatabase, placements, TenantDatabaseRestoreStep.Full, recoverAtEnd: false);

    foreach (var placement in placements)
    {
      Assert.Contains(
        $"MOVE N'{placement.LogicalName}' TO {SqlServerRestoreCommandText.ParameterFor(placement)}",
        command,
        StringComparison.Ordinal);
    }
  }

  // Paths travel as parameters. A physical path must never appear in the command text, or configuration
  // would become an injection surface.
  [Fact]
  public void Physical_paths_are_never_interpolated_into_the_command()
  {
    var placements = Placements();

    var command = SqlServerRestoreCommandText.Restore(
      VerificationDatabase, placements, TenantDatabaseRestoreStep.Full, recoverAtEnd: false);

    foreach (var placement in placements)
    {
      Assert.DoesNotContain(placement.PhysicalPath, command, StringComparison.OrdinalIgnoreCase);
    }
  }

  // A chain must not be recovered until its final segment has been applied, or the remaining segments become
  // unrestorable (ADR-022 §17, Level C).
  [Fact]
  public void Intermediate_steps_use_norecovery_and_only_the_last_recovers()
  {
    var intermediate = SqlServerRestoreCommandText.Restore(
      VerificationDatabase, Placements(), TenantDatabaseRestoreStep.Full, recoverAtEnd: false);
    var final = SqlServerRestoreCommandText.Restore(
      VerificationDatabase, Placements(), TenantDatabaseRestoreStep.Log, recoverAtEnd: true);

    Assert.EndsWith("NORECOVERY", intermediate, StringComparison.Ordinal);
    Assert.EndsWith("RECOVERY", final, StringComparison.Ordinal);
    Assert.DoesNotContain("NORECOVERY", final, StringComparison.Ordinal);
  }

  [Fact]
  public void A_log_step_uses_restore_log()
  {
    var command = SqlServerRestoreCommandText.Restore(
      VerificationDatabase, Placements(), TenantDatabaseRestoreStep.Log, recoverAtEnd: true);

    Assert.StartsWith("RESTORE LOG ", command, StringComparison.Ordinal);
  }

  [Fact]
  public void The_database_name_is_quoted_as_an_identifier()
  {
    var command = SqlServerRestoreCommandText.Restore(
      VerificationDatabase, Placements(), TenantDatabaseRestoreStep.Full, recoverAtEnd: true);

    Assert.Contains($"[{VerificationDatabase}]", command, StringComparison.Ordinal);
  }

  // Logical names come from the BACKUP rather than from this platform — the one input in the command that
  // neither configuration nor generation controls — so the literal escape is exercised directly.
  [Fact]
  public void A_logical_file_name_containing_a_quote_is_escaped()
  {
    var quoted = SqlServerRestoreCommandText.QuoteLiteral("tenant's data");

    Assert.Equal("N'tenant''s data'", quoted);
  }

  [Fact]
  public void File_list_and_header_reads_take_the_device_as_a_parameter()
  {
    Assert.Contains(
      SqlServerRestoreCommandText.DeviceParameterName,
      SqlServerRestoreCommandText.FileListOnly(),
      StringComparison.Ordinal);
    Assert.Contains(
      SqlServerRestoreCommandText.DeviceParameterName,
      SqlServerRestoreCommandText.HeaderOnly(),
      StringComparison.Ordinal);
  }

  private static IReadOnlyList<TenantDatabaseVerificationFilePlacement> Placements() =>
    TenantDatabaseVerificationFileLayout.Plan(
      [
        new TenantDatabaseBackupFileEntry("tenant_data", TenantDatabaseVerificationFileLayout.DataFileType),
        new TenantDatabaseBackupFileEntry("tenant_data2", TenantDatabaseVerificationFileLayout.DataFileType),
        new TenantDatabaseBackupFileEntry("tenant_log", TenantDatabaseVerificationFileLayout.LogFileType)
      ],
      VerificationDatabase,
      @"D:\verify\data",
      @"D:\verify\log");
}

[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseVerificationFileLayoutTests
{
  private const string VerificationDatabase = "SSAS_Verify_42_7";

  // MULTIPLE DATA AND LOG FILES, driven by what the server reports. A layout that assumed a single MDF/LDF
  // pair would fail on exactly the largest databases most worth verifying.
  [Fact]
  public void Every_logical_file_is_placed_under_the_configured_roots_by_type()
  {
    var placements = TenantDatabaseVerificationFileLayout.Plan(
      [
        new TenantDatabaseBackupFileEntry("d1", TenantDatabaseVerificationFileLayout.DataFileType),
        new TenantDatabaseBackupFileEntry("d2", TenantDatabaseVerificationFileLayout.DataFileType),
        new TenantDatabaseBackupFileEntry("l1", TenantDatabaseVerificationFileLayout.LogFileType),
        new TenantDatabaseBackupFileEntry("l2", TenantDatabaseVerificationFileLayout.LogFileType)
      ],
      VerificationDatabase,
      @"D:\verify\data",
      @"D:\verify\log");

    Assert.Equal(4, placements.Count);
    Assert.EndsWith(".mdf", placements[0].PhysicalPath, StringComparison.Ordinal);
    Assert.EndsWith(".mdf", placements[1].PhysicalPath, StringComparison.Ordinal);
    Assert.EndsWith(".ldf", placements[2].PhysicalPath, StringComparison.Ordinal);
    Assert.EndsWith(".ldf", placements[3].PhysicalPath, StringComparison.Ordinal);
    Assert.StartsWith(@"D:\verify\data", placements[0].PhysicalPath, StringComparison.Ordinal);
    Assert.StartsWith(@"D:\verify\log", placements[2].PhysicalPath, StringComparison.Ordinal);
  }

  // No two files may share a physical path, or a restore would overwrite its own output.
  [Fact]
  public void Physical_paths_are_unique_within_a_verification()
  {
    var placements = TenantDatabaseVerificationFileLayout.Plan(
      [
        new TenantDatabaseBackupFileEntry("same", TenantDatabaseVerificationFileLayout.DataFileType),
        new TenantDatabaseBackupFileEntry("same", TenantDatabaseVerificationFileLayout.DataFileType)
      ],
      VerificationDatabase,
      @"D:\verify\data",
      @"D:\verify\log");

    Assert.NotEqual(placements[0].PhysicalPath, placements[1].PhysicalPath);
  }

  // Two verifications never collide, because the database name carries the run identity.
  [Fact]
  public void Physical_paths_are_unique_across_verifications()
  {
    var first = Plan("SSAS_Verify_42_7");
    var second = Plan("SSAS_Verify_42_8");

    Assert.NotEqual(first[0].PhysicalPath, second[0].PhysicalPath);
  }

  // A logical name that is hostile as a path — separators, traversal — cannot influence the physical path,
  // because the physical name is built from the verification database name and an ordinal index.
  [Fact]
  public void A_hostile_logical_file_name_cannot_escape_the_configured_root()
  {
    var placements = TenantDatabaseVerificationFileLayout.Plan(
      [new TenantDatabaseBackupFileEntry(@"..\..\Windows\System32\evil", TenantDatabaseVerificationFileLayout.DataFileType)],
      VerificationDatabase,
      @"D:\verify\data",
      @"D:\verify\log");

    Assert.StartsWith(@"D:\verify\data", placements[0].PhysicalPath, StringComparison.Ordinal);
    Assert.DoesNotContain("..", placements[0].PhysicalPath, StringComparison.Ordinal);
  }

  // Defence in depth: a layout can only be planned for a name inside the reserved vocabulary, so no future
  // caller can direct a restore at a production path.
  [Fact]
  public void A_layout_cannot_be_planned_for_a_name_outside_the_reserved_vocabulary()
  {
    Assert.Throws<ArgumentException>(() => TenantDatabaseVerificationFileLayout.Plan(
      [new TenantDatabaseBackupFileEntry("d1", TenantDatabaseVerificationFileLayout.DataFileType)],
      "TenantProduction",
      @"D:\verify\data",
      @"D:\verify\log"));
  }

  [Fact]
  public void An_empty_file_list_is_refused()
  {
    Assert.Throws<ArgumentException>(() => TenantDatabaseVerificationFileLayout.Plan(
      [], VerificationDatabase, @"D:\verify\data", @"D:\verify\log"));
  }

  private static IReadOnlyList<TenantDatabaseVerificationFilePlacement> Plan(string databaseName) =>
    TenantDatabaseVerificationFileLayout.Plan(
      [new TenantDatabaseBackupFileEntry("d1", TenantDatabaseVerificationFileLayout.DataFileType)],
      databaseName,
      @"D:\verify\data",
      @"D:\verify\log");
}

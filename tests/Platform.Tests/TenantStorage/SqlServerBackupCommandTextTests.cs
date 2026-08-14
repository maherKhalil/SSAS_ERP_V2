using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// SQL Server backup command construction (ADR-022 §9). These prove the two properties most easily lost in a
// longer method: the database identifier is safely quoted, and COPY_ONLY never reaches the managed chain.
public sealed class SqlServerBackupCommandTextTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 30, 45, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_full_backup_uses_checksum_and_a_parameterized_path()
  {
    var command = SqlServerBackupCommandText.Build(
      TenantDatabaseBackupOperation.SqlServerFull(), "SSAS_Tenant_01", compress: true);

    Assert.Contains("BACKUP DATABASE [SSAS_Tenant_01]", command, StringComparison.Ordinal);
    Assert.Contains("TO DISK = @path", command, StringComparison.Ordinal);
    Assert.Contains("CHECKSUM", command, StringComparison.Ordinal);
    Assert.Contains("COMPRESSION", command, StringComparison.Ordinal);

    // The path is never interpolated, so no configuration value can become SQL text.
    Assert.DoesNotContain("\\", command, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_managed_full_backup_never_emits_copy_only()
  {
    // A copy-only full does not reset the differential base, so a chain built on one silently produces
    // differentials anchored to an older full (ADR-022 §9). This must be unreachable, not merely unused.
    foreach (var compress in new[] { true, false })
    {
      var full = SqlServerBackupCommandText.Build(
        TenantDatabaseBackupOperation.SqlServerFull(), "db", compress);
      var differential = SqlServerBackupCommandText.Build(
        TenantDatabaseBackupOperation.SqlServerDifferential(), "db", compress);
      var log = SqlServerBackupCommandText.Build(
        TenantDatabaseBackupOperation.SqlServerTransactionLog(), "db", compress);

      Assert.DoesNotContain("COPY_ONLY", full, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("COPY_ONLY", differential, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("COPY_ONLY", log, StringComparison.OrdinalIgnoreCase);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_command_never_changes_a_recovery_model()
  {
    // The platform detects, reports and degrades readiness; it never switches a recovery model, because
    // switching to FULL starts log growth on a database that is by definition misconfigured (ADR-022 §9).
    foreach (var operation in new[]
    {
      TenantDatabaseBackupOperation.SqlServerFull(),
      TenantDatabaseBackupOperation.SqlServerDifferential(),
      TenantDatabaseBackupOperation.SqlServerTransactionLog()
    })
    {
      var command = SqlServerBackupCommandText.Build(operation, "db", compress: false);
      Assert.DoesNotContain("ALTER DATABASE", command, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("SET RECOVERY", command, StringComparison.OrdinalIgnoreCase);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_differential_and_a_log_backup_use_their_own_shapes()
  {
    Assert.Contains("WITH DIFFERENTIAL", SqlServerBackupCommandText.Build(
      TenantDatabaseBackupOperation.SqlServerDifferential(), "db", false), StringComparison.Ordinal);

    var log = SqlServerBackupCommandText.Build(
      TenantDatabaseBackupOperation.SqlServerTransactionLog(), "db", false);
    Assert.StartsWith("BACKUP LOG", log, StringComparison.Ordinal);
    Assert.DoesNotContain("DIFFERENTIAL", log, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_database_identifier_is_bracket_quoted_and_closing_brackets_are_doubled()
  {
    // The name comes from the trusted registry rather than a caller, so this is defence in depth — but a
    // database identifier cannot be a parameter, so quoting is the only control available.
    Assert.Equal("[normal]", SqlServerBackupCommandText.QuoteIdentifier("normal"));
    Assert.Equal("[weird]]name]", SqlServerBackupCommandText.QuoteIdentifier("weird]name"));

    var command = SqlServerBackupCommandText.Build(
      TenantDatabaseBackupOperation.SqlServerFull(), "evil] WITH COPY_ONLY --", compress: false);

    // The injected text is neutralised inside the quoted identifier rather than becoming syntax.
    Assert.Contains("[evil]] WITH COPY_ONLY --]", command, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_unsupported_operation_code_is_refused_rather_than_guessed()
  {
    var unknown = TenantDatabaseBackupOperation.Create("SqlServer", "SnapshotOfSomething").Value;

    Assert.Throws<NotSupportedException>(() =>
      SqlServerBackupCommandText.Build(unknown, "db", compress: false));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Artifact_names_are_deterministic_unique_and_carry_no_customer_identity()
  {
    var full = SqlServerBackupCommandText.ArtifactFileName(
      42, TenantDatabaseBackupOperation.SqlServerFull(), 7, Now);
    var log = SqlServerBackupCommandText.ArtifactFileName(
      42, TenantDatabaseBackupOperation.SqlServerTransactionLog(), 8, Now);

    Assert.Equal("42_Full_20260814T123045Z_7.bak", full);
    Assert.Equal("42_TransactionLog_20260814T123045Z_8.trn", log);

    // Different runs never collide, which is what makes INIT safe and evidence correlation deterministic.
    Assert.NotEqual(full, SqlServerBackupCommandText.ArtifactFileName(
      42, TenantDatabaseBackupOperation.SqlServerFull(), 9, Now));
  }

  [Theory]
  [InlineData(3, true)]
  [InlineData(2, true)]
  [InlineData(4, false)]
  [Trait("Decision", "ADR-022")]
  public void Compression_capability_is_read_from_engine_metadata(int engineEdition, bool supported)
  {
    // Determined beforehand rather than discovered by catching a failed backup: engine edition 4 is Express,
    // which cannot compress.
    Assert.Equal(supported, SqlServerBackupCommandText.SupportsCompression(engineEdition));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Preferred_compression_falls_back_to_uncompressed_rather_than_failing()
  {
    // An unavailable capability is not a policy violation (ADR-022 §9).
    var resolved = SqlServerBackupCommandText.ResolveCompression(
      TenantDatabaseBackupCompressionMode.PreferredWhereSupported, engineSupportsCompression: false);

    Assert.True(resolved.IsSuccess);
    Assert.False(resolved.Value);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Required_compression_fails_before_the_command_when_unsupported()
  {
    var resolved = SqlServerBackupCommandText.ResolveCompression(
      TenantDatabaseBackupCompressionMode.Required, engineSupportsCompression: false);

    Assert.True(resolved.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupCompressionNotSupported.Code, resolved.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Disabled_compression_is_honoured_even_where_supported()
  {
    var resolved = SqlServerBackupCommandText.ResolveCompression(
      TenantDatabaseBackupCompressionMode.Disabled, engineSupportsCompression: true);

    Assert.True(resolved.IsSuccess);
    Assert.False(resolved.Value);
  }
}

using Microsoft.Extensions.Options;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// The Phase B trust boundaries (ADR-022 §11): where a backup destination may come from, and which identity
// reaches the database. Both fail closed.
public sealed class TenantDatabaseBackupTrustTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_destination_resolves_only_from_trusted_configuration()
  {
    var options = Options();
    options.Value.BackupDestinations["PrimaryVault"] = new TenantStorageBackupDestinationOptions
    {
      DirectoryPath = @"D:\Backups\Primary"
    };

    var resolved = new TenantDatabaseBackupDestinationResolver(options).Resolve("PrimaryVault");

    Assert.True(resolved.IsSuccess);
    Assert.Equal(@"D:\Backups\Primary", resolved.Value.DirectoryPath);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("Decision", "ADR-022")]
  public void A_blank_destination_key_is_refused(string? key)
  {
    var resolved = new TenantDatabaseBackupDestinationResolver(Options()).Resolve(key);

    Assert.True(resolved.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupDestinationKeyRequired.Code, resolved.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void An_unknown_destination_key_fails_closed_and_never_falls_back()
  {
    // The security property this whole indirection exists for: an unrecognised key produces no backup at
    // all, rather than a backup written somewhere plausible.
    var options = Options();
    options.Value.BackupDestinations["Configured"] = new TenantStorageBackupDestinationOptions
    {
      DirectoryPath = @"D:\Backups\Configured"
    };

    var resolved = new TenantDatabaseBackupDestinationResolver(options).Resolve("SomewhereElse");

    Assert.True(resolved.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupDestinationNotConfigured.Code, resolved.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_caller_supplied_path_is_not_a_destination_key()
  {
    // A path is simply not in the key namespace, so supplying one resolves to nothing. This is what makes
    // destination injection structurally impossible rather than merely discouraged.
    foreach (var attempt in new[]
    {
      @"C:\Windows\Temp", @"\\attacker\share", "https://example.invalid/steal", "/var/tmp"
    })
    {
      var resolved = new TenantDatabaseBackupDestinationResolver(Options()).Resolve(attempt);
      Assert.True(resolved.IsFailure);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_backup_connection_factory_never_falls_back_to_the_runtime_credential()
  {
    // A server that is ROUTABLE is not automatically a server the platform may back up. Falling back to the
    // runtime entry would silently restore exactly the credential reuse ADR-022 §11 forbids.
    var options = Options();
    options.Value.Servers["PrimarySqlServer"] = new TenantStorageServerOptions
    {
      ConnectionString = "Server=runtime;Integrated Security=True"
    };

    var created = new TenantDatabaseBackupConnectionFactory(options).Create(
      new TenantDatabaseConnectionTarget(
        "PrimarySqlServer", "SSAS_Tenant_01", TenantDatabaseHostingMode.PlatformManaged));

    Assert.True(created.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupServerNotConfigured.Code, created.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_backup_connection_uses_its_own_identity_and_is_not_pooled()
  {
    var options = Options();
    options.Value.BackupServers["PrimarySqlServer"] = new TenantStorageServerOptions
    {
      ConnectionString = "Server=backup-host;Integrated Security=True;TrustServerCertificate=True"
    };

    var created = new TenantDatabaseBackupConnectionFactory(options).Create(
      new TenantDatabaseConnectionTarget(
        "PrimarySqlServer", "SSAS_Tenant_01", TenantDatabaseHostingMode.PlatformManaged));

    Assert.True(created.IsSuccess);
    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(created.Value.ConnectionString);

    Assert.Equal("SSAS_Tenant_01", builder.InitialCatalog);
    Assert.True(builder.IntegratedSecurity);

    // Pooling is off so that closing the connection genuinely ENDS the session — which is what releases a
    // session-scoped ownership lock. A pooled connection is reset, not ended.
    Assert.False(builder.Pooling);
    created.Value.Dispose();
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public void The_backup_connection_factory_refuses_a_customer_managed_database()
  {
    // Defence in depth: the executor refuses first, but this layer is what would open a socket to a
    // customer's server, so it refuses independently.
    var options = Options();
    options.Value.BackupServers["CustomerServer"] = new TenantStorageServerOptions
    {
      ConnectionString = "Server=customer;Integrated Security=True"
    };

    var created = new TenantDatabaseBackupConnectionFactory(options).Create(
      new TenantDatabaseConnectionTarget(
        "CustomerServer", "CustomerERP", TenantDatabaseHostingMode.CustomerManaged));

    Assert.True(created.IsFailure);
    Assert.Equal(TenantStorageErrors.UnsupportedHostingMode.Code, created.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_successful_backup_never_evaluates_to_protected()
  {
    // The most damaging shortcut available in this slice, refused structurally: a database that has just
    // taken a good backup has a baseline that has never been restored once, which is what
    // VerificationOverdue describes and what ADR-022 §18 refuses to cut over on.
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterSuccessfulBackup();

    Assert.NotEqual(TenantDatabaseRecoveryReadinessStatus.Protected, status);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, status);
  }

  private static IOptions<TenantStorageOptions> Options() =>
    Microsoft.Extensions.Options.Options.Create(new TenantStorageOptions());
}

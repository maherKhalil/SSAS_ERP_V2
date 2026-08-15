using Microsoft.Extensions.Options;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;
using Xunit;

namespace SSAS.Platform.Tests.TenantStorage;

// Restore-verification configuration and the connection trust boundary (ADR-022 §17, v1.2).
//
// The behaviour being pinned here is FAIL CLOSED. A verification target that is absent, unresolvable or not
// isolated must stop verification — never redirect it to the server hosting the authoritative database,
// which is the exact outcome the topology decision exists to prevent (compliance rules 32 and 44).
[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseRestoreVerificationConfigurationTests
{
  [Fact]
  public void Restore_verification_is_disabled_by_default()
  {
    Assert.False(new TenantDatabaseRestoreVerificationOptions().Enabled);
  }

  // A disabled deployment is an ordinary host. Refusing to start it would punish everyone not using restore
  // verification at all.
  [Fact]
  public void A_disabled_configuration_needs_no_verification_target()
  {
    var result = Validate(new TenantDatabaseRestoreVerificationOptions());

    Assert.True(result.Succeeded);
  }

  // Enabling without a target FAILS STARTUP rather than failing at the first restore — and the message says
  // there is no fallback, because that is the thing an operator would otherwise assume.
  [Fact]
  public void Enabling_without_a_restore_server_key_fails_startup()
  {
    var result = Validate(new TenantDatabaseRestoreVerificationOptions
    {
      Enabled = true,
      RestoreDataRoot = @"D:\verify\data",
      RestoreLogRoot = @"D:\verify\log"
    });

    Assert.True(result.Failed);
    Assert.Contains(result.Failures!, failure =>
      failure.Contains("RestoreServerKey", StringComparison.Ordinal));
  }

  [Theory]
  [InlineData(null, @"D:\verify\log")]
  [InlineData(@"D:\verify\data", null)]
  public void Enabling_without_file_roots_fails_startup(string? dataRoot, string? logRoot)
  {
    var result = Validate(new TenantDatabaseRestoreVerificationOptions
    {
      Enabled = true,
      RestoreServerKey = "verify",
      RestoreDataRoot = dataRoot,
      RestoreLogRoot = logRoot
    });

    Assert.True(result.Failed);
  }

  // NO SILENT CLAMPING. A non-positive grace period would make a verification database eligible for deletion
  // while it is still in use, and quietly correcting it to something sensible is behaviour nobody asked for.
  [Fact]
  public void A_non_positive_orphan_grace_period_fails_startup()
  {
    var result = Validate(new TenantDatabaseRestoreVerificationOptions
    {
      Enabled = true,
      RestoreServerKey = "verify",
      RestoreDataRoot = @"D:\verify\data",
      RestoreLogRoot = @"D:\verify\log",
      OrphanCleanupGracePeriod = TimeSpan.Zero
    });

    Assert.True(result.Failed);
    Assert.Contains(result.Failures!, failure =>
      failure.Contains("OrphanCleanupGracePeriod", StringComparison.Ordinal));
  }

  [Fact]
  public void A_complete_enabled_configuration_validates()
  {
    var result = Validate(Enabled());

    Assert.True(result.Succeeded);
  }

  // ---- The connection boundary.

  [Fact]
  public void A_disabled_deployment_refuses_to_open_a_verification_connection()
  {
    var factory = Factory(new TenantDatabaseRestoreVerificationOptions(), storage => { });

    var result = factory.Create(new TenantDatabaseVerificationTarget("verify", "primary"));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationNotConfigured.Code, result.Error.Code);
  }

  // FAILS CLOSED on an unknown key — and explicitly does NOT fall back to BackupServers or Servers, which
  // would reintroduce both the credential reuse and the topology violation the separation prevents.
  [Fact]
  public void An_unresolvable_verification_key_fails_closed_without_falling_back()
  {
    var factory = Factory(Enabled(), storage =>
    {
      storage.Servers["primary"] = new TenantStorageServerOptions { ConnectionString = "Server=primary;" };
      storage.BackupServers["primary"] = new TenantStorageServerOptions { ConnectionString = "Server=primary;" };
    });

    var result = factory.Create(new TenantDatabaseVerificationTarget("verify", "primary"));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationServerNotConfigured.Code, result.Error.Code);
  }

  // THE ISOLATION RULE (compliance rule 32). A verification target equal to the server hosting the
  // authoritative database is refused unless the deployment has explicitly declared itself non-production.
  [Fact]
  public void A_verification_target_on_the_authoritative_server_is_refused_by_default()
  {
    var factory = Factory(Enabled(), storage =>
      storage.VerificationServers["primary"] =
        new TenantStorageServerOptions { ConnectionString = "Server=primary;Integrated Security=True;" });

    var result = factory.Create(new TenantDatabaseVerificationTarget("primary", "primary"));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationTargetNotIsolated.Code, result.Error.Code);
  }

  // The non-production exception is EXPLICIT and opt-in. It is never reached by failing to resolve a
  // dedicated target.
  [Fact]
  public void Same_instance_verification_is_permitted_only_when_explicitly_configured()
  {
    var options = Enabled();
    options.RestoreServerKey = "primary";
    options.AllowSameInstanceVerification = true;

    var factory = Factory(options, storage =>
      storage.VerificationServers["primary"] =
        new TenantStorageServerOptions { ConnectionString = "Server=primary;Integrated Security=True;" });

    var result = factory.Create(new TenantDatabaseVerificationTarget("primary", "primary"));

    Assert.True(result.IsSuccess);
    result.Value.Dispose();
  }

  [Fact]
  public void An_isolated_verification_target_opens_against_master_without_pooling()
  {
    var factory = Factory(Enabled(), storage =>
      storage.VerificationServers["verify"] =
        new TenantStorageServerOptions { ConnectionString = "Server=verifyhost;Integrated Security=True;" });

    var result = factory.Create(new TenantDatabaseVerificationTarget("verify", "primary"));

    Assert.True(result.IsSuccess);
    using var connection = result.Value;

    // `master`, because a restore that CREATES a database cannot connect to that database — and never the
    // source tenant database.
    Assert.Equal("master", connection.Database, StringComparer.OrdinalIgnoreCase);
    Assert.Contains("Pooling=False", connection.ConnectionString, StringComparison.OrdinalIgnoreCase);
  }

  private static TenantDatabaseRestoreVerificationOptions Enabled() =>
    new()
    {
      Enabled = true,
      RestoreServerKey = "verify",
      RestoreDataRoot = @"D:\verify\data",
      RestoreLogRoot = @"D:\verify\log"
    };

  private static ValidateOptionsResult Validate(TenantDatabaseRestoreVerificationOptions options) =>
    new TenantDatabaseRestoreVerificationOptionsValidator().Validate(null, options);

  private static TenantDatabaseVerificationConnectionFactory Factory(
    TenantDatabaseRestoreVerificationOptions verification,
    Action<TenantStorageOptions> configureStorage)
  {
    var storage = new TenantStorageOptions();
    configureStorage(storage);

    return new TenantDatabaseVerificationConnectionFactory(
      Options.Create(storage),
      Options.Create(verification));
  }
}

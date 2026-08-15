using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// Fleet scheduler configuration (ADR-022 §13, TS-Backup Phase C).
//
// These exist because the first cut of Phase C registered a hardcoded options singleton: the scheduler was
// present, defaulted off, and had no configuration path to turn on. A fleet scheduler nobody can enable
// without a rebuild is not a fleet scheduler, so the binding is exercised through the REAL options pipeline
// rather than by constructing the type.
public sealed class TenantDatabaseBackupSchedulerOptionsTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_scheduler_is_disabled_unless_configuration_enables_it()
  {
    // Safe default: unattended fleet backups need credentials and destinations that do not exist by default.
    var options = Resolve([]);

    Assert.False(options.Enabled);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Configuration_can_enable_and_tune_every_scheduler_setting()
  {
    // The regression that matters: each of these must actually reach the scheduler through binding.
    var options = Resolve(new Dictionary<string, string?>
    {
      ["TenantStorage:BackupScheduler:Enabled"] = "true",
      ["TenantStorage:BackupScheduler:SweepInterval"] = "00:02:00",
      ["TenantStorage:BackupScheduler:BatchSize"] = "42",
      ["TenantStorage:BackupScheduler:MaxConcurrentBackups"] = "5",
      ["TenantStorage:BackupScheduler:MaxConcurrentPerServer"] = "3",
      ["TenantStorage:BackupScheduler:StartupDelay"] = "00:00:05",
      ["TenantStorage:BackupScheduler:MaximumJitter"] = "00:00:10",
      ["TenantStorage:BackupScheduler:FailureRetryBackoff"] = "00:10:00",
      ["TenantStorage:BackupScheduler:SkipRetryBackoff"] = "00:00:30"
    });

    Assert.True(options.Enabled);
    Assert.Equal(TimeSpan.FromMinutes(2), options.SweepInterval);
    Assert.Equal(42, options.BatchSize);
    Assert.Equal(5, options.MaxConcurrentBackups);
    Assert.Equal(3, options.MaxConcurrentPerServer);
    Assert.Equal(TimeSpan.FromSeconds(5), options.StartupDelay);
    Assert.Equal(TimeSpan.FromSeconds(10), options.MaximumJitter);
    Assert.Equal(TimeSpan.FromMinutes(10), options.FailureRetryBackoff);
    Assert.Equal(TimeSpan.FromSeconds(30), options.SkipRetryBackoff);
  }

  [Theory]
  [Trait("Decision", "ADR-022")]
  [InlineData("MaxConcurrentBackups", "0")]
  [InlineData("MaxConcurrentBackups", "-1")]
  [InlineData("MaxConcurrentPerServer", "0")]
  [InlineData("BatchSize", "0")]
  [InlineData("BatchSize", "-5")]
  [InlineData("SweepInterval", "-00:00:01")]
  [InlineData("SweepInterval", "00:00:00")]
  [InlineData("StartupDelay", "-00:00:01")]
  [InlineData("MaximumJitter", "-00:00:01")]
  [InlineData("FailureRetryBackoff", "00:00:00")]
  [InlineData("SkipRetryBackoff", "00:00:00")]
  public void An_enabled_scheduler_refuses_to_start_on_invalid_configuration(string key, string value)
  {
    // Fail at startup, not at 03:00. A zero concurrency cap is the sharpest example: a semaphore with no
    // permits never admits anyone, so the sweep would hang rather than visibly do nothing.
    var settings = new Dictionary<string, string?>
    {
      ["TenantStorage:BackupScheduler:Enabled"] = "true",
      [$"TenantStorage:BackupScheduler:{key}"] = value
    };

    var failure = Assert.Throws<OptionsValidationException>(() => Resolve(settings));
    Assert.Contains(key, string.Join(" ", failure.Failures), StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_per_server_cap_above_the_global_cap_is_rejected()
  {
    // It could never be reached, so it is almost certainly a misunderstanding of which cap does what.
    var failure = Assert.Throws<OptionsValidationException>(() => Resolve(new Dictionary<string, string?>
    {
      ["TenantStorage:BackupScheduler:Enabled"] = "true",
      ["TenantStorage:BackupScheduler:MaxConcurrentBackups"] = "2",
      ["TenantStorage:BackupScheduler:MaxConcurrentPerServer"] = "4"
    }));

    Assert.Contains("MaxConcurrentPerServer", string.Join(" ", failure.Failures), StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_disabled_scheduler_is_not_validated()
  {
    // A host that does not use fleet backups should not be refused startup over scheduler values it never
    // reads. Disabled plus nonsense is simply disabled.
    var options = Resolve(new Dictionary<string, string?>
    {
      ["TenantStorage:BackupScheduler:Enabled"] = "false",
      ["TenantStorage:BackupScheduler:MaxConcurrentBackups"] = "0"
    });

    Assert.False(options.Enabled);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void No_scheduler_setting_can_weaken_execution_safety()
  {
    // ADR-022 compliance rules 29 and 30. Scheduling is an operational preference; the provider's visibility
    // and in-flight checks are correctness preconditions and must have no configuration surface at all.
    foreach (var property in typeof(TenantDatabaseBackupSchedulerOptions).GetProperties())
    {
      Assert.DoesNotContain("InFlight", property.Name, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("Visibility", property.Name, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("Ownership", property.Name, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("Safety", property.Name, StringComparison.OrdinalIgnoreCase);
    }
  }

  // Resolves through the real options pipeline — Bind, the registered validator, and ValidateOnStart
  // semantics — rather than constructing the options object.
  private static TenantDatabaseBackupSchedulerOptions Resolve(Dictionary<string, string?> settings)
  {
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    var services = new ServiceCollection();
    services.AddOptions<TenantDatabaseBackupSchedulerOptions>()
      .Bind(configuration.GetSection(TenantDatabaseBackupSchedulerOptions.SectionName))
      .ValidateOnStart();
    services.AddSingleton<IValidateOptions<TenantDatabaseBackupSchedulerOptions>,
      TenantDatabaseBackupSchedulerOptionsValidator>();

    using var provider = services.BuildServiceProvider();
    return provider.GetRequiredService<IOptions<TenantDatabaseBackupSchedulerOptions>>().Value;
  }
}

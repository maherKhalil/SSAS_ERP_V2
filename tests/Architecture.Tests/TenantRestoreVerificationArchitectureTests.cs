using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Application.Abstractions.Persistence;

namespace SSAS.Architecture.Tests;

// Structural guards for restore verification (ADR-022 §17, v1.2).
//
// These exist because the next slices add code that CREATES AND DESTROYS DATABASES on a schedule. Every
// guard below is aimed at something that would be easy to introduce accidentally and expensive to notice:
// a `WITH REPLACE` added to make an awkward restore work, a caller-supplied path threaded through for
// testability, a runtime credential reused because it was already injected.
public sealed class TenantRestoreVerificationArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly =
    typeof(TenantDatabaseVerificationConnectionFactory).Assembly;

  private static readonly Assembly DomainAssembly = typeof(TenantDatabaseVerificationNaming).Assembly;

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_d7_executor_is_the_only_production_consumer_of_the_restore_provider()
  {
    var consumers = InfrastructureAssembly.GetTypes()
      .Where(type => !type.IsInterface &&
        !type.GetInterfaces().Contains(typeof(ITenantDatabaseRestoreVerificationProvider)))
      .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .SelectMany(constructor => constructor.GetParameters())
        .Any(parameter => parameter.ParameterType == typeof(ITenantDatabaseRestoreVerificationProvider)))
      .ToArray();

    Assert.Single(consumers);
    Assert.Equal("TenantDatabaseRestoreVerificationExecutor", consumers[0].Name);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_d7_executor_carries_every_execution_authority_boundary()
  {
    var dependencies = InfrastructureType("TenantDatabaseRestoreVerificationExecutor")
      .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantDatabaseRegistryReadRepository), dependencies);
    Assert.Contains(typeof(ITenantDatabaseBackupReadRepository), dependencies);
    Assert.Contains(typeof(ITenantDatabaseRestoreVerificationRunStore), dependencies);
    Assert.Contains(typeof(ITenantDatabaseVerificationConnectionFactory), dependencies);
    Assert.Contains(typeof(ITenantDatabaseRestoreVerificationProbe), dependencies);
    Assert.Contains(typeof(ITenantDatabaseRecoveryReadinessWriter), dependencies);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Post_restore_probe_uses_only_the_verification_connection_boundary()
  {
    var dependencies = InfrastructureType("SqlServerRestoreVerificationProbe")
      .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantDatabaseVerificationConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseBackupConnectionFactory), dependencies);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Restore_provider_and_probe_cannot_write_recovery_readiness()
  {
    foreach (var type in new[]
    {
      InfrastructureType("SqlServerTenantDatabaseRestoreVerificationProvider"),
      InfrastructureType("SqlServerRestoreVerificationProbe")
    })
    {
      var dependencies = type
        .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .SelectMany(constructor => constructor.GetParameters())
        .Select(parameter => parameter.ParameterType);
      Assert.DoesNotContain(typeof(ITenantDatabaseRecoveryReadinessWriter), dependencies);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void D9_scheduler_delegates_to_d7_and_no_cleanup_executor_exists()
  {
    var dependencies = typeof(TenantDatabaseRestoreVerificationScheduler)
      .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();
    Assert.Contains(typeof(IServiceScopeFactory), dependencies);
    Assert.Contains(typeof(ITenantDatabaseRecoveryReadinessRefresher), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseRestoreVerificationProvider), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseRecoveryReadinessWriter), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseRestoreVerificationExecutor), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseRestoreVerificationRunStore), dependencies);

    var cleanupExecutors = InfrastructureAssembly.GetTypes()
      .Where(type => type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true)
      .Where(type => type.Name.Contains("CleanupExecutor", StringComparison.OrdinalIgnoreCase))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(cleanupExecutors);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Readiness_refresher_owns_the_recovery_dimension_write_boundary()
  {
    var dependencies = typeof(TenantDatabaseRecoveryReadinessRefresher)
      .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantDatabaseRecoveryReadinessWriter), dependencies);
    Assert.Contains(typeof(ITenantDatabaseBackupReadRepository), dependencies);
    Assert.Contains(typeof(ITenantDatabaseRestoreVerificationFleetReadRepository), dependencies);
  }

  // TWO INVARIANTS LIVE NEXT DOOR, not here: that no restore command can emit `WITH REPLACE`, and that a
  // restore layout cannot be planned for a name outside the reserved vocabulary. Both are enforced in
  // SqlServerRestoreCommandTextTests and TenantDatabaseVerificationFileLayoutTests, because the command
  // builder and the layout planner are `internal` to Infrastructure and only the Platform test assembly sees
  // them. Widening `InternalsVisibleTo` purely to duplicate an assertion would trade a real encapsulation
  // boundary for a redundant test.

  // The verification connection factory must not reach the runtime or backup credential registries. A second
  // path to those would let verification connect somewhere the topology decision forbids.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_verification_connection_factory_does_not_depend_on_runtime_or_backup_connection_factories()
  {
    var dependencies = typeof(TenantDatabaseVerificationConnectionFactory)
      .GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.DoesNotContain(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseBackupConnectionFactory), dependencies);
  }

  // The run store persists verification state and must never be able to open a database connection of its
  // own — that would put a restore path inside the component that records them.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_verification_run_store_cannot_reach_a_tenant_or_verification_server()
  {
    var dependencies = typeof(TenantDatabaseRestoreVerificationRunStore)
      .GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.DoesNotContain(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseBackupConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseVerificationConnectionFactory), dependencies);
  }

  // NO SAFETY SWITCHES IN CONFIGURATION. Verification options are scheduling and topology preferences; the
  // reserved naming vocabulary, the isolation rule and the cleanup conjunction are correctness preconditions
  // with no configuration path, exactly as the provider's in-flight guard has none (compliance rule 29).
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Verification_configuration_exposes_no_safety_bypass()
  {
    var forbidden = new[]
    {
      "SkipIsolation", "AllowProduction", "DisableGuard", "IgnoreRegistered", "AllowReplace",
      "SkipNameCheck", "ForceDrop", "DisableCleanupChecks", "AllowAnyDatabase"
    };

    var properties = typeof(TenantDatabaseRestoreVerificationOptions)
      .GetProperties()
      .Select(property => property.Name)
      .ToArray();

    foreach (var name in forbidden)
    {
      Assert.DoesNotContain(name, properties, StringComparer.OrdinalIgnoreCase);
    }
  }

  // Retention workers, artifact deletion and point-in-time restore remain OUT OF SCOPE. The recovery
  // activation gate is now IN scope BY DECISION (TS-Storage Phase E) — and is admitted by an exhaustive
  // allowlist rather than by dropping the term, so a SECOND activation or cutover type still trips this
  // guard until someone decides it belongs.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Tenant_storage_introduces_no_retention_deletion_or_undecided_cutover_component()
  {
    var forbidden = new[]
    {
      "Retention", "ArtifactDeletion", "Cutover", "Activation", "PointInTime", "Stopat"
    };

    // The Phase E recovery activation decision and its inputs. Nothing else.
    var decided = new[]
    {
      "SSAS.Platform.Domain.TenantStorage.TenantDatabaseRecoveryActivation",
      "SSAS.Platform.Domain.TenantStorage.TenantDatabaseRecoveryActivationInputs",
      "SSAS.Platform.Domain.TenantStorage.TenantDatabaseRecoveryActivationDecision"
    };

    // SCOPED TO TENANT STORAGE, deliberately. An unscoped sweep matches unrelated subsystems — localization
    // has its own perfectly legitimate activation types — and a guard that fails for reasons unconnected to
    // what it protects gets weakened rather than heeded.
    var offenders = InfrastructureAssembly.GetTypes()
      .Concat(DomainAssembly.GetTypes())
      .Where(type => !type.IsNested &&
        !Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
      .Where(type => type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true)
      .Where(type => forbidden.Any(term => type.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
      .Select(type => type.FullName)
      .Where(name => !decided.Contains(name, StringComparer.Ordinal))
      .ToArray();

    Assert.Empty(offenders);
  }

  // CustomerManaged has no platform restore path. The readiness evaluator refuses it structurally rather
  // than relying on a caller to check first (ADR-022 §12, compliance rule 7).
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void A_customer_managed_database_can_never_evaluate_to_protected()
  {
    foreach (var verificationInterval in new int?[] { null, 30 })
    {
      var status = TenantDatabaseRecoveryReadinessEvaluator.Evaluate(
        new TenantDatabaseRecoveryReadinessInputs(
          TenantDatabaseHostingMode.CustomerManaged,
          PolicyExists: true,
          PolicyEnabled: true,
          TenantDatabaseBackupManagementMode.AutomaticByPlatform,
          FullBackupIntervalMinutes: 10_080,
          DifferentialBackupIntervalMinutes: null,
          TransactionLogBackupIntervalMinutes: null,
          RestoreVerificationIntervalDays: verificationInterval,
          MaximumBackupAgeMinutes: null,
          LastSuccessfulFullBackupUtc: DateTimeOffset.UnixEpoch,
          LastSuccessfulDifferentialBackupUtc: null,
          LastSuccessfulLogBackupUtc: null,
          LastRestoreVerificationUtc: DateTimeOffset.UnixEpoch,
          ObservedRecoveryModel: TenantDatabaseRecoveryModel.Full),
        DateTimeOffset.UnixEpoch);

      Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Unknown, status);
    }
  }

  // The destructive-cleanup predicate must remain a CONJUNCTION. If any single condition ever stops being
  // required, this fails — which is the whole reason it is written as one predicate over one record rather
  // than as scattered checks at the call site.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Automated_cleanup_eligibility_requires_every_condition()
  {
    var eligible = new TenantDatabaseVerificationCleanupCandidate(
      DatabaseName: "SSAS_Verify_1_1",
      HasMatchingVerificationRecord: true,
      RecordedDatabaseName: "SSAS_Verify_1_1",
      IsTargetOfActiveVerification: false,
      IsRegisteredTenantDatabase: false,
      HasTenantAssignment: false,
      Age: TimeSpan.FromHours(12),
      GracePeriod: TimeSpan.FromHours(6));

    Assert.True(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(eligible));

    // Each mutation falsifies exactly one condition.
    var falsified = new[]
    {
      eligible with { DatabaseName = "TenantProduction" },
      eligible with { HasMatchingVerificationRecord = false },
      eligible with { RecordedDatabaseName = "SSAS_Verify_9_9" },
      eligible with { IsTargetOfActiveVerification = true },
      eligible with { IsRegisteredTenantDatabase = true },
      eligible with { HasTenantAssignment = true },
      eligible with { Age = TimeSpan.Zero }
    };

    foreach (var candidate in falsified)
    {
      Assert.False(TenantDatabaseVerificationTargetGuard.IsEligibleForAutomatedCleanup(candidate));
    }
  }

  // Every new Phase D persisted string must be Unicode. Asserted against the EF model's store types, because
  // a CLR-type check is blind to enums and value objects converted with HasConversion<string>() — which is
  // how the first attempt at the project-wide Unicode guard found nothing.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Every_persisted_verification_string_is_unicode()
  {
    using var context = PlatformContext();
    var entity = context.Model.FindEntityType(typeof(TenantDatabaseRestoreVerificationRun));
    Assert.NotNull(entity);

    var nonUnicode = entity!.GetProperties()
      .Select(property => new { property.Name, StoreType = property.GetColumnType() })
      .Where(property => property.StoreType is not null &&
        property.StoreType.Contains("char", StringComparison.OrdinalIgnoreCase) &&
        !property.StoreType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase) &&
        !property.StoreType.StartsWith("nchar", StringComparison.OrdinalIgnoreCase))
      .Select(property => $"{property.Name}:{property.StoreType}")
      .ToArray();

    Assert.Empty(nonUnicode);
  }

  // Model-only. No connection is opened; the string is never used to reach a server.
  private static PlatformDbContext PlatformContext()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=(local);Database=ArchitectureModelOnly;Integrated Security=True")
      .Options;
    return new PlatformDbContext(options, new ModelUser(), new ModelTenant(), new ModelClock());
  }

  private static Type InfrastructureType(string name) =>
    InfrastructureAssembly.GetTypes().Single(type => type.Name == name);

  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => "architecture-tests";

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}

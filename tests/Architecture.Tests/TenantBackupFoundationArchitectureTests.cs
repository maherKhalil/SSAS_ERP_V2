using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// TS-Backup Phase A guards (ADR-022). These protect the boundaries this slice deliberately did NOT cross,
// so a later slice cannot quietly widen it: no provider, no scheduler, no restore, no destination
// resolution, no backup SQL, and no fourth dimension leaking into the traffic gate.
public sealed class TenantBackupFoundationArchitectureTests
{
  private static readonly Assembly DomainAssembly = typeof(TenantDatabase).Assembly;

  private static readonly Assembly ApplicationAssembly = typeof(TenantDatabaseDescriptor).Assembly;

  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_metadata_belongs_to_the_platform_context_only()
  {
    // Backup state describes the physical database rather than living inside it, and a database that cannot
    // be reached is exactly when its protection state must still be readable.
    using var platform = PlatformContext();
    Assert.NotNull(platform.Model.FindEntityType(typeof(TenantDatabaseBackupPolicy)));
    Assert.NotNull(platform.Model.FindEntityType(typeof(TenantDatabaseBackupRun)));

    using var tenant = TenantContext();
    Assert.Null(tenant.Model.FindEntityType(typeof(TenantDatabaseBackupPolicy)));
    Assert.Null(tenant.Model.FindEntityType(typeof(TenantDatabaseBackupRun)));
    Assert.Null(tenant.Model.FindEntityType(typeof(TenantDatabase)));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_entities_are_platform_metadata_not_tenant_owned()
  {
    // If either became ITenantOwnedEntity the global filter would hide a shared database's protection state
    // from every tenant in it — and a shared database is ONE backup target, not one per tenant.
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabaseBackupPolicy).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabaseBackupRun).GetInterfaces());
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_state_attaches_to_the_physical_database_never_to_a_tenant_or_assignment()
  {
    // ADR-022 §1. Policy per assignment would create rows that disagree with each other about the same
    // physical database, and policy per tenant would multiply one shared chain by its tenant count.
    foreach (var type in new[] { typeof(TenantDatabaseBackupPolicy), typeof(TenantDatabaseBackupRun) })
    {
      var names = type.GetProperties().Select(property => property.Name).ToArray();
      Assert.Contains("TenantDatabaseId", names);
      Assert.DoesNotContain("TenantId", names);
      Assert.DoesNotContain("TenantDatabaseAssignmentId", names);
    }

    // Recovery readiness lives on the physical database row, never duplicated onto assignments.
    var assignmentProperties = typeof(TenantDatabaseAssignment).GetProperties()
      .Select(property => property.Name).ToArray();
    Assert.DoesNotContain("RecoveryReadinessStatus", assignmentProperties);
    Assert.DoesNotContain(assignmentProperties, name => name.Contains("Backup", StringComparison.Ordinal));
    Assert.DoesNotContain(assignmentProperties, name => name.Contains("Recovery", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_management_mode_is_distinct_from_migration_management_mode()
  {
    // ADR-018 already paid to correct this conflation once. Reusing the migration enum would make "customer
    // hosts it, we migrate it, their DBA backs it up" inexpressible.
    Assert.NotEqual(
      typeof(TenantDatabaseBackupManagementMode),
      typeof(TenantDatabaseMigrationManagementMode));

    var policyModeType = typeof(TenantDatabaseBackupPolicy)
      .GetProperty(nameof(TenantDatabaseBackupPolicy.ManagementMode))!.PropertyType;
    Assert.Equal(typeof(TenantDatabaseBackupManagementMode), policyModeType);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Recovery_readiness_has_its_own_writer_and_no_combined_health_api_exists()
  {
    // Compile-time separation is what makes one-writer-per-dimension enforceable: a component holding the
    // recovery writer CANNOT express a connectivity, schema or migration write.
    var recoveryMethods = typeof(ITenantDatabaseRecoveryReadinessWriter).GetMethods()
      .Select(method => method.Name).ToArray();
    Assert.Contains("RecordRecoveryReadinessAsync", recoveryMethods);
    Assert.DoesNotContain(recoveryMethods, name => name.Contains("Connectivity", StringComparison.Ordinal));
    Assert.DoesNotContain(recoveryMethods, name => name.Contains("Schema", StringComparison.Ordinal));
    Assert.DoesNotContain(recoveryMethods, name => name.Contains("Migration", StringComparison.Ordinal));

    // And the health writer gained no recovery method.
    var healthMethods = typeof(ITenantDatabaseHealthWriter).GetMethods().Select(method => method.Name).ToArray();
    Assert.DoesNotContain(healthMethods, name => name.Contains("Recovery", StringComparison.Ordinal));

    // No generic all-dimension writer anywhere in Infrastructure.
    var forbidden = new[] { "RecordAllHealthAsync", "RecordHealthAsync", "UpdateHealthAsync" };
    foreach (var type in InfrastructureAssembly.GetTypes().Where(type => type.IsPublic))
    {
      foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
      {
        Assert.DoesNotContain(method.Name, forbidden);
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Normal_erp_traffic_is_not_gated_on_recovery_readiness()
  {
    // ADR-022 §7. A durability problem and an availability problem are different problems: the data is
    // intact and readable when backups are late, and denying traffic would convert one into the other.
    var gateSources = typeof(ITenantDatabaseTrafficGate).Assembly.GetTypes()
      .Where(type => type.Name.Contains("TrafficGate", StringComparison.Ordinal));

    foreach (var type in gateSources)
    {
      foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
      {
        foreach (var parameter in method.GetParameters())
        {
          Assert.NotEqual(typeof(TenantDatabaseRecoveryReadinessStatus), parameter.ParameterType);
        }
      }
    }

    // The routing record the gate reads carries no recovery field either, so it could not consult one.
    var routeProperties = typeof(TenantDatabaseAssignmentRecord).GetProperties()
      .Select(property => property.Name).ToArray();
    Assert.DoesNotContain(routeProperties, name => name.Contains("Recovery", StringComparison.Ordinal));
    Assert.DoesNotContain(routeProperties, name => name.Contains("Backup", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Phase_a_grants_no_backup_execution_authority()
  {
    // No contract anywhere can cause a backup or a restore. Execution arrives with the provider in Phase B,
    // and the Phase B session-loss exit gate blocks Phase C.
    var forbiddenFragments = new[]
    {
      "ExecuteBackup", "RunBackup", "RunFullBackup", "PerformBackup", "StartBackupOperation",
      "Restore", "VerifyOnly", "BackupDatabase"
    };

    // Scoped to the tenant-storage surface rather than whole assemblies: localization has a long-standing
    // and entirely unrelated RestoreDefault feature, and a blanket scan would flag it forever while saying
    // nothing about backup authority.
    foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly })
    {
      foreach (var type in assembly.GetTypes().Where(IsTenantStorageType))
      {
        foreach (var method in type.GetMethods(
          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
          BindingFlags.DeclaredOnly))
        {
          // Property accessors are excluded: LastRestoreVerificationUtc is a recorded OBSERVATION, and its
          // generated getter would otherwise read as a restore capability. Verbs are what matter here.
          if (method.IsSpecialName)
          {
            continue;
          }

          // RecordVerification records an OUTCOME; it does not perform one.
          //
          // ExecuteBackupAsync is the SQL Server provider's own command-issuing method, added by the
          // reviewed Phase B slice. It is exempted BY TYPE rather than by name, so an execution verb
          // appearing anywhere else in the tenant-storage surface still fails — which is the boundary this
          // guard actually protects now that execution exists at all.
          if (method.Name is "RecordVerification" ||
            (method.Name is "ExecuteBackupAsync" && type.Name is "SqlServerTenantDatabaseBackupProvider"))
          {
            continue;
          }

          Assert.DoesNotContain(
            forbiddenFragments,
            fragment => method.Name.Contains(fragment, StringComparison.Ordinal));
        }
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Phase_a_introduces_no_backup_provider_and_no_scheduler()
  {
    // Phase B owns the SQL Server provider and Phase C owns fleet scheduling, so both are expected and are
    // exempted BY EXACT NAME. What must still not appear is a backup WORKER or a restore-verification
    // runtime — those belong to Phase D and beyond, and neither is on this list.
    var delivered = new[]
    {
      "SqlServerTenantDatabaseBackupProvider",
      "ITenantDatabaseBackupScheduler",
      "TenantDatabaseBackupScheduler",
      "TenantDatabaseBackupSchedulerOptions",
      "TenantDatabaseBackupSchedulerHostedService"
    };

    foreach (var type in InfrastructureAssembly.GetTypes()
      .Where(type => !delivered.Contains(type.Name, StringComparer.Ordinal)))
    {
      Assert.DoesNotContain("BackupProvider", type.Name, StringComparison.Ordinal);
      Assert.DoesNotContain("BackupScheduler", type.Name, StringComparison.Ordinal);
      Assert.DoesNotContain("BackupWorker", type.Name, StringComparison.Ordinal);
      Assert.DoesNotContain("RestoreVerification", type.Name, StringComparison.Ordinal);

      // No hosted service was added for backups. The existing tenant-storage bootstrap service is the only
      // hosted service in this area and is unrelated.
      if (type.Name.Contains("Backup", StringComparison.Ordinal) ||
        type.Name.Contains("Recovery", StringComparison.Ordinal))
      {
        Assert.DoesNotContain(
          typeof(Microsoft.Extensions.Hosting.IHostedService), type.GetInterfaces());
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_state_persists_a_trusted_key_and_never_a_resolved_destination()
  {
    // ADR-022 §11 and compliance rule 23. BACKUP DATABASE writes a complete copy of the database wherever
    // it is told, so an attacker who could influence the destination would have exfiltration without
    // reading a single row through the application. The Platform database therefore stores a KEY.
    var forbidden = new[]
    {
      "Path", "UncPath", "Url", "Uri", "Endpoint", "ConnectionString", "SasToken", "AccessKey",
      "Password", "Secret", "Credential", "ResolvedDestination"
    };

    foreach (var type in new[]
    {
      typeof(TenantDatabaseBackupPolicy), typeof(TenantDatabaseBackupRun),
      typeof(TenantDatabaseBackupPolicyRecord), typeof(TenantDatabaseBackupRunRecord)
    })
    {
      foreach (var property in type.GetProperties())
      {
        Assert.DoesNotContain(
          forbidden,
          fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
      }
    }

    Assert.Equal(
      typeof(string),
      typeof(TenantDatabaseBackupPolicy)
        .GetProperty(nameof(TenantDatabaseBackupPolicy.DestinationKey))!.PropertyType);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_operation_vocabulary_stays_provider_scoped()
  {
    // ADR-022 §10 and compliance rule 22: no core enum may claim Full/Differential/TransactionLog apply to
    // every provider. If one is ever introduced, this fails.
    foreach (var type in DomainAssembly.GetTypes().Where(type => type.IsEnum))
    {
      var names = Enum.GetNames(type);
      var looksUniversal =
        names.Contains("Full", StringComparer.Ordinal) &&
        names.Contains("Differential", StringComparer.Ordinal) &&
        names.Contains("TransactionLog", StringComparer.Ordinal);
      Assert.False(looksUniversal, $"{type.Name} encodes SQL Server backup vocabulary as a universal enum.");
    }

    Assert.Equal(
      typeof(TenantDatabaseBackupOperation),
      typeof(TenantDatabaseBackupRun).GetProperty(nameof(TenantDatabaseBackupRun.Operation))!.PropertyType);
  }

  // The tenant-storage surface: the namespaces that own physical database metadata, plus anything named
  // after backup or recovery wherever it lives.
  private static bool IsTenantStorageType(Type type) =>
    (type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) ?? false) ||
    type.Name.Contains("Backup", StringComparison.Ordinal) ||
    type.Name.Contains("Recovery", StringComparison.Ordinal) ||
    type.Name.Contains("TenantDatabase", StringComparison.Ordinal);

  private static PlatformDbContext PlatformContext()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=(local);Database=ArchitectureModelOnly;Integrated Security=True")
      .Options;
    return new PlatformDbContext(options, new ModelUser(), new ModelTenant(), new ModelClock());
  }

  private static TenantDbContext TenantContext()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=(local);Database=ArchitectureModelOnly;Integrated Security=True")
      .Options;
    return new TenantDbContext(options, new ModelUser(), new ModelTenant(), new ModelClock());
  }

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
    public DateTimeOffset UtcNow => new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
  }
}

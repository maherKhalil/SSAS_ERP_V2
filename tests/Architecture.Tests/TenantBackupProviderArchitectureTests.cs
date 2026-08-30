using System.Reflection;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// TS-Backup Phase B guards (ADR-022). Phase B is the first slice permitted to execute a backup, so these
// protect the boundaries it must NOT cross: no scheduler, no restore, no artifact deletion, no runtime
// credential reuse, and no destination reaching the provider from above.
public sealed class TenantBackupProviderArchitectureTests
{
  private static readonly Assembly ApplicationAssembly = typeof(ITenantDatabaseBackupProvider).Assembly;

  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_execution_lives_only_in_infrastructure()
  {
    // The Application layer declares the contract; only Infrastructure may compose a backup command.
    Assert.True(typeof(ITenantDatabaseBackupProvider).IsInterface);
    Assert.Equal(InfrastructureAssembly, typeof(SqlServerTenantDatabaseBackupProvider).Assembly);
    Assert.NotNull(CommandTextType);

    // No Application type composes SQL.
    // ⚠ NINE TESTS IN THIS FILE PASSED OVER AN EMPTY TYPE SET (T-258). Each enumeration now has a
    // floor on the set its loop actually reads.
    var applicationTypes = ApplicationAssembly.GetTypes();
    Assert.True(applicationTypes.Length >= 20,
      $"only {applicationTypes.Length} Application types were found; the assembly reference is wrong " +
      "or the enumeration collapsed, and the name check below would pass reading nothing.");

    foreach (var type in applicationTypes)
    {
      Assert.DoesNotContain("BackupCommandText", type.Name, StringComparison.Ordinal);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_provider_never_depends_on_the_runtime_connection_factory()
  {
    // The central credential-separation guarantee (ADR-022 §11): the request-serving identity must never
    // hold backup privileges, so the provider cannot even reach the factory that carries it.
    var dependencies = typeof(SqlServerTenantDatabaseBackupProvider)
      .GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.DoesNotContain(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.Contains(typeof(ITenantDatabaseBackupConnectionFactory), dependencies);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_application_request_carries_no_destination_or_database_name()
  {
    // A caller contributes an identifier and a policy-derived key, never a location. This is what makes
    // destination injection structurally impossible (compliance rule 23).
    var forbidden = new[]
    {
      "Path", "Directory", "Unc", "Url", "Uri", "Endpoint", "ConnectionString", "DatabaseName",
      "ServerName", "Credential", "Password", "Secret"
    };

    foreach (var type in new[]
    {
      typeof(TenantDatabaseBackupRequest), typeof(TenantDatabaseBackupOptions),
      typeof(TenantDatabaseBackupProviderResult), typeof(TenantDatabaseBackupExecutionOutcome)
    })
    {
      foreach (var property in type.GetProperties())
      {
        Assert.DoesNotContain(
          forbidden,
          fragment => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_resolved_destination_never_escapes_infrastructure()
  {
    // The descriptor and its resolver are internal, so no Application or Domain type can hold a resolved
    // path even by accident.
    var descriptor = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.TenantStorage.TenantDatabaseBackupDestination", throwOnError: true)!;
    var resolver = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.TenantStorage.ITenantDatabaseBackupDestinationResolver", throwOnError: true)!;

    Assert.False(descriptor.IsPublic);
    Assert.False(resolver.IsPublic);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_provider_layer_still_hosts_no_loop_of_its_own()
  {
    // Originally "Phase B adds no scheduler and no hosted service". Phase C landed fleet scheduling, so the
    // blanket prohibition is retired — but the boundary underneath it is not.
    //
    // What still binds: the PROVIDER and its immediate collaborators must remain passive. Scheduling lives
    // in the scheduler types, which are exempted by exact name and separately guarded by
    // TenantBackupSchedulerArchitectureTests; nothing else in the backup surface may run a loop, host a
    // service, or become a worker.
    var schedulingComponents = new[]
    {
      "ITenantDatabaseBackupScheduler",
      "TenantDatabaseBackupScheduler",
      "TenantDatabaseBackupSchedulerOptions",
      "TenantDatabaseBackupSchedulerHostedService",
      "TenantDatabaseBackupSchedulerOptionsValidator",
      "TenantDatabaseBackupSweepSummary"
    };

    // The NAME predicate is what collapses quietly: rename the types and every assembly still loads
    // while this set empties.
    var backupComponents = InfrastructureAssembly.GetTypes()
      .Where(type => type.Name.Contains("Backup", StringComparison.Ordinal))
      .Where(type => !schedulingComponents.Contains(type.Name, StringComparer.Ordinal))
      .ToArray();

    Assert.True(backupComponents.Length >= 3,
      $"only {backupComponents.Length} non-scheduling Backup components were found; the name predicate " +
      "has stopped matching and the checks below read nothing.");

    foreach (var type in backupComponents)
    {
      Assert.DoesNotContain(typeof(Microsoft.Extensions.Hosting.IHostedService), type.GetInterfaces());
      Assert.DoesNotContain("Scheduler", type.Name, StringComparison.Ordinal);
      Assert.DoesNotContain("Worker", type.Name, StringComparison.Ordinal);
      Assert.DoesNotContain("Sweep", type.Name, StringComparison.Ordinal);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Phase_b_implements_no_restore_and_no_artifact_deletion()
  {
    // Restore verification is Phase D; the platform deletes no artifacts in V1 at all (ADR-022 §16).
    var forbidden = new[] { "Restore", "VerifyOnly", "Delete", "Purge", "Retention" };

    var backupOrRecovery = InfrastructureAssembly.GetTypes()
      .Where(type => type.Name.Contains("Backup", StringComparison.Ordinal) ||
        type.Name.Contains("Recovery", StringComparison.Ordinal))
      .ToArray();

    Assert.True(backupOrRecovery.Length >= 5,
      $"only {backupOrRecovery.Length} Backup/Recovery types were found; the name predicate has " +
      "stopped matching.");

    foreach (var type in backupOrRecovery)
    {
      foreach (var method in type.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName))
      {
        Assert.DoesNotContain(
          forbidden,
          fragment => method.Name.Contains(fragment, StringComparison.Ordinal));
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_managed_chain_can_never_emit_copy_only_or_change_a_recovery_model()
  {
    // Proven against every command the builder can produce rather than against a template string, so a later
    // edit to the template cannot quietly reintroduce either.
    foreach (var operation in new[]
    {
      TenantDatabaseBackupOperation.SqlServerFull(),
      TenantDatabaseBackupOperation.SqlServerDifferential(),
      TenantDatabaseBackupOperation.SqlServerTransactionLog()
    })
    {
      foreach (var compress in new[] { true, false })
      {
        var command = BuildCommand(operation, compress);
        Assert.DoesNotContain("COPY_ONLY", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET RECOVERY", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER DATABASE", command, StringComparison.OrdinalIgnoreCase);
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void No_production_code_requires_sysadmin_or_the_broad_server_state_permission()
  {
    // ADR-022 LOW-A: in-flight detection needs VIEW SERVER PERFORMANCE STATE on SQL Server 2022 — granted by
    // deployment, never assumed or requested by code, and never satisfied with sysadmin.
    foreach (var assembly in new[] { ApplicationAssembly, InfrastructureAssembly })
    {
      var assemblyTypes = assembly.GetTypes();
      Assert.True(assemblyTypes.Length >= 10,
        $"{assembly.GetName().Name} yielded only {assemblyTypes.Length} types; the enumeration " +
        "collapsed and the name check below read nothing.");

      foreach (var type in assemblyTypes)
      {
        Assert.DoesNotContain("Sysadmin", type.Name, StringComparison.OrdinalIgnoreCase);
      }
    }

    // And no code path grants itself anything.
    foreach (var operation in new[]
    {
      TenantDatabaseBackupOperation.SqlServerFull(),
      TenantDatabaseBackupOperation.SqlServerTransactionLog()
    })
    {
      var command = BuildCommand(operation, compress: false);
      Assert.DoesNotContain("GRANT", command, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("sp_addsrvrolemember", command, StringComparison.OrdinalIgnoreCase);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Backup_and_migration_ownership_use_distinct_non_contending_resources()
  {
    // ADR-022 §14: the two locks protect different concerns and must NOT exclude one another. All managed
    // backup types may run during a migration.
    Assert.NotEqual(typeof(TenantDatabaseBackupOwnership), typeof(TenantDatabaseMigrationOwnership));

    var backupResource = ResourceNameOf(typeof(TenantDatabaseBackupOwnership));
    var migrationResource = ResourceNameOf(typeof(TenantDatabaseMigrationOwnership));

    Assert.NotEqual(migrationResource, backupResource);
    Assert.Equal("SSAS.TenantStorage.Backup", backupResource);
  }

  // Reached by reflection rather than by reference: the command builder is internal to Infrastructure, which
  // is itself part of what this guard asserts.
  private static readonly Type CommandTextType = InfrastructureAssembly.GetType(
    "SSAS.Platform.Infrastructure.TenantStorage.SqlServerBackupCommandText", throwOnError: true)!;

  private static string BuildCommand(TenantDatabaseBackupOperation operation, bool compress) =>
    (string)CommandTextType
      .GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!
      .Invoke(null, [operation, "db", compress])!;

  private static string ResourceNameOf(Type ownershipType) =>
    (string)ownershipType
      .GetField("LockResource", BindingFlags.NonPublic | BindingFlags.Static)!
      .GetRawConstantValue()!;
}

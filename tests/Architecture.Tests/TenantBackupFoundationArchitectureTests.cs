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
      // Author-written types only. A lambda inside an exempt method compiles to a display class whose
      // generated method inherits the enclosing name — `<BeginRestoreAsync>b__0` — so without this the guard
      // would demand an exemption for machinery nobody wrote. The sibling type-vocabulary guard excludes
      // compiler-generated types for exactly the same reason.
      foreach (var type in assembly.GetTypes()
        .Where(type => !Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
        .Where(IsTenantStorageType))
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
          //
          // TS-Backup Phase D (ADR-022 §17, v1.2) adds two more, on the same terms.
          //
          // BeginRestore/BeginRestoreAsync RECORD that a restore is about to begin — the durable write that
          // makes a crashed process's orphan identifiable. They perform nothing, and the whole reason they
          // exist is that the record must precede the operation.
          //
          // SqlServerRestoreCommandText BUILDS COMMAND TEXT and never executes it, so its methods are
          // exempted BY TYPE. Restore EXECUTION belongs to a later slice and has no component here yet: an
          // execution verb appearing on any other tenant-storage type still fails this guard, which is
          // precisely the boundary worth holding while the destructive half of the capability is unwritten.
          //
          // CanRestoreInto is a PREDICATE — it answers whether a name may be used as a verification target,
          // and its whole purpose is to REFUSE. Reading it as a restore capability would flag the very guard
          // that prevents one.
          //
          // TS-Backup Phase D6: RESTORE EXECUTION NOW EXISTS, in exactly one type. Exempted BY TYPE, on the
          // same terms as the backup provider in Phase B — the boundary this guard protects is that an
          // execution verb appearing on any OTHER tenant-storage type still fails, which is what stops a
          // second restore path arriving unnoticed.
          if (method.Name is "RecordVerification" or "BeginRestore" or "BeginRestoreAsync" or "CanRestoreInto" ||
            (method.Name is "ExecuteBackupAsync" && type.Name is "SqlServerTenantDatabaseBackupProvider") ||
            type.Name is "SqlServerRestoreCommandText" or
              "SqlServerTenantDatabaseRestoreVerificationProvider" or
              "ITenantDatabaseRestoreVerificationProvider" or
              "TenantDatabaseBackupChainSelector")
          {
            continue;
          }

          // The offender is NAMED in the failure. A guard that says only "something matched" costs more to
          // diagnose than it saves, and this one fires whenever a slice legitimately extends the surface.
          var matched = Array.Find(
            forbiddenFragments,
            fragment => method.Name.Contains(fragment, StringComparison.Ordinal));
          Assert.True(matched is null,
            $"{type.FullName}.{method.Name} contains the execution verb '{matched}'.");
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
      "TenantDatabaseBackupSchedulerHostedService",
      "TenantDatabaseBackupSchedulerOptionsValidator",

      // TS-Backup Phase D (ADR-022 §17, v1.2): the restore-verification FOUNDATION — configuration, the
      // credential boundary and the durable operation record. Exempted by exact name, and note what is
      // deliberately still absent: no verification scheduler, no hosted service, no restore provider, no
      // orphan-cleanup worker. Each of those would fail this guard until a slice adds it explicitly.
      "TenantDatabaseRestoreVerificationOptions",
      "TenantDatabaseRestoreVerificationOptionsValidator",
      "TenantDatabaseRestoreVerificationRunStore",
      "AddTenantDatabaseRestoreVerification",
      "TenantDatabaseRestoreVerificationRunConfiguration",

      // TS-Backup Phase D7: post-restore probes and the executor contract.
      "SqlServerRestoreVerificationProbe",
      "TenantDatabaseRestoreProbeResult",
      "TenantDatabaseRestoreProbeOutcome",
      "TenantDatabaseRestoreVerificationExecutor",
      "TenantDatabaseRestoreSequenceResult",
      "AddBackupCheckpointLsn",

      // TS-Backup Phase D6: the isolated restore provider and its contract. Still no verification SCHEDULER,
      // no hosted service and no cleanup worker — each of those would fail this guard until added explicitly.
      "SqlServerTenantDatabaseRestoreVerificationProvider",
      "ITenantDatabaseRestoreVerificationProvider",
      "TenantDatabaseRestoreVerificationRequest",
      "TenantDatabaseRestoreVerificationResult",
       "TenantDatabaseRestoreVerificationOutcome"
       ,"SqlServerTenantDatabaseRestoreVerificationServerObserver"
       ,"TenantDatabaseRestoreVerificationReconciler"
       ,"ITenantDatabaseRestoreVerificationReconciler"
       ,"TenantDatabaseRestoreVerificationScheduler"
       ,"ITenantDatabaseRestoreVerificationScheduler"
       ,"TenantDatabaseRestoreVerificationHostedService"
       ,"TenantDatabaseRestoreVerificationFleetReadRepository"
       ,"TenantDatabaseRestoreVerificationReconciliationSummary"
       ,"TenantDatabaseRestoreVerificationSweepSummary"
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

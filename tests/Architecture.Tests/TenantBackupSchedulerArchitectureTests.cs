using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// TS-Backup Phase C guards (ADR-022 §13, v1.1).
//
// Phase C is the first slice that runs backups UNATTENDED. Everything the earlier phases enforced about
// authority, destination trust and credential separation now has to hold without anyone watching, so these
// guards are mostly about what the scheduler is structurally unable to reach.
public sealed class TenantBackupSchedulerArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  private static readonly string[] OffsetPagingVocabulary = ["skip", "offset", "pageNumber", "pageIndex"];

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void There_is_exactly_one_backup_scheduler_hosted_service()
  {
    // One loop. A second hosted service would double the fleet's sweep rate invisibly, and Phase B's
    // ownership would absorb the duplication as skips rather than as an obvious fault.
    var hostedBackupServices = InfrastructureAssembly.GetTypes()
      .Where(type => typeof(IHostedService).IsAssignableFrom(type))
      .Where(type => type.Name.Contains("Backup", StringComparison.Ordinal))
      .ToArray();

    Assert.Single(hostedBackupServices);
    Assert.Equal(typeof(TenantDatabaseBackupSchedulerHostedService), hostedBackupServices[0]);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_scheduler_reaches_sql_server_only_through_the_executor()
  {
    // THE CENTRAL PHASE C BOUNDARY. The scheduler decides WHICH database and WHICH operation; the executor
    // decides whether it may happen and owns the run lifecycle. Letting the scheduler hold a provider would
    // route around every authority check the executor performs.
    var dependencies = ConstructorDependencies(typeof(TenantDatabaseBackupScheduler));

    Assert.Contains(typeof(ITenantDatabaseBackupExecutor), dependencies);
    Assert.DoesNotContain(typeof(ITenantDatabaseBackupProvider), dependencies);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_scheduler_cannot_reach_destinations_connections_or_credentials()
  {
    // It never learns where a backup is written or how the server is reached. Those resolve inside
    // Infrastructure at execution time, from trusted configuration (ADR-022 §11, compliance rule 23).
    var dependencies = ConstructorDependencies(typeof(TenantDatabaseBackupScheduler))
      .Concat(ConstructorDependencies(typeof(TenantDatabaseBackupSchedulerHostedService)))
      .Select(type => type.Name)
      .ToArray();

    foreach (var forbidden in new[]
    {
      "ITenantDatabaseBackupConnectionFactory", "ITenantDatabaseConnectionFactory",
      "ITenantDatabaseBackupDestinationResolver", "TenantStorageOptions", "TenantDatabaseBackupDestination"
    })
    {
      Assert.DoesNotContain(forbidden, dependencies);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_scheduler_surface_carries_no_destination_or_credential_vocabulary()
  {
    // The projection the scheduler works from names an identifier, a routing bucket and timings — never a
    // location or a secret. Asserted on the type surface so a later field cannot quietly widen it.
    var forbidden = new[]
    {
      "Path", "Directory", "Unc", "Url", "Uri", "Endpoint", "ConnectionString",
      "DatabaseName", "Credential", "Password", "Secret"
    };

    foreach (var type in new[]
    {
      typeof(TenantDatabaseBackupDueCandidate), typeof(TenantDatabaseBackupSweepSummary),
      typeof(TenantDatabaseBackupSchedulerOptions)
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
  public void Phase_c_adds_no_restore_retention_or_deletion_capability()
  {
    // Restore verification is Phase D; the platform deletes no artifacts in V1 at all (ADR-022 §16).
    var forbidden = new[] { "Restore", "VerifyOnly", "Delete", "Purge", "Retention", "Prune", "Sweep" };

    foreach (var type in SchedulerTypes())
    {
      foreach (var method in type.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName))
      {
        // RunSweepAsync is the sweep itself; "Sweep" is forbidden only as a deletion verb elsewhere.
        if (method.Name is "RunSweepAsync" or "RunOneSweepAsync")
        {
          continue;
        }

        Assert.DoesNotContain(
          forbidden,
          fragment => method.Name.Contains(fragment, StringComparison.Ordinal));
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Only_automatic_platform_managed_databases_are_ever_selected()
  {
    // Eligibility is enforced in the evaluator as well as in SQL, so a repository change cannot silently
    // widen what the scheduler will dispatch. CustomerManaged, CustomerDba and PlatformAfterApproval are all
    // refused here, and again by the executor (ADR-022 §5, §12).
    foreach (var mode in Enum.GetValues<TenantDatabaseBackupManagementMode>())
    {
      var candidate = EligibleCandidate() with { ManagementMode = mode };
      var eligible = TenantDatabaseBackupDueEvaluator.IsEligible(candidate);

      Assert.Equal(mode == TenantDatabaseBackupManagementMode.AutomaticByPlatform, eligible);
    }

    foreach (var hosting in Enum.GetValues<TenantDatabaseHostingMode>())
    {
      var candidate = EligibleCandidate() with { HostingMode = hosting };
      var eligible = TenantDatabaseBackupDueEvaluator.IsEligible(candidate);

      Assert.Equal(hosting == TenantDatabaseHostingMode.PlatformManaged, eligible);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void In_flight_safety_has_no_disable_switch_anywhere_in_production_options()
  {
    // ADR-022 v1.1 compliance rule 29: the in-flight check is a correctness precondition, not a feature
    // flag. Phase C removed `InFlightDetectionEnabled`; this stops it — or anything like it — coming back.
    var operationalProperties = typeof(TenantDatabaseBackupOperationalOptions).GetProperties();

    foreach (var property in operationalProperties)
    {
      Assert.False(
        property.Name.Contains("InFlight", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Visibility", StringComparison.OrdinalIgnoreCase),
        $"{property.Name} looks like a switch over in-flight safety, which must not be configurable");
    }

    // The scheduler's own options may disable SCHEDULING — that is legitimate — but must not reach the
    // provider's safety checks.
    foreach (var property in typeof(TenantDatabaseBackupSchedulerOptions).GetProperties())
    {
      Assert.False(
        property.Name.Contains("InFlight", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Visibility", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Safety", StringComparison.OrdinalIgnoreCase),
        $"{property.Name} would let scheduling configuration weaken execution safety");
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Fleet_discovery_is_keyset_paged_by_identifier()
  {
    // Keyset, not OFFSET (ADR-022 §13). Asserted on the contract's shape: the page is requested by an
    // exclusive cursor and a size, which is a query OFFSET paging cannot express.
    var list = typeof(ITenantDatabaseBackupFleetReadRepository)
      .GetMethod(nameof(ITenantDatabaseBackupFleetReadRepository.ListBackupCandidatesAsync))!;

    // Discovery is per server AND keyset-paged: the page is requested for one ServerKey, from an exclusive
    // cursor, with a bounded size. The server dimension is what makes fairness cross page boundaries; the
    // cursor is what keeps paging off OFFSET.
    var parameters = list.GetParameters();
    Assert.Equal("serverKey", parameters[0].Name);
    Assert.Equal("afterId", parameters[1].Name);
    Assert.Equal(typeof(long), parameters[1].ParameterType);
    Assert.Equal("take", parameters[2].Name);

    // And no "skip"/"offset"/"page number" vocabulary anywhere on the contract.
    foreach (var method in typeof(ITenantDatabaseBackupFleetReadRepository).GetMethods())
    {
      foreach (var parameter in method.GetParameters())
      {
        Assert.DoesNotContain(
          OffsetPagingVocabulary,
          fragment => string.Equals(parameter.Name, fragment, StringComparison.OrdinalIgnoreCase));
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void Phase_c_persists_no_scheduler_state()
  {
    // No scheduler table, no lease table, no NextDueUtc. Due-ness is derived from policy plus the
    // successful-backup timestamps Phase B already maintains, so there is no second source of truth to
    // drift out of step with the fleet.
    // Scoped to the concepts Phase C decided not to persist, rather than to any entity whose name happens to
    // contain a word. A future phase may legitimately need an entity called something-Lease; what it may not
    // do is give the BACKUP SCHEDULER persisted state, because due-ness is derived from policy plus the
    // successful-backup timestamps Phase B already maintains.
    var model = PlatformModel();

    foreach (var entity in model.GetEntityTypes())
    {
      var name = entity.ClrType.Name;

      Assert.False(
        name.Contains("BackupScheduler", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("SchedulerLease", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("BackupLease", StringComparison.OrdinalIgnoreCase),
        $"{name} would give the backup scheduler persisted state");

      // NextDueUtc is the specific denormalisation Phase C rejected: a second source of truth for due-ness
      // that can drift out of step with the timestamps it duplicates.
      foreach (var property in entity.GetProperties())
      {
        Assert.DoesNotContain("NextDue", property.Name, StringComparison.OrdinalIgnoreCase);
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public void The_scheduler_introduces_no_runtime_credential_reuse()
  {
    // The request-serving identity must never gain backup privileges (ADR-022 §11). Phase C adds a new
    // caller into the backup path, so the assertion is repeated against it.
    foreach (var type in SchedulerTypes())
    {
      var dependencies = ConstructorDependencies(type).Select(dependency => dependency.Name).ToArray();
      Assert.DoesNotContain("ITenantDatabaseConnectionFactory", dependencies);
      Assert.DoesNotContain("ITenantDbContextFactory", dependencies);
    }
  }

  private static Type[] SchedulerTypes() =>
    [typeof(TenantDatabaseBackupScheduler), typeof(TenantDatabaseBackupSchedulerHostedService)];

  private static Type[] ConstructorDependencies(Type type) =>
    [.. type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)];

  private static TenantDatabaseBackupDueCandidate EligibleCandidate() =>
    new(
      1, "PrimarySqlServer",
      TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseProvisioningStatus.Ready,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      PolicyEnabled: true,
      FullBackupIntervalMinutes: 60,
      DifferentialBackupIntervalMinutes: null,
      TransactionLogBackupIntervalMinutes: null,
      LastSuccessfulFullBackupUtc: null,
      LastSuccessfulDifferentialBackupUtc: null,
      LastSuccessfulLogBackupUtc: null);

  private static Microsoft.EntityFrameworkCore.Metadata.IModel PlatformModel()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=architecture-test;Database=model-only;Integrated Security=True")
      .Options;
    using var context = new PlatformDbContext(options, new ModelUser(), new ModelTenant(), new ModelClock());
    return context.Model;
  }

  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => null;

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
    public DateTimeOffset UtcNow => new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
  }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// TS-1A/TS-1B guards (ADR-017). These protect the boundaries the slice deliberately did NOT cross, so a
// later slice cannot quietly widen it: no routing runtime, no customer-managed connectivity, no tenant
// DbContext, no HTTP surface, and no credential material in the registry.
public sealed class TenantStorageRegistryArchitectureTests
{
  private static readonly Assembly DomainAssembly = typeof(TenantDatabase).Assembly;
  private static readonly Assembly InfrastructureAssembly = typeof(PlatformDbContext).Assembly;

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Registry_entities_are_platform_metadata_not_tenant_owned()
  {
    // Registry rows describe physical databases and routing, not tenant business data. If either became
    // ITenantOwnedEntity the global filter would hide routing from the very infrastructure that resolves
    // it, and bootstrap could not run without an ambient tenant.
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabase).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabaseAssignment).GetInterfaces());
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Registry_entities_persist_no_credential_or_endpoint_material()
  {
    // ADR-017: only trusted routing metadata is persisted. Complete connection strings, passwords,
    // certificates, private keys, and customer endpoint/credential-reference fields must never appear.
    //
    // Substring terms are chosen to be unambiguous; "Host" and "Port" are matched as EXACT names instead,
    // because HostingMode is a legitimate routing dimension and "Port" collides with ordinary words.
    string[] forbiddenSubstrings =
    [
      "ConnectionString", "Password", "Secret", "Credential", "Certificate", "PrivateKey",
      "Endpoint", "AuthenticationMode"
    ];
    string[] forbiddenExactNames = ["Host", "Port", "ServerInstanceName", "Address"];

    foreach (var type in new[] { typeof(TenantDatabase), typeof(TenantDatabaseAssignment) })
    {
      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        Assert.DoesNotContain(forbiddenSubstrings, term =>
          property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(forbiddenExactNames, name =>
          string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
      }
    }
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public void No_customer_managed_runtime_path_exists()
  {
    // CustomerManaged is an architecture-ready enum value only. Nothing may connect to, resolve secrets
    // for, or validate connectivity to a customer-managed database in this slice.
    var offenders = InfrastructureAssembly.GetTypes()
      .Where(type => type.Name.Contains("CustomerManaged", StringComparison.Ordinal) ||
        type.Name.Contains("CustomerDatabase", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_context_is_distinct_from_the_platform_context()
  {
    // TenantDbContext must be a SEPARATE context, not PlatformDbContext renamed or subclassed. Both derive
    // from the shared persistence base so the tenant guard and audit rules are one implementation, but
    // neither may derive from the other — that would make the two planes one model again.
    Assert.NotEqual(typeof(TenantDbContext), typeof(PlatformDbContext));
    Assert.False(typeof(PlatformDbContext).IsAssignableFrom(typeof(TenantDbContext)));
    Assert.False(typeof(TenantDbContext).IsAssignableFrom(typeof(PlatformDbContext)));
    Assert.True(typeof(PersistenceDbContext).IsAssignableFrom(typeof(TenantDbContext)));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_two_contexts_own_disjoint_entities()
  {
    // The boundary is asserted on the built EF models rather than on file locations, so moving a
    // configuration file cannot silently move an entity across the plane boundary.
    var tenantEntities = TenantModelEntities();
    var platformEntities = PlatformModelEntities();

    Assert.Contains(typeof(Company), tenantEntities);
    Assert.DoesNotContain(typeof(Company), platformEntities);
    Assert.Empty(tenantEntities.Intersect(platformEntities));

    // Platform authority and identity data must never appear in a tenant database: it is what keeps
    // authentication independent of tenant-storage availability (ADR-017 platform database boundary).
    foreach (var platformOnly in new[]
      {
        typeof(Tenant), typeof(TenantUser), typeof(Role), typeof(PlatformSupportPrincipal),
        typeof(TenantDatabase), typeof(TenantDatabaseAssignment)
      })
    {
      Assert.DoesNotContain(platformOnly, tenantEntities);
      Assert.Contains(platformOnly, platformEntities);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Tenant_entities_are_tenant_owned_and_retain_their_tenant_id()
  {
    // Every entity in the tenant model must be tenant-owned, so the global filter and write guard apply to
    // all of it. TenantId is retained even in a dedicated database (ADR-017 "TenantId retention").
    foreach (var entity in TenantModelEntities())
    {
      Assert.Contains(typeof(ITenantOwnedEntity), entity.GetInterfaces());
    }

    var company = TenantModel().FindEntityType(typeof(Company));
    Assert.NotNull(company);
    Assert.NotNull(company!.FindProperty(nameof(ITenantOwnedEntity.TenantId)));
    Assert.NotNull(company.GetQueryFilter());
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_model_has_no_cross_database_relationship_to_the_platform_plane()
  {
    // A foreign key or navigation from Company to Tenant would be a cross-database reference the moment a
    // tenant is moved to a dedicated catalog — prohibited, and impossible to satisfy.
    var company = TenantModel().FindEntityType(typeof(Company));
    Assert.NotNull(company);
    Assert.Empty(company!.GetForeignKeys());
    Assert.Empty(company.GetNavigations());
    Assert.DoesNotContain(TenantModelEntities(), entity => entity == typeof(Tenant));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_model_is_tenant_invariant()
  {
    // Two models built for different tenants must be identical in shape. EF caches the model per options,
    // so any tenant-conditional configuration would let one tenant's model serve another (ADR-017 rule 3).
    static string[] Describe(Guid tenantId) =>
      [.. BuildTenantContext(tenantId).Model.GetEntityTypes()
        .SelectMany(entity => entity.GetProperties()
          .Select(property => $"{entity.ClrType.Name}.{property.Name}:{property.GetColumnName()}"))
        .OrderBy(value => value, StringComparer.Ordinal)];

    Assert.Equal(Describe(Guid.NewGuid()), Describe(Guid.NewGuid()));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_tenant_context_is_never_pooled_and_never_captures_a_connection_at_registration()
  {
    // Pooled contexts could carry connection identity across tenants (ADR-017 rule 4), and options built
    // once at registration would pin every tenant to the first route resolved (rule 2). The factory is
    // therefore the only construction path, and it takes the tenant per call.
    var registration = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence",
      "PlatformPersistenceServiceCollectionExtensions.cs"));

    Assert.DoesNotContain("AddDbContextPool<TenantDbContext>", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("PooledDbContextFactory", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("AddDbContext<TenantDbContext>", registration, StringComparison.Ordinal);

    var create = typeof(ITenantDbContextFactory).GetMethod(nameof(ITenantDbContextFactory.CreateAsync));
    Assert.NotNull(create);
    Assert.Contains(create!.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_tenant_migration_stream_is_separate_from_the_platform_stream()
  {
    // Separate history table AND separate schema, so neither stream's applied migrations can be read as
    // the other's. Migration files live under the tenant folder for the same reason.
    Assert.Equal(TenantPersistenceConstants.Schema, TenantPersistenceConstants.MigrationHistorySchema);
    Assert.NotEqual(PlatformPersistenceConstants.Schema, TenantPersistenceConstants.MigrationHistorySchema);

    var tenantMigrations = InfrastructureAssembly.GetTypes()
      .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
      .Where(type => type.Namespace?.Contains("TenantErp", StringComparison.Ordinal) == true)
      .ToArray();
    Assert.NotEmpty(tenantMigrations);

    // No migration type is shared between the streams.
    var platformMigrations = InfrastructureAssembly.GetTypes()
      .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
      .Where(type => type.Namespace?.Contains("TenantErp", StringComparison.Ordinal) != true)
      .ToArray();
    Assert.NotEmpty(platformMigrations);
    Assert.Empty(tenantMigrations.Intersect(platformMigrations));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Tenant_context_creation_cannot_bypass_the_routed_connection_factory()
  {
    // The factory composes resolver + connection factory. If it built provider options directly it could
    // reach a database the connection factory would have refused — CustomerManaged, or an unconfigured
    // ServerKey — so the dependency on ITenantDatabaseConnectionFactory is itself the guard.
    var dependencies = typeof(TenantDbContextFactory).GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantDatabaseResolver), dependencies);
    Assert.Contains(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.DoesNotContain(typeof(PlatformDbContext), dependencies);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Health_state_lives_on_the_physical_database_never_on_the_assignment()
  {
    // A shared database has ONE schema and ONE migration state. Copying that onto every tenant's
    // assignment would create rows that can disagree with each other about the same physical database.
    string[] healthProperties =
    [
      "ConnectivityStatus", "SchemaCompatibilityStatus", "MigrationExecutionStatus",
      "MigrationManagementMode", "LastSchemaCheckUtc", "AppliedMigration"
    ];

    foreach (var property in healthProperties)
    {
      Assert.NotNull(typeof(TenantDatabase).GetProperty(property));
      Assert.Null(typeof(TenantDatabaseAssignment).GetProperty(property));
    }
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_request_path_never_migrates_and_startup_never_fleet_migrates()
  {
    // DDL authority does not belong in the process serving requests, and startup must not scale with
    // estate size. Asserted over source text because the property is the ABSENCE of a call.
    var infrastructure = Path.Combine(RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure");

    foreach (var file in new[]
      {
        Path.Combine(infrastructure, "Persistence", "TenantErp", "TenantDbContextFactory.cs"),
        Path.Combine(infrastructure, "Persistence", "TenantErp", "TenantDbContextProvider.cs"),
        Path.Combine(infrastructure, "Persistence", "TenantErp", "TenantUnitOfWork.cs"),
        Path.Combine(infrastructure, "Persistence", "Repositories", "CompanyRepository.cs"),
        Path.Combine(infrastructure, "Persistence", "Queries", "CompanyReadService.cs")
      })
    {
      var source = File.ReadAllText(file);
      Assert.DoesNotContain("MigrateAsync", source, StringComparison.Ordinal);
      Assert.DoesNotContain("EnsureCreated", source, StringComparison.Ordinal);
    }

    // No hosted service migrates. The orchestrator is invoked explicitly, never registered to run itself.
    var registration = File.ReadAllText(Path.Combine(
      infrastructure, "Persistence", "PlatformPersistenceServiceCollectionExtensions.cs"));
    Assert.DoesNotContain("AddHostedService<TenantDatabaseMigration", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("AddHostedService<TenantDatabaseSchemaHealth", registration, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_traffic_gate_is_consulted_before_a_connection_is_built()
  {
    // Gating that ran after connection construction would already have reached the database it is meant
    // to keep traffic away from.
    var source = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure",
      "Persistence", "TenantErp", "TenantDbContextFactory.cs"));

    var gateIndex = source.IndexOf("trafficGate.Evaluate", StringComparison.Ordinal);
    var connectionIndex = source.IndexOf("connectionFactory.Create", StringComparison.Ordinal);

    Assert.True(gateIndex > 0, "The tenant context factory must consult the traffic gate.");
    Assert.True(connectionIndex > gateIndex, "The traffic gate must be evaluated before a connection is built.");
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Schema_health_never_reports_backup_or_recovery_readiness()
  {
    // TS-Backup is a separate dimension (ADR-018). A schema-compatible database is not automatically a
    // recoverable one, and folding the two would let a green release imply a durability guarantee that
    // nothing has established.
    foreach (var type in new[]
      {
        typeof(TenantDatabaseHealth), typeof(TenantDatabaseDescriptor),
        typeof(TenantDatabaseSchemaHealthResult), typeof(TenantDatabaseHealthSweepSummary)
      })
    {
      Assert.DoesNotContain(type.GetProperties(), property =>
        property.Name.Contains("Backup", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Recovery", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Restore", StringComparison.OrdinalIgnoreCase));
    }

    // And no backup RUNTIME exists. TS-Backup Phase A (ADR-022) adds backup METADATA — policy, run history
    // and their persistence — which is why the permitted names are enumerated rather than the check being
    // dropped. Anything else named after backup or restore still fails here, so a provider, scheduler,
    // restore worker or verification runtime cannot arrive unnoticed under a later slice.
    var phaseAMetadata = new[]
    {
      "TenantDatabaseBackupPolicyConfiguration",
      "TenantDatabaseBackupRunConfiguration",
      "TenantDatabaseBackupReadRepository",
      "AddTenantDatabaseBackupRecoveryFoundation",

      // TS-Backup Phase D (ADR-022 §17, v1.2): verification persistence. Metadata and its migration, on the
      // same terms as Phase A's — an orphan-cleanup worker or a restore provider is still not on this list.
      "TenantDatabaseRestoreVerificationRunConfiguration",
      "AddTenantDatabaseRestoreVerification",

      // TS-Backup Phase B (ADR-022): single-database SQL Server backup execution. Enumerated by exact name
      // for the same reason as above — a scheduler, restore worker or retention service would still fail
      // this guard, because none of them is on this list.
      "ITenantDatabaseBackupConnectionFactory",
      "TenantDatabaseBackupConnectionFactory",
      "ITenantDatabaseBackupDestinationResolver",
      "TenantDatabaseBackupDestinationResolver",
      "TenantDatabaseBackupDestination",
      "TenantDatabaseBackupOwnership",
      "TenantDatabaseBackupOperationalOptions",
      "ITenantDatabaseBackupRunStore",
      "TenantDatabaseBackupRunStore",
      "TenantDatabaseBackupExecutor",
      "SqlServerBackupCommandText",
      "SqlServerTenantDatabaseBackupProvider",
      "TenantStorageBackupDestinationOptions",

      // Phase B remediation: the visibility check and evidence reconciliation were extracted from the
      // provider so a low-privilege test principal can exercise the PRODUCTION code rather than a copy of
      // its SQL. Still execution components, still enumerated by exact name.
      "SqlServerBackupVisibility",
      "SqlServerBackupEvidence",
      "SqlServerBackupEvidenceRecord",

      // TS-Backup Phase C (ADR-022 §13): fleet scheduling. Enumerated by exact name for the same reason as
      // above — a restore worker, retention service or artifact-deletion component would still fail this
      // guard, because none of them is on this list.
      "ITenantDatabaseBackupScheduler",
      "TenantDatabaseBackupScheduler",
      "TenantDatabaseBackupSchedulerOptions",
      "TenantDatabaseBackupSchedulerHostedService",
      "TenantDatabaseBackupSchedulerOptionsValidator",
      "TenantDatabaseBackupSweepSummary",
      "TenantDatabaseBackupFleetReadRepository",

      // TS-Backup Phase D (ADR-022 §17, v1.2): the restore-verification foundation. Enumerated by exact name
      // for the same reason as everything above — a retention worker, an artifact-deletion component or a
      // Phase E cutover guard would still fail this guard, because none of them is on this list.
      //
      // NOTE what is NOT here and must not be added without a decision: no orphan-cleanup worker, no
      // verification scheduler, no restore provider. Those arrive in later slices and each one is a
      // deliberate addition rather than a category this guard already waves through.
      "TenantDatabaseRestoreVerificationOptions",
      "TenantDatabaseRestoreVerificationOptionsValidator",
      "TenantDatabaseRestoreVerificationRunStore",
      "ITenantDatabaseVerificationConnectionFactory",
      "TenantDatabaseVerificationConnectionFactory",
      "TenantDatabaseVerificationTarget",
      "TenantDatabaseVerificationFileLayout",
      "TenantDatabaseVerificationFilePlacement",
      "TenantDatabaseBackupFileEntry",
      "SqlServerRestoreCommandText",
      "TenantDatabaseRestoreStep",

      // TS-Backup Phase D5/D6 (ADR-022 §17): deterministic chain selection and the isolated restore
      // provider. Enumerated by exact name on the same terms as everything above — a retention worker, an
      // orphan-cleanup worker or a Phase E cutover guard would still fail this guard.
      "SqlServerTenantDatabaseRestoreVerificationProvider",
      "TenantDatabaseRestoreDevice",

      // TS-Backup Phase D7 (ADR-022 §17): post-restore probes and the checkpoint-LSN migration.
      "SqlServerRestoreVerificationProbe",
      "TenantDatabaseRestoreProbeResult",
      "TenantDatabaseRestoreVerificationExecutor",
      "TenantDatabaseRestoreSequenceResult",
      "AddBackupCheckpointLsn",
       "TenantDatabaseRestoreProbeOutcome"
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

    var offenders = InfrastructureAssembly.GetTypes()
      // Top-level, author-written types only. Nested records and compiler-generated async state machines
      // inherit their enclosing type's vocabulary ("BackupEvidence", "<ExecuteBackupAsync>d__8") and would
      // otherwise have to be enumerated one by one, which would make the allow-list noise rather than a
      // boundary. Excluding them structurally keeps the guard about COMPONENTS.
      .Where(type => !type.IsNested &&
        !Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
      .Where(type => type.Name.Contains("Backup", StringComparison.OrdinalIgnoreCase) ||
        type.Name.Contains("Restore", StringComparison.OrdinalIgnoreCase))
      .Where(type => !phaseAMetadata.Contains(type.Name, StringComparer.Ordinal))
      .Select(type => type.FullName)
      .ToArray();
    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Health_and_migration_reach_databases_only_through_the_trusted_connection_factory()
  {
    // A second, weaker connection path would let health or migration reach a database that request
    // routing would refuse — including a customer-managed one.
    foreach (var type in new[]
      {
        typeof(TenantDatabaseSchemaHealthService), typeof(TenantDatabaseMigrationOrchestrator)
      })
    {
      var dependencies = type.GetConstructors()
        .SelectMany(constructor => constructor.GetParameters())
        .Select(parameter => parameter.ParameterType)
        .ToArray();
      Assert.Contains(typeof(ITenantDatabaseConnectionFactory), dependencies);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_tenant_design_time_factory_has_no_silent_connection_fallback()
  {
    // The command is published operational procedure now; a silent localhost default would make a
    // forgotten environment variable look like a successful migration.
    var source = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure",
      "Persistence", "TenantErp", "TenantDbContextDesignTimeFactory.cs"));

    Assert.DoesNotContain("Server=localhost", source, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("??", source.Split("ResolveConnectionString")[0], StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Each_health_dimension_has_its_own_writer_method()
  {
    // ONE WRITER PER DIMENSION. The previous single `RecordHealthAsync(id, mutate)` let any caller write
    // any combination of dimensions, which is how a connectivity check came to erase schema state it had
    // never observed. A dimension-scoped API makes that mistake hard to express — and it matters more once
    // recovery readiness (TS-Backup) becomes a third writer on the same row.
    var writer = typeof(ITenantDatabaseHealthWriter);
    var methods = writer.GetMethods().Select(method => method.Name).ToArray();

    Assert.Contains(nameof(ITenantDatabaseHealthWriter.RecordConnectivityAsync), methods);
    Assert.Contains(nameof(ITenantDatabaseHealthWriter.RecordSchemaAsync), methods);

    // No general-purpose "write whatever you like" entry point remains.
    Assert.DoesNotContain("RecordHealthAsync", methods);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_connectivity_service_never_writes_schema_state()
  {
    // Asserted over source because the property is the ABSENCE of a call: a connectivity probe that wrote
    // any schema-owned value would reintroduce the exact defect this split removes.
    var source = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "TenantStorage",
      "TenantDatabaseConnectivityHealthService.cs"));

    Assert.DoesNotContain("RecordSchemaAsync", source, StringComparison.Ordinal);
    Assert.DoesNotContain("RecordSchemaHealth", source, StringComparison.Ordinal);
    Assert.DoesNotContain("SchemaCompatibilityStatus", source, StringComparison.Ordinal);
    // It also has no business reading migration history — that is the schema service's job.
    Assert.DoesNotContain("GetAppliedMigrations", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TenantDbContextBuilder", source, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_schema_service_writes_schema_only_when_schema_was_observed()
  {
    // The result carries an explicit observation flag, and persistence is gated on it. Without that gate a
    // failed connection would once again write Unknown over a verdict it never looked at.
    Assert.NotNull(typeof(TenantDatabaseSchemaHealthResult).GetProperty("SchemaObserved"));

    var source = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "TenantStorage",
      "TenantDatabaseSchemaHealthService.cs"));

    Assert.Contains("SchemaObserved", source, StringComparison.Ordinal);
    Assert.Contains("if (!result.SchemaObserved)", source, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Connectivity_and_schema_freshness_read_separate_clocks()
  {
    // Schema age must come from LastSchemaCheckUtc alone. If the gate consulted the connectivity timestamp,
    // a frequent connectivity cadence would silently keep a stale schema verdict looking fresh forever.
    var gate = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Application", "TenantStorage",
      "TenantDatabaseTrafficGate.cs"));

    Assert.Contains("EvaluateFreshness(health.LastSchemaCheckUtc", gate, StringComparison.Ordinal);
    Assert.DoesNotContain("EvaluateFreshness(health.LastConnectivityCheckUtc", gate, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_connectivity_service_reaches_databases_only_through_the_trusted_connection_factory()
  {
    var dependencies = typeof(TenantDatabaseConnectivityHealthService).GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ITenantDatabaseConnectionFactory), dependencies);
    Assert.Contains(typeof(ITenantDatabaseHealthWriter), dependencies);
    // No schema dependency at all — the split is structural, not merely behavioural.
    Assert.DoesNotContain(typeof(ITenantDatabaseSchemaHealthService), dependencies);
  }

  private static IModel TenantModel() => BuildTenantContext(Guid.NewGuid()).Model;

  private static Type[] TenantModelEntities() =>
    [.. TenantModel().GetEntityTypes().Select(entity => entity.ClrType)];

  private static Type[] PlatformModelEntities()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=architecture-test;Database=model-only;Integrated Security=True")
      .Options;
    using var context = new PlatformDbContext(options, new ModelUser(), new ModelTenant(null), new ModelClock());
    return [.. context.Model.GetEntityTypes().Select(entity => entity.ClrType)];
  }

  // Model construction only — no connection is ever opened.
  private static TenantDbContext BuildTenantContext(Guid tenantId)
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=architecture-test;Database=model-only;Integrated Security=True")
      .Options;
    return new TenantDbContext(options, new ModelUser(), new ModelTenant(tenantId), new ModelClock());
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

  private sealed class ModelTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 14, 11, 0, 0, TimeSpan.Zero);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_resolver_is_application_level_and_free_of_ambient_request_context()
  {
    // The resolver must be equally usable from a background worker, so it may not depend on HTTP context.
    // Taking the tenant as an explicit parameter is what keeps that true.
    Assert.Equal(typeof(ITenantDatabaseResolver).Assembly, typeof(TenantDatabaseResolver).Assembly);

    var dependencies = typeof(TenantDatabaseResolver).GetConstructors()
      .SelectMany(constructor => constructor.GetParameters())
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();
    Assert.DoesNotContain("IHttpContextAccessor", dependencies);

    var resolveMethod = typeof(ITenantDatabaseResolver).GetMethod(nameof(ITenantDatabaseResolver.ResolveAsync));
    Assert.NotNull(resolveMethod);
    Assert.Contains(resolveMethod!.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void The_route_carries_only_non_secret_metadata()
  {
    // The route crosses into the Application layer, so anything on it may reach logs and diagnostics.
    string[] forbidden =
    [
      "ConnectionString", "Password", "Username", "Secret", "Credential", "Certificate", "PrivateKey",
      "Endpoint", "Token", "AuthenticationMode"
    ];

    foreach (var property in typeof(TenantDatabaseRoute).GetProperties())
    {
      Assert.DoesNotContain(forbidden, term =>
        property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Connection_construction_stays_in_infrastructure()
  {
    // Credential material must not travel through the Application layer; the factory therefore lives in
    // Infrastructure and returns an open-able connection rather than a credentialed string.
    Assert.Equal(InfrastructureAssembly, typeof(ITenantDatabaseConnectionFactory).Assembly);
    Assert.Equal(InfrastructureAssembly, typeof(TenantDatabaseConnectionFactory).Assembly);

    // Both overloads — route-addressed for the request path, physical-database-addressed for health and
    // migration — must return an open-able connection rather than a credentialed string.
    var createMethods = typeof(ITenantDatabaseConnectionFactory)
      .GetMethods()
      .Where(method => method.Name == nameof(ITenantDatabaseConnectionFactory.Create))
      .ToArray();

    Assert.NotEmpty(createMethods);
    foreach (var createMethod in createMethods)
    {
      Assert.DoesNotContain("String", createMethod.ReturnType.GenericTypeArguments.Select(type => type.Name));
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void Every_routing_cache_is_declared_and_version_gated()
  {
    // SUPERSEDES the TS-1C rule that no routing cache may exist. TS-1C was deliberately uncached so that
    // RoutingVersion semantics could be proven first; TS-Storage Phase E2 then added the cache ADR-020
    // describes, and validity is keyed to RoutingVersion rather than to a TTL or an invalidation message.
    //
    // The rule is now an EXHAUSTIVE ALLOWLIST rather than a prohibition, because "no cache" no longer
    // describes the system and a guard that no longer describes the system stops being read. Anything
    // cache-shaped that is not one of these is a second cache, and a second cache is a second authority.
    string[] declaredCacheTypes =
    [
      "SSAS.Platform.Application.TenantStorage.ITenantRoutingCache",
      "SSAS.Platform.Application.TenantStorage.ITenantRoutingCacheInvalidator",
      "SSAS.Platform.Application.TenantStorage.TenantRoutingCacheEntry",
      "SSAS.Platform.Application.TenantStorage.TenantRoutingCacheOptions",
      "SSAS.Platform.Infrastructure.TenantStorage.TenantRoutingMemoryCache"
    ];

    var storageTypes = typeof(ITenantDatabaseResolver).Assembly.GetTypes()
      .Concat(InfrastructureAssembly.GetTypes())
      .Where(type => !type.IsNested)
      .Where(type =>
        type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true ||
        type.Name.Contains("TenantDatabase", StringComparison.Ordinal) ||
        type.Name.Contains("TenantStorage", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(storageTypes
      .Where(type => type.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase))
      .Select(type => type.FullName ?? type.Name)
      .Where(name => !declaredCacheTypes.Contains(name, StringComparer.Ordinal)));

    // ...and only the version-aware resolver may hold one. Every other consumer would be a path from a
    // remembered route to a live connection without an authoritative version comparison in between.
    Assert.Empty(storageTypes
      .Where(type => type.GetConstructors()
        .SelectMany(constructor => constructor.GetParameters())
        .Any(parameter => parameter.ParameterType.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase)))
      .Select(type => type.FullName ?? type.Name)
      .Where(name => !string.Equals(
        name, "SSAS.Platform.Application.TenantStorage.VersionAwareTenantDatabaseResolver",
        StringComparison.Ordinal)));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public void No_tenant_storage_http_surface_or_permission_is_introduced_yet()
  {
    // No administrative API exists, so no storage permission may exist either — a permission granting
    // access to nothing is worse than no permission at all.
    var apiDirectory = Path.Combine(RepositoryRoot(), "src", "Platform", "SSAS.Platform.API");
    Assert.False(Directory.Exists(Path.Combine(apiDirectory, "TenantStorage")));

    var permissionNames = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Application", "Permissions", "PlatformPermissionNames.cs"));
    Assert.DoesNotContain("TenantStorage", permissionNames, StringComparison.Ordinal);
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory!.FullName;
  }
}

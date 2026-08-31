using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Localization;

namespace SSAS.Integration.Tests;

public sealed class PlatformLocalizationSqlServerTests
{
  private const string PreviousMigration = "20260801135811_AddUserLogoutSessionRevocationReason";
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Migration_bootstraps_existing_tenants_and_supports_downgrade_reapply()
  {
    await using var database = LocalizationSqlDatabase.CreateUnmigrated();
    var tenantId = Guid.NewGuid();
    await using (var context = database.CreateContext(tenantId))
    {
      var migrator = context.Database.GetService<IMigrator>();
      await migrator.MigrateAsync(PreviousMigration);
      context.Tenants.Add(CreateTenant("BOOTSTRAP"));
      await context.SaveChangesAsync();
      tenantId = await context.Tenants.Select(tenant => tenant.TenantId).SingleAsync();

      await migrator.MigrateAsync();

      Assert.Empty(await context.Database.GetPendingMigrationsAsync());
      Assert.Equal(1, await ScalarIntAsync(context, "SELECT COUNT(*) FROM [platform].[LocalizationCatalogStates] WHERE [LocalizationCatalogStateId] = 1 AND [CatalogSchemaVersion] = 1 AND [HighestActivatedCatalogVersion] = 1"));
      Assert.Equal(1, await ScalarIntAsync(context, $"SELECT COUNT(*) FROM [platform].[TenantLocalizationSettings] WHERE [TenantId] = '{tenantId:D}' AND [TenantDefaultCulture] = 'en' AND [TenantLocalizationVersion] = 1"));
      Assert.Equal(4, await ScalarIntAsync(context, "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'platform') AND name IN (N'LocalizationCatalogStates', N'TenantLocalizationSettings', N'TenantLocalizationOverrides', N'TenantLocalizationOverrideVersions')"));
      Assert.Equal(4, await ScalarIntAsync(context, "SELECT COUNT(*) FROM sys.triggers WHERE name LIKE N'TR_%Localization%'"));

      await migrator.MigrateAsync(PreviousMigration);
      Assert.Equal(0, await ScalarIntAsync(context, "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'platform') AND name LIKE N'%Localization%'"));
      Assert.Equal(0, await ScalarIntAsync(context, "SELECT COUNT(*) FROM sys.triggers WHERE name LIKE N'TR_%Localization%'"));

      await migrator.MigrateAsync();
      Assert.Empty(await context.Database.GetPendingMigrationsAsync());
      Assert.Equal(1, await ScalarIntAsync(context, $"SELECT COUNT(*) FROM [platform].[TenantLocalizationSettings] WHERE [TenantId] = '{tenantId:D}' AND [TenantDefaultCulture] = 'en' AND [TenantLocalizationVersion] = 1"));
    }
  }

  [Fact]
  public async Task Missing_settings_read_is_non_mutating_and_first_mutation_self_heals()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateTenantAsync("SELFHEAL");
    await using var context = database.CreateContext(tenantId);
    var repository = new TenantLocalizationSettingsRepository(context);

    Assert.Null(await repository.GetForUpdateAsync(tenantId));
    Assert.Equal(0, await context.TenantLocalizationSettings.IgnoreQueryFilters().CountAsync());

    await using var transaction = await context.Database.BeginTransactionAsync();
    var settings = await repository.GetOrCreateForUpdateAsync(tenantId, LocalizationCulture.English);
    Assert.Equal(1, settings.TenantLocalizationVersion.Value);
    Assert.True(settings.IncrementVersion().IsSuccess);
    await context.SaveChangesAsync();
    await transaction.CommitAsync();

    Assert.Equal(2L, await context.TenantLocalizationSettings.IgnoreQueryFilters()
      .Where(candidate => candidate.TenantId == tenantId)
      .Select(candidate => candidate.TenantLocalizationVersion.Value)
      .SingleAsync());
  }

  [Fact]
  public async Task Concurrent_first_settings_creation_produces_one_retained_row()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateTenantAsync("CONCURRENT");

    async Task<long> InitializeAsync()
    {
      await using var context = database.CreateContext(tenantId);
      await using var transaction = await context.Database.BeginTransactionAsync();
      var repository = new TenantLocalizationSettingsRepository(context);
      var settings = await repository.GetOrCreateForUpdateAsync(tenantId, LocalizationCulture.English);
      await transaction.CommitAsync();
      return settings.TenantLocalizationVersion.Value;
    }

    var versions = await Task.WhenAll(InitializeAsync(), InitializeAsync());

    Assert.Equal([1L, 1L], versions);
    await using var verification = database.CreateContext(tenantId);
    Assert.Equal(1, await verification.TenantLocalizationSettings.IgnoreQueryFilters().CountAsync());
  }

  [Fact]
  public async Task Aggregate_and_history_enforce_coherence_uniqueness_fingerprints_and_immutability()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateTenantAsync("INVARIANTS");
    await using var context = database.CreateContext(tenantId);
    var definition = GetDefinition("platform.common.validation.required");
    var value = LocalizationText.Create("Enter {fieldName}.", definition.TextFormat).Value;
    var aggregate = TenantLocalizationOverride.Create(
      tenantId,
      LocalizationCulture.English,
      definition,
      value,
      GeneratedLocalizationCatalog.Instance.CatalogVersion,
      "integration-actor",
      Guid.NewGuid(),
      Now,
      TenantLocalizationVersion.Create(2).Value).Value;

    context.TenantLocalizationOverrides.Add(aggregate);
    await context.SaveChangesAsync();

    Assert.NotEmpty(aggregate.RowVersion);
    Assert.Equal(1, await context.TenantLocalizationOverrideVersions.CountAsync());
    Assert.Equal(32, await context.TenantLocalizationOverrides.Select(candidate => candidate.PlaceholderFingerprint.Bytes.Length).SingleAsync());
    Assert.Equal("bigint", await ScalarStringAsync(context, "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'TenantLocalizationOverrides' AND COLUMN_NAME = 'CurrentVersionNumber'"));
    Assert.Equal("binary", await ScalarStringAsync(context, "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'TenantLocalizationOverrideVersions' AND COLUMN_NAME = 'CompatibilityFingerprint'"));
    Assert.Equal(200, await ScalarIntAsync(context, "SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'TenantLocalizationOverrides' AND COLUMN_NAME = 'ResourceKey'"));

    var invalidRestoredDefault = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      """
      INSERT INTO [platform].[TenantLocalizationOverrideVersions]
        ([TenantLocalizationOverrideVersionId], [TenantLocalizationOverrideId], [TenantId], [ResourceKey], [Culture],
         [VersionNumber], [TextFormat], [PlainTextValue], [MultilineTextValue], [IsActive], [ChangeType],
         [PriorLogicalVersionNumber], [UndoTargetVersionNumber], [CatalogVersion], [ResourceVersion],
         [PlaceholderFingerprint], [CompatibilityFingerprint], [ActorId], [OccurredUtc])
      SELECT {0}, [TenantLocalizationOverrideId], [TenantId], [ResourceKey], [Culture],
         2, [TextFormat], [CurrentPlainTextValue], [CurrentMultilineTextValue], 1, 'RestoredDefault',
         1, NULL, [CatalogVersion], [ResourceVersion], [PlaceholderFingerprint], [CompatibilityFingerprint],
         N'constraint-test', SYSUTCDATETIME()
      FROM [platform].[TenantLocalizationOverrides]
      WHERE [TenantLocalizationOverrideId] = {1}
      """,
      Guid.NewGuid(),
      aggregate.Id));
    Assert.Equal(547, invalidRestoredDefault.Number);

    var duplicate = TenantLocalizationOverride.Create(
      tenantId,
      LocalizationCulture.English,
      definition,
      value,
      GeneratedLocalizationCatalog.Instance.CatalogVersion,
      "integration-actor",
      Guid.NewGuid(),
      Now,
      TenantLocalizationVersion.Create(3).Value).Value;
    context.TenantLocalizationOverrides.Add(duplicate);
    await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    context.ChangeTracker.Clear();

    var updateFailure = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [platform].[TenantLocalizationOverrideVersions] SET [ActorId] = N'changed' WHERE [TenantLocalizationOverrideId] = {0}",
      aggregate.Id));
    Assert.Equal(51000, updateFailure.Number);
    var historyDeleteFailure = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "DELETE FROM [platform].[TenantLocalizationOverrideVersions] WHERE [TenantLocalizationOverrideId] = {0}",
      aggregate.Id));
    Assert.Equal(51000, historyDeleteFailure.Number);
    var aggregateDeleteFailure = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "DELETE FROM [platform].[TenantLocalizationOverrides] WHERE [TenantLocalizationOverrideId] = {0}",
      aggregate.Id));
    Assert.Equal(51000, aggregateDeleteFailure.Number);
  }

  [Fact]
  public async Task Application_mutations_use_trusted_context_and_preserve_lineage_and_no_op_behavior()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateActiveTenantAsync("MUTATIONS");
    await using var context = database.CreateContext(tenantId);
    var dispatcher = new RecordingDomainEventDispatcher();
    var settingsRepository = new TenantLocalizationSettingsRepository(context);
    var overrideRepository = new TenantLocalizationOverrideRepository(context);
    var eligibility = new TenantAuthenticationEligibilityReadService(context);
    var unitOfWork = TestUnitOfWork.Platform(context, dispatcher);
    var currentTenant = new TestCurrentTenant(tenantId);
    var currentUser = new TestCurrentUser();
    var clock = new TestClock();
    var catalog = GeneratedLocalizationCatalog.Instance;

    var create = new CreateTenantLocalizationOverrideCommandHandler(
      settingsRepository, overrideRepository, eligibility, ReadyAuditReadiness.Instance,
      unitOfWork, catalog, currentTenant, currentUser, clock);
    var created = await create.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      "Please enter {fieldName}."));
    Assert.True(created.IsSuccess);
    Assert.Equal(1, created.Value.CurrentVersionNumber);
    Assert.Equal(2, created.Value.TenantLocalizationVersion);
    Assert.Single(dispatcher.Events);

    var duplicate = await create.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      "Please enter {fieldName}."));
    Assert.Equal(SSAS.Platform.Domain.Localization.LocalizationErrors.OverrideAlreadyExists, duplicate.Error);

    var update = new UpdateTenantLocalizationOverrideCommandHandler(
      settingsRepository, overrideRepository, eligibility, ReadyAuditReadiness.Instance,
      unitOfWork, catalog, currentTenant, currentUser, clock);
    var stale = await update.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      "A value for {fieldName} is required.",
      [0]));
    Assert.Equal(SSAS.Platform.Domain.IdentityAccessErrors.ConcurrencyConflict, stale.Error);
    var updated = await update.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      "A value for {fieldName} is required.",
      created.Value.RowVersion));
    Assert.True(updated.IsSuccess);
    Assert.Equal(2, updated.Value.CurrentVersionNumber);
    Assert.Equal(3, updated.Value.TenantLocalizationVersion);

    var restore = new RestoreTenantLocalizationDefaultCommandHandler(
      settingsRepository, overrideRepository, eligibility, ReadyAuditReadiness.Instance,
      unitOfWork, catalog, currentTenant, currentUser, clock);
    var restored = await restore.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      updated.Value.RowVersion));
    Assert.True(restored.IsSuccess);
    Assert.Equal(3, restored.Value.CurrentVersionNumber);
    Assert.Equal(4, restored.Value.TenantLocalizationVersion);

    var alreadyDefault = await restore.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      restored.Value.RowVersion));
    Assert.Equal(SSAS.Platform.Domain.Localization.LocalizationErrors.OverrideAlreadyDefault, alreadyDefault.Error);
    Assert.Equal(4L, await context.TenantLocalizationSettings.Select(settings => settings.TenantLocalizationVersion.Value).SingleAsync());

    var undo = new UndoTenantLocalizationOverrideCommandHandler(
      settingsRepository, overrideRepository, eligibility, ReadyAuditReadiness.Instance,
      unitOfWork, catalog, currentTenant, currentUser, clock);
    var undone = await undo.HandleAsync(new(
      "platform.common.validation.required",
      "en",
      2,
      restored.Value.RowVersion));
    Assert.True(undone.IsSuccess);
    Assert.Equal(4, undone.Value.CurrentVersionNumber);
    Assert.Equal(5, undone.Value.TenantLocalizationVersion);

    var history = new GetTenantLocalizationHistoryQueryHandler(
      new TenantLocalizationHistoryReadService(context), new RequestTenantEligibility(eligibility), currentTenant);
    var historyResult = await history.HandleAsync(new("platform.common.validation.required", "en"));
    Assert.True(historyResult.IsSuccess);
    Assert.Equal([4L, 3L, 2L, 1L], historyResult.Value.Entries.Select(entry => entry.VersionNumber));
    Assert.Equal(1, historyResult.Value.EligibleUndoTargetVersion);
    Assert.Equal(4, dispatcher.Events.Count);

    var administration = await new TenantLocalizationAdministrationReadService(context).ReadAsync(
      tenantId,
      LocalizationCulture.English,
      [ResourceKey.Create("platform.common.validation.required").Value]);
    Assert.Single(administration);
    Assert.Equal(4, administration[0].CurrentVersionNumber);
    Assert.Equal(1, administration[0].EligibleUndoTargetVersion);

    using var cache = new LocalizationMemoryCache(clock);
    var overrideReadService = new TenantLocalizationOverrideReadService(context);
    var loadedOverrides = await overrideReadService.ReadAsync(
      tenantId,
      LocalizationCulture.English,
      [ResourceKey.Create("platform.common.validation.required").Value]);
    Assert.Single(loadedOverrides);
    var resolver = new LocalizationTextResolver(
      catalog,
      overrideReadService,
      new TenantLocalizationVersionReader(context),
      cache,
      new RequestTenantEligibility(eligibility),
      new RecordingLocalizationDiagnostics(),
      currentTenant);
    var effective = await resolver.ResolveAsync(new(
      "platform.common.validation.required",
      "en",
      new Dictionary<string, string>(StringComparer.Ordinal) { ["fieldName"] = "Email" }));
    Assert.True(effective.IsSuccess);
    Assert.Equal(LocalizationResolutionSource.TenantOverride, effective.Value.ResolutionSource);
    Assert.Equal("A value for Email is required.", effective.Value.Text);
  }

  [Fact]
  public async Task Concurrent_application_create_has_one_deterministic_loser()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateActiveTenantAsync("CREATE_RACE");

    async Task<Result<LocalizationMutationResult>> CreateAsync()
    {
      await using var context = database.CreateContext(tenantId);
      var handler = new CreateTenantLocalizationOverrideCommandHandler(
        new TenantLocalizationSettingsRepository(context),
        new TenantLocalizationOverrideRepository(context),
        new TenantAuthenticationEligibilityReadService(context),
        ReadyAuditReadiness.Instance,
        TestUnitOfWork.Platform(context, new RecordingDomainEventDispatcher()),
        GeneratedLocalizationCatalog.Instance,
        new TestCurrentTenant(tenantId),
        new TestCurrentUser(),
        new TestClock());
      return await handler.HandleAsync(new("platform.common.actions.save", "en", "Store"));
    }

    var results = await Task.WhenAll(CreateAsync(), CreateAsync());

    Assert.Single(results, result => result.IsSuccess);
    Assert.Single(results, result => result.Error == SSAS.Platform.Domain.Localization.LocalizationErrors.OverrideAlreadyExists);
    await using var verification = database.CreateContext(tenantId);
    Assert.Equal(1, await verification.TenantLocalizationOverrides.CountAsync());
    Assert.Equal(1, await verification.TenantLocalizationOverrideVersions.CountAsync());
    Assert.Equal(2L, await verification.TenantLocalizationSettings
      .Select(settings => settings.TenantLocalizationVersion.Value)
      .SingleAsync());
  }

  [Fact]
  public async Task Catalog_activation_enforces_equal_higher_and_lower_environment_policy()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    await using var context = database.CreateContext(null);
    var equal = await new LocalizationCatalogActivationService(context, CreateCatalog(1)).ActivateAsync(true);
    Assert.Equal(LocalizationCatalogActivationOutcome.Equal, equal.Outcome);

    var higher = await new LocalizationCatalogActivationService(context, CreateCatalog(2)).ActivateAsync(true);
    Assert.Equal(LocalizationCatalogActivationOutcome.Activated, higher.Outcome);
    Assert.Equal(2L, await context.LocalizationCatalogStates.AsNoTracking()
      .Select(state => state.HighestActivatedCatalogVersion.Value)
      .SingleAsync());

    await Assert.ThrowsAsync<LocalizationCatalogActivationException>(() =>
      new LocalizationCatalogActivationService(context, CreateCatalog(1)).ActivateAsync(true));
    var development = await new LocalizationCatalogActivationService(context, CreateCatalog(1)).ActivateAsync(false);
    Assert.Equal(LocalizationCatalogActivationOutcome.DevelopmentLowerVersionWarning, development.Outcome);
    Assert.Equal(2L, await context.LocalizationCatalogStates.AsNoTracking()
      .Select(state => state.HighestActivatedCatalogVersion.Value)
      .SingleAsync());
  }

  [Fact]
  public async Task Concurrent_catalog_activation_serializes_and_never_lowers_state()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();

    async Task<LocalizationCatalogActivationResult> ActivateAsync()
    {
      await using var context = database.CreateContext(null);
      return await new LocalizationCatalogActivationService(context, CreateCatalog(2)).ActivateAsync(true);
    }

    var results = await Task.WhenAll(ActivateAsync(), ActivateAsync());

    Assert.Single(results, result => result.Outcome == LocalizationCatalogActivationOutcome.Activated);
    Assert.Single(results, result => result.Outcome == LocalizationCatalogActivationOutcome.Equal);
    await using var verification = database.CreateContext(null);
    Assert.Equal(2L, await verification.LocalizationCatalogStates.AsNoTracking()
      .Select(state => state.HighestActivatedCatalogVersion.Value)
      .SingleAsync());
  }

  private static LocalizationCatalog CreateCatalog(long version) => new(
    GeneratedLocalizationCatalog.Instance.CatalogSchemaVersion,
    CatalogVersion.Create(version).Value,
    GeneratedLocalizationCatalog.Instance.Resources,
    GeneratedLocalizationCatalog.Instance.GetNeutralFallback(LocalizationCulture.English),
    GeneratedLocalizationCatalog.Instance.GetNeutralFallback(LocalizationCulture.Arabic));

  private static SSAS.BuildingBlocks.Localization.Catalog.LocalizationResourceDefinition GetDefinition(string key)
  {
    Assert.True(GeneratedLocalizationCatalog.Instance.TryGet(ResourceKey.Create(key).Value, out var definition));
    return definition;
  }

  private static Tenant CreateTenant(string code) => Tenant.Create(
    TenantCode.Create(code).Value,
    TenantName.Create($"{code} Tenant").Value,
    "integration-actor",
    Guid.NewGuid(),
    Now).Value;

  private static async Task<int> ScalarIntAsync(PlatformDbContext context, string commandText) =>
    Convert.ToInt32(await ScalarAsync(context, commandText), System.Globalization.CultureInfo.InvariantCulture);

  private static async Task<string> ScalarStringAsync(PlatformDbContext context, string commandText) =>
    Convert.ToString(await ScalarAsync(context, commandText), System.Globalization.CultureInfo.InvariantCulture)!;

  private static async Task<object?> ScalarAsync(PlatformDbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return await command.ExecuteScalarAsync();
  }

  private sealed class LocalizationSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static LocalizationSqlDatabase CreateUnmigrated()
    {
      var configured = IntegrationSqlEnvironment.BaseConnectionString;
      var builder = new SqlConnectionStringBuilder(configured)
      {
        InitialCatalog = $"SSAS_ERP_FP004_{Guid.NewGuid():N}"
      };
      return new LocalizationSqlDatabase(builder.ConnectionString);
    }

    public static async Task<LocalizationSqlDatabase> CreateAsync()
    {
      var database = CreateUnmigrated();
      try
      {
        await using var context = database.CreateContext(null);
        await context.Database.MigrateAsync();
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext(Guid? tenantId)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(tenantId), new TestClock());
    }

    public async Task<Guid> CreateTenantAsync(string code)
    {
      await using var context = CreateContext(null);
      var tenant = CreateTenant(code);
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      return tenant.TenantId;
    }

    public async Task<Guid> CreateActiveTenantAsync(string code)
    {
      await using var context = CreateContext(null);
      var tenant = CreateTenant(code);
      Assert.True(tenant.Activate("integration-actor", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      return tenant.TenantId;
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext(null);
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed class TestCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-actor";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }

  private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
  {
    public List<DomainEvent> Events { get; } = [];

    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
      Events.AddRange(domainEvents);
      return Task.CompletedTask;
    }
  }

  private sealed class RecordingLocalizationDiagnostics : ILocalizationDiagnostics
  {
    public void RecordMissingResource(string resourceKey)
    {
    }

    public void RecordDegradedTenant(Guid tenantId)
    {
    }
  }

  [Theory]
  [InlineData("create")]
  [InlineData("update")]
  [InlineData("undo")]
  [InlineData("restore")]
  public async Task Audit_unavailable_leaves_all_localization_sql_state_and_events_unchanged(string operation)
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateActiveTenantAsync($"AUDIT_{operation.ToUpperInvariant()}");
    await using var context = database.CreateContext(tenantId);
    var dispatcher = new RecordingDomainEventDispatcher();
    var settings = new TenantLocalizationSettingsRepository(context);
    var overrides = new TenantLocalizationOverrideRepository(context);
    var eligibility = new TenantAuthenticationEligibilityReadService(context);
    var unitOfWork = TestUnitOfWork.Platform(context, dispatcher);
    var currentTenant = new TestCurrentTenant(tenantId);
    var currentUser = new TestCurrentUser();
    var clock = new TestClock();
    var resourceKey = operation == "create" ? "platform.common.actions.cancel" : "platform.common.actions.save";
    LocalizationMutationResult? baseline = null;

    if (operation != "create")
    {
      var create = new CreateTenantLocalizationOverrideCommandHandler(
        settings, overrides, eligibility, ReadyAuditReadiness.Instance, unitOfWork,
        GeneratedLocalizationCatalog.Instance, currentTenant, currentUser, clock);
      var created = await create.HandleAsync(new(resourceKey, "en", "Store"));
      Assert.True(created.IsSuccess);
      baseline = created.Value;
      if (operation == "undo")
      {
        var update = new UpdateTenantLocalizationOverrideCommandHandler(
          settings, overrides, eligibility, ReadyAuditReadiness.Instance, unitOfWork,
          GeneratedLocalizationCatalog.Instance, currentTenant, currentUser, clock);
        var updated = await update.HandleAsync(new(resourceKey, "en", "Keep", baseline.RowVersion));
        Assert.True(updated.IsSuccess);
        baseline = updated.Value;
      }
    }

    context.ChangeTracker.Clear();
    var beforeSettingsVersion = await context.TenantLocalizationSettings
      .Where(item => item.TenantId == tenantId)
      .Select(item => (long?)item.TenantLocalizationVersion.Value)
      .SingleOrDefaultAsync();
    var beforeOverrideCount = await context.TenantLocalizationOverrides.CountAsync();
    var beforeHistoryCount = await context.TenantLocalizationOverrideVersions.CountAsync();
    var beforeEventCount = dispatcher.Events.Count;

    Result<LocalizationMutationResult> result = operation switch
    {
      "create" => await new CreateTenantLocalizationOverrideCommandHandler(
        settings, overrides, eligibility, UnavailableAuditReadiness.Instance, unitOfWork,
        GeneratedLocalizationCatalog.Instance, currentTenant, currentUser, clock)
        .HandleAsync(new(resourceKey, "en", "Cancel now")),
      "update" => await new UpdateTenantLocalizationOverrideCommandHandler(
        settings, overrides, eligibility, UnavailableAuditReadiness.Instance, unitOfWork,
        GeneratedLocalizationCatalog.Instance, currentTenant, currentUser, clock)
        .HandleAsync(new(resourceKey, "en", "Changed", baseline!.RowVersion)),
      "undo" => await new UndoTenantLocalizationOverrideCommandHandler(
        settings, overrides, eligibility, UnavailableAuditReadiness.Instance, unitOfWork,
        GeneratedLocalizationCatalog.Instance, currentTenant, currentUser, clock)
        .HandleAsync(new(resourceKey, "en", 1, baseline!.RowVersion)),
      "restore" => await new RestoreTenantLocalizationDefaultCommandHandler(
        settings, overrides, eligibility, UnavailableAuditReadiness.Instance, unitOfWork,
        GeneratedLocalizationCatalog.Instance, currentTenant, currentUser, clock)
        .HandleAsync(new(resourceKey, "en", baseline!.RowVersion)),
      _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    Assert.Equal(LocalizationManagementErrors.AuditReadinessUnavailable, result.Error);
    context.ChangeTracker.Clear();
    Assert.Equal(beforeSettingsVersion, await context.TenantLocalizationSettings
      .Where(item => item.TenantId == tenantId)
      .Select(item => (long?)item.TenantLocalizationVersion.Value)
      .SingleOrDefaultAsync());
    Assert.Equal(beforeOverrideCount, await context.TenantLocalizationOverrides.CountAsync());
    Assert.Equal(beforeHistoryCount, await context.TenantLocalizationOverrideVersions.CountAsync());
    Assert.Equal(beforeEventCount, dispatcher.Events.Count);
  }

  [Fact]
  public async Task Audit_ready_does_not_bypass_suspended_tenant_locked_eligibility()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var tenantId = await database.CreateActiveTenantAsync("AUDIT_SUSPENDED");
    await using var context = database.CreateContext(tenantId);
    var tenant = await context.Tenants.SingleAsync(item => item.Id == tenantId);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "integration-actor", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    await context.SaveChangesAsync();
    context.ChangeTracker.Clear();

    var handler = new CreateTenantLocalizationOverrideCommandHandler(
      new TenantLocalizationSettingsRepository(context),
      new TenantLocalizationOverrideRepository(context),
      new TenantAuthenticationEligibilityReadService(context),
      ReadyAuditReadiness.Instance,
      TestUnitOfWork.Platform(context, new RecordingDomainEventDispatcher()),
      GeneratedLocalizationCatalog.Instance,
      new TestCurrentTenant(tenantId),
      new TestCurrentUser(),
      new TestClock());

    var result = await handler.HandleAsync(new("platform.common.actions.save", "en", "Store"));

    Assert.Equal(SSAS.Platform.Domain.Localization.LocalizationErrors.TenantIneligible, result.Error);
    Assert.Empty(await context.TenantLocalizationOverrides.ToArrayAsync());
    Assert.Empty(await context.TenantLocalizationOverrideVersions.ToArrayAsync());
    Assert.Empty(await context.TenantLocalizationSettings.Where(item => item.TenantId == tenantId).ToArrayAsync());
  }

  [Fact]
  public async Task Effective_explicit_batch_is_tenant_isolated_ignores_sensitive_overrides_and_reflects_tenant_version()
  {
    await using var database = await LocalizationSqlDatabase.CreateAsync();
    var firstTenantId = await database.CreateActiveTenantAsync("EFFECTIVE_A");
    var secondTenantId = await database.CreateActiveTenantAsync("EFFECTIVE_B");
    var keys = new[] { "platform.common.actions.save", "platform.authentication.errors.authentication_failed" };

    await using (var firstContext = database.CreateContext(firstTenantId))
    {
      var create = new CreateTenantLocalizationOverrideCommandHandler(
        new TenantLocalizationSettingsRepository(firstContext),
        new TenantLocalizationOverrideRepository(firstContext),
        new TenantAuthenticationEligibilityReadService(firstContext),
        ReadyAuditReadiness.Instance,
        TestUnitOfWork.Platform(firstContext, new RecordingDomainEventDispatcher()),
        GeneratedLocalizationCatalog.Instance,
        new TestCurrentTenant(firstTenantId),
        new TestCurrentUser(),
        new TestClock());
      var created = await create.HandleAsync(new("platform.common.actions.save", "en", "Tenant A Save"));
      Assert.True(created.IsSuccess);
      Assert.Equal(2, created.Value.TenantLocalizationVersion);

      using var cache = new LocalizationMemoryCache(new TestClock());
      var resolver = new LocalizationTextResolver(
        GeneratedLocalizationCatalog.Instance,
        new TenantLocalizationOverrideReadService(firstContext),
        new TenantLocalizationVersionReader(firstContext),
        cache,
        new RequestTenantEligibility(new TenantAuthenticationEligibilityReadService(firstContext)),
        new RecordingLocalizationDiagnostics(),
        new TestCurrentTenant(firstTenantId));
      var result = await resolver.ResolveExplicitBatchAsync(new(keys, "en"));

      Assert.True(result.IsSuccess);
      Assert.Equal(["platform.authentication.errors.authentication_failed", "platform.common.actions.save"], result.Value.Select(item => item.ResourceKey.Value));
      Assert.Equal(LocalizationResolutionSource.SystemDefault, result.Value[0].ResolutionSource);
      Assert.Equal(LocalizationResolutionSource.TenantOverride, result.Value[1].ResolutionSource);
      Assert.Equal("Tenant A Save", result.Value[1].Text);
      Assert.Equal(2, result.Value[1].TenantLocalizationVersion!.Value.Value);
    }

    await using (var secondContext = database.CreateContext(secondTenantId))
    {
      using var cache = new LocalizationMemoryCache(new TestClock());
      var resolver = new LocalizationTextResolver(
        GeneratedLocalizationCatalog.Instance,
        new TenantLocalizationOverrideReadService(secondContext),
        new TenantLocalizationVersionReader(secondContext),
        cache,
        new RequestTenantEligibility(new TenantAuthenticationEligibilityReadService(secondContext)),
        new RecordingLocalizationDiagnostics(),
        new TestCurrentTenant(secondTenantId));
      var result = await resolver.ResolveExplicitBatchAsync(new(keys, "en"));

      Assert.True(result.IsSuccess);
      Assert.All(result.Value, item => Assert.NotEqual(LocalizationResolutionSource.TenantOverride, item.ResolutionSource));
      Assert.DoesNotContain(result.Value, item => item.Text == "Tenant A Save");
    }
  }

  private sealed class ReadyAuditReadiness : ILocalizationManagementAuditReadiness
  {
    public static ReadyAuditReadiness Instance { get; } = new();

    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(
      CancellationToken cancellationToken = default) =>
      Task.FromResult(LocalizationManagementAuditReadinessResult.Ready);
  }

  private sealed class UnavailableAuditReadiness : ILocalizationManagementAuditReadiness
  {
    public static UnavailableAuditReadiness Instance { get; } = new();

    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(
      CancellationToken cancellationToken = default) =>
      Task.FromResult(LocalizationManagementAuditReadinessResult.Unavailable);
  }
}

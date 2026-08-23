using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

public sealed class TenantCompanyOrganizationSqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-CMP-0001")]
  [Trait("Decision", "DEC-CMP-0004")]
  public async Task Company_migration_enforces_schema_uniqueness_and_cross_tenant_isolation()
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantA = await SeedTenantAsync(database, "TENANTA");
    var tenantB = await SeedTenantAsync(database, "TENANTB");

    await using var context = database.CreateTenantContext(tenantA);
    Assert.Empty(await context.Database.GetPendingMigrationsAsync());

    var entity = context.Model.FindEntityType(typeof(Company));
    Assert.NotNull(entity);
    Assert.NotNull(entity.GetQueryFilter());
    Assert.Null(entity.FindProperty("CompanyId"));
    Assert.Equal("CompanyId", entity.FindProperty(nameof(Company.Id))?.GetColumnName());
    Assert.True(entity.FindProperty(nameof(Company.RowVersion))?.IsConcurrencyToken);
    Assert.Equal("rowversion", entity.FindProperty(nameof(Company.RowVersion))?.GetColumnType());

    Assert.Equal(
      [
        "CompanyId", "TenantId", "CompanyCode", "NormalizedCompanyCode", "CompanyName", "BaseCurrencyCode",
        "Status", "StatusChangeReasonCode", "StatusChangedUtc", "StatusChangedBy", "CreatedUtc", "ModifiedUtc",
        "CreatedBy", "ModifiedBy", "RowVersion"
      ],
      await ReadStringsAsync(
        context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'tenant' AND TABLE_NAME = 'Companies' ORDER BY ORDINAL_POSITION"));
    Assert.Equal("uniqueidentifier", await ReadStringAsync(
      context,
      "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'tenant' AND TABLE_NAME = 'Companies' AND COLUMN_NAME = 'CompanyId'"));
    Assert.Equal("char", await ReadStringAsync(
      context,
      "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'tenant' AND TABLE_NAME = 'Companies' AND COLUMN_NAME = 'BaseCurrencyCode'"));
    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(
      context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'tenant' AND TABLE_NAME = 'Companies' AND COLUMN_NAME = 'NormalizedCompanyCode'"));
    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(
      context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'tenant' AND TABLE_NAME = 'Companies' AND COLUMN_NAME = 'BaseCurrencyCode'"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[tenant].[Companies]') AND name = N'UX_Companies_TenantId_NormalizedCompanyCode' AND is_unique = 1"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[tenant].[Companies]') AND name = N'IX_Companies_TenantId_Status_CompanyName_CompanyId'"));
    // NO foreign key to Tenant, by design (ADR-017 "Cross-database foreign keys"). Tenant stays in the
    // Platform database; once the two are separate catalogs this constraint could not exist, so it is gone
    // in the shared topology too rather than working until the first dedicated tenant and then failing.
    // TenantId is still enforced — by the global query filter, the write-side tenant guard, and validation
    // at creation — which the surrounding assertions cover.
    Assert.Equal(0, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[tenant].[Companies]')"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[tenant].[TR_Companies_PreventDelete]')"));

    context.Companies.AddRange(
      CreateCompany(tenantA, "  acme  ", "Acme"),
      CreateCompany(tenantA, "BETA", "Beta"));
    var inserted = await SaveAsync(context);
    Assert.True(inserted.IsSuccess);
    Assert.Equal(2, await context.Companies.CountAsync());
    Assert.NotEmpty((await context.Companies.FirstAsync()).RowVersion);

    context.Companies.Add(CreateCompany(tenantA, "acme", "Acme Duplicate"));
    var duplicate = await SaveAsync(context);
    Assert.Equal("Persistence.UniqueConstraint", duplicate.Error.Code);
    context.ChangeTracker.Clear();

    await using var contextB = database.CreateTenantContext(tenantB);
    contextB.Companies.Add(CreateCompany(tenantB, "ACME", "Acme In Other Tenant"));
    Assert.True((await SaveAsync(contextB)).IsSuccess);

    Assert.Equal(2, await context.Companies.CountAsync());
    Assert.Equal(1, await contextB.Companies.CountAsync());
    Assert.Equal(3, await context.Companies.IgnoreQueryFilters().CountAsync());
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0003")]
  [Trait("Security", "SEC-CMP-0205")]
  public async Task Company_rows_cannot_be_physically_deleted()
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantA = await SeedTenantAsync(database, "TENANTA");
    await using var context = database.CreateTenantContext(tenantA);
    var company = CreateCompany(tenantA, "ONE", "Company One");
    context.Companies.Add(company);
    Assert.True((await SaveAsync(context)).IsSuccess);

    var persisted = await context.Companies.SingleAsync(item => item.Id == company.CompanyId);
    context.Companies.Remove(persisted);
    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    context.Entry(persisted).State = EntityState.Unchanged;

    var single = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "DELETE FROM [tenant].[Companies] WHERE [CompanyId] = {0}",
      company.CompanyId));
    Assert.Equal(51000, single.Number);

    var batch = await Assert.ThrowsAsync<SqlException>(() =>
      context.Database.ExecuteSqlRawAsync("DELETE FROM [tenant].[Companies]"));
    Assert.Equal(51000, batch.Number);

    Assert.Equal(1, await context.Companies.CountAsync());
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0004")]
  public async Task Company_check_constraints_reject_invalid_raw_writes()
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantA = await SeedTenantAsync(database, "TENANTA");
    await using var context = database.CreateTenantContext(tenantA);
    var company = CreateCompany(tenantA, "CHECK", "Check Company");
    context.Companies.Add(company);
    Assert.True((await SaveAsync(context)).IsSuccess);

    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [tenant].[Companies] SET [Status] = N'Deleted' WHERE [CompanyId] = {0}",
      company.CompanyId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [tenant].[Companies] SET [StatusChangeReasonCode] = N'FreeForm' WHERE [CompanyId] = {0}",
      company.CompanyId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [tenant].[Companies] SET [CompanyCode] = N'   ' WHERE [CompanyId] = {0}",
      company.CompanyId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [tenant].[Companies] SET [CompanyName] = N'   ' WHERE [CompanyId] = {0}",
      company.CompanyId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [tenant].[Companies] SET [BaseCurrencyCode] = 'usd' WHERE [CompanyId] = {0}",
      company.CompanyId));
  }

  [Fact]
  [Trait("Acceptance", "AC-CMP-0014")]
  public async Task Company_rowversion_rejects_stale_lifecycle_write()
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantA = await SeedTenantAsync(database, "TENANTA");
    Guid companyId;
    await using (var setup = database.CreateTenantContext(tenantA))
    {
      var company = CreateCompany(tenantA, "CONCURRENT", "Concurrent Company");
      setup.Companies.Add(company);
      Assert.True((await SaveAsync(setup)).IsSuccess);
      companyId = company.CompanyId;
    }

    await using var firstContext = database.CreateTenantContext(tenantA);
    await using var staleContext = database.CreateTenantContext(tenantA);
    var first = await firstContext.Companies.SingleAsync(item => item.Id == companyId);
    var stale = await staleContext.Companies.SingleAsync(item => item.Id == companyId);
    Assert.True(first.Activate(CompanyStatusChangeReason.Administrative, "actor-1", Guid.NewGuid(), CompanySqlDatabase.Now.AddMinutes(1)).IsSuccess);
    Assert.True((await SaveAsync(firstContext)).IsSuccess);
    Assert.True(stale.UpdateProfile(CompanyName.Create("Renamed").Value, "actor-2", Guid.NewGuid(), CompanySqlDatabase.Now.AddMinutes(1)).IsSuccess);

    var staleResult = await SaveAsync(staleContext);
    Assert.Equal("Persistence.ConcurrencyConflict", staleResult.Error.Code);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0003")]
  public async Task Tenant_migration_rolls_back_and_reapplies_with_its_trigger()
  {
    await using var database = CompanySqlDatabase.CreateUnmigrated();
    await using (var platform = database.CreateContext())
    {
      await platform.Database.MigrateAsync();
    }

    await using var context = database.CreateTenantContext();
    var migrator = context.Database.GetService<IMigrator>();
    await migrator.MigrateAsync();
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[tenant].[Companies]')"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[tenant].[TR_Companies_PreventDelete]')"));

    // Target 0 rather than a named predecessor: this is the FIRST migration of the tenant stream, which is
    // itself the point — the tenant stream has its own baseline and does not continue the platform one.
    await migrator.MigrateAsync(Migration.InitialDatabase);
    Assert.Equal(0, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[tenant].[Companies]')"));
    Assert.Equal(0, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[tenant].[TR_Companies_PreventDelete]')"));

    // The platform schema is untouched by rolling the tenant stream back — the streams are independent.
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[platform].[Tenants]')"));

    await migrator.MigrateAsync();
    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[tenant].[TR_Companies_PreventDelete]')"));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task Tenant_and_platform_migration_histories_are_separate()
  {
    // ADR-018: the two streams must never be mistakable for one another. Distinct history tables in
    // distinct schemas is what makes "which migrations does this database have" answerable per stream.
    await using var database = await CompanySqlDatabase.CreateAsync();
    await using var tenant = database.CreateTenantContext();

    Assert.Equal(1, await ReadInt32Async(
      tenant, "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[tenant].[__EFMigrationsHistory]')"));
    Assert.Equal(1, await ReadInt32Async(
      tenant, "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[platform].[__EFMigrationsHistory]')"));

    var tenantApplied = await ReadStringsAsync(
      tenant, "SELECT [MigrationId] FROM [tenant].[__EFMigrationsHistory] ORDER BY [MigrationId]");
    var platformApplied = await ReadStringsAsync(
      tenant, "SELECT [MigrationId] FROM [platform].[__EFMigrationsHistory] ORDER BY [MigrationId]");

    // No migration id appears in both, and neither stream can be inferred from the other.
    Assert.NotEmpty(tenantApplied);
    Assert.NotEmpty(platformApplied);
    Assert.Empty(tenantApplied.Intersect(platformApplied, StringComparer.Ordinal));
    Assert.Contains(tenantApplied, id => id.EndsWith("AddTenantCompanyOrganization", StringComparison.Ordinal));
    Assert.DoesNotContain(platformApplied, id => id.EndsWith("AddTenantCompanyOrganization", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Existing_platform_company_rows_are_preserved_by_the_move()
  {
    // The data move is the risky part of this slice, so it is proven against real SQL rather than reasoned
    // about: seed rows into the retired platform table, run the tenant migration, and assert every column
    // survived. RowVersion is regenerated by the target database and is therefore excluded.
    await using var database = CompanySqlDatabase.CreateUnmigrated();
    Guid tenantId;
    var companyId = Guid.NewGuid();

    await using (var platform = database.CreateContext())
    {
      await platform.Database.MigrateAsync();
      var tenant = Tenant.Create(
        TenantCode.Create("MOVED").Value, TenantName.Create("Moved Tenant").Value,
        "integration-actor", Guid.NewGuid(), CompanySqlDatabase.Now).Value;
      platform.Tenants.Add(tenant);
      Assert.True((await SaveAsync(platform)).IsSuccess);
      tenantId = tenant.TenantId;

      // Written directly, because PlatformDbContext no longer maps Company at all — which is the point.
      await platform.Database.ExecuteSqlRawAsync(
        """
        INSERT INTO [platform].[Companies_MigratedToTenant]
          ([CompanyId], [TenantId], [CompanyCode], [NormalizedCompanyCode], [CompanyName], [BaseCurrencyCode],
           [Status], [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [ModifiedUtc],
           [CreatedBy], [ModifiedBy])
        VALUES ({0}, {1}, N'LEGACY', N'LEGACY', N'Legacy Company', 'EUR', N'Active', N'Administrative',
                {2}, N'status-actor', {3}, {4}, N'creator', N'modifier');
        """,
        companyId, tenantId, CompanySqlDatabase.Now, CompanySqlDatabase.Now, CompanySqlDatabase.Now);
    }

    await using var tenantContext = database.CreateTenantContext(tenantId);
    await tenantContext.Database.MigrateAsync();

    var moved = await tenantContext.Companies.SingleAsync(company => company.Id == companyId);
    Assert.Equal(tenantId, moved.TenantId);
    Assert.Equal("LEGACY", moved.CompanyCode.Value);
    Assert.Equal("Legacy Company", moved.CompanyName.Value);
    Assert.Equal("EUR", moved.BaseCurrencyCode.Value);
    Assert.Equal(CompanyStatus.Active, moved.Status);
    Assert.Equal(CompanyStatusChangeReason.Administrative, moved.StatusChangeReasonCode);
    Assert.Equal("creator", moved.CreatedBy);
    Assert.Equal("modifier", moved.ModifiedBy);
    Assert.Equal("status-actor", moved.StatusChangedBy);
    Assert.Equal(CompanySqlDatabase.Now, moved.CreatedUtc);
    Assert.NotEmpty(moved.RowVersion);

    // Re-running is idempotent: no duplicate, no overwrite.
    await tenantContext.Database.ExecuteSqlRawAsync(
      "EXEC(N'INSERT INTO [tenant].[Companies] ([CompanyId],[TenantId],[CompanyCode],[NormalizedCompanyCode],[CompanyName],[BaseCurrencyCode],[Status],[StatusChangeReasonCode],[StatusChangedUtc],[StatusChangedBy],[CreatedUtc],[ModifiedUtc],[CreatedBy],[ModifiedBy]) " +
      "SELECT [CompanyId],[TenantId],[CompanyCode],[NormalizedCompanyCode],[CompanyName],[BaseCurrencyCode],[Status],[StatusChangeReasonCode],[StatusChangedUtc],[StatusChangedBy],[CreatedUtc],[ModifiedUtc],[CreatedBy],[ModifiedBy] " +
      "FROM [platform].[Companies_MigratedToTenant] AS source WHERE NOT EXISTS (SELECT 1 FROM [tenant].[Companies] AS existing WHERE existing.[CompanyId] = source.[CompanyId]);')");
    Assert.Equal(1, await tenantContext.Companies.CountAsync());
  }

  [Fact]
  [Trait("Scenario", "TS-CMP-0045")]
  public async Task Company_insert_with_mismatched_tenant_is_rejected_by_assign_tenant()
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantA = await SeedTenantAsync(database, "TENANTA");
    var tenantB = await SeedTenantAsync(database, "TENANTB");

    await using var context = database.CreateTenantContext(tenantA);
    // The aggregate carries Tenant B while the trusted current-tenant context is Tenant A.
    context.Companies.Add(CreateCompany(tenantB, "MISMATCH", "Mismatch Company"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    context.ChangeTracker.Clear();
    Assert.Equal(0, await context.Companies.IgnoreQueryFilters().CountAsync());
  }

  [Fact]
  [Trait("Scenario", "TS-CMP-0045")]
  public async Task Company_tenant_id_cannot_change_after_creation()
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantA = await SeedTenantAsync(database, "TENANTA");
    var tenantB = await SeedTenantAsync(database, "TENANTB");
    Guid companyId;
    await using (var setup = database.CreateTenantContext(tenantA))
    {
      var company = CreateCompany(tenantA, "OWNED", "Owned Company");
      setup.Companies.Add(company);
      Assert.True((await SaveAsync(setup)).IsSuccess);
      companyId = company.CompanyId;
    }

    await using (var mutate = database.CreateTenantContext(tenantA))
    {
      var company = await mutate.Companies.SingleAsync(item => item.Id == companyId);
      mutate.Entry(company).Property(item => item.TenantId).CurrentValue = tenantB;
      await Assert.ThrowsAsync<InvalidOperationException>(() => mutate.SaveChangesAsync());
    }

    await using (var verify = database.CreateTenantContext(tenantA))
    {
      Assert.Equal(tenantA, await verify.Companies
        .Where(item => item.Id == companyId)
        .Select(item => item.TenantId)
        .SingleAsync());
    }
  }

  [Theory]
  [InlineData("ACME")]
  [InlineData("acme")]
  [InlineData("  acme  ")]
  [Trait("Scenario", "TS-CMP-0028")]
  public async Task Concurrent_company_creation_commits_exactly_one_and_maps_the_loser(string competingCode)
  {
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantId = await SeedTenantAsync(database, "TENANTA");
    await using var firstContext = database.CreateTenantContext(tenantId);
    await using var secondContext = database.CreateTenantContext(tenantId);
    var gate = new AsyncGate(2);
    var firstDispatcher = new RecordingDomainEventDispatcher();
    var secondDispatcher = new RecordingDomainEventDispatcher();
    var tenant = new FixedCurrentTenant(tenantId);
    var user = new TestCurrentUser();
    var clock = new TestClock();
    var firstHandler = new CreateCompanyCommandHandler(
      new GatedCompanyRepository(new CompanyRepository(new DirectTenantContextProvider(firstContext)), gate),
      new TenantUnitOfWork(new DirectTenantContextProvider(firstContext), firstDispatcher),
      tenant,
      user,
      clock);
    var secondHandler = new CreateCompanyCommandHandler(
      new GatedCompanyRepository(new CompanyRepository(new DirectTenantContextProvider(secondContext)), gate),
      new TenantUnitOfWork(new DirectTenantContextProvider(secondContext), secondDispatcher),
      tenant,
      user,
      clock);

    var results = await Task.WhenAll(
      firstHandler.HandleAsync(new CreateCompanyCommand("Acme", "Acme One", "USD")),
      secondHandler.HandleAsync(new CreateCompanyCommand(competingCode, "Acme Two", "USD")));

    var successIndex = Array.FindIndex(results, result => result.IsSuccess);
    var failureIndex = Array.FindIndex(results, result => result.IsFailure);
    Assert.NotEqual(-1, successIndex);
    Assert.NotEqual(-1, failureIndex);
    Assert.NotEqual(successIndex, failureIndex);
    Assert.Equal(CompanyErrors.CodeConflict, results[failureIndex].Error);
    Assert.Single(successIndex == 0 ? firstDispatcher.Events : secondDispatcher.Events);
    Assert.Empty(successIndex == 0 ? secondDispatcher.Events : firstDispatcher.Events);

    await using var verification = database.CreateTenantContext(tenantId);
    Assert.Equal(1, await verification.Companies.CountAsync());
    Assert.Equal("ACME", await verification.Companies
      .Select(company => company.NormalizedCompanyCode)
      .SingleAsync());
  }

  [Fact]
  [Trait("Scenario", "TS-CMP-0048")]
  public async Task Company_preserves_utc_audit_and_status_metadata_across_transition()
  {
    var createdAt = CompanySqlDatabase.Now;
    var activatedAt = CompanySqlDatabase.Now.AddHours(3);
    await using var database = await CompanySqlDatabase.CreateAsync();
    var tenantId = await SeedTenantAsync(database, "TENANTA");
    Guid companyId;

    await using (var createContext = database.CreateTenantContext(tenantId, new StubClock(createdAt)))
    {
      var company = Company.Create(
        tenantId,
        CompanyCode.Create("AUDIT").Value,
        CompanyName.Create("Audit Company").Value,
        BaseCurrencyCode.Create("USD").Value,
        "creator-actor",
        Guid.NewGuid(),
        createdAt).Value;
      createContext.Companies.Add(company);
      Assert.True((await SaveAsync(createContext)).IsSuccess);
      companyId = company.CompanyId;
    }

    await using (var afterCreate = database.CreateTenantContext(tenantId))
    {
      var company = await afterCreate.Companies.SingleAsync(item => item.Id == companyId);
      Assert.Equal(CompanyStatus.Inactive, company.Status);
      Assert.Equal(CompanyStatusChangeReason.Created, company.StatusChangeReasonCode);
      Assert.Equal(createdAt, company.CreatedUtc);
      // CreatedBy/ModifiedBy are stamped from the persistence current-user, not the domain actor.
      Assert.Equal("integration-actor", company.CreatedBy);
      Assert.Equal(createdAt, company.ModifiedUtc);
      Assert.Equal(createdAt, company.StatusChangedUtc);
      Assert.Equal("creator-actor", company.StatusChangedBy);
      Assert.Equal(TimeSpan.Zero, company.CreatedUtc.Offset);
      Assert.Equal(TimeSpan.Zero, company.ModifiedUtc.Offset);
      Assert.Equal(TimeSpan.Zero, company.StatusChangedUtc.Offset);
    }

    await using (var transitionContext = database.CreateTenantContext(tenantId, new StubClock(activatedAt)))
    {
      var company = await transitionContext.Companies.SingleAsync(item => item.Id == companyId);
      Assert.True(company.Activate(CompanyStatusChangeReason.Administrative, "activator-actor", Guid.NewGuid(), activatedAt).IsSuccess);
      Assert.True((await SaveAsync(transitionContext)).IsSuccess);
    }

    await using (var afterTransition = database.CreateTenantContext(tenantId))
    {
      var company = await afterTransition.Companies.SingleAsync(item => item.Id == companyId);
      Assert.Equal(CompanyStatus.Active, company.Status);
      Assert.Equal(CompanyStatusChangeReason.Administrative, company.StatusChangeReasonCode);
      Assert.Equal(activatedAt, company.StatusChangedUtc);
      Assert.Equal("activator-actor", company.StatusChangedBy);
      Assert.Equal(activatedAt, company.ModifiedUtc);
      Assert.Equal(createdAt, company.CreatedUtc);
      Assert.Equal("integration-actor", company.CreatedBy);
      Assert.Equal(TimeSpan.Zero, company.ModifiedUtc.Offset);
      Assert.Equal(TimeSpan.Zero, company.StatusChangedUtc.Offset);
    }
  }

  private static async Task<Guid> SeedTenantAsync(CompanySqlDatabase database, string code)
  {
    await using var context = database.CreateContext();
    var tenant = Tenant.Create(
      TenantCode.Create(code).Value,
      TenantName.Create($"{code} Tenant").Value,
      "integration-actor",
      Guid.NewGuid(),
      CompanySqlDatabase.Now).Value;
    context.Tenants.Add(tenant);
    Assert.True((await SaveAsync(context)).IsSuccess);
    return tenant.TenantId;
  }

  private static Company CreateCompany(Guid tenantId, string code, string name, string currency = "USD") => Company.Create(
    tenantId,
    CompanyCode.Create(code).Value,
    CompanyName.Create(name).Value,
    BaseCurrencyCode.Create(currency).Value,
    "integration-actor",
    Guid.NewGuid(),
    CompanySqlDatabase.Now).Value;

  private static Task<Result<int>> SaveAsync(PlatformDbContext context) =>
    new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher()).SaveChangesAsync();

  // Goes through the real TenantUnitOfWork so the tenant stream's failure translation — concurrency,
  // unique violation, write failure — is the code under test rather than a test-local reimplementation.
  private static Task<Result<int>> SaveAsync(TenantDbContext context) =>
    new TenantUnitOfWork(new DirectTenantContextProvider(context), new NoOpDomainEventDispatcher())
      .SaveChangesAsync();

  // Supplies an already-constructed context. Routing itself is proven separately; here the point is the
  // unit of work, so the provider is deliberately trivial.
  private sealed class DirectTenantContextProvider(TenantDbContext context) : ITenantDbContextProvider
  {
    public Task<Result<TenantDbContext>> ResolveAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success(context));

    public Task<TenantDbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(context);
  }

  private static async Task<string> ReadStringAsync(DbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
  }

  private static async Task<int> ReadInt32Async(DbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private static async Task<string[]> ReadStringsAsync(DbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    await using var reader = await command.ExecuteReaderAsync();
    var values = new List<string>();
    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return [.. values];
  }

  private sealed class CompanySqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);

    public static CompanySqlDatabase CreateUnmigrated()
    {
      var databaseName = $"SSAS_ERP_FP005_{Guid.NewGuid():N}";
      var configured = IntegrationSqlEnvironment.BaseConnectionString;
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      return new CompanySqlDatabase(builder.ConnectionString);
    }

    public static async Task<CompanySqlDatabase> CreateAsync()
    {
      var database = CreateUnmigrated();
      try
      {
        // BOTH streams are applied to this one catalog, which is exactly today's shared topology: the
        // tenant ERP schema physically co-resides with the platform schema while remaining a separate
        // context, separate schema and separate migration history.
        await using (var platform = database.CreateContext())
        {
          await platform.Database.MigrateAsync();
        }

        await using var tenant = database.CreateTenantContext();
        await tenant.Database.MigrateAsync();
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext(Guid? currentTenantId = null, IDateTimeProvider? clock = null)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new FixedCurrentTenant(currentTenantId), clock ?? new TestClock());
    }

    // Same catalog, different context, different migration history table. Constructed directly here rather
    // than through the routing factory because these tests are about the tenant SCHEMA and its guarantees;
    // TenantDbContextRoutingSqlServerTests covers the routed construction path.
    public TenantDbContext CreateTenantContext(Guid? currentTenantId = null, IDateTimeProvider? clock = null)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;
      return new TenantDbContext(options, new TestCurrentUser(), new FixedCurrentTenant(currentTenantId), clock ?? new TestClock());
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext();
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-actor";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class FixedCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => CompanySqlDatabase.Now;
  }

  private sealed class StubClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => now;
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

  private sealed class AsyncGate(int participantCount)
  {
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;

    public Task SignalAndWaitAsync()
    {
      if (Interlocked.Increment(ref arrivals) == participantCount)
      {
        ready.SetResult();
      }

      return ready.Task;
    }
  }

  private sealed class GatedCompanyRepository(ICompanyRepository inner, AsyncGate gate) : ICompanyRepository
  {
    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
      inner.GetByIdAsync(companyId, cancellationToken);

    public async Task<bool> NormalizedCodeExistsAsync(
      string normalizedCompanyCode,
      CancellationToken cancellationToken = default)
    {
      var exists = await inner.NormalizedCodeExistsAsync(normalizedCompanyCode, cancellationToken);
      await gate.SignalAndWaitAsync();
      return exists;
    }

    public Task AddAsync(Company company, CancellationToken cancellationToken = default) =>
      inner.AddAsync(company, cancellationToken);
  }
}

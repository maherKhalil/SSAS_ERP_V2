using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Integration.Tests;

public sealed class PlatformAuthenticationPersistenceTests
{
  private const string InitialMigration = "20260731170937_InitialIdentityAccess";

  [Fact]
  [Trait("NonFunctional", "NFR-AUTH-0304")]
  [Trait("Scenario", "TS-AUTH-0060")]
  [Trait("Scenario", "TS-AUTH-0068")]
  public async Task Migration_chain_upgrades_fp001_and_models_global_authentication_ownership()
  {
    await using var database = await SqlTestDatabase.CreateAsync(migrate: false);
    await using var context = database.CreateContext(Guid.NewGuid());

    var designTimeModel = context.GetService<IDesignTimeModel>().Model;
    var accountType = designTimeModel.FindEntityType(typeof(AuthenticationAccount));
    var tokenType = designTimeModel.FindEntityType(typeof(AccountActionToken));
    Assert.NotNull(accountType);
    Assert.NotNull(tokenType);
    Assert.Null(accountType.GetQueryFilter());
    Assert.Null(tokenType.GetQueryFilter());
    Assert.Equal("Latin1_General_100_BIN2", accountType.FindProperty(nameof(AuthenticationAccount.NormalizedLoginEmail))?.GetCollation());
    Assert.Equal("binary(32)", tokenType.FindProperty("secretHash")?.GetColumnType());
    Assert.True(accountType.FindProperty(nameof(AuthenticationAccount.RowVersion))?.IsConcurrencyToken);
    Assert.True(tokenType.FindProperty(nameof(AccountActionToken.RowVersion))?.IsConcurrencyToken);

    await context.GetService<IMigrator>().MigrateAsync(InitialMigration);
    Assert.DoesNotContain("AuthenticationAccounts", await ReadPlatformTablesAsync(context));

    await context.Database.MigrateAsync();
    var tables = await ReadPlatformTablesAsync(context);
    Assert.Contains("AuthenticationAccounts", tables);
    Assert.Contains("AccountActionTokens", tables);
    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-AUTH-0008")]
  [Trait("Decision", "DEC-AUTH-0024")]
  [Trait("Decision", "DEC-AUTH-0029")]
  [Trait("Requirement", "SEC-AUTH-0202")]
  [Trait("Acceptance", "AC-AUTH-0009")]
  [Trait("Scenario", "TS-AUTH-0061")]
  [Trait("Scenario", "TS-AUTH-0062")]
  [Trait("Scenario", "TS-AUTH-0063")]
  public async Task Sql_server_enforces_global_accounts_exact_hash_storage_unique_active_tokens_and_history()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();
    var seed = await CreateSeedAsync(database, tenantId);
    var firstHash = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    var secondHash = Enumerable.Range(32, 32).Select(value => (byte)value).ToArray();
    var thirdHash = Enumerable.Range(64, 32).Select(value => (byte)value).ToArray();

    await using (var context = database.CreateContext(tenantId))
    {
      var first = AccountActionToken.CreateInvitation(
        Guid.NewGuid(), firstHash, seed.IdentityId, seed.AccountId, tenantId, seed.TenantUserId,
        database.Clock.UtcNow, database.Clock.UtcNow.AddHours(24), Guid.NewGuid());
      context.AccountActionTokens.Add(first);
      Assert.True((await SaveAsync(context)).IsSuccess);
      Assert.True(first.Consume(Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(1)).IsSuccess);
      Assert.True((await SaveAsync(context)).IsSuccess);

      var second = AccountActionToken.CreateInvitation(
        Guid.NewGuid(), secondHash, seed.IdentityId, seed.AccountId, tenantId, seed.TenantUserId,
        database.Clock.UtcNow.AddMinutes(2), database.Clock.UtcNow.AddHours(24), Guid.NewGuid());
      context.AccountActionTokens.Add(second);
      Assert.True((await SaveAsync(context)).IsSuccess);
      Assert.True(second.Revoke("integration-user", "Replaced", Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(3)).IsSuccess);
      Assert.True((await SaveAsync(context)).IsSuccess);

      var active = AccountActionToken.CreateInvitation(
        Guid.NewGuid(), thirdHash, seed.IdentityId, seed.AccountId, tenantId, seed.TenantUserId,
        database.Clock.UtcNow.AddMinutes(4), database.Clock.UtcNow.AddHours(24), Guid.NewGuid());
      context.AccountActionTokens.Add(active);
      Assert.True((await SaveAsync(context)).IsSuccess);
    }

    await using (var duplicateContext = database.CreateContext(tenantId))
    {
      var duplicate = AccountActionToken.CreateInvitation(
        Guid.NewGuid(), RandomHash(), seed.IdentityId, seed.AccountId, tenantId, seed.TenantUserId,
        database.Clock.UtcNow.AddMinutes(5), database.Clock.UtcNow.AddHours(24), Guid.NewGuid());
      duplicateContext.AccountActionTokens.Add(duplicate);
      var result = await SaveAsync(duplicateContext);
      Assert.True(result.IsFailure);
      Assert.Equal("Persistence.UniqueConstraint", result.Error.Code);
    }

    await using (var duplicateSelectorContext = database.CreateContext(tenantId))
    {
      var existingSelector = await duplicateSelectorContext.AccountActionTokens
        .Select(token => token.PublicId)
        .FirstAsync();
      duplicateSelectorContext.AccountActionTokens.Add(AccountActionToken.CreatePasswordReset(
        existingSelector,
        RandomHash(),
        seed.IdentityId,
        seed.AccountId,
        database.Clock.UtcNow.AddMinutes(6),
        database.Clock.UtcNow.AddMinutes(36),
        Guid.NewGuid()));
      var result = await SaveAsync(duplicateSelectorContext);
      Assert.True(result.IsFailure);
      Assert.Equal("Persistence.UniqueConstraint", result.Error.Code);
    }

    await using (var context = database.CreateContext(Guid.NewGuid()))
    {
      Assert.Single(await context.AuthenticationAccounts.ToArrayAsync());
      Assert.Equal(3, await context.AccountActionTokens.CountAsync());
      Assert.Single(await context.AccountActionTokens.Where(token => token.ConsumedUtc != null).ToArrayAsync());
      Assert.Single(await context.AccountActionTokens.Where(token => token.RevokedUtc != null).ToArrayAsync());
      Assert.Single(await context.AccountActionTokens.Where(token => token.ConsumedUtc == null && token.RevokedUtc == null).ToArrayAsync());

      var storedHash = await ReadBytesAsync(context,
        "SELECT TOP (1) [SecretHash] FROM [platform].[AccountActionTokens] ORDER BY [AccountActionTokenId]");
      Assert.Equal(32, storedHash.Length);
      Assert.Equal(firstHash, storedHash);
      var columns = await ReadColumnNamesAsync(context, "AccountActionTokens");
      Assert.Contains("SecretHash", columns);
      Assert.DoesNotContain(columns, name => name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Plain", StringComparison.OrdinalIgnoreCase));
      Assert.Equal(0, await ReadInt32Async(context,
        "SELECT COUNT(*) FROM [platform].[AuthenticationAccounts] WHERE [NormalizedLoginEmail] = N'global.user@example.com'"));
    }

    await VerifyAccountUniquenessAsync(database, tenantId, seed);
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0025")]
  [Trait("Requirement", "FR-AUTH-0101")]
  [Trait("Scenario", "TS-AUTH-0061")]
  public async Task Concurrent_account_creation_allows_only_one_identity_and_authentication_account()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    using var ready = new Barrier(2);

    async Task<Result<int>> CreateAsync()
    {
      await using var context = database.CreateContext(Guid.NewGuid());
      var unitOfWork = new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher());
      await using var transaction = await unitOfWork.BeginTransactionAsync();
      var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
      context.Identities.Add(identity);
      var identitySave = await unitOfWork.SaveChangesAsync();
      if (identitySave.IsFailure)
      {
        return identitySave;
      }

      Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(30)));
      context.AuthenticationAccounts.Add(AuthenticationAccount.CreatePending(
        identity.Id,
        LoginEmail.Create("concurrent.account@example.com").Value));
      var accountSave = await unitOfWork.SaveChangesAsync();
      if (accountSave.IsFailure)
      {
        return accountSave;
      }

      await transaction.CommitAsync();
      return accountSave;
    }

    var attempts = await Task.WhenAll(Task.Run(CreateAsync), Task.Run(CreateAsync));

    Assert.Single(attempts.Where(result => result.IsSuccess));
    Assert.Single(attempts.Where(result => result.Error.Code == "Persistence.UniqueConstraint"));
    await using var verification = database.CreateContext(Guid.NewGuid());
    Assert.Single(await verification.Identities.ToArrayAsync());
    Assert.Single(await verification.AuthenticationAccounts.ToArrayAsync());
  }

  [Fact]
  [Trait("Requirement", "FR-AUTH-0102")]
  [Trait("Scenario", "TS-AUTH-0061")]
  public async Task Sql_server_rejects_active_account_without_verified_email_and_password_change_timestamp()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    await using var context = database.CreateContext(Guid.NewGuid());
    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await SaveAsync(context)).IsSuccess);

    var exception = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO [platform].[AuthenticationAccounts]
        ([IdentityId], [LoginEmail], [NormalizedLoginEmail], [Status], [FailedAttemptCount], [SecurityVersion],
         [CreatedUtc], [ModifiedUtc], [PasswordHash])
      VALUES
        ({identity.Id}, N'unverified@example.com', N'UNVERIFIED@EXAMPLE.COM', N'Active', 0, 1,
         {database.Clock.UtcNow}, {database.Clock.UtcNow}, N'integration-password-hash')
      """));

    Assert.Equal(547, exception.Number);
    Assert.Empty(await context.AuthenticationAccounts.ToArrayAsync());
  }

  [Fact]
  [Trait("Decision", "DEC-AUTH-0031")]
  [Trait("NonFunctional", "NFR-AUTH-0304")]
  [Trait("Acceptance", "AC-AUTH-0016")]
  [Trait("Scenario", "TS-AUTH-0064")]
  [Trait("Scenario", "TS-AUTH-0065")]
  [Trait("Scenario", "TS-AUTH-0067")]
  public async Task Rowversion_allows_one_token_consumer_preserves_failed_attempts_and_restricts_deletes()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();
    var seed = await CreateSeedAsync(database, tenantId);
    long tokenId;

    await using (var context = database.CreateContext(tenantId))
    {
      var token = AccountActionToken.CreatePasswordReset(
        Guid.NewGuid(), RandomHash(), seed.IdentityId, seed.AccountId,
        database.Clock.UtcNow, database.Clock.UtcNow.AddMinutes(30), Guid.NewGuid());
      context.AccountActionTokens.Add(token);
      Assert.True((await SaveAsync(context)).IsSuccess);
      tokenId = token.Id;
    }

    await using (var first = database.CreateContext(tenantId))
    await using (var second = database.CreateContext(tenantId))
    {
      var firstToken = await first.AccountActionTokens.SingleAsync(token => token.Id == tokenId);
      var secondToken = await second.AccountActionTokens.SingleAsync(token => token.Id == tokenId);
      Assert.True(firstToken.Consume(Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(1)).IsSuccess);
      Assert.True(secondToken.Consume(Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(1)).IsSuccess);
      Assert.True((await SaveAsync(first)).IsSuccess);
      var stale = await SaveAsync(second);
      Assert.True(stale.IsFailure);
      Assert.Equal("Persistence.ConcurrencyConflict", stale.Error.Code);
    }

    await using (var first = database.CreateContext(tenantId))
    await using (var second = database.CreateContext(tenantId))
    {
      var firstAccount = await first.AuthenticationAccounts.SingleAsync(account => account.Id == seed.AccountId);
      var secondAccount = await second.AuthenticationAccounts.SingleAsync(account => account.Id == seed.AccountId);
      Assert.True(firstAccount.RecordFailedAttempt(5, TimeSpan.FromMinutes(15), Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True(secondAccount.RecordFailedAttempt(5, TimeSpan.FromMinutes(15), Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(first)).IsSuccess);
      var stale = await SaveAsync(second);
      Assert.True(stale.IsFailure);
      Assert.Equal("Persistence.ConcurrencyConflict", stale.Error.Code);
      await second.Entry(secondAccount).ReloadAsync();
      Assert.True(secondAccount.RecordFailedAttempt(
        5, TimeSpan.FromMinutes(15), Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(second)).IsSuccess);
      for (var attempt = 0; attempt < 3; attempt++)
      {
        Assert.True(secondAccount.RecordFailedAttempt(
          5, TimeSpan.FromMinutes(15), Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
        Assert.True((await SaveAsync(second)).IsSuccess);
      }
    }

    await using (var verification = database.CreateContext(tenantId))
    {
      var account = await verification.AuthenticationAccounts.SingleAsync();
      Assert.Equal(5, account.FailedAttemptCount);
      Assert.Equal(database.Clock.UtcNow.AddMinutes(15), account.LockoutEndUtc);
      Assert.NotNull((await verification.AccountActionTokens.SingleAsync()).ConsumedUtc);
    }

    await using (var deleteContext = database.CreateContext(tenantId))
    {
      deleteContext.AuthenticationAccounts.Remove(await deleteContext.AuthenticationAccounts.SingleAsync());
      var result = await SaveAsync(deleteContext);
      Assert.True(result.IsFailure);
      Assert.Equal("Persistence.WriteFailure", result.Error.Code);
    }

    await using (var verification = database.CreateContext(tenantId))
    {
      Assert.Single(await verification.AuthenticationAccounts.ToArrayAsync());
      Assert.Single(await verification.AccountActionTokens.ToArrayAsync());
    }
  }

  private static async Task VerifyAccountUniquenessAsync(SqlTestDatabase database, Guid tenantId, Seed seed)
  {
    await using (var sameIdentity = database.CreateContext(tenantId))
    {
      sameIdentity.AuthenticationAccounts.Add(AuthenticationAccount.CreatePending(
        seed.IdentityId,
        LoginEmail.Create("another@example.com").Value));
      var result = await SaveAsync(sameIdentity);
      Assert.True(result.IsFailure);
      Assert.Equal("Persistence.UniqueConstraint", result.Error.Code);
    }

    long secondIdentityId;
    await using (var context = database.CreateContext(tenantId))
    {
      var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
      context.Identities.Add(identity);
      Assert.True((await SaveAsync(context)).IsSuccess);
      secondIdentityId = identity.Id;
    }

    await using (var sameEmail = database.CreateContext(Guid.NewGuid()))
    {
      sameEmail.AuthenticationAccounts.Add(AuthenticationAccount.CreatePending(
        secondIdentityId,
        LoginEmail.Create("GLOBAL.USER@EXAMPLE.COM").Value));
      var result = await SaveAsync(sameEmail);
      Assert.True(result.IsFailure);
      Assert.Equal("Persistence.UniqueConstraint", result.Error.Code);
    }
  }

  private static async Task<Seed> CreateSeedAsync(SqlTestDatabase database, Guid tenantId)
  {
    await using var context = database.CreateContext(tenantId);
    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await SaveAsync(context)).IsSuccess);
    var account = AuthenticationAccount.CreatePending(identity.Id, LoginEmail.Create("Global.User@example.com").Value);
    context.AuthenticationAccounts.Add(account);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    var membership = TenantUser.CreatePending(
      identity.Id,
      tenantId,
      EmailAddress.Create("member@example.com").Value,
      UserDisplayName.Create("Pending Member").Value,
      Guid.NewGuid(),
      database.Clock.UtcNow);
    context.TenantUsers.Add(membership);
    Assert.True((await SaveAsync(context)).IsSuccess);
    return new Seed(identity.Id, account.Id, membership.Id);
  }

  private static byte[] RandomHash() => Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray();

  private static Task<Result<int>> SaveAsync(PlatformDbContext context) =>
    new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher()).SaveChangesAsync();

  private static async Task<IReadOnlyCollection<string>> ReadPlatformTablesAsync(PlatformDbContext context)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'platform'";
    var values = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) values.Add(reader.GetString(0));
    return values;
  }

  private static async Task<IReadOnlyCollection<string>> ReadColumnNamesAsync(PlatformDbContext context, string table)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = @table";
    var parameter = command.CreateParameter();
    parameter.ParameterName = "@table";
    parameter.Value = table;
    command.Parameters.Add(parameter);
    var values = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) values.Add(reader.GetString(0));
    return values;
  }

  private static async Task<byte[]> ReadBytesAsync(PlatformDbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return (byte[])(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a binary value."));
  }

  private static async Task<int> ReadInt32Async(PlatformDbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private sealed class SqlTestDatabase(string connectionString) : IAsyncDisposable
  {
    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public static async Task<SqlTestDatabase> CreateAsync(bool migrate = true)
    {
      var name = $"SSAS_ERP_FP002_M2_{Guid.NewGuid():N}";
      var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
      var connectionString = new SqlConnectionStringBuilder(configured) { InitialCatalog = name }.ConnectionString;
      var database = new SqlTestDatabase(connectionString);
      try
      {
        if (migrate)
        {
          await using var context = database.CreateContext(Guid.NewGuid());
          await context.Database.MigrateAsync();
        }
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext(Guid tenantId)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(tenantId), Clock);
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext(Guid.NewGuid());
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed record Seed(long IdentityId, long AccountId, long TenantUserId);

  private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-user";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class MutableClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}

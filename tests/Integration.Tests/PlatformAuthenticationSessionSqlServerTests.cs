using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

// Phase 3C-3 platform authentication session persistence SQL verification (ADR-016 / DEC-TEN-0022).
public sealed class PlatformAuthenticationSessionSqlServerTests
{
  private const string Client = "ssas-erp-web";

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Migration_creates_platform_session_tables_with_expected_non_tenant_shape()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());

    var sessionColumns = await ReadStringsAsync(context,
      "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformAuthenticationSessions' ORDER BY ORDINAL_POSITION");
    Assert.Contains("IdentityId", sessionColumns);
    Assert.Contains("PlatformSupportPrincipalId", sessionColumns);
    Assert.Contains("SecurityVersionAtCreation", sessionColumns);
    Assert.Contains("RowVersion", sessionColumns);
    foreach (var forbidden in new[] { "TenantId", "TenantUserId", "CompanyId" })
    {
      Assert.DoesNotContain(forbidden, sessionColumns);
    }

    var refreshColumns = await ReadStringsAsync(context,
      "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformRefreshTokenRecords' ORDER BY ORDINAL_POSITION");
    Assert.Contains("PlatformAuthenticationSessionId", refreshColumns);
    Assert.Contains("PublicId", refreshColumns);
    Assert.Contains("SecretHash", refreshColumns);

    // Foreign keys: Identity + composite Principal(Id, IdentityId) + refresh session FK — all Restrict (NO ACTION).
    Assert.Equal(1, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[platform].[PlatformAuthenticationSessions]') AND referenced_object_id = OBJECT_ID(N'[platform].[Identities]') AND delete_referential_action = 0"));
    Assert.Equal(1, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[platform].[PlatformAuthenticationSessions]') AND referenced_object_id = OBJECT_ID(N'[platform].[PlatformSupportPrincipals]') AND delete_referential_action = 0"));

    // Unique public-id index, RowVersion columns, and CHECK constraints exist.
    Assert.Equal(1, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[PlatformRefreshTokenRecords]') AND name = N'UX_PlatformRefreshTokenRecords_PublicId' AND is_unique = 1"));
    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformAuthenticationSessions' AND COLUMN_NAME='Status'"));
    foreach (var check in new[]
    {
      "CK_PlatformAuthenticationSessions_Status",
      "CK_PlatformAuthenticationSessions_RevocationReason",
      "CK_PlatformAuthenticationSessions_LifecycleMetadata",
      "CK_PlatformAuthenticationSessions_Expiry",
      "CK_PlatformAuthenticationSessions_SecurityVersion"
    })
    {
      Assert.Equal(1, await ReadInt32Async(context,
        $"SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[platform].[PlatformAuthenticationSessions]') AND name = N'{check}'"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Tenant_session_schema_is_unchanged_by_the_platform_migration()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    // The tenant AuthenticationSessions/RefreshTokenRecords tables keep their tenant columns and gain no
    // platform column or discriminator.
    var tenantSession = await ReadStringsAsync(context,
      "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='AuthenticationSessions'");
    Assert.Contains("TenantId", tenantSession);
    Assert.Contains("TenantUserId", tenantSession);
    Assert.DoesNotContain("PlatformSupportPrincipalId", tenantSession);
    Assert.DoesNotContain("SecurityPlane", tenantSession);

    var tenantRefresh = await ReadStringsAsync(context,
      "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='RefreshTokenRecords'");
    Assert.DoesNotContain("PlatformAuthenticationSessionId", tenantRefresh);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Session_and_refresh_token_round_trip_through_the_real_provider()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var publicId = Guid.NewGuid();

    var sessionId = await PersistSessionWithTokenAsync(database, identityId, principalId, publicId);

    await using var verify = database.CreateContext();
    var session = await verify.PlatformAuthenticationSessions
      .Include(item => item.RefreshTokenRecords)
      .AsNoTracking()
      .SingleAsync(item => item.Id == sessionId);
    Assert.Equal(identityId, session.IdentityId);
    Assert.Equal(principalId, session.PlatformSupportPrincipalId);
    Assert.Equal(AuthenticationSessionStatus.Active, session.Status);
    Assert.True(session.SecurityVersionAtCreation > 0);
    Assert.Single(session.RefreshTokenRecords);
    Assert.Equal(publicId, session.RefreshTokenRecords.Single().PublicId);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Foreign_keys_reject_an_unknown_identity_or_a_mismatched_principal_identity()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var otherIdentityId = await SeedIdentityAsync(database);

    // Unknown identity → Identity FK violation.
    await using (var context = database.CreateContext())
    {
      context.PlatformAuthenticationSessions.Add(NewSession(999999, principalId));
      await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    // Principal exists but bound to a different identity → composite (PrincipalId, IdentityId) FK violation.
    await using (var context = database.CreateContext())
    {
      context.PlatformAuthenticationSessions.Add(NewSession(otherIdentityId, principalId));
      await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Rowversion_detects_a_concurrent_conflicting_update()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var sessionId = await PersistSessionWithTokenAsync(database, identityId, principalId, Guid.NewGuid());

    await using var contextA = database.CreateContext();
    await using var contextB = database.CreateContext();
    var sessionA = await contextA.PlatformAuthenticationSessions.SingleAsync(item => item.Id == sessionId);
    var sessionB = await contextB.PlatformAuthenticationSessions.SingleAsync(item => item.Id == sessionId);

    Assert.True(sessionA.Revoke(PlatformAuthenticationSessionRevocationReason.Administrative, "a", PlatformSessionSqlDatabase.Now).IsSuccess);
    await contextA.SaveChangesAsync();

    Assert.True(sessionB.Revoke(PlatformAuthenticationSessionRevocationReason.UserLogout, "b", PlatformSessionSqlDatabase.Now).IsSuccess);
    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Duplicate_refresh_token_public_id_violates_uniqueness()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var sharedPublicId = Guid.NewGuid();
    await PersistSessionWithTokenAsync(database, identityId, principalId, sharedPublicId);

    // A second session's initial refresh token reusing the same public id is rejected by the unique index.
    await Assert.ThrowsAnyAsync<Exception>(() => PersistSessionWithTokenAsync(database, identityId, principalId, sharedPublicId));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Platform_refresh_token_is_invisible_to_the_tenant_session_repository()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var publicId = Guid.NewGuid();
    await PersistSessionWithTokenAsync(database, identityId, principalId, publicId);

    await using var context = database.CreateContext();

    // The platform repository resolves the platform token to a platform (identity+principal) locator...
    var platformLocator = await new PlatformAuthenticationSessionRepository(context).GetRefreshTokenLocatorAsync(publicId);
    Assert.NotNull(platformLocator);
    Assert.Equal(identityId, platformLocator!.IdentityId);
    Assert.Equal(principalId, platformLocator.PlatformSupportPrincipalId);

    // ...while the tenant repository (querying only tenant tables) cannot see it at all.
    var tenantLocator = await new AuthenticationSessionRepository(context).GetRefreshTokenLocatorAsync(publicId);
    Assert.Null(tenantLocator);

    // And the platform repository only ever finds platform tokens.
    Assert.Null(await new PlatformAuthenticationSessionRepository(context).GetRefreshTokenLocatorAsync(Guid.NewGuid()));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Active_session_limit_query_counts_platform_sessions_only()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    await PersistSessionWithTokenAsync(database, identityId, principalId, Guid.NewGuid());
    await PersistSessionWithTokenAsync(database, identityId, principalId, Guid.NewGuid());

    await using var context = database.CreateContext();
    var repository = new PlatformAuthenticationSessionRepository(context);
    var active = await repository.ListActiveUnexpiredByIdentityForUpdateAsync(identityId, PlatformSessionSqlDatabase.Now.AddMinutes(1));

    Assert.Equal(2, active.Count);
    Assert.All(active, session => Assert.Equal(AuthenticationSessionStatus.Active, session.Status));
    // The tenant session table is unaffected (no platform session leaked into it).
    Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[AuthenticationSessions]"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Active_by_principal_query_returns_only_active_sessions_for_that_principal()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var otherIdentityId = await SeedIdentityAsync(database);
    var otherPrincipalId = await SeedPrincipalAsync(database, otherIdentityId);

    var activeOne = await PersistSessionWithTokenAsync(database, identityId, principalId, Guid.NewGuid());
    var toRevoke = await PersistSessionWithTokenAsync(database, identityId, principalId, Guid.NewGuid());
    await PersistSessionWithTokenAsync(database, otherIdentityId, otherPrincipalId, Guid.NewGuid());

    // Revoke one of the principal's sessions.
    await using (var context = database.CreateContext())
    {
      var session = await context.PlatformAuthenticationSessions.SingleAsync(item => item.Id == toRevoke);
      Assert.True(session.Revoke(PlatformAuthenticationSessionRevocationReason.Administrative, "actor", PlatformSessionSqlDatabase.Now).IsSuccess);
      await context.SaveChangesAsync();
    }

    await using var verify = database.CreateContext();
    var active = await new PlatformAuthenticationSessionRepository(verify).ListActiveByPrincipalForUpdateAsync(principalId);

    Assert.Single(active);
    Assert.Equal(activeOne, active.Single().Id);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Physical_deletion_of_platform_session_or_refresh_history_is_rejected()
  {
    await using var database = await PlatformSessionSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedIdentityAndPrincipalAsync(database);
    var sessionId = await PersistSessionWithTokenAsync(database, identityId, principalId, Guid.NewGuid());

    // Physically deleting a refresh-token record (the dependent) is rejected by the retention guard.
    await using (var context = database.CreateContext())
    {
      var token = await context.PlatformRefreshTokenRecords.SingleAsync();
      context.PlatformRefreshTokenRecords.Remove(token);
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    // Physically deleting a platform session is rejected by the retention guard (childless session, so the
    // guard — not the identifying-FK — is what rejects it).
    var childlessSessionId = await PersistChildlessSessionAsync(database, identityId, principalId);
    await using (var context = database.CreateContext())
    {
      var session = await context.PlatformAuthenticationSessions.SingleAsync(item => item.Id == childlessSessionId);
      context.PlatformAuthenticationSessions.Remove(session);
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    // Revocation (a status UPDATE, not a DELETE) remains allowed.
    await using (var context = database.CreateContext())
    {
      var session = await context.PlatformAuthenticationSessions.SingleAsync(item => item.Id == sessionId);
      Assert.True(session.Revoke(PlatformAuthenticationSessionRevocationReason.UserLogout, "actor", PlatformSessionSqlDatabase.Now).IsSuccess);
      await context.SaveChangesAsync();
    }

    await using var status = database.CreateContext();
    Assert.Equal("Revoked", await ReadStringAsync(status,
      $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
  }

  // ---- Seeding + persistence helpers ----

  private static PlatformAuthenticationSession NewSession(long identityId, long principalId) =>
    PlatformAuthenticationSession.Create(
      identityId, principalId, Client, Guid.NewGuid(), securityVersionAtCreation: 1,
      PlatformSessionSqlDatabase.Now, PlatformSessionSqlDatabase.Now.AddMinutes(30), PlatformSessionSqlDatabase.Now.AddHours(8));

  private static async Task<long> PersistSessionWithTokenAsync(
    PlatformSessionSqlDatabase database, long identityId, long principalId, Guid publicId)
  {
    await using var context = database.CreateContext();
    var uow = new TestPlatformUnitOfWork(context);
    var repository = new PlatformAuthenticationSessionRepository(context);
    var session = NewSession(identityId, principalId);
    await repository.AddAsync(session);
    Assert.True((await uow.SaveChangesAsync()).IsSuccess);

    session.CreateInitialRefreshToken(publicId, new byte[32], PlatformSessionSqlDatabase.Now);
    Assert.True((await uow.SaveChangesAsync()).IsSuccess);
    return session.Id;
  }

  private static async Task<long> PersistChildlessSessionAsync(
    PlatformSessionSqlDatabase database, long identityId, long principalId)
  {
    await using var context = database.CreateContext();
    var session = NewSession(identityId, principalId);
    await new PlatformAuthenticationSessionRepository(context).AddAsync(session);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return session.Id;
  }

  private static async Task<(long IdentityId, long PrincipalId)> SeedIdentityAndPrincipalAsync(PlatformSessionSqlDatabase database)
  {
    var identityId = await SeedIdentityAsync(database);
    var principalId = await SeedPrincipalAsync(database, identityId);
    return (identityId, principalId);
  }

  private static async Task<long> SeedIdentityAsync(PlatformSessionSqlDatabase database)
  {
    await using var context = database.CreateContext();
    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return identity.Id;
  }

  private static async Task<long> SeedPrincipalAsync(PlatformSessionSqlDatabase database, long identityId)
  {
    await using var context = database.CreateContext();
    var principal = PlatformSupportPrincipal.Register(identityId).Value;
    await context.PlatformSupportPrincipals.AddAsync(principal);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return principal.Id;
  }

  private static async Task<string[]> ReadStringsAsync(PlatformDbContext context, string commandText)
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

  private static async Task<int> ReadInt32Async(PlatformDbContext context, string commandText)
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

  private static async Task<string> ReadStringAsync(PlatformDbContext context, string commandText)
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

  // ---- Harness ----

  private sealed class TestPlatformUnitOfWork(PlatformDbContext context)
    : SSAS.Platform.Application.Abstractions.Persistence.IPlatformUnitOfWork
  {
    private readonly PlatformUnitOfWork inner = new(context, new NoOpDomainEventDispatcher());

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      inner.SaveChangesAsync(cancellationToken);

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      inner.BeginTransactionAsync(cancellationToken);
  }

  private sealed class PlatformSessionSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 11, 11, 0, 0, TimeSpan.Zero);

    public static async Task<PlatformSessionSqlDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP003_PSESS_{Guid.NewGuid():N}";
      var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      var database = new PlatformSessionSqlDatabase(builder.ConnectionString);
      try
      {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(), new TestClock());
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

  private sealed class TestCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => Guid.NewGuid();
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => PlatformSessionSqlDatabase.Now;
  }
}

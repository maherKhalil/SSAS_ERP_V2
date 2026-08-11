using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Platform.API;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.Integration.Tests;

public sealed class PlatformAuthenticationPersistenceTests
{
  private const string InitialMigration = "20260731170937_InitialIdentityAccess";
  private const string TenantLifecycleMigration = "20260801085259_AddTenantLifecycle";
  private const string AuthenticationSessionsMigration = "20260801111512_AddAuthenticationSessionsAndTenantSelection";
  private const string UserLogoutMigration = "20260801135811_AddUserLogoutSessionRevocationReason";

  [Fact]
  [Trait("Scenario", "TS-AUTH-0113")]
  [Trait("Acceptance", "AC-AUTH-0051")]
  public async Task User_logout_constraint_migration_upgrades_downgrades_reapplies_and_retains_session_data()
  {
    await using var database = await SqlTestDatabase.CreateAsync(migrate: false);
    await using (var migrationContext = database.CreateContext(Guid.NewGuid()))
      await migrationContext.GetService<IMigrator>().MigrateAsync(AuthenticationSessionsMigration);
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var seed = await CreateRefreshSessionSeedAsync(database, 91, clientId);

    await using (var upgradeContext = database.CreateContext(seed.TenantId))
      await upgradeContext.GetService<IMigrator>().MigrateAsync(UserLogoutMigration);
    await using (var acceptedContext = database.CreateContext(seed.TenantId))
    {
      await acceptedContext.Database.ExecuteSqlInterpolatedAsync($$"""
        UPDATE [platform].[AuthenticationSessions]
        SET [Status] = N'Revoked', [RevokedUtc] = SYSUTCDATETIME(), [RevocationReason] = N'UserLogout'
        WHERE [AuthenticationSessionId] = {{seed.AuthenticationSessionId}}
        """);
      Assert.Equal("UserLogout", await acceptedContext.Database.SqlQueryRaw<string>(
        "SELECT [RevocationReason] AS [Value] FROM [platform].[AuthenticationSessions] WHERE [AuthenticationSessionId] = {0}",
        seed.AuthenticationSessionId).SingleAsync());
    }

    await using (var invalidContext = database.CreateContext(seed.TenantId))
    {
      await Assert.ThrowsAsync<SqlException>(() => invalidContext.Database.ExecuteSqlInterpolatedAsync($$"""
        UPDATE [platform].[AuthenticationSessions]
        SET [RevocationReason] = N'InvalidReason'
        WHERE [AuthenticationSessionId] = {{seed.AuthenticationSessionId}}
        """));
    }

    await using (var prepareDowngrade = database.CreateContext(seed.TenantId))
      await prepareDowngrade.Database.ExecuteSqlInterpolatedAsync($$"""
        UPDATE [platform].[AuthenticationSessions]
        SET [RevocationReason] = N'Administrative'
        WHERE [AuthenticationSessionId] = {{seed.AuthenticationSessionId}}
        """);
    await using (var downgradeContext = database.CreateContext(seed.TenantId))
      await downgradeContext.GetService<IMigrator>().MigrateAsync(AuthenticationSessionsMigration);
    await using (var retainedContext = database.CreateContext(seed.TenantId))
    {
      Assert.Equal(1, await retainedContext.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS [Value] FROM [platform].[AuthenticationSessions] WHERE [AuthenticationSessionId] = {0}",
        seed.AuthenticationSessionId).SingleAsync());
      await Assert.ThrowsAsync<SqlException>(() => retainedContext.Database.ExecuteSqlInterpolatedAsync($$"""
        UPDATE [platform].[AuthenticationSessions]
        SET [RevocationReason] = N'UserLogout'
        WHERE [AuthenticationSessionId] = {{seed.AuthenticationSessionId}}
        """));
    }

    await using (var reapplyContext = database.CreateContext(seed.TenantId))
    {
      await reapplyContext.GetService<IMigrator>().MigrateAsync(UserLogoutMigration);
      await reapplyContext.Database.ExecuteSqlInterpolatedAsync($$"""
        UPDATE [platform].[AuthenticationSessions]
        SET [RevocationReason] = N'UserLogout'
        WHERE [AuthenticationSessionId] = {{seed.AuthenticationSessionId}}
        """);
      await reapplyContext.GetService<IMigrator>().MigrateAsync();
      Assert.Empty(await reapplyContext.Database.GetPendingMigrationsAsync());
    }
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0060")]
  [Trait("Scenario", "TS-AUTH-0067")]
  public async Task Session_migration_upgrades_rolls_back_and_reapplies_without_deferred_tables()
  {
    await using var database = await SqlTestDatabase.CreateAsync(migrate: false);
    await using var context = database.CreateContext(Guid.NewGuid());
    var migrator = context.GetService<IMigrator>();

    await migrator.MigrateAsync(TenantLifecycleMigration);
    var before = await ReadPlatformTablesAsync(context);
    Assert.DoesNotContain("AuthenticationSessions", before);
    Assert.DoesNotContain("RefreshTokenRecords", before);
    Assert.DoesNotContain("TenantSelectionTransactions", before);

    await migrator.MigrateAsync();
    var applied = await ReadPlatformTablesAsync(context);
    Assert.Contains("AuthenticationSessions", applied);
    Assert.Contains("RefreshTokenRecords", applied);
    Assert.Contains("TenantSelectionTransactions", applied);
    Assert.DoesNotContain(applied, table => table is "AccessTokens" or "SigningKeys" or "AuthenticationAuditStore");

    await migrator.MigrateAsync(TenantLifecycleMigration);
    var rolledBack = await ReadPlatformTablesAsync(context);
    Assert.DoesNotContain("AuthenticationSessions", rolledBack);
    Assert.DoesNotContain("RefreshTokenRecords", rolledBack);
    Assert.DoesNotContain("TenantSelectionTransactions", rolledBack);

    await migrator.MigrateAsync();
    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    Assert.Equal(3, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.triggers WHERE name IN (N'TR_AuthenticationSessions_PreventDelete', N'TR_RefreshTokenRecords_PreventDelete', N'TR_TenantSelectionTransactions_PreventDelete')"));
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0062")]
  [Trait("Scenario", "TS-AUTH-0067")]
  [Trait("Scenario", "TS-AUTH-0068")]
  public async Task Session_migration_models_global_ownership_hash_only_storage_rowversions_and_delete_guards()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    await using var context = database.CreateContext(Guid.NewGuid());

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    var model = context.GetService<IDesignTimeModel>().Model;
    var sessionType = model.FindEntityType(typeof(AuthenticationSession));
    var refreshType = model.FindEntityType(typeof(RefreshTokenRecord));
    var selectionType = model.FindEntityType(typeof(TenantSelectionTransaction));
    Assert.NotNull(sessionType);
    Assert.NotNull(refreshType);
    Assert.NotNull(selectionType);
    Assert.Null(sessionType.GetQueryFilter());
    Assert.Null(refreshType.GetQueryFilter());
    Assert.Null(selectionType.GetQueryFilter());
    Assert.True(sessionType.FindProperty(nameof(AuthenticationSession.RowVersion))?.IsConcurrencyToken);
    Assert.True(refreshType.FindProperty(nameof(RefreshTokenRecord.RowVersion))?.IsConcurrencyToken);
    Assert.True(selectionType.FindProperty(nameof(TenantSelectionTransaction.RowVersion))?.IsConcurrencyToken);
    Assert.Equal("binary(32)", refreshType.FindProperty("secretHash")?.GetColumnType());
    Assert.Equal("binary(32)", selectionType.FindProperty("secretHash")?.GetColumnType());

    var tables = await ReadPlatformTablesAsync(context);
    Assert.Contains("AuthenticationSessions", tables);
    Assert.Contains("RefreshTokenRecords", tables);
    Assert.Contains("TenantSelectionTransactions", tables);
    Assert.Equal(3, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.triggers WHERE name IN (N'TR_AuthenticationSessions_PreventDelete', N'TR_RefreshTokenRecords_PreventDelete', N'TR_TenantSelectionTransactions_PreventDelete')"));
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0066")]
  [Trait("Scenario", "TS-AUTH-0089")]
  [Trait("Acceptance", "AC-AUTH-0021")]
  [Trait("Acceptance", "AC-AUTH-0031")]
  public async Task Repeated_concurrent_refresh_rotates_once_and_verified_loser_compromises_only_owning_session()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

    for (var iteration = 0; iteration < 3; iteration++)
    {
      var seed = await CreateRefreshSessionSeedAsync(database, iteration, clientId);
      await using var firstContext = database.CreateContext(seed.TenantId);
      await using var secondContext = database.CreateContext(seed.TenantId);
      var firstHandler = CreateRefreshHandler(database, firstContext);
      var secondHandler = CreateRefreshHandler(database, secondContext);

      var attempts = await Task.WhenAll(
        firstHandler.HandleAsync(new RefreshAuthenticationSessionCommand(
          new SensitiveAuthenticationTokenInput(seed.RawRefreshToken), clientId)),
        secondHandler.HandleAsync(new RefreshAuthenticationSessionCommand(
          new SensitiveAuthenticationTokenInput(seed.RawRefreshToken), clientId)));

      Assert.Single(attempts.Where(result => result.IsSuccess));
      Assert.Single(attempts.Where(result => result.IsFailure &&
        result.Error.Code == "AuthenticationSession.RefreshFailed"));
      await using var verification = database.CreateContext(seed.TenantId);
      var session = await verification.AuthenticationSessions
        .Include(value => value.RefreshTokenRecords)
        .SingleAsync(value => value.Id == seed.AuthenticationSessionId);
      Assert.Equal(AuthenticationSessionStatus.Compromised, session.Status);
      Assert.Equal(2, session.RefreshTokenRecords.Count);
      Assert.Single(session.RefreshTokenRecords.Where(token => token.ConsumedUtc.HasValue));
      Assert.Single(session.RefreshTokenRecords.Where(token => token.RevokedUtc.HasValue));
    }
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0088")]
  [Trait("Acceptance", "AC-AUTH-0034")]
  public async Task Concurrent_selection_consumption_creates_exactly_one_session()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var seed = await CreateSelectionSeedAsync(database, clientId);
    await using var firstContext = database.CreateContext(seed.TenantId);
    await using var secondContext = database.CreateContext(seed.TenantId);
    var firstHandler = CreateSelectionHandler(database, firstContext);
    var secondHandler = CreateSelectionHandler(database, secondContext);

    var attempts = await Task.WhenAll(
      firstHandler.HandleAsync(new SelectTenantCommand(
        new SensitiveAuthenticationTokenInput(seed.RawSelectionProof), clientId, seed.TenantUserId, seed.TenantId)),
      secondHandler.HandleAsync(new SelectTenantCommand(
        new SensitiveAuthenticationTokenInput(seed.RawSelectionProof), clientId, seed.TenantUserId, seed.TenantId)));

    Assert.Single(attempts.Where(result => result.IsSuccess));
    Assert.Single(attempts.Where(result => result.IsFailure &&
      result.Error.Code == "Authentication.TenantSelectionFailed"));
    await using var verification = database.CreateContext(seed.TenantId);
    Assert.Single(await verification.AuthenticationSessions.AsNoTracking().ToArrayAsync());
    Assert.Single(await verification.RefreshTokenRecords.AsNoTracking().ToArrayAsync());
    Assert.NotNull((await verification.TenantSelectionTransactions.AsNoTracking().SingleAsync()).ConsumedUtc);
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0110")]
  [Trait("Acceptance", "AC-AUTH-0046")]
  public async Task Access_token_issuance_failure_rolls_back_login_selection_and_refresh_state()
  {
    await using var loginDatabase = await SqlTestDatabase.CreateAsync();
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var failedIssuer = new FailingAccessTokenIssuer();

    var loginSeed = await CreateSelectionSeedAsync(loginDatabase, clientId);
    await using (var loginContext = loginDatabase.CreateContext(loginSeed.TenantId))
    {
      var account = await loginContext.AuthenticationAccounts.AsNoTracking().SingleAsync();
      var tenantEligibility = new TenantAuthenticationEligibilityReadService(loginContext);
      var memberships = new IdentityTenantMembershipReadService(loginContext, tenantEligibility);
      var unitOfWork = new PlatformUnitOfWork(loginContext, new NoOpDomainEventDispatcher());
      var sessionRepository = new AuthenticationSessionRepository(loginContext);
      var tokenService = new AuthenticationTokenService();
      var policy = new AuthenticationPolicy();
      var creator = new AuthenticationSessionCreator(
        sessionRepository,
        unitOfWork,
        tokenService,
        new AccessTokenClaimsProvider(loginContext, new PlatformPermissionCatalog()),
        failedIssuer,
        policy);
      var handler = new BeginTenantAccessCommandHandler(
        new AuthenticationAccountRepository(loginContext),
        new TenantSelectionTransactionRepository(loginContext),
        memberships,
        new AuthenticationClientRegistry(Options.Create(new AuthenticationClientOptions())),
        tokenService,
        creator,
        unitOfWork,
        policy,
        loginDatabase.Clock);

      var result = await handler.HandleAsync(new BeginTenantAccessCommand(
        new VerifiedIdentity(account.IdentityId, account.SecurityVersion), clientId));

      Assert.True(result.IsFailure);
      Assert.Equal("Authentication.AccessTokenUnavailable", result.Error.Code);
    }
    await using (var loginVerification = loginDatabase.CreateContext(loginSeed.TenantId))
    {
      Assert.Empty(await loginVerification.AuthenticationSessions.AsNoTracking().ToArrayAsync());
      Assert.Empty(await loginVerification.RefreshTokenRecords.AsNoTracking().ToArrayAsync());
    }

    await using var selectionDatabase = await SqlTestDatabase.CreateAsync();
    var selectionSeed = await CreateSelectionSeedAsync(selectionDatabase, clientId);
    await using (var selectionContext = selectionDatabase.CreateContext(selectionSeed.TenantId))
    {
      var result = await CreateSelectionHandler(selectionDatabase, selectionContext, failedIssuer).HandleAsync(
        new SelectTenantCommand(new SensitiveAuthenticationTokenInput(selectionSeed.RawSelectionProof),
          clientId, selectionSeed.TenantUserId, selectionSeed.TenantId));
      Assert.True(result.IsFailure);
      Assert.Equal("Authentication.AccessTokenUnavailable", result.Error.Code);
    }
    await using (var selectionVerification = selectionDatabase.CreateContext(selectionSeed.TenantId))
    {
      var selectionPublicId = Guid.ParseExact(selectionSeed.RawSelectionProof[..32], "N");
      Assert.Null((await selectionVerification.TenantSelectionTransactions.AsNoTracking()
        .SingleAsync(value => value.PublicId == selectionPublicId)).ConsumedUtc);
      Assert.DoesNotContain(await selectionVerification.AuthenticationSessions.AsNoTracking().ToArrayAsync(),
        value => value.TenantId == selectionSeed.TenantId);
    }

    await using var refreshDatabase = await SqlTestDatabase.CreateAsync();
    var refreshSeed = await CreateRefreshSessionSeedAsync(refreshDatabase, 93, clientId);
    await using (var refreshContext = refreshDatabase.CreateContext(refreshSeed.TenantId))
    {
      var result = await CreateRefreshHandler(refreshDatabase, refreshContext, failedIssuer).HandleAsync(
        new RefreshAuthenticationSessionCommand(new SensitiveAuthenticationTokenInput(refreshSeed.RawRefreshToken), clientId));
      Assert.True(result.IsFailure);
      Assert.Equal("Authentication.AccessTokenUnavailable", result.Error.Code);
    }
    await using (var refreshVerification = refreshDatabase.CreateContext(refreshSeed.TenantId))
    {
      var session = await refreshVerification.AuthenticationSessions.Include(value => value.RefreshTokenRecords)
        .SingleAsync(value => value.Id == refreshSeed.AuthenticationSessionId);
      Assert.Equal(AuthenticationSessionStatus.Active, session.Status);
      Assert.Single(session.RefreshTokenRecords);
      Assert.Null(session.RefreshTokenRecords.Single().ConsumedUtc);
      Assert.Null(session.RefreshTokenRecords.Single().RevokedUtc);
    }
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0054")]
  [Trait("Acceptance", "AC-TEN-0030")]
  public async Task Corrupt_platform_support_assignment_is_excluded_from_tenant_access_token_claims()
  {
    // ADR-015 / DEC-TEN-0018 defense in depth: even if persistent SQL state contains an invalid
    // PlatformSupport permission assignment on a tenant role, a tenant access token must not carry it.
    // Exercises real SQL persistence, the real AccessTokenClaimsProvider, the real PlatformPermissionCatalog,
    // and (through the provider) the real TenantPermissionClaimFilter — no mocking, no manual claim construction.
    await using var database = await SqlTestDatabase.CreateAsync();
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var seed = await CreateClaimsSeedWithCorruptPlatformAssignmentAsync(database, clientId);

    await using var context = database.CreateContext(seed.TenantId);
    var claimsProvider = new AccessTokenClaimsProvider(context, new PlatformPermissionCatalog());

    var result = await claimsProvider.GetClaimsAsync(
      seed.AuthenticationSessionId,
      seed.IdentityId,
      seed.TenantUserId,
      seed.TenantId,
      clientId,
      seed.SecurityVersion);

    Assert.True(result.IsSuccess);
    // The force-seeded PlatformSupport permission must be filtered out of the tenant token claims.
    Assert.DoesNotContain(PlatformPermissionNames.ManageTenants, result.Value.Permissions);
    // The legitimate Tenant-scoped permission still survives: filtering is selective, not destructive.
    Assert.Contains(PlatformPermissionNames.ViewCompanies, result.Value.Permissions);
  }

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

  private static RefreshAuthenticationSessionCommandHandler CreateRefreshHandler(
    SqlTestDatabase database,
    PlatformDbContext context,
    IAccessTokenIssuer? accessTokenIssuer = null)
  {
    var tenantEligibility = new TenantAuthenticationEligibilityReadService(context);
    return new RefreshAuthenticationSessionCommandHandler(
      new AuthenticationAccountRepository(context),
      new AuthenticationSessionRepository(context),
      new IdentityTenantMembershipReadService(context, tenantEligibility),
      new AuthenticationClientRegistry(Options.Create(new AuthenticationClientOptions())),
      new AuthenticationTokenService(),
      new AccessTokenClaimsProvider(context, new PlatformPermissionCatalog()),
      accessTokenIssuer ?? new TestAccessTokenIssuer(),
      new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher()),
      new AuthenticationPolicy(),
      database.Clock);
  }

  private static SelectTenantCommandHandler CreateSelectionHandler(
    SqlTestDatabase database,
    PlatformDbContext context,
    IAccessTokenIssuer? accessTokenIssuer = null)
  {
    var tenantEligibility = new TenantAuthenticationEligibilityReadService(context);
    var unitOfWork = new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher());
    var sessionRepository = new AuthenticationSessionRepository(context);
    var tokenService = new AuthenticationTokenService();
    var policy = new AuthenticationPolicy();
    return new SelectTenantCommandHandler(
      new AuthenticationAccountRepository(context),
      new TenantSelectionTransactionRepository(context),
      new IdentityTenantMembershipReadService(context, tenantEligibility),
      new AuthenticationClientRegistry(Options.Create(new AuthenticationClientOptions())),
      tokenService,
      new AuthenticationSessionCreator(sessionRepository, unitOfWork, tokenService,
        new AccessTokenClaimsProvider(context, new PlatformPermissionCatalog()), accessTokenIssuer ?? new TestAccessTokenIssuer(), policy),
      unitOfWork,
      database.Clock);
  }

  private static async Task<SelectionSeed> CreateSelectionSeedAsync(
    SqlTestDatabase database,
    AuthenticationClientId clientId)
  {
    var tenant = Tenant.Create(
      TenantCode.Create("SELECTION").Value,
      TenantName.Create("Selection Tenant").Value,
      "integration-actor",
      Guid.NewGuid(),
      database.Clock.UtcNow).Value;
    long identityId;
    long accountSecurityVersion;
    await using (var globalContext = database.CreateContext(Guid.NewGuid()))
    {
      globalContext.Tenants.Add(tenant);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      Assert.True(tenant.Activate("integration-actor", Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(1)).IsSuccess);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);

      var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
      globalContext.Identities.Add(identity);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      var account = AuthenticationAccount.CreatePending(
        identity.Id,
        LoginEmail.Create("selection@example.com").Value);
      globalContext.AuthenticationAccounts.Add(account);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      identityId = identity.Id;
      accountSecurityVersion = account.SecurityVersion;
    }

    long tenantUserId;
    await using (var tenantContext = database.CreateContext(tenant.Id))
    {
      var membership = TenantUser.CreateActive(
        identityId,
        tenant.Id,
        EmailAddress.Create("selection.member@example.com").Value,
        UserDisplayName.Create("Selection Member").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      tenantContext.TenantUsers.Add(membership);
      Assert.True((await SaveAsync(tenantContext)).IsSuccess);
      tenantUserId = membership.Id;
    }

    var tokenService = new AuthenticationTokenService();
    var generated = tokenService.GenerateTenantSelectionProof(identityId, accountSecurityVersion, clientId);
    var raw = generated.SensitiveProof.RevealOnce().Value;
    await using (var authenticationContext = database.CreateContext(tenant.Id))
    {
      authenticationContext.TenantSelectionTransactions.Add(TenantSelectionTransaction.Create(
        generated.PublicId,
        identityId,
        clientId.Value,
        accountSecurityVersion,
        generated.SecretHash,
        database.Clock.UtcNow,
        database.Clock.UtcNow.AddMinutes(5),
        Guid.NewGuid()));
      Assert.True((await SaveAsync(authenticationContext)).IsSuccess);
    }

    return new SelectionSeed(tenant.Id, tenantUserId, raw);
  }

  private static async Task<RefreshSessionSeed> CreateRefreshSessionSeedAsync(
    SqlTestDatabase database,
    int iteration,
    AuthenticationClientId clientId)
  {
    var tenant = Tenant.Create(
      TenantCode.Create($"AUTH{iteration}").Value,
      TenantName.Create($"Authentication Tenant {iteration}").Value,
      "integration-actor",
      Guid.NewGuid(),
      database.Clock.UtcNow).Value;
    long identityId;
    long accountSecurityVersion;
    await using (var globalContext = database.CreateContext(Guid.NewGuid()))
    {
      globalContext.Tenants.Add(tenant);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      Assert.True(tenant.Activate("integration-actor", Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(1)).IsSuccess);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);

      var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
      globalContext.Identities.Add(identity);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      var account = AuthenticationAccount.CreatePending(
        identity.Id,
        LoginEmail.Create($"refresh.{iteration}@example.com").Value);
      globalContext.AuthenticationAccounts.Add(account);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      identityId = identity.Id;
      accountSecurityVersion = account.SecurityVersion;
    }

    long tenantUserId;
    await using (var tenantContext = database.CreateContext(tenant.Id))
    {
      var membership = TenantUser.CreateActive(
        identityId,
        tenant.Id,
        EmailAddress.Create($"refresh.member.{iteration}@example.com").Value,
        UserDisplayName.Create($"Refresh Member {iteration}").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      tenantContext.TenantUsers.Add(membership);
      Assert.True((await SaveAsync(tenantContext)).IsSuccess);
      tenantUserId = membership.Id;
    }

    await using var authenticationContext = database.CreateContext(tenant.Id);
    var session = AuthenticationSession.Create(
      identityId,
      tenantUserId,
      tenant.Id,
      clientId.Value,
      Guid.NewGuid(),
      accountSecurityVersion,
      database.Clock.UtcNow,
      database.Clock.UtcNow.AddDays(30),
      database.Clock.UtcNow.AddDays(90));
    authenticationContext.AuthenticationSessions.Add(session);
    Assert.True((await SaveAsync(authenticationContext)).IsSuccess);
    var tokenService = new AuthenticationTokenService();
    var generated = tokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, clientId);
    var raw = generated.SensitiveToken.RevealOnce().Value;
    session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, database.Clock.UtcNow, Guid.NewGuid());
    Assert.True((await SaveAsync(authenticationContext)).IsSuccess);
    return new RefreshSessionSeed(tenant.Id, session.Id, raw);
  }

  private static async Task<ClaimsCorruptionSeed> CreateClaimsSeedWithCorruptPlatformAssignmentAsync(
    SqlTestDatabase database,
    AuthenticationClientId clientId)
  {
    var tenant = Tenant.Create(
      TenantCode.Create("CLAIMFILTER").Value,
      TenantName.Create("Claim Filter Tenant").Value,
      "integration-actor",
      Guid.NewGuid(),
      database.Clock.UtcNow).Value;

    long identityId;
    long accountSecurityVersion;
    await using (var globalContext = database.CreateContext(Guid.NewGuid()))
    {
      globalContext.Tenants.Add(tenant);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      Assert.True(tenant.Activate("integration-actor", Guid.NewGuid(), database.Clock.UtcNow.AddMinutes(1)).IsSuccess);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);

      var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
      globalContext.Identities.Add(identity);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      var account = AuthenticationAccount.CreatePending(
        identity.Id,
        LoginEmail.Create("claim.filter@example.com").Value);
      globalContext.AuthenticationAccounts.Add(account);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(globalContext)).IsSuccess);
      identityId = identity.Id;
      accountSecurityVersion = account.SecurityVersion;
    }

    long tenantUserId;
    long roleId;
    await using (var tenantContext = database.CreateContext(tenant.Id))
    {
      // Role carries a legitimate Tenant-scoped permission through the real domain guard.
      var role = Role.CreateCustom(
        tenant.Id,
        RoleName.Create("Claim Filter Role").Value,
        null,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      var catalog = new PlatformPermissionCatalog();
      Assert.True(catalog.TryGet(PlatformPermissionNames.ViewCompanies, out var companiesView));
      Assert.True(role.AssignPermission(companiesView, "integration-actor", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      tenantContext.Roles.Add(role);
      Assert.True((await SaveAsync(tenantContext)).IsSuccess);
      roleId = role.Id;

      var membership = TenantUser.CreateActive(
        identityId,
        tenant.Id,
        EmailAddress.Create("claim.filter.member@example.com").Value,
        UserDisplayName.Create("Claim Filter Member").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      Assert.True(membership.AssignRole(role, "integration-actor", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      tenantContext.TenantUsers.Add(membership);
      Assert.True((await SaveAsync(tenantContext)).IsSuccess);
      tenantUserId = membership.Id;
    }

    long authenticationSessionId;
    await using (var authenticationContext = database.CreateContext(tenant.Id))
    {
      var session = AuthenticationSession.Create(
        identityId,
        tenantUserId,
        tenant.Id,
        clientId.Value,
        Guid.NewGuid(),
        accountSecurityVersion,
        database.Clock.UtcNow,
        database.Clock.UtcNow.AddDays(30),
        database.Clock.UtcNow.AddDays(90));
      authenticationContext.AuthenticationSessions.Add(session);
      Assert.True((await SaveAsync(authenticationContext)).IsSuccess);
      authenticationSessionId = session.Id;
    }

    // Force-seed a corrupt PlatformSupport assignment straight into SQL, bypassing Role.AssignPermission
    // (which rejects non-Tenant scopes). This models corrupt/manual database state that production writes cannot create.
    await using (var corruptionContext = database.CreateContext(tenant.Id))
    {
      await corruptionContext.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[RolePermissionAssignments] ([TenantId], [RoleId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({tenant.Id}, {roleId}, {PlatformPermissionNames.ManageTenants}, {database.Clock.UtcNow}, {"corruption-test"})");
    }

    return new ClaimsCorruptionSeed(tenant.Id, identityId, tenantUserId, authenticationSessionId, accountSecurityVersion);
  }

  private sealed record ClaimsCorruptionSeed(
    Guid TenantId,
    long IdentityId,
    long TenantUserId,
    long AuthenticationSessionId,
    long SecurityVersion);

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

  private sealed class SqlTestDatabase : IAsyncDisposable
  {
    private readonly string connectionString;

    private SqlTestDatabase(string connectionString)
    {
      this.connectionString = connectionString;
      ConnectionString = connectionString;
    }

    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));
    public string ConnectionString { get; }

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

  private sealed record RefreshSessionSeed(Guid TenantId, long AuthenticationSessionId, string RawRefreshToken);

  private sealed record SelectionSeed(Guid TenantId, long TenantUserId, string RawSelectionProof);

  private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0112")]
  [Trait("Scenario", "TS-AUTH-0118")]
  [Trait("Acceptance", "AC-AUTH-0047")]
  public async Task Logout_racing_refresh_serializes_and_leaves_no_usable_refresh_token()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var seed = await CreateRefreshSessionSeedAsync(database, 92, clientId);
    CurrentAuthenticationSession current;
    await using (var readContext = database.CreateContext(seed.TenantId))
    {
      var session = await readContext.AuthenticationSessions.AsNoTracking()
        .SingleAsync(value => value.Id == seed.AuthenticationSessionId);
      var account = await readContext.AuthenticationAccounts.AsNoTracking()
        .SingleAsync(value => value.IdentityId == session.IdentityId);
      current = new CurrentAuthenticationSession(
        session.IdentityId,
        session.TenantId,
        session.TenantUserId,
        session.Id,
        clientId,
        account.SecurityVersion);
    }

    await using var refreshContext = database.CreateContext(seed.TenantId);
    await using var logoutContext = database.CreateContext(seed.TenantId);
    var refresh = CreateRefreshHandler(database, refreshContext);
    var logout = new RevokeCurrentAuthenticationSessionCommandHandler(
      new FixedCurrentAuthenticationSession(current),
      new AuthenticationAccountRepository(logoutContext),
      new AuthenticationSessionRepository(logoutContext),
      new PlatformUnitOfWork(logoutContext, new NoOpDomainEventDispatcher()),
      database.Clock);

    var refreshTask = refresh.HandleAsync(new RefreshAuthenticationSessionCommand(
      new SensitiveAuthenticationTokenInput(seed.RawRefreshToken), clientId));
    var logoutTask = logout.HandleAsync(new RevokeCurrentAuthenticationSessionCommand());
    await Task.WhenAll(refreshTask, logoutTask);
    var refreshResult = await refreshTask;
    var logoutResult = await logoutTask;

    Assert.True(logoutResult.IsSuccess);
    Assert.True(refreshResult.IsSuccess ||
      refreshResult.Error.Code == "AuthenticationSession.RefreshFailed");
    await using var verification = database.CreateContext(seed.TenantId);
    var final = await verification.AuthenticationSessions.Include(value => value.RefreshTokenRecords)
      .SingleAsync(value => value.Id == seed.AuthenticationSessionId);
    Assert.Equal(AuthenticationSessionStatus.Revoked, final.Status);
    Assert.Equal(AuthenticationSessionRevocationReason.UserLogout, final.RevocationReason);
    Assert.DoesNotContain(final.RefreshTokenRecords, token => token.IsActive(database.Clock.UtcNow));
  }

  [Fact]
  [Trait("Scenario", "TS-AUTH-0112")]
  [Trait("Scenario", "TS-AUTH-0118")]
  public async Task Concurrent_http_refresh_and_logout_use_validated_transport_and_sql_serialization()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var clientId = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var seed = await CreateRefreshSessionSeedAsync(database, 94, clientId);
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:Platform"] = database.ConnectionString,
      ["Jwt:Issuer"] = "https://integration.ssas.local",
      ["Jwt:Audience"] = "ssas-integration",
      ["Jwt:AccessTokenLifetime"] = "00:15:00",
      ["Jwt:ClockSkewSeconds"] = "30",
      ["Jwt:MaximumEncodedTokenSize"] = "8192",
      ["AuthenticationTransport:AllowedOrigins:0"] = "https://app.integration.test",
      ["AuthenticationTransport:ProxyMode"] = "Direct",
      ["Authentication:CompromisedPasswords:Enabled"] = "false"
    });
    builder.Services
      .AddPlatformRequestContext()
      .AddPlatformInfrastructure(builder.Configuration)
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostAuthenticationTransport(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostProblemDetails()
      .AddPlatformModule();
    await using var application = builder.Build();
    application.UseExceptionHandler();
    application.UseCors(AuthenticationTransportServiceCollectionExtensions.CorsPolicy);
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformAuthenticationEndpoints();
    await application.StartAsync();

    string accessToken;
    string csrfValue;
    await using (var scope = application.Services.CreateAsyncScope())
    {
      var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
      var session = await context.AuthenticationSessions.AsNoTracking()
        .Include(value => value.RefreshTokenRecords)
        .SingleAsync(value => value.Id == seed.AuthenticationSessionId);
      var account = await context.AuthenticationAccounts.AsNoTracking()
        .SingleAsync(value => value.IdentityId == session.IdentityId);
      var claims = await scope.ServiceProvider.GetRequiredService<IAccessTokenClaimsProvider>().GetClaimsAsync(
        session.Id,
        session.IdentityId,
        session.TenantUserId,
        session.TenantId,
        clientId,
        account.SecurityVersion);
      Assert.True(claims.IsSuccess);
      var issued = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>().Issue(claims.Value, DateTimeOffset.UtcNow);
      Assert.True(issued.IsSuccess);
      accessToken = issued.Value.AccessToken.RevealOnce().Value;
      csrfValue = scope.ServiceProvider.GetRequiredService<AuthenticationCsrfService>().Create(
        seed.RawRefreshToken,
        session.Id,
        session.RefreshTokenRecords.Single().ExpiresUtc);
    }

    using var client = application.GetTestClient();
    using var refreshRequest = AuthenticationRequest("refresh", seed.RawRefreshToken, csrfValue);
    using var logoutRequest = AuthenticationRequest("logout", seed.RawRefreshToken, csrfValue);
    logoutRequest.Headers.Authorization = new("Bearer", accessToken);
    var refreshTask = client.SendAsync(refreshRequest);
    var logoutTask = client.SendAsync(logoutRequest);
    await Task.WhenAll(refreshTask, logoutTask);
    using var refreshResponse = await refreshTask;
    using var logoutResponse = await logoutTask;

    Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
    Assert.Contains(refreshResponse.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
    Assert.Equal("no-store", logoutResponse.Headers.CacheControl?.ToString());
    Assert.Equal("no-store", refreshResponse.Headers.CacheControl?.ToString());
    await using var verification = database.CreateContext(seed.TenantId);
    var final = await verification.AuthenticationSessions.Include(value => value.RefreshTokenRecords)
      .SingleAsync(value => value.Id == seed.AuthenticationSessionId);
    Assert.Equal(AuthenticationSessionStatus.Revoked, final.Status);
    Assert.Equal(AuthenticationSessionRevocationReason.UserLogout, final.RevocationReason);
    Assert.DoesNotContain(final.RefreshTokenRecords, token => token.IsActive(database.Clock.UtcNow));
  }

  private static HttpRequestMessage AuthenticationRequest(string operation, string refreshToken, string csrfValue)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, $"https://localhost/api/platform/auth/{operation}");
    request.Headers.Add("Origin", "https://app.integration.test");
    request.Headers.Add(AuthenticationCsrfService.HeaderName, csrfValue);
    request.Headers.TryAddWithoutValidation("Cookie",
      $"{AuthenticationEndpointRouteBuilderExtensions.RefreshCookieName}={refreshToken}; {AuthenticationCsrfService.CookieName}={csrfValue}");
    return request;
  }

  private sealed class TestAccessTokenIssuer : IAccessTokenIssuer
  {
    public Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Success(new IssuedAccessToken(new SensitiveAccessToken("integration-access-token"), issuedUtc.AddMinutes(15)));

    public Result<IssuedAccessToken> Issue(PlatformAccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Success(new IssuedAccessToken(new SensitiveAccessToken("integration-platform-access-token"), issuedUtc.AddMinutes(15)));
  }

  private sealed class FailingAccessTokenIssuer : IAccessTokenIssuer
  {
    public Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Failure<IssuedAccessToken>(AuthenticationErrors.AccessTokenIssuanceUnavailable);

    public Result<IssuedAccessToken> Issue(PlatformAccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Failure<IssuedAccessToken>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
  }

  private sealed class FixedCurrentAuthenticationSession(CurrentAuthenticationSession value)
    : ICurrentAuthenticationSession
  {
    public CurrentAuthenticationSession? Value { get; } = value;
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

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

// Phase 2 platform-support authority SQL verification (ADR-015 / DEC-TEN-0018).
public sealed class PlatformSupportAuthoritySqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Migration_creates_platform_support_authority_tables_with_expected_shape()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());

    var principalEntity = context.Model.FindEntityType(typeof(PlatformSupportPrincipal));
    Assert.NotNull(principalEntity);
    Assert.Null(principalEntity!.GetQueryFilter());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(PlatformSupportPrincipal).GetInterfaces());
    Assert.True(principalEntity.FindProperty(nameof(PlatformSupportPrincipal.RowVersion))?.IsConcurrencyToken);

    Assert.Equal(
      ["PlatformSupportPrincipalId", "IdentityId", "RowVersion", "CreatedUtc", "ModifiedUtc", "CreatedBy", "ModifiedBy", "Status", "StatusChangedBy", "StatusChangedUtc"],
      await ReadStringsAsync(
        context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'PlatformSupportPrincipals' ORDER BY ORDINAL_POSITION"));
    Assert.Equal(
      ["PlatformPermissionAssignmentId", "PlatformSupportPrincipalId", "PermissionName", "AssignedUtc", "AssignedBy", "RemovedUtc", "RemovedBy"],
      await ReadStringsAsync(
        context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'PlatformPermissionAssignments' ORDER BY ORDINAL_POSITION"));

    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(
      context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'PlatformPermissionAssignments' AND COLUMN_NAME = 'PermissionName'"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[PlatformSupportPrincipals]') AND name = N'UX_PlatformSupportPrincipals_IdentityId' AND is_unique = 1"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[PlatformPermissionAssignments]') AND name = N'UX_PlatformPermissionAssignments_Principal_Permission' AND is_unique = 1 AND has_filter = 1"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[platform].[PlatformSupportPrincipals]') AND referenced_object_id = OBJECT_ID(N'[platform].[Identities]')"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Authority_grant_and_revoke_round_trip_through_the_real_provider_and_leave_tenant_tables_untouched()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      var result = await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId));
      Assert.True(result.IsSuccess);
      principalId = result.Value;
    }

    await using (var context = database.CreateContext())
    {
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewTenants))).IsSuccess);

      // Duplicate active grant is a conflict (enforced by the unique filtered index → mapped error).
      var duplicate = await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants));
      Assert.True(duplicate.IsFailure);
    }

    await using (var context = database.CreateContext())
    {
      var read = new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog());
      var permissions = await read.GetActivePermissionsAsync(principalId);
      Assert.Equal([PlatformPermissionNames.ManageTenants, PlatformPermissionNames.ViewTenants], permissions);
    }

    await using (var context = database.CreateContext())
    {
      var revoke = new RevokePlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await revoke.HandleAsync(new RevokePlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    await using (var context = database.CreateContext())
    {
      var read = new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog());
      Assert.Equal([PlatformPermissionNames.ViewTenants], await read.GetActivePermissionsAsync(principalId));
      // The revoked grant is retained (history), not physically removed.
      Assert.Equal(2, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE PlatformSupportPrincipalId = {principalId}"));
      // Tenant authority tables are entirely unaffected by platform-support authority operations.
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[Roles]"));
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[RolePermissionAssignments]"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Tenant_scoped_permission_grant_is_rejected_before_persistence()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
    }

    await using (var context = database.CreateContext())
    {
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      var tenant = await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewCompanies));
      Assert.True(tenant.IsFailure);
      var unknown = await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, "Platform.Unknown.Thing"));
      Assert.True(unknown.IsFailure);
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(0, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE PlatformSupportPrincipalId = {principalId}"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Corrupt_non_platform_support_assignment_is_excluded_from_authority_reads()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewTenants))).IsSuccess);
    }

    // Force-seed a corrupt Tenant-scoped permission directly into SQL, bypassing the write-side guard.
    await using (var context = database.CreateContext())
    {
      await context.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {PlatformPermissionNames.ViewCompanies}, {PlatformSupportSqlDatabase.Now}, {"corruption-test"})");
    }

    await using (var context = database.CreateContext())
    {
      var read = new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog());
      var permissions = await read.GetActivePermissionsAsync(principalId);
      Assert.DoesNotContain(PlatformPermissionNames.ViewCompanies, permissions);
      Assert.Contains(PlatformPermissionNames.ViewTenants, permissions);
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Duplicate_active_assignment_violates_database_uniqueness()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    await using (var context = database.CreateContext())
    {
      await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {PlatformPermissionNames.ManageTenants}, {PlatformSupportSqlDatabase.Now}, {"race"})"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Physical_deletion_of_a_platform_support_principal_is_rejected()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
    }

    // Childless principal: no assignment rows exist, so FK Restrict cannot cause the failure — the
    // DbContext physical-delete guard must, before any SQL DELETE is issued (InvalidOperationException,
    // not SqlException).
    await using (var context = database.CreateContext())
    {
      var principal = await context.PlatformSupportPrincipals.SingleAsync(item => item.Id == principalId);
      context.PlatformSupportPrincipals.Remove(principal);
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE PlatformSupportPrincipalId = {principalId}"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Physical_deletion_of_a_platform_permission_assignment_is_rejected()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    // Active assignment: physical delete must be rejected by the guard. Revocation stays valid because
    // it is an UPDATE (RemovedUtc/RemovedBy), not a DELETE.
    await using (var context = database.CreateContext())
    {
      var assignment = await context.PlatformPermissionAssignments.SingleAsync();
      context.PlatformPermissionAssignments.Remove(assignment);
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE PlatformSupportPrincipalId = {principalId}"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0020")]
  public async Task Status_migration_creates_expected_lifecycle_columns_and_check_constraint()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    // The 'Active' default that backfills pre-existing rows is proven functionally by
    // Existing_principal_backfills_to_active_with_null_status_metadata.
    Assert.Equal("NO", await ReadStringAsync(
      context,
      "SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformSupportPrincipals' AND COLUMN_NAME='Status'"));
    Assert.Equal("YES", await ReadStringAsync(
      context,
      "SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformSupportPrincipals' AND COLUMN_NAME='StatusChangedUtc'"));
    Assert.Equal("YES", await ReadStringAsync(
      context,
      "SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformSupportPrincipals' AND COLUMN_NAME='StatusChangedBy'"));
    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(
      context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='platform' AND TABLE_NAME='PlatformSupportPrincipals' AND COLUMN_NAME='Status'"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[platform].[PlatformSupportPrincipals]') AND name = N'CK_PlatformSupportPrincipals_Status'"));
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0081")]
  [Trait("Scenario", "TS-TEN-0082")]
  public async Task Existing_principal_backfills_to_active_with_null_status_metadata()
  {
    // Bring the database only up to the Phase-2 authority migration (before the Status column exists).
    await using var database = await PlatformSupportSqlDatabase.CreateAsync(migrate: false);
    await using (var pre = database.CreateContext())
    {
      await pre.GetService<IMigrator>().MigrateAsync(PlatformSupportSqlDatabase.PlatformSupportAuthorityMigration);
    }

    // Insert an Identity and a principal at the pre-lifecycle schema via raw SQL (the entity's Status
    // property does not yet have a column, so EF cannot be used here).
    await using (var seed = database.CreateContext())
    {
      await seed.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[Identities] ([Subject], [CreatedUtc], [ModifiedUtc]) VALUES ({$"local:{Guid.NewGuid():N}"}, {PlatformSupportSqlDatabase.Now}, {PlatformSupportSqlDatabase.Now})");
      await seed.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformSupportPrincipals] ([IdentityId], [CreatedUtc], [ModifiedUtc]) SELECT [IdentityId], {PlatformSupportSqlDatabase.Now}, {PlatformSupportSqlDatabase.Now} FROM [platform].[Identities]");
    }

    // Apply the lifecycle migration to the now non-empty table.
    await using (var upgrade = database.CreateContext())
    {
      await upgrade.GetService<IMigrator>().MigrateAsync();
    }

    await using (var verify = database.CreateContext())
    {
      Assert.Equal("Active", await ReadStringAsync(verify, "SELECT [Status] FROM [platform].[PlatformSupportPrincipals]"));
      Assert.Equal(1, await ReadInt32Async(
        verify,
        "SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE [StatusChangedUtc] IS NULL AND [StatusChangedBy] IS NULL"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0020")]
  public async Task Status_check_constraint_rejects_an_undefined_value()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);
    await using var context = database.CreateContext();

    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
      $"INSERT INTO [platform].[PlatformSupportPrincipals] ([IdentityId], [Status], [CreatedUtc], [ModifiedUtc]) VALUES ({identityId}, {"Suspended"}, {PlatformSupportSqlDatabase.Now}, {PlatformSupportSqlDatabase.Now})"));
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0083")]
  [Trait("Scenario", "TS-TEN-0084")]
  public async Task Disable_and_reenable_round_trip_through_the_real_handlers()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    byte[] version;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    await using (var read = database.CreateContext())
    {
      var principal = await read.PlatformSupportPrincipals.AsNoTracking().SingleAsync();
      version = principal.RowVersion;
    }

    await using (var context = database.CreateContext())
    {
      var disable = new DisablePlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformAuthenticationSessionRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await disable.HandleAsync(new DisablePlatformSupportPrincipalCommand(principalId, version))).IsSuccess);
    }

    await using (var verify = database.CreateContext())
    {
      var principal = await verify.PlatformSupportPrincipals.Include(p => p.PermissionAssignments).AsNoTracking().SingleAsync();
      Assert.Equal(SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Disabled, principal.Status);
      Assert.NotNull(principal.StatusChangedUtc);
      Assert.Equal("integration-actor", principal.StatusChangedBy);
      Assert.False(version.SequenceEqual(principal.RowVersion));
      Assert.Single(principal.PermissionAssignments.Where(a => a.IsActive));
      version = principal.RowVersion;
    }

    await using (var context = database.CreateContext())
    {
      var reenable = new ReenablePlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await reenable.HandleAsync(new ReenablePlatformSupportPrincipalCommand(principalId, version))).IsSuccess);
    }

    await using (var verify = database.CreateContext())
    {
      var principal = await verify.PlatformSupportPrincipals.Include(p => p.PermissionAssignments).AsNoTracking().SingleAsync();
      Assert.Equal(SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Active, principal.Status);
      Assert.NotNull(principal.StatusChangedUtc);
      Assert.Single(principal.PermissionAssignments.Where(a => a.IsActive));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0020")]
  public async Task Grant_is_rejected_and_revoke_is_allowed_while_disabled()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    byte[] version;
    await using (var read = database.CreateContext())
    {
      version = (await read.PlatformSupportPrincipals.AsNoTracking().SingleAsync()).RowVersion;
    }

    await using (var context = database.CreateContext())
    {
      var disable = new DisablePlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformAuthenticationSessionRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await disable.HandleAsync(new DisablePlatformSupportPrincipalCommand(principalId, version))).IsSuccess);
    }

    await using (var context = database.CreateContext())
    {
      // Grant is rejected while Disabled.
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewTenants))).IsFailure);

      // Revoke remains allowed while Disabled.
      var revoke = new RevokePlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await revoke.HandleAsync(new RevokePlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    await using (var verify = database.CreateContext())
    {
      var principal = await verify.PlatformSupportPrincipals.Include(p => p.PermissionAssignments).AsNoTracking().SingleAsync();
      Assert.Equal(SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Disabled, principal.Status);
      Assert.Empty(principal.PermissionAssignments.Where(a => a.IsActive));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0020")]
  public async Task Stale_rowversion_on_a_lifecycle_transition_is_a_concurrency_conflict()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    byte[] staleVersion;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
    }

    await using (var read = database.CreateContext())
    {
      staleVersion = (await read.PlatformSupportPrincipals.AsNoTracking().SingleAsync()).RowVersion;
    }

    // A successful Disable advances the RowVersion, invalidating the captured version.
    await using (var context = database.CreateContext())
    {
      var disable = new DisablePlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformAuthenticationSessionRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await disable.HandleAsync(new DisablePlatformSupportPrincipalCommand(principalId, staleVersion))).IsSuccess);
    }

    await using (var context = database.CreateContext())
    {
      var reenable = new ReenablePlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      var result = await reenable.HandleAsync(new ReenablePlatformSupportPrincipalCommand(principalId, staleVersion));
      Assert.True(result.IsFailure);
      Assert.Equal("Persistence.ConcurrencyConflict", result.Error.Code);
    }
  }

  // ---- Phase 4C: platform authority read/query surface (DEC-TEN-0025), exercised through the real handlers ----

  [Fact]
  [Trait("Decision", "DEC-TEN-0025")]
  public async Task Principal_list_returns_active_and_disabled_paginated_deterministically_with_stable_paging()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var principalA = await RegisterPrincipalAsync(database);
    var principalB = await RegisterPrincipalAsync(database);
    var principalC = await RegisterPrincipalAsync(database);
    await DisablePrincipalDirectAsync(database, principalC); // Disabled principals remain listable.

    await using var context = database.CreateContext();
    var handler = new ListPlatformSupportPrincipalsQueryHandler(
      new PlatformSupportAuthorityReadService(context), new TestCurrentUser());

    var page1 = await handler.HandleAsync(new ListPlatformSupportPrincipalsQuery(1, 2));
    var page2 = await handler.HandleAsync(new ListPlatformSupportPrincipalsQuery(2, 2));

    Assert.True(page1.IsSuccess);
    Assert.Equal(3, page1.Value.TotalCount);
    Assert.Equal(2, page1.Value.TotalPages);
    // Deterministic ORDER BY Id: page 1 = {A,B}, page 2 = {C}; no duplication/skip across the boundary.
    Assert.Equal([principalA, principalB], page1.Value.Items.Select(item => item.PlatformSupportPrincipalId));
    Assert.Equal([principalC], page2.Value.Items.Select(item => item.PlatformSupportPrincipalId));
    Assert.Equal(
      SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Disabled,
      page2.Value.Items.Single().Status);
    Assert.All(page1.Value.Items, item => Assert.NotEmpty(item.RowVersion));

    // Invalid paging is rejected by the handler; oversized page size is bounded.
    Assert.True((await handler.HandleAsync(new ListPlatformSupportPrincipalsQuery(0, 10))).IsFailure);
    Assert.True((await handler.HandleAsync(new ListPlatformSupportPrincipalsQuery(1, 0))).IsFailure);
    Assert.True((await handler.HandleAsync(new ListPlatformSupportPrincipalsQuery(1, 101))).IsFailure);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0025")]
  public async Task Get_principal_returns_authority_metadata_and_missing_principal_is_not_found()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var principalId = await RegisterPrincipalAsync(database);

    await using var context = database.CreateContext();
    var handler = new GetPlatformSupportPrincipalQueryHandler(
      new PlatformSupportAuthorityReadService(context), new TestCurrentUser());

    var found = await handler.HandleAsync(new GetPlatformSupportPrincipalQuery(principalId));
    Assert.True(found.IsSuccess);
    Assert.Equal(principalId, found.Value.PlatformSupportPrincipalId);
    Assert.True(found.Value.IdentityId > 0);
    Assert.Equal(SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Active, found.Value.Status);
    Assert.NotEmpty(found.Value.RowVersion);

    var missing = await handler.HandleAsync(new GetPlatformSupportPrincipalQuery(principalId + 987));
    Assert.True(missing.IsFailure);
    Assert.Equal("PlatformSupport.PrincipalNotFound", missing.Error.Code);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0025")]
  public async Task Assignment_history_includes_active_and_revoked_records_with_audit_metadata_ordered_by_recency()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var principalId = await RegisterPrincipalAsync(database);
    await GrantAsync(database, principalId, PlatformPermissionNames.ManageTenants);
    await GrantAsync(database, principalId, PlatformPermissionNames.ViewTenants);
    await RevokeAsync(database, principalId, PlatformPermissionNames.ManageTenants);

    await using var context = database.CreateContext();
    var handler = new ListPlatformPermissionAssignmentsQueryHandler(
      new PlatformSupportAuthorityReadService(context), new TestCurrentUser());

    var result = await handler.HandleAsync(new ListPlatformPermissionAssignmentsQuery(principalId));

    Assert.True(result.IsSuccess);
    Assert.Equal(2, result.Value.Count); // both the active and the revoked assignment are retained history
    var manage = result.Value.Single(item => item.PermissionName == PlatformPermissionNames.ManageTenants);
    Assert.False(manage.IsActive);
    Assert.NotNull(manage.RemovedUtc);
    Assert.Equal("integration-actor", manage.RemovedBy);
    Assert.Equal("integration-actor", manage.AssignedBy);
    var view = result.Value.Single(item => item.PermissionName == PlatformPermissionNames.ViewTenants);
    Assert.True(view.IsActive);
    Assert.Null(view.RemovedUtc);
    // Same AssignedUtc (fixed test clock) ⇒ stable Id-descending tie-breaker; the later-granted ViewTenants is first.
    Assert.Equal(view.PlatformPermissionAssignmentId, result.Value[0].PlatformPermissionAssignmentId);
    Assert.True(result.Value[0].PlatformPermissionAssignmentId > result.Value[1].PlatformPermissionAssignmentId);

    // Missing principal ⇒ not-found (distinct from an empty history).
    var missing = await handler.HandleAsync(new ListPlatformPermissionAssignmentsQuery(principalId + 987));
    Assert.True(missing.IsFailure);
    Assert.Equal("PlatformSupport.PrincipalNotFound", missing.Error.Code);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0025")]
  public async Task Active_projection_is_catalog_filtered_while_history_retains_revoked_tenant_scoped_and_retired_rows()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var principalId = await RegisterPrincipalAsync(database);
    await GrantAsync(database, principalId, PlatformPermissionNames.ViewTenants);
    await GrantAsync(database, principalId, PlatformPermissionNames.ManageTenants);
    await RevokeAsync(database, principalId, PlatformPermissionNames.ManageTenants);

    // Force-seed rows the write-side guard would reject: a Tenant-scoped permission and a since-retired
    // permission no longer in the catalog. They are persisted authority history but not effective authority.
    await using (var seed = database.CreateContext())
    {
      await seed.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {PlatformPermissionNames.ViewCompanies}, {PlatformSupportSqlDatabase.Now}, {"corruption-test"})");
      await seed.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {"Platform.Some.RetiredPermission"}, {PlatformSupportSqlDatabase.Now}, {"legacy-actor"})");
    }

    await using var context = database.CreateContext();
    var active = await new GetActivePlatformSupportPermissionsQueryHandler(
      new PlatformSupportAuthorityReadService(context),
      new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog()),
      new TestCurrentUser()).HandleAsync(new GetActivePlatformSupportPermissionsQuery(principalId));

    Assert.True(active.IsSuccess);
    // Effective now = only active, catalog-valid PlatformSupport permissions.
    Assert.Equal([PlatformPermissionNames.ViewTenants], active.Value);
    Assert.DoesNotContain(PlatformPermissionNames.ManageTenants, active.Value); // revoked
    Assert.DoesNotContain(PlatformPermissionNames.ViewCompanies, active.Value);  // tenant-scoped
    Assert.DoesNotContain("Platform.Some.RetiredPermission", active.Value);      // not in catalog

    var history = await new ListPlatformPermissionAssignmentsQueryHandler(
      new PlatformSupportAuthorityReadService(context), new TestCurrentUser())
      .HandleAsync(new ListPlatformPermissionAssignmentsQuery(principalId));

    Assert.True(history.IsSuccess);
    var names = history.Value.Select(item => item.PermissionName).ToArray();
    // History keeps every persisted row, including the revoked, tenant-scoped, and retired ones.
    Assert.Contains(PlatformPermissionNames.ManageTenants, names);
    Assert.Contains(PlatformPermissionNames.ViewCompanies, names);
    Assert.Contains("Platform.Some.RetiredPermission", names);
    Assert.Equal(4, history.Value.Count);

    // Effective-permissions read for a missing principal is not-found.
    await using var missingContext = database.CreateContext();
    var missing = await new GetActivePlatformSupportPermissionsQueryHandler(
      new PlatformSupportAuthorityReadService(missingContext),
      new PlatformSupportPermissionReadService(missingContext, new PlatformPermissionCatalog()),
      new TestCurrentUser()).HandleAsync(new GetActivePlatformSupportPermissionsQuery(principalId + 987));
    Assert.True(missing.IsFailure);
    Assert.Equal("PlatformSupport.PrincipalNotFound", missing.Error.Code);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0025")]
  public async Task Disabled_principal_remains_fully_readable_and_reads_touch_no_tenant_tables()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var principalId = await RegisterPrincipalAsync(database);
    await GrantAsync(database, principalId, PlatformPermissionNames.ViewTenants);
    await DisablePrincipalDirectAsync(database, principalId);

    await using var context = database.CreateContext();
    var authorityRead = new PlatformSupportAuthorityReadService(context);

    var principal = await new GetPlatformSupportPrincipalQueryHandler(authorityRead, new TestCurrentUser())
      .HandleAsync(new GetPlatformSupportPrincipalQuery(principalId));
    Assert.True(principal.IsSuccess);
    Assert.Equal(SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Disabled, principal.Value.Status);
    Assert.NotNull(principal.Value.StatusChangedUtc);

    // History and retained active assignments remain visible for a Disabled principal.
    var history = await new ListPlatformPermissionAssignmentsQueryHandler(authorityRead, new TestCurrentUser())
      .HandleAsync(new ListPlatformPermissionAssignmentsQuery(principalId));
    Assert.True(history.IsSuccess);
    Assert.Single(history.Value);
    Assert.True(history.Value.Single().IsActive);

    // The reads never depend on tenant data: tenant authority tables are empty and untouched.
    Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[Tenants]"));
    Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[Roles]"));
  }

  private static async Task<long> RegisterPrincipalAsync(PlatformSupportSqlDatabase database)
  {
    var identityId = await SeedIdentityAsync(database);
    await using var context = database.CreateContext();
    var register = new RegisterPlatformSupportPrincipalCommandHandler(
      new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
    return (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
  }

  private static async Task GrantAsync(PlatformSupportSqlDatabase database, long principalId, string permissionName)
  {
    await using var context = database.CreateContext();
    var grant = new GrantPlatformPermissionCommandHandler(
      new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
    Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, permissionName))).IsSuccess);
  }

  private static async Task RevokeAsync(PlatformSupportSqlDatabase database, long principalId, string permissionName)
  {
    await using var context = database.CreateContext();
    var revoke = new RevokePlatformPermissionCommandHandler(
      new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
    Assert.True((await revoke.HandleAsync(new RevokePlatformPermissionCommand(principalId, permissionName))).IsSuccess);
  }

  private static async Task DisablePrincipalDirectAsync(PlatformSupportSqlDatabase database, long principalId)
  {
    await using var context = database.CreateContext();
    var principal = await context.PlatformSupportPrincipals.SingleAsync(item => item.Id == principalId);
    Assert.True(principal.Disable("seed", PlatformSupportSqlDatabase.Now).IsSuccess);
    Assert.True((await Uow(context).SaveChangesAsync()).IsSuccess);
  }

  private static TestPlatformUnitOfWork Uow(PlatformDbContext context) => new(context);

  private static async Task<long> SeedIdentityAsync(PlatformSupportSqlDatabase database)
  {
    await using var context = database.CreateContext();
    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return identity.Id;
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

  // Thin PlatformUnitOfWork wrapper so tests can reuse the production unit of work without a DI container.
  private sealed class TestPlatformUnitOfWork(PlatformDbContext context)
    : SSAS.Platform.Application.Abstractions.Persistence.IPlatformUnitOfWork
  {
    private readonly PlatformUnitOfWork inner = TestUnitOfWork.Platform(context, new NoOpDomainEventDispatcher());

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      inner.SaveChangesAsync(cancellationToken);

    public Task<SSAS.BuildingBlocks.Application.Abstractions.Persistence.ITransaction> BeginTransactionAsync(
      CancellationToken cancellationToken = default) =>
      inner.BeginTransactionAsync(cancellationToken);
  }

  private sealed class PlatformSupportSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);

    public const string PlatformSupportAuthorityMigration = "20260810165035_AddPlatformSupportAuthority";

    public static async Task<PlatformSupportSqlDatabase> CreateAsync(bool migrate = true)
    {
      var databaseName = $"SSAS_ERP_FP003_PSA_{Guid.NewGuid():N}";
      var configured = IntegrationSqlEnvironment.BaseConnectionString;
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      var database = new PlatformSupportSqlDatabase(builder.ConnectionString);
      try
      {
        if (migrate)
        {
          await using var context = database.CreateContext();
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
    public DateTimeOffset UtcNow => PlatformSupportSqlDatabase.Now;
  }
}

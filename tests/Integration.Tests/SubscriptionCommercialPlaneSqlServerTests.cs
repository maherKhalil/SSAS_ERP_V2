using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Integration.Tests;

// THE COMMERCIAL PLANE AGAINST REAL SQL (FP-014, T-035).
//
// ---- WHY THIS SUITE EXISTS AND `Platform.Tests` IS NOT ENOUGH.
//
// `Platform.Tests` proves the domain refuses a bad append. It cannot prove a COMMIT is refused — T-014
// established that when the Platform model would not build on SQLite, and the append-only guard's real
// behaviour had to be shown against a real table. The same applies here twice over: the guard runs inside
// `SaveChangesAsync`, and the schema `CHECK`s and unique index only exist in a real database.
//
// So this suite asserts the three mechanisms that only a server can demonstrate:
//   1. `PreventAppendOnlyMutation` refuses an UPDATE and a DELETE of a persisted commercial record;
//   2. the unique index makes two records at the same instant impossible, not merely unlikely;
//   3. the `CHECK`s refuse the incoherent term and grant shapes the domain will not produce.
public sealed class SubscriptionCommercialPlaneSqlServerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  // The migration whose OWN effect section 4 asserts (`DEC-L-041`). Named rather than inferred from the
  // end of the chain, so the claim moves only when someone deliberately moves it.
  private const string CommercialPlaneMigration = "20260826031515_AddSubscriptionCommercialPlane";

  // ==================================================================================================
  // 1. THE APPEND-ONLY GUARD, ACTUALLY REFUSING.
  // ==================================================================================================

  [Fact]
  public async Task A_persisted_subscription_record_cannot_be_updated()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, planId) = await database.SeedAsync();

    await using (var context = database.CreateContext())
    {
      context.TenantSubscriptions.Add(NewSubscription(tenantId, planId, Now));
      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateContext())
    {
      var record = await context.TenantSubscriptions.SingleAsync();

      // A field a well-meaning caller might "correct" rather than append a new record for.
      context.Entry(record).Property(subscription => subscription.ChangeReasonText).CurrentValue = "corrected";

      var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
        () => context.SaveChangesAsync());
      Assert.Contains("append-only", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    // And nothing was written: the refusal is before the command, not a rollback after it.
    await using (var context = database.CreateContext())
    {
      Assert.Null((await context.TenantSubscriptions.SingleAsync()).ChangeReasonText);
    }
  }

  // ---- THE DELETE IS REFUSED TWICE OVER, AND THE FIRST REFUSAL IS NOT THE GUARD.
  //
  // Written expecting `PreventAppendOnlyMutation` to refuse this at `SaveChangesAsync`, as it does for
  // `TenantEntitlementGrant` below. It does not get the chance: `TenantSubscription` owns
  // `SubscriptionTerm`, so `Remove` severs a required owned relationship and **EF throws at tracking time,
  // before any save is attempted**.
  //
  // Asserted as it actually behaves rather than as it was expected to, because the difference matters to a
  // future reader. The record is protected either way — and it is protected by two independent mechanisms,
  // only one of which is the append-only guard. **Remove the guard and this test still passes**, which is
  // exactly why the grant test below exists: that type owns nothing, so its delete reaches the guard and
  // demonstrates it.
  [Fact]
  public async Task A_persisted_subscription_record_cannot_be_deleted()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, planId) = await database.SeedAsync();

    await using (var context = database.CreateContext())
    {
      context.TenantSubscriptions.Add(NewSubscription(tenantId, planId, Now));
      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateContext())
    {
      var record = await context.TenantSubscriptions.SingleAsync();

      var refusal = Assert.Throws<InvalidOperationException>(() => context.TenantSubscriptions.Remove(record));
      Assert.Contains("severed", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await context.TenantSubscriptions.CountAsync());
    }
  }

  // ---- AND THE GUARD ITSELF, DEMONSTRATED ON A DELETE THAT REACHES IT.
  //
  // The same refusal one layer down: a raw SQL delete bypasses EF entirely and therefore bypasses both
  // mechanisms. It succeeds, and that is worth knowing rather than assuming — `PreventAppendOnlyMutation`
  // is a write-boundary guard, not a database-level one, and nothing here claims otherwise. `ADR-017`'s
  // audit posture and the platform-plane permission set are what stand between a caller and raw SQL.
  [Fact]
  public async Task The_guard_is_a_write_boundary_and_raw_sql_is_outside_it()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, planId) = await database.SeedAsync();

    await using (var context = database.CreateContext())
    {
      context.TenantSubscriptions.Add(NewSubscription(tenantId, planId, Now));
      await context.SaveChangesAsync();
    }

    await database.ExecuteAsync("DELETE FROM [platform].[TenantSubscriptions];");

    await using (var context = database.CreateContext())
    {
      Assert.Equal(0, await context.TenantSubscriptions.CountAsync());
    }
  }

  // The grant history is append-only on the same terms and for the same reason: a grant in force last March
  // must still be discoverable next March.
  [Fact]
  public async Task A_persisted_entitlement_grant_cannot_be_updated_or_deleted()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, _) = await database.SeedAsync();

    await using (var context = database.CreateContext())
    {
      context.TenantEntitlementGrants.Add(TenantEntitlementGrant.GrantModule(
        tenantId, ModuleKey.Create("Attendance").Value, Now, null, "integration", null, null, Now).Value);
      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateContext())
    {
      var grant = await context.TenantEntitlementGrants.SingleAsync();
      context.Entry(grant).Property(item => item.ReasonText).CurrentValue = "changed";
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    await using (var context = database.CreateContext())
    {
      context.TenantEntitlementGrants.Remove(await context.TenantEntitlementGrants.SingleAsync());
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }
  }

  // ---- THE PLAN IS *NOT* APPEND-ONLY, AND THAT ASYMMETRY IS DELIBERATE.
  //
  // A catalog entry is edited before and between uses. Asserting the difference stops a future reader
  // concluding the whole commercial plane is frozen and "fixing" the plan to match.
  [Fact]
  public async Task A_plan_can_be_edited_because_a_catalog_entry_is_not_a_history()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (_, planId) = await database.SeedAsync();

    await using (var context = database.CreateContext())
    {
      var plan = await context.SubscriptionPlans.SingleAsync(item => item.Id == planId);
      Assert.True(plan.Activate("integration", Now.AddMinutes(1)).IsSuccess);
      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(
        SSAS.Platform.Domain.Enums.SubscriptionPlanStatus.Active,
        (await context.SubscriptionPlans.SingleAsync(item => item.Id == planId)).Status);
    }
  }

  // ==================================================================================================
  // 2. THE UNIQUE INDEX — THE MECHANICAL BACKSTOP FOR "EXACTLY ONE IN FORCE".
  // ==================================================================================================
  //
  // The domain refuses a non-monotonic append, but two callers reading the same maximum concurrently could
  // each satisfy that rule. This index is what makes the resulting pair impossible rather than rare, and it
  // is why the domain check and the constraint are both present.
  [Fact]
  public async Task Two_records_at_the_same_instant_are_impossible()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, planId) = await database.SeedAsync();

    await using (var context = database.CreateContext())
    {
      context.TenantSubscriptions.Add(NewSubscription(tenantId, planId, Now));
      await context.SaveChangesAsync();
    }

    await using (var context = database.CreateContext())
    {
      // Both records pass every domain check individually — the second was constructed as though it were
      // the first, which is exactly what a racing caller produces.
      context.TenantSubscriptions.Add(NewSubscription(tenantId, planId, Now));

      var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
      Assert.IsType<SqlException>(failure.InnerException);
    }
  }

  // ==================================================================================================
  // 3. THE `CHECK` CONSTRAINTS, EXERCISED BY RAW SQL.
  // ==================================================================================================
  //
  // Raw SQL deliberately: the domain will not construct these shapes, so the only way to prove the database
  // refuses them is to bypass the domain — which is also the only way they could ever arrive.

  [Fact]
  public async Task A_perpetual_term_carrying_an_end_date_is_refused_by_the_database()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, planId) = await database.SeedAsync();

    var failure = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync($"""
      INSERT INTO [platform].[TenantSubscriptions]
        ([TenantSubscriptionId],[TenantId],[SubscriptionPlanId],[EffectiveFromUtc],[TermKind],
         [TermStartUtc],[TermEndUtc],[BillingCurrencyCode],[CreatedUtc],[ChangedBy])
      VALUES (NEWID(),'{tenantId:D}','{planId:D}',SYSDATETIMEOFFSET(),N'Perpetual',
         SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),N'USD',SYSDATETIMEOFFSET(),N'raw');
      """));

    Assert.Contains("CK_TenantSubscriptions_Term", failure.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task A_grant_that_is_neither_a_module_grant_nor_a_limit_raise_is_refused()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();
    var (tenantId, _) = await database.SeedAsync();

    var failure = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync($"""
      INSERT INTO [platform].[TenantEntitlementGrants]
        ([TenantEntitlementGrantId],[TenantId],[GrantKind],[ModuleKey],[LimitKey],[LimitValue],
         [EffectiveFromUtc],[CreatedUtc],[GrantedBy])
      VALUES (NEWID(),'{tenantId:D}',N'ModuleGrant',NULL,NULL,NULL,
         SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),N'raw');
      """));

    Assert.Contains("CK_TenantEntitlementGrants_Shape", failure.Message, StringComparison.Ordinal);
  }

  // ==================================================================================================
  // 4. THE MIGRATION CREATED THE PLANE EMPTY — PINNED TO THAT MIGRATION (`DEC-L-041`).
  // ==================================================================================================
  //
  // `CON-0001` and `OD-SUB-0004`: no backfill and no default plan.
  //
  // ---- THIS TEST USED TO OBSERVE THE END OF THE CHAIN, WHICH IS THE THING ITS OWN COMMENT WARNED ABOUT.
  //
  // It migrated a fresh database all the way and asserted every table was empty — while stating that
  // *"the migration writes no rows" is a claim about the migration rather than about the code that reads
  // it*. The claim was pinned to the chain, so `AddTrialSubscriptionSeed` (T-041), which deliberately DOES
  // write rows, would have turned it red for a reason that has nothing to do with what it asserts.
  //
  // **It now migrates to `AddSubscriptionCommercialPlane` and stops there.** Not a relaxation — the
  // strictly stronger claim, and one that stays true however many migrations follow.
  [Fact]
  public async Task The_commercial_plane_migration_itself_creates_no_rows()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync(CommercialPlaneMigration);

    await using var context = database.CreateContext();

    Assert.Equal(0, await context.SubscriptionPlans.CountAsync());
    Assert.Equal(0, await context.ModuleDefinitions.CountAsync());
    Assert.Equal(0, await context.TenantSubscriptions.CountAsync());
    Assert.Equal(0, await context.TenantEntitlementGrants.CountAsync());
  }

  // ---- AND THE OTHER HALF OF THE PAIR: THE NEXT MIGRATION FILLS WHAT THIS ONE LEFT EMPTY.
  //
  // Together these two are the ordering, stated as behaviour rather than as a comment: **the plane is
  // created empty and proved empty, and a separate, dated migration seeds it.** The `THROW` at the foot of
  // `AddSubscriptionCommercialPlane` is a check on its own effect and does not fire for its successor —
  // asserted here, because a reader could reasonably expect it to.
  [Fact]
  public async Task The_following_migration_seeds_the_trial_plan_without_tripping_that_throw()
  {
    await using var database = await SubscriptionSqlDatabase.CreateAsync();

    await using var context = database.CreateContext();

    var plan = await context.SubscriptionPlans
      .AsNoTracking()
      .Include(item => item.ModuleGrants)
      .SingleAsync(item => item.Id == TrialSubscription.PlanId);

    Assert.Equal(TrialSubscription.PlanCodeValue, plan.PlanCode.Value);
    Assert.Equal(SSAS.Platform.Domain.Enums.SubscriptionPlanStatus.Active, plan.Status);
    Assert.Equal(
      [.. TrialSubscription.ModuleKeys.Order(StringComparer.Ordinal)],
      plan.ModuleGrants.Select(grant => grant.ModuleKey.Value).Order(StringComparer.Ordinal));

    // No tenant existed when the seed ran, so it issued nothing. The plane is seeded; the estate is not
    // invented.
    Assert.Equal(0, await context.TenantSubscriptions.CountAsync());
  }

  private static TenantSubscription NewSubscription(Guid tenantId, Guid planId, DateTimeOffset effectiveFrom) =>
    TenantSubscription.Append(
      tenantId, planId, effectiveFrom, null, SubscriptionTerm.Perpetual(effectiveFrom),
      "USD", "integration", null, null, Now).Value;

  private sealed class SubscriptionSqlDatabase(string connectionString) : IAsyncDisposable
  {
    // `targetMigration` stops the chain at a named migration (`DEC-L-041`): a claim about what ONE
    // migration did is asserted against a database migrated to exactly that point, so it cannot be
    // falsified by a successor doing something entirely legitimate. Null runs the whole chain.
    public static async Task<SubscriptionSqlDatabase> CreateAsync(string? targetMigration = null)
    {
      var builder = new SqlConnectionStringBuilder(IntegrationSqlEnvironment.BaseConnectionString)
      {
        InitialCatalog = $"SSAS_ERP_FP014_{Guid.NewGuid():N}"
      };
      var database = new SubscriptionSqlDatabase(builder.ConnectionString);
      try
      {
        await using var context = database.CreateContext();

        if (targetMigration is null)
        {
          await context.Database.MigrateAsync();
        }
        else
        {
          await context.Database.GetService<IMigrator>().MigrateAsync(targetMigration);
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
      return new PlatformDbContext(options, new TestUser(), new TestTenant(), new TestClock());
    }

    public async Task<(Guid TenantId, Guid PlanId)> SeedAsync()
    {
      await using var context = CreateContext();

      var tenant = SSAS.Platform.Domain.Tenants.Tenant.Create(
        TenantCode.Create($"T{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create("Commercial plane tenant").Value,
        "integration",
        Guid.NewGuid(),
        Now).Value;
      context.Tenants.Add(tenant);

      var plan = SubscriptionPlan.Create(
        PlanCode.Create($"P{Guid.NewGuid():N}"[..12]).Value,
        PlanName.Create("Standard").Value,
        "integration",
        Now).Value;
      plan.GrantModule(ModuleKey.Create("HR").Value, "integration", Now);
      plan.SetLimit(PlanLimit.Seats, 100, "integration", Now);
      plan.SetPrice("USD", SSAS.Platform.Domain.Enums.SubscriptionBillingPeriod.Monthly, 99.9900m,
        "integration", Now);
      context.SubscriptionPlans.Add(plan);

      await context.SaveChangesAsync();
      return (tenant.TenantId, plan.SubscriptionPlanId);
    }

    public async Task ExecuteAsync(string sql)
    {
      await using var connection = new SqlConnection(connectionString);
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext();
      await context.Database.EnsureDeletedAsync();
    }

    private sealed class TestUser : ICurrentUser
    {
      public string? UserId => "integration";
      public string? UserName => null;
      public string? Email => null;
      public Guid? CompanyId => null;
      public string? SessionId => null;
      public string? TokenId => null;
      public IReadOnlyCollection<string> Roles => [];
      public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestTenant : ICurrentTenant
    {
      public Guid? TenantId => null;
    }

    private sealed class TestClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => Now;
    }
  }
}

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// WHAT T-047 GAVE UP, ASSERTED AGAINST REAL SQL RATHER THAN DESCRIBED (T-047).
// ==================================================================================================
//
// `PersistenceDbContext` no longer forces `Restrict` onto ownership foreign keys, and
// `RelaxOwnershipDeleteBehaviour` moved the three on `SubscriptionPlan`'s owned collections to
// `Cascade`. **Through EF nothing changed at all** — owned rows are deleted with their owner by
// definition, and EF ordered those deletes itself under `Restrict`.
//
// **What changed is the behaviour of a raw `DELETE`**, and this suite is that change written down as a
// fact. The concession was argued in the task; a paragraph arguing it reads like reassurance, and a test
// that fails when the behaviour changes does not.
//
// ---- WHY THIS BELONGS IN INTEGRATION AND NOWHERE ELSE.
//
// Referential actions live in the database. `Architecture.Tests` asserts what the MODEL says, which is
// how the defect is prevented from returning; only a server can say what the schema actually does when a
// row is deleted outside the write boundary. T-035 already established that raw SQL is outside it —
// `The_guard_is_a_write_boundary_and_raw_sql_is_outside_it` — so this is not a hypothetical route.
public sealed class SubscriptionPlanOwnershipCascadeSqlServerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  // ==================================================================================================
  // 1. THE CONCESSION. A RAW DELETE OF AN UNREFERENCED PLAN NOW TAKES ITS OWNED ROWS WITH IT.
  // ==================================================================================================
  //
  // Under `Restrict` this `DELETE` failed. It now succeeds, and the plan's module grants, limits and
  // prices go with it. **That is the whole of what the change costs**, and it is asserted rather than
  // asserted-to-be-harmless: if someone later decides the cost is unacceptable, this test is what tells
  // them precisely what they are buying back.
  [Fact]
  public async Task A_raw_delete_of_an_unreferenced_plan_now_cascades_to_its_owned_rows()
  {
    await using var database = await OwnershipSqlDatabase.CreateAsync();
    var planId = await database.AddPlanAsync();

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await context.SubscriptionPlans.CountAsync(plan => plan.Id == planId));
      Assert.Equal(3, await database.OwnedRowCountAsync(planId));
    }

    // No EF, no write boundary, no domain. Exactly the path that bypasses everything.
    await database.ExecuteAsync(
      $"DELETE FROM [platform].[SubscriptionPlans] WHERE [SubscriptionPlanId] = '{planId}';");

    await using (var context = database.CreateContext())
    {
      Assert.Equal(0, await context.SubscriptionPlans.CountAsync(plan => plan.Id == planId));
      Assert.Equal(0, await database.OwnedRowCountAsync(planId));
    }
  }

  // ==================================================================================================
  // 2. AND WHAT STILL PROTECTS: A PLAN A TENANT IS ON CANNOT BE DELETED AT ALL.
  // ==================================================================================================
  //
  // **This is the half that makes the concession narrow enough to accept.** `TenantSubscriptions`'
  // foreign key to `SubscriptionPlans` is a REFERENCE key, not an ownership key, so the `Restrict` loop
  // still covers it. The exposure T-047 opened is a raw delete of a plan **nobody is on** — and
  // `SubscriptionPlan`'s lifecycle has no removal in the first place, because historical subscription
  // records point at it.
  [Fact]
  public async Task A_raw_delete_of_a_plan_a_tenant_is_on_is_still_refused()
  {
    await using var database = await OwnershipSqlDatabase.CreateAsync();
    var planId = await database.AddPlanAsync();
    var tenantId = await database.AddTenantOnPlanAsync(planId);

    var refusal = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
      $"DELETE FROM [platform].[SubscriptionPlans] WHERE [SubscriptionPlanId] = '{planId}';"));

    Assert.Contains(
      "FK_TenantSubscriptions_SubscriptionPlans_SubscriptionPlanId",
      refusal.Message,
      StringComparison.Ordinal);

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await context.SubscriptionPlans.CountAsync(plan => plan.Id == planId));
      Assert.Equal(3, await database.OwnedRowCountAsync(planId));
      Assert.Equal(1, await context.TenantSubscriptions.CountAsync(item => item.TenantId == tenantId));
    }
  }

  // ==================================================================================================
  // 3. THE SCHEMA SAYS `CASCADE` ON THE THREE, AND `NO ACTION` EVERYWHERE ELSE ON THE PLAN.
  // ==================================================================================================
  //
  // Read from `sys.foreign_keys` rather than inferred from behaviour, so a future migration that quietly
  // changed one of them fails here even if no test happened to delete anything. **`RelaxOwnershipDeleteBehaviour`
  // is hand-written** — EF generated it empty, because the snapshot cannot express the difference — so
  // nothing else in the build would ever notice if it were dropped.
  [Fact]
  public async Task The_schema_carries_cascade_on_ownership_and_no_action_on_the_reference()
  {
    await using var database = await OwnershipSqlDatabase.CreateAsync();

    foreach (var name in new[]
    {
      "FK_SubscriptionPlanLimits_SubscriptionPlans_SubscriptionPlanId",
      "FK_SubscriptionPlanModules_SubscriptionPlans_SubscriptionPlanId",
      "FK_SubscriptionPlanPrices_SubscriptionPlans_SubscriptionPlanId",
    })
    {
      Assert.Equal("CASCADE", await database.DeleteRuleAsync(name));
    }

    Assert.Equal(
      "NO_ACTION",
      await database.DeleteRuleAsync("FK_TenantSubscriptions_SubscriptionPlans_SubscriptionPlanId"));
  }

  private sealed class OwnershipSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static async Task<OwnershipSqlDatabase> CreateAsync()
    {
      var builder = new SqlConnectionStringBuilder(IntegrationSqlEnvironment.BaseConnectionString)
      {
        InitialCatalog = $"SSAS_ERP_T047_{Guid.NewGuid():N}"
      };
      var database = new OwnershipSqlDatabase(builder.ConnectionString);
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
      return new PlatformDbContext(options, new TestUser(), new TestTenant(), new TestClock());
    }

    public async Task<Guid> AddPlanAsync()
    {
      await using var context = CreateContext();

      var plan = SubscriptionPlan.Create(
        PlanCode.Create($"P{Guid.NewGuid():N}"[..12]).Value,
        PlanName.Create("Ownership cascade").Value,
        "integration",
        Now).Value;

      // One row in each owned collection, so the cascade has something to take.
      plan.GrantModule(ModuleKey.Create("HR").Value, "integration", Now);
      plan.SetLimit(PlanLimit.Seats, 50, "integration", Now);
      plan.SetPrice("USD", SubscriptionBillingPeriod.Monthly, 10.0000m, "integration", Now);
      plan.Activate("integration", Now);

      context.SubscriptionPlans.Add(plan);
      await context.SaveChangesAsync();

      return plan.SubscriptionPlanId;
    }

    public async Task<Guid> AddTenantOnPlanAsync(Guid planId)
    {
      await using var context = CreateContext();

      var tenant = SSAS.Platform.Domain.Tenants.Tenant.Create(
        TenantCode.Create($"T{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create("Ownership cascade tenant").Value,
        "integration",
        Guid.NewGuid(),
        Now).Value;
      context.Tenants.Add(tenant);

      context.TenantSubscriptions.Add(TenantSubscription.Append(
        tenant.TenantId, planId, Now, null, SubscriptionTerm.Perpetual(Now),
        "USD", "integration", null, null, Now).Value);

      await context.SaveChangesAsync();
      return tenant.TenantId;
    }

    public async Task<int> OwnedRowCountAsync(Guid planId) =>
      await ScalarAsync<int>(
        "SELECT (SELECT COUNT(*) FROM [platform].[SubscriptionPlanModules] WHERE [SubscriptionPlanId] = @p) + " +
        "(SELECT COUNT(*) FROM [platform].[SubscriptionPlanLimits] WHERE [SubscriptionPlanId] = @p) + " +
        "(SELECT COUNT(*) FROM [platform].[SubscriptionPlanPrices] WHERE [SubscriptionPlanId] = @p)",
        planId);

    public async Task<string> DeleteRuleAsync(string foreignKeyName) =>
      await ScalarAsync<string>(
        "SELECT delete_referential_action_desc FROM sys.foreign_keys WHERE name = @n", name: foreignKeyName);

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

    private async Task<T> ScalarAsync<T>(string sql, Guid? planId = null, string? name = null)
    {
      await using var connection = new SqlConnection(connectionString);
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      if (planId is { } id)
      {
        command.Parameters.AddWithValue("@p", id);
      }

      if (name is not null)
      {
        command.Parameters.AddWithValue("@n", name);
      }

      return (T)(await command.ExecuteScalarAsync())!;
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

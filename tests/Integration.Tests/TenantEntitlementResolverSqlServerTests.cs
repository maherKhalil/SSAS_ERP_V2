using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Subscriptions;

namespace SSAS.Integration.Tests;

// THE RESOLVER AGAINST REAL SQL (FP-014, T-040).
//
// ---- WHAT ONLY A REAL DATABASE CAN SHOW.
//
// `Platform.Tests` proves the snapshot decides correctly given facts. It cannot prove the READ produces
// the right facts: "the record in force is the greatest `EffectiveFromUtc <= now`" is an ordering over
// rows, the owned plan collections are a join, and `DEC-SUB-0009`'s intra-database keys are real
// constraints. All three need a server.
//
// The append-only history makes this sharper than a usual read test — the resolver must pick **one** row
// out of several for the same tenant, and picking the wrong one is invisible in any test with a single
// record.
public sealed class TenantEntitlementResolverSqlServerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  // ---- THE LATEST APPEND WINS, AND IT IS NOT THE LAST ROW INSERTED.
  //
  // Three records are written in an order that does NOT match their effective dates, so a resolver
  // returning "the last one saved" or "the first one found" gets a different answer from the correct one.
  [Fact]
  public async Task The_record_in_force_is_the_greatest_effective_from_not_the_last_written()
  {
    await using var database = await EntitlementSqlDatabase.CreateAsync();
    var tenantId = await database.SeedTenantAsync();

    var small = await database.SeedPlanAsync("SMALL", ["HR"], seats: 10);
    var large = await database.SeedPlanAsync("LARGE", ["HR", "Payroll"], seats: 500);

    // Written newest-first, deliberately.
    await database.AppendSubscriptionAsync(tenantId, large, Now.AddDays(-1));
    await database.AppendSubscriptionAsync(tenantId, small, Now.AddDays(-30));

    var snapshot = await database.ReadAsync(tenantId);

    Assert.Equal(large, snapshot.SubscriptionPlanId);
    Assert.Contains("Payroll", snapshot.PlanModules);
    Assert.Equal(500, snapshot.LimitAt("Seats", Now));
  }

  // A record dated in the future has not taken effect, and must not be picked over the one that has.
  [Fact]
  public async Task A_future_dated_record_is_not_in_force_yet()
  {
    await using var database = await EntitlementSqlDatabase.CreateAsync();
    var tenantId = await database.SeedTenantAsync();
    var current = await database.SeedPlanAsync("NOW", ["HR"], seats: 10);
    var future = await database.SeedPlanAsync("LATER", ["HR", "Payroll"], seats: 999);

    await database.AppendSubscriptionAsync(tenantId, current, Now.AddDays(-10));
    await database.AppendSubscriptionAsync(tenantId, future, Now.AddYears(1));

    var snapshot = await database.ReadAsync(tenantId);

    Assert.Equal(current, snapshot.SubscriptionPlanId);
    Assert.DoesNotContain("Payroll", snapshot.PlanModules);
  }

  [Fact]
  public async Task A_tenant_with_no_subscription_reads_as_entitled_to_nothing()
  {
    await using var database = await EntitlementSqlDatabase.CreateAsync();
    var tenantId = await database.SeedTenantAsync();

    var snapshot = await database.ReadAsync(tenantId);

    Assert.Null(snapshot.SubscriptionPlanId);
    Assert.Empty(snapshot.PlanModules);
    Assert.False(snapshot.IsModuleEnabledAt("HR", Now));
  }

  // ---- A GRANT READ FROM SQL ADDS A MODULE THE PLAN DOES NOT CARRY.
  [Fact]
  public async Task An_additive_grant_is_read_and_applied()
  {
    await using var database = await EntitlementSqlDatabase.CreateAsync();
    var tenantId = await database.SeedTenantAsync();
    var plan = await database.SeedPlanAsync("BASE", ["HR"], seats: 10);
    await database.AppendSubscriptionAsync(tenantId, plan, Now.AddDays(-1));
    await database.GrantModuleAsync(tenantId, "Attendance");

    var snapshot = await database.ReadAsync(tenantId);

    Assert.False(snapshot.PlanModules.Contains("Attendance"));
    Assert.True(snapshot.IsModuleEnabledAt("Attendance", Now));
    Assert.True(snapshot.IsModuleEnabledAt("HR", Now));
  }

  // ---- EXPIRY, ALL THE WAY THROUGH: A REAL ROW, A REAL READ, AND THE CLOCK.
  //
  // The subscription is real and its term is real; nothing is written between the two assertions.
  [Fact]
  public async Task A_real_expired_term_denies_every_module_without_a_write()
  {
    await using var database = await EntitlementSqlDatabase.CreateAsync();
    var tenantId = await database.SeedTenantAsync();
    var plan = await database.SeedPlanAsync("FIXED", ["HR"], seats: 10);

    await database.AppendSubscriptionAsync(
      tenantId, plan, Now.AddDays(-30), SubscriptionTerm.Fixed(Now.AddDays(-30), Now.AddDays(-1)).Value);

    var snapshot = await database.ReadAsync(tenantId);

    // Inside the term it was entitled; now it is not, and the row is untouched.
    Assert.True(snapshot.IsModuleEnabledAt("HR", Now.AddDays(-15)));
    Assert.False(snapshot.IsModuleEnabledAt("HR", Now));

    await using var context = database.CreateContext();
    Assert.Equal(1, await context.TenantSubscriptions.CountAsync());
  }

  private sealed class EntitlementSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static async Task<EntitlementSqlDatabase> CreateAsync()
    {
      var builder = new SqlConnectionStringBuilder(IntegrationSqlEnvironment.BaseConnectionString)
      {
        InitialCatalog = $"SSAS_ERP_FP014R_{Guid.NewGuid():N}"
      };
      var database = new EntitlementSqlDatabase(builder.ConnectionString);
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

    public async Task<SSAS.Platform.Application.Subscriptions.TenantEntitlementSnapshot> ReadAsync(Guid tenantId)
    {
      await using var context = CreateContext();
      return await new TenantEntitlementReader(context).ReadAsync(tenantId, CancellationToken.None);
    }

    public async Task<Guid> SeedTenantAsync()
    {
      await using var context = CreateContext();
      var tenant = SSAS.Platform.Domain.Tenants.Tenant.Create(
        TenantCode.Create($"T{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create("Resolver tenant").Value, "integration", Guid.NewGuid(), Now).Value;
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      return tenant.TenantId;
    }

    public async Task<Guid> SeedPlanAsync(string code, string[] modules, long seats)
    {
      await using var context = CreateContext();
      var plan = SubscriptionPlan.Create(
        PlanCode.Create($"{code}{Guid.NewGuid():N}"[..12]).Value,
        PlanName.Create(code).Value, "integration", Now).Value;

      foreach (var module in modules)
      {
        plan.GrantModule(ModuleKey.Create(module).Value, "integration", Now);
      }

      plan.SetLimit(PlanLimit.Seats, seats, "integration", Now);
      plan.SetPrice("USD", SubscriptionBillingPeriod.Monthly, 10m, "integration", Now);
      context.SubscriptionPlans.Add(plan);
      await context.SaveChangesAsync();
      return plan.SubscriptionPlanId;
    }

    public async Task AppendSubscriptionAsync(
      Guid tenantId, Guid planId, DateTimeOffset effectiveFrom, SubscriptionTerm? term = null)
    {
      await using var context = CreateContext();
      var record = TenantSubscription.Append(
        tenantId, planId, effectiveFrom, null,
        term ?? SubscriptionTerm.Perpetual(effectiveFrom),
        "USD", "integration", null, null, Now).Value;
      context.TenantSubscriptions.Add(record);
      await context.SaveChangesAsync();
    }

    public async Task GrantModuleAsync(Guid tenantId, string moduleKey)
    {
      await using var context = CreateContext();
      context.TenantEntitlementGrants.Add(TenantEntitlementGrant.GrantModule(
        tenantId, ModuleKey.Create(moduleKey).Value, Now.AddDays(-1), null,
        "integration", null, null, Now).Value);
      await context.SaveChangesAsync();
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

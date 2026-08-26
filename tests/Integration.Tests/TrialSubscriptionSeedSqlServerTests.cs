using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Seeding;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE TRIAL SEED AGAINST REAL SQL, RUN TWICE (FP-014, `DEC-L-034`, T-041).
// ==================================================================================================
//
// ---- WHY THE SEED IS EXECUTED HERE AND NOT MERELY THE MIGRATION.
//
// A migration runs once, so a migration cannot demonstrate that re-running it is safe. `TrialSubscriptionSeed.Sql`
// is one string shared by the migration and by these tests, so **the statement proved idempotent here is
// the statement that shipped** — not a transcription of it.
//
// The fixture migrates the whole chain, which means the seed has ALREADY RUN ONCE, against a database with
// no tenants. Every test below therefore starts from the state a real deployment starts from: the plan and
// the module catalog seeded, and no subscription issued to anybody.
//
// ---- AND WHY THIS CANNOT BE DONE IN `Platform.Tests`.
//
// Three of the claims exist only in a server: the `NOT EXISTS` guards are SQL, the append-only refusal
// happens inside `SaveChangesAsync` against a real table, and the `CHECK` on the term shape is a
// constraint. `TrialSubscriptionIssuanceTests` covers the C# half; this covers the half with a database
// underneath it.
public sealed class TrialSubscriptionSeedSqlServerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  // ==================================================================================================
  // 1. RUNNING IT TWICE ISSUES ONCE. THIS IS THE CRITERION.
  // ==================================================================================================
  //
  // Three tenants, seeded, then seeded again. **Both the count and the `EffectiveFromUtc` values are
  // asserted unchanged**, because a second issuance would not have to increase the count to do damage —
  // the record in force is the one with the greatest `EffectiveFromUtc`, so a later duplicate silently
  // replaces the earlier one even where the total happens to look wrong only on inspection.
  [Fact]
  public async Task Running_the_seed_twice_issues_exactly_one_subscription_per_tenant()
  {
    await using var database = await TrialSeedSqlDatabase.CreateAsync();
    var tenantIds = await database.AddTenantsAsync(3);

    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using (var context = database.CreateContext())
    {
      Assert.Equal(3, await context.TenantSubscriptions.CountAsync());
    }

    var issuedAt = await database.EffectiveFromByTenantAsync();

    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using (var context = database.CreateContext())
    {
      Assert.Equal(3, await context.TenantSubscriptions.CountAsync());

      foreach (var tenantId in tenantIds)
      {
        Assert.Single(context.TenantSubscriptions.Where(item => item.TenantId == tenantId));
      }
    }

    // Nothing moved. A second record would have taken effect later and become the one in force.
    Assert.Equal(issuedAt, await database.EffectiveFromByTenantAsync());
  }

  // ---- AND WHAT IT WROTE IS THE TRIAL, FIELD BY FIELD.
  //
  // The same plan, the same fourteen days and the same `XXX` currency the C# issuer writes at tenant
  // creation — that identity is what `DEC-L-034` means by one rule for existing and new tenants.
  [Fact]
  public async Task The_seeded_record_is_the_trial_plan_with_a_fixed_fourteen_day_term()
  {
    await using var database = await TrialSeedSqlDatabase.CreateAsync();
    await database.AddTenantsAsync(1);

    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using var context = database.CreateContext();
    var issued = await context.TenantSubscriptions.AsNoTracking().SingleAsync();

    Assert.Equal(TrialSubscription.PlanId, issued.SubscriptionPlanId);
    Assert.Equal(SubscriptionTermKind.Fixed, issued.Term.Kind);
    Assert.Equal(TrialSubscription.BillingCurrencyCode, issued.BillingCurrencyCode.Trim());
    Assert.Equal(TrialSubscription.ChangeReasonCode, issued.ChangeReasonCode);
    Assert.Equal(
      TimeSpan.FromDays(TrialSubscription.TermDays),
      issued.Term.EndUtc!.Value - issued.Term.StartUtc);

    // No history was reconstructed: the record takes effect when the seed ran, not when the tenant was
    // created. `EffectiveFromUtc` equals the term start for the same reason — one instant, written once.
    Assert.Equal(issued.Term.StartUtc, issued.EffectiveFromUtc);
  }

  // ---- THE CASE THE GUARD EXISTS FOR: A TENANT THAT ALREADY BOUGHT SOMETHING.
  //
  // Not a duplicate row — a **silent downgrade**. The seeded trial would take effect after the purchased
  // plan and become the record in force, and nothing would report an error.
  [Fact]
  public async Task A_tenant_already_holding_a_subscription_is_left_untouched()
  {
    await using var database = await TrialSeedSqlDatabase.CreateAsync();
    var tenantIds = await database.AddTenantsAsync(2);
    var purchasedPlanId = await database.AddPurchasedPlanAsync();

    await using (var context = database.CreateContext())
    {
      context.TenantSubscriptions.Add(TenantSubscription.Append(
        tenantIds[0], purchasedPlanId, Now, null, SubscriptionTerm.Perpetual(Now),
        "USD", "sales", null, null, Now).Value);
      await context.SaveChangesAsync();
    }

    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using (var context = database.CreateContext())
    {
      // The purchaser keeps exactly what they bought.
      var purchaser = await context.TenantSubscriptions.AsNoTracking()
        .SingleAsync(item => item.TenantId == tenantIds[0]);
      Assert.Equal(purchasedPlanId, purchaser.SubscriptionPlanId);
      Assert.Equal(SubscriptionTermKind.Perpetual, purchaser.Term.Kind);

      // And the tenant beside them still gets the trial: the guard is per tenant, not a global "has
      // anything been seeded".
      var other = await context.TenantSubscriptions.AsNoTracking()
        .SingleAsync(item => item.TenantId == tenantIds[1]);
      Assert.Equal(TrialSubscription.PlanId, other.SubscriptionPlanId);
    }
  }

  // ---- NO STATUS FILTER, AND THIS IS THE ASSERTION THAT SAYS SO.
  //
  // `OD-SUB-0010` ruled subscription state and `TenantStatus` orthogonal. An archived tenant is seeded
  // like any other — the alternative is a tenant reactivated a year from now being the only one in the
  // estate holding nothing, discovered by its owner rather than by us.
  [Fact]
  public async Task An_archived_tenant_is_seeded_like_every_other()
  {
    await using var database = await TrialSeedSqlDatabase.CreateAsync();
    var archivedId = await database.AddArchivedTenantAsync();

    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using var context = database.CreateContext();

    Assert.Equal(
      TenantStatus.Archived,
      (await context.Tenants.AsNoTracking().SingleAsync(item => item.Id == archivedId)).Status);
    Assert.Equal(
      TrialSubscription.PlanId,
      (await context.TenantSubscriptions.AsNoTracking()
        .SingleAsync(item => item.TenantId == archivedId)).SubscriptionPlanId);
  }

  // ==================================================================================================
  // 2. THE CATALOG IS SEEDED ONCE TOO, HOWEVER OFTEN THE SEED RUNS.
  // ==================================================================================================
  //
  // The plan, its four module grants, its price and the four module definitions each carry their own
  // `NOT EXISTS`. Asserted after a second and third run, because a `NOT EXISTS` guarding the plan row
  // would not stop a duplicate grant.
  [Fact]
  public async Task The_plan_the_grants_the_price_and_the_catalog_are_written_exactly_once()
  {
    await using var database = await TrialSeedSqlDatabase.CreateAsync();

    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);
    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using var context = database.CreateContext();

    var plan = await context.SubscriptionPlans
      .AsNoTracking()
      .Include(item => item.ModuleGrants)
      .Include(item => item.Prices)
      .SingleAsync(item => item.Id == TrialSubscription.PlanId);

    Assert.Equal(TrialSubscription.PlanNameValue, plan.PlanName.Value);
    Assert.Equal(SubscriptionPlanStatus.Active, plan.Status);
    Assert.Equal(TrialSubscription.ModuleKeys.Length, plan.ModuleGrants.Count);
    Assert.Equal(
      [.. TrialSubscription.ModuleKeys.Order(StringComparer.Ordinal)],
      plan.ModuleGrants.Select(grant => grant.ModuleKey.Value).Order(StringComparer.Ordinal));

    // The price is not decoration: `REQ-SUB-0023` requires the plan to carry one in the tenant's billing
    // currency, and `XXX` is ISO 4217's "no currency involved" (`DEC-L-040`) rather than a placeholder.
    var price = Assert.Single(plan.Prices);
    Assert.Equal(TrialSubscription.BillingCurrencyCode, price.CurrencyCode.Trim());
    Assert.Equal(0m, price.Amount);

    Assert.Equal(TrialSubscription.ModuleCatalog.Length, await context.ModuleDefinitions.CountAsync());
    Assert.True(await context.ModuleDefinitions.AllAsync(definition => definition.IsGateable));
  }

  // ==================================================================================================
  // 3. AND THE SEEDED ROWS ARE APPEND-ONLY LIKE ANY OTHER.
  // ==================================================================================================
  //
  // A record written by a migration is not a lesser record. `PreventAppendOnlyMutation` refuses to update
  // it exactly as it refuses one written through the domain — asserted here because "the seed wrote it"
  // is precisely the reasoning someone would use to justify correcting it in place.
  [Fact]
  public async Task A_seeded_subscription_cannot_be_corrected_in_place()
  {
    await using var database = await TrialSeedSqlDatabase.CreateAsync();
    await database.AddTenantsAsync(1);
    await database.ExecuteAsync(TrialSubscriptionSeed.Sql);

    await using (var context = database.CreateContext())
    {
      var seeded = await context.TenantSubscriptions.SingleAsync();
      context.Entry(seeded).Property(item => item.ChangeReasonText).CurrentValue = "corrected";

      var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
        () => context.SaveChangesAsync());
      Assert.Contains("append-only", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(
        TrialSubscription.ChangeReasonText,
        (await context.TenantSubscriptions.AsNoTracking().SingleAsync()).ChangeReasonText);
    }
  }

  private sealed class TrialSeedSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static async Task<TrialSeedSqlDatabase> CreateAsync()
    {
      var builder = new SqlConnectionStringBuilder(IntegrationSqlEnvironment.BaseConnectionString)
      {
        InitialCatalog = $"SSAS_ERP_T041_{Guid.NewGuid():N}"
      };
      var database = new TrialSeedSqlDatabase(builder.ConnectionString);
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

    public async Task<IReadOnlyList<Guid>> AddTenantsAsync(int count)
    {
      await using var context = CreateContext();

      var tenants = Enumerable.Range(0, count).Select(_ => NewTenant()).ToList();
      context.Tenants.AddRange(tenants);
      await context.SaveChangesAsync();

      return [.. tenants.Select(tenant => tenant.TenantId)];
    }

    public async Task<Guid> AddArchivedTenantAsync()
    {
      await using var context = CreateContext();

      var tenant = NewTenant();
      Assert.True(tenant.Archive(
        TenantStatusChangeReason.CustomerClosure, "integration", Guid.NewGuid(), Now).IsSuccess);

      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();

      return tenant.TenantId;
    }

    public async Task<Guid> AddPurchasedPlanAsync()
    {
      await using var context = CreateContext();

      var plan = SubscriptionPlan.Create(
        PlanCode.Create($"P{Guid.NewGuid():N}"[..12]).Value,
        PlanName.Create("Standard").Value,
        "sales",
        Now).Value;
      plan.GrantModule(ModuleKey.Create("HR").Value, "sales", Now);
      plan.SetPrice("USD", SubscriptionBillingPeriod.Monthly, 99.9900m, "sales", Now);
      plan.Activate("sales", Now);

      context.SubscriptionPlans.Add(plan);
      await context.SaveChangesAsync();

      return plan.SubscriptionPlanId;
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> EffectiveFromByTenantAsync()
    {
      await using var context = CreateContext();

      return await context.TenantSubscriptions
        .AsNoTracking()
        .ToDictionaryAsync(item => item.TenantId, item => item.EffectiveFromUtc);
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

    private static Tenant NewTenant() =>
      Tenant.Create(
        TenantCode.Create($"T{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create("Trial seed tenant").Value,
        "integration",
        Guid.NewGuid(),
        Now).Value;

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

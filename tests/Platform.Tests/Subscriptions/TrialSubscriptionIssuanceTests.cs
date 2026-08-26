using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Subscriptions;

// ==================================================================================================
// THE TRIAL AT TENANT CREATION, AND THE REFUSAL TO ISSUE IT TWICE (FP-014, `DEC-L-034`, T-041).
// ==================================================================================================
//
// T-040 made a tenant holding no subscription record reach no gated module. **These tests are the reason
// that is not a lockout for anything created from now on.**
//
// ---- WHAT IS REAL HERE AND WHAT IS NOT.
//
// The issuer and the command handler are the real types. What is faked is persistence, because the claims
// under test are about WHICH RECORD IS WRITTEN and WHEN ONE IS NOT — neither of which needs a database.
// The half that does need one — the seed against real SQL, and the append-only guard actually refusing —
// is `TrialSubscriptionSeedSqlServerTests`.
public sealed class TrialSubscriptionIssuanceTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  // ==================================================================================================
  // 1. CREATING A TENANT ISSUES THE TRIAL, IN THE SAME UNIT OF WORK.
  // ==================================================================================================
  //
  // The record is asserted field by field rather than merely counted, because "a subscription exists" is
  // not the claim — **the claim is that it is the SAME plan and the SAME term the cutover seed writes**,
  // which is what `DEC-L-034` means by one rule for existing and new tenants.
  [Fact]
  public async Task Creating_a_tenant_issues_the_trial_plan_with_a_fourteen_day_term_and_commits_once()
  {
    var subscriptions = new FakeSubscriptionRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateTenantCommandHandler(
      new FakeTenantRepository(),
      new TrialSubscriptionIssuer(subscriptions, new FixedClock(Noon)),
      unitOfWork,
      new TestCurrentUser("platform-actor"),
      new FixedClock(Noon));

    var created = await handler.HandleAsync(new CreateTenantCommand("ACME", "Acme Trading"));

    Assert.True(created.IsSuccess);

    var issued = Assert.Single(subscriptions.Added);
    Assert.Equal(created.Value, issued.TenantId);
    Assert.Equal(TrialSubscription.PlanId, issued.SubscriptionPlanId);
    Assert.Equal(SubscriptionTermKind.Fixed, issued.Term.Kind);
    Assert.Equal(Noon, issued.Term.StartUtc);
    Assert.Equal(Noon.AddDays(14), issued.Term.EndUtc);
    Assert.Equal("XXX", issued.BillingCurrencyCode);
    Assert.Equal("TRIAL", issued.ChangeReasonCode);

    // ---- ONE SAVE, COVERING BOTH.
    //
    // The tenant and its trial commit together. Two saves would mean a window in which the tenant exists
    // and is entitled to nothing, and a failure in that window would leave it there permanently.
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  // ---- THE TERM IS FOURTEEN DAYS AND THERE IS NO GRACE AFTER IT.
  //
  // `DEC-L-009` ruled no grace period and the owner ruled the fourteen days. Stated as an assertion rather
  // than a comment so that softening it is a failing test rather than an unnoticed kindness.
  [Fact]
  public void The_term_is_exactly_fourteen_days_with_no_grace_period()
  {
    var term = TrialSubscription.TermFrom(Noon);

    Assert.True(term.IsSuccess);
    Assert.Equal(Noon.AddDays(14), term.Value.EndUtc);
    Assert.False(term.Value.HasExpiredAt(Noon.AddDays(14)));
    Assert.True(term.Value.HasExpiredAt(Noon.AddDays(14).AddTicks(1)));
  }

  // ==================================================================================================
  // 2. RUNNING IT TWICE ISSUES ONCE.
  // ==================================================================================================
  //
  // ---- AND THE CLOCK MOVES BETWEEN THE RUNS, WHICH IS THE POINT.
  //
  // With a frozen clock a second append would be refused anyway — by `NonMonotonicAppend`, for a reason
  // that has nothing to do with double-issuing. Advancing the clock removes that alternative explanation:
  // the second append is now perfectly legal and is refused because **the tenant already holds a
  // record**, which is the rule being tested.
  [Fact]
  public async Task Issuing_twice_leaves_the_tenant_with_exactly_one_subscription()
  {
    var subscriptions = new FakeSubscriptionRepository();
    var clock = new MovableClock(Noon);
    var issuer = new TrialSubscriptionIssuer(subscriptions, clock);
    var tenantId = Guid.NewGuid();

    Assert.True((await issuer.IssueAsync(tenantId)).IsSuccess);

    clock.Now = Noon.AddDays(3);
    var second = await issuer.IssueAsync(tenantId);

    // A no-op, not a failure: re-running the issuance is an ordinary thing to do and must not fault.
    Assert.True(second.IsSuccess);
    Assert.Single(subscriptions.Added);
    Assert.Equal(Noon, subscriptions.Added[0].EffectiveFromUtc);
  }

  // ---- THE CASE THE GUARD ACTUALLY PROTECTS: A TENANT THAT BOUGHT SOMETHING.
  //
  // A trial appended after a purchased plan would take effect LATER, and the record in force is the one
  // with the greatest `EffectiveFromUtc` — so it would silently become the plan they are on. That is a
  // paying customer downgraded to a 14-day trial with no error anywhere.
  [Fact]
  public async Task A_tenant_already_on_a_purchased_plan_is_left_alone()
  {
    var purchasedPlanId = Guid.NewGuid();
    var subscriptions = new FakeSubscriptionRepository();
    var tenantId = Guid.NewGuid();

    await subscriptions.AddAsync(TenantSubscription.Append(
      tenantId, purchasedPlanId, Noon, null, SubscriptionTerm.Perpetual(Noon),
      "USD", "sales", null, null, Noon).Value);

    var issuer = new TrialSubscriptionIssuer(subscriptions, new FixedClock(Noon.AddDays(30)));

    Assert.True((await issuer.IssueAsync(tenantId)).IsSuccess);

    var untouched = Assert.Single(subscriptions.Added);
    Assert.Equal(purchasedPlanId, untouched.SubscriptionPlanId);
  }

  // Two tenants are two subscriptions: the guard is per tenant, not a global "has anyone been seeded".
  [Fact]
  public async Task Another_tenant_is_still_issued_one()
  {
    var subscriptions = new FakeSubscriptionRepository();
    var issuer = new TrialSubscriptionIssuer(subscriptions, new FixedClock(Noon));

    Assert.True((await issuer.IssueAsync(Guid.NewGuid())).IsSuccess);
    Assert.True((await issuer.IssueAsync(Guid.NewGuid())).IsSuccess);

    Assert.Equal(2, subscriptions.Added.Count);
  }

  // ==================================================================================================
  // 3. A MISSING SEED FAILS BY NAME, AND TAKES THE TENANT WITH IT.
  // ==================================================================================================
  //
  // The foreign key would refuse the insert anyway, so the tenant is safe either way. What this buys is
  // the DIAGNOSIS: "the trial plan has not been seeded" names its remedy and a constraint violation does
  // not. The tenant is not created, which is correct — a tenant created without a trial is locked out.
  [Fact]
  public async Task A_missing_trial_plan_fails_by_name_and_no_tenant_is_created()
  {
    var tenants = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateTenantCommandHandler(
      tenants,
      new TrialSubscriptionIssuer(
        new FakeSubscriptionRepository(planExists: false), new FixedClock(Noon)),
      unitOfWork,
      new TestCurrentUser("platform-actor"),
      new FixedClock(Noon));

    var created = await handler.HandleAsync(new CreateTenantCommand("ACME", "Acme Trading"));

    Assert.Equal("Subscription.TrialPlanMissing", created.Error.Code);
    Assert.Equal(0, unitOfWork.SaveCount);
  }

  // ==================================================================================================
  // 4. THE TRIAL IS A PLAN, AND THE MODEL STILL CARRIES NOTHING THAT SAYS SO.
  // ==================================================================================================
  //
  // `OD-SUB-0014` ruled a trial is a plan with a short term — not a state and not a flag. This is that
  // ruling as a tripwire: a reflective sweep of the two types a trial is written into, failing if anyone
  // adds an `IsTrial`, a `Trial` status or any other member naming the concept.
  //
  // A test can only check the names, not the intent. What it stops is the ordinary version of the
  // mistake — the convenience column added because a query was awkward — which is how a second way to be
  // entitled arrives in practice.
  [Fact]
  public void No_type_in_the_subscription_model_carries_a_trial_flag()
  {
    var members = typeof(TenantSubscription).GetProperties()
      .Select(property => property.Name)
      .Concat(typeof(SubscriptionPlan).GetProperties().Select(property => property.Name))
      .Concat(Enum.GetNames<SubscriptionPlanStatus>())
      .Concat(Enum.GetNames<TenantStatus>())
      .ToList();

    Assert.NotEmpty(members);
    Assert.DoesNotContain(
      members,
      name => name.Contains("Trial", StringComparison.OrdinalIgnoreCase));
  }

  private sealed class FakeSubscriptionRepository(bool planExists = true) : ITenantSubscriptionRepository
  {
    public List<TenantSubscription> Added { get; } = [];

    public Task<DateTimeOffset?> GreatestEffectiveFromUtcAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Added
        .Where(subscription => subscription.TenantId == tenantId)
        .Select(subscription => (DateTimeOffset?)subscription.EffectiveFromUtc)
        .Max());

    public Task<bool> PlanExistsAsync(
      Guid subscriptionPlanId, CancellationToken cancellationToken = default) =>
      Task.FromResult(planExists);

    public Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default)
    {
      Added.Add(subscription);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeTenantRepository : ITenantRepository
  {
    public Tenant? Added { get; private set; }

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult<Tenant?>(null);

    public Task<Tenant?> GetByNormalizedCodeAsync(
      string normalizedTenantCode, CancellationToken cancellationToken = default) =>
      Task.FromResult<Tenant?>(null);

    public Task<bool> NormalizedCodeExistsAsync(
      string normalizedTenantCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
      Added = tenant;
      return Task.CompletedTask;
    }
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Issuance shares the caller's unit of work; it opens no transaction.");
  }

  private sealed class TestCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId => userId;
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class FixedClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = now;
  }

  private sealed class MovableClock(DateTimeOffset now) : IDateTimeProvider
  {
    public DateTimeOffset Now { get; set; } = now;

    public DateTimeOffset UtcNow => Now;
  }
}

using Xunit;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.Subscriptions;

public class PlansDomainTests
{
  [Fact]
  public void SubscriptionPlan_ShouldUpdateName()
  {
    var plan = SubscriptionPlan.Create(PlanCode.Create("P1").Value, PlanName.Create("Plan 1").Value, "actor", DateTimeOffset.UtcNow).Value;
    
    var result = plan.UpdateName(PlanName.Create("Plan 1 Updated").Value, "actor2", DateTimeOffset.UtcNow);

    Assert.True(result.IsSuccess);
    Assert.Equal("Plan 1 Updated", plan.PlanName.Value);
    Assert.Equal("actor2", plan.ModifiedBy);
  }

  [Fact]
  public void SubscriptionPlan_ReplaceModules_ReplacesExistingModules()
  {
    var plan = SubscriptionPlan.Create(PlanCode.Create("P1").Value, PlanName.Create("Plan 1").Value, "actor", DateTimeOffset.UtcNow).Value;
    plan.GrantModule(ModuleKey.Create("HR").Value, "actor", DateTimeOffset.UtcNow);

    var result = plan.ReplaceModules([ModuleKey.Create("Payroll").Value], "actor2", DateTimeOffset.UtcNow);

    Assert.True(result.IsSuccess);
    Assert.Single(plan.ModuleGrants);
    Assert.Equal("Payroll", plan.ModuleGrants.First().ModuleKey.Value);
  }

  [Fact]
  public void SubscriptionPlan_ReplaceLimits_ReplacesExistingLimits()
  {
    var plan = SubscriptionPlan.Create(PlanCode.Create("P1").Value, PlanName.Create("Plan 1").Value, "actor", DateTimeOffset.UtcNow).Value;
    plan.SetLimit("Employees", 10, "actor", DateTimeOffset.UtcNow);

    var result = plan.ReplaceLimits([("Employees", 20)], "actor2", DateTimeOffset.UtcNow);

    Assert.True(result.IsSuccess);
    Assert.Single(plan.Limits);
    Assert.Equal(20, plan.Limits.First().LimitValue);
  }

  [Fact]
  public void SubscriptionPlan_ReplacePrices_ReplacesExistingPrices()
  {
    var plan = SubscriptionPlan.Create(PlanCode.Create("P1").Value, PlanName.Create("Plan 1").Value, "actor", DateTimeOffset.UtcNow).Value;
    plan.SetPrice("USD", SubscriptionBillingPeriod.Monthly, 100m, "actor", DateTimeOffset.UtcNow);

    var result = plan.ReplacePrices([("EUR", SubscriptionBillingPeriod.Annual, 1000m)], "actor2", DateTimeOffset.UtcNow);

    Assert.True(result.IsSuccess);
    Assert.Single(plan.Prices);
    Assert.Equal("EUR", plan.Prices.First().CurrencyCode);
  }
}

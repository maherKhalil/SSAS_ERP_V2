using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.Platform.Tests.TenantStorage;

// ==================================================================================================
// THE TRANSITIONAL ENTITLEMENT RESOLVER — WHAT IT ANSWERS, AND WHY THAT IS THE HONEST ANSWER.
// ==================================================================================================
//
// ---- THIS PINS A DELIBERATE PLACEHOLDER, NOT A FEATURE.
//
// `TransitionalGrantsEveryModuleEntitlement` answers **true for every module and every tenant**. That is
// not a bug and not a default that someone forgot to change: there is no plan, no per-tenant subscription
// assignment and no entitlement grant anywhere in this product. `OD-SUB-0004` places that data in the
// Platform database, and the build obligation is **no backfill and no default plan** — so until the schema
// exists, any answer other than "yes" would be inventing a commercial state the product has not recorded.
//
// **This does not satisfy `BR-PLT-0008`**, which is why these tests assert the placeholder's behaviour
// rather than an entitlement rule. The rule arrives with the data.
//
// ---- WHY IT IS WORTH TESTING AT ALL.
//
// Two reasons, and neither is coverage. The first is that "grants everything" is the property that makes
// mounting the seam SAFE — if it ever answered false for anything, installing the gate would have silently
// broken every module route. The second is the argument-guard below: the resolver is asked with a key it
// receives from a route group, and a blank key would mean a group was mounted with no module identity at
// all.
public sealed class TransitionalModuleEntitlementTests
{
  // Concrete rather than interface-typed: this suite pins THIS implementation's behaviour, and the
  // contract's own shape is asserted by `ModuleEnablementArchitectureTests`. CA1859 agrees.
  private static readonly TransitionalGrantsEveryModuleEntitlement Entitlement = new();

  // Asserted once, because everything below would still pass if the type stopped implementing the contract
  // the seam actually resolves.
  [Fact]
  public void The_transitional_resolver_implements_the_entitlement_contract() =>
    Assert.IsAssignableFrom<ITenantModuleEntitlement>(Entitlement);

  // ---- EVERY DECLARED MODULE KEY IS GRANTED.
  //
  // The four real keys, named rather than generated, so this fails if a key is renamed without the seam
  // being revisited.
  [Theory]
  [InlineData("HR")]
  [InlineData("Finance.GL")]
  [InlineData("Payroll")]
  [InlineData("Attendance")]
  public async Task Every_module_is_granted(string moduleKey) =>
    Assert.True(await Entitlement.IsEnabledAsync(moduleKey, CancellationToken.None));

  // ---- INCLUDING A KEY NO MODULE DECLARES.
  //
  // Stated explicitly because it is the property that must NOT survive replacement. The real resolver must
  // answer false for an unrecognised key — an unknown module is not entitled — so this test is expected to
  // be inverted by the task that replaces this type, and it should be read as a marker of that.
  [Fact]
  public async Task Even_an_unrecognised_module_is_granted_which_the_real_resolver_must_not_do() =>
    Assert.True(await Entitlement.IsEnabledAsync("NoSuchModule", CancellationToken.None));

  // ---- A BLANK KEY IS A PROGRAMMING ERROR, NOT A COMMERCIAL STATE.
  //
  // The key reaches the resolver from a route group's `RequireModule` call. Blank means a group was mounted
  // with no module identity, which no entitlement answer can be correct for — refusing would hide the
  // defect behind a plausible 403, and granting would gate nothing while appearing to.
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task A_blank_module_key_throws_rather_than_being_answered(string moduleKey) =>
    await Assert.ThrowsAsync<ArgumentException>(
      async () => await Entitlement.IsEnabledAsync(moduleKey, CancellationToken.None));

  [Fact]
  public async Task A_null_module_key_throws() =>
    await Assert.ThrowsAsync<ArgumentNullException>(
      async () => await Entitlement.IsEnabledAsync(null!, CancellationToken.None));
}

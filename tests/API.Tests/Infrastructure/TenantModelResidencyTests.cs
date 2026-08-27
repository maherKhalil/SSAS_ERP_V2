using Microsoft.Extensions.DependencyInjection;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// RESIDENCY IS MODEL MEMBERSHIP, AND THIS ASSERTS THAT RATHER THAN A PROXY FOR IT (T-074).
// ==================================================================================================
//
// ---- WHAT DECIDES WHETHER A TYPE TRAVELS AT CUTOVER.
//
// `TenantCutoverCopyPlan.Build(IModel model)` selects `ITenantOwnedEntity` **within the model it is
// handed**, and `TenantCutoverCopyService.cs:156` hands it `ITenantModelSource.Model`. So a type travels
// if and only if it is IN THAT MODEL. The interface decides which of the model's entities are copied; it
// does not decide which entities are in the model.
//
// ---- THE INTERFACE IS NOT THE PROPERTY, AND BOTH DIRECTIONS HAVE A COUNTER-EXAMPLE IN THIS TREE.
//
//   TenantUser   carries ITenantOwnedEntity (TenantUser.cs:9) and does NOT travel — it is not in the model
//   Branch       is a Platform.Domain type that DOES travel — it is in the model (TenantDbContext.cs:84)
//   Company      likewise (TenantDbContext.cs:86)
//
// **Neither the interface nor the assembly decides.** `SubscriptionResidencyArchitectureTests` asserts the
// commercial types LACK the interface, which is true and cheap and is not this property: it passes today
// because those types happen to satisfy both. A guard that is right by coincidence is one nobody notices
// going wrong, so the real property is asserted here as well rather than instead.
//
// ---- WHY A NAMED LIST IS RIGHT HERE, WHEN IT USUALLY IS NOT.
//
// This is a list of DECISIONS, not of code. Each entry is a type someone decided belongs in the Platform
// database, and a new member should have to be added by a person who has thought about which database it
// belongs in — H9 makes exactly this argument for its own exact inventory. A discovered list would also
// pass vacuously the day the discovery stopped matching.
//
// It deliberately does NOT restate the tenant model's inventory: H9 asserts that, and two lists of one
// thing drift.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantModelResidencyTests(HostWebApplicationFactory factory)
{
  // ---- THE COMMERCIAL PLANE (FP-014, DEC-SUB-0011).
  //
  // Platform-administered records ABOUT a tenant, never owned by one (DEC-SUB-0002). A copy of a tenant's
  // subscription history in its new dedicated database would be read from the wrong place or not at all.
  private static readonly Type[] CommercialPlane =
  [
    typeof(SubscriptionPlan),
    typeof(PlanModuleGrant),
    typeof(PlanLimit),
    typeof(PlanPrice),
    typeof(ModuleDefinition),
    typeof(TenantSubscription),
    typeof(TenantEntitlementGrant)
  ];

  // ---- THE IDENTITY AND ACCESS PLANE (ADR-030 Decision 1).
  //
  // Every one of these carries a `TenantId` and therefore LOOKS tenant-owned, which is what makes the
  // decision a decision. `TenantUser` is the case that matters most: it is the only entry that actually
  // carries `ITenantOwnedEntity`, so **the proxy guard could not express this one at all** — asserting the
  // absence of an interface it has would simply fail.
  //
  // `ADR-030` places the identity-to-employee mapping in this plane and rests on its residency, so this is
  // the guard that package will lean on.
  private static readonly Type[] IdentityAndAccessPlane =
  [
    typeof(TenantUser),
    typeof(TenantUserRoleAssignment),

    // ADR-030's identity-to-employee mapping (T-082). It declines `ITenantOwnedEntity`, so the proxy guard
    // in `SubscriptionResidencyArchitectureTests` happens to cover it — but the property that keeps it out
    // of the tenant database is THIS one, and it is the reason the type declines the interface at all: the
    // interface's only effect would be to make the mapping travel if anything ever added it to the tenant
    // model, which is exactly what `ADR-030` Decision 1 forbids.
    typeof(UserEmployeeLink),
    typeof(Identity),
    typeof(Role),
    typeof(RolePermissionAssignment),
    typeof(UserBranchAccess),
    typeof(UserCompanyAccess),
    typeof(Tenant)
  ];

  [Fact]
  public void No_platform_resident_type_is_in_the_model_the_cutover_copies_from()
  {
    var model = factory.Services.GetRequiredService<ITenantModelSource>().Model;

    var declared = CommercialPlane.Concat(IdentityAndAccessPlane).ToArray();

    // NOT VACUOUS. An empty list would make the loop below a check that cannot fail, and the counts are
    // stated so a plane emptied by a bad merge fails here rather than passing quietly.
    Assert.Equal(7, CommercialPlane.Length);
    Assert.Equal(9, IdentityAndAccessPlane.Length);

    var travelling = declared
      .Where(type => model.FindEntityType(type) is not null)
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // The message names what would happen, not who is at fault. A type may be here because it was added to
    // the tenant model by mistake, or because someone deliberately moved it and did not update this list —
    // and which of those it is cannot be read off the model.
    Assert.True(
      travelling.Length == 0,
      "A Platform-resident type is in the tenant model, so TenantCutoverCopyPlan.Build will sweep it into " +
      "the Shared-to-Dedicated copy if it carries ITenantOwnedEntity, and a tenant's new database will " +
      "carry rows that belong in the Platform database. Either the type was added to the tenant model in " +
      $"error, or its residency changed and this list was not updated:{Environment.NewLine}" +
      string.Join(Environment.NewLine, travelling));
  }

  // ---- THE CONTROL, AND IT IS WHAT KEEPS THE ASSERTION ABOVE FROM BEING TRIVIAL.
  //
  // `FindEntityType` returning null proves nothing on its own: it returns null for a type that is absent
  // and for a model that is empty, and it would keep passing if the resolved model were replaced by one
  // built with no configuration at all. Two Platform.Domain types ARE in this model deliberately, so
  // asserting their PRESENCE proves the model is populated and that `FindEntityType` distinguishes the two
  // cases on the very same assembly the absences are drawn from.
  [Fact]
  public void The_resolved_model_is_the_populated_one_and_two_platform_types_are_in_it_deliberately()
  {
    var model = factory.Services.GetRequiredService<ITenantModelSource>().Model;

    Assert.NotNull(model.FindEntityType(typeof(Branch)));
    Assert.NotNull(model.FindEntityType(typeof(Company)));
  }
}

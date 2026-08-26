using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Architecture.Tests;

// THE COMMERCIAL PLANE IS PLATFORM-RESIDENT AND DOES NOT TRAVEL AT CUTOVER (FP-014, `DEC-SUB-0011`).
//
// ---- WHY THIS NEEDS A TEST RATHER THAN A COMMENT.
//
// `TenantCutoverCopyPlan.Build` derives the Shared→Dedicated manifest by **reflecting over
// `ITenantOwnedEntity` within the model it is handed**. That is a good design — the manifest cannot drift
// from the model — and it has one consequence worth guarding: a commercial entity that acquired
// `ITenantOwnedEntity`, or was added to the tenant model by mistake, would be **swept into the copy
// automatically and silently**.
//
// The failure would not look like a failure. The cutover would succeed, and a tenant's new dedicated
// database would carry a copy of its subscription history — which by `ADR-017` and `DEC-SUB-0003` lives in
// the Platform database and nowhere else, and which the entitlement resolver would then read from the wrong
// place or not at all.
//
// So the absence is asserted rather than assumed, which is what the task asked for: an entity added to the
// Platform context could be swept in by a future reflection-based manifest, and nothing else would notice.
public sealed class SubscriptionResidencyArchitectureTests
{
  // The commercial types, named rather than discovered. A scan could pass vacuously; naming them means a
  // type renamed or moved fails here rather than dropping silently out of the check.
  private static readonly Type[] CommercialTypes =
  [
    typeof(SubscriptionPlan),
    typeof(PlanModuleGrant),
    typeof(PlanLimit),
    typeof(PlanPrice),
    typeof(ModuleDefinition),
    typeof(TenantSubscription),
    typeof(TenantEntitlementGrant),
  ];

  // ---- NONE OF THEM IS TENANT-OWNED, WHICH IS WHAT KEEPS THEM OUT OF THE MANIFEST.
  //
  // `TenantSubscription` and `TenantEntitlementGrant` both carry a `TenantId`, and that is exactly why this
  // is worth asserting: they LOOK tenant-owned. The tenant is the **subject** of the agreement, never its
  // owner (`DEC-SUB-0002`) — the rows are platform-administered commercial records about a tenant, and a
  // tenant cannot read, still less write, its own.
  [Fact]
  public void No_commercial_type_is_tenant_owned()
  {
    var owned = CommercialTypes
      .Where(type => typeof(ITenantOwnedEntity).IsAssignableFrom(type))
      .Select(type => type.Name)
      .ToList();

    Assert.True(
      owned.Count == 0,
      "A commercial type marked ITenantOwnedEntity would be swept into the Shared→Dedicated copy manifest " +
      $"automatically, because TenantCutoverCopyPlan.Build reflects over that interface: {string.Join(", ", owned)}. " +
      "DEC-SUB-0011 places the commercial plane outside cutover, and DEC-SUB-0002 makes the tenant the " +
      "subject of the agreement rather than its owner.");
  }

  // ---- AND NONE OF THEM IS IN THE TENANT ASSEMBLY'S REACH.
  //
  // A second, independent handle on the same property. The first would pass if someone kept the interface
  // off but registered the type on `TenantDbContext` anyway; this one asserts the types live where the
  // Platform context can see them and the tenant model cannot claim them by accident.
  [Fact]
  public void Every_commercial_type_lives_in_the_platform_domain_assembly()
  {
    var platformDomain = typeof(SubscriptionPlan).Assembly;

    Assert.All(CommercialTypes, type => Assert.Equal(platformDomain, type.Assembly));
    Assert.Equal("SSAS.Platform.Domain", platformDomain.GetName().Name);
  }

  // ---- THE APPEND-ONLY PAIR CARRIES NO CONCURRENCY STATE, BECAUSE IT IS NEVER UPDATED.
  //
  // `EmployeePositionAssignment` established the reasoning: "a record that is never updated has no
  // concurrency state to protect". A `RowVersion` on an append-only type is not merely redundant — it is an
  // invitation to the update `PreventAppendOnlyMutation` refuses, and it would make the type look editable
  // to anyone reading the model rather than the guard.
  [Fact]
  public void An_append_only_commercial_record_declares_no_rowversion_and_no_modified_columns()
  {
    Type[] appendOnly = [typeof(TenantSubscription), typeof(TenantEntitlementGrant)];

    foreach (var type in appendOnly)
    {
      Assert.True(
        typeof(IAppendOnlyEntity).IsAssignableFrom(type),
        $"{type.Name} must be IAppendOnlyEntity — OD-SUB-0008 ruled the history append-only, and " +
        "PlatformDbContext.PreventAppendOnlyMutation is what makes that real rather than decorative.");

      foreach (var forbidden in new[] { "RowVersion", "ModifiedUtc", "ModifiedBy" })
      {
        Assert.True(
          type.GetProperty(forbidden, BindingFlags.Instance | BindingFlags.Public) is null,
          $"{type.Name} declares {forbidden}, which an append-only record must not have: the row is never " +
          "updated, so there is no concurrency state to protect and no modification to record.");
      }
    }
  }

  // ---- `ADR-029`: NO SSAS TYPE MAY BE CAPABLE OF HOLDING CARDHOLDER DATA.
  //
  // Cheap to assert on this slice, so asserted rather than reported as unchecked. The ADR's decision 4 is
  // about capability, not intent — a property that *could* hold a PAN is the finding, whatever it is named
  // for. This covers the seven types shipped here; it is **not** a repository-wide guard, and `BR-SUB-0020`
  // binds the whole product.
  [Fact]
  public void No_commercial_type_declares_a_property_capable_of_holding_cardholder_data()
  {
    string[] forbidden =
      ["pan", "cardnumber", "primaryaccountnumber", "cvv", "cvc", "cardholder", "expiry", "expirationdate"];

    var offenders = CommercialTypes
      .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(property => (Type: type, property.Name)))
      .Where(entry => forbidden.Any(term =>
        entry.Name.Replace("_", string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)))
      .Select(entry => $"{entry.Type.Name}.{entry.Name}")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "ADR-029 decision 4: no SSAS type may be CAPABLE of holding a primary account number, card " +
      $"verification value, cardholder name or expiry date. Offenders: {string.Join(", ", offenders)}.");
  }
}

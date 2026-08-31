using System.Reflection;
using SSAS.Attendance.API;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.GL.API;
using SSAS.HR.API;
using SSAS.Payroll.API;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE ENABLEMENT SEAM IS COMPLETE, AND A FIFTH MODULE CANNOT SKIP IT BY OMISSION (FP-014).
// ==================================================================================================
//
// ---- WHAT THIS ASSERTS AND WHAT IT DELIBERATELY DOES NOT.
//
// These are SURFACE assertions — which types exist, how many, and in which assemblies. Reflection cannot
// read a method body, so nothing here can see that a route group actually calls `RequireModule`. That half
// is asserted where it can be: `ModuleEnablementCoverageTests` in `API.Tests` boots the real host and reads
// the mapped endpoint inventory, and it is the test that fails when a module route is added ungated.
//
// The split is the one `AppendOnlyEnforcementArchitectureTests` already uses: shape here, behaviour where
// behaviour can be exercised.
//
// ---- WHY THE COUNT IS FOUR AND NOT SEVENTEEN.
//
// `OD-SUB-0005` ruled the gateable unit is the thing carrying exactly one `IPermissionCatalogContributor`
// and one `Add*Module()` registration — today HR, Finance/GL, Payroll and Attendance. The product mounts
// seventeen route groups and HR alone mounts seven of them; gating on the route would need a second notion
// of "module" that disagreed with the permission catalog's, which is what that ruling excluded.
public sealed class ModuleEnablementArchitectureTests
{
  // The four gateable module API assemblies, named rather than discovered. A scan would pass vacuously if
  // it ever returned nothing, and naming them means a module moved to another assembly fails here on the
  // day it moves rather than silently dropping out of the count.
  private static readonly Assembly[] ModuleApiAssemblies =
  [
    typeof(HrModuleEnablement).Assembly,
    typeof(GlModuleEnablement).Assembly,
    typeof(PayrollModuleEnablement).Assembly,
    typeof(AttendanceModuleEnablement).Assembly,
  ];

  // ==================================================================================================
  // ⚠ THE NAMED LIST ABOVE COVERS EVERY MODULE THE BUILD SHIPS (item 171).
  // ==================================================================================================
  //
  // Naming the four assemblies is deliberate and the comment above says why: a scan returning nothing
  // would pass vacuously, and naming them catches a module MOVING on the day it moves. **That reasoning
  // is sound and this does not undo it** -- it closes the other direction, which naming cannot: a module
  // ADDED and never appended here would drop out silently, and every assertion over the list would stay
  // green while covering three modules out of five.
  //
  // "Which types count as a module" is a convention judgement, so it is not asked. "Which assemblies are
  // modules" is a fact about the layout -- a project under `src/Modules/` -- and that is what is asked.
  [Fact]
  public void Every_module_api_assembly_the_build_ships_is_named_in_the_list()
  {
    var shipped = DeployedProductAssemblies.ModuleProjectNames(".API");
    var named = DeployedProductAssemblies.NamesOf(ModuleApiAssemblies);

    Assert.NotEmpty(shipped);
    Assert.Empty(shipped.Except(named, StringComparer.Ordinal));
  }

  private static IEnumerable<Type> DescriptorsIn(Assembly assembly) =>
    assembly.GetTypes()
      .Where(type => type is { IsClass: true, IsAbstract: false })
      .Where(typeof(IModuleEnablementDescriptor).IsAssignableFrom);

  // ---- EVERY GATEABLE MODULE DECLARES EXACTLY ONE KEY.
  //
  // Exactly one, not at least one: two descriptors in a module assembly would mean two keys for one
  // `IPermissionCatalogContributor`, which is the seventeen-route-groups mistake arriving by a different
  // door.
  [Fact]
  public void Every_gateable_module_declares_exactly_one_module_key()
  {
    foreach (var assembly in ModuleApiAssemblies)
    {
      var descriptors = DescriptorsIn(assembly).ToList();

      Assert.True(
        descriptors.Count == 1,
        $"{assembly.GetName().Name} must declare exactly one IModuleEnablementDescriptor, found " +
        $"{descriptors.Count}: {string.Join(", ", descriptors.Select(type => type.Name))}. " +
        "OD-SUB-0005 makes the module the gateable unit, so one module means one key.");
    }
  }

  // ---- THE KEYS ARE PRESENT, NON-EMPTY AND DISTINCT.
  //
  // Two modules sharing a key would make one tenant's entitlement to either grant both, silently, and no
  // other test would notice — the routes would still be gated and the gate would still answer.
  [Fact]
  public void Module_keys_are_non_empty_and_distinct()
  {
    var keys = ModuleApiAssemblies
      .SelectMany(DescriptorsIn)
      .Select(type => ((IModuleEnablementDescriptor)Activator.CreateInstance(type)!).ModuleKey)
      .ToList();

    Assert.Equal(4, keys.Count);
    Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
    Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
  }

  // ---- "ALL MODULES" MEANS ALL OF THEM, AND THIS IS WHAT KEEPS IT TRUE (FP-014, `DEC-L-034`, T-041).
  //
  // The 14-day trial is an ALL-MODULE plan, and `Platform.Domain` cannot reference a module assembly — so
  // `TrialSubscription.ModuleKeys` writes the four keys down a second time. Two lists of the same thing
  // agree until someone edits one.
  //
  // **The drift this catches is silent and expensive**: a fifth module added to the product would simply
  // be absent from every trial, so every new tenant would evaluate one module and find nothing, with no
  // error anywhere and nothing to notice until a customer asks why a feature they were shown is missing.
  //
  // Ordinal and set-based, because these are keys rather than prose: the ORDER of the two lists is not a
  // fact about anything, but their CONTENT is.
  [Fact]
  public void The_trials_module_list_is_exactly_the_set_the_product_declares()
  {
    var declared = ModuleApiAssemblies
      .SelectMany(DescriptorsIn)
      .Select(type => ((IModuleEnablementDescriptor)Activator.CreateInstance(type)!).ModuleKey)
      .ToHashSet(StringComparer.Ordinal);

    var granted = TrialSubscription.ModuleKeys.ToHashSet(StringComparer.Ordinal);

    Assert.NotEmpty(declared);
    Assert.True(
      declared.SetEquals(granted),
      "TrialSubscription.ModuleKeys must equal the set of module keys the product declares. Declared: " +
      $"{string.Join(", ", declared.Order(StringComparer.Ordinal))}. Granted by the trial: " +
      $"{string.Join(", ", granted.Order(StringComparer.Ordinal))}. DEC-L-034 makes the trial an " +
      "ALL-MODULE plan, so a module missing here is a module no trial tenant can reach.");
  }

  // And the human-facing catalog covers the same keys: a granted module with no definition is a name the
  // database cannot explain, which is what a plan list would have to render.
  [Fact]
  public void The_trials_module_catalog_covers_every_key_it_grants()
  {
    var catalogued = TrialSubscription.ModuleCatalog
      .Select(entry => entry.Key)
      .ToHashSet(StringComparer.Ordinal);

    Assert.True(catalogued.SetEquals(TrialSubscription.ModuleKeys.ToHashSet(StringComparer.Ordinal)));
    Assert.All(
      TrialSubscription.ModuleCatalog,
      entry => Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName)));
  }

  // ---- NO PLATFORM-PLANE ASSEMBLY DECLARES A KEY (REQ-SUB-0013).
  //
  // Platform-plane routes are never subject to module enablement: authentication, tenant selection,
  // refresh, logout, platform support, localization, identity/access and company. A tenant whose
  // subscription has lapsed must still be able to authenticate and be re-enabled — a gate on the platform
  // plane would make a commercial lapse unrecoverable without database surgery.
  //
  // Declaring a key is the first step to being gated, so the absence is asserted here rather than only in
  // the endpoint-coverage test.
  [Fact]
  public void No_platform_plane_assembly_declares_a_module_key()
  {
    var platformApi = typeof(SSAS.Platform.API.ServiceCollectionExtensions).Assembly;

    var offenders = DescriptorsIn(platformApi).Select(type => type.Name).ToList();

    Assert.True(
      offenders.Count == 0,
      "Platform-plane assemblies must not declare a module enablement key (REQ-SUB-0013): " +
      $"{string.Join(", ", offenders)}. Gating the platform plane would leave a lapsed tenant unable to " +
      "authenticate, and therefore unable to be re-enabled.");
  }

  // ==================================================================================================
  // EXACTLY ONE ENTITLEMENT IMPLEMENTATION — SO THE NEXT TASK REPLACES IT RATHER THAN ADDING BESIDE IT.
  // ==================================================================================================
  //
  // Today that one implementation is `TenantModuleEntitlement`, which resolves the per-tenant assignment
  // from the Platform database (`OD-SUB-0004`). It REPLACED the transitional grant-everything resolver in
  // T-040 rather than being added beside it, and that type is deleted.
  //
  // **This test is what stops it being added BESIDE the transitional one.** Two registered implementations
  // would leave the container's last-wins ordering deciding whether entitlement is real — a difference
  // invisible in every test that does not assert a refusal, which today is all of them.
  [Fact]
  public void Exactly_one_entitlement_implementation_exists()
  {
    var assemblies = ModuleApiAssemblies
      .Append(typeof(ITenantModuleEntitlement).Assembly)
      .Append(typeof(SSAS.Platform.API.ServiceCollectionExtensions).Assembly)
      .Append(typeof(SSAS.Platform.Infrastructure.Persistence.PlatformDbContext).Assembly)
      .Distinct();

    var implementations = assemblies
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type is { IsClass: true, IsAbstract: false })
      .Where(typeof(ITenantModuleEntitlement).IsAssignableFrom)
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      implementations.Count == 1,
      "Exactly one ITenantModuleEntitlement implementation must exist. Found " +
      $"{implementations.Count}: {string.Join(", ", implementations)}. The transitional resolver is to be " +
      "REPLACED when subscription data arrives, not competed with — two implementations would let " +
      "container ordering decide whether entitlement is enforced.");

    Assert.Equal(typeof(SSAS.Platform.API.Subscriptions.TenantModuleEntitlement).FullName, implementations[0]);
  }
}

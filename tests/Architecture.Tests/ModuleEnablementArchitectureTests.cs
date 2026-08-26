using System.Reflection;
using SSAS.Attendance.API;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.GL.API;
using SSAS.HR.API;
using SSAS.Payroll.API;

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

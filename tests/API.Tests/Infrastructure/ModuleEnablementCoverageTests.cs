using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.API.Tests.Infrastructure;
using SSAS.Attendance.API;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.GL.API;
using SSAS.HR.API;
using SSAS.Payroll.API;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE COMPLETENESS GUARD: EVERY MODULE ROUTE IS GATED, AND NO PLATFORM ROUTE IS (FP-014).
// ==================================================================================================
//
// ---- THIS IS THE TEST THAT FAILS WHEN A MODULE ROUTE IS ADDED UNGATED.
//
// `ModuleEnablementArchitectureTests` asserts the SHAPE — four module keys, one entitlement
// implementation, no key on the platform plane. Reflection cannot read a method body, so nothing there can
// see whether a route group actually called `RequireModule`.
//
// This one can, because it boots the real host and reads the endpoint inventory the Host actually mapped.
// A new route group added to HR, or a fifth module mounted without the gate, fails here on the day it is
// written. That is the guarantee `OD-SUB-0003` was ruled for: the seam ships before the next module so the
// next module cannot be ungated by omission.
//
// ---- HOW AN ENDPOINT'S OWNER IS DETERMINED, AND WHY NOT BY ROUTE PREFIX.
//
// Each endpoint is attributed to the assembly declaring its HANDLER, read from the `MethodInfo` minimal
// APIs place in endpoint metadata. **Not by route prefix** — `OD-SUB-0005` ruled the unit is the module,
// and matching on `/api/hr/...` would introduce exactly the second, divergent notion of "module" that
// ruling excluded. It would also be wrong in a way that is easy to miss: HR mounts seven prefixes.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class ModuleEnablementCoverageTests(HostWebApplicationFactory factory)
{
  private static readonly HashSet<Assembly> ModuleApiAssemblies =
  [
    typeof(HrModuleEnablement).Assembly,
    typeof(GlModuleEnablement).Assembly,
    typeof(PayrollModuleEnablement).Assembly,
    typeof(AttendanceModuleEnablement).Assembly,
  ];

  private IReadOnlyList<Endpoint> Endpoints() =>
    [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints];

  private static Assembly? OwnerOf(Endpoint endpoint) =>
    endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly;

  private static string Describe(Endpoint endpoint) =>
    endpoint is RouteEndpoint route ? route.RoutePattern.RawText ?? endpoint.DisplayName ?? "?"
      : endpoint.DisplayName ?? "?";

  // ---- THE SCAN MUST FIND SOMETHING, OR EVERY ASSERTION BELOW IS VACUOUS.
  //
  // A host that mapped nothing, or a metadata change that stopped exposing `MethodInfo`, would make the two
  // tests below pass while asserting nothing at all. This is the tripwire for that.
  [Fact]
  public void The_endpoint_inventory_covers_all_four_modules()
  {
    var owners = Endpoints()
      .Select(OwnerOf)
      .Where(assembly => assembly is not null && ModuleApiAssemblies.Contains(assembly))
      .Distinct()
      .ToList();

    Assert.Equal(4, owners.Count);
  }

  // ---- EVERY MODULE-OWNED ROUTE CARRIES THE GATE.
  [Fact]
  public void Every_module_owned_endpoint_is_gated()
  {
    var ungated = Endpoints()
      .Where(endpoint => OwnerOf(endpoint) is { } owner && ModuleApiAssemblies.Contains(owner))
      .Where(endpoint => endpoint.Metadata.GetMetadata<ModuleEnablementMetadata>() is null)
      .Select(Describe)
      .OrderBy(route => route, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      ungated.Count == 0,
      $"{ungated.Count} module-owned endpoint(s) are not behind the enablement gate: " +
      $"{string.Join(", ", ungated)}. Apply RequireModule(<Module>ModuleEnablement.Key) to the route " +
      "GROUP — on the group rather than each route, so a route added later cannot forget it.");
  }

  // ---- AND EVERY ONE OF THEM CARRIES ITS OWN MODULE'S KEY.
  //
  // Coverage alone would be satisfied by HR's routes declaring Payroll's key. Once real assignment data
  // exists that would entitle the wrong tenants to the wrong module, and nothing else in the suite would
  // notice, because the routes would still be gated and the gate would still answer.
  [Fact]
  public void Every_module_owned_endpoint_declares_its_own_module_key()
  {
    var expected = ModuleApiAssemblies.ToDictionary(
      assembly => assembly,
      assembly => ((IModuleEnablementDescriptor)Activator.CreateInstance(
        assembly.GetTypes().Single(type =>
          type is { IsClass: true, IsAbstract: false } &&
          typeof(IModuleEnablementDescriptor).IsAssignableFrom(type)))!).ModuleKey);

    var mismatched = Endpoints()
      .Where(endpoint => OwnerOf(endpoint) is { } owner && ModuleApiAssemblies.Contains(owner))
      .Select(endpoint => (Endpoint: endpoint, Owner: OwnerOf(endpoint)!,
        Declared: endpoint.Metadata.GetMetadata<ModuleEnablementMetadata>()?.ModuleKey))
      .Where(entry => entry.Declared is not null && entry.Declared != expected[entry.Owner])
      .Select(entry => $"{Describe(entry.Endpoint)} declares '{entry.Declared}', expected " +
        $"'{expected[entry.Owner]}'")
      .ToList();

    Assert.True(
      mismatched.Count == 0,
      "A route must carry its OWN module's key: " + string.Join("; ", mismatched));
  }

  // ---- AND NO PLATFORM-PLANE ROUTE IS GATED (REQ-SUB-0013).
  //
  // The seven exempt groups — Host, authentication, support authentication, localization, identity/access,
  // support authority and company — are exactly the surface that must stay reachable so a tenant whose
  // subscription has lapsed can still authenticate and be re-enabled. Gating any of them would make a
  // commercial lapse unrecoverable without database surgery.
  //
  // This is the half of the guard that a well-meaning "gate everything" change would break, and it is why
  // the assertion is two-sided rather than one.
  [Fact]
  public void No_platform_plane_endpoint_is_gated()
  {
    var gated = Endpoints()
      .Where(endpoint => OwnerOf(endpoint) is { } owner && !ModuleApiAssemblies.Contains(owner))
      .Where(endpoint => endpoint.Metadata.GetMetadata<ModuleEnablementMetadata>() is not null)
      .Select(Describe)
      .OrderBy(route => route, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      gated.Count == 0,
      $"{gated.Count} platform-plane endpoint(s) are behind the module enablement gate, which " +
      $"REQ-SUB-0013 forbids: {string.Join(", ", gated)}. A tenant whose subscription has lapsed must " +
      "still be able to authenticate.");
  }
}

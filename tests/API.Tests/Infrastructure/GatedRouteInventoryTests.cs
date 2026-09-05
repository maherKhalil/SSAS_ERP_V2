namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE CONTROLS ON `GatedRouteInventory`. NO CRITERION IS CITED HERE AND NONE SHOULD BE.
// ==================================================================================================
//
// **An instrument that silently returns less than it should makes every claim built on it weaker in the
// reassuring direction.** These assert that the enumerator still sees what it is supposed to see. *They are
// about the instrument; the product is not the subject.*
//
// ⚠⚠ AND THEY DELIBERATELY DO NOT PIN THE ROUTE SET. An exact set of gated routes would redden every time
// any module adds an endpoint — **high rate, and OFF-SUBJECT for an instrument whose job is to enumerate
// rather than to police.** *That is the pair that decides whether a strict guard survives, applied here to
// my own instrument rather than to somebody else's guard.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class GatedRouteInventoryTests(HostWebApplicationFactory factory)
{
  [Fact]
  public void The_inventory_sees_every_module_and_routes_it_can_name()
  {
    var routes = GatedRouteInventory.Of(factory);

    // ---- CONTROL ONE: THE POPULATION IS NOT EMPTY AND SPANS ALL FOUR MODULES.
    //
    // `NotEmpty` alone would catch "the host mapped nothing" — vivid and rare — and miss "one module's
    // routes stopped being seen", which is dull, common, and would silently shrink any denominator taken
    // from here. Binding the KEY SET names the population instead.
    Assert.Equal(
      ["Attendance", "Finance.GL", "HR", "Payroll"],
      routes.Select(route => route.ModuleKey).Distinct().OrderBy(key => key, StringComparer.Ordinal));

    // ---- CONTROL TWO: KNOWN ROUTES ARE PRESENT, SO A ZERO IS ABOUT THE TREE AND NOT THE MATCHER.
    //
    // Two modules named, not one: a walk that lost a single assembly still satisfies a one-module check.
    Assert.Contains(routes, route => route.Pattern.Contains("/api/hr/employees", StringComparison.Ordinal));
    Assert.Contains(routes, route => route.Pattern.Contains("/api/gl", StringComparison.Ordinal));

    // ---- CONTROL THREE: EVERY ENTRY IS USABLE AS DATA.
    //
    // An entry whose pattern fell back to "?" is a row that would silently poison any comparison made
    // against the population later.
    Assert.DoesNotContain(routes, route => route.Pattern == "?" || route.Owner == "?");
  }
}

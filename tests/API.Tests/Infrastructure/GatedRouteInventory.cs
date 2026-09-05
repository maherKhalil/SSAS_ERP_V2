using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Attendance.API;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.GL.API;
using SSAS.HR.API;
using SSAS.Payroll.API;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE ENUMERATED GATED-ROUTE POPULATION. AN INSTRUMENT, NOT A WITNESS.
// ==================================================================================================
//
// **This carries no criterion trait and asserts nothing about the product.** It exists because three
// separate questions have wanted a closed set of gated routes and each one re-derived it privately.
//
// ---- ⚠⚠⚠ THE DEFINITION, WRITTEN OUT SO THE POPULATION OUTLIVES ANY ONE INSTRUMENT.
//
// *A number whose only derivation is a script is retired the day the script is lost.* So, in prose:
//
//   **A GATED ROUTE is an endpoint in the RUNNING HOST'S `EndpointDataSource` that (a) is a
//   `RouteEndpoint`, (b) whose handler's declaring type belongs to one of the FOUR module API assemblies
//   — HR, GL, Payroll, Attendance — and (c) carries `ModuleEnablementMetadata` in its metadata.**
//
// **The module key is read from that metadata, never parsed from the route text.** The owner is read from
// the endpoint's `MethodInfo` metadata, never inferred from the path — *`/api/hr/...` is a naming
// convention and conventions are not evidence.*
//
// ---- ⚠⚠⚠ THE UNIT IS AN **ENDPOINT**, NOT A PATH, AND THE TWO NUMBERS DIFFER BY TWENTY-FIVE.
//
// Measured at f0adfa6: ***116 GATED ENDPOINTS ACROSS 91 DISTINCT PATTERNS.***
//
//     Attendance   27 endpoints / 21 patterns
//     Finance.GL   21 endpoints / 17 patterns
//     HR           46 endpoints / 36 patterns
//     Payroll      22 endpoints / 17 patterns
//
// **A pattern repeats when several HTTP methods share it** — `/api/attendance/calendars` is one path and
// two endpoints. ***SO A DENOMINATOR TAKEN FROM HERE MUST SAY WHICH UNIT IT MEANS: "every gated route" is
// 116 or 91 depending on a choice nobody would notice being made.*** *`AC-SUB-0019` says "every gated route
// of every module", and the refusal is produced per REQUEST, so the endpoint count is the right one for it
// — but that is an argument, not an obvious reading, and it belongs beside the number.*
//
// ---- ⚠⚠ HOW THE POPULATION IS CLOSED, WHICH IS THE PART THAT MAKES IT USABLE AS A DENOMINATOR.
//
// ***IT IS THE LIVE ROUTE TABLE OF THE BUILT HOST, NOT A SOURCE SCAN.*** A source scan over `MapGet(...)`
// literals is open in two directions at once: it misses routes composed from constants — measured earlier
// tonight, **13 of 15 group prefixes in this tree live in `RoutePrefix` constants and are invisible to a
// literal matcher** — and it counts routes that are written but never registered. **The host's endpoint
// table has neither problem: a route is in it if and only if it ships.**
//
// ⚠ WHAT THAT CLOSURE COSTS, STATED BECAUSE A DENOMINATOR'S LIMITS TRAVEL WITH IT: this population is
// exactly *what the host registers under the test configuration*. A route mapped only under some other
// configuration is outside it, and this instrument cannot see that it exists.
//
// ---- ⚠ RELATIONSHIP TO `ModuleEnablementCoverageTests`, WHICH ALREADY SCANS ENDPOINTS.
//
// **That file's scan is private to it and counts owner ASSEMBLIES; this enumerates ROUTES with their keys.**
// *It is the same mechanism at a different granularity* — its
// `The_endpoint_scan_finds_all_four_modules_which_is_what_stops_the_gating_test_below_being_vacuous` is the
// anti-vacuity control for its own assertions, and the control below is this one's. **Neither is a
// substitute for the other and no assertion here duplicates one there.**
public static class GatedRouteInventory
{
  private static readonly HashSet<Assembly> ModuleApiAssemblies =
  [
    typeof(HrModuleEnablement).Assembly,
    typeof(GlModuleEnablement).Assembly,
    typeof(PayrollModuleEnablement).Assembly,
    typeof(AttendanceModuleEnablement).Assembly,
  ];

  public sealed record GatedRoute(string Pattern, string ModuleKey, string Owner);

  public static IReadOnlyList<GatedRoute> Of(HostWebApplicationFactory factory)
  {
    ArgumentNullException.ThrowIfNull(factory);

    return
    [
      .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
        .OfType<RouteEndpoint>()
        .Where(endpoint => OwnerOf(endpoint) is { } owner && ModuleApiAssemblies.Contains(owner))
        .Select(endpoint => new
        {
          Endpoint = endpoint,
          Key = endpoint.Metadata.GetMetadata<ModuleEnablementMetadata>()?.ModuleKey,
        })
        .Where(entry => entry.Key is not null)
        .Select(entry => new GatedRoute(
          entry.Endpoint.RoutePattern.RawText ?? entry.Endpoint.DisplayName ?? "?",
          entry.Key!,
          OwnerOf(entry.Endpoint)!.GetName().Name ?? "?"))
        .OrderBy(route => route.ModuleKey, StringComparer.Ordinal)
        .ThenBy(route => route.Pattern, StringComparer.Ordinal),
    ];
  }

  private static Assembly? OwnerOf(Endpoint endpoint) =>
    endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly;
}

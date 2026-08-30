using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.API.Tests.Infrastructure;
using SSAS.Attendance.Application.Permissions;
using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.API.Tests.Attendance;

// ==================================================================================================
// ATTENDANCE'S ROUTE INVENTORY (T-099) — THE THIRD THING, DEFERRED DELIBERATELY IN T-077.
// ==================================================================================================
//
// T-077 built `AttendanceRoutePermissionTests` and said why an inventory was NOT part of it: *"An inventory
// catches an UNDOCUMENTED route; this catches an UNGUARDED one. They are different properties and the
// second is the one whose absence was measured."*
//
// **This is the first property, and it is timelier than it was.** Attendance gained two self-service routes
// in T-089, and T-098 showed what an out-of-date surface costs — a nineteen-row table that omitted four
// live routes for the whole of FP-011, asserting something false by omission.
//
// ---- IT FOLLOWS `PayrollRouteInventoryTests`, AND THE CHOICE IS DELIBERATE.
//
// HR, GL and Payroll each have one. **Payroll's is the only one whose three properties are three separately
// NAMED tests**, so a failure says which property broke rather than "the inventory differs". HR's folds the
// permission check into a 213-line file and GL's is close but thinner on the self-service reason.
//
// **And Payroll's carries its self-service row with the reason inline**, which is exactly what Attendance
// needs: the two `/me/` routes carry SELF permissions that share a prefix with the administrative ones and
// nothing else.
//
// ---- IT ENUMERATES FROM THE REAL HOST.
//
// Attendance's own harness registers only what the self-service routes need (T-089), so it is the wrong
// instrument for a whole-surface claim. `HostWebApplicationFactory` wraps the real `Program`, which is what
// `AttendanceRoutePermissionTests` already uses.
//
// **The "every route requires a permission" property is NOT repeated here.** It lives in that file, it is
// asserted against the same enumeration, and a second copy would be two places to edit and one place to
// forget.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class AttendanceRouteInventoryTests(HostWebApplicationFactory factory)
{
  private const string RoutePrefix = "/api/attendance";

  // ---- PINNED BY NAME, NOT BY COUNT.
  //
  // A count alone passes when one route is removed and another added — T-077's swap argument. Naming every
  // route means a change to the surface has to be acknowledged here, which is where a reviewer sees it.
  private static readonly (string Method, string Pattern, string Permission)[] Expected =
  [
    // ---- WORKING CALENDAR. Structural configuration, not personal data.
    ("POST", "/api/attendance/calendars", AttendancePermissionNames.ManageCalendars),
    ("GET", "/api/attendance/calendars", AttendancePermissionNames.ViewCalendars),
    ("PUT", "/api/attendance/calendars/{workingCalendarId}", AttendancePermissionNames.ManageCalendars),
    ("POST", "/api/attendance/calendars/{workingCalendarId}/holidays", AttendancePermissionNames.ManageCalendars),
    ("POST", "/api/attendance/calendars/{workingCalendarId}/holidays/remove", AttendancePermissionNames.ManageCalendars),
    ("GET", "/api/attendance/calendars/working-days", AttendancePermissionNames.ViewCalendars),

    // ---- PERIODS. Closing and reopening carry their own permission, separate from creating.
    ("POST", "/api/attendance/periods", AttendancePermissionNames.ManagePeriods),
    ("GET", "/api/attendance/periods", AttendancePermissionNames.ViewPeriods),
    ("POST", "/api/attendance/periods/{attendancePeriodId}/close", AttendancePermissionNames.ClosePeriods),
    ("POST", "/api/attendance/periods/{attendancePeriodId}/reopen", AttendancePermissionNames.ClosePeriods),

    // ---- RECORDS. `OD-ATT-0012` ruled adjustments-never-edits, so a correction is a POST to a
    // ---- sub-resource rather than a PUT. `No_attendance_record_responds_to_put_or_delete` asserts it.
    ("POST", "/api/attendance/records", AttendancePermissionNames.ManageRecords),
    ("GET", "/api/attendance/records", AttendancePermissionNames.ViewRecords),
    ("POST", "/api/attendance/records/{attendanceRecordId}/adjustments", AttendancePermissionNames.ManageRecords),

    // ---- LEAVE TYPES. A catalog, permissioned separately from the leave it classifies.
    ("POST", "/api/attendance/leave-types", AttendancePermissionNames.ManageLeaveTypes),
    ("GET", "/api/attendance/leave-types", AttendancePermissionNames.ViewLeaveTypes),
    ("PUT", "/api/attendance/leave-types/{leaveTypeId}", AttendancePermissionNames.ManageLeaveTypes),
    ("POST", "/api/attendance/leave-types/{leaveTypeId}/activate", AttendancePermissionNames.ManageLeaveTypes),
    ("POST", "/api/attendance/leave-types/{leaveTypeId}/deactivate", AttendancePermissionNames.ManageLeaveTypes),

    // ---- LEAVE REQUESTS. Approving and rejecting carry `ApproveLeave`; submitting and cancelling do not.
    ("POST", "/api/attendance/leave-requests", AttendancePermissionNames.ManageLeave),
    ("GET", "/api/attendance/leave-requests", AttendancePermissionNames.ViewLeave),
    ("POST", "/api/attendance/leave-requests/{leaveRequestId}/approve", AttendancePermissionNames.ApproveLeave),
    ("POST", "/api/attendance/leave-requests/{leaveRequestId}/reject", AttendancePermissionNames.ApproveLeave),
    ("POST", "/api/attendance/leave-requests/{leaveRequestId}/cancel", AttendancePermissionNames.ManageLeave),

    // ---- BALANCES. Entitlement is settable; consumed is not (`AC-ATT-0040`).
    ("PUT", "/api/attendance/leave-balances", AttendancePermissionNames.ManageLeave),
    ("GET", "/api/attendance/leave-balances", AttendancePermissionNames.ViewLeave),

    // ---- SELF-SERVICE (FP-015, T-089). TWO ROUTES, TWO PERMISSIONS, AND THAT IS THE POINT.
    //
    // They carry the SELF permissions, which share a prefix with the administrative ones and nothing else.
    // **A single `Attendance.ViewOwn` would be a widening wearing the costume of a simplification** —
    // granting sight of one's own attendance would silently grant sight of one's own leave, which the
    // administrative plane treats as a separate decision.
    //
    // `TS-SS-0013` asserts neither substitutes for the other at runtime; this row pins the pairing.
    ("GET", "/api/attendance/me/records", AttendancePermissionNames.ViewOwnRecords),
    ("GET", "/api/attendance/me/leave-requests", AttendancePermissionNames.ViewOwnLeave)
  ];

  [Fact]
  public void The_attendance_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = Routes()
      .Select(route => (Method: FirstMethodOf(route), Pattern: route.RoutePattern.RawText!))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    // NOT VACUOUS. A prefix filter that stopped matching — a mount point renamed, the module unregistered —
    // would leave an empty set on both sides of an `Assert.Equal` that compared it to an empty expectation.
    Assert.NotEmpty(actual);

    var expected = Expected
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void Every_route_requires_the_permission_the_inventory_names()
  {
    // ---- THE PROPERTY THE SET COMPARISON ABOVE CANNOT SEE.
    //
    // A route can be present, named correctly, and gated on the WRONG permission — `ViewLeave` where
    // `ApproveLeave` belongs would satisfy every assertion above while handing approval to any reader.
    var actual = Routes().ToDictionary(
      route => $"{FirstMethodOf(route)} {route.RoutePattern.RawText}",
      route => route.Metadata.GetMetadata<IAuthorizeData>()?.Policy ?? string.Empty,
      StringComparer.Ordinal);

    foreach (var (method, pattern, permission) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");
      Assert.Equal($"{PermissionPolicyNames.TenantPrefix}{permission}", actual[key]);
    }
  }

  // ================================================================================================
  // TWO PERMISSIONS ARE ENFORCED IN A HANDLER AND APPEAR IN NO ROW. THAT IS RECORDED, NOT SILENT.
  // ================================================================================================
  //
  // `Attendance.Leave.ViewSensitive` and `Attendance.Leave.ApproveAtRoot` are declared, catalogued and
  // **required by no route** — and they authorise something real:
  //
  //   ViewSensitive    `AttendanceReadService` — per-row redaction of a sensitive leave TYPE
  //   ApproveAtRoot    `LeaveApprovalRouter`   — approving a request that has reached the root
  //
  // **An inventory keyed on route-to-permission would imply they are missing.** They are not: enforcement
  // inside a handler is a deliberate shape, because both are decisions about PART of a response or PART of
  // a flow rather than about reaching an endpoint. A route gate can only refuse the whole request.
  //
  // This test exists so a reader comparing the inventory against
  // `AttendancePermissionCatalogContributor` finds an explanation rather than a discrepancy — and so that
  // a THIRD such permission has to be added here by a person, who will meet the reasoning.
  [Fact]
  public void The_two_permissions_enforced_in_a_handler_are_named_rather_than_missing()
  {
    string[] enforcedInHandler =
    [
      AttendancePermissionNames.ApproveLeaveAtRoot,
      AttendancePermissionNames.ViewSensitiveLeave
    ];

    var routed = Expected.Select(route => route.Permission).Distinct(StringComparer.Ordinal).ToArray();

    // Neither is route-gated, which is the claim this test is making. If one ever becomes a route's
    // permission, this fails and the entry above has to go — the two lists cannot both be right.
    Assert.Empty(enforcedInHandler.Intersect(routed, StringComparer.Ordinal));

    // AND THEY ARE CATALOGUED, which is what makes them grantable and therefore real. A permission no
    // catalog defines refuses every caller (FP-006P); one enforced in a handler and never catalogued would
    // be worse — it would refuse silently, inside a response.
    var catalogued = new AttendancePermissionCatalogContributor()
      .Permissions.Select(permission => permission.Name).ToArray();

    Assert.All(enforcedInHandler, permission => Assert.Contains(permission, catalogued, StringComparer.Ordinal));
  }

  // ---- THE ABSENT VERBS ARE THE RULING MADE VISIBLE IN THE SURFACE.
  //
  // `OD-ATT-0012` ruled adjustments-never-edits and `AttendanceRecord` is `IAppendOnlyEntity`, so a
  // correction is a POST to `/adjustments`. `TS-ATT-0024` asserts the verbs answer 405; this asserts they
  // are not MAPPED, which is the stronger claim — a route could answer 405 for a reason unrelated to the
  // ruling.
  [Fact]
  [Trait("Decision", "OD-ATT-0012")]
  public void No_attendance_record_responds_to_put_or_delete()
  {
    var mutating = Routes()
      .Where(route => route.RoutePattern.RawText!.Contains("/records", StringComparison.Ordinal))
      .Where(route => FirstMethodOf(route) is "PUT" or "DELETE")
      .Select(route => $"{FirstMethodOf(route)} {route.RoutePattern.RawText}")
      .ToArray();

    Assert.Empty(mutating);
  }

  // ---- AND THE WHOLE MODULE HAS NO DELETE, LIKE PAYROLL AND UNLIKE GL.
  //
  // GL has one destructive route — discarding a draft, which was never part of the ledger. Attendance has
  // none: a record is append-only, a leave request is cancelled rather than removed, and a leave type
  // deactivates. Removing a holiday is a POST to `/holidays/remove`, which is the product's spelling for a
  // named administrative act.
  [Fact]
  public void No_attendance_route_responds_to_delete() =>
    Assert.Empty(Routes().Where(route => FirstMethodOf(route) == "DELETE"));

  private RouteEndpoint[] Routes() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
      .OfType<RouteEndpoint>()
      .Where(endpoint =>
        endpoint.RoutePattern.RawText?.StartsWith(RoutePrefix, StringComparison.Ordinal) ?? false)
  ];

  private static string FirstMethodOf(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }
}

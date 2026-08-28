using System.Net;
using SSAS.API.Tests.Infrastructure;
using SSAS.Attendance.Application.Permissions;

namespace SSAS.API.Tests.Attendance;

// ==================================================================================================
// FP-015's SECOND VERTICAL SLICE: AN EMPLOYEE READS THEIR OWN ATTENDANCE AND LEAVE (T-089).
// ==================================================================================================
//
// Payroll shipped one self route in T-088. Attendance ships TWO, because the module permissions records and
// leave separately, and every criterion below is asserted on both.
//
//   TS-SS-0013   the two self permissions DO NOT SUBSTITUTE FOR EACH OTHER — the criterion this slice
//                exists to make true, and the one a single `Attendance.ViewOwn` would have made false
//   AC-SS-0005   the administrative permission alone is refused on a self route
//   AC-SS-0007   the contract names no employee on any surface (`SelfServiceContractRule`)
//   AC-SS-0008   an unmapped caller gets 404 `attendance.no_linked_employee`, asserted as a STATUS
//
// ---- EVERY POSITIVE CASE ASSERTS WHAT THE HANDLER PASSED DOWN, NOT MERELY THAT IT RETURNED 200.
//
// A handler that resolved nobody and read a hard-coded employee would return 200 with the stub's rows. So
// each success asserts the EMPLOYEE and the SCOPE the read service was handed — those carry the guarantee,
// and they are the only place it is observable.
[Collection(AttendanceApiEndpointGroup.Name)]
public sealed class AttendanceSelfServiceTests : IClassFixture<AttendanceApiTestHost>
{
  private const string RecordsRoute = "/api/attendance/me/records";
  private const string LeaveRoute = "/api/attendance/me/leave-requests";

  private readonly AttendanceApiTestHost host;

  public AttendanceSelfServiceTests(AttendanceApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ================================================================================================
  // TS-SS-0013 — NEITHER SELF PERMISSION SUBSTITUTES FOR THE OTHER, ASSERTED BOTH DIRECTIONS.
  // ================================================================================================
  //
  // **This is the criterion that decides whether the two-permission split was real.** A single
  // `Attendance.ViewOwn` would pass every other test in this file while making both of these fail, which is
  // exactly why a coarser permission is a WIDENING wearing the costume of a simplification.
  //
  // Both directions, because they are different failures: records-opens-leave discloses a leave history to
  // someone granted only a timesheet, and leave-opens-records the reverse. A guard asserting one direction
  // would be half a guard, and the missing half would be discovered by whichever one shipped broken.
  [Fact]
  [Trait("Criterion", "TS-SS-0013")]
  public async Task The_records_self_permission_does_not_open_the_leave_route()
  {
    var response = await Send(LeaveRoute, AttendancePermissionNames.ViewOwnRecords);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    // The refusal happened at the door, so the read service was never reached. Without this, a handler that
    // ran and returned an empty list would look identical from the status code alone.
    Assert.Empty(host.Reads.LeaveForEmployee);
  }

  [Fact]
  [Trait("Criterion", "TS-SS-0013")]
  public async Task The_leave_self_permission_does_not_open_the_records_route()
  {
    var response = await Send(RecordsRoute, AttendancePermissionNames.ViewOwnLeave);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Empty(host.Reads.RecordsForEmployee);
  }

  // ---- THE POSITIVE CASE FOR RECORDS, AND ITS SCOPE.
  //
  // The self permission ALONE — no administrative permission, no company-access grant beyond the caller's
  // own employee — reads the caller's own records. **And the scope carries the employee's own BRANCH**,
  // which is the dimension `ResolveForOwnLeaveAsync` deliberately does not carry: records are branch-owned
  // (`OD-ATT-0011`) and leave is not.
  [Fact]
  [Trait("Criterion", "REQ-SS-0004")]
  public async Task The_records_self_permission_alone_reads_the_callers_own_records()
  {
    var response = await Send(RecordsRoute, AttendancePermissionNames.ViewOwnRecords);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var call = Assert.Single(host.Reads.RecordsForEmployee);

    // The SUBJECT came from the link, not from anywhere a caller could reach.
    Assert.Equal(AttendanceApiTestHost.EmployeeId, call.EmployeeId);
    Assert.Equal([2L], host.SelfService.AskedForUser);

    // The SCOPE came from that employee's own placement — company AND branch.
    Assert.Equal(AttendanceApiTestHost.TenantId, call.Scope.TenantId);
    Assert.Equal([AttendanceApiTestHost.CompanyA], call.Scope.CompanyIds);
    Assert.Equal([AttendanceApiTestHost.BranchId], call.Scope.BranchIds);
  }

  // ---- THE POSITIVE CASE FOR LEAVE, AND THE DIMENSION IT DOES NOT CARRY.
  //
  // Leave is company-owned and asserted NOT branch-owned, so the leave scope carries the sentinel branch the
  // module's own `ResolveCompanyOnlyAsync` uses — a value no leave query reads. Asserting it is what stops a
  // future edit quietly giving the leave route a real branch and filtering on a column the type lacks.
  [Fact]
  [Trait("Criterion", "REQ-SS-0004")]
  public async Task The_leave_self_permission_alone_reads_the_callers_own_leave()
  {
    var response = await Send(LeaveRoute, AttendancePermissionNames.ViewOwnLeave);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var call = Assert.Single(host.Reads.LeaveForEmployee);

    Assert.Equal(AttendanceApiTestHost.EmployeeId, call.EmployeeId);
    Assert.Equal([AttendanceApiTestHost.CompanyA], call.Scope.CompanyIds);
    Assert.Equal([Guid.Empty], call.Scope.BranchIds);
  }

  // ---- AC-SS-0005. THE ADMINISTRATIVE PERMISSION IS NOT A SUPERSET OF THE SELF ONE.
  //
  // A caller holding `Attendance.Records.View` can already read this employee through the administrative
  // route. That is not a reason to let it open the self route: the two answer different questions, and a
  // self route that accepted an administrative permission would make the self permission optional and its
  // absence unenforceable.
  [Theory]
  [Trait("Criterion", "AC-SS-0005")]
  [InlineData(RecordsRoute, "Attendance.Records.View")]
  [InlineData(LeaveRoute, "Attendance.Leave.View")]
  public async Task The_administrative_permission_alone_is_refused_on_a_self_route(
    string route, string administrative)
  {
    var response = await Send(route, administrative);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AC-SS-0008. AN UNMAPPED CALLER, ASSERTED AS A STATUS AND A CODE.
  //
  // **T-076's finding is why the status is asserted rather than "nothing threw":** an unmapped error falls
  // through the mapper with no exception and no log entry, so a test asserting absence of failure passes
  // against `500 request.failed`. The assertion is the code.
  [Theory]
  [Trait("Criterion", "AC-SS-0008")]
  [InlineData(RecordsRoute, "Attendance.Records.ViewOwn")]
  [InlineData(LeaveRoute, "Attendance.Leave.ViewOwn")]
  public async Task An_unlinked_caller_is_refused_with_the_named_condition(string route, string permission)
  {
    host.SelfService.LinkedEmployee = null;

    var response = await Send(route, permission);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("attendance.no_linked_employee", await AttendanceApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE DANGLING LINK, ANSWERING THE SAME WAY AND FOR A STATED REASON.
  //
  // A link naming an employee that no longer exists is reachable — `ADR-030` Decision 4 forbids the
  // cross-database foreign key that would prevent it. It collapses into the same refusal deliberately:
  // distinguishing it would tell the caller a link exists pointing at a record that does not, which is a
  // `BR-PLT-0002` disclosure with extra steps.
  //
  // **Asserted rather than merely commented, because the two paths are different code** — one returns before
  // the placement lookup and one after, and only a test proves they still agree.
  [Fact]
  [Trait("Criterion", "AC-SS-0008")]
  public async Task A_link_naming_an_employee_with_no_placement_is_refused_identically()
  {
    // The link resolves to an employee; the placement lookup finds nothing for it.
    host.SelfService.EmployeePlacement = null;

    var response = await Send(RecordsRoute, AttendancePermissionNames.ViewOwnRecords);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("attendance.no_linked_employee", await AttendanceApiTestHost.ProblemCodeAsync(response));

    // The placement WAS consulted — otherwise this test would pass against a resolver that never looked,
    // and the dangling-link path would be untested while appearing covered.
    Assert.NotEmpty(host.SelfService.AskedForPlacement);
    Assert.Empty(host.Reads.RecordsForEmployee);
  }

  // ---- AC-SS-0007 / TS-SS-0003, ON BOTH ROUTES, THROUGH THE SHARED RULE.
  //
  // `/me/records` binds `fromDate` and `toDate` and that is correct: a date range narrows a set the caller
  // is already authorized to see. **What the rule forbids is binding the SUBJECT** — see
  // `SelfServiceContractRule` for why the criterion had to be stated that way rather than as "binds
  // nothing", and why it lives in one place across the two modules.
  [Theory]
  [Trait("Criterion", "AC-SS-0007")]
  [InlineData(RecordsRoute)]
  [InlineData(LeaveRoute)]
  public void The_self_route_contract_names_no_employee_on_any_surface(string route) =>
    SelfServiceContractRule.AssertNoSubjectOnAnySurface(host.MappedEndpoint(route));

  // ---- AND THE DATE FILTERS REACH THE READ, WHICH IS THE OTHER HALF OF ALLOWING THEM.
  //
  // The rule above permits a narrowing filter. This asserts the one on this route actually narrows — a
  // bound parameter the handler silently dropped would be a contract promising a filter that does nothing.
  [Fact]
  public async Task The_records_route_passes_its_date_filters_through()
  {
    var response = await host.Client.SendAsync(AttendanceApiTestHost.Request(
      HttpMethod.Get,
      $"{RecordsRoute}?fromDate=2026-01-01&toDate=2026-01-31",
      host.TokenWith(AttendancePermissionNames.ViewOwnRecords)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var call = Assert.Single(host.Reads.RecordsForEmployee);

    Assert.Equal(new DateOnly(2026, 1, 1), call.FromDate);
    Assert.Equal(new DateOnly(2026, 1, 31), call.ToDate);
  }

  private Task<HttpResponseMessage> Send(string route, params string[] permissions) =>
    host.Client.SendAsync(AttendanceApiTestHost.Request(
      HttpMethod.Get, route, host.TokenWith(permissions)));
}

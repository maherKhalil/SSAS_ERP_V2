using System.Net;
using SSAS.Attendance.Application.Permissions;

namespace SSAS.API.Tests.Attendance;

// =================================================================================================
// A MALFORMED IDENTIFIER IS A BAD REQUEST, NOT A MISSING RESOURCE (T-236).
// =================================================================================================
//
// ---- THE CONVENTION, AND WHERE IT IS STATED.
//
// A 400 for a malformed identifier is this product's convention: `Detail_with_a_malformed_company_id_
// returns_400` asserts it for Company, and the same shape is asserted across HR. **Attendance contradicted
// it, and nothing asserted either behaviour** — which is why the divergence survived: no test held the
// convention here, and no test held the deviation either.
//
// ---- ⚠ THE MECHANISM IS THE ROUTE CONSTRAINT, AND IT IS INVISIBLE FROM THE HANDLER.
//
// Company routes are `{companyId}` with NO constraint, so a malformed value reaches the handler, fails to
// parse, and becomes a 400 the handler chose. Attendance routes were `{workingCalendarId:guid}` — **the
// constraint is part of route MATCHING, so `not-a-guid` matches no route at all and ASP.NET answers 404
// before any Attendance code runs.**
//
// That is why this could not be found by reading handlers: **there is no code to read.** The behaviour is
// a property of the route table, and the difference between the two modules is one token in a string.
//
// ---- WHY 400 RATHER THAN 404, GIVEN BOTH ARE DEFENSIBLE IN ISOLATION.
//
// **A 404 says "this resource does not exist", which is a claim about the caller's authorisation to know
// that.** For a value that cannot name any resource, it is also a lie of a specific kind: it tells a
// caller the identifier was well-formed and simply absent. 400 says the request was malformed, which is
// what happened, and it does not leak whether a well-formed identifier would have been found.
//
// **The point is not which is better — it is that one product answers one way.** Six tests across three
// modules asserted 400 while twelve Attendance routes answered 404.
public sealed class AttendanceMalformedIdentifierTests(AttendanceApiTestHost host)
  : IClassFixture<AttendanceApiTestHost>
{
  // ⚠ EVERY ATTENDANCE ROUTE THAT TAKES AN IDENTIFIER IN ITS PATH, ENUMERATED RATHER THAN SAMPLED.
  //
  // Twelve, from `AttendanceEndpointRouteBuilderExtensions`. A sample would have proved the convention for
  // whichever route was picked and said nothing about the other eleven — and the defect was uniform, so a
  // sample would have looked like a complete answer.
  public static TheoryData<string, string, string> RoutesTakingAnIdentifier() => new()
  {
    { "PUT", "/api/attendance/calendars/not-a-guid", AttendancePermissionNames.ManageCalendars },
    { "POST", "/api/attendance/calendars/not-a-guid/holidays", AttendancePermissionNames.ManageCalendars },
    { "POST", "/api/attendance/calendars/not-a-guid/holidays/remove", AttendancePermissionNames.ManageCalendars },
    { "POST", "/api/attendance/periods/not-a-guid/close", AttendancePermissionNames.ClosePeriods },
    { "POST", "/api/attendance/periods/not-a-guid/reopen", AttendancePermissionNames.ClosePeriods },
    { "POST", "/api/attendance/records/not-a-guid/adjustments", AttendancePermissionNames.ManageRecords },
    { "PUT", "/api/attendance/leave-types/not-a-guid", AttendancePermissionNames.ManageLeaveTypes },
    { "POST", "/api/attendance/leave-types/not-a-guid/activate", AttendancePermissionNames.ManageLeaveTypes },
    { "POST", "/api/attendance/leave-types/not-a-guid/deactivate", AttendancePermissionNames.ManageLeaveTypes },
    { "POST", "/api/attendance/leave-requests/not-a-guid/approve", AttendancePermissionNames.ApproveLeave },
    { "POST", "/api/attendance/leave-requests/not-a-guid/reject", AttendancePermissionNames.ApproveLeave },
    { "POST", "/api/attendance/leave-requests/not-a-guid/cancel", AttendancePermissionNames.ManageLeave },
  };

  [Theory]
  [MemberData(nameof(RoutesTakingAnIdentifier))]
  public async Task A_malformed_identifier_is_a_bad_request(string method, string path, string permission)
  {
    host.ResetToAuthorizedState();

    var response = await Send(new HttpMethod(method), path, permission, "{}");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ⚠ THE ADMISSION CONTROL, AND ITS DISCRIMINATOR IS THE PROBLEM BODY RATHER THAN THE STATUS.
  //
  // Without this, "returns 400" is satisfied by a route that refuses everything, and removing a route
  // constraint could plausibly break well-formed routing rather than fix it.
  //
  // **A well-formed identifier for a leave type that does not exist legitimately answers 404** — so status
  // alone cannot tell "the handler ran and found nothing" from "no route matched". The difference is the
  // BODY: a handler refusal carries a problem document with an `attendance.*` code; **a routing failure
  // carries no body at all.** That is what this asserts.
  [Fact]
  public async Task A_well_formed_identifier_reaches_the_handler_rather_than_failing_to_route()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Put,
      $"/api/attendance/leave-types/{Guid.NewGuid()}",
      AttendancePermissionNames.ManageLeaveTypes,
      $$"""{"companyId":"{{AttendanceApiTestHost.CompanyA}}","name":"Annual","isPaid":true}""");

    var body = await response.Content.ReadAsStringAsync();

    Assert.False(string.IsNullOrWhiteSpace(body),
      $"a well-formed identifier produced a bodyless {(int)response.StatusCode}, which is a routing " +
      "failure rather than a handler decision — the route no longer matches a valid id.");
  }

  private async Task<HttpResponseMessage> Send(
    HttpMethod method, string path, string permission, string body)
  {
    var request = AttendanceApiTestHost.Request(method, path, host.TokenWith(permission));
    request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    return await host.Client.SendAsync(request);
  }
}

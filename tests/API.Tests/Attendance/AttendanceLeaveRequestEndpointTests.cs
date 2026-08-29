using System.Net;
using System.Text.Json;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Domain.Leave;

namespace SSAS.API.Tests.Attendance;

// =================================================================================================
// ATTENDANCE'S LEAVE-REQUEST ROUTES, OVER HTTP (T-197). THE FIRST SLICE OF 25.
// =================================================================================================
//
// ---- ⚠ THE FIRST REQUEST EVER ISSUED AGAINST THIS MODULE'S ADMINISTRATIVE SURFACE FOUND A 500.
//
// `Attendance.LeaveSubmissionBusy` had no arm in `AttendanceApiErrorMapper` and fell through to
// `ApiErrors.WriteFailure` — **500 `request.failed`** — when another submission for the same employee holds
// the lock. **A double-clicked submit button is sufficient**, which is the same observation that put
// overlapping leave on the owner's decision list in the first place.
//
// So the lock closed the DATA defect and left the ANSWER wrong, and nothing could see it: the error is
// produced by `SqlServerLeaveSubmissionLock` in INFRASTRUCTURE and merely propagated by the handler, so it
// never appears in the handler's source. `Every_error_a_site_is_responsible_for_is_mapped` walks the errors
// a handler names, and this one enters through a seam that walk does not cross — **comprehensive over what
// it looks at, and blind to what arrives from elsewhere**, which is the day's recurring shape.
//
// ---- WHAT THIS FILE PROVES, AND WHAT IT DELIBERATELY DOES NOT.
//
// The handlers are covered in `Attendance.Tests` against doubles like these. **What only HTTP can reach is
// the wiring**: that the route exists at the documented method and path, that the body binds, that the
// company header is established, and that a refusal becomes the right STATUS AND CODE rather than a 500.
//
// Assertions here are about what came back over the wire. Asserting business behaviour would be proving the
// stub — the mistake T-187 made in Payroll, which this module has room to avoid.
public sealed class AttendanceLeaveRequestEndpointTests(AttendanceApiTestHost host)
  : IClassFixture<AttendanceApiTestHost>
{
  private const string Route = "/api/attendance/leave-requests";

  private static readonly Guid LeaveTypeId = Guid.Parse("66666666-6666-6666-6666-666666666666");

  private string ManageToken => host.TokenWith(AttendancePermissionNames.ManageLeave);

  private static string SubmitBody(string? endDate = "2026-09-24") => endDate is null
    ? $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","employeeId":"{{AttendanceApiTestHost.EmployeeId}}",
       "leaveTypeId":"{{LeaveTypeId}}","startDate":"2026-09-22"}
      """
    : $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","employeeId":"{{AttendanceApiTestHost.EmployeeId}}",
       "leaveTypeId":"{{LeaveTypeId}}","startDate":"2026-09-22","endDate":"{{endDate}}"}
      """;

  // ⚠ THE DEFECT. Planting the mapper arm away restores the 500 and reddens this.
  [Fact]
  public async Task A_busy_submission_lock_is_a_retryable_conflict_and_not_a_server_error()
  {
    host.ResetToAuthorizedState();
    host.SubmissionLock.Failure = LeaveErrors.SubmissionBusy;

    try
    {
      var response = await host.Client.SendAsync(WithBody(
        HttpMethod.Post, Route, ManageToken, SubmitBody()));

      Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

      // The DISTINGUISHABLE code, not the generic conflict. Every other 409 on this surface means change
      // the request; this one means send it again, and an employee told to alter a correct leave request
      // is being given the wrong instruction.
      Assert.Equal("attendance.leave_submission_busy", await AttendanceApiTestHost.ProblemCodeAsync(response));
    }
    finally
    {
      host.SubmissionLock.Failure = null;
    }
  }

  [Fact]
  public async Task A_submission_is_created_and_names_the_new_request()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(WithBody(
      HttpMethod.Post, Route, ManageToken, SubmitBody()));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    // The body carries the identifier the Location header points at. Binding, routing and the response
    // projection are all on this path and none of them is visible to a handler test.
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.True(document.RootElement.TryGetProperty("leaveRequestId", out var id));
    Assert.NotEqual(Guid.Empty, id.GetGuid());
  }

  [Fact]
  public async Task A_submission_missing_a_required_field_is_a_validation_failure_not_a_server_error()
  {
    host.ResetToAuthorizedState();

    // `endDate` omitted. The endpoint declares its required fields rather than letting a default DateOnly
    // through as 0001-01-01, and that declaration is only exercised over the wire.
    var response = await host.Client.SendAsync(WithBody(
      HttpMethod.Post, Route, ManageToken, SubmitBody(endDate: null)));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await AttendanceApiTestHost.ProblemCodeAsync(response));
  }

  // ---- ⚠ THE T-099 SHAPE, NOW CHECKED FROM THE OUTSIDE.
  //
  // `/leave-requests/{id}/approve` was once gated on `ViewLeave` where `ApproveLeave` belonged — a route
  // present, correctly named, and satisfying every surface comparison while handing approval to any reader.
  // A document comparison caught it. **Nothing had ever tried it with a reader's token.**
  [Fact]
  public async Task The_view_permission_alone_cannot_approve_a_leave_request()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(WithBody(
      HttpMethod.Post, $"{Route}/{Guid.NewGuid()}/approve",
      host.TokenWith(AttendancePermissionNames.ViewLeave), """{"decisionNote":"fine"}"""));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Cancelling_a_request_that_does_not_exist_is_a_not_found()
  {
    host.ResetToAuthorizedState();
    host.LeaveRequests.Existing = null;

    var response = await host.Client.SendAsync(WithBody(
      HttpMethod.Post, $"{Route}/{Guid.NewGuid()}/cancel", ManageToken, "{}"));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("attendance.not_found", await AttendanceApiTestHost.ProblemCodeAsync(response));
  }

  // `AttendanceApiTestHost.Request`'s fourth parameter is the COMPANY HEADER, not a body — the mistake this
  // helper exists to stop repeating. The header keeps its default so every request here is company-scoped.
  private static HttpRequestMessage WithBody(HttpMethod method, string path, string token, string body)
  {
    var request = AttendanceApiTestHost.Request(method, path, token);
    request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    return request;
  }
}

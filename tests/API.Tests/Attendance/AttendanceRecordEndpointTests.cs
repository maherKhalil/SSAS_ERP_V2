using System.Net;
using SSAS.Attendance.Application.Permissions;

namespace SSAS.API.Tests.Attendance;

// =================================================================================================
// ATTENDANCE RECORDS, OVER HTTP (T-208). THE LAST SLICE OF THE 25.
// =================================================================================================
//
// Recording attendance and adjusting a record are the two writes that feed payroll quantities, so what
// reaches the ledger begins here. Three routes, none of which had ever received a request.
//
// ---- ⚠ RECORDING AND ADJUSTING ARE THE SAME AUTHORITY, AND THAT IS A DECISION RATHER THAN AN OVERSIGHT.
//
// Both are `ManageRecords`. `OD-ATT-0012` puts the correction of an attendance record on an ADJUSTMENT
// path rather than an edit, precisely so the original stays and the change is additive — so the authority
// is over the record's history, not over one direction of change. The same shape as GL's account
// activation carrying the DEACTIVATE permission: **the authority is over the toggle, not the direction.**
//
// Each refusal carries its admission, which is now the default here: a refusal assertion alone cannot tell
// a route that guards correctly from one that has stopped guarding while something behind it still does.
public sealed class AttendanceRecordEndpointTests(AttendanceApiTestHost host)
  : IClassFixture<AttendanceApiTestHost>
{
  private const string Records = "/api/attendance/records";

  private static string RecordBody(string? note = "Regular day") =>
    $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","employeeId":"{{AttendanceApiTestHost.EmployeeId}}","attendanceDate":"2026-09-22","workedQuantity":8,"overtimeQuantity":2,"overtimeTier":"night","paidAbsenceQuantity":0,"unpaidAbsenceQuantity":0,"note":"{{note}}"}
      """;

  [Fact]
  public async Task Recording_attendance_needs_management_not_merely_viewing()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, Records,
      AttendancePermissionNames.ViewRecords, RecordBody());

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // The admission half. Without it a records route that refused everyone would satisfy the assertion above.
  [Fact]
  public async Task Managing_records_reaches_the_handler_rather_than_being_refused_at_the_gate()
  {
    host.ResetToAuthorizedState();
    host.Periods.Existing = null;

    var response = await Send(HttpMethod.Post, Records,
      AttendancePermissionNames.ManageRecords, RecordBody());

    // ⚠ A CONFLICT, NOT A NOT-FOUND, AND THE MODULE IS RIGHT WHERE I WAS WRONG.
    //
    // I asserted 404 first, reasoning that a missing period is a missing thing. The handler answers
    // `AttendancePeriodErrors.NoOpenPeriod` and the mapper makes it 409, which is the better answer:
    // **the period is not absent, the company has none OPEN to record into**, and the remedy is to open
    // one rather than to go looking for something that was never named in the request.
    Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  [Fact]
  public async Task An_adjustment_needs_management_not_merely_viewing()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, $"{Records}/{Guid.NewGuid()}/adjustments",
      AttendancePermissionNames.ViewRecords,
      """{"workedDelta":1,"overtimeDelta":0,"overtimeTier":null,"paidAbsenceDelta":0,"unpaidAbsenceDelta":0,"note":"Correction"}""");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Adjusting_an_unknown_record_is_a_not_found_rather_than_a_server_error()
  {
    host.ResetToAuthorizedState();
    host.Records.Existing = null;

    var response = await Send(HttpMethod.Post, $"{Records}/{Guid.NewGuid()}/adjustments",
      AttendancePermissionNames.ManageRecords,
      """{"workedDelta":1,"overtimeDelta":0,"overtimeTier":null,"paidAbsenceDelta":0,"unpaidAbsenceDelta":0,"note":"Correction"}""");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("attendance.not_found", await AttendanceApiTestHost.ProblemCodeAsync(response));
  }

  // ---- ⚠ AN ADJUSTMENT WITHOUT A NOTE IS REFUSED, AND THE REASON IS AUDIT RATHER THAN VALIDATION.
  //
  // `note` is the only non-optional field on the adjustment: `OD-ATT-0012` makes the correction additive so
  // the original survives, and a surviving original with an unexplained delta beside it is worse than no
  // record of the correction at all. The required-field declaration is transport-level and only a request
  // can exercise it.
  [Fact]
  public async Task An_adjustment_without_a_note_is_refused()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, $"{Records}/{Guid.NewGuid()}/adjustments",
      AttendancePermissionNames.ManageRecords,
      """{"workedDelta":1,"overtimeDelta":0,"overtimeTier":null,"paidAbsenceDelta":0,"unpaidAbsenceDelta":0}""");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await AttendanceApiTestHost.ProblemCodeAsync(response));
  }

  private async Task<HttpResponseMessage> Send(
    HttpMethod method, string path, string permission, string body)
  {
    // `AttendanceApiTestHost.Request`'s fourth parameter is the COMPANY HEADER, not a body.
    var request = AttendanceApiTestHost.Request(method, path, host.TokenWith(permission));
    request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    return await host.Client.SendAsync(request);
  }
}

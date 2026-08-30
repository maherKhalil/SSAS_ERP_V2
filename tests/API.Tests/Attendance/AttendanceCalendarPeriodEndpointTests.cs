using System.Text.Json;
using System.Net;
using SSAS.Attendance.Application.Permissions;

namespace SSAS.API.Tests.Attendance;

// =================================================================================================
// ATTENDANCE'S CALENDAR AND PERIOD ROUTES, OVER HTTP (T-199). SLICE 2 OF 25.
// =================================================================================================
//
// Ten routes, none of which had ever received a request. What these prove is the WIRING — the route
// exists at the documented method and path, the body binds, the company header is established, the
// permission gate is the one the document names, and a refusal becomes a status rather than a 500. The
// handlers themselves are covered in `Attendance.Tests`.
//
// ---- ⚠ THE PERMISSION ASSERTIONS ARE THE POINT, AND THEY ARE NOT THE SWEEP'S ASSERTIONS.
//
// `AttendanceRoutePermissionTests` already asserts every route REQUIRES some permission. It enumerates the
// route table and never issues a request, so it cannot tell `ClosePeriods` from `ManagePeriods` — it only
// knows a permission is present. **That is the same gap T-099 exploited**, where `approve` required
// `ViewLeave` and satisfied every structural check while handing approval to any reader.
//
// So these ask a sharper question: does the route refuse a token carrying the NEIGHBOURING permission? A
// close gated on `ManagePeriods` rather than `ClosePeriods` would pass the sweep and fail here.
public sealed class AttendanceCalendarPeriodEndpointTests(AttendanceApiTestHost host)
  : IClassFixture<AttendanceApiTestHost>
{
  private const string Calendars = "/api/attendance/calendars";
  private const string Periods = "/api/attendance/periods";

  [Fact]
  public async Task A_calendar_is_created()
  {
    host.ResetToAuthorizedState();
    host.WorkingCalendars.NameTaken = false;

    var response = await Send(HttpMethod.Post, Calendars,
      AttendancePermissionNames.ManageCalendars,
      $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","name":"Standard",
       "weekendDays":[5,6],"isDefault":true}
      """);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  // The VIEW permission on a MANAGE route. The route sweep cannot distinguish these; only a request can.
  [Fact]
  public async Task The_view_permission_alone_cannot_create_a_calendar()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, Calendars,
      AttendancePermissionNames.ViewCalendars,
      $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","name":"Standard",
       "weekendDays":[5,6],"isDefault":true}
      """);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task A_holiday_added_to_an_unknown_calendar_is_a_not_found()
  {
    host.ResetToAuthorizedState();
    var previous = host.WorkingCalendars.Existing;
    host.WorkingCalendars.Existing = null;

    try
    {
      var response = await Send(HttpMethod.Post, $"{Calendars}/{Guid.NewGuid()}/holidays",
        AttendancePermissionNames.ManageCalendars,
        """{"holidayDate":"2026-09-23","name":"National Day"}""");

      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      Assert.Equal("attendance.not_found", await AttendanceApiTestHost.ProblemCodeAsync(response));
    }
    finally
    {
      host.WorkingCalendars.Existing = previous;
    }
  }

  [Fact]
  public async Task An_overlapping_period_is_a_conflict_rather_than_a_server_error()
  {
    host.ResetToAuthorizedState();
    host.Periods.Overlapping = true;

    try
    {
      var response = await Send(HttpMethod.Post, Periods,
        AttendancePermissionNames.ManagePeriods,
        $$"""
        {"companyId":"{{AttendanceApiTestHost.CompanyA}}","name":"September 2026",
         "startDate":"2026-09-01","endDate":"2026-09-30"}
        """);

      Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
      Assert.Equal("attendance.conflict", await AttendanceApiTestHost.ProblemCodeAsync(response));
    }
    finally
    {
      host.Periods.Overlapping = false;
    }
  }

  [Fact]
  public async Task Closing_a_period_that_does_not_exist_is_a_not_found()
  {
    host.ResetToAuthorizedState();
    host.Periods.Existing = null;

    var response = await Send(HttpMethod.Post, $"{Periods}/{Guid.NewGuid()}/close",
      AttendancePermissionNames.ClosePeriods, "{}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("attendance.not_found", await AttendanceApiTestHost.ProblemCodeAsync(response));
  }

  // ⚠ THE NEIGHBOURING PERMISSION, WHICH IS THE ONE A STRUCTURAL SWEEP CANNOT SEE. Closing a period is
  // gated on `ClosePeriods`, NOT on `ManagePeriods` — creating a period and closing one are different
  // authorities, and a caller who may open next month's period may not seal last month's.
  [Fact]
  public async Task Managing_periods_does_not_confer_the_authority_to_close_one()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, $"{Periods}/{Guid.NewGuid()}/close",
      AttendancePermissionNames.ManagePeriods, "{}");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Reopening_a_period_is_the_same_authority_as_closing_one()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, $"{Periods}/{Guid.NewGuid()}/reopen",
      AttendancePermissionNames.ManagePeriods, "{}");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // THE WORKING-DAYS QUERY, WHICH NOTHING HAD EVER CALLED (T-240).
  // ================================================================================================
  //
  // `GET /calendars/working-days` was one of five live routes addressed by no test. It is the query
  // `REQ-ATT-0003` requires, and it exists **so a client gets the SAME answer the domain uses** — a client
  // computing working days itself drifts the first time a holiday moves, and the drift surfaces as a leave
  // request consuming a different number of days than the preview promised.
  //
  // ⚠ **THAT PURPOSE IS EXACTLY WHAT A STATUS-ONLY TEST CANNOT CHECK.** A 200 carrying a number the handler
  // invented would look identical to a 200 carrying the domain's number. So this asserts the VALUE reaches
  // the caller unaltered, and that the query parameters reach the read service unaltered — which together
  // are the pass-through the route exists to provide.
  [Fact]
  public async Task The_working_days_query_returns_the_read_services_answer_and_its_own_arguments()
  {
    host.ResetToAuthorizedState();
    host.Reads.WorkingDays = 17;

    var response = await Send(HttpMethod.Get,
      $"{Calendars}/working-days?companyId={AttendanceApiTestHost.CompanyA}"
        + "&fromDate=2026-03-01&toDate=2026-03-31",
      AttendancePermissionNames.ViewCalendars,
      body: null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    // 17 rather than a plausible number: an arithmetic accident cannot land on it, so the assertion fails
    // if anything between the read service and the wire substitutes its own answer.
    Assert.Equal(17, payload.RootElement.GetProperty("workingDays").GetInt32());

    Assert.Equal(AttendanceApiTestHost.CompanyA, host.Reads.WorkingDaysCompanyId);
    Assert.Equal(
      (new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)),
      host.Reads.WorkingDaysRange);
  }

  // The neighbouring-permission question this class exists to ask, for the route nothing had called.
  [Fact]
  public async Task The_working_days_query_refuses_a_token_without_the_view_calendars_grant()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Get,
      $"{Calendars}/working-days?companyId={AttendanceApiTestHost.CompanyA}"
        + "&fromDate=2026-03-01&toDate=2026-03-31",
      AttendancePermissionNames.ViewPeriods,
      body: null);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpResponseMessage> Send(
    HttpMethod method, string path, string permission, string? body)
  {
    // `AttendanceApiTestHost.Request`'s fourth parameter is the COMPANY HEADER, not a body.
    var request = AttendanceApiTestHost.Request(method, path, host.TokenWith(permission));

    // A GET carrying a JSON body is not what a client sends, and the working-days query is a GET.
    if (body is not null)
    {
      request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    }
    return await host.Client.SendAsync(request);
  }
}

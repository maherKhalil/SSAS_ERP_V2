using System.Net;
using SSAS.Attendance.Application.Permissions;

namespace SSAS.API.Tests.Attendance;

// =================================================================================================
// ATTENDANCE'S LEAVE-TYPE AND BALANCE ROUTES, OVER HTTP (T-204). SLICE 3 OF 25.
// =================================================================================================
//
// Seven routes that had never received a request.
//
// ---- ⚠ THE FIRST TEST GUARDS A DEFECT THIS PRODUCT HAS ALREADY SHIPPED ONCE.
//
// `CreateLeaveTypeRequest.Behaviour` carries a `JsonStringEnumConverter`, and its own comment records why:
// without it `"Unpaid"` cannot become a `LeaveBehaviour`, because `JsonSerializerOptions.Default` reads
// enums from NUMBERS only. **The whole record then fails to bind and the route answers `400
// request.invalid` for every well-formed request** — no leave type can be created, so no leave can ever be
// requested. FP-012 found exactly that in Payroll.
//
// **Nothing could catch it before this file.** The transport test reflects over the record and asserts the
// attribute is PRESENT; the handler tests never serialise anything. Only a real request proves the
// attribute does what it is there for — and an attribute that is present but ineffective is precisely the
// shape that survives a structural check.
public sealed class AttendanceLeaveTypeBalanceEndpointTests(AttendanceApiTestHost host)
  : IClassFixture<AttendanceApiTestHost>
{
  private const string LeaveTypes = "/api/attendance/leave-types";
  private const string Balances = "/api/attendance/leave-balances";

  [Fact]
  public async Task A_leave_type_is_created_from_a_behaviour_written_as_a_string()
  {
    host.ResetToAuthorizedState();
    host.LeaveTypes.CodeTaken = false;

    var response = await Send(HttpMethod.Post, LeaveTypes,
      AttendancePermissionNames.ManageLeaveTypes,
      $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","code":"UNP","name":"Unpaid Leave",
       "behaviour":"Unpaid","isSensitive":false}
      """);

    // ⚠ A 400 HERE MEANS THE CONVERTER IS GONE, NOT THAT THE REQUEST IS WRONG. That is the whole point of
    // the assertion: the failure mode is total and it looks like a client error.
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task The_view_permission_alone_cannot_create_a_leave_type()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Post, LeaveTypes,
      AttendancePermissionNames.ViewLeaveTypes,
      $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","code":"UNP","name":"Unpaid Leave",
       "behaviour":"Unpaid","isSensitive":false}
      """);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task A_duplicate_leave_type_code_is_a_conflict_rather_than_a_server_error()
  {
    host.ResetToAuthorizedState();
    host.LeaveTypes.CodeTaken = true;

    try
    {
      var response = await Send(HttpMethod.Post, LeaveTypes,
        AttendancePermissionNames.ManageLeaveTypes,
        $$"""
        {"companyId":"{{AttendanceApiTestHost.CompanyA}}","code":"ANN","name":"Annual",
         "behaviour":"PaidFromBalance","isSensitive":false}
        """);

      Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
      Assert.Equal("attendance.conflict", await AttendanceApiTestHost.ProblemCodeAsync(response));
    }
    finally
    {
      host.LeaveTypes.CodeTaken = false;
    }
  }

  [Fact]
  public async Task Activating_an_unknown_leave_type_is_a_not_found()
  {
    host.ResetToAuthorizedState();
    var previous = host.LeaveTypes.Existing;
    host.LeaveTypes.Existing = null;

    try
    {
      var response = await Send(HttpMethod.Post, $"{LeaveTypes}/{Guid.NewGuid()}/activate",
        AttendancePermissionNames.ManageLeaveTypes, """{"rowVersion":null}""");

      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      Assert.Equal("attendance.not_found", await AttendanceApiTestHost.ProblemCodeAsync(response));
    }
    finally
    {
      host.LeaveTypes.Existing = previous;
    }
  }

  // ---- ⚠ THE BALANCE ROUTE IS GATED ON LEAVE, NOT ON LEAVE TYPES, AND THAT IS EASY TO GET BACKWARDS.
  //
  // Setting an entitlement is `ManageLeave`. An administrator who may define the CATALOGUE of leave types
  // must not thereby be able to grant an individual employee thirty days, and the two permissions sit two
  // lines apart in the same route group.
  [Fact]
  public async Task Managing_leave_types_does_not_confer_the_authority_to_grant_an_entitlement()
  {
    host.ResetToAuthorizedState();

    var response = await Send(HttpMethod.Put, Balances,
      AttendancePermissionNames.ManageLeaveTypes,
      $$"""
      {"companyId":"{{AttendanceApiTestHost.CompanyA}}","employeeId":"{{AttendanceApiTestHost.EmployeeId}}",
       "leaveTypeId":"66666666-6666-6666-6666-666666666666","periodYear":2026,"entitlementQuantity":30}
      """);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task An_entitlement_for_an_unknown_leave_type_is_a_not_found()
  {
    host.ResetToAuthorizedState();
    var previous = host.LeaveTypes.Existing;
    host.LeaveTypes.Existing = null;

    try
    {
      var response = await Send(HttpMethod.Put, Balances,
        AttendancePermissionNames.ManageLeave,
        $$"""
        {"companyId":"{{AttendanceApiTestHost.CompanyA}}","employeeId":"{{AttendanceApiTestHost.EmployeeId}}",
         "leaveTypeId":"66666666-6666-6666-6666-666666666666","periodYear":2026,"entitlementQuantity":30}
        """);

      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      Assert.Equal("attendance.not_found", await AttendanceApiTestHost.ProblemCodeAsync(response));
    }
    finally
    {
      host.LeaveTypes.Existing = previous;
    }
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

using System.Net;
using SSAS.HR.Contracts.Employment;
using SSAS.Payroll.Application.Permissions;

namespace SSAS.API.Tests.Payroll;

// ==================================================================================================
// A CONTRACT EMPLOYEE TAKES NO COMPENSATION RECORD (T-153).
// ==================================================================================================
//
// The rule is stated once, in `PayrollErrors.CompensationNotAvailableForContract`. **These are the wire
// consequences of it**, and they are here rather than beside the handler because the thing a client sees
// is a status code, and only the transport can produce one.
//
// ---- ⚠ ALL THREE CASES SEND THE SAME BODY. THAT IS THE POINT.
//
// The payload never varies — **only the employment type HR reports does.** A test that also changed the
// body could not tell a pairing refusal from a validation refusal, and the two carry different statuses
// for a reason a client acts on: one is fixed by editing the request, the other is not.
public sealed class PayrollContractEngagementTests(PayrollApiTestHost host)
  : IClassFixture<PayrollApiTestHost>
{
  private const string Path =
    "/api/payroll/employees/44444444-4444-4444-4444-444444444444/compensation";

  private const string Body =
    """{"companyId":"22222222-2222-2222-2222-222222222222","effectiveFromUtc":"2026-01-01T00:00:00Z","baseAmount":5000,"wasOutsideGradeBand":false}""";

  [Fact]
  [Trait("Decision", "DEC-POS-0023")]
  public async Task A_contract_employee_is_refused_a_compensation_record()
  {
    host.ResetToAuthorizedState();
    host.Engagement.EmploymentType = EmploymentType.Contract;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, Path, host.TokenWith(PayrollPermissionNames.ManageCompensation), Body));

    // CONFLICT, not `BadRequest`. The body is well formed and the state refuses it — the same request
    // succeeds unchanged once HR changes the employment type.
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  // ---- ⚠ THE CONTROL, AND WITHOUT IT THE TEST ABOVE PROVES ALMOST NOTHING.
  //
  // A refusal on a route that refuses everything would look identical. **This sends the byte-identical body
  // with the type set to `FullTime` and requires it through**, so the `Conflict` above is attributable to
  // the employment type and to nothing else in the request.
  [Fact]
  [Trait("Decision", "DEC-POS-0023")]
  public async Task The_same_request_succeeds_for_a_full_time_employee()
  {
    host.ResetToAuthorizedState();
    host.Engagement.EmploymentType = EmploymentType.FullTime;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, Path, host.TokenWith(PayrollPermissionNames.ManageCompensation), Body));

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  // ---- AND A NULL IS NOT A GRANT.
  //
  // `GetEmploymentTypeAsync` returns null for an employee HR cannot resolve, which is a different fact from
  // any employment type. **A handler that tested only `is EmploymentType.Contract` would let this through**
  // and record compensation against an id that names nobody — so the distinct status is the evidence the
  // two answers are actually being told apart.
  [Fact]
  public async Task An_employee_hr_cannot_resolve_is_refused_and_not_treated_as_non_contract()
  {
    host.ResetToAuthorizedState();
    host.Engagement.EmploymentType = null;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, Path, host.TokenWith(PayrollPermissionNames.ManageCompensation), Body));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  // ---- AUTHORISATION STILL COMES FIRST, AND THE ORDER IS A DISCLOSURE DECISION.
  //
  // An unauthorised caller must not learn whether an employee exists in HR. With the type set to null, a
  // handler that read HR before authorising would answer `NotFound` — **a probe for the existence of any
  // employee id, available to anyone who can reach the route.** `Forbidden` is the proof it does not.
  [Fact]
  public async Task An_unauthorized_caller_learns_nothing_about_whether_the_employee_exists()
  {
    host.ResetToAuthorizedState();
    host.Engagement.EmploymentType = null;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, Path, host.TokenWith(PayrollPermissionNames.ViewElements), Body));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }
}

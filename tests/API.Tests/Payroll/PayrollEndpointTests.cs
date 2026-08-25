using System.Net;
using SSAS.GL.Contracts.Posting;
using SSAS.HR.Contracts.Employment;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.API.Tests.Payroll;

[Collection(PayrollApiEndpointGroup.Name)]
public sealed class PayrollEndpointTests(PayrollApiTestHost host) : IClassFixture<PayrollApiTestHost>
{
  // Every permission, for the tests that are about something other than authorization.
  private static readonly string[] AllPermissions =
  [
    PayrollPermissionNames.ViewCompensation, PayrollPermissionNames.ManageCompensation,
    PayrollPermissionNames.ViewElements, PayrollPermissionNames.ManageElements,
    PayrollPermissionNames.ViewRuns, PayrollPermissionNames.ManageRuns,
    PayrollPermissionNames.ApproveRuns, PayrollPermissionNames.PostRuns,
    PayrollPermissionNames.ViewPayslips
  ];

  // ================================================================================================
  // THE FP-011 DEFECT. THIS IS THE TEST WHOSE ABSENCE LET GL SHIP EVERY WRITE ROUTE BROKEN.
  // ================================================================================================
  //
  // `StrictRequestReader` deserializes with case-sensitive defaults. GL's request records carried no
  // `[property: JsonPropertyName]`, so `{"code":"4100"}` never bound to `Code`, the reader returned null,
  // and EVERY GL write route answered `400 request.invalid` — while routes, handlers, domain and mapper
  // were all correct. The fault was an ABSENCE, which reading the code does not reveal.
  //
  // Each case below sends a correctly-cased body and asserts the response is NOT `request.invalid`. It does
  // not assert success: a handler may legitimately refuse for a business reason. It asserts the body BOUND.
  [Theory]
  [InlineData("POST", "/api/payroll/employees/44444444-4444-4444-4444-444444444444/compensation",
    """{"companyId":"22222222-2222-2222-2222-222222222222","effectiveFromUtc":"2026-01-01T00:00:00Z","baseAmount":5000,"wasOutsideGradeBand":false}""")]
  [InlineData("POST", "/api/payroll/elements",
    """{"code":"BASIC","name":"Basic Salary","kind":"Earning","behaviour":"BaseSalary","defaultRateOrAmount":0,"calculationOrder":0}""")]
  [InlineData("POST", "/api/payroll/periods",
    """{"companyId":"22222222-2222-2222-2222-222222222222","anyDateInPeriodUtc":"2026-01-15T00:00:00Z","payDateUtc":"2026-02-05T00:00:00Z"}""")]
  [InlineData("POST", "/api/payroll/runs",
    """{"companyId":"22222222-2222-2222-2222-222222222222","payrollPeriodId":"66666666-6666-6666-6666-666666666666"}""")]
  public async Task Every_write_route_binds_a_correctly_cased_body(string method, string path, string body)
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      new HttpMethod(method), path, host.TokenWith(AllPermissions), body));

    if (response.StatusCode == HttpStatusCode.BadRequest)
    {
      var code = await PayrollApiTestHost.ProblemCodeAsync(response);

      Assert.True(
        code != "request.invalid",
        $"{method} {path} refused a correctly-cased body with request.invalid — the JsonPropertyName defect.");
    }
  }

  [Fact]
  public async Task A_reversal_binds_its_body_too()
  {
    host.ResetToAuthorizedState();
    var run = SeedPostedRun();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/reversals", host.TokenWith(AllPermissions),
      """{"reversalDateUtc":"2026-02-10T00:00:00Z","description":"Correction"}"""));

    Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task A_request_supplying_a_currency_is_refused_as_an_unknown_property()
  {
    // `DEC-PAY-0003`: the company's base currency is projected on read and never accepted. The strict reader
    // doing its ordinary job, not a special case.
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, "/api/payroll/elements", host.TokenWith(AllPermissions),
      """{"code":"BASIC","name":"Basic","kind":"Earning","behaviour":"BaseSalary","defaultRateOrAmount":0,"calculationOrder":0,"currencyCode":"SAR"}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // THE BLEED TEST (BR-PAY-0010, OD-PAY-0016)
  // ================================================================================================

  [Theory]
  [InlineData("/api/payroll/employees/44444444-4444-4444-4444-444444444444/compensation")]
  [InlineData("/api/payroll/employees/44444444-4444-4444-4444-444444444444/compensation/current")]
  [InlineData("/api/payroll/employees/44444444-4444-4444-4444-444444444444/payslips")]
  public async Task Hr_permissions_do_not_reach_pay_data(string path)
  {
    // ---- THE WHOLE POINT OF `OD-PAY-0016`.
    //
    // A caller holding EVERY HR permission and no payroll permission must read no compensation and no
    // payslip. `DEC-POS-0018` separated a permission for STRUCTURAL pay bands; individual compensation is
    // personal data, so the separation applies with more force.
    host.ResetToAuthorizedState();

    var hrToken = host.TokenWith(
      "HR.Employees.View", "HR.Employees.Update", "HR.Employees.Terminate", "HR.Employees.Transfer",
      "HR.SalaryGrades.View", "HR.Departments.View", "HR.Positions.View");

    var response = await host.Client.SendAsync(
      PayrollApiTestHost.Request(HttpMethod.Get, path, hrToken));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Element_permissions_do_not_reach_compensation()
  {
    // Elements are structural — a definition says what the company pays, not who receives it. Holding the
    // structural permission must not open the personal one.
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get,
      "/api/payroll/employees/44444444-4444-4444-4444-444444444444/compensation",
      host.TokenWith(PayrollPermissionNames.ViewElements, PayrollPermissionNames.ManageElements)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0009")]
  public async Task Approval_is_refused_to_a_caller_holding_every_other_payroll_permission()
  {
    // The sensitive act is its own grant. This is the separation-of-duties claim made testable: someone who
    // can do everything else still cannot approve.
    host.ResetToAuthorizedState();
    var run = SeedCalculatedRun();

    var everythingElse = AllPermissions
      .Where(permission => permission != PayrollPermissionNames.ApproveRuns)
      .ToArray();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/approval", host.TokenWith(everythingElse)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Posting_is_refused_to_a_caller_who_may_only_approve()
  {
    host.ResetToAuthorizedState();
    var run = SeedApprovedRun();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/posting",
      host.TokenWith(PayrollPermissionNames.ApproveRuns, PayrollPermissionNames.ViewRuns)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // THE TWO APPROVAL REFUSALS, AND BOTH NAME WHAT WENT WRONG
  // ================================================================================================

  [Fact]
  [Trait("Decision", "OD-PAY-0014")]
  public async Task Approval_into_a_closed_period_is_refused_and_names_the_period()
  {
    host.ResetToAuthorizedState();
    var run = SeedCalculatedRun();
    host.Ledger.Window = new PostingWindow(PostingWindowStatus.PeriodClosed, "January 2026");

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/approval", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.period_closed", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0012")]
  public async Task Approval_with_an_unmapped_element_is_refused_and_names_the_element()
  {
    host.ResetToAuthorizedState();
    var run = SeedCalculatedRun(mapAccount: false);

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/approval", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.element_unmapped", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A_ledger_refusal_at_posting_refuses_the_transition()
  {
    // `OD-PAY-0013`'s deciding property: a run must not be able to claim it posted when it did not.
    host.ResetToAuthorizedState();
    var run = SeedApprovedRun();
    host.Ledger.PostOutcome = JournalPostingOutcome.Refused(JournalPostingStatus.AccountUnavailable);

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/posting", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.ledger_refused", await PayrollApiTestHost.ProblemCodeAsync(response));
    Assert.Equal(PayrollRunStatus.Approved, run.Status);
    Assert.Null(run.JournalEntryId);
  }

  [Fact]
  public async Task A_successful_posting_records_the_journal_and_the_journal_balances()
  {
    host.ResetToAuthorizedState();
    var run = SeedApprovedRun();
    var journal = Guid.NewGuid();
    host.Ledger.PostOutcome = JournalPostingOutcome.Success(journal);

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/posting", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(journal, run.JournalEntryId);

    // The composed journal must balance before it ever reaches GL — if Payroll sends an unbalanced one,
    // `BR-GL-0001` refuses it and the defect is Payroll's.
    var posted = host.Ledger.LastPosted!;
    Assert.Equal(posted.Lines.Sum(line => line.Debit), posted.Lines.Sum(line => line.Credit));
  }

  [Fact]
  public async Task A_run_that_is_not_posted_cannot_be_reversed()
  {
    host.ResetToAuthorizedState();
    var run = SeedApprovedRun();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/reversals", host.TokenWith(AllPermissions),
      """{"reversalDateUtc":"2026-02-10T00:00:00Z","description":"Correction"}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.run_state_invalid", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task An_unknown_run_is_not_found_rather_than_forbidden()
  {
    // Reporting an out-of-scope record as forbidden would let a caller enumerate the estate one probe at a
    // time. On this surface the directory being denied is people's pay.
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get, $"/api/payroll/runs/{Guid.NewGuid()}", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("payroll.not_found", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task An_anonymous_caller_is_refused_before_anything_is_read()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get, "/api/payroll/runs", token: null));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---- SEEDING, THROUGH THE REAL AGGREGATES.
  //
  // Nothing here fabricates state a handler could not produce — an approved run is approved by calling
  // `Approve`, so the append-only line set is the one the product would have written.
  private PayrollRun SeedCalculatedRun(bool mapAccount = true)
  {
    var element = PayElement.Create(
      PayrollApiTestHost.CompanyA, "BASIC", "Basic", PayElementKind.Earning,
      PayElementBehaviour.BaseSalary, 0m, 0).Value;

    if (mapAccount)
    {
      element.MapToAccount(PayrollApiTestHost.AccountId);
    }

    var payable = PayElement.Create(
      PayrollApiTestHost.CompanyA, "NETPAY", "Net pay", PayElementKind.Deduction,
      PayElementBehaviour.NetPayPayable, 0m, 99).Value;
    payable.MapToAccount(PayrollApiTestHost.AccountId);

    host.Elements.Stored.Add(element);
    host.Elements.Stored.Add(payable);

    var period = PayrollPeriod.CreateAlignedTo(
      PayrollApiTestHost.CompanyA, Guid.NewGuid(), "January 2026",
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero)).Value;
    host.Periods.Stored.Add(period);

    var run = PayrollRun.Create(PayrollApiTestHost.CompanyA, period.Id).Value;
    run.SetCalculation(
      [new PayrollRunDraftLine(
        Guid.NewGuid(), run.Id, PayrollApiTestHost.EmployeeId, element.Id,
        PayElementKind.Earning, 5000m, 0, mapAccount ? PayrollApiTestHost.AccountId : null)],
      "tester");

    host.Runs.Stored.Add(run);
    return run;
  }

  private PayrollRun SeedApprovedRun()
  {
    var run = SeedCalculatedRun();
    run.Approve("approver");
    return run;
  }

  private PayrollRun SeedPostedRun()
  {
    var run = SeedApprovedRun();
    run.MarkPosted(Guid.NewGuid(), "poster");
    return run;
  }
}

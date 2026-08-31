using System.Text.Json;
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

  // ⚠ A REFUSAL INSIDE A COLLECTION NAMES THE PATH, NOT JUST THE COLLECTION (T-272).
  //
  // `field` was a flat property name until this, and a flat name cannot address an element: an assignment
  // with an empty pay element is wrong at `assignments[].payElementId`, not at `assignments`. **This is
  // where attribution is worth most** -- a caller sending ten assignments needs to know which property of
  // an element is at fault, and `request.invalid` alone tells them nothing at all.
  //
  // Asserted through a real request because the path crosses the guard that raises it, the mapper, the
  // projection and serialization -- and the architecture guard proves only that it RESOLVES, not that it
  // travels.
  [Fact]
  public async Task A_refusal_inside_a_collection_names_the_path_to_the_element_property()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post,
      "/api/payroll/employees/44444444-4444-4444-4444-444444444444/compensation",
      host.TokenWith(AllPermissions),
      """
      {"companyId":"22222222-2222-2222-2222-222222222222","effectiveFromUtc":"2026-01-01T00:00:00Z",
       "baseAmount":5000,"wasOutsideGradeBand":false,
       "assignments":[{"payElementId":"00000000-0000-0000-0000-000000000000","rateOrAmount":10}]}
      """));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal("assignments[].payElementId",
      document.RootElement.GetProperty("field").GetString());
  }

  // ================================================================================================
  // THE BLEED TEST (BR-PAY-0010, OD-PAY-0016)
  // ================================================================================================

  // ⚠ CITED BY B18 pass 15, body-confirmed: the criterion verbatim, BOTH halves. A caller holding seven
  // HR permissions and no payroll permission is refused 403 on compensation, compensation/current AND
  // payslips -- the theory covers the two nouns the criterion names rather than one of them.
  [Trait("Criterion", "AC-PAY-0027")]
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
  // ⚠ CITED BY B18 pass 14, body-confirmed: the criterion verbatim -- a caller holding every OTHER payroll permission is refused 403.
  [Trait("Criterion", "AC-PAY-0016")]
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

  // ---- AN AMBIGUOUS CALENDAR REFUSES AT BOTH PAYROLL CONSUMERS (T-188).
  //
  // Adding a value to `PostingWindowStatus` is only safe if every consumer refuses an unknown status
  // rather than falling through as open. **That was measured at both sites, and this is the measurement.**
  // One tests `PeriodNotFound || FiscalPeriodId is null`; the other tests `PeriodClosed` then `!IsOpen`.
  //
  // ⚠⚠ **AND WHAT PAYROLL ACTUALLY ANSWERS IS WORSE THAN A MISLEADING REMEDY: `payroll.not_found`, A
  // GENERIC 404.** `PayrollErrors.FiscalPeriodNotFound` maps to `NotFound`, so an operator whose calendar
  // has two overlapping years is told the thing they asked for does not exist.
  //
  // The GL side is now honest — `CalendarAmbiguous` says repair the calendar — and the payroll-facing
  // answer is not. **Deliberately NOT fixed here**, because distinguishing it is the same argument one
  // layer out and belongs with the payroll error vocabulary. **This test pins the current answer so the
  // day someone distinguishes it, this reddens and explains itself rather than being quietly updated.**
  [Theory]
  [Trait("Decision", "DEC-L-084")]
  [InlineData("periods")]
  [InlineData("approval")]
  public async Task An_ambiguous_calendar_refuses_rather_than_falling_through_as_open(string route)
  {
    host.ResetToAuthorizedState();
    host.Ledger.Window = new PostingWindow(PostingWindowStatus.CalendarAmbiguous, null);

    var response = route == "periods"
      ? await host.Client.SendAsync(PayrollApiTestHost.Request(
          HttpMethod.Post, "/api/payroll/periods", host.TokenWith(AllPermissions),
          """{"companyId":"22222222-2222-2222-2222-222222222222","anyDateInPeriodUtc":"2026-01-15T00:00:00Z","payDateUtc":"2026-02-05T00:00:00Z"}"""))
      : await host.Client.SendAsync(PayrollApiTestHost.Request(
          HttpMethod.Post, $"/api/payroll/runs/{SeedCalculatedRun().Id}/approval",
          host.TokenWith(AllPermissions)));

    // Refused, and NOT as an open window. The status is unknown to both call sites and neither treats
    // an unknown status as permission to post.
    Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal("payroll.not_found", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0014")]
  // ⚠ CITED BY B18 pass 14 as PARTLY PINNED; FULLY PINNED SINCE T-270. `AC-PAY-0022` is *"a run whose
  // pay date falls in a closed fiscal period cannot be approved, AND THE RESPONSE NAMES THE PERIOD"*.
  // The body asserted 409 and `payroll.period_closed` -- the CONDITION -- and nothing said which period.
  //
  // ⚠ THE CONTRAST DRAWN IN THE ORIGINAL NOTE WAS WRONG AND IS WORTH KEEPING FOR THAT REASON. It said
  // `AC-PAY-0021`'s test *"really does assert `Contains("HOUSING")`"*: it does, in
  // `PayElementDomainTests` -- **at the domain, where the string is constructed.** Comparing an API test
  // against a domain test made one endpoint look careless when in fact **no API test in this file, or in
  // `GlEndpointTests`, asserted a named subject at all.** The shape was a layer's, not a test's.
  [Trait("Criterion", "AC-PAY-0022")]
  public async Task Approval_into_a_closed_period_is_refused_and_names_the_period()
  {
    host.ResetToAuthorizedState();
    var run = SeedCalculatedRun();
    // ⚠ THE FISCAL PERIOD IS DELIBERATELY NOT NAMED "January 2026" -- THE RUN'S OWN PERIOD IS.
    //
    // `PeriodClosedForPosting` is handed `window.PeriodName ?? period.Name`, so with the two names equal
    // a handler that ignored the window entirely and printed the run's own period would satisfy the
    // assertion below perfectly. **The names have to differ for that assertion to have a subject** -- and
    // the closed thing is the FISCAL period, which is not the payroll period that shares its month.
    const string closedFiscalPeriod = "FY2026-P01";
    host.Ledger.Window = new PostingWindow(PostingWindowStatus.PeriodClosed, closedFiscalPeriod);

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/approval", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.period_closed", await PayrollApiTestHost.ProblemCodeAsync(response));

    // ---- ⚠ AND THE PERIOD, WHICH IS THE HALF THE NAME PROMISES AND THE CODE CANNOT CARRY.
    //
    // `payroll.period_closed` names the CONDITION. `AC-PAY-0022` is that the response names the PERIOD,
    // and until `ApiError.Detail` existed it could not: the problem document carried `code`,
    // `correlationId` and `resourceKey` and no message member, so the value was built in
    // `PayrollErrors.PeriodClosedForPosting` and died at the mapper. **The channel arrived and nothing
    // came back to assert through it** -- which is why the test name has promised this since it was
    // written and the body never delivered it.
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Contains(
      closedFiscalPeriod,
      document.RootElement.GetProperty("detail").GetString(),
      StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0012")]
  // `AC-PAY-0021`'s TRANSPORT half. `PayElementDomainTests` pins that the message is BUILT with the
  // element code; this pins that it SURVIVES to the caller. Two tests, one criterion, and the second
  // was the one nobody had written.
  [Trait("Criterion", "AC-PAY-0021")]
  public async Task Approval_with_an_unmapped_element_is_refused_and_names_the_element()
  {
    host.ResetToAuthorizedState();
    var run = SeedCalculatedRun(mapAccount: false);

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/approval", host.TokenWith(AllPermissions)));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.element_unmapped", await PayrollApiTestHost.ProblemCodeAsync(response));

    // ---- ⚠ AND THE ELEMENT. THE SAME OMISSION AS THE TEST ABOVE, IN THE SAME FILE.
    //
    // `PayElementDomainTests` asserts `Unmapped("HOUSING").Message` contains "HOUSING" -- at the DOMAIN,
    // where the string is constructed. **That proves the message is built and says nothing about whether
    // it reaches a caller**, and the mapper, `ShowsDetail` and serialization all sit between the two.
    // `SeedCalculatedRun(mapAccount: false)` leaves `BASIC` unmapped, so the code the caller must go and
    // fix is the one asserted here.
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Contains(
      "BASIC", document.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
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

    // ⚠ WITHOUT THIS, AN EMPTY LINE SET SATISFIES THE LINE ABOVE PERFECTLY: 0 == 0, under a test name that
    // promises the journal BALANCES. `PayrollChainSqlServerTests` has carried this second assertion since it
    // was written; this site had the equality copied and the control left behind.
    Assert.True(posted.Lines.Sum(line => line.Debit) > 0m);
  }

  // ⚠ THE SIBLING OF THE TEST BELOW, AND IT WAS A 500 UNTIL T-198.
  //
  // `MarkReversed()` returns two errors and the handler propagates both. `RunNotReversible` had a mapper
  // arm and `RunAlreadyReversed` did not, so the two halves of one aggregate method answered 409 and 500.
  // Found by enumerating codes produced in Domain or Infrastructure that no mapper handles — the error is
  // never named in the handler, so the guard that walks a handler's own source cannot see it.
  [Fact]
  public async Task A_run_that_is_already_reversed_cannot_be_reversed_again()
  {
    host.ResetToAuthorizedState();
    var run = SeedPostedRun();
    Assert.True(run.MarkReversed().IsSuccess);

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{run.Id}/reversals", host.TokenWith(AllPermissions),
      """{"reversalDateUtc":"2026-02-10T00:00:00Z","description":"Correction"}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("payroll.run_state_invalid", await PayrollApiTestHost.ProblemCodeAsync(response));
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

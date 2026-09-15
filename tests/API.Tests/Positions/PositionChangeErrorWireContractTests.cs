using System.Net;
using SSAS.HR.Application.Permissions;

namespace SSAS.API.Tests.Positions;

// ==================================================================================================
// `Employee.PositionUnchanged` ON THE WIRE — THE ONE RULED CODE THAT LIVES ON THIS ROUTE.
// ==================================================================================================
//
// `POST /api/hr/employees/{employeeId}/change-position` is mapped in `PositionEndpointRouteBuilderExtensions`
// but answers through `EmployeeApiErrorMapper`, because the route answers about an EMPLOYEE. That mapper had
// no `Employee.Position*` arm at all until T-080, so this route answered `500 request.failed` to a caller
// who asked to move an employee to the position they already hold.
//
// It lives here rather than beside the other four because the route does: the test that would have caught
// the defect had to be on the host that mounts it, and no test on this host called the route's error path.
[Collection(PositionApiEndpointGroup.Name)]
public sealed class PositionChangeErrorWireContractTests : IClassFixture<PositionApiTestHost>
{
  private readonly PositionApiTestHost host;

  public PositionChangeErrorWireContractTests(PositionApiTestHost host)
  {
    this.host = host;
    host.Reset();
  }

  // ---- THE DESTINATION IS THE EMPLOYEE'S CURRENT POSITION, WHICH IS REFUSED RATHER THAN NO-OPPED.
  //
  // `ChangeEmployeePositionCommandHandler:96-99` refuses before looking the destination up, deliberately:
  // an unchanged destination answered with a success would report that something happened when nothing did,
  // and would write a history row for a move that never occurred.
  //
  // `PositionA` is the position the seeded employee already holds
  // (`EmployeeApiTestStubs.cs:255-263`, `StampInitialAssignment`), so sending it is the whole test.
  //
  // ⚠⚠ RENAMED BY 269, AND THE OLD NAME WAS FALSE. It read
  // `..._is_a_400_request_invalid` while the body asserts `422 position.unchanged` — the name survived the
  // T-274 correction described below because nothing checks a test's name against its assertions. A reader
  // grepping test names to learn whether this answers 400 or 422 got the wrong answer from the artefact
  // most likely to be skimmed.
  //
  // ⚠ CITED BY 269: `AC-POS-0034`, FIRST CLAUSE — *refused with `position.unchanged`*. The second clause,
  // *AND NO HISTORY RECORD IS WRITTEN*, is not assertable here: this is a wire-contract test against a
  // stubbed host, with no persisted history to inspect.
  //
  // ⚠⚠ CORRECTION, SAME SWEEP: THIS COMMENT ORIGINALLY SAID THAT PARTNER DID NOT EXIST — *"it needs an
  // integration-layer partner before the citation is complete, and until that exists this criterion is
  // covered in part."* THAT WAS FALSE WHEN WRITTEN. `EmployeeBoundarySqlServerTests.P5_A_change_to_the_
  // current_position_is_refused` asserts the refusal AND that the history count is unchanged, which is the
  // whole second clause.
  //
  // I missed it because I searched the POSITION suite for a criterion about a POSITION change — and the
  // change is a write to an EMPLOYEE, so it is tested where the employees are. Fifth time in this sweep
  // that coverage sat under the other party to the relationship, and the first where I had already written
  // the absence down.
  [Fact]
  [Trait("Criterion", "AC-POS-0034")]
  public async Task Moving_an_employee_to_the_position_they_already_hold_is_refused_as_unchanged()
  {
    using var request = PositionApiTestHost.Request(
      HttpMethod.Post,
      $"/api/hr/employees/{PositionApiTestHost.EmployeeId}/change-position",
      host.TokenWith(HrPermissionNames.UpdateEmployees, HrPermissionNames.ViewEmployees),
      $$"""
      {"positionId":"{{Employees.EmployeeApiTestHost.PositionA}}","expectedRowVersion":"AAAAAAAAB9E="}
      """);

    using var response = await host.Client.SendAsync(request);

    // ⚠ 422 AND `position.unchanged` SINCE T-274 -- THIS TEST PINNED A DIVERGENCE, NOT A CONTRACT.
    //
    // It asserted `400 request.invalid`. **It was not failing; it recorded what the product did.** What
    // the product did disagreed with `FP-008 api-contracts.md:167` -- `422 position.unchanged` -- and with
    // `AC-POS-0034`, in BOTH the status and the code. Nothing anywhere recorded a reason to differ, so the
    // code moved to the contract rather than the contract to the code.
    //
    // 422 is the contract's own distinction and it is the right one: the body is well-formed and every
    // field is valid. What is refused is the request's MEANING against current state.
    Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

    using var document = System.Text.Json.JsonDocument.Parse(
      await response.Content.ReadAsStringAsync());

    Assert.Equal("position.unchanged", document.RootElement.GetProperty("code").GetString());

    // The field survives the status change: the caller sent `positionId` and it is still the input at
    // fault. Status is the category, the code is the instruction, and the field is which input.
    Assert.Equal("positionId", document.RootElement.GetProperty("field").GetString());
  }

  // ================================================================================================
  // THE `/change-position` HALF OF THE CROSS-COMPANY DISCLOSURE GUARD.
  // ================================================================================================
  //
  // THE TWIN OF `EmployeeEndpointTests.A46_A_cross_company_employee_is_indistinguishable_from_an_absent_one`,
  // AND IT LIVES HERE FOR THE REASON THE FILE HEADER ALREADY GIVES: the route is mounted on this host and
  // nowhere else, so the test that can reach it has to be on the host that mounts it. That test covers the
  // other SIX members of the class; this covers the SEVENTH. Neither covers the class alone, and each states
  // the population it can see.
  //
  // WHAT IT ASSERTS is identical: a request naming an employee in ANOTHER COMPANY must answer
  // byte-identically to one naming an employee that does not exist, in EVERY content state — here
  // `:90 Terminated` and `:97 PositionUnchanged`, which are this member's own.
  //
  // ⚠ THE `unchanged` STATE IS THE ONE WORTH THE LINE. It is not a bit. Because `:97` sits ABOVE the version
  // check at `:107`, a caller holding only an employee id can ask "is X this employee's position?" once per
  // candidate X, with no valid rowversion, and read the answer off the status code. That is an equality
  // oracle over a field of a record in another company, and it is why this guard enumerates the states
  // rather than asserting the first one.
  //
  // ANTI-VACUITY: verified RED against the unfixed tree of 2026-09-06, every cell.
  //
  // ⚠⚠ AND AGAINST THE REGRESSION THAT WILL ACTUALLY HAPPEN, WHICH IS RELOCATION, NOT DELETION — PLANTED IN
  // THIS HANDLER, not merely reasoned from the twin.
  //
  // A red against a MISSING check only proves the guard notices absence, and nobody deletes a security
  // check; they MOVE it. On 2026-09-06 `:78`'s check was moved below `:90 Terminated` and `:97
  // PositionUnchanged`, left above the version check — the plausible wrong fix, "I put it before the version
  // check". RESULT: RED, 2 of 5 cells, exactly `terminated` and `destination-unchanged`, with `active`,
  // `inactive` and `correct-version` still green. So this guard detects RE-ORDERING in its own member and
  // names which states reopened.
  //
  // (An earlier version of this comment said the relocation was planted on the twin only and that this
  // member's behaviour was "reasoned, not measured". It is now measured. The sentence is replaced rather
  // than deleted so the upgrade is visible.)
  //
  // ⚠ BOUNDED TO THE STALE-VERSION PATH, exactly as the twin is: a caller holding the correct rowversion runs
  // on to the save, where the real refusal is the company write floor at `TenantDbContext:415-457` and this
  // harness's stub unit of work is blind to it.
  [Fact]
  [Trait("Tripwire", "CrossCompanyDisclosure")]
  public async Task A_cross_company_employee_is_indistinguishable_from_an_absent_one()
  {
    const string Stale = "AAAAAAAAB9A=";

    var correct = Convert.ToBase64String(Employees.StubEmployeeRepository.CurrentRowVersion);

    var changed = "{\"positionId\":\"" + PositionApiTestHost.UnknownId +
      "\",\"expectedRowVersion\":\"" + Stale + "\"}";
    var unchanged = "{\"positionId\":\"" + Employees.EmployeeApiTestHost.PositionA +
      "\",\"expectedRowVersion\":\"" + Stale + "\"}";

    // The null check precedes the version check, so the absent answer is token-independent.
    var absent = await AnswerFor(changed, present: false,
      Employees.EmployeeApiTestHost.CompanyA, SSAS.HR.Domain.Employees.EmployeeStatus.Active);

    // ---- THE CORRECT-ROWVERSION STATE. The remedy sits above the version check, so it covers this caller
    // too; a guard sending only a stale token would assert a strictly smaller surface than the fix while
    // reading as though it covered all of it.
    //
    // ⚠ On an unfixed tree this cell reflects the HARNESS past the version check — the stub unit of work
    // returns success and the real refusal there is the company write floor at `TenantDbContext:415-457`,
    // which no API-layer test can observe.
    var correctVersion = "{\"positionId\":\"" + PositionApiTestHost.UnknownId +
      "\",\"expectedRowVersion\":\"" + correct + "\"}";

    (string Name, string Body, SSAS.HR.Domain.Employees.EmployeeStatus Status)[] states =
    [
      ("active", changed, SSAS.HR.Domain.Employees.EmployeeStatus.Active),
      ("inactive", changed, SSAS.HR.Domain.Employees.EmployeeStatus.Inactive),
      ("terminated", changed, SSAS.HR.Domain.Employees.EmployeeStatus.Terminated),
      ("destination-unchanged", unchanged, SSAS.HR.Domain.Employees.EmployeeStatus.Active),
      ("correct-version", correctVersion, SSAS.HR.Domain.Employees.EmployeeStatus.Active)
    ];

    var failures = new List<string>();
    var distinct = new HashSet<string>(StringComparer.Ordinal);

    foreach (var state in states)
    {
      var crossCompany = await AnswerFor(state.Body, present: true,
        Employees.EmployeeApiTestHost.CompanyB, state.Status);

      // Content states only: `correct-version` varies the token, not the record, and folding it into the
      // channel-3 count would inflate it with a different axis.
      if (!string.Equals(state.Name, "correct-version", StringComparison.Ordinal))
      {
        distinct.Add(crossCompany);
      }

      if (!string.Equals(crossCompany, absent, StringComparison.Ordinal))
      {
        failures.Add(
          $"/change-position [{state.Name}] DISCLOSES AN EMPLOYEE IN ANOTHER COMPANY.\n" +
          $"    cross-company answer : {crossCompany}\n" +
          $"    absent answer        : {absent}\n" +
          "    The company check must sit immediately after the null check at `:78`, ABOVE\n" +
          "    both `:90 Terminated` and `:97 PositionUnchanged`.");
      }
    }

    Assert.True(
      failures.Count == 0,
      $"{failures.Count} of {states.Length} cross-company states are distinguishable from absence.\n\n" +
      string.Join("\n\n", failures) +
      $"\n\nCONTENT DISCRIMINATION (channel 3), measured on the stale-version path:\n" +
      $"    change-position      {distinct.Count} distinct answer(s) over {states.Length - 1} content states");
  }

  private async Task<string> AnswerFor(
    string body, bool present, Guid company, SSAS.HR.Domain.Employees.EmployeeStatus status)
  {
    try
    {
      if (present)
      {
        var employee = Employees.StubEmployeeRepository.NewEmployee(status);
        employee.CompanyId = company;
        host.EmployeeRepository.Employee = employee;
      }
      else
      {
        host.EmployeeRepository.Employee = null;
      }

      using var request = PositionApiTestHost.Request(
        HttpMethod.Post,
        "/api/hr/employees/" + PositionApiTestHost.EmployeeId + "/change-position",
        host.TokenWith(HrPermissionNames.UpdateEmployees, HrPermissionNames.ViewEmployees),
        body);

      using var response = await host.Client.SendAsync(request);
      var raw = await response.Content.ReadAsStringAsync();

      return ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " +
        System.Text.RegularExpressions.Regex.Replace(
          raw, "\"correlationId\":\"[^\"]*\"", "\"correlationId\":\"*\"");
    }
    finally
    {
      host.EmployeeRepository.Reset();
    }
  }
}

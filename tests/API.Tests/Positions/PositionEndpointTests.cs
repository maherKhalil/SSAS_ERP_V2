using System.Net;
using System.Text.Json;
using SSAS.API.Tests.Employees;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.API.Tests.Positions;

// ==================================================================================================
// THE POSITION HTTP SURFACE, EXERCISED (FP-008 Phase 4).
// ==================================================================================================
//
// ---- THE AUTHORIZATION CASES ARE THEORIES OVER EVERY ROUTE, NOT ONE TEST PER ROUTE.
//
// "Unauthenticated is 401" and "the wrong permission is 403" are properties of the SURFACE, not of any
// individual route, and twenty near-identical tests would state that badly: a route added later would need
// somebody to remember to add two more. Driving them from one route table means a new route is covered the
// moment it is listed, and the table itself is the thing a reviewer reads.
//
// ---- AND THE PERMISSION-BLEED CASES RUN BOTH WAYS.
//
// It is not enough that a caller without a permission is refused. The interesting failure is a caller with
// a DIFFERENT HR permission being let through — that is what a copy-pasted `RequirePermission` produces,
// and it looks correct at every call site. The pay-band separation is the sharpest instance and gets its
// own tests: `HR.Positions.View` must not reach a salary grade, in either direction of the mistake.
[Collection(PositionApiEndpointGroup.Name)]
public sealed class PositionEndpointTests : IClassFixture<PositionApiTestHost>
{
  private readonly PositionApiTestHost host;

  public PositionEndpointTests(PositionApiTestHost host)
  {
    this.host = host;
    host.Reset();
  }

  // Every FP-008 route, with the permission it demands and a body that would be valid if it got that far.
  // The `null` body marks a GET.
  //
  // ⚠ THE DATA LIVES IN `Fp008Routes()` AND THIS PROJECTS IT, so the drift guard below can read the same
  // rows in a TYPED form. Enumerating `TheoryData` would mean casting `object[]` positionally, which is the
  // kind of untyped access that goes wrong silently when a column is added.
  public static TheoryData<string, string, string, string?> AllRoutes()
  {
    var data = new TheoryData<string, string, string, string?>();

    foreach (var (method, path, permission, body) in Fp008Routes())
    {
      data.Add(method, path, permission, body);
    }

    return data;
  }

  private static (string Method, string Path, string Permission, string? Body)[] Fp008Routes()
  {
    var position = PositionApiTestHost.PositionId;
    var jobGrade = PositionApiTestHost.JobGradeId;
    var salaryGrade = PositionApiTestHost.SalaryGradeId;
    var employee = PositionApiTestHost.EmployeeId;

    const string token = """{"expectedRowVersion":"AAAAAAAAB9E="}""";

    return new (string, string, string, string?)[]
    {
      ("POST", "/api/hr/positions", HrPermissionNames.CreatePositions,
        """{"code":"ACC-SR","title":"Senior Accountant","jobGradeId":null}"""),
      ("GET", "/api/hr/positions", HrPermissionNames.ViewPositions, null),
      ("GET", $"/api/hr/positions/{position}", HrPermissionNames.ViewPositions, null),
      ("PUT", $"/api/hr/positions/{position}", HrPermissionNames.UpdatePositions,
        """{"code":"ACC-SR","title":"Renamed","jobGradeId":null,"expectedRowVersion":"AAAAAAAAB9E="}"""),
      ("POST", $"/api/hr/positions/{position}/activate", HrPermissionNames.DeactivatePositions, token),
      ("POST", $"/api/hr/positions/{position}/deactivate", HrPermissionNames.DeactivatePositions, token),

      ("POST", "/api/hr/job-grades", HrPermissionNames.CreateJobGrades,
        """{"code":"G7","name":"Grade 7","rankOrder":70,"salaryGradeId":null}"""),
      ("GET", "/api/hr/job-grades", HrPermissionNames.ViewJobGrades, null),
      ("GET", $"/api/hr/job-grades/{jobGrade}", HrPermissionNames.ViewJobGrades, null),
      ("PUT", $"/api/hr/job-grades/{jobGrade}", HrPermissionNames.UpdateJobGrades,
        """{"code":"G7","name":"Renamed","rankOrder":70,"salaryGradeId":null,"expectedRowVersion":"AAAAAAAAB9E="}"""),
      ("POST", $"/api/hr/job-grades/{jobGrade}/activate", HrPermissionNames.DeactivateJobGrades, token),
      ("POST", $"/api/hr/job-grades/{jobGrade}/deactivate", HrPermissionNames.DeactivateJobGrades, token),

      ("POST", "/api/hr/salary-grades", HrPermissionNames.CreateSalaryGrades,
        """{"code":"S7","name":"Band 7","rankOrder":70,"minimumAmount":null,"midpointAmount":null,"maximumAmount":null}"""),
      ("GET", "/api/hr/salary-grades", HrPermissionNames.ViewSalaryGrades, null),
      ("GET", $"/api/hr/salary-grades/{salaryGrade}", HrPermissionNames.ViewSalaryGrades, null),
      ("PUT", $"/api/hr/salary-grades/{salaryGrade}", HrPermissionNames.UpdateSalaryGrades,
        """{"code":"S7","name":"Renamed","rankOrder":70,"minimumAmount":null,"midpointAmount":null,"maximumAmount":null,"expectedRowVersion":"AAAAAAAAB9E="}"""),
      ("POST", $"/api/hr/salary-grades/{salaryGrade}/activate", HrPermissionNames.DeactivateSalaryGrades, token),
      ("POST", $"/api/hr/salary-grades/{salaryGrade}/deactivate", HrPermissionNames.DeactivateSalaryGrades, token),

      ("POST", $"/api/hr/employees/{employee}/change-position", HrPermissionNames.UpdateEmployees,
        """{"positionId":"aaaaaaaa-0000-0000-0000-aaaaaaaaaaaa","expectedRowVersion":"AAAAAAAAB9E="}"""),
      ("GET", $"/api/hr/employees/{employee}/position-history", HrPermissionNames.ViewEmployees, null)
    };
  }

  // ================================================================================================
  // THE ROUTE AXIS CANNOT GO STALE EITHER (270)
  // ================================================================================================
  //
  // The three theories below derive their PERMISSION axis reflectively and their ROUTE axis from a
  // hand-written list. **That asymmetry was written down as a bound and left open:** a route added to the
  // product and to `HrRouteInventoryTests`' exact inventory but NOT to `AllRoutes` is pinned for its
  // pairing and never probed for bleed — it passes both guards while nothing checks it refuses a caller
  // holding every other permission. This closes it, and the bound above is now discharged rather than
  // merely stated.
  //
  // ⚠ THE COMPARISON NEEDS NO FAMILY FILTER, WHICH IS WHY IT LIVES HERE AND NOT BESIDE THE INVENTORY.
  // `HrRouteInventoryTests.MappedRoutes()` unions three harnesses and spans all 41 HR routes, so comparing
  // against it would need a hand-written "which of these are FP-008" predicate — the same staleness one
  // level up. **`host.MappedRoutes()` is the POSITION harness alone**, which maps exactly the four groups
  // this package adds. The population is the mechanism's own output.
  //
  // ⚠⚠ ONE CANONICALISER, APPLIED TO BOTH SIDES. The probed list carries concrete ids
  // (`/api/hr/positions/{a real guid}`) because it issues real requests; the mapped list carries patterns
  // (`/api/hr/positions/{positionId}`) and a trailing slash on group roots. Normalising only ONE side would
  // compare two shapes and fail on the difference rather than on the drift. Every parameter segment — a
  // brace pattern or a parsed `Guid` — collapses to `{}`, and empty segments are dropped.
  //
  // ⚠⚠⚠ AND THE CANONICALISER HAS AN INJECTIVITY CONTROL, BECAUSE IT IS THE FAILURE THIS TEST INVITES.
  // A normaliser that collapsed too much — every id AND every literal segment — would map both sides onto
  // a handful of identical strings and the two differences would be empty. **That is a green test over a
  // canonicaliser that destroyed its own subject.** Asserting the probed set stays as large as the route
  // table proves the collapse is injective HERE, which is the only place it needs to be.
  [Fact]
  [Trait("Criterion", "AC-POS-0068")]
  public void The_bleed_theory_probes_every_route_the_position_harness_maps()
  {
    var probed = Fp008Routes()
      .Select(route => $"{route.Method} {Canonical(route.Path)}")
      .ToArray();

    var mapped = host.MappedRoutes()
      .Select(route => $"{route.Method} {Canonical(route.Pattern)}")
      .ToArray();

    // ANTI-VACUITY. Both sides non-empty, and the canonicaliser proven injective over the probed set: if
    // it collapsed two distinct routes onto one string, the count drops and this fails before the
    // set comparison below can pass for the wrong reason.
    Assert.NotEmpty(mapped);
    Assert.Equal(Fp008Routes().Length, probed.Distinct(StringComparer.Ordinal).Count());

    // NAMED, NOT COUNTED. With twenty routes on each side, "the sets differ" sends the reader to diff two
    // lists by hand; the offending member is what they actually need.
    var unprobed = mapped.Except(probed, StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal);
    var stale = probed.Except(mapped, StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal);

    Assert.True(
      !unprobed.Any(),
      $"the harness maps these and the bleed theory never probes them: {string.Join(", ", unprobed)}. " +
      "A route reaching the product without reaching this list is pinned for its pairing and never " +
      "checked against a caller holding every other permission.");

    Assert.True(
      !stale.Any(),
      $"the bleed theory probes these and the harness maps nothing matching: {string.Join(", ", stale)}. " +
      "The route was renamed or removed and this list still claims to cover it.");
  }

  // Both sides through this, never one. A brace pattern and a concrete id are the same thing said twice.
  private static string Canonical(string path) =>
    string.Join('/', path
      .Split('/', StringSplitOptions.RemoveEmptyEntries)
      .Select(segment =>
        segment.StartsWith('{') || Guid.TryParse(segment, out _) ? "{}" : segment));

  // ---- EVERY ROUTE REFUSES AN UNAUTHENTICATED CALLER.
  [Theory]
  [MemberData(nameof(AllRoutes))]
  public async Task Every_route_refuses_an_unauthenticated_caller(
    string method, string path, string permission, string? body)
  {
    _ = permission;

    using var response = await host.Client.SendAsync(
      PositionApiTestHost.Request(new HttpMethod(method), path, token: null, body));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---- EVERY ROUTE REFUSES A CALLER HOLDING NO HR PERMISSION AT ALL.
  [Theory]
  [MemberData(nameof(AllRoutes))]
  public async Task Every_route_refuses_a_caller_without_its_permission(
    string method, string path, string permission, string? body)
  {
    _ = permission;

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      new HttpMethod(method), path, host.TokenWith(), body));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AND EVERY ROUTE REFUSES A CALLER HOLDING A DIFFERENT HR PERMISSION.
  //
  // The bleed test. A caller holding every HR permission EXCEPT the one this route demands must still be
  // refused — which is what catches a route wired to the wrong constant, the defect no happy-path test
  // notices because the happy path grants everything.
  //
  // ⚠ CITED BY 269 FOR TWO CRITERIA, AND THE DERIVED POPULATION IS WHY.
  //
  // `AC-POS-0041` — *each permission authorizes exactly its own operations AND NO OTHER; a caller holding
  // `HR.Positions.View` cannot create, update or deactivate.* The "no other" half is the hard one, and it
  // is carried here by SUBTRACTION: `AllHrPermissions()` reads every constant off `HrPermissionNames`
  // reflectively and this grants all of them EXCEPT the one the route demands. A per-permission assertion
  // could not do this — it would have to enumerate the permissions that must NOT work, which is the open
  // set, and it would go stale the day a permission is added. This cannot.
  //
  // `AC-POS-0042` — *permission bleed proven in BOTH directions.* Both are here because `AllRoutes` carries
  // the employee-prefix routes too: a POSITION route granted every other permission (which includes every
  // `HR.Employees.*`) is refused, AND `change-position` granted every other permission (which includes
  // every `HR.Positions.*`) is refused. One theory, both directions, neither by a separate hand-written case.
  //
  // ⚠⚠ THE BOUND, STATED SO IT IS NOT MISTAKEN FOR MORE: the PERMISSION axis is derived and cannot go
  // stale; the ROUTE axis is the hand-written `AllRoutes` list. A route added to the product and to
  // `HrRouteInventoryTests`' exact inventory but NOT to `AllRoutes` is pinned for its pairing and never
  // probed for bleed. The inventory makes a new route visible; it does not add it here.
  [Theory]
  [MemberData(nameof(AllRoutes))]
  [Trait("Criterion", "AC-POS-0041")]
  [Trait("Criterion", "AC-POS-0042")]
  public async Task Every_route_refuses_a_caller_holding_every_other_hr_permission(
    string method, string path, string permission, string? body)
  {
    var others = AllHrPermissions().Where(name => name != permission).ToArray();

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      new HttpMethod(method), path, host.TokenWith(others), body));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // THE PAY-BAND SEPARATION, IN BOTH DIRECTIONS (DEC-POS-0018)
  // ================================================================================================
  //
  // The route's `RequirePermission` is the first gate; the resolver refusing to mint a
  // `SalaryGradeReadScope` is the second. This harness runs the REAL resolver, so a 403 here means both
  // held rather than only the first.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  public async Task A_position_viewer_cannot_read_a_salary_grade()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/salary-grades/{PositionApiTestHost.SalaryGradeId}",
      host.TokenWith(HrPermissionNames.ViewPositions, HrPermissionNames.ViewJobGrades)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AND THE OTHER DIRECTION: the pay-band permission grants no position read.
  //
  // A separation that only worked one way would still leak — the point is that the two are independent,
  // not that one is stronger.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  public async Task A_salary_grade_viewer_cannot_read_a_position()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/positions/{PositionApiTestHost.PositionId}",
      host.TokenWith(HrPermissionNames.ViewSalaryGrades)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // READS
  // ================================================================================================

  [Fact]
  public async Task An_authorized_position_read_succeeds_and_carries_the_grade_block()
  {
    host.PositionReads.Detail = new PositionDetail(
      PositionApiTestHost.PositionId,
      PositionApiTestHost.CompanyA,
      "ACC-SR",
      "Senior Accountant",
      PositionApiTestHost.JobGradeId,
      new PositionJobGradeSummary(PositionApiTestHost.JobGradeId, "G7", "Grade 7", 70),
      PositionStatus.Active,
      RowVersion);

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/positions/{PositionApiTestHost.PositionId}",
      host.TokenWith(HrPermissionNames.ViewPositions, HrPermissionNames.ViewEmployees)));

    Assert.Equal(
      HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await PositionApiTestHost.BodyAsync(response));
    var root = document.RootElement;

    Assert.Equal("ACC-SR", root.GetProperty("code").GetString());
    Assert.Equal("Senior Accountant", root.GetProperty("title").GetString());
    Assert.Equal("Active", root.GetProperty("status").GetString());
    Assert.Equal("G7", root.GetProperty("jobGrade").GetProperty("code").GetString());
    Assert.Equal(70, root.GetProperty("jobGrade").GetProperty("rankOrder").GetInt32());
  }

  // ---- A POSITION OUTSIDE THE SCOPE IS 404, NOT 403 (BR-PLT-0002).
  //
  // A distinct refusal would confirm the position exists in a company the caller may not see. The stub
  // returns `PositionNotFound` for any identifier it does not hold, which is what the read handler produces
  // for unknown, other-tenant, other-company and out-of-scope alike.
  [Fact]
  [Trait("Rule", "BRULE-POS-0002")]
  public async Task A_position_outside_the_scope_is_not_found()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/positions/{PositionApiTestHost.UnknownId}",
      host.TokenWith(HrPermissionNames.ViewPositions)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("position.not_found", await PositionApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A_job_grade_outside_the_scope_is_not_found()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/job-grades/{PositionApiTestHost.UnknownId}",
      host.TokenWith(HrPermissionNames.ViewJobGrades)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("job_grade.not_found", await PositionApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A_salary_grade_outside_the_scope_is_not_found()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/salary-grades/{PositionApiTestHost.UnknownId}",
      host.TokenWith(HrPermissionNames.ViewSalaryGrades)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("salary_grade.not_found", await PositionApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // THE TWO COMPOSED FIELDS (DEC-POS-0034, DEC-POS-0035)
  // ================================================================================================

  // ---- WITH AN EMPLOYEE SCOPE, THE COUNT IS A NUMBER.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  // ⚠ CITED BY 269: `AC-POS-0066`'s PERMITTED half — *`employeeCount` is a NUMBER for a caller holding
  // `HR.Employees.View`.* `TS-POS-0070` names both halves and requires them asserted SEPARATELY, which is
  // what these two tests are. Without this one, `null` everywhere satisfies the criterion's letter while
  // making the field useless — the same degenerate case `AC-POS-0045`'s converse leg closes.
  [Trait("Criterion", "AC-POS-0066")]
  public async Task EmployeeCount_is_a_number_for_a_caller_who_can_read_employees()
  {
    host.PositionReads.Detail = ActivePosition();
    host.EmployeeReads.PositionHolderCount = 12;

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/positions/{PositionApiTestHost.PositionId}",
      host.TokenWith(HrPermissionNames.ViewPositions, HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await PositionApiTestHost.BodyAsync(response));

    Assert.Equal(12, document.RootElement.GetProperty("employeeCount").GetInt32());
  }

  // ---- WITHOUT ONE, IT IS NULL — PRESENT AND NULL, NOT ABSENT.
  //
  // Both halves matter and are asserted separately: the property must EXIST so the JSON shape is stable
  // across callers, and its value must be null rather than 0 because 0 would be a lie about a position that
  // may well have holders.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  // ⚠ CITED BY 269: `AC-POS-0066`'s UNPERMITTED half, and it asserts both things the criterion distinguishes
  // — the field is PRESENT (`TryGetProperty`, with the failure message naming the reason: a response whose
  // SHAPE varies by permission) and its value is NULL rather than `0`.
  //
  // ⚠⚠ THE `null`-VERSUS-`0` DISTINCTION IS A DISCLOSURE RULE, NOT A NULLABILITY PREFERENCE. `0` asserts
  // *this position has no employees* — a statement about data the caller may not see, and FALSE whenever
  // the position is occupied, which the arrangement here makes concrete by setting the real holder count
  // to 12 before asserting the caller sees `null`.
  [Trait("Criterion", "AC-POS-0066")]
  public async Task EmployeeCount_is_null_for_a_caller_who_cannot_read_employees()
  {
    host.PositionReads.Detail = ActivePosition();
    host.EmployeeReads.PositionHolderCount = 12;

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/positions/{PositionApiTestHost.PositionId}",
      host.TokenWith(HrPermissionNames.ViewPositions)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await PositionApiTestHost.BodyAsync(response));

    Assert.True(
      document.RootElement.TryGetProperty("employeeCount", out var count),
      "the field must be present for every caller, or the JSON shape varies per caller");

    Assert.Equal(JsonValueKind.Null, count.ValueKind);
  }

  // ---- THE CURRENCY IS ECHOED FROM THE COMPANY, NOT STORED (DEC-POS-0015).
  [Fact]
  [Trait("Decision", "DEC-POS-0035")]
  // ⚠ CITED BY 269: `AC-POS-0067`'s ECHO clause — *`currencyCode` is echoed from the owning Company through
  // a module-facing contract.* `TS-POS-0071` maps this scenario to `AC-POS-0067` and `AC-POS-0022` together.
  //
  // The criterion's OTHER clause — *and `SSAS.HR.*` references no Platform assembly to obtain it; a build in
  // which `HR.API` can see `SSAS.Platform.Domain` fails regardless of what it reads* — is a build claim no
  // HTTP test can reach, and is
  // `PositionApplicationArchitectureTests.No_hr_assembly_references_a_platform_assembly`.
  [Trait("Criterion", "AC-POS-0067")]
  public async Task A_salary_grade_read_echoes_the_owning_companys_currency()
  {
    host.SalaryGradeReads.Detail = new SalaryGradeDetail(
      PositionApiTestHost.SalaryGradeId,
      PositionApiTestHost.CompanyA,
      "S7",
      "Band 7",
      70,
      12000m,
      15000m,
      18000m,
      SalaryGradeStatus.Active,
      RowVersion);

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/salary-grades/{PositionApiTestHost.SalaryGradeId}",
      host.TokenWith(HrPermissionNames.ViewSalaryGrades)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await PositionApiTestHost.BodyAsync(response));
    var root = document.RootElement;

    Assert.Equal(StubTenantCompanyCurrencyLookup.Code, root.GetProperty("currencyCode").GetString());
    Assert.Equal(12000m, root.GetProperty("minimumAmount").GetDecimal());
    Assert.Equal(18000m, root.GetProperty("maximumAmount").GetDecimal());
  }

  // ---- AND IT IS REJECTED ON WRITE (`AC-POS-0022`, second clause).
  //
  // Sending it is an undeclared field, so the strict reader answers 400 rather than ignoring it and
  // leaving the caller believing they set something.
  //
  // ⚠ CITED BY 269 ON THE SET, NOT ALONE. `AC-POS-0022` has two clauses and this test can only reach one:
  // the FIRST — *`tenant.SalaryGrades` has NO CURRENCY COLUMN* — is a schema claim carried by
  // `PositionSchemaSqlServerTests.No_position_table_stores_a_currency`. Neither citation is honest alone.
  //
  // ⚠⚠ AND THE ERROR CODE IS NOW ASSERTED, NOT ONLY THE STATUS (269). A bare `400` has another plausible
  // producer here — a missing or malformed required field — so the status alone did not discriminate the
  // undeclared-property refusal from an ordinary validation failure. Its department counterpart, `D5`,
  // asserted both from the start.
  [Fact]
  [Trait("Decision", "DEC-POS-0015")]
  [Trait("Criterion", "AC-POS-0022")]
  public async Task Sending_a_currency_code_on_a_salary_grade_write_is_rejected()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/salary-grades",
      host.TokenWith(HrPermissionNames.CreateSalaryGrades),
      """{"code":"S7","name":"Band 7","rankOrder":70,"currencyCode":"USD"}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await PositionApiTestHost.ProblemCodeAsync(response));
  }

  // ---- OWNERSHIP IS NOT ACCEPTED FROM THE BODY, AND THE REFUSAL WRITES NOTHING (`AC-POS-0002`, 274).
  //
  // *`TenantId` and `CompanyId` supplied in the request body are REFUSED, not honoured and not silently
  // ignored: rejected with `400 request.invalid`, and NO POSITION IS PRODUCED.* Both clauses are here, and
  // they are separate claims: a handler that refused the request AFTER handing the aggregate to the
  // repository would satisfy the status assertion and violate the criterion.
  //
  // The mechanism is `StrictRequestReader.ReadStrictJsonAsync` with an allowlist of exactly
  // `code / title / jobGradeId`; an undeclared property makes the whole request null, which becomes
  // `ApiErrors.RequestInvalid`. So the refusal is a TRANSPORT fact and no test below the endpoint can
  // reach it — the criterion was corrected to this wording precisely because the product REFUSES where the
  // draft said it IGNORES.
  //
  // ⚠⚠ THE CONTROL IS THE REASON THIS TEST IS SHAPED THIS WAY, AND IT RETRO-FITS ONE THAT WAS MISSING.
  // `Assert.Null(Added)` passes when the write was refused AND when the write could never have happened —
  // a wrong route, a refused permission, a stub nothing reaches. Before this test, `Added` was asserted
  // exactly ONCE in the whole of `API.Tests` and that assertion was `Assert.Null`, so NOTHING anywhere
  // proved the field can become non-null. The stub's own header calls it *how a test asserts that a refused
  // write wrote nothing* — a self-explaining comment over an uncontrolled instrument.
  //
  // The accepted create below is that control, and it runs in the same test against the same host, so the
  // null that follows means REFUSED rather than UNREACHABLE.
  [Theory]
  [InlineData("tenantId", "\"11111111-1111-1111-1111-111111111111\"")]
  [InlineData("companyId", "\"22222222-2222-2222-2222-222222222222\"")]
  [Trait("Criterion", "AC-POS-0002")]
  public async Task An_ownership_field_in_the_body_is_refused_and_produces_no_position(
    string field, string value)
  {
    host.PositionReads.Detail = ActivePosition();

    // THE CONTROL. The same route, the same permission, the same stub — everything but the offending field.
    //
    // ⚠ IT ASSERTS THE MECHANISM, NOT THE STATUS, AND THE REASON IS A HARNESS LIMIT WORTH KNOWING.
    // A create reads its own result back by the NEW position's id, and `StubPositionReads.GetAsync` answers
    // `PositionNotFound` for any id that is not the one seeded on `Detail` — an id no test can predict,
    // because the aggregate mints it. So a valid create here reaches `AddAsync`, writes, and THEN 500s on
    // the read-back. **This harness cannot express a fully successful position create at all**, which is
    // why none exists: the same shape as `AC-POS-0009`'s revocation, where a fixture made the test
    // unwritable rather than anyone forgetting to write it.
    //
    // `Added` becoming non-null is exactly the control this test needs — it proves the write path is
    // REACHED through this route with this permission — and the later 500 does not weaken it.
    using var accepted = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/positions",
      host.TokenWith(HrPermissionNames.CreatePositions),
      """{"code":"ACC-SR","title":"Senior Accountant","jobGradeId":null}"""));

    Assert.NotNull(host.PositionRepository.Added);

    host.PositionRepository.Reset();

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/positions",
      host.TokenWith(HrPermissionNames.CreatePositions),
      $$"""{"code":"ACC-SR","title":"Senior Accountant","jobGradeId":null,"{{field}}":{{value}}}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await PositionApiTestHost.ProblemCodeAsync(response));

    // AND NO POSITION IS PRODUCED — the criterion's second clause, meaningful because of the control above.
    Assert.Null(host.PositionRepository.Added);
  }

  // ================================================================================================
  // MAPPER ARMS
  // ================================================================================================

  // ---- THE BAND REFUSALS, ALL THREE, ONE WIRE CODE.
  [Theory]
  [InlineData("""{"code":"S7","name":"Band 7","rankOrder":70,"minimumAmount":100}""")]
  [InlineData("""{"code":"S7","name":"Band 7","rankOrder":70,"minimumAmount":300,"midpointAmount":200,"maximumAmount":100}""")]
  [InlineData("""{"code":"S7","name":"Band 7","rankOrder":70,"minimumAmount":-1,"midpointAmount":200,"maximumAmount":300}""")]
  [Trait("Decision", "DEC-POS-0027")]
  public async Task An_unusable_band_is_refused_as_amounts_invalid(string body)
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/salary-grades",
      host.TokenWith(HrPermissionNames.CreateSalaryGrades),
      body));

    Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    Assert.Equal("salary_grade.amounts_invalid", await PositionApiTestHost.ProblemCodeAsync(response));
  }

  // ---- A DUPLICATE CODE IS THE FAMILY'S OWN CONFLICT, NOT A SHARED ONE.
  [Fact]
  public async Task A_duplicate_position_code_is_a_position_code_conflict()
  {
    host.PositionRepository.CodeTaken = true;

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/positions",
      host.TokenWith(HrPermissionNames.CreatePositions),
      """{"code":"ACC-SR","title":"Senior Accountant","jobGradeId":null}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("position.code_conflict", await PositionApiTestHost.ProblemCodeAsync(response));

    // AND NOTHING WAS WRITTEN. A conflict that still handed the aggregate to the repository would be a
    // partial write the response denied.
    Assert.Null(host.PositionRepository.Added);
  }

  // ================================================================================================
  // ⚠⚠ `AC-POS-0047`'s PRE-CHECK HALF — A `[Fact]`, NOT A THEORY, AND THE REASON IS RECORDED IN `B20`
  // ================================================================================================
  //
  // The criterion is *every position and grade mutation refuses a stale `RowVersion` with `409`*. Gated, it
  // had only the STRUCTURAL half — `PositionApplicationArchitectureTests` asserts the commands CARRY a
  // version. **That a version is carried says nothing about it being CHECKED.** The behavioural half lived
  // only in `PositionApplicationSqlServerTests`, which the merge gate does not run.
  //
  // ---- ⚠⚠⚠ WHY THIS CANNOT BE A `[Theory]` OVER THE ROUTE TABLE.
  //
  // `Fp008Routes()` hardcodes `PositionApiTestHost.PositionId`, and the stub matches on identity
  // (`Existing?.Id == positionId`). `Entity<TId>.Id` is GET-ONLY, assigned once through a private
  // constructor — **there is no setter to place a chosen id through.** So the route must carry the id the
  // SEEDED aggregate actually has, which is known only at run time.
  //
  // **`[MemberData]`/`[InlineData]` are enumerated at DISCOVERY time, before `IAsyncLifetime` builds
  // anything — so a theory cannot see the seed.** ⚠ That is `B20`'s recorded amendment, and its conclusion
  // applies unchanged: **the obvious implementation is IMPOSSIBLE, not merely awkward, so the loop inside a
  // `[Fact]` is the CORRECT shape rather than a compromise.** The loop therefore collects offenders and
  // asserts the collection is empty — an `Assert` inside it would report only the first family.
  //
  // ---- WHAT IT COVERS, AND THE TWO THINGS IT DOES NOT.
  //
  // `PositionCommandHandlers:245` compares the supplied version against the repository's and fails with
  // `ConcurrencyConflict` before the aggregate is mutated. Its own comment: *"compared again by the database
  // on save — **this is the friendly error, not the rule**."* **This covers the friendly error only** — a
  // stubbed repository never commits, so the authoritative token is out of reach. Hence `pre_check`.
  //
  // ⚠⚠ **AND THE SEEDED `RowVersion` IS LOAD-BEARING, NOT SETUP NOISE.** A freshly created aggregate has
  // `RowVersion = []`, which differs from ANY eight-byte token — so without placing a known value this test
  // would pass because the seed is EMPTY rather than because the caller is STALE. **It would be green for
  // the wrong reason, and the matching-version control below is what proves it is not.**
  [Fact]
  [Trait("Criterion", "AC-POS-0047")]
  public async Task A_stale_rowversion_is_refused_at_the_handler_pre_check_on_every_family()
  {
    // ⚠ THE SEED COMES FROM `Reset()`, NOT FROM HERE. It used to be built in this test; once the host began
    // seeding every family for every test, a second construction would have been a copy of the three stamps
    // that could drift from the one the rest of the suite uses.
    // ⚠⚠ NAMED, NOT NULL-FORGIVEN. A bare `!` here produced an opaque `NullReferenceException` when the
    // seeding was removed — the stub's loud guard never fired, because this test dereferences the aggregate
    // BEFORE any request reaches the stub. **An unseeded run must say what is missing at the first place it
    // can, not at the first place it happens to crash.**
    var seeded = (
      Required(host.PositionRepository.Existing, nameof(Position)),
      Required(host.JobGradeRepository.Existing, nameof(JobGrade)),
      Required(host.SalaryGradeRepository.Existing, nameof(SalaryGrade)));
    var offenders = new List<string>();

    foreach (var (label, route, permission, body) in UpdateRequests(seeded, StaleRowVersion))
    {
      using var response = await host.Client.SendAsync(
        PositionApiTestHost.Request(HttpMethod.Put, route, host.TokenWith(permission), body));

      if (response.StatusCode != HttpStatusCode.Conflict)
      {
        offenders.Add($"{label}: stale version answered {(int)response.StatusCode}, expected 409");
      }
    }

    // ⚠ THE CONTROL, IN THE SAME TEST. The identical requests carrying the version the seed actually holds
    // must NOT conflict — otherwise this asserts *the handler refuses everything* and cannot tell that from
    // *the handler refuses stale versions*.
    foreach (var (label, route, permission, body) in UpdateRequests(seeded, CurrentRowVersion))
    {
      using var response = await host.Client.SendAsync(
        PositionApiTestHost.Request(HttpMethod.Put, route, host.TokenWith(permission), body));

      if (response.StatusCode == HttpStatusCode.Conflict)
      {
        offenders.Add($"{label}: the CURRENT version also conflicted — the refusal is not about staleness");
      }
    }

    Assert.Empty(offenders);
  }

  private const string CurrentRowVersion = "AAAAAAAAB9E=";
  private const string StaleRowVersion = "AAAAAAAAAAA=";

  private static T Required<T>(T? seeded, string aggregate)
    where T : class =>
    seeded ?? throw new InvalidOperationException(
      $"`PositionApiTestHost.Reset()` did not seed a {aggregate}. Every route in this file would answer 404 " +
      "and the tests that only distinguish 403 from not-403 would still pass.");

  private static (string Label, string Route, string Permission, string Body)[] UpdateRequests(
    (Position Position, JobGrade JobGrade, SalaryGrade SalaryGrade) seeded, string version) =>
  [
    ("position", $"/api/hr/positions/{seeded.Position.Id}", HrPermissionNames.UpdatePositions,
      $$"""{"code":"ACC-SR","title":"Renamed","jobGradeId":null,"expectedRowVersion":"{{version}}"}"""),
    ("job grade", $"/api/hr/job-grades/{seeded.JobGrade.Id}", HrPermissionNames.UpdateJobGrades,
      $$"""{"code":"G7","name":"Renamed","rankOrder":70,"salaryGradeId":null,"expectedRowVersion":"{{version}}"}"""),
    ("salary grade", $"/api/hr/salary-grades/{seeded.SalaryGrade.Id}", HrPermissionNames.UpdateSalaryGrades,
      $$"""{"code":"S7","name":"Renamed","rankOrder":70,"minimumAmount":null,"midpointAmount":null,"maximumAmount":null,"expectedRowVersion":"{{version}}"}""")
  ];

  [Fact]
  // ⚠ CITED BY 269: `AC-POS-0061`'s WIRE half. The name states the criterion and the assertion is on the
  // CODE, not the status — a 409 alone cannot distinguish the two indexes, which is the whole criterion.
  // The application half is
  // `PositionApplicationSqlServerTests.A_grade_code_conflict_and_a_rank_conflict_are_distinguishable`.
  [Trait("Criterion", "AC-POS-0061")]
  public async Task A_duplicate_job_grade_rank_is_a_rank_conflict_not_a_code_conflict()
  {
    host.JobGradeRepository.RankTaken = true;

    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/job-grades",
      host.TokenWith(HrPermissionNames.CreateJobGrades),
      """{"code":"G7","name":"Grade 7","rankOrder":70,"salaryGradeId":null}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

    // The two unique indexes are NOT interchangeable — `api-contracts.md` records this as the case the
    // department precedent did not cover, and this is the assertion that makes the distinction real.
    Assert.Equal("job_grade.rank_conflict", await PositionApiTestHost.ProblemCodeAsync(response));
  }

  // ---- AN UNDECLARED FIELD IS A 400, ON EVERY WRITE.
  [Fact]
  public async Task A_create_rejects_an_undeclared_field()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Post,
      "/api/hr/positions",
      host.TokenWith(HrPermissionNames.CreatePositions),
      """{"code":"ACC-SR","title":"Senior Accountant","branchId":"44444444-4444-4444-4444-444444444444"}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ---- AND AN UPDATE CANNOT EXPRESS A LIFECYCLE CHANGE.
  //
  // `status` is not on the declared set, so sending it is a 400 rather than a silently ignored field — the
  // structural half of `DEC-POS-0011`'s "status has its own operation".
  [Fact]
  [Trait("Decision", "DEC-POS-0011")]
  public async Task An_update_cannot_express_a_status_change()
  {
    using var response = await host.Client.SendAsync(PositionApiTestHost.Request(
      HttpMethod.Put,
      $"/api/hr/positions/{PositionApiTestHost.PositionId}",
      host.TokenWith(HrPermissionNames.UpdatePositions),
      """{"code":"ACC-SR","title":"Renamed","status":"Inactive","expectedRowVersion":"AAAAAAAAB9E="}"""));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // A SQL Server rowversion is exactly eight bytes, and `RowVersionCodec.Encode` enforces it. Seeding a
  // shorter array made every read 500 rather than fail a decode — worth the named constant so the next
  // seeded aggregate cannot repeat it.
  private static readonly byte[] RowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

  private static PositionDetail ActivePosition() => new(
    PositionApiTestHost.PositionId,
    PositionApiTestHost.CompanyA,
    "ACC-SR",
    "Senior Accountant",
    null,
    null,
    PositionStatus.Active,
    RowVersion);

  // Every HR permission the product defines, read from the single source rather than listed — so a
  // permission added later is included in the bleed theory without anyone remembering to add it.
  private static string[] AllHrPermissions() =>
    typeof(HrPermissionNames)
      .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
      .Where(field => field.IsLiteral && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();
}

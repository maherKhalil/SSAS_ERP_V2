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
  public static TheoryData<string, string, string, string?> AllRoutes()
  {
    var position = PositionApiTestHost.PositionId;
    var jobGrade = PositionApiTestHost.JobGradeId;
    var salaryGrade = PositionApiTestHost.SalaryGradeId;
    var employee = PositionApiTestHost.EmployeeId;

    const string token = """{"expectedRowVersion":"AAAAAAAAB9E="}""";

    return new TheoryData<string, string, string, string?>
    {
      { "POST", "/api/hr/positions", HrPermissionNames.CreatePositions,
        """{"code":"ACC-SR","title":"Senior Accountant","jobGradeId":null}""" },
      { "GET", "/api/hr/positions", HrPermissionNames.ViewPositions, null },
      { "GET", $"/api/hr/positions/{position}", HrPermissionNames.ViewPositions, null },
      { "PUT", $"/api/hr/positions/{position}", HrPermissionNames.UpdatePositions,
        """{"code":"ACC-SR","title":"Renamed","jobGradeId":null,"expectedRowVersion":"AAAAAAAAB9E="}""" },
      { "POST", $"/api/hr/positions/{position}/activate", HrPermissionNames.DeactivatePositions, token },
      { "POST", $"/api/hr/positions/{position}/deactivate", HrPermissionNames.DeactivatePositions, token },

      { "POST", "/api/hr/job-grades", HrPermissionNames.CreateJobGrades,
        """{"code":"G7","name":"Grade 7","rankOrder":70,"salaryGradeId":null}""" },
      { "GET", "/api/hr/job-grades", HrPermissionNames.ViewJobGrades, null },
      { "GET", $"/api/hr/job-grades/{jobGrade}", HrPermissionNames.ViewJobGrades, null },
      { "PUT", $"/api/hr/job-grades/{jobGrade}", HrPermissionNames.UpdateJobGrades,
        """{"code":"G7","name":"Renamed","rankOrder":70,"salaryGradeId":null,"expectedRowVersion":"AAAAAAAAB9E="}""" },
      { "POST", $"/api/hr/job-grades/{jobGrade}/activate", HrPermissionNames.DeactivateJobGrades, token },
      { "POST", $"/api/hr/job-grades/{jobGrade}/deactivate", HrPermissionNames.DeactivateJobGrades, token },

      { "POST", "/api/hr/salary-grades", HrPermissionNames.CreateSalaryGrades,
        """{"code":"S7","name":"Band 7","rankOrder":70,"minimumAmount":null,"midpointAmount":null,"maximumAmount":null}""" },
      { "GET", "/api/hr/salary-grades", HrPermissionNames.ViewSalaryGrades, null },
      { "GET", $"/api/hr/salary-grades/{salaryGrade}", HrPermissionNames.ViewSalaryGrades, null },
      { "PUT", $"/api/hr/salary-grades/{salaryGrade}", HrPermissionNames.UpdateSalaryGrades,
        """{"code":"S7","name":"Renamed","rankOrder":70,"minimumAmount":null,"midpointAmount":null,"maximumAmount":null,"expectedRowVersion":"AAAAAAAAB9E="}""" },
      { "POST", $"/api/hr/salary-grades/{salaryGrade}/activate", HrPermissionNames.DeactivateSalaryGrades, token },
      { "POST", $"/api/hr/salary-grades/{salaryGrade}/deactivate", HrPermissionNames.DeactivateSalaryGrades, token },

      { "POST", $"/api/hr/employees/{employee}/change-position", HrPermissionNames.UpdateEmployees,
        """{"positionId":"aaaaaaaa-0000-0000-0000-aaaaaaaaaaaa","expectedRowVersion":"AAAAAAAAB9E="}""" },
      { "GET", $"/api/hr/employees/{employee}/position-history", HrPermissionNames.ViewEmployees, null }
    };
  }

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

using System.Net;
using System.Text.Json;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;
using SSAS.Platform.Domain;

namespace SSAS.API.Tests.Departments;

// ==================================================================================================
// THE DEPARTMENT HTTP CONTRACT, PROVEN OVER REAL HTTP (FP-007 Phase 4).
// ==================================================================================================
//
// These answer a question the application and SQL tests cannot: what a CALLER sees. The same refusal that
// Integration.Tests proves correct in the database has to arrive as the right status code and the right
// problem code — and for the two unique-constraint contexts, as a DIFFERENT code depending on which
// operation raised it, which is the defect this surface was created to fix.
[Collection(DepartmentApiEndpointGroup.Name)]
public sealed class DepartmentEndpointTests : IClassFixture<DepartmentApiTestHost>
{
  private const string Route = "/api/hr/departments";

  private readonly DepartmentApiTestHost host;

  public DepartmentEndpointTests(DepartmentApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ================================================================================================
  // CREATE
  // ================================================================================================

  [Fact]
  public async Task D1_Authorized_create_succeeds()
  {
    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task D2_Create_without_a_token_is_unauthorized()
  {
    var response = await Send(HttpMethod.Post, Route, token: null, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task D3_Create_without_the_create_permission_is_forbidden()
  {
    var response = await Send(HttpMethod.Post, Route, ViewToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- PERMISSION BLEED. Holding a DIFFERENT HR permission is not holding this one.
  //
  // The failure this guards against is a route wired to the wrong constant, or a permission check that
  // passes on "any HR permission". Employee permissions are the sharpest probe: they are in the same
  // module and the same token shape, so nothing about the request looks unusual.
  [Fact]
  public async Task D4_Create_with_an_unrelated_hr_permission_is_forbidden()
  {
    var token = host.TokenWith(
      HrPermissionNames.CreateEmployees,
      HrPermissionNames.UpdateEmployees,
      HrPermissionNames.TransferEmployees);

    var response = await Send(HttpMethod.Post, Route, token, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task D5_Create_rejects_an_undeclared_field()
  {
    const string body = """
      {"code":"FIN","name":"Finance","companyId":"22222222-2222-2222-2222-222222222222"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE FIRST OF THE TWO UNIQUE-CONSTRAINT CONTEXTS.
  //
  // On create, Persistence.UniqueConstraint means the unique index on NormalizedCode had the last word —
  // the same answer the pre-check gives, so a race and a sequential duplicate are indistinguishable.
  [Fact]
  public async Task D6_A_unique_constraint_violation_on_create_maps_to_a_code_conflict()
  {
    host.UnitOfWork.Failure = IdentityAccessErrors.UniqueConstraintViolation;

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.code_conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D7_A_duplicate_code_conflicts()
  {
    host.Repository.CodeExists = true;

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.code_conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // READ
  // ================================================================================================

  [Fact]
  public async Task D8_Authorized_get_succeeds()
  {
    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D9_Get_without_the_view_permission_is_forbidden()
  {
    var token = host.TokenWith(HrPermissionNames.CreateDepartments);

    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", token);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // Scoped absence. Unknown, another tenant's, another company's and out-of-scope are one answer.
  [Fact]
  public async Task D10_An_out_of_scope_department_is_not_found()
  {
    host.Reads.DetailError = DepartmentErrors.NotFound;

    var response = await Send(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", ViewToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("department.not_found", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D11_Search_succeeds()
  {
    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // An unparseable page size is a malformed request, not a reason to substitute a default.
  [Fact]
  public async Task D12_Search_with_an_unparseable_page_size_is_rejected()
  {
    var response = await Send(HttpMethod.Get, $"{Route}?pageSize=many", ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D13_Children_succeeds()
  {
    var response = await Send(
      HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}/children", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D14_Children_of_an_out_of_scope_department_is_not_found()
  {
    host.Reads.Children = null;

    var response = await Send(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}/children", ViewToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("department.not_found", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // UPDATE AND HIERARCHY
  // ================================================================================================

  [Fact]
  public async Task D15_Authorized_update_succeeds()
  {
    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", UpdateToken, ValidUpdateBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D16_Update_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken, ValidUpdateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // An ordinary update cannot express a hierarchy move: parentDepartmentId is not a declared field.
  [Fact]
  public async Task D17_Update_cannot_express_a_hierarchy_move()
  {
    const string body = """
      {"code":"FIN","name":"Finance","parentDepartmentId":"cccccccc-cccc-cccc-cccc-cccccccccccc","expectedRowVersion":"AAAAAAAAB9E="}
      """;

    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D18_An_invalid_row_version_is_rejected()
  {
    const string body = """
      {"code":"FIN","name":"Finance","expectedRowVersion":"not-base64"}
      """;

    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ---- MOVE AND MOVE-TO-ROOT ARE SEPARATE ROUTES.
  //
  // A null parent on the move route is NOT "become a root": that is the other route, and accepting null
  // here would make the most destructive reading of the field the quiet one.
  [Fact]
  public async Task D19_Move_requires_a_parent()
  {
    const string body = """{"parentDepartmentId":null,"expectedRowVersion":"AAAAAAAAB9E="}""";

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/move", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task D20_Move_to_root_succeeds()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/move-to-root", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D21_Move_to_root_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/move-to-root", ViewToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // MANAGER
  // ================================================================================================

  [Fact]
  public async Task D22_Assign_manager_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", ViewToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- THE SECOND UNIQUE-CONSTRAINT CONTEXT, AND THE POINT OF THE WHOLE MAPPER.
  //
  // The SAME persistence error that means "code already taken" on create means something else entirely
  // here: the only unique constraint on this route is PK_DepartmentManagers, so a violation means another
  // caller seated a manager first. Answering department.code_conflict — which is what routing this
  // through the employee mapper produced — would name a conflict on a field this request never sent.
  //
  // It must be indistinguishable from a stale rowversion, because both mean "somebody got there first".
  [Fact]
  public async Task D23_A_unique_constraint_violation_on_assign_manager_maps_to_a_concurrency_conflict()
  {
    host.UnitOfWork.Failure = IdentityAccessErrors.UniqueConstraintViolation;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", UpdateToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // And the rowversion loser gets the identical answer, which is what "indistinguishable" means.
  [Fact]
  public async Task D24_A_concurrency_conflict_on_assign_manager_maps_identically()
  {
    host.UnitOfWork.Failure = IdentityAccessErrors.ConcurrencyConflict;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", UpdateToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D25_Remove_manager_without_an_assignment_is_refused()
  {
    host.Repository.Manager = null;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager/remove", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.manager_invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // LIFECYCLE — BOTH DIRECTIONS CARRY THE DEACTIVATE PERMISSION
  // ================================================================================================

  [Fact]
  public async Task D26_Deactivate_succeeds_with_the_deactivate_permission()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/deactivate", DeactivateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ---- THE RULING, ENFORCED.
  //
  // Update authority is NOT enough to reopen a department. That permission governs whether a department
  // may receive employees, and a caller who may only rename one must not be able to undo a closure someone
  // with the sensitive permission deliberately made.
  [Fact]
  public async Task D27_Activate_is_forbidden_with_only_the_update_permission()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/activate", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task D28_Deactivate_is_forbidden_with_only_the_update_permission()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/deactivate", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task D29_Deactivating_a_department_with_active_children_is_refused()
  {
    host.Repository.HasActiveChildren = true;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/deactivate", DeactivateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.transition_invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // COMPANY CONTEXT
  // ================================================================================================

  // ---- THE HEADER'S SYNTAX IS THE CALLER'S PROBLEM; ITS SCOPE IS NOT THEIR BUSINESS.
  //
  // A missing or malformed X-Company-Id is a MALFORMED REQUEST: the caller can already see their own
  // header, so saying so discloses nothing, and a generic denial would send them hunting a permissions
  // problem they do not have.
  //
  // Driven through the establisher's error rather than by omitting the header, following the employee
  // suite's convention — the stub establisher does not read the header, and the REAL one's five-step
  // validation is proven against live state in Integration.Tests. What this layer owns, and what is
  // asserted here, is the answer that reaches the caller.
  // ---- ⚠ AND THE TWO CONDITIONS ANSWER DIFFERENTLY SINCE T-268, DELIBERATELY.
  //
  // Both arrive by the same route and look alike, which is why they are asserted side by side rather than
  // in separate tests. Only one of them is a **precondition**:
  //
  //   `Company.InvalidSelectionFormat` -> `request.invalid`
  //       The header is malformed. **Fix your input and try again** -- an ordinary validation failure,
  //       and one of the 129 domain codes that collapse into the generic code.
  //
  //   `Company.SelectionRequired`      -> `company.selection_required`
  //       The header is absent and no company is selected. **You are not in a state where this request
  //       means anything**: the remedy is a DIFFERENT call -- select a company -- and then this same
  //       request unchanged. A client that cannot tell it from a malformed field cannot offer the picker.
  //
  // Both stay 400. Each is a client error, and the actionable difference is carried by the code, because
  // **the status is the category and the code is the instruction.** A reader who finds this assertion
  // changed should be able to see from here that it was intended.
  [Theory]
  [InlineData("Company.SelectionRequired", "company.selection_required")]
  [InlineData("Company.InvalidSelectionFormat", "request.invalid")]
  public async Task D30_A_missing_company_selection_is_a_precondition_and_a_malformed_one_is_not(
    string code, string expectedWireCode)
  {
    host.CompanyContext.Error = new SSAS.BuildingBlocks.Domain.Error(code, "malformed");

    var response = await Send(HttpMethod.Get, Route, ViewToken);

    // Still 400 for both -- the category did not change, only the instruction.
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(expectedWireCode, await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D31_An_unauthorized_company_is_denied()
  {
    host.CompanyAccess.Permitted = [];
    host.CompanyContext.Error = new SSAS.BuildingBlocks.Domain.Error("Company.InvalidSelection", "denied");

    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("company.scope_denied", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE MANAGER'S THREE STATES SURVIVE THE WIRE.
  //
  // "Assigned but undisclosed" must not collapse into "no manager": the first says the caller may not see
  // who, the second says there is nobody. A department is company-visible while employees are
  // branch-scoped, so the distinction is reachable by ordinary callers.
  [Fact]
  public async Task D32_An_undisclosed_manager_is_reported_as_assigned_without_an_identity()
  {
    host.Reads.Detail = StubDepartmentReads.SampleDetail(
      manager: DepartmentManagerSummary.Undisclosed());

    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await DepartmentApiTestHost.BodyAsync(response);

    Assert.Contains("\"isAssigned\":true", body, StringComparison.Ordinal);
    Assert.Contains("\"employeeId\":null", body, StringComparison.Ordinal);
    Assert.DoesNotContain("\"fullName\":\"", body, StringComparison.Ordinal);
  }

  // ================================================================================================
  // employeeCount — THE FP-007 FIELD THAT NEVER SHIPPED (api-contracts, DEC-POS-0034)
  // ================================================================================================
  //
  // Specified by FP-007, absent from the implementation, and marked "matched" by FP-007's own as-built
  // pass. These four tests are what makes the claim checkable rather than asserted, and they are written
  // around the one distinction the field is easy to get wrong: ZERO AND NULL ARE DIFFERENT ANSWERS.
  //
  //   0    — the caller can read employees and this department has none they can see;
  //   null — the caller cannot read employees at all, so no number would be honest.
  //
  // Both are seeded to the SAME stub value where it matters, so a test passing by accident — because the
  // stub was never reached and returned its default — is not possible.

  // ---- WITH AN EMPLOYEE SCOPE, THE COUNT IS A NUMBER.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D33_EmployeeCount_is_a_number_for_a_caller_who_can_read_employees()
  {
    host.EmployeeReads.DepartmentMemberCount = 12;

    var response = await Send(
      HttpMethod.Get,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(HrPermissionNames.ViewDepartments, HrPermissionNames.ViewEmployees));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    Assert.Equal(12, document.RootElement.GetProperty("employeeCount").GetInt32());
  }

  // ---- ZERO IS A NUMBER, NOT AN ABSENCE.
  //
  // An empty department read by a caller who CAN read employees answers 0 — and the assertion checks the
  // JSON value KIND as well as the value, because `GetInt32()` on a null would throw rather than report the
  // difference this test exists to pin down.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D34_EmployeeCount_is_zero_for_an_empty_department_and_zero_is_not_null()
  {
    host.EmployeeReads.DepartmentMemberCount = 0;

    var response = await Send(
      HttpMethod.Get,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(HrPermissionNames.ViewDepartments, HrPermissionNames.ViewEmployees));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    var count = document.RootElement.GetProperty("employeeCount");

    Assert.Equal(JsonValueKind.Number, count.ValueKind);
    Assert.Equal(0, count.GetInt32());
  }

  // ---- WITHOUT ONE, IT IS NULL — PRESENT AND NULL, NOT ABSENT.
  //
  // Both halves are asserted separately: the property must EXIST so the JSON shape is stable across
  // callers, and its value must be null rather than 0. The stub is seeded to 12 precisely so a `0` here
  // would prove the count was taken when it should not have been, instead of looking like a correct empty
  // department.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D35_EmployeeCount_is_null_for_a_caller_who_cannot_read_employees()
  {
    host.EmployeeReads.DepartmentMemberCount = 12;

    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    Assert.True(
      document.RootElement.TryGetProperty("employeeCount", out var count),
      "the field must be present for every caller, or the JSON shape varies per caller");

    Assert.Equal(JsonValueKind.Null, count.ValueKind);
  }

  // ---- THE COUNT IS TAKEN UNDER THE CALLER'S OWN EMPLOYEE SCOPE, NOT A WIDER ONE.
  //
  // The count cannot be issued without an `EmployeeReadScope` — the interface makes that a compile error —
  // so what is left to prove at this layer is that the scope handed to the counter is the CALLER'S: their
  // authorized companies and their authorized branches, neither widened. Whether the SQL then honours that
  // scope is proven against a real database in `Integration.Tests`, because a stub cannot filter rows.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D36_EmployeeCount_is_counted_under_the_callers_own_employee_scope()
  {
    host.EmployeeReads.DepartmentMemberCount = 3;

    var response = await Send(
      HttpMethod.Get,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(HrPermissionNames.ViewDepartments, HrPermissionNames.ViewEmployees));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var scope = host.EmployeeReads.LastScope;

    Assert.NotNull(scope);
    Assert.Equal(DepartmentApiTestHost.TenantId, scope.TenantId);
    Assert.Equal(
      [DepartmentApiTestHost.CompanyA, DepartmentApiTestHost.CompanyB],
      scope.Companies.CompanyIds.OrderBy(id => id).ToArray());
    Assert.Equal([DepartmentApiTestHost.BranchA], scope.Branches.BranchIds.ToArray());
  }

  // ---- AND IT IS ON THE WRITE-BACK REPRESENTATION TOO.
  //
  // Every write reads the department back through the scoped path and returns the same shape a GET does.
  // Composing the count in only the read route would give one contract two shapes, so this asserts the
  // field survives an update — the cheapest probe that the composer sits on the shared path.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D37_A_write_back_carries_the_same_employeeCount_field()
  {
    host.EmployeeReads.DepartmentMemberCount = 7;

    var response = await Send(
      HttpMethod.Put,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(
        HrPermissionNames.UpdateDepartments,
        HrPermissionNames.ViewDepartments,
        HrPermissionNames.ViewEmployees),
      ValidUpdateBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    Assert.Equal(7, document.RootElement.GetProperty("employeeCount").GetInt32());
  }

  private const string ValidCreateBody = """
    {"code":"FIN","name":"Finance"}
    """;

  private const string ValidUpdateBody = """
    {"code":"FIN","name":"Finance Renamed","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private const string ValidRowVersionBody = """
    {"expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private const string ValidAssignManagerBody = """
    {"employeeId":"dddddddd-dddd-dddd-dddd-dddddddddddd","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private string ViewToken => host.TokenWith(HrPermissionNames.ViewDepartments);

  private string CreateToken =>
    host.TokenWith(HrPermissionNames.CreateDepartments, HrPermissionNames.ViewDepartments);

  private string UpdateToken =>
    host.TokenWith(HrPermissionNames.UpdateDepartments, HrPermissionNames.ViewDepartments);

  private string DeactivateToken =>
    host.TokenWith(HrPermissionNames.DeactivateDepartments, HrPermissionNames.ViewDepartments);

  private Task<HttpResponseMessage> Send(
    HttpMethod method, string path, string? token, string? body = null) =>
    host.Client.SendAsync(DepartmentApiTestHost.Request(method, path, token, body));
}
